using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The Director's traps (prototype 3, 2026-09-25): a tower of boxes
    /// standing beside a doorway, waiting, or (2026-09-27) a stack of crates
    /// standing beside a lane. Nothing happens to it while the building is
    /// calm -- people walk past it to the toilet all day. Once the fire is
    /// lit, the first frightened person to run through the room it watches
    /// -- the corridor, for the archway; the stockroom, for the stack --
    /// brings it down a beat later (their own reaction lag, never the same
    /// tick): the owner's rule (2026-09-27), "once people start running down
    /// the corridor".
    /// <para>
    /// The fall is the physics engine's (2026-09-27, the owner's rule: "the
    /// toppled boxes should be normal physics objects"): each box is let go
    /// of and shoved toward its own slot across the doorway, and where it
    /// lands is where it lands. While enough of them lie still in the
    /// doorway's strip (<see cref="TrapSettings.PileHoldsAtBoxes"/>) for
    /// half a second, the doorway is shut for people and fire, as a doorway
    /// with things wedged in it already is: its plug keeps bodies out,
    /// routes go round it, the fire cannot cross it, and at it people do what
    /// they do at any wedged door -- whoever can lift a box carries or
    /// throws it clear, the strong shove, and everybody else gives up and
    /// goes round. Too few boxes left in the gap, carried off or kicked out
    /// or burnt, and the way is open again; kicked back in, and it shuts
    /// again. A run where the boxes bounce wide leaves the way open, and
    /// that is the run.
    /// </para>
    /// <para>
    /// A trap across a lane has no doorway to shut: its crates fall along
    /// the line the trap names and block the lane by their weight alone --
    /// and, being too heavy to carry, they go on the map people steer by
    /// once they have settled, so routes go the other way.
    /// </para>
    /// <para>
    /// The standing tower's boxes are pinned so the crowd cannot shove it
    /// over early; from the fall on they are ordinary boxes, pinned by
    /// nothing.
    /// </para>
    /// Phase 1½, from the Director; it reads the fire and the crowd only.
    /// </summary>
    internal sealed class TrapSystem
    {
        private enum TrapPhase
        {
            /// <summary>The tower stands, pinned, waiting for the fire and a runner.</summary>
            Standing,

            /// <summary>Sprung: the boxes come down at <c>fallTick</c>.</summary>
            Falling,

            /// <summary>The boxes are loose; the doorway is shut while enough of them lie in it.</summary>
            Fallen,

            /// <summary>No boxes or no doorway in this building: nothing to fall.</summary>
            Inert
        }

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly PhysicsObjectSystem objects;
        private readonly FlammablesSystem flammables;
        private readonly SoundSystem sound;
        private readonly TrapSettings settings;

        private readonly TrapDefinition[] traps;

        /// <summary>The doorway each trap falls across, or -1 for a trap across a lane.</summary>
        private readonly int[] doorOf;

        /// <summary>The room each trap watches for a runner.</summary>
        private readonly int[] triggerRoom;
        private readonly int[][] boxesOf;
        private readonly TrapPhase[] phase;
        private readonly int[] fallTick;
        private readonly ulong[] triggerEventId;
        private readonly ulong[] fellEventId;
        private readonly ulong[] heapEventId;

        /// <summary>Ticks in a row that enough boxes have lain still in the doorway, while it is not yet shut.</summary>
        private readonly int[] settledTicks;

        public TrapSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry, DoorSystem doors,
            PhysicsObjectSystem objects, FlammablesSystem flammables, SoundSystem sound)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.objects = objects;
            this.flammables = flammables;
            this.sound = sound;
            settings = context.Scenario.Traps;
            traps = context.Scenario.TrapDefinitions ?? Array.Empty<TrapDefinition>();
            doorOf = new int[traps.Length];
            triggerRoom = new int[traps.Length];
            boxesOf = new int[traps.Length][];
            phase = new TrapPhase[traps.Length];
            fallTick = new int[traps.Length];
            triggerEventId = new ulong[traps.Length];
            fellEventId = new ulong[traps.Length];
            heapEventId = new ulong[traps.Length];
            settledTicks = new int[traps.Length];
            for (int t = 0; t < traps.Length; t++)
            {
                doorOf[t] = traps[t].IsDoorTrap ? doors.IndexOf(traps[t].DoorId) : -1;
                triggerRoom[t] = doorOf[t] >= 0 ? geometry.DoorRoom(doorOf[t]) : RoomIndexOf(traps[t].TriggerRoomId);

                // Only the boxes that are actually in the building: a test
                // that empties the floor of loose things has emptied the
                // tower too, and a trap with no boxes or no doorway is inert.
                var present = new System.Collections.Generic.List<int>();
                SimulationId[] ids = traps[t].BoxIds;
                for (int b = 0; b < ids.Length; b++)
                {
                    int box = objects.IndexOf(ids[b]);
                    if (box >= 0)
                    {
                        present.Add(box);
                    }
                }

                boxesOf[t] = present.ToArray();
                if ((traps[t].IsDoorTrap && doorOf[t] < 0) || triggerRoom[t] < 0 || boxesOf[t].Length == 0)
                {
                    phase[t] = TrapPhase.Inert;
                    continue;
                }

                for (int b = 0; b < boxesOf[t].Length; b++)
                {
                    // Standing, the tower is pinned and nobody may take from
                    // it: the crowd walking past it all day must not be the
                    // thing that brings it down, and neither may somebody
                    // tidying up or a strong runner barging past.
                    objects.Pin(boxesOf[t][b]);
                }
            }
        }

        public int Count => traps.Length;

        private int RoomIndexOf(SimulationId roomId)
        {
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (geometry.RoomId(r) == roomId)
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>Where a trap's boxes come down: the middle of its doorway, or of its line across the lane.</summary>
        private LogicalPosition Landing(int trap) =>
            doorOf[trap] >= 0 ? geometry.DoorCentre(doorOf[trap]) : traps[trap].LandingCentre;

        /// <summary>Whether this trap's boxes are lying across their doorway, shutting it -- or, for a trap across a lane, have come down at all.</summary>
        public bool IsFallen(int trap) =>
            phase[trap] == TrapPhase.Fallen && (doorOf[trap] < 0 || doors.IsPiled(doorOf[trap]));

        /// <summary>Whether this trap has been sprung, whether or not the boxes have landed yet.</summary>
        public bool IsSprung(int trap) => phase[trap] == TrapPhase.Falling || phase[trap] == TrapPhase.Fallen;

        /// <summary>
        /// The tick the most recent tower came down, or -1 while none has:
        /// the Director measures the socket's wait from it (2026-09-27).
        /// </summary>
        public int LatestFallTick
        {
            get
            {
                int latest = -1;
                for (int t = 0; t < traps.Length; t++)
                {
                    if (phase[t] == TrapPhase.Fallen)
                    {
                        latest = Math.Max(latest, fallTick[t]);
                    }
                }

                return latest;
            }
        }

        /// <summary>How many of this trap's boxes are lying still and unburnt in its doorway right now; none for a trap across a lane.</summary>
        public int BoxesInTheDoorway(int trap)
        {
            int count = 0;
            if (doorOf[trap] < 0)
            {
                return 0;
            }

            int[] boxes = boxesOf[trap];
            for (int b = 0; b < boxes.Length; b++)
            {
                if (IsInTheGap(doorOf[trap], boxes[b], out bool still) && still)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Every trap, in authored order: armed by the Director once the fire
        /// is lit (a bin smouldering counts: the owner's rule, 2026-09-27, is
        /// that the boxes come down when people start running, whatever the
        /// fire is doing), sprung by the first frightened runner, landing a
        /// beat later. <paramref name="cause"/> is what armed it, for the
        /// trigger to name.
        /// </summary>
        public void Advance(bool armed, ulong cause)
        {
            for (int t = 0; t < traps.Length; t++)
            {
                switch (phase[t])
                {
                    case TrapPhase.Standing:
                        if (armed)
                        {
                            Watch(t, cause);
                        }

                        break;
                    case TrapPhase.Falling:
                        if (context.Tick >= fallTick[t])
                        {
                            Fall(t);
                        }

                        break;
                    case TrapPhase.Fallen:
                        if (doorOf[t] >= 0)
                        {
                            KeepTheHeap(t);
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// The first frightened person running through the room the trap
        /// watches springs it: on their feet, scared, and moving at
        /// <see cref="TrapSettings.TriggerSpeedMillimetresPerTick"/> or
        /// more. Whichever way they are running: the owner's rule is that
        /// people running is what brings it down. Lowest index wins, so a
        /// replay names the same person; the fall lands their own reaction
        /// lag later, because nothing happens on the tick a thing is caused.
        /// </summary>
        private void Watch(int trap, ulong cause)
        {
            int room = triggerRoom[trap];
            int pace = settings.TriggerSpeedMillimetresPerTick;
            Agent runner = null;
            using (Crowd.Nearby near = crowd.Gather(geometry.RoomBounds(room)))
            {
                for (int c = 0; c < near.Count; c++)
                {
                    Agent agent = crowd.All[near[c]];
                    if (!agent.IsParticipating || agent.Fear.State != AgentFearState.Scared ||
                        agent.Body.State != AgentBodyState.Upright || agent.Body.Speed < pace ||
                        geometry.RoomAt(agent.Body.Position) != room)
                    {
                        continue;
                    }

                    if (runner == null || agent.Index < runner.Index)
                    {
                        runner = agent;
                    }
                }
            }

            if (runner == null)
            {
                return;
            }

            phase[trap] = TrapPhase.Falling;
            fallTick[trap] = context.ReactionTick();
            // Sprung because the fire was lit: that event is its cause, so
            // the story can trace the fallen boxes back to it.
            triggerEventId[trap] = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.TrapTriggered,
                Landing(trap), 0, 0, cause, runner.Id).EventId;
        }

        /// <summary>
        /// The tower comes down: every box is let go of and shoved toward
        /// its own slot across the gap, the crash is heard, and everybody
        /// within earshot thinks again about where they were going. Whoever
        /// is in the boxes' way is hit by them as by any thrown thing.
        /// </summary>
        private void Fall(int trap)
        {
            int door = doorOf[trap];
            LogicalPosition at = Landing(trap);
            ulong fell = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxTowerFell, at,
                0, 0, triggerEventId[trap], door >= 0 ? doors.IdOf(door) : default).EventId;
            fellEventId[trap] = fell;
            phase[trap] = TrapPhase.Fallen;
            settledTicks[trap] = 0;
            ToppleTheBoxes(trap, fell);
            sound.Crash(traps[trap].TrapId, at, settings.CrashSoundRadiusMillimetres, fell);
            TellEverybodyNear(at);
        }

        /// <summary>
        /// Each box is aimed at a slot in a row across the gap -- for a
        /// doorway, just inside the wall line on its own side (the
        /// corridor's, for the archway); for a lane, along the line the trap
        /// names -- each slot beside the last; when the row is full the rest
        /// aim at the same slots again and land on top, or wherever they
        /// bounce to. On the doorway's own side rather than beyond it because
        /// the fire that reaches them comes down the corridor, and flames
        /// only heat what is in their room. All let go of before any is
        /// shoved, or the upper boxes would still be held while the lower
        /// ones left.
        /// </summary>
        private void ToppleTheBoxes(int trap, ulong fell)
        {
            int[] boxes = boxesOf[trap];
            for (int b = 0; b < boxes.Length; b++)
            {
                objects.Unpin(boxes[b]);
            }

            int door = doorOf[trap];
            int width = door >= 0 ? doors.WidthOf(door) : traps[trap].LandingWidthMillimetres;
            int along = -width / 2;
            int placedInRow = 0;
            for (int b = 0; b < boxes.Length; b++)
            {
                int box = boxes[b];
                int size = objects.SizeOf(box);
                if (along + size > width / 2 && placedInRow > 0)
                {
                    along = -width / 2;
                    placedInRow = 0;
                }

                LogicalPosition slot = door >= 0
                    ? geometry.DoorPoint(door, along + size / 2, -settings.PileBeyondMillimetres)
                    : traps[trap].LandingCentre + IntegerMath.Displacement(traps[trap].LandingHeadingDegrees, along + size / 2);
                LogicalPosition from = objects.PositionOf(box);
                int heading = IntegerMath.HeadingBetween(from, slot, 0);
                objects.Topple(box, heading, SpeedToReach(box, IntegerMath.Distance(from, slot)), settings.ToppleLiftPercent,
                    settings.ToppleTumbles, fell);
                along += size;
                placedInRow++;
            }
        }

        /// <summary>
        /// The speed, in millimetres a tick, that lands a box this far away
        /// from as high as it stands, lifted by <see cref="TrapSettings.ToppleLiftPercent"/>
        /// of its own speed: from height h, distance d, lift L and the
        /// engine's gravity g, the throw lands when h + L·d = g·d²/(2v²), so
        /// v = d·√(g / 2(h + L·d)). A box on top of the stack is sent more
        /// gently than the one on the floor, because it has further to fall.
        /// Whole numbers throughout, so the fall replays.
        /// </summary>
        private int SpeedToReach(int box, long distance)
        {
            long gravity = 9810L * context.Scenario.PhysicsFeel.GravityPercent / 100L;
            long height = objects.BottomOf(box);
            long denominator = 2L * (height + settings.ToppleLiftPercent * distance / 100L);
            if (distance <= 0 || denominator <= 0)
            {
                return 0;
            }

            long perSecond = distance * IntegerMath.Sqrt(gravity * 1_000_000L / denominator) / 1000L;
            return (int)(perSecond / Run.TicksPerSecond * settings.ToppleSpeedPercent / 100L);
        }

        /// <summary>
        /// The way somebody was running for may just have shut. Everybody
        /// within earshot who is frightened decides again, each at their own
        /// reaction tick; the calm look toward the crash through the sound.
        /// </summary>
        private void TellEverybodyNear(LogicalPosition where)
        {
            long radius = settings.CrashSoundRadiusMillimetres;
            using Crowd.Nearby near = crowd.Within(where, radius);
            for (int c = 0; c < near.Count; c++)
            {
                Agent agent = crowd.All[near[c]];
                if (!agent.IsParticipating || agent.Fear.State == AgentFearState.Calm ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, where) > radius * radius)
                {
                    continue;
                }

                context.ThinkAgainSoon(agent.Intent);
            }
        }

        /// <summary>
        /// The heap is a fact about where the boxes lie, not a thing kept.
        /// While the doorway is open: once enough boxes have lain still in
        /// its strip, with nobody in the gap, for <see cref="TrapSettings.HeapSettleTicks"/>
        /// in a row, it shuts (<see cref="DoorSystem.PileInto"/>). While it is
        /// shut: once too few lie still there -- carried off, thrown clear,
        /// kicked out or burnt -- for as long again, it opens. A box kicked
        /// along the gap that comes to rest in it within that time opens
        /// nothing; one kicked back in later shuts it again.
        /// </summary>
        private void KeepTheHeap(int trap)
        {
            int door = doorOf[trap];
            int[] boxes = boxesOf[trap];
            int resting = 0;
            for (int b = 0; b < boxes.Length; b++)
            {
                if (IsInTheGap(door, boxes[b], out bool still) && still)
                {
                    resting++;
                }
            }

            bool piled = doors.IsPiled(door);
            bool enough = resting >= settings.PileHoldsAtBoxes;
            bool changing = piled ? !enough : enough && doors.NobodyInTheDoorway(door);
            settledTicks[trap] = changing ? settledTicks[trap] + 1 : 0;
            if (settledTicks[trap] < settings.HeapSettleTicks)
            {
                return;
            }

            settledTicks[trap] = 0;
            if (!piled)
            {
                doors.PileInto(door);
                heapEventId[trap] = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxHeapSettled,
                    geometry.DoorCentre(door), resting, 0, fellEventId[trap], doors.IdOf(door)).EventId;
                return;
            }

            doors.ClearPile(door);
            context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxPileCleared,
                geometry.DoorCentre(door), resting, 0, heapEventId[trap], doors.IdOf(door));
        }

        /// <summary>
        /// In the gap: in the doorway's heap strip (<see cref="TrapSettings.HeapGapMillimetres"/>
        /// either side of the wall line, wider than the strip that jams a
        /// door), in nobody's arms and not burnt out; <paramref name="still"/>
        /// when it has also come to rest.
        /// </summary>
        private bool IsInTheGap(int door, int box, out bool still)
        {
            still = false;
            if (objects.IsDormant(box) || objects.HolderOf(box) >= 0 ||
                flammables.ObjectState(box) == ObjectBurnState.Burnt)
            {
                return false;
            }

            if (!geometry.IsObjectInDoorway(door, objects.PositionOf(box), objects.RadiusOf(box), settings.HeapGapMillimetres))
            {
                return false;
            }

            still = !objects.IsMoving(box);
            return true;
        }
    }
}

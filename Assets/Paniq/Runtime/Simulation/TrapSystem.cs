using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The Director's traps (prototype 3, 2026-09-25): a tower of boxes
    /// standing beside a doorway, waiting. Nothing happens to it while the
    /// building is calm -- people walk past it to the toilet all day. Once
    /// the fire is lit, the first person to come within reach of it brings
    /// it down a beat later (their own reaction lag, never the same tick),
    /// and the boxes land wedged across the doorway.
    /// <para>
    /// The fallen tower is nothing new to the rest of the game: the doorway
    /// becomes a shut door with things wedged in it, which every doorway
    /// already knows how to be. Its plug keeps bodies out, routes stop
    /// going through it, the fire cannot cross it, and at it people do what
    /// they do at any wedged door -- whoever can lift a box throws it clear,
    /// the strong shove, and everybody else gives up and goes round. Each
    /// box is an ordinary box: carried off, thrown clear or burnt, it leaves
    /// the heap, and when fewer than <see cref="TrapSettings.PileHoldsAtBoxes"/>
    /// are left lying unburnt in the doorway the way is open again.
    /// </para>
    /// <para>
    /// The boxes are placed by this system rather than thrown by the physics
    /// engine, because the trap's whole point is that it blocks the way: a
    /// guaranteed wall of cardboard, drawn as a tumble by the presentation.
    /// While they stand and while they lie in the heap the boxes are pinned,
    /// so the crowd cannot shove the tower over early or kick the heap apart;
    /// picking one up or throwing it clear unpins it.
    /// </para>
    /// Phase 1½, from the Director; it reads the fire and the crowd only.
    /// </summary>
    internal sealed class TrapSystem
    {
        private enum TrapPhase
        {
            Standing,
            Falling,
            Fallen,
            Cleared
        }

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly DoorSystem doors;
        private readonly PhysicsObjectSystem objects;
        private readonly FlammablesSystem flammables;
        private readonly FireSystem fire;
        private readonly BodySystem body;
        private readonly SoundSystem sound;
        private readonly PeopleBodies people;
        private readonly TrapSettings settings;

        private readonly TrapDefinition[] traps;
        private readonly int[] doorOf;
        private readonly int[][] boxesOf;
        private readonly TrapPhase[] phase;
        private readonly int[] fallTick;
        private readonly ulong[] triggerEventId;
        private readonly ulong[] fellEventId;

        /// <summary>
        /// The middle of each standing tower: what "near the tower" is
        /// measured from. Worked out once, because a standing tower's boxes
        /// are pinned and cannot move until it falls.
        /// </summary>
        private readonly LogicalPosition[] centreOf;

        public TrapSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry, DoorSystem doors,
            PhysicsObjectSystem objects, FlammablesSystem flammables, FireSystem fire, BodySystem body, SoundSystem sound,
            PeopleBodies people)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.objects = objects;
            this.flammables = flammables;
            this.fire = fire;
            this.body = body;
            this.sound = sound;
            this.people = people;
            settings = context.Scenario.Traps;
            traps = context.Scenario.TrapDefinitions ?? Array.Empty<TrapDefinition>();
            doorOf = new int[traps.Length];
            boxesOf = new int[traps.Length][];
            phase = new TrapPhase[traps.Length];
            fallTick = new int[traps.Length];
            triggerEventId = new ulong[traps.Length];
            fellEventId = new ulong[traps.Length];
            centreOf = new LogicalPosition[traps.Length];
            for (int t = 0; t < traps.Length; t++)
            {
                doorOf[t] = doors.IndexOf(traps[t].DoorId);

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
                if (doorOf[t] < 0 || boxesOf[t].Length == 0)
                {
                    phase[t] = TrapPhase.Cleared;
                    continue;
                }

                long x = 0;
                long z = 0;
                for (int b = 0; b < boxesOf[t].Length; b++)
                {
                    // Standing, the tower is pinned and nobody may take from
                    // it: the crowd walking past it all day must not be the
                    // thing that brings it down, and neither may somebody
                    // tidying up or a strong runner barging past.
                    objects.Pin(boxesOf[t][b]);
                    LogicalPosition p = objects.PositionOf(boxesOf[t][b]);
                    x += p.X;
                    z += p.Z;
                }

                centreOf[t] = new LogicalPosition((int)(x / boxesOf[t].Length), (int)(z / boxesOf[t].Length));
            }
        }

        public int Count => traps.Length;

        /// <summary>Whether this trap's boxes are lying across their doorway, shutting it.</summary>
        public bool IsFallen(int trap) => phase[trap] == TrapPhase.Fallen;

        /// <summary>Whether this trap has been sprung, whether or not the boxes have landed yet.</summary>
        public bool IsSprung(int trap) => phase[trap] != TrapPhase.Standing;

        /// <summary>The middle of the standing tower's footprint: what "near the tower" is measured from.</summary>
        public LogicalPosition CentreOf(int trap) => centreOf[trap];

        /// <summary>How many of this trap's boxes are lying unburnt in its doorway right now.</summary>
        public int BoxesInTheDoorway(int trap)
        {
            int count = 0;
            int[] boxes = boxesOf[trap];
            for (int b = 0; b < boxes.Length; b++)
            {
                if (IsInTheHeap(doorOf[trap], boxes[b]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Every trap, in authored order: armed by the Director, sprung by the
        /// first person near, landing a beat later. Armed once the fire is lit
        /// -- or, with the Director climbing its ladder (2026-09-26), once a
        /// fire has got out of the room it started in, so a bin put out in the
        /// meeting room never brings the tower down. <paramref name="cause"/>
        /// is what armed it, for the trigger to name.
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
                        KeepTheHeap(t);
                        break;
                }
            }
        }

        /// <summary>
        /// The first participating person within reach springs it. Lowest
        /// index wins, so a replay names the same person; the fall lands
        /// their own reaction lag later, because nothing happens on the tick
        /// a thing is caused.
        /// </summary>
        private void Watch(int trap, ulong cause)
        {
            int radius = traps[trap].TriggerRadiusMillimetres > 0
                ? traps[trap].TriggerRadiusMillimetres
                : settings.TriggerRadiusMillimetres;
            LogicalPosition centre = CentreOf(trap);
            Agent nearest = null;
            using (Crowd.Nearby near = crowd.Within(centre, radius))
            {
                for (int c = 0; c < near.Count; c++)
                {
                    Agent agent = crowd.All[near[c]];
                    if (!agent.IsParticipating ||
                        LogicalPosition.DistanceSquared(agent.Body.Position, centre) > (long)radius * radius)
                    {
                        continue;
                    }

                    if (nearest == null || agent.Index < nearest.Index)
                    {
                        nearest = agent;
                    }
                }
            }

            if (nearest == null)
            {
                return;
            }

            phase[trap] = TrapPhase.Falling;
            fallTick[trap] = context.ReactionTick();
            // Sprung because the fire was lit, or got loose: that event is its
            // cause, so the story can trace the fallen boxes back to it.
            triggerEventId[trap] = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.TrapTriggered,
                centre, 0, 0, cause, nearest.Id).EventId;
        }

        /// <summary>
        /// The tower comes down: the doorway shuts, the boxes are laid in a
        /// row along it (a second row on top when there are more than fit),
        /// anybody standing in the doorway is knocked clear, the crash is
        /// heard, and everybody within earshot thinks again about where they
        /// were going.
        /// </summary>
        private void Fall(int trap)
        {
            int door = doorOf[trap];
            LogicalPosition doorCentre = geometry.DoorCentre(door);
            ulong fell = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxTowerFell, doorCentre,
                0, 0, triggerEventId[trap], doors.IdOf(door)).EventId;
            fellEventId[trap] = fell;
            phase[trap] = TrapPhase.Fallen;
            doors.PileInto(door);
            LayTheBoxes(trap, door, fell);
            KnockPeopleClear(door, fell);
            sound.Crash(traps[trap].TrapId, doorCentre, settings.CrashSoundRadiusMillimetres, fell);
            TellEverybodyNear(doorCentre);
        }

        /// <summary>
        /// The boxes in a row across the gap, just inside the wall line on
        /// the doorway's own side (the corridor's, for the archway), each
        /// box beside the last; when the row is full the rest go on top of
        /// it, in order. Pinned where they land. On the doorway's own side
        /// rather than beyond it because the fire that reaches them comes
        /// down the corridor, and flames only heat what is in their room:
        /// laid beyond the line the boxes would be a wall the fire could
        /// never burn.
        /// </summary>
        private void LayTheBoxes(int trap, int door, ulong fell)
        {
            int[] boxes = boxesOf[trap];
            int width = doors.WidthOf(door);
            int heading = geometry.DoorwayRunsAlongX(door) ? 90 : 0;
            int along = -width / 2;
            int row = 0;
            int rowBottom = 0;
            int nextRowBottom = 0;
            int placedInRow = 0;
            for (int b = 0; b < boxes.Length; b++)
            {
                int box = boxes[b];
                int size = objects.SizeOf(box);
                if (along + size > width / 2 && placedInRow > 0)
                {
                    // The row is full: start another on top of it.
                    row++;
                    rowBottom = nextRowBottom;
                    along = -width / 2;
                    placedInRow = 0;
                }

                LogicalPosition spot = geometry.DoorPoint(door, along + size / 2, -settings.PileBeyondMillimetres);
                // Let go, put down, and held again -- as part of the heap now,
                // which people may take from: that is how it is cleared.
                objects.Unpin(box);
                objects.PlaceAt(box, spot, rowBottom, heading, fell);
                objects.Pin(box, mayBeTaken: true);
                nextRowBottom = Math.Max(nextRowBottom, rowBottom + ObjectShapes.TopHeight(objects.KindOf(box), size));
                along += size;
                placedInRow++;
            }
        }

        /// <summary>
        /// Nobody can stand where the boxes now lie: anybody astride the wall
        /// line (where the doorway's plug now is) or under the boxes on the
        /// doorway's own side of it is shifted clear of them and knocked
        /// down, away from the wall line. Shifted first, because a body left
        /// inside a pinned box would be pushed anywhere the engine liked.
        /// </summary>
        private void KnockPeopleClear(int door, ulong fell)
        {
            int bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            int reach = settings.PileBeyondMillimetres + objects.WidestRadius;
            using Crowd.Nearby near = crowd.Gather(geometry.DoorwaySearchArea(door, bodyRadius, reach));
            for (int c = 0; c < near.Count; c++)
            {
                Agent agent = crowd.All[near[c]];
                if (!agent.IsParticipating)
                {
                    continue;
                }

                LogicalPosition at = agent.Body.Position;
                int side = geometry.SideOf(door, at);
                bool inTheWay = geometry.IsObjectInDoorway(door, at, bodyRadius, 0) ||
                                (side < 0 && geometry.IsObjectInDoorway(door, at, bodyRadius, reach));
                if (!inTheWay)
                {
                    continue;
                }

                // Clear of the boxes on their own side: past the plug on the
                // far side, past the row of boxes on the near side.
                int clear = side > 0 ? bodyRadius + 50 : reach + bodyRadius + 50;
                LogicalPosition spot = geometry.DoorPoint(door, (int)geometry.AlongOffset(door, at), side > 0 ? clear : -clear);
                people.ShiftTo(agent, spot);
                body.BlowOver(agent, geometry.OutwardHeading(door, side), settings.KnockClearMillimetres, 0, fell);
            }
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
        /// The heap is whatever boxes still lie unburnt in the doorway. A box
        /// carried off, thrown clear, kicked out or burnt is unpinned and no
        /// longer counts; a loose box that comes to rest in the doorway
        /// again is part of the heap again. Too few left, and the doorway is
        /// a way through once more.
        /// </summary>
        private void KeepTheHeap(int trap)
        {
            int door = doorOf[trap];
            int[] boxes = boxesOf[trap];
            int inTheHeap = 0;
            for (int b = 0; b < boxes.Length; b++)
            {
                int box = boxes[b];
                bool inHeap = IsInTheHeap(door, box);
                if (inHeap)
                {
                    inTheHeap++;
                    if (!objects.IsPinned(box) && objects.HolderOf(box) < 0 && !objects.IsMoving(box))
                    {
                        objects.Pin(box, mayBeTaken: true);
                    }
                }
                else if (objects.IsPinned(box))
                {
                    objects.Unpin(box);
                }
            }

            if (inTheHeap >= settings.PileHoldsAtBoxes)
            {
                return;
            }

            for (int b = 0; b < boxes.Length; b++)
            {
                if (objects.IsPinned(boxes[b]))
                {
                    objects.Unpin(boxes[b]);
                }
            }

            phase[trap] = TrapPhase.Cleared;
            doors.ClearPile(door);
            context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxPileCleared,
                geometry.DoorCentre(door), 0, 0, fellEventId[trap], doors.IdOf(door));
        }

        /// <summary>
        /// In the heap: lying still in the doorway, in nobody's arms, not
        /// burnt out, and not in the air. The same strip that makes any
        /// doorway count as wedged, so what the door calls jammed and what
        /// the trap calls the heap are one thing.
        /// </summary>
        private bool IsInTheHeap(int door, int box)
        {
            if (objects.IsDormant(box) || objects.HolderOf(box) >= 0 || objects.IsMoving(box) ||
                flammables.ObjectState(box) == ObjectBurnState.Burnt)
            {
                return false;
            }

            return geometry.IsObjectInDoorway(door, objects.PositionOf(box), objects.RadiusOf(box),
                context.Scenario.Blockades.BlockGapMillimetres);
        }
    }
}

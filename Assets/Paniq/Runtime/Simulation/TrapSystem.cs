using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The building's stacks (prototype 3, 2026-09-25): a tower of boxes
    /// standing beside a doorway, or (2026-09-27) a stack of crates standing
    /// beside a lane. Since 2026-10-02 a stack comes down for one reason
    /// only: somebody's body actually runs into it (the owner: "the boxes
    /// falling looks scripted. Keep it, but make it purely dynamic so if an
    /// agent actually bumps into it, it falls. No director trigger").
    /// <para>
    /// A bump is a new contact between a person and one of the stack's
    /// boxes with the person closing on the box at
    /// <see cref="TrapSettings.BumpSpeedMillimetresPerTick"/> or more: a
    /// running pace. Calm people walk slower than that, so the stack stands
    /// all day while people brush past it; anybody at a run brings it down,
    /// calm or frightened, shoved or under their own power, fire or no fire.
    /// Nothing arms it, nothing creaks, and the Director never touches it.
    /// </para>
    /// <para>
    /// The fall begins a reaction lag after the bump (never on its tick) and
    /// is the physics engine's (2026-09-27, the owner's rule: "the toppled
    /// boxes should be normal physics objects"): each box is let go of and
    /// thrown toward its own slot in a heap a stride ahead of the bumped
    /// stack, in the direction the bumper was going, a little harder for a
    /// faster or stronger bumper. Where the boxes land is where they land.
    /// While enough of a doorway stack's boxes lie still in the doorway's
    /// strip (<see cref="TrapSettings.PileHoldsAtBoxes"/>) for half a
    /// second, the doorway is shut for people and fire, as a doorway with
    /// things wedged in it already is; too few left in the gap, and the way
    /// is open again.
    /// </para>
    /// <para>
    /// The standing stack's boxes are pinned, so nothing short of a bump
    /// moves them and nobody takes one; from the fall on they are ordinary
    /// boxes, pinned by nothing.
    /// </para>
    /// Reads the engine's contacts after its step; the fall and the heap are
    /// kept in phase 1½.
    /// </summary>
    internal sealed class TrapSystem
    {
        private enum TrapPhase
        {
            /// <summary>The stack stands, pinned, until somebody runs into it.</summary>
            Standing,

            /// <summary>Bumped: the boxes come down at <c>fallTick</c>.</summary>
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
        private readonly PeopleBodies people;
        private readonly TrapSettings settings;

        private readonly TrapDefinition[] traps;

        /// <summary>The doorway each stack stands by, or -1 for a stack beside a lane.</summary>
        private readonly int[] doorOf;
        private readonly int[][] boxesOf;

        /// <summary>For every loose thing in the run: the stack it is a box of, or -1.</summary>
        private readonly int[] trapOfBox;
        private readonly TrapPhase[] phase;
        private readonly int[] fallTick;
        private readonly ulong[] triggerEventId;
        private readonly ulong[] fellEventId;
        private readonly ulong[] heapEventId;

        /// <summary>Ticks in a row that enough boxes have lain still in the doorway, while it is not yet shut.</summary>
        private readonly int[] settledTicks;

        /// <summary>Where the heap is aimed, which way the bumper was going, and how hard the boxes are thrown.</summary>
        private readonly LogicalPosition[] heapSpot;
        private readonly int[] heapHeading;
        private readonly int[] throwPercent;

        public TrapSystem(SimulationContext context, Crowd crowd, WorldGeometry geometry, DoorSystem doors,
            PhysicsObjectSystem objects, FlammablesSystem flammables, SoundSystem sound, PeopleBodies people)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.doors = doors;
            this.objects = objects;
            this.flammables = flammables;
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
            heapEventId = new ulong[traps.Length];
            settledTicks = new int[traps.Length];
            heapSpot = new LogicalPosition[traps.Length];
            heapHeading = new int[traps.Length];
            throwPercent = new int[traps.Length];
            trapOfBox = new int[objects.Count];
            for (int b = 0; b < trapOfBox.Length; b++)
            {
                trapOfBox[b] = -1;
            }

            for (int t = 0; t < traps.Length; t++)
            {
                doorOf[t] = traps[t].IsDoorTrap ? doors.IndexOf(traps[t].DoorId) : -1;

                // Only the boxes that are actually in the building: a test
                // that empties the floor of loose things has emptied the
                // stack too, and a stack with no boxes or no doorway is inert.
                var present = new List<int>();
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
                if ((traps[t].IsDoorTrap && doorOf[t] < 0) || boxesOf[t].Length == 0)
                {
                    phase[t] = TrapPhase.Inert;
                    continue;
                }

                for (int b = 0; b < boxesOf[t].Length; b++)
                {
                    // Standing, the stack is pinned and nobody may take from
                    // it: only a body running into it brings it down.
                    objects.Pin(boxesOf[t][b]);
                    trapOfBox[boxesOf[t][b]] = t;
                }
            }
        }

        public int Count => traps.Length;

        /// <summary>Whether this stack's boxes are lying across their doorway, shutting it -- or, for a stack beside a lane, have come down at all.</summary>
        public bool IsFallen(int trap) =>
            phase[trap] == TrapPhase.Fallen && (doorOf[trap] < 0 || doors.IsPiled(doorOf[trap]));

        public SimulationId IdOf(int trap) => traps[trap].TrapId;

        /// <summary>How many of this stack's boxes are lying still and unburnt in its doorway right now; none for a stack beside a lane.</summary>
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
        /// After the engine's step: every new contact between a person and a
        /// box of a standing stack. A body closing on the box at a running
        /// pace has knocked it; the stack comes down a reaction lag later.
        /// One knock a stack: the first in the engine's own contact order,
        /// which is the same in a replay. The one draw here is that lag.
        /// </summary>
        public void FeelTheBumps(IReadOnlyList<PhysicsWorld.Contact> contacts)
        {
            for (int c = 0; c < contacts.Count; c++)
            {
                PhysicsWorld.Contact contact = contacts[c];
                if (!contact.Began || contact.BodyA < 0 || contact.BodyA >= trapOfBox.Length || contact.BodyB < 0)
                {
                    continue;
                }

                int trap = trapOfBox[contact.BodyA];
                if (trap < 0 || phase[trap] != TrapPhase.Standing)
                {
                    continue;
                }

                Agent agent = people.PersonAt(contact.BodyB);
                if (agent == null || !agent.IsParticipating)
                {
                    continue;
                }

                // How fast they were closing on the box along the line
                // between them, as a thing meeting a person is judged: the
                // box is held still, so only the person's speed counts, and
                // somebody brushing past its corner is not running into it.
                LogicalPosition box = objects.PositionOf(contact.BodyA);
                long nx = (long)agent.Body.Position.X - box.X;
                long nz = (long)agent.Body.Position.Z - box.Z;
                long length = IntegerMath.Sqrt(nx * nx + nz * nz);
                if (length == 0L)
                {
                    continue;
                }

                (long vx, long vz) = people.VelocityOf(agent);
                int closing = (int)(-(vx * nx + vz * nz) / length / PhysicsWorld.SubMillimetre);
                if (closing < settings.BumpSpeedMillimetresPerTick)
                {
                    continue;
                }

                Knock(trap, contact.BodyA, agent, closing, IntegerMath.HeadingOf(vx, vz, agent.Body.Heading), contact.Point);
            }
        }

        /// <summary>
        /// Knocked: the heap is aimed a stride ahead of the bumped stack in
        /// the bumper's direction -- or, with a wall that way, a stride back
        /// toward the bumper -- thrown a little harder for a faster or
        /// stronger bumper, and comes down a reaction lag from now.
        /// </summary>
        private void Knock(int trap, int box, Agent bumper, int closing, int heading, LogicalPosition point)
        {
            LogicalPosition foot = objects.PositionOf(box);

            // A stack with a wall behind it has nowhere to go that way: hit
            // from the front it topples forward, back the way the bumper
            // came and on top of them. (The stockroom's stack stands
            // against its north wall: run into from the lane, it comes down
            // across the lane.)
            LogicalPosition ahead = foot + IntegerMath.Displacement(heading, settings.HeapAheadMillimetres);
            if (geometry.RoomAtPoint(ahead) != geometry.RoomAtPoint(foot))
            {
                heading = IntegerMath.NormalizeDegrees(heading + 180);
                ahead = foot + IntegerMath.Displacement(heading, settings.HeapAheadMillimetres);
            }

            heapHeading[trap] = heading;
            heapSpot[trap] = OnTheStacksOwnFloor(foot, ahead);
            int percent = settings.ToppleSpeedPercent +
                          (closing - settings.BumpSpeedMillimetresPerTick) / 10 * settings.BumpThrowPercentPerTenMillimetresPerTick +
                          (bumper.Traits.Strength - 5) * settings.BumpThrowPercentPerStrength;
            throwPercent[trap] = Math.Max(50, Math.Min(150, percent));
            triggerEventId[trap] = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.TrapTriggered,
                point, closing, 0, bumper.Fear.State == AgentFearState.Scared ? bumper.Fear.ScaredEventId : 0UL,
                bumper.Id).EventId;
            phase[trap] = TrapPhase.Falling;
            fallTick[trap] = checked(context.Tick + context.ReactionLag());
        }

        /// <summary>How far in from the wall line a box is aimed when its spot would be beyond the wall: a box's half-width and the wall's.</summary>
        private const int InFromTheWallMillimetres = 450;

        /// <summary>
        /// A spot to aim a box at, kept on the floor of the room the stack
        /// stands in: a stack knocked toward the wall behind it comes down
        /// against that wall, not through it into the room next door.
        /// </summary>
        private LogicalPosition OnTheStacksOwnFloor(LogicalPosition from, LogicalPosition spot)
        {
            int room = geometry.RoomAtPoint(from);
            if (room >= 0 && geometry.RoomAtPoint(spot) != room)
            {
                LogicalBounds floor = geometry.RoomBounds(room);
                spot = new LogicalPosition(
                    Math.Max(floor.MinX + InFromTheWallMillimetres, Math.Min(floor.MaxX - InFromTheWallMillimetres, spot.X)),
                    Math.Max(floor.MinZ + InFromTheWallMillimetres, Math.Min(floor.MaxZ - InFromTheWallMillimetres, spot.Z)));
            }

            return geometry.ClampIntoRoom(from, spot);
        }

        /// <summary>
        /// Every stack, in authored order: one that was knocked comes down
        /// when its beat is up, and a fallen one by a doorway keeps its heap.
        /// Nothing happens to a stack still standing: only a bump moves it.
        /// </summary>
        public void Advance()
        {
            for (int t = 0; t < traps.Length; t++)
            {
                switch (phase[t])
                {
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
        /// The stack comes down: every box is let go of and thrown toward
        /// its own slot in the heap, the crash is heard, and everybody
        /// within earshot thinks again about where they were going. Whoever
        /// is in the boxes' way is hit by them as by any thrown thing.
        /// </summary>
        private void Fall(int trap)
        {
            int door = doorOf[trap];
            LogicalPosition at = heapSpot[trap];
            ulong fell = context.Events.Append(context.Tick, traps[trap].TrapId, CausalEventType.BoxTowerFell, at,
                0, 0, triggerEventId[trap], door >= 0 ? doors.IdOf(door) : default).EventId;
            fellEventId[trap] = fell;
            phase[trap] = TrapPhase.Fallen;
            settledTicks[trap] = 0;
            ToppleAhead(trap, fell);
            sound.Crash(traps[trap].TrapId, at, settings.CrashSoundRadiusMillimetres, fell);
            TellEverybodyNear(at);
        }

        /// <summary>
        /// The boxes come down in a heap of rows across the bumper's way,
        /// <see cref="TrapSettings.HeapWidthMillimetres"/> wide, the rows one
        /// box deep and centred on the heap's spot, each slot kept on floor.
        /// All let go of before any is thrown, or the upper boxes would
        /// still be held while the lower ones left. Aimed, thrown by the
        /// engine, and where they land is where they land.
        /// </summary>
        private void ToppleAhead(int trap, ulong fell)
        {
            int[] boxes = boxesOf[trap];
            for (int b = 0; b < boxes.Length; b++)
            {
                objects.Unpin(boxes[b]);
            }

            LogicalPosition spot = heapSpot[trap];
            int ahead = heapHeading[trap];
            int across = IntegerMath.NormalizeDegrees(ahead + 90);
            int width = Math.Max(1, settings.HeapWidthMillimetres);
            int size = objects.SizeOf(boxes[0]);
            int perRow = Math.Max(1, width / Math.Max(1, size));
            int rows = (boxes.Length + perRow - 1) / perRow;
            for (int b = 0; b < boxes.Length; b++)
            {
                int box = boxes[b];
                int row = b / perRow;
                int inRow = b % perRow;
                int countInRow = Math.Min(perRow, boxes.Length - row * perRow);
                int along = (2 * inRow - (countInRow - 1)) * size / 2;
                int deep = (2 * row - (rows - 1)) * size / 2;
                LogicalPosition slot = spot + IntegerMath.Displacement(across, along) + IntegerMath.Displacement(ahead, deep);
                slot = OnTheStacksOwnFloor(spot, slot);
                LogicalPosition from = objects.PositionOf(box);
                int heading = IntegerMath.HeadingBetween(from, slot, ahead);
                objects.Topple(box, heading, SpeedToReach(box, IntegerMath.Distance(from, slot), throwPercent[trap]),
                    settings.ToppleLiftPercent, settings.ToppleTumbles, fell);
            }
        }

        /// <summary>
        /// The speed, in millimetres a tick, that lands a box this far away
        /// from as high as it stands, lifted by <see cref="TrapSettings.ToppleLiftPercent"/>
        /// of its own speed: from height h, distance d, lift L and the
        /// engine's gravity g, the throw lands when h + L·d = g·d²/(2v²), so
        /// v = d·√(g / 2(h + L·d)). A box on top of the stack is sent more
        /// gently than the one on the floor, because it has further to fall.
        /// Scaled by <paramref name="percent"/>: how hard the bump was.
        /// Whole numbers throughout, so the fall replays.
        /// </summary>
        private int SpeedToReach(int box, long distance, int percent)
        {
            long gravity = 9810L * context.Scenario.PhysicsFeel.GravityPercent / 100L;
            long height = objects.BottomOf(box);
            long denominator = 2L * (height + settings.ToppleLiftPercent * distance / 100L);
            if (distance <= 0 || denominator <= 0)
            {
                return 0;
            }

            long perSecond = distance * IntegerMath.Sqrt(gravity * 1_000_000L / denominator) / 1000L;
            return (int)(perSecond / Run.TicksPerSecond * percent / 100L);
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

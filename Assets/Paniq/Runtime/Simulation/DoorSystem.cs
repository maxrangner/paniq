using System;

namespace Paniq.Simulation
{
    /// <summary>One door's runtime state. Where it sits never changes; whether it is locked, unlocked or open does.</summary>
    internal sealed class DoorRuntime
    {
        public SimulationId Id;

        /// <summary>The room whose wall holds this door; what lies beyond is worked out by the geometry.</summary>
        public int Room;

        public WallSide Side;
        public int Centre;
        public int Width;
        public DoorState State;

        /// <summary>Shoving damage taken so far; the door breaks at the scenario's door strength.</summary>
        public int Damage;

        /// <summary>
        /// A ragged hole blasted through a wall rather than a door in a frame. It
        /// is permanently open: nobody opens, shuts, locks or batters it.
        /// </summary>
        public bool IsHole;

        /// <summary>
        /// Whether this opening is in the world at all. Authored doors always
        /// are; a spare slot kept aside for a blast hole is not, until a charge
        /// is spent on it.
        /// </summary>
        public bool Placed = true;
        public ulong UnlockedEventId;
        public ulong OpenedEventId;
    }

    /// <summary>
    /// Door state. Every door starts locked. A player click unlocks it; a
    /// second click opens it; a click on an open door closes it again
    /// (unlocked), unless someone is in the doorway. People can open an
    /// unlocked door, close an open one and lock a closed one themselves (see
    /// <see cref="DoorBehaviour"/>). A broken door stays open for good. This
    /// system is the only one that changes a door's state; the player's clicks
    /// reach it through <see cref="PlayerCommandSystem"/>.
    /// </summary>
    internal sealed class DoorSystem
    {
        private readonly SimulationContext context;
        private readonly DoorRuntime[] doors;
        private readonly WorldGeometry geometry;
        private Crowd crowd;
        private PhysicsObjectSystem objects;

        /// <summary>
        /// Per door, the thing wedged in its doorway, or -1. Worked out once at
        /// the end of each tick (see <see cref="ResolveBlockages"/>), so the
        /// decisions in the next tick read a settled answer rather than one that
        /// changes as objects slide.
        /// </summary>
        private readonly int[] blockedBy;

        public DoorSystem(SimulationContext context, DoorRuntime[] doors, WorldGeometry geometry)
        {
            this.context = context;
            this.doors = doors;
            this.geometry = geometry;
            blockedBy = new int[doors.Length];
            for (int i = 0; i < blockedBy.Length; i++)
            {
                blockedBy[i] = -1;
            }
        }

        /// <summary>
        /// Doors in ascending ID order. The ones leading out of the building
        /// start locked (they are the player's to unlock); inside doors start
        /// shut but unlocked, so people can open them themselves.
        /// </summary>
        public static DoorRuntime[] CreateDoors(FireReactionScenarioData scenario)
        {
            var definitions = (FireReactionDoorDefinition[])scenario.Doors.Clone();
            Array.Sort(definitions, (left, right) => left.DoorId.CompareTo(right.DoorId));
            SimulationId[] spares = scenario.BlastHoles ?? Array.Empty<SimulationId>();
            var doors = new DoorRuntime[definitions.Length + spares.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                doors[i] = new DoorRuntime
                {
                    Id = definitions[i].DoorId,
                    Room = Array.FindIndex(scenario.Rooms, r => r.RoomId == definitions[i].RoomId),
                    Side = definitions[i].Side,
                    Centre = definitions[i].CentreAlongWallMillimetres,
                    Width = definitions[i].WidthMillimetres,
                    State = definitions[i].StartsLocked ? DoorState.Locked : DoorState.Unlocked
                };
            }

            // The spare slots a blast hole can be placed in, appended in their
            // authored order so a slot's number never means two different things
            // over a run. They are not in the world until a charge is spent.
            for (int i = 0; i < spares.Length; i++)
            {
                doors[definitions.Length + i] = new DoorRuntime
                {
                    Id = spares[i],
                    IsHole = true,
                    Placed = false,
                    State = DoorState.Broken
                };
            }

            return doors;
        }

        /// <summary>The openings in the world. Spare hole slots past this are not there yet.</summary>
        public int Count => geometry.DoorCount;

        /// <summary>How many sticks of TNT the player has left.</summary>
        public int BlastChargesRemaining
        {
            get
            {
                int left = 0;
                for (int i = 0; i < doors.Length; i++)
                {
                    left += doors[i].IsHole && !doors[i].Placed ? 1 : 0;
                }

                return left;
            }
        }

        /// <summary>
        /// Blasts a hole through the wall nearest a point. Refuses when there is
        /// no charge left, no wall near enough, or no room for a hole there;
        /// returns the <c>PowerBlastedWall</c> event, or 0 if nothing happened.
        /// </summary>
        public ulong TryBlastWall(LogicalPosition where, int costForTheLog)
        {
            int slot = -1;
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].IsHole && !doors[i].Placed)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0 || !geometry.TryPlaceHole(slot, where, out LogicalPosition centre))
            {
                return 0UL;
            }

            // A hole is open for good, and the escape rules want an event to name
            // as the reason anybody got out through it.
            ulong blasted = context.Events.Append(context.Tick, doors[slot].Id, FireReactionEventType.PowerBlastedWall,
                centre, costForTheLog, 0, 0UL, doors[slot].Id).EventId;
            doors[slot].OpenedEventId = blasted;
            return blasted;
        }

        /// <summary>The people, needed to tell whether a doorway is clear. Set once, when the crowd exists.</summary>
        public void UseCrowd(Crowd people) => crowd = people;

        /// <summary>The loose things, needed to tell whether a doorway is wedged. Set once, when they exist.</summary>
        public void UseObjects(PhysicsObjectSystem physicsObjects) => objects = physicsObjects;

        /// <summary>The thing wedged in this doorway, or -1.</summary>
        public int ObstructionIn(int door) => blockedBy[door];

        /// <summary>Whether something is wedged in this doorway, so the door will not budge either way.</summary>
        public bool IsObstructed(int door) => blockedBy[door] >= 0;

        public DoorState StateOf(int door) => doors[door].State;

        /// <summary>
        /// Phase 8's tail, once every object has finished moving: which doorway
        /// each thing is wedged in. Doors in ascending index and, within a door,
        /// the lowest-numbered thing wins, so a replay always names the same one.
        /// A blockage appearing or clearing goes in the log, so the causal trail
        /// can answer "why would that door not open?".
        /// </summary>
        public void ResolveBlockages()
        {
            int gap = context.Scenario.Blockades.BlockGapMillimetres;
            for (int door = 0; door < Count; door++)
            {
                int found = -1;
                using (PhysicsObjectSystem.Nearby candidates =
                    objects.Gather(geometry.DoorwaySearchArea(door, objects.WidestRadius, gap)))
                {
                    for (int c = 0; c < candidates.Count; c++)
                    {
                        int i = candidates[c];
                        if (objects.IsDormant(i) || objects.HolderOf(i) >= 0 || objects.OccupantOf(i) >= 0)
                        {
                            continue;
                        }

                        if (geometry.IsObjectInDoorway(door, objects.PositionOf(i), objects.RadiusOf(i), gap))
                        {
                            // Ascending order, so this is still the lowest-numbered one.
                            found = i;
                            break;
                        }
                    }
                }

                int was = blockedBy[door];
                if (found == was)
                {
                    continue;
                }

                blockedBy[door] = found;
                if (found >= 0)
                {
                    objects.RecordBlockage(found, doors[door].Id, geometry.DoorCentre(door));
                }
                else
                {
                    objects.RecordUnblocking(was, doors[door].Id, geometry.DoorCentre(door));
                }
            }
        }

        /// <summary>
        /// The opening with this ID, or -1. A spare hole slot is not an opening
        /// yet, so nothing can be aimed at one.
        /// </summary>
        public int IndexOf(SimulationId id)
        {
            for (int i = 0; i < Count; i++)
            {
                if (doors[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The player's click, carried out by
        /// <see cref="PlayerCommandSystem"/> at the start of its tick.
        /// </summary>
        public void ClickDoor(int door)
        {
            DoorRuntime d = doors[door];
            switch (d.State)
            {
                case DoorState.Locked:
                    // The player is the cause, so this is a root event.
                    d.State = DoorState.Unlocked;
                    d.UnlockedEventId = context.Events.Append(
                        context.Tick, d.Id, FireReactionEventType.DoorUnlocked, geometry.DoorCentre(door)).EventId;
                    break;
                case DoorState.Unlocked:
                    Open(door, d.UnlockedEventId);
                    break;
                case DoorState.Open:
                    // The player is the cause, so this is a root event.
                    TryClose(door, d.Id, 0UL);
                    break;
            }
        }

        /// <summary>True when nobody (other than <paramref name="ignore"/>) is in the way of the door swinging shut.</summary>
        public bool IsDoorwayClear(int door, Agent ignore = null)
        {
            if (IsObstructed(door))
            {
                return false;
            }

            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] != ignore && agents[i].IsParticipating && geometry.IsInDoorway(door, agents[i].Body.Position))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Shuts an open door (it is then unlocked), if nobody is in the
        /// doorway. Returns the <c>DoorClosed</c> event, or 0 when it did not close.
        /// </summary>
        public ulong TryClose(int door, SimulationId closer, ulong causalParentEventId, Agent ignore = null)
        {
            DoorRuntime d = doors[door];
            if (d.State != DoorState.Open || !IsDoorwayClear(door, ignore))
            {
                return 0UL;
            }

            d.State = DoorState.Unlocked;
            return context.Events.Append(context.Tick, closer, FireReactionEventType.DoorClosed, geometry.DoorCentre(door),
                0, 0, causalParentEventId, d.Id).EventId;
        }

        /// <summary>Locks a shut door (from either side); a later player click unlocks it again.</summary>
        public void Lock(int door, Agent locker, ulong causalParentEventId)
        {
            DoorRuntime d = doors[door];
            if (d.State != DoorState.Unlocked)
            {
                return;
            }

            d.State = DoorState.Locked;
            context.Events.Append(context.Tick, locker.Id, FireReactionEventType.DoorLocked, geometry.DoorCentre(door),
                0, 0, causalParentEventId, d.Id);
        }

        /// <summary>Opens a door, caused by the player's unlock or by a person's attempt.</summary>
        public void Open(int door, ulong causalParentEventId)
        {
            if (IsObstructed(door))
            {
                // Something is wedged against it: it will not budge.
                return;
            }

            DoorRuntime d = doors[door];
            d.State = DoorState.Open;
            d.OpenedEventId = context.Events.Append(
                context.Tick,
                d.Id,
                FireReactionEventType.DoorOpened,
                geometry.DoorCentre(door),
                d.Width,
                0,
                causalParentEventId).EventId;
        }

        /// <summary>
        /// A strong person's shove weakens the door. Returns true when this
        /// shove broke it.
        /// </summary>
        public bool Batter(int door, Agent shover, int damage, ulong shoveEventId)
        {
            DoorRuntime d = doors[door];
            if (damage <= 0 || d.State == DoorState.Open || d.State == DoorState.Broken)
            {
                return false;
            }

            d.Damage += damage;
            if (d.Damage < context.Scenario.Exits.DoorStrength)
            {
                return false;
            }

            Break(door, shover, shoveEventId);
            return true;
        }

        /// <summary>
        /// Smashed open by a person: it stays open for good. Escapes through
        /// it name this event as their cause.
        /// </summary>
        private void Break(int door, Agent breaker, ulong shoveEventId)
        {
            DoorRuntime d = doors[door];
            d.State = DoorState.Broken;
            d.OpenedEventId = context.Events.Append(
                context.Tick,
                breaker.Id,
                FireReactionEventType.DoorBrokenDown,
                geometry.DoorCentre(door),
                d.Width,
                0,
                shoveEventId,
                d.Id).EventId;
        }

        public SimulationId IdOf(int door) => doors[door].Id;

        public int WidthOf(int door) => doors[door].Width;

        public ulong OpenedEventIdOf(int door) => doors[door].OpenedEventId;

        public FireReactionDoorSnapshot GetSnapshot(int door)
        {
            DoorRuntime d = doors[door];
            int damagePercent = Math.Min(100, d.Damage * 100 / context.Scenario.Exits.DoorStrength);
            return new FireReactionDoorSnapshot(d.Id, d.Side, geometry.DoorCentre(door), d.Width, d.State, damagePercent,
                d.IsHole, IsObstructed(door), geometry.DoorLeadsOutside(door));
        }

        public FireReactionDoorSnapshot[] GetSnapshots()
        {
            // Only the openings that are really there: a spare hole slot has no
            // position to draw and nothing to say about it.
            var snapshots = new FireReactionDoorSnapshot[Count];
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i] = GetSnapshot(i);
            }

            return snapshots;
        }
    }
}

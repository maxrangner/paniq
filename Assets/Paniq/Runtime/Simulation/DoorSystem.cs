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
        /// Ticks this door has stood with flames against it. A shut door used
        /// to stop fire for ever, which turned every closed room into a
        /// permanent safe room; now it holds the fire off for a while and then
        /// burns through. Kept separately from shoving damage because the two
        /// are different kinds of harm, and a door half battered is not half
        /// burnt.
        /// </summary>
        public int Scorch;

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

        /// <summary>
        /// Which way this leaf is allowed to swing: both ways by default, so it
        /// gives way to whoever pushes it and is only stopped by something lying
        /// on the side it would swing into.
        /// </summary>
        public DoorSwingRule Swing = DoorSwingRule.BothWays;

        /// <summary>
        /// Which way the leaf stands open: +1 out of <see cref="Room"/>, -1 into
        /// it, 0 while it is shut. Chosen when it opens, and the side that has to
        /// be clear again before it can be shut.
        /// </summary>
        public int OpenSide;

        /// <summary>
        /// A pair of swing doors: always <see cref="DoorState.Open"/> for people,
        /// sight and sound, never shut, locked or battered, and a door in the
        /// fire's way all the same until it burns through (in half a shut
        /// door's time) or something wedged in the gap props it open.
        /// </summary>
        public bool Swings;

        /// <summary>
        /// Something is lying in the doorway, as worked out at the end of the
        /// last tick (see <see cref="DoorSystem.ResolveBlockages"/>). Written
        /// here so the geometry, which has no door system to ask, can tell
        /// that a swing door is propped and the fire may come through.
        /// </summary>
        public bool Obstructed;

        /// <summary>
        /// The player has a hand on it, holding it shut (prototype 3,
        /// 2026-09-25). Nobody opens it while it is held, locked or not;
        /// somebody strong enough bursts it in one push. Lasts until the
        /// player lets go.
        /// </summary>
        public bool HeldShut;

        /// <summary>
        /// An archway with the tower of boxes lying across it (see
        /// <see cref="TrapSystem"/>): shut, though it has no leaf, for people
        /// and fire alike, until enough of the boxes are gone.
        /// </summary>
        public bool Piled;
    }

    /// <summary>
    /// Which way a door leaf may swing. Both ways is ordinary; the one-way rules
    /// are here for a fire door or a turnstile a later scenario may want to
    /// author. Append only: the value is part of the replay fingerprint.
    /// </summary>
    public enum DoorSwingRule
    {
        BothWays,

        /// <summary>Only away from the room whose wall holds it.</summary>
        AwayFromItsRoom,

        /// <summary>Only into the room whose wall holds it.</summary>
        IntoItsRoom
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
    internal sealed class DoorSystem : IBindable
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
            openedThisTick = new int[doors.Length];
            proppedThisTick = new int[doors.Length];
            for (int i = 0; i < blockedBy.Length; i++)
            {
                blockedBy[i] = -1;
                slotById[doors[i].Id] = i;
            }
        }

        /// <summary>
        /// The doors that became a way through this tick, in the order it
        /// happened, so the crowd can be told once at the end of the tick rather
        /// than mid-decision. Fixed size, filled and emptied in place.
        /// </summary>
        private readonly int[] openedThisTick;
        private int openedCount;

        /// <summary>How many doors opened this tick, and which.</summary>
        public int OpeningsThisTick => openedCount;

        public int OpeningAt(int index) => openedThisTick[index];

        /// <summary>Everybody has been told; start the next tick's list empty.</summary>
        public void ClearOpenings()
        {
            openedCount = 0;
            proppedCount = 0;
        }

        /// <summary>
        /// The swing doors something got wedged into this tick, so the fire on
        /// either side can be told the way is open now. Nobody else cares: to
        /// people the doorway was open already.
        /// </summary>
        private readonly int[] proppedThisTick;
        private int proppedCount;

        public int ProppingsThisTick => proppedCount;

        public int ProppingAt(int index) => proppedThisTick[index];

        /// <summary>A door became a way through: note it for the end of the tick.</summary>
        private void RecordOpening(int door)
        {
            for (int i = 0; i < openedCount; i++)
            {
                if (openedThisTick[i] == door)
                {
                    return;
                }
            }

            openedThisTick[openedCount++] = door;
        }

        /// <summary>
        /// Whether the leaf is allowed to swing this way at all. Ordinary doors
        /// go both ways; the one-way rules are here for a fire door a later
        /// scenario may author. +1 is out of the door's own room.
        /// </summary>
        public bool CanSwing(int door, int side)
        {
            DoorRuntime d = doors[door];
            return side > 0 ? d.Swing != DoorSwingRule.IntoItsRoom : d.Swing != DoorSwingRule.AwayFromItsRoom;
        }

        /// <summary>A ragged gap blasted through a wall: there is no leaf to swing, pull at or shoulder.</summary>
        public bool IsHole(int door) => doors[door].IsHole;

        /// <summary>
        /// Doors in ascending ID order. The ones leading out of the building
        /// start locked (they are the player's to unlock); inside doors start
        /// shut but unlocked, so people can open them themselves.
        /// </summary>
        public static DoorRuntime[] CreateDoors(ScenarioData scenario)
        {
            var definitions = (DoorDefinition[])scenario.Doors.Clone();
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

                    // An archway is a doorway with nothing in it. That is
                    // exactly what a hole blown through a wall already is, so
                    // it is one: permanently open, nothing to shut, and the
                    // fire walks through it. The only difference is that this
                    // one was there from the start rather than being made.
                    IsHole = definitions[i].IsOpening,

                    // Swing doors stand open from the start and stay so:
                    // nothing below ever shuts them. Only the fire treats
                    // them as a door.
                    Swings = definitions[i].Swings,
                    State = definitions[i].IsOpening || definitions[i].Swings
                        ? definitions[i].IsOpening ? DoorState.Broken : DoorState.Open
                        : definitions[i].StartsLocked ? DoorState.Locked : DoorState.Unlocked
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
            ulong blasted = context.Events.Append(context.Tick, doors[slot].Id, CausalEventType.PowerBlastedWall,
                centre, costForTheLog, 0, 0UL, doors[slot].Id).EventId;
            doors[slot].OpenedEventId = blasted;

            // A hole in a wall is a way out that was not there a moment ago, and
            // it deserves the same notice as a door swinging open.
            RecordOpening(slot);
            return blasted;
        }

        /// <summary>
        /// The people (is a doorway clear?), the loose things (is it wedged?)
        /// and the bodies (is anybody lying across it?) are all built after the
        /// doors, so they are handed over once everything exists.
        /// </summary>
        public void Bind(Systems systems)
        {
            crowd = systems.Crowd;
            objects = systems.Objects;
            physics = systems.Physics;
            people = systems.People;
        }

        /// <summary>The thing wedged in this doorway, or -1.</summary>
        public int ObstructionIn(int door) => blockedBy[door];

        /// <summary>Whether something is wedged in this doorway, so the door will not budge either way.</summary>
        public bool IsObstructed(int door) => blockedBy[door] >= 0;

        public DoorState StateOf(int door) => doors[door].State;

        /// <summary>Whether the player is holding this door shut.</summary>
        public bool IsHeldShut(int door) => doors[door].HeldShut;

        /// <summary>Whether the tower of boxes is lying across this doorway.</summary>
        public bool IsPiled(int door) => doors[door].Piled;

        /// <summary>
        /// Whether a person can simply push this door open: shut but not
        /// locked, nothing wedged in it, nobody holding it, no heap of boxes
        /// across it. The one question every person at a door asks first.
        /// </summary>
        public bool CanBePushedOpen(int door)
        {
            DoorRuntime d = doors[door];
            return d.State == DoorState.Unlocked && !IsObstructed(door) && !d.HeldShut && !d.Piled;
        }

        /// <summary>
        /// The player takes hold of a door and holds it shut. An open door is
        /// pulled shut first, if the doorway is clear; otherwise the hand
        /// stays on it and it shuts the moment the doorway clears (see
        /// <see cref="KeepHeldDoorsShut"/>). Returns whether anything
        /// changed: a door already held, a swing door, a hole or a broken
        /// door is nothing to hold, and a locked door needs no hand on it --
        /// its lock already holds it, and a hand there would have let the
        /// strong through it in one push, which a lock does not.
        /// </summary>
        public bool HoldShut(int door)
        {
            DoorRuntime d = doors[door];
            if (d.HeldShut || d.IsHole || d.Swings || d.State == DoorState.Broken || d.State == DoorState.Locked)
            {
                return false;
            }

            d.HeldShut = true;
            ulong held = context.Events.Append(context.Tick, d.Id, CausalEventType.PowerHeldDoor, geometry.DoorCentre(door),
                0, 0, 0UL, d.Id).EventId;
            if (d.State == DoorState.Open)
            {
                TryClose(door, d.Id, held);
            }

            return true;
        }

        /// <summary>The player lets go of a door they were holding; nothing happens if they were not.</summary>
        public void Release(int door)
        {
            DoorRuntime d = doors[door];
            if (!d.HeldShut)
            {
                return;
            }

            d.HeldShut = false;
            context.Events.Append(context.Tick, d.Id, CausalEventType.PowerReleasedDoor, geometry.DoorCentre(door),
                0, 0, 0UL, d.Id);
        }

        /// <summary>
        /// Phase 1's tail: a held door that is still open (somebody was in
        /// the doorway when the player took hold of it) shuts as soon as the
        /// doorway is clear. Ascending door index, no random draw.
        /// </summary>
        public void KeepHeldDoorsShut()
        {
            for (int door = 0; door < Count; door++)
            {
                DoorRuntime d = doors[door];
                if (d.HeldShut && d.State == DoorState.Open)
                {
                    TryClose(door, d.Id, 0UL);
                }
            }
        }

        /// <summary>
        /// The tower of boxes has come down across this archway (see
        /// <see cref="TrapSystem"/>): it is shut now, for people and fire,
        /// though it still has no leaf. Its plug appears with the next
        /// physics step, as for any door that shuts.
        /// </summary>
        public void PileInto(int door)
        {
            DoorRuntime d = doors[door];
            d.Piled = true;
            d.State = DoorState.Unlocked;
            d.OpenSide = 0;
        }

        /// <summary>
        /// Enough of the boxes are gone: the archway is an archway again,
        /// open for good. The fire beside it is told, as beside any door
        /// that has just opened.
        /// </summary>
        public void ClearPile(int door)
        {
            DoorRuntime d = doors[door];
            if (!d.Piled)
            {
                return;
            }

            d.Piled = false;
            d.State = DoorState.Broken;
            RecordOpening(door);
        }

        /// <summary>
        /// One number that changes whenever any door does: state, damage or
        /// scorch. The end of a round reads it to tell a building where
        /// something is still happening from one that has settled.
        /// </summary>
        public long DoorSignature
        {
            get
            {
                long signature = 0L;
                for (int door = 0; door < Count; door++)
                {
                    DoorRuntime d = doors[door];
                    signature = signature * 31L + (int)d.State + d.Damage + d.Scorch + (d.HeldShut ? 7 : 0) + (d.Piled ? 11 : 0);
                }

                return signature;
            }
        }

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
                doors[door].Obstructed = found >= 0;
                if (found >= 0)
                {
                    objects.RecordBlockage(found, doors[door].Id, geometry.DoorCentre(door));
                    if (doors[door].Swings && doors[door].State != DoorState.Broken)
                    {
                        // Propped open: the fire, which had this door in its
                        // way, is told the way is clear now, or a burning
                        // square that had nowhere to go would never wake.
                        proppedThisTick[proppedCount++] = door;
                    }
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
            // A spare slot is in the table too, and answers only once a hole
            // has been placed in it: that is when it becomes an opening.
            return slotById.TryGetValue(id, out int slot) && slot < Count ? slot : -1;
        }

        /// <summary>Every slot by its ID, built once, so a click is not a walk down the list.</summary>
        private readonly System.Collections.Generic.Dictionary<SimulationId, int> slotById =
            new System.Collections.Generic.Dictionary<SimulationId, int>();

        /// <summary>
        /// The player's click, carried out by
        /// <see cref="PlayerCommandSystem"/> at the start of its tick. Returns
        /// whether the door actually did anything, because a click now costs
        /// influence and a door that will not budge must not be charged for.
        /// </summary>
        public bool ClickDoor(int door)
        {
            DoorRuntime d = doors[door];
            switch (d.State)
            {
                case DoorState.Locked:
                    UnlockByPlayer(door);
                    return true;
                case DoorState.Unlocked:
                    return Open(door, d.UnlockedEventId);
                case DoorState.Open:
                    // The player is the cause, so this is a root event.
                    return TryClose(door, d.Id, 0UL) != 0UL;
                default:
                    // Broken down: there is nothing left to work.
                    return false;
            }
        }

        /// <summary>
        /// The player turns the key (2026-09-25), carried out by
        /// <see cref="PlayerCommandSystem"/>: a locked door is unlocked, a
        /// shut one locked, and an open one shut and then locked -- if nobody
        /// is in the doorway. Returns whether the door actually changed, so a
        /// key that turned nothing is not charged for. Swing doors, archways
        /// and holes have no key.
        /// </summary>
        public bool ToggleLock(int door)
        {
            DoorRuntime d = doors[door];
            if (d.IsHole || d.Swings || d.HeldShut)
            {
                // Nothing to turn a key in; and a door the player is holding
                // is not also being locked by them.
                return false;
            }

            switch (d.State)
            {
                case DoorState.Locked:
                    UnlockByPlayer(door);
                    return true;
                case DoorState.Unlocked:
                    LockByPlayer(door);
                    return true;
                case DoorState.Open:
                    if (TryClose(door, d.Id, 0UL) == 0UL)
                    {
                        return false;
                    }

                    LockByPlayer(door);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The player unlocks a door: a root event, with the door as its source.</summary>
        private void UnlockByPlayer(int door)
        {
            DoorRuntime d = doors[door];
            d.State = DoorState.Unlocked;
            d.UnlockedEventId = context.Events.Append(
                context.Tick, d.Id, CausalEventType.DoorUnlocked, geometry.DoorCentre(door)).EventId;
        }

        /// <summary>The player locks a shut door: a root event, with the door as both its source and its target.</summary>
        private void LockByPlayer(int door)
        {
            DoorRuntime d = doors[door];
            d.State = DoorState.Locked;
            context.Events.Append(context.Tick, d.Id, CausalEventType.DoorLocked, geometry.DoorCentre(door),
                0, 0, 0UL, d.Id);
        }

        /// <summary>True when nobody (other than <paramref name="ignore"/>) is in the way of the door swinging shut.</summary>
        public bool IsDoorwayClear(int door, Agent ignore = null)
        {
            if (IsObstructed(door))
            {
                return false;
            }

            using (Crowd.Nearby near = crowd.Gather(geometry.PersonDoorwaySearchArea(door)))
            {
                for (int c = 0; c < near.Count; c++)
                {
                    Agent other = crowd.All[near[c]];
                    if (other != ignore && other.IsParticipating && geometry.IsInDoorway(door, other.Body.Position))
                    {
                        return false;
                    }
                }
            }

            // Nobody's middle is in the doorway, but a body is long: somebody
            // lying across the threshold with only their legs in the gap stops
            // the door as surely as somebody standing in it.
            int ignoreHandle = ignore != null && people != null ? people.HandleOf(ignore) : -1;
            return physics == null || !physics.IsAnyBodyInDoorway(door, ignoreHandle);
        }

        private PhysicsWorld physics;
        private PeopleBodies people;

        /// <summary>
        /// Shuts an open door (it is then unlocked), if nobody is in the
        /// doorway. Returns the <c>DoorClosed</c> event, or 0 when it did not close.
        /// </summary>
        public ulong TryClose(int door, SimulationId closer, ulong causalParentEventId, Agent ignore = null)
        {
            DoorRuntime d = doors[door];
            if (d.State != DoorState.Open || d.Swings || !IsDoorwayClear(door, ignore))
            {
                // Nothing to shut: swing doors shut themselves behind
                // whoever went through, and stand open to whoever comes next.
                return 0UL;
            }

            d.State = DoorState.Unlocked;
            d.OpenSide = 0;
            return context.Events.Append(context.Tick, closer, CausalEventType.DoorClosed, geometry.DoorCentre(door),
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
            context.Events.Append(context.Tick, locker.Id, CausalEventType.DoorLocked, geometry.DoorCentre(door),
                0, 0, causalParentEventId, d.Id);
        }

        /// <summary>
        /// Which way the leaf swings when somebody standing on
        /// <paramref name="pushedFrom"/> opens it: away from them, which is what
        /// happens when you push a door, unless the scenario says this one only
        /// goes one way. The player (0) gets the door's usual way out of its
        /// room, so a door nobody is touching swings the way it always has.
        /// </summary>
        private int ChooseSwing(int door, int pushedFrom)
        {
            int first = pushedFrom == 0 ? 1 : -pushedFrom;
            return CanSwing(door, first) ? first : -first;
        }

        /// <summary>Opens a door, caused by the player's unlock or by a person's attempt.</summary>
        /// <param name="pushedFrom">
        /// Which side the person opening it is standing on: +1 beyond the door's
        /// own room, -1 inside it, 0 for the player, who is not standing
        /// anywhere. The leaf swings away from them if it can, the way a real
        /// door gives when you push it.
        /// </param>
        /// <returns>Whether it opened; something wedged in the doorway stops it.</returns>
        public bool Open(int door, ulong causalParentEventId, int pushedFrom = 0)
        {
            if (IsObstructed(door) || doors[door].HeldShut || doors[door].Piled)
            {
                // Something is wedged against it, the player is holding it,
                // or the boxes are lying across it: it will not budge.
                return false;
            }

            int swing = ChooseSwing(door, pushedFrom);

            DoorRuntime d = doors[door];
            d.OpenSide = swing;
            d.State = DoorState.Open;
            RecordOpening(door);
            d.OpenedEventId = context.Events.Append(
                context.Tick,
                d.Id,
                CausalEventType.DoorOpened,
                geometry.DoorCentre(door),
                d.Width,
                0,
                causalParentEventId).EventId;
            return true;
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
            // It comes off its hinges away from whoever was shouldering it,
            // whatever is lying on the far side: it is not swinging any more.
            Break(door, breaker.Id, -geometry.SideOf(door, breaker.Body.Position), shoveEventId,
                CausalEventType.DoorBrokenDown);
        }

        /// <summary>
        /// A door that is not a door any more, however it got that way. It
        /// stays open for good, and whatever comes through it -- people
        /// escaping, fire spreading -- names this event as its cause.
        /// </summary>
        private void Break(int door, SimulationId source, int fallsToward, ulong causalParentEventId,
            CausalEventType how)
        {
            DoorRuntime d = doors[door];
            d.State = DoorState.Broken;
            d.OpenSide = fallsToward;

            // Nothing left to hold: whoever had a hand on it has lost it.
            d.HeldShut = false;
            RecordOpening(door);
            d.OpenedEventId = context.Events.Append(
                context.Tick,
                source,
                how,
                geometry.DoorCentre(door),
                d.Width,
                0,
                causalParentEventId,
                d.Id).EventId;
        }

        /// <summary>
        /// Phase 9's tail: doors standing in the fire. A shut door holds the
        /// flames off, but only for a while -- once it has stood in them for
        /// the scenario's burn-through time it goes, and the fire comes on.
        /// <para>
        /// This is what stops a shut door being a permanent firebreak. It used
        /// to be one, so a building with its doors closed had rooms the fire
        /// could never reach, and shutting yourself in was a way to win rather
        /// than a way to buy time.
        /// </para>
        /// Ascending door index and no random draw, so a replay agrees.
        /// </summary>
        public void ScorchInTheFire(FireSystem fire)
        {
            int reach = context.Scenario.Exits.FireAtDoorRadiusMillimetres;
            for (int door = 0; door < Count; door++)
            {
                DoorRuntime d = doors[door];
                if (d.IsHole || d.State == DoorState.Broken || (d.State == DoorState.Open && !d.Swings) ||
                    (d.Swings && d.Obstructed))
                {
                    // Nothing standing in the way for the flames to eat: an
                    // open door, a hole, or swing doors propped open.
                    continue;
                }

                int through = BurnThroughTicksOf(d);

                // Only flames in a room the door opens onto can reach it. It
                // used to be any flames within reach in a straight line, wall
                // or no wall: the storage closet is two metres deep, so a fire
                // in the bathroom the other side of its back wall was exactly
                // within reach of its door, and burnt it open from a room the
                // door has nothing to do with.
                LogicalPosition centre = geometry.DoorCentre(door);
                int room = geometry.DoorRoom(door);
                long gap = fire.NearestCellDistanceSquaredInRooms(centre, room, geometry.RoomBeyond(door, room),
                    out LogicalPosition flames, out int cell);
                if (cell < 0 || gap > (long)reach * reach)
                {
                    continue;
                }

                d.Scorch++;
                if (d.Scorch < through)
                {
                    continue;
                }

                // What is left of it falls away from the flames.
                Break(door, d.Id, -geometry.SideOf(door, flames), fire.CellEventId(cell),
                    CausalEventType.DoorBurntThrough);
            }
        }

        /// <summary>How long this door stands in the flames before it goes: swing doors half as long as a shut door.</summary>
        private int BurnThroughTicksOf(DoorRuntime d) =>
            d.Swings ? context.Scenario.Exits.SwingDoorBurnThroughTicks : context.Scenario.Exits.DoorBurnThroughTicks;

        /// <summary>How far through burning this door is, nought to a hundred, for the display.</summary>
        private int ScorchPercent(DoorRuntime d) =>
            Math.Min(100, d.Scorch * 100 / BurnThroughTicksOf(d));

        public SimulationId IdOf(int door) => doors[door].Id;

        public int WidthOf(int door) => doors[door].Width;

        public ulong OpenedEventIdOf(int door) => doors[door].OpenedEventId;

        public DoorSnapshot GetSnapshot(int door)
        {
            DoorRuntime d = doors[door];
            int damagePercent = Math.Min(100, d.Damage * 100 / context.Scenario.Exits.DoorStrength);
            return new DoorSnapshot(d.Id, d.Side, geometry.DoorCentre(door), d.Width, d.State, damagePercent,
                ScorchPercent(d),
                d.IsHole, IsObstructed(door), geometry.DoorLeadsOutside(door), d.OpenSide, IsObstructed(door), d.Swings,
                d.HeldShut, d.Piled);
        }

        public DoorSnapshot[] GetSnapshots()
        {
            // Only the openings that are really there: a spare hole slot has no
            // position to draw and nothing to say about it.
            var snapshots = new DoorSnapshot[Count];
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i] = GetSnapshot(i);
            }

            return snapshots;
        }
    }
}

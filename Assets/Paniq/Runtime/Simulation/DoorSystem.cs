using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>One door's runtime state. Where it sits never changes; whether it is locked, unlocked or open does.</summary>
    internal sealed class DoorRuntime
    {
        public SimulationId Id;
        public WallSide Side;
        public int Centre;
        public int Width;
        public DoorState State;

        /// <summary>Shoving damage taken so far; the door breaks at the scenario's door strength.</summary>
        public int Damage;
        public ulong UnlockedEventId;
        public ulong OpenedEventId;
    }

    /// <summary>
    /// Door state and player commands. Every door starts locked. A player
    /// click unlocks it; a second click opens it; a click on an open door
    /// closes it again (unlocked), unless someone is in the doorway. People
    /// can open an unlocked door, close an open one and lock a closed one
    /// themselves (see <see cref="DoorBehaviour"/>). A broken door stays
    /// open for good. This system is the only one that changes a door's state.
    /// </summary>
    internal sealed class DoorSystem
    {
        private readonly SimulationContext context;
        private readonly DoorRuntime[] doors;
        private readonly WorldGeometry geometry;
        private Crowd crowd;
        private readonly List<PlayerCommand> pendingCommands = new List<PlayerCommand>();
        private readonly List<PlayerCommand> commandHistory = new List<PlayerCommand>();
        private long nextCommandSequence = 1L;

        public DoorSystem(SimulationContext context, DoorRuntime[] doors, WorldGeometry geometry)
        {
            this.context = context;
            this.doors = doors;
            this.geometry = geometry;
        }

        /// <summary>Doors in ascending ID order, all locked.</summary>
        public static DoorRuntime[] CreateDoors(FireReactionScenarioData scenario)
        {
            var definitions = (FireReactionDoorDefinition[])scenario.Doors.Clone();
            Array.Sort(definitions, (left, right) => left.DoorId.CompareTo(right.DoorId));
            var doors = new DoorRuntime[definitions.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i] = new DoorRuntime
                {
                    Id = definitions[i].DoorId,
                    Side = definitions[i].Side,
                    Centre = definitions[i].CentreAlongWallMillimetres,
                    Width = definitions[i].WidthMillimetres,
                    State = DoorState.Locked
                };
            }

            return doors;
        }

        public int Count => doors.Length;

        /// <summary>The people, needed to tell whether a doorway is clear. Set once, when the crowd exists.</summary>
        public void UseCrowd(Crowd people) => crowd = people;

        public DoorState StateOf(int door) => doors[door].State;

        /// <summary>Every command queued so far, in sequence order.</summary>
        public IReadOnlyList<PlayerCommand> Commands => commandHistory;

        /// <summary>
        /// Queues a player action for a tick that has not started yet. Commands
        /// for the same tick run in the order they were queued.
        /// </summary>
        public PlayerCommand QueueCommand(PlayerCommandType commandType, SimulationId targetId, int targetTick)
        {
            if (targetTick <= context.Tick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick),
                    $"Tick {targetTick} has already started; the next tick is {context.Tick + 1}.");
            }

            if (FindDoor(targetId) < 0)
            {
                throw new ArgumentException($"Unknown door ID {targetId}.", nameof(targetId));
            }

            var command = new PlayerCommand(targetTick, nextCommandSequence++, commandType, targetId);
            pendingCommands.Add(command);
            commandHistory.Add(command);
            return command;
        }

        /// <summary>Phase 1: commands for this tick, in sequence order.</summary>
        public void ConsumeCommands()
        {
            for (int i = 0; i < pendingCommands.Count; i++)
            {
                PlayerCommand command = pendingCommands[i];
                if (command.TargetTick != context.Tick)
                {
                    continue;
                }

                pendingCommands.RemoveAt(i);
                i--;
                if (command.CommandType == PlayerCommandType.ClickDoor)
                {
                    ClickDoor(FindDoor(command.TargetId));
                }
            }
        }

        private int FindDoor(SimulationId id)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ClickDoor(int door)
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

        public ulong OpenedEventIdOf(int door) => doors[door].OpenedEventId;

        public FireReactionDoorSnapshot GetSnapshot(int door)
        {
            DoorRuntime d = doors[door];
            int damagePercent = Math.Min(100, d.Damage * 100 / context.Scenario.Exits.DoorStrength);
            return new FireReactionDoorSnapshot(d.Id, d.Side, geometry.DoorCentre(door), d.Width, d.State, damagePercent);
        }

        public FireReactionDoorSnapshot[] GetSnapshots()
        {
            var snapshots = new FireReactionDoorSnapshot[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                snapshots[i] = GetSnapshot(i);
            }

            return snapshots;
        }
    }
}

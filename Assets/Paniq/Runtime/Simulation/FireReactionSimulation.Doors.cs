using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Doors, player clicks and escaping. Every door starts locked. A player
    /// click unlocks it, a second click opens it. Panicking people head for a
    /// door: an unlocked one they open, a locked one they rattle and maybe
    /// try to force (it never gives) before looking for another way out. An
    /// open door leads to a short strip outside; walking far enough along it
    /// is an escape.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private const int DoorApproachInset = 600;
        private const int DoorArrivalDistance = 400;
        private const int DoorStripInset = 1000;
        private const int DoorOutsideTarget = 1500;
        private const int DoorCommitDistance = 2500;
        private const int DoorNoSwerveDistance = 2000;
        private const int DoorOpenBonus = 4000;
        private const int DoorCurrentChoiceBonus = 1500;
        private const int DoorChoiceNoise = 1500;
        private const int DoorInFirePenalty = 8000;
        private const int DoorGiveUpGlanceMinimumTicks = 15;
        private const int DoorGiveUpGlanceMaximumTicks = 30;

        private sealed class DoorRuntime
        {
            public StableAgentId Id;
            public WallSide Side;
            public int Centre;
            public int Width;
            public DoorState State;
            public ulong UnlockedEventId;
            public ulong OpenedEventId;
        }

        private readonly List<PlayerCommand> pendingCommands = new List<PlayerCommand>();
        private readonly List<PlayerCommand> commandHistory = new List<PlayerCommand>();
        private DoorRuntime[] doors;
        private long nextCommandSequence = 1L;

        public int DoorCount => doors.Length;

        /// <summary>Every command queued so far, in sequence order. Replaying them gives the same run.</summary>
        public IReadOnlyList<PlayerCommand> Commands => commandHistory;

        public FireReactionDoorSnapshot GetDoor(int index) => ToSnapshot(doors[index]);

        /// <summary>
        /// Queues a player action for a tick that has not started yet. Commands
        /// for the same tick run in the order they were queued.
        /// </summary>
        public PlayerCommand QueueCommand(PlayerCommandType commandType, StableAgentId targetId, int targetTick)
        {
            if (targetTick <= tick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick),
                    $"Tick {targetTick} has already started; the next tick is {tick + 1}.");
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

        private void InitializeDoors()
        {
            var definitions = (FireReactionDoorDefinition[])scenario.Doors.Clone();
            Array.Sort(definitions, (left, right) => left.DoorId.CompareTo(right.DoorId));
            doors = new DoorRuntime[definitions.Length];
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
        }

        private int FindDoor(StableAgentId id)
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

        /// <summary>Phase 1: commands for this tick, in sequence order.</summary>
        private void ConsumeCommands()
        {
            for (int i = 0; i < pendingCommands.Count; i++)
            {
                PlayerCommand command = pendingCommands[i];
                if (command.TargetTick != tick)
                {
                    continue;
                }

                pendingCommands.RemoveAt(i);
                i--;
                if (command.CommandType == PlayerCommandType.ClickDoor)
                {
                    ClickDoor(doors[FindDoor(command.TargetId)]);
                }
            }
        }

        private void ClickDoor(DoorRuntime door)
        {
            switch (door.State)
            {
                case DoorState.Locked:
                    // The player is the cause, so this is a root event.
                    door.State = DoorState.Unlocked;
                    door.UnlockedEventId = eventLog.Append(
                        tick, door.Id, FireReactionEventType.DoorUnlocked, DoorPoint(door, 0, 0)).EventId;
                    break;
                case DoorState.Unlocked:
                    OpenDoor(door, door.UnlockedEventId);
                    break;
            }
        }

        private void OpenDoor(DoorRuntime door, ulong causalParentEventId)
        {
            door.State = DoorState.Open;
            door.OpenedEventId = eventLog.Append(
                tick,
                door.Id,
                FireReactionEventType.DoorOpened,
                DoorPoint(door, 0, 0),
                door.Width,
                0,
                causalParentEventId).EventId;
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>
        /// A point near a door: <paramref name="along"/> millimetres along the
        /// wall from the door's centre and <paramref name="outward"/>
        /// millimetres out of the room (negative is inside).
        /// </summary>
        private LogicalPosition DoorPoint(DoorRuntime door, int along, int outward)
        {
            LogicalBounds room = scenario.RoomBounds;
            switch (door.Side)
            {
                case WallSide.North:
                    return new LogicalPosition(door.Centre + along, room.MaxZ + outward);
                case WallSide.South:
                    return new LogicalPosition(door.Centre + along, room.MinZ - outward);
                case WallSide.East:
                    return new LogicalPosition(room.MaxX + outward, door.Centre + along);
                default:
                    return new LogicalPosition(room.MinX - outward, door.Centre + along);
            }
        }

        /// <summary>How far past the door's wall a point is (negative inside the room).</summary>
        private long OutsideDistance(DoorRuntime door, LogicalPosition position)
        {
            LogicalBounds room = scenario.RoomBounds;
            switch (door.Side)
            {
                case WallSide.North:
                    return (long)position.Z - room.MaxZ;
                case WallSide.South:
                    return (long)room.MinZ - position.Z;
                case WallSide.East:
                    return (long)position.X - room.MaxX;
                default:
                    return (long)room.MinX - position.X;
            }
        }

        /// <summary>How far along the wall a point is from the door's centre.</summary>
        private static long AlongOffset(DoorRuntime door, LogicalPosition position)
        {
            return door.Side == WallSide.North || door.Side == WallSide.South
                ? (long)position.X - door.Centre
                : (long)position.Z - door.Centre;
        }

        /// <summary>The walkable strip through an open door: from 1 m inside the wall to the end of the doorway outside.</summary>
        private LogicalBounds DoorwayStrip(DoorRuntime door)
        {
            LogicalPosition inner = DoorPoint(door, -door.Width / 2, -DoorStripInset);
            LogicalPosition outer = DoorPoint(door, door.Width / 2, scenario.DoorwayDepthMillimetres);
            return new LogicalBounds(
                Math.Min(inner.X, outer.X), Math.Max(inner.X, outer.X),
                Math.Min(inner.Z, outer.Z), Math.Max(inner.Z, outer.Z));
        }

        private bool IsInsideRoom(LogicalPosition position)
        {
            return scenario.RoomBounds.ContainsCircle(position, scenario.OccupancyRadiusMillimetres);
        }

        /// <summary>
        /// Only someone heading for this door, or already out of the room,
        /// may step into its doorway. Calm people treat every door as wall.
        /// </summary>
        private bool CanUseDoorway(AgentRuntime agent, int doorIndex)
        {
            return doors[doorIndex].State == DoorState.Open &&
                   (agent.ExitDoorIndex == doorIndex || !IsInsideRoom(agent.Position));
        }

        /// <summary>The agent's whole footprint fits in the room or in a doorway it may use.</summary>
        private bool IsInWalkableSpace(AgentRuntime agent, LogicalPosition position)
        {
            if (IsInsideRoom(position))
            {
                return true;
            }

            for (int d = 0; d < doors.Length; d++)
            {
                if (CanUseDoorway(agent, d) &&
                    DoorwayStrip(doors[d]).ContainsCircle(position, scenario.OccupancyRadiusMillimetres))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the swept footprint clips the frame of any open door.</summary>
        private bool ClipsDoorFrame(LogicalPosition start, LogicalPosition destination)
        {
            long radiusSquared = (long)scenario.OccupancyRadiusMillimetres * scenario.OccupancyRadiusMillimetres;
            for (int d = 0; d < doors.Length; d++)
            {
                DoorRuntime door = doors[d];
                if (door.State != DoorState.Open)
                {
                    continue;
                }

                if (IntegerMath.SegmentPassesWithin(start, destination, DoorPoint(door, -door.Width / 2, 0), radiusSquared) ||
                    IntegerMath.SegmentPassesWithin(start, destination, DoorPoint(door, door.Width / 2, 0), radiusSquared))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Keeps a step inside the space the agent may walk in: if the
        /// destination is already fine it stays; otherwise it is clamped into
        /// the room, or into the doorway the agent is standing in.
        /// </summary>
        private LogicalPosition ClampIntoWalkable(AgentRuntime agent, LogicalPosition position)
        {
            if (IsInWalkableSpace(agent, position))
            {
                return position;
            }

            int radius = scenario.OccupancyRadiusMillimetres;
            if (!IsInsideRoom(agent.Position))
            {
                for (int d = 0; d < doors.Length; d++)
                {
                    LogicalBounds strip = DoorwayStrip(doors[d]);
                    if (CanUseDoorway(agent, d) && strip.ContainsCircle(agent.Position, radius))
                    {
                        return Clamp(position, strip, radius);
                    }
                }
            }

            return Clamp(position, scenario.RoomBounds, radius);
        }

        private static LogicalPosition Clamp(LogicalPosition position, LogicalBounds bounds, int radius)
        {
            return new LogicalPosition(
                Math.Max(bounds.MinX + radius, Math.Min(bounds.MaxX - radius, position.X)),
                Math.Max(bounds.MinZ + radius, Math.Min(bounds.MaxZ - radius, position.Z)));
        }

        /// <summary>True when the agent is lined up with its exit door, so that wall should not push it away.</summary>
        private bool IsLinedUpWithExit(AgentRuntime agent, WallSide side)
        {
            if (agent.ExitDoorIndex < 0)
            {
                return false;
            }

            DoorRuntime door = doors[agent.ExitDoorIndex];
            return door.Side == side && Math.Abs(AlongOffset(door, agent.Position)) <= door.Width / 2;
        }

        // ---------------------------------------------------------------- escape (phase 6)

        /// <summary>Anyone far enough out through an open door has escaped and leaves the run.</summary>
        private void ResolveExits()
        {
            for (int i = 0; i < agents.Length; i++)
            {
                AgentRuntime agent = agents[i];
                if (agent.Participation != AgentParticipation.Participating || IsInsideRoom(agent.Position))
                {
                    continue;
                }

                for (int d = 0; d < doors.Length; d++)
                {
                    DoorRuntime door = doors[d];
                    if (door.State == DoorState.Open &&
                        OutsideDistance(door, agent.Position) >= scenario.EscapeDepthMillimetres &&
                        Math.Abs(AlongOffset(door, agent.Position)) <= door.Width / 2)
                    {
                        agent.Participation = AgentParticipation.NoLongerParticipating;
                        agent.Outcome = AgentTerminalOutcome.Escaped;
                        eventLog.Append(tick, agent.Id, FireReactionEventType.AgentEscaped, agent.Position,
                            0, 0, door.OpenedEventId);
                        break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- panicked people and doors

        /// <summary>
        /// Picks the door to run for, or -1 when every door has failed this
        /// person recently. People can see an open doorway, but a closed door
        /// looks the same to them locked or not; they remember a door that
        /// would not open for a while.
        /// </summary>
        private int ChooseExitDoor(AgentRuntime agent)
        {
            int best = -1;
            long bestScore = long.MinValue;
            long inFire = (long)scenario.DangerDistanceMillimetres * scenario.DangerDistanceMillimetres;
            for (int d = 0; d < doors.Length; d++)
            {
                DoorRuntime door = doors[d];
                bool open = door.State == DoorState.Open;
                if (!open && tick < agent.DoorAvoidUntilTick[d])
                {
                    continue;
                }

                LogicalPosition approach = DoorPoint(door, 0, -DoorApproachInset);
                long score = random.NextIntInclusive(0, DoorChoiceNoise) - Distance(agent.Position, approach);
                if (open)
                {
                    score += DoorOpenBonus;
                }

                if (d == agent.ExitDoorIndex)
                {
                    score += DoorCurrentChoiceBonus;
                }

                if (RoutePassesNearFire(agent.Position, approach))
                {
                    score -= EscapeRoutePenalty;
                }

                if (NearestFireDistanceSquared(approach) < inFire)
                {
                    score -= DoorInFirePenalty;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }

            return best;
        }

        /// <summary>
        /// Where to run for the chosen door: just inside it, or, once it is
        /// open and the agent is lined up with the gap, out through it.
        /// </summary>
        private LogicalPosition DoorTarget(AgentRuntime agent)
        {
            DoorRuntime door = doors[agent.ExitDoorIndex];
            if (door.State == DoorState.Open &&
                (!IsInsideRoom(agent.Position) ||
                 Math.Abs(AlongOffset(door, agent.Position)) <= door.Width / 2 - scenario.OccupancyRadiusMillimetres + 50))
            {
                return DoorPoint(door, 0, DoorOutsideTarget);
            }

            return DoorPoint(door, 0, -DoorApproachInset);
        }

        /// <summary>True while the agent is close to the door it is running for.</summary>
        private bool IsNearExit(AgentRuntime agent, int distance)
        {
            if (agent.ExitDoorIndex < 0)
            {
                return false;
            }

            long limit = distance;
            return LogicalPosition.DistanceSquared(agent.Position, DoorPoint(doors[agent.ExitDoorIndex], 0, 0)) <
                   limit * limit;
        }

        /// <summary>Reached a closed door: try the handle.</summary>
        private bool HasReachedClosedExit(AgentRuntime agent)
        {
            if (agent.ExitDoorIndex < 0 || doors[agent.ExitDoorIndex].State == DoorState.Open)
            {
                return false;
            }

            DoorRuntime door = doors[agent.ExitDoorIndex];
            long arrival = DoorArrivalDistance;
            long reach = DoorApproachInset + DoorArrivalDistance / 2;
            return LogicalPosition.DistanceSquared(agent.Position, DoorPoint(door, 0, -DoorApproachInset)) < arrival * arrival ||
                   LogicalPosition.DistanceSquared(agent.Position, DoorPoint(door, 0, 0)) < reach * reach;
        }

        private void StartDoorAttempt(AgentRuntime agent)
        {
            DoorRuntime door = doors[agent.ExitDoorIndex];
            agent.BlockedTicks = 0;
            agent.DoorAttemptEventId = eventLog.Append(
                tick,
                agent.Id,
                FireReactionEventType.AgentTriedDoor,
                DoorPoint(door, 0, 0),
                0,
                0,
                agent.ScaredEventId).EventId;
            if (door.State == DoorState.Unlocked)
            {
                agent.Activity = AgentActivityState.OpeningDoor;
                agent.ActivityEndTick = checked(tick + scenario.DoorOpenTicks);
            }
            else
            {
                agent.Activity = AgentActivityState.TryingDoor;
                agent.ActivityEndTick = checked(tick + scenario.DoorTryTicks);
            }
        }

        /// <summary>
        /// Opening, rattling or forcing a door. Returns true while the person
        /// is still busy at the door this tick.
        /// </summary>
        private bool UpdateDoorAttempt(AgentRuntime agent, bool inDanger)
        {
            int doorIndex = agent.ExitDoorIndex;
            DoorRuntime door = doors[doorIndex];
            LogicalPosition doorCentre = DoorPoint(door, 0, 0);
            ApplyBody(agent, HeadingBetween(agent.Position, doorCentre, agent.Heading), 0,
                agent.PanicTurnRate, scenario.PanicAcceleration);

            if (door.State == DoorState.Open)
            {
                // Someone else got it open: go.
                agent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            if (inDanger)
            {
                // The fire is too close to stand here: run for it.
                agent.Activity = AgentActivityState.Fleeing;
                agent.DoorAvoidUntilTick[doorIndex] = checked(tick + random.NextIntInclusive(
                    scenario.DoorCrowdedAvoidMinimumTicks, scenario.DoorCrowdedAvoidMaximumTicks));
                agent.ExitDoorIndex = -1;
                agent.NextPanicDecisionTick = tick;
                return false;
            }

            if (door.State == DoorState.Unlocked && agent.Activity != AgentActivityState.OpeningDoor)
            {
                // Unlocked while they were rattling it: it opens at once.
                OpenDoor(door, agent.DoorAttemptEventId);
                agent.Activity = AgentActivityState.Fleeing;
                return false;
            }

            switch (agent.Activity)
            {
                case AgentActivityState.OpeningDoor:
                    if (tick >= agent.ActivityEndTick)
                    {
                        OpenDoor(door, agent.DoorAttemptEventId);
                        agent.Activity = AgentActivityState.Fleeing;
                        return false;
                    }

                    return true;

                case AgentActivityState.TryingDoor:
                    if (tick < agent.ActivityEndTick)
                    {
                        return true;
                    }

                    if (random.NextPercent(scenario.DoorForceChancePercent))
                    {
                        agent.Activity = AgentActivityState.ForcingDoor;
                        agent.ActivityEndTick = checked(tick + random.NextIntInclusive(
                            scenario.DoorForceMinimumTicks, scenario.DoorForceMaximumTicks));
                        agent.NextShoveTick = tick + 1;
                    }
                    else
                    {
                        GiveUpOnDoor(agent);
                    }

                    return true;

                default:
                    if (tick >= agent.NextShoveTick)
                    {
                        // A shoulder into the door: a thud, and nothing gives.
                        CausalEvent shove = eventLog.Append(
                            tick,
                            agent.Id,
                            FireReactionEventType.AgentForcedDoor,
                            doorCentre,
                            scenario.BumpSoundRadiusMillimetres,
                            0,
                            agent.DoorAttemptEventId);
                        EmitSound(agent.Id, doorCentre, scenario.BumpSoundRadiusMillimetres, 0, shove.EventId);
                        agent.NextShoveTick = checked(tick + random.NextIntInclusive(
                            scenario.DoorShoveMinimumTicks, scenario.DoorShoveMaximumTicks));
                    }

                    if (tick >= agent.ActivityEndTick)
                    {
                        GiveUpOnDoor(agent);
                    }

                    return true;
            }
        }

        /// <summary>
        /// This door will not open: remember that for a while, glance toward
        /// the next way out, then run for it.
        /// </summary>
        private void GiveUpOnDoor(AgentRuntime agent)
        {
            int doorIndex = agent.ExitDoorIndex;
            DoorRuntime door = doors[doorIndex];
            eventLog.Append(tick, agent.Id, FireReactionEventType.AgentGaveUpOnDoor, DoorPoint(door, 0, 0),
                0, 0, agent.DoorAttemptEventId);
            agent.DoorAvoidUntilTick[doorIndex] = checked(tick + random.NextIntInclusive(
                scenario.DoorAvoidMinimumTicks, scenario.DoorAvoidMaximumTicks));
            agent.ExitDoorIndex = -1;

            int next = ChooseExitDoor(agent);
            agent.Activity = AgentActivityState.Hesitating;
            agent.ActivityEndTick = checked(tick + random.NextIntInclusive(
                DoorGiveUpGlanceMinimumTicks, DoorGiveUpGlanceMaximumTicks));
            if (next >= 0)
            {
                agent.LookHeading = HeadingBetween(agent.Position, DoorPoint(doors[next], 0, -DoorApproachInset), agent.Heading);
            }
            else
            {
                int side = random.NextIntInclusive(0, 1) == 0 ? -1 : 1;
                agent.LookHeading = IntegerMath.NormalizeDegrees(agent.Heading + side * random.NextIntInclusive(90, 150));
            }
        }

        /// <summary>Stuck in the crowd on the way to a door: try another for a little while.</summary>
        private void AvoidCrowdedExit(AgentRuntime agent)
        {
            if (agent.ExitDoorIndex < 0)
            {
                return;
            }

            agent.DoorAvoidUntilTick[agent.ExitDoorIndex] = checked(tick + random.NextIntInclusive(
                scenario.DoorCrowdedAvoidMinimumTicks, scenario.DoorCrowdedAvoidMaximumTicks));
        }

        private static bool IsAtDoor(AgentRuntime agent)
        {
            return agent.Activity == AgentActivityState.OpeningDoor ||
                   agent.Activity == AgentActivityState.TryingDoor ||
                   agent.Activity == AgentActivityState.ForcingDoor;
        }

        private FireReactionDoorSnapshot ToSnapshot(DoorRuntime door)
        {
            return new FireReactionDoorSnapshot(door.Id, door.Side, DoorPoint(door, 0, 0), door.Width, door.State);
        }

        private FireReactionDoorSnapshot[] GetDoorSnapshots()
        {
            var snapshots = new FireReactionDoorSnapshot[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                snapshots[i] = ToSnapshot(doors[i]);
            }

            return snapshots;
        }
    }
}

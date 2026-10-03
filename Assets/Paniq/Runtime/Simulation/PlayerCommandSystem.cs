using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Everything the player does, and the only way it reaches the run. A
    /// command is queued for a tick that has not started yet, validated as it
    /// is queued, and consumed at the start of its target tick in the order it
    /// was queued. The whole queue is kept, so replaying the same commands on
    /// the same seed gives the same run.
    /// <para>
    /// This system holds the queue but decides very little: each command is
    /// carried out by the system that owns those rules (doors by
    /// <see cref="DoorSystem"/>, the hand by <see cref="InfluenceSystem"/>,
    /// and so on), wired up after construction because those systems are
    /// built later. Everything is free: the purse and the cards were
    /// deleted on 2026-10-03.
    /// </para>
    /// </summary>
    internal sealed class PlayerCommandSystem : IBindable
    {
        private readonly SimulationContext context;
        private readonly List<PlayerCommand> pending = new List<PlayerCommand>();
        private readonly List<PlayerCommand> history = new List<PlayerCommand>();
        private long nextSequence = 1L;

        private DoorSystem doors;
        private PhysicsObjectSystem objects;
        private Crowd crowd;
        private RoundSystem round;
        private AlarmSystem alarms;
        private NudgeSystem nudges;
        private InfluenceSystem influence;
        private HandChargeSystem handCharge;
        private TugSystem tugs;
        private FearSystem fear;

        public PlayerCommandSystem(SimulationContext context)
        {
            this.context = context;
        }

        /// <summary>Every system a command reaches into is built after this one, so they are handed over once everything exists.</summary>
        public void Bind(Systems systems)
        {
            round = systems.Round;
            alarms = systems.Alarms;
            doors = systems.Doors;
            objects = systems.Objects;
            crowd = systems.Crowd;
            nudges = systems.Nudges;
            influence = systems.Influence;
            handCharge = systems.HandCharge;
            tugs = systems.Tugs;
            fear = systems.Fear;
        }

        /// <summary>Every command queued so far, in sequence order.</summary>
        public IReadOnlyList<PlayerCommand> Commands => history;

        /// <summary>
        /// Queues a player action for a tick that has not started yet. Commands
        /// for the same tick run in the order they were queued. Throws if the
        /// tick has already started, or if the command names something that is
        /// not there.
        /// </summary>
        public PlayerCommand Queue(PlayerCommandType commandType, SimulationId targetId, LogicalPosition point, int targetTick)
        {
            if (targetTick <= context.Tick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick),
                    $"Tick {targetTick} has already started; the next tick is {context.Tick + 1}.");
            }

            Validate(commandType, targetId);
            var command = new PlayerCommand(targetTick, nextSequence++, commandType, targetId, point);
            pending.Add(command);
            history.Add(command);
            return command;
        }

        /// <summary>
        /// Refuses a command that names something the run does not have. Whether
        /// a card can actually be played where it points is decided when it is
        /// consumed, not here, because the world will have moved on by then.
        /// </summary>
        private void Validate(PlayerCommandType commandType, SimulationId targetId)
        {
            switch (commandType)
            {
                case PlayerCommandType.ClickDoor:
                case PlayerCommandType.ToggleLock:
                    if (doors.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown door ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.InfluenceDoor:
                case PlayerCommandType.RepelDoor:
                    if (doors.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown door ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.InfluenceThing:
                case PlayerCommandType.RepelThing:
                    if (objects.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown thing ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.InfluenceSpot:
                case PlayerCommandType.RepelSpot:
                case PlayerCommandType.ReleaseInfluence:
                case PlayerCommandType.LeaveInfluence:
                case PlayerCommandType.MoveInfluence:
                case PlayerCommandType.SetHandStrength:
                case PlayerCommandType.SetHandReach:
                    break;
                case PlayerCommandType.NudgePerson:
                case PlayerCommandType.NudgePersonFrom:
                case PlayerCommandType.TugPerson:
                case PlayerCommandType.ReleaseTug:
                    if (crowd.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown person ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.PullAlarm:
                    if (alarms.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown alarm ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.TriggerEvent:
                case PlayerCommandType.SetCrowdPanicked:
                case PlayerCommandType.SetCrowdCalm:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(commandType), $"Unknown command type {commandType}.");
            }
        }

        /// <summary>Phase 1: the commands for this tick, in sequence order.</summary>
        public void Consume()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PlayerCommand command = pending[i];
                if (command.TargetTick != context.Tick)
                {
                    continue;
                }

                pending.RemoveAt(i);
                i--;
                Carry(command);
            }
        }

        private void Carry(PlayerCommand command)
        {
            if (command.CommandType == PlayerCommandType.ClickDoor)
            {
                doors.ClickDoor(doors.IndexOf(command.TargetId));
                return;
            }

            // The key is the player's to turn (2026-09-25).
            if (command.CommandType == PlayerCommandType.ToggleLock)
            {
                doors.ToggleLock(doors.IndexOf(command.TargetId));
                return;
            }

            // The player pulls a fire alarm: nothing, once the bells ring.
            if (command.CommandType == PlayerCommandType.PullAlarm)
            {
                alarms.PullByPlayer(alarms.IndexOf(command.TargetId));
                return;
            }

            // A nudge (prototype 3, 2026-09-25): nothing
            // at all to somebody already out of the building or dead.
            if (command.CommandType == PlayerCommandType.NudgePerson ||
                command.CommandType == PlayerCommandType.NudgePersonFrom)
            {
                Agent nudged = crowd.All[crowd.IndexOf(command.TargetId)];
                if (nudged.IsParticipating)
                {
                    nudges.Nudge(nudged, command.Point, command.CommandType == PlayerCommandType.NudgePersonFrom);
                }

                return;
            }

            // Influence (2026-09-26): free, not a card. Since 2026-09-29 a
            // press is the hand going on a place, full at once, and the
            // release is it coming off. A spot off the floor is no place at
            // all, and nothing is written.
            // Since 2026-09-30 the right button's hand is the same hand the
            // other way round: it pushes people away from the place.
            bool repels = command.CommandType == PlayerCommandType.RepelDoor ||
                          command.CommandType == PlayerCommandType.RepelThing ||
                          command.CommandType == PlayerCommandType.RepelSpot;
            bool press = repels || command.CommandType == PlayerCommandType.InfluenceDoor ||
                         command.CommandType == PlayerCommandType.InfluenceThing ||
                         command.CommandType == PlayerCommandType.InfluenceSpot ||
                         command.CommandType == PlayerCommandType.TugPerson;
            if (press && !handCharge.MayPress)
            {
                // The hand's charge has run dry (2026-09-30): nothing is
                // taken until the bar has rested, and nothing is written,
                // as for a press off the floor. Letting go always goes through.
                return;
            }

            if (command.CommandType == PlayerCommandType.InfluenceDoor || command.CommandType == PlayerCommandType.RepelDoor)
            {
                influence.OnDoor(doors.IndexOf(command.TargetId), command.TargetId, repels);
                return;
            }

            if (command.CommandType == PlayerCommandType.InfluenceThing || command.CommandType == PlayerCommandType.RepelThing)
            {
                int thing = objects.IndexOf(command.TargetId);
                if (!objects.IsDormant(thing))
                {
                    influence.OnThing(thing, command.TargetId, objects.PositionOf(thing), repels);
                }

                return;
            }

            if (command.CommandType == PlayerCommandType.InfluenceSpot || command.CommandType == PlayerCommandType.RepelSpot)
            {
                influence.TryOnSpot(command.Point, repels);
                return;
            }

            if (command.CommandType == PlayerCommandType.ReleaseInfluence)
            {
                influence.Release();
                return;
            }

            // A click rather than a hold (2026-09-30): the place stays a
            // moment, then comes off by itself.
            if (command.CommandType == PlayerCommandType.LeaveInfluence)
            {
                influence.Leave();
                return;
            }

            // A hand held down and dragged (2026-09-30): it slides with the
            // pointer, and whoever answers it goes on answering it.
            if (command.CommandType == PlayerCommandType.MoveInfluence)
            {
                influence.Move(command.Point);
                return;
            }

            // The Tab panel's dials (2026-09-30): this run's own settings,
            // from the next tick on.
            if (command.CommandType == PlayerCommandType.SetHandStrength)
            {
                influence.SetStrength((int)command.Point.X);
                return;
            }

            if (command.CommandType == PlayerCommandType.SetHandReach)
            {
                influence.SetReach((int)command.Point.X);
                return;
            }

            // The tug (2026-09-29): free, not a card, and nothing at all to
            // somebody already out of the building or dead.
            if (command.CommandType == PlayerCommandType.TugPerson)
            {
                tugs.Tug(crowd.All[crowd.IndexOf(command.TargetId)]);
                return;
            }

            if (command.CommandType == PlayerCommandType.ReleaseTug)
            {
                tugs.Release(crowd.All[crowd.IndexOf(command.TargetId)]);
                return;
            }

            // Setting the disaster going.
            if (command.CommandType == PlayerCommandType.TriggerEvent)
            {
                round.TriggerEvent();
                return;
            }

            // The crowd switch (2026-10-01): free, not a card, and the whole
            // building at once -- each person a few ticks after the next, by
            // the fear system's own stagger. "Panicked" also counts as the
            // round beginning, so a test level that empties ends with a
            // score; "calm" silences the bells as well, or they would
            // frighten everybody straight back.
            if (command.CommandType == PlayerCommandType.SetCrowdPanicked)
            {
                ulong flicked = context.Events.Append(context.Tick, default, CausalEventType.PowerPanickedCrowd,
                    default).EventId;
                round.Begin(flicked);
                fear.PanicEveryone(flicked, crowd.All);
                return;
            }

            if (command.CommandType == PlayerCommandType.SetCrowdCalm)
            {
                ulong flicked = context.Events.Append(context.Tick, default, CausalEventType.PowerCalmedCrowd,
                    default).EventId;
                alarms.Silence(flicked);
                fear.CalmEveryone(flicked, crowd.All);
                return;
            }

        }
    }
}

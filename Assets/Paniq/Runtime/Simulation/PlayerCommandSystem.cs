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
    /// <see cref="DoorSystem"/>, fire by <see cref="FireSystem"/>, and so on),
    /// wired up after construction because those systems are built later. A
    /// card the player cannot afford, or that cannot be played where they
    /// pointed, does nothing and costs nothing.
    /// </para>
    /// </summary>
    internal sealed class PlayerCommandSystem
    {
        private readonly SimulationContext context;
        private readonly List<PlayerCommand> pending = new List<PlayerCommand>();
        private readonly List<PlayerCommand> history = new List<PlayerCommand>();
        private long nextSequence = 1L;

        private DoorSystem doors;
        private FireSystem fire;
        private PhysicsObjectSystem objects;
        private Crowd crowd;
        private InfluenceSystem influence;
        private SoundSystem sound;
        private BodySystem body;
        private WorldGeometry geometry;
        private RoundSystem round;

        public PlayerCommandSystem(SimulationContext context)
        {
            this.context = context;
        }

        /// <summary>Wired up after construction, because these are all built after this system.</summary>
        public void Use(DoorSystem doorSystem, FireSystem fireSystem, PhysicsObjectSystem physicsObjects, Crowd people,
            InfluenceSystem influenceSystem, SoundSystem soundSystem, BodySystem bodySystem, WorldGeometry world,
            RoundSystem theRound)
        {
            round = theRound;
            doors = doorSystem;
            fire = fireSystem;
            objects = physicsObjects;
            crowd = people;
            influence = influenceSystem;
            sound = soundSystem;
            body = bodySystem;
            geometry = world;
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
                    if (doors.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown door ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.PlayBeefcake:
                    if (crowd.IndexOf(targetId) < 0)
                    {
                        throw new ArgumentException($"Unknown person ID {targetId}.", nameof(targetId));
                    }

                    break;
                case PlayerCommandType.SpawnFire:
                case PlayerCommandType.SpawnExtinguisher:
                case PlayerCommandType.BlastWall:
                case PlayerCommandType.TriggerEvent:
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

            // Setting the disaster going is not a card: it costs nothing, so
            // it is dealt with before the purse is consulted at all.
            if (command.CommandType == PlayerCommandType.TriggerEvent)
            {
                round.TriggerEvent();
                return;
            }

            // A card the player cannot pay for does nothing at all.
            if (!influence.CanAfford(command.CommandType))
            {
                return;
            }

            bool played;
            switch (command.CommandType)
            {
                case PlayerCommandType.PlayBeefcake:
                    played = PlayBeefcake(command);
                    break;
                case PlayerCommandType.SpawnFire:
                    played = SpawnFire(command);
                    break;
                case PlayerCommandType.SpawnExtinguisher:
                    played = SpawnExtinguisher(command);
                    break;
                case PlayerCommandType.BlastWall:
                    played = BlastWall(command);
                    break;
                default:
                    played = false;
                    break;
            }

            if (played)
            {
                influence.Spend(command.CommandType);
            }
        }

        /// <summary>
        /// Beefcake: as strong as a person can be, for good. Nothing else about
        /// them changes, and because traits are read when they are used the very
        /// next tick already has them shouldering doors off their hinges.
        /// </summary>
        private bool PlayBeefcake(PlayerCommand command)
        {
            Agent agent = crowd.All[crowd.IndexOf(command.TargetId)];
            if (!agent.IsParticipating || agent.Traits.Strength >= AgentTraitValues.Maximum)
            {
                // Gone, or already as strong as they can get.
                return false;
            }

            agent.Traits = agent.Traits.WithStrength(AgentTraitValues.Maximum);
            context.Events.Append(context.Tick, agent.Id, FireReactionEventType.PowerBeefcake, agent.Body.Position,
                influence.CostOf(command.CommandType), 0, 0UL, agent.Id);
            return true;
        }

        /// <summary>A fire where the player pointed, if that square is floor, dry and not already alight.</summary>
        private bool SpawnFire(PlayerCommand command)
        {
            // Asked before anything is written down, because the causal log is
            // append-only: a card that cannot be played leaves no trace.
            int cell = fire.CellCovering(command.Point);
            if (!fire.CanIgniteForPlayer(cell))
            {
                return false;
            }

            CausalEvent card = context.Events.Append(context.Tick, default, FireReactionEventType.PowerSpawnedFire,
                command.Point, influence.CostOf(command.CommandType));
            fire.TryIgniteForPlayer(cell, card.EventId, out ulong _);
            return true;
        }

        /// <summary>
        /// TNT: a hole through the wall the player pointed at, and the bang that
        /// goes with it. The order is fixed so the run repeats: the hole first,
        /// then the noise, then the loose things flung away from it, then the
        /// people knocked over.
        /// </summary>
        private bool BlastWall(PlayerCommand command)
        {
            ulong blasted = doors.TryBlastWall(command.Point, influence.CostOf(command.CommandType));
            if (blasted == 0UL)
            {
                return false;
            }

            BlastSettings blast = context.Scenario.Blast;
            sound.Bang(default, command.Point, blast.BangHearingRadiusMillimetres, blast.BangAlarmRadiusMillimetres, blasted);
            objects.FlingFrom(command.Point, blast.ThrowRadiusMillimetres, blast.ThrowSpeedMillimetresPerTick, -1, blasted);

            Agent[] people = crowd.All;
            long radius = blast.KnockDownRadiusMillimetres;
            for (int i = 0; i < people.Length; i++)
            {
                Agent agent = people[i];
                if (!agent.IsParticipating ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, command.Point) > radius * radius)
                {
                    continue;
                }

                int away = IntegerMath.HeadingBetween(command.Point, agent.Body.Position, agent.Body.Heading);
                body.BlowOver(agent, away,
                    blast.ShoveDistanceMillimetres * context.Scenario.PhysicsFeel.BlastStrengthPercent / 100,
                    context.Scenario.PhysicsFeel.BlastLiftPercent, blasted);
            }

            return true;
        }

        /// <summary>A full extinguisher stood on clear floor where the player pointed.</summary>
        private bool SpawnExtinguisher(PlayerCommand command)
        {
            if (!objects.TryPlaceSpareExtinguisher(command.Point, out int index))
            {
                return false;
            }

            context.Events.Append(context.Tick, objects.IdOf(index),
                FireReactionEventType.PowerSpawnedExtinguisher, command.Point,
                influence.CostOf(command.CommandType), 0, 0UL, objects.IdOf(index));
            OfferItToWhoeverCanSeeIt(command.Point);
            return true;
        }

        /// <summary>
        /// Everybody in the same room, within sight of where the bottle was put
        /// down, notices it. For a while afterwards they need less nerve than
        /// usual to go and take it, so standing one in front of a frightened
        /// office reads as handing it to them rather than as set dressing.
        /// Ascending ID order, and no random draw, so a replay agrees.
        /// </summary>
        private void OfferItToWhoeverCanSeeIt(LogicalPosition spot)
        {
            ExtinguisherSettings settings = context.Scenario.Extinguishers;
            int room = geometry.RoomAtPoint(spot);
            long reach = settings.OfferedNoticeRangeMillimetres;
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                if (!agent.IsParticipating || agent.Burning.IsBurning ||
                    geometry.RoomOf(agent) != room ||
                    LogicalPosition.DistanceSquared(agent.Body.Position, spot) > reach * reach)
                {
                    continue;
                }

                agent.Carry.SawAnExtinguisherUntilTick = checked(context.Tick + settings.OfferedTicks);
            }
        }
    }
}

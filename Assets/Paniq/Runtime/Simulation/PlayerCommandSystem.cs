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
    internal sealed class PlayerCommandSystem : IBindable
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
        private DeckSystem deck;
        private SoundSystem sound;
        private BodySystem body;
        private WorldGeometry geometry;
        private RoundSystem round;
        private PowerSystem power;

        public PlayerCommandSystem(SimulationContext context)
        {
            this.context = context;
        }

        /// <summary>Every system a command reaches into is built after this one, so they are handed over once everything exists.</summary>
        public void Bind(Systems systems)
        {
            round = systems.Round;
            power = systems.Power;
            doors = systems.Doors;
            fire = systems.Fire;
            objects = systems.Objects;
            crowd = systems.Crowd;
            influence = systems.Influence;
            deck = systems.Deck;
            sound = systems.Sound;
            body = systems.Body;
            geometry = systems.Geometry;
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
                // Every card below names a place, not a thing, so there is
                // nothing to check here: whether the throw caught anybody is
                // decided when it lands, because the world will have moved on
                // by then. Beefcake used to name a person and be checked here;
                // it is thrown at a patch like the rest of them now.
                case PlayerCommandType.PlayBeefcake:
                case PlayerCommandType.PlayCourage:
                case PlayerCommandType.PlayTerror:
                case PlayerCommandType.PlayBastard:
                case PlayerCommandType.PlayColdHeart:
                case PlayerCommandType.SpawnFire:
                case PlayerCommandType.SpawnExtinguisher:
                case PlayerCommandType.BlastWall:
                case PlayerCommandType.TriggerEvent:
                case PlayerCommandType.PopFuseBox:
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
                // A door is priced by what the click would do to it -- turning
                // the key, walking it open, pulling it shut -- rather than by
                // the command, so its cost is asked for here and not from the
                // card table. Same rule as a card: a click they cannot pay for,
                // or one the door refuses, does nothing and costs nothing.
                int door = doors.IndexOf(command.TargetId);
                int price = influence.CostOfDoorClick(doors.StateOf(door));
                if (!influence.CanAfford(price))
                {
                    return;
                }

                if (doors.ClickDoor(door))
                {
                    influence.Spend(price);
                }

                return;
            }

            // Setting the disaster going is not a card: it costs nothing, so
            // it is dealt with before the purse is consulted at all.
            if (command.CommandType == PlayerCommandType.TriggerEvent)
            {
                round.TriggerEvent();
                return;
            }

            // A card they are not holding is not theirs to play. Cards are not
            // bought -- the dead deal them -- so having the influence for one is
            // only half of being able to play it.
            if (!deck.Holds(command.CommandType))
            {
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
                case PlayerCommandType.PlayCourage:
                case PlayerCommandType.PlayTerror:
                case PlayerCommandType.PlayBastard:
                case PlayerCommandType.PlayColdHeart:
                    played = PlayTraitCard(command);
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
                case PlayerCommandType.PopFuseBox:
                    played = PopFuseBox(command);
                    break;
                default:
                    played = false;
                    break;
            }

            // Only a card that actually did something is paid for, and only a
            // card that is paid for leaves the hand. A throw that caught
            // nobody was a miss: it costs neither the influence nor the card.
            if (played)
            {
                influence.Spend(command.CommandType);
                deck.Discard(command.CommandType);
            }
        }

        /// <summary>
        /// What each trait card does: which dial it moves, which end it moves
        /// it to, and what the log calls it. Every one of them is thrown at a
        /// patch of floor rather than at a chosen person.
        /// </summary>
        private static bool DialOf(
            PlayerCommandType card, out AgentTrait trait, out int end, out CausalEventType logged)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake:
                    trait = AgentTrait.Strength;
                    end = AgentTraitValues.Maximum;
                    logged = CausalEventType.PowerBeefcake;
                    return true;
                case PlayerCommandType.PlayCourage:
                    trait = AgentTrait.Bravery;
                    end = AgentTraitValues.Maximum;
                    logged = CausalEventType.PowerCourage;
                    return true;
                case PlayerCommandType.PlayTerror:
                    trait = AgentTrait.Nervousness;
                    end = AgentTraitValues.Maximum;
                    logged = CausalEventType.PowerTerror;
                    return true;
                case PlayerCommandType.PlayBastard:
                    trait = AgentTrait.Evil;
                    end = AgentTraitValues.Maximum;
                    logged = CausalEventType.PowerBastard;
                    return true;
                case PlayerCommandType.PlayColdHeart:
                    trait = AgentTrait.Compassion;
                    end = AgentTraitValues.Minimum;
                    logged = CausalEventType.PowerColdHeart;
                    return true;
                default:
                    trait = AgentTrait.Strength;
                    end = 0;
                    logged = default;
                    return false;
            }
        }

        /// <summary>
        /// A trait card, thrown at a patch of floor: everybody standing inside
        /// it has that one dial slammed to the end of its scale, for the rest
        /// of the round. Nothing else about them changes, and because traits
        /// are read when they are used rather than cached, the very next tick
        /// already has them behaving like the person they have become.
        /// <para>
        /// It counts as played -- and so is paid for, and leaves the hand -- if
        /// it moved anybody's dial. A throw that catches nobody is a miss, and
        /// so is one that catches four people who were all at that end
        /// already: the established rule is that a card which does nothing is
        /// free. A throw that catches the wrong person is spent, which is the
        /// whole of the player's accuracy.
        /// </para>
        /// <para>
        /// The crowd is walked in ascending order (which is the order
        /// <see cref="Crowd.Within"/> reports), so the events this writes go
        /// into the log in the same order on every replay of a seed.
        /// </para>
        /// </summary>
        private bool PlayTraitCard(PlayerCommand command)
        {
            if (!DialOf(command.CommandType, out AgentTrait trait, out int end, out CausalEventType logged))
            {
                return false;
            }

            long radius = context.Scenario.Influence.CardPatchRadiusMillimetres;
            int price = influence.CostOf(command.CommandType);
            bool caught = false;

            using (Crowd.Nearby inside = crowd.Within(command.Point, radius))
            {
                for (int i = 0; i < inside.Count; i++)
                {
                    Agent agent = crowd.All[inside[i]];
                    if (!agent.IsParticipating || agent.Traits.Of(trait) == end)
                    {
                        // Gone, or that dial is already where this card would
                        // put it.
                        continue;
                    }

                    // The index gathers a box, not a circle, so the corners
                    // have to be turned down by hand or the patch would catch
                    // people 2.1 m away on the diagonal.
                    if (LogicalPosition.DistanceSquared(agent.Body.Position, command.Point) > radius * radius)
                    {
                        continue;
                    }

                    agent.Traits = agent.Traits.With(trait, end);
                    context.Events.Append(
                        context.Tick, agent.Id, logged, agent.Body.Position, price, 0, 0UL, agent.Id);
                    caught = true;
                }
            }

            return caught;
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

            CausalEvent card = context.Events.Append(context.Tick, default, CausalEventType.PowerSpawnedFire,
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
        /// <summary>
        /// Pop the fuse box by hand. The spark then runs the other way, out of
        /// the maintenance room and along the line of sockets, popping each in
        /// turn -- which costs no extra rules, because a run of cable has no
        /// direction of its own.
        /// <para>
        /// Refused, at no cost and with nothing written down, when there is no
        /// fuse box near where the player pointed or it has already gone. The
        /// reach is checked before anything is written, because the log only
        /// ever grows and must not record something that did not happen.
        /// </para>
        /// </summary>
        private bool PopFuseBox(PlayerCommand command)
        {
            if (!power.CanPopTheFuseBoxNear(command.Point))
            {
                return false;
            }

            ulong played = context.Events.Append(context.Tick, default,
                CausalEventType.PowerPoppedFuseBox, command.Point,
                influence.CostOf(command.CommandType), 0, 0UL).EventId;
            power.PopTheFuseBoxNear(command.Point, played);
            return true;
        }

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

            long radius = blast.KnockDownRadiusMillimetres;
            using (Crowd.Nearby people = crowd.Within(command.Point, radius))
            {
                for (int c = 0; c < people.Count; c++)
                {
                    Agent agent = crowd.All[people[c]];
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
                CausalEventType.PowerSpawnedExtinguisher, command.Point,
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
            using Crowd.Nearby near = crowd.Within(spot, reach);
            for (int c = 0; c < near.Count; c++)
            {
                Agent agent = crowd.All[near[c]];
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

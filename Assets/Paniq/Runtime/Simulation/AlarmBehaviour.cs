namespace Paniq.Simulation
{
    /// <summary>
    /// Raising the alarm. Somebody who has taken in that there is a fire, who
    /// is brave, thinks of other people or is used to being listened to, and
    /// who is not in immediate danger, breaks off to hit the alarm on the wall
    /// of the room they are in before running. Everyone else leaves it to
    /// somebody else.
    /// <para>
    /// One of the frightened's options (<see cref="ITaskOption"/>): taken up
    /// at a decision moment, carried on every tick after.
    /// </para>
    /// </summary>
    internal sealed class AlarmBehaviour : ITaskOption, IBindable
    {
        private readonly SimulationContext context;

        /// <summary>The player's hand, built after this: a hand on a pull station makes the brave pull it sooner (2026-09-29).</summary>
        private InfluenceSystem influence;

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
            tells = systems.Tells;
        }

        /// <summary>The wind-up before going back toward the flames (2026-09-30).</summary>
        private TellSystem tells;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly WorldGeometry geometry;
        private readonly AlarmSystem alarms;
        private readonly Locomotion locomotion;
        private readonly FrightenedWalk walk;
        private readonly AlarmSettings settings;
        private readonly PanicSettings panic;

        public AlarmBehaviour(SimulationContext context, WorldGeometry geometry, AlarmSystem alarms, Locomotion locomotion,
            FrightenedWalk walk)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.geometry = geometry;
            this.alarms = alarms;
            this.locomotion = locomotion;
            this.walk = walk;
            settings = context.Scenario.Alarm;
            panic = context.Scenario.Panic;
        }

        public static bool IsRaisingTheAlarm(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.GoingToAlarm || activity == AgentActivityState.PullingAlarm;
        }

        /// <summary>
        /// Who thinks of it: a leader, somebody who thinks of other people, or
        /// somebody brave (the owner asked for the brave, 2026-09-25). With
        /// the player's hand on the station and felt (2026-09-29), a little
        /// less bravery does.
        /// </summary>
        private bool WouldRaiseIt(Agent agent, bool pulledToIt)
        {
            if (agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Body.State != AgentBodyState.Upright)
            {
                return false;
            }

            int bravery = settings.PullMinimumBravery - (pulledToIt ? settings.PulledBraveryBonus : 0);
            return agent.Traits.Leadership >= settings.PullMinimumLeadership ||
                   agent.Traits.Compassion >= settings.PullMinimumCompassion ||
                   agent.Traits.Bravery >= bravery;
        }

        public bool IsDoing(Agent agent) => IsRaisingTheAlarm(agent);

        public MotorIntent? Continue(Agent agent, in Situation situation) => Update(agent, situation.InDanger);

        /// <summary>Bells silent, nothing in their hands, nobody in their care, on their feet, and out of the flames.</summary>
        public bool Wants(Agent agent, in Situation situation) =>
            !situation.InDanger && alarms.Enabled && !alarms.Ringing && agent.Carry.ItemIndex < 0 &&
            agent.Help.TargetIndex < 0 && agent.Body.State == AgentBodyState.Upright;

        /// <summary>Who thinks of it, which station, and off they go -- or nothing.</summary>
        public bool TryBegin(Agent agent, in Situation situation, out MotorIntent? first)
        {
            first = Begin(agent, situation);
            return first.HasValue;
        }

        private MotorIntent? Begin(Agent agent, in Situation situation)
        {
            bool inDanger = situation.InDanger;

            // The player's hand on a pull station, felt from here (2026-09-29):
            // it is worth going for from as far as the pull reaches, not only
            // the usual short walk, and takes a little less nerve.
            InfluenceSystem.Place pull = default;
            int felt = 0;
            int pulledStation = influence != null && influence.TryGetPull(agent, out pull, out felt)
                ? alarms.StationAt(influence, pull)
                : -1;
            bool pulledToIt = pulledStation >= 0 && felt > 0;

            // Driven hard (2026-09-30): they go whatever their nerve, and for
            // the station their goal is on (the owner: "agents acted upon
            // should be stuff they normally wouldn't").
            bool forTheHand = pulledToIt && felt >= context.Scenario.Influence.ActsAgainstNatureFromPerMille &&
                              influence.MayAnswer(agent);
            bool wouldAnyway = WouldRaiseIt(agent, pulledToIt);
            if (!wouldAnyway && !(forTheHand && agent.Carry.ItemIndex < 0 && agent.Help.TargetIndex < 0 &&
                                  agent.Body.State == AgentBodyState.Upright))
            {
                return null;
            }

            int alarm = forTheHand
                ? pulledStation
                : alarms.NearestUnpulledWithin(
                    agent.Body.Position,
                    geometry.RoomOf(agent),
                    geometry.Routes.ReachFrom(agent.Body.Position, bodyRadius),
                    geometry.Routes);
            if (alarm < 0 && pulledToIt)
            {
                alarm = pulledStation;
            }

            if (alarm < 0)
            {
                return null;
            }

            // Turning back (2026-09-30): a walk to a station past the flames
            // is wound up to first, unless the player's hand sent them.
            if (tells != null)
            {
                TellSystem.GoingBack going = tells.BeforeGoingBack(agent, alarms.PositionOf(alarm),
                    TellSystem.AlarmTarget(alarm), agent.Fear.ScaredEventId, forTheHand);
                if (going == TellSystem.GoingBack.Wait)
                {
                    return tells.StandIntent(agent);
                }

                if (going == TellSystem.GoingBack.Refuse)
                {
                    return null;
                }
            }

            if (forTheHand)
            {
                ulong press = pull.EventId;
                influence.Answer(agent, pull, felt);
                if (!WouldRaiseIt(agent, false))
                {
                    influence.ActedAgainstNature(agent, AgainstTheirNature.PulledTheAlarm, press, alarms.IdOf(alarm),
                        agent.Body.Position);
                }
            }

            agent.Alarm.AlarmIndex = alarm;
            agent.Intent.Activity = AgentActivityState.GoingToAlarm;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(settings.FetchTimeoutTicks));
            return Update(agent, inDanger);
        }

        /// <summary>Walking to the alarm, hitting it, and then getting on with running.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            int alarm = agent.Alarm.AlarmIndex;

            // The fire arrived, or they are off their feet: cut short.
            // Somebody else got there first: done, by them. Taking too long,
            // or hemmed in: it came to nothing.
            bool onTheWay = agent.Intent.Activity == AgentActivityState.GoingToAlarm;
            if (alarm < 0 || inDanger || !agent.Body.IsOnTheirFeet)
            {
                End(agent, TaskEnd.Interrupted);
                return null;
            }

            if (onTheWay && alarms.Ringing)
            {
                End(agent, TaskEnd.Done);
                return null;
            }

            if (onTheWay && (context.Tick >= agent.Intent.ActivityEndTick || agent.Body.BlockedTicks >= panic.BlockedGiveUpTicks))
            {
                End(agent, TaskEnd.GaveUp);
                return null;
            }

            LogicalPosition spot = alarms.PositionOf(alarm);
            if (agent.Intent.Activity == AgentActivityState.PullingAlarm)
            {
                if (context.Tick < agent.Intent.ActivityEndTick)
                {
                    // Reaching for it: standing still, facing the wall.
                    return FaceIt(agent, spot);
                }

                alarms.Pull(alarm, agent, agent.Fear.ScaredEventId);
                End(agent, TaskEnd.Done);
                return null;
            }

            long reach = settings.ArrivalMillimetres;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, spot) <= reach * reach)
            {
                agent.Intent.Activity = AgentActivityState.PullingAlarm;
                agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(settings.PressTicks));
                return FaceIt(agent, spot);
            }

            // Round what is in the way, and through the doors on the way
            // (2026-09-27): the alarm is chosen by how far it is to walk to
            // it, which may be through a doorway, and a shut door used to
            // stop them dead.
            if (!walk.TryStep(agent, spot, TraitEffects.FleeSpeed(agent), agent.Fear.ScaredEventId, out MotorIntent step))
            {
                End(agent, TaskEnd.GaveUp);
                return null;
            }

            return step;
        }

        private MotorIntent FaceIt(Agent agent, LogicalPosition spot)
        {
            int heading = IntegerMath.HeadingBetween(agent.Body.Position, spot, agent.Body.Heading);
            return PanicIntent.StandAndFace(agent, heading, panic);
        }

        /// <summary>Done with the alarm, one way or the other (<see cref="Tasks.End"/>): they pick a way out a beat later.</summary>
        private void End(Agent agent, TaskEnd how)
        {
            agent.Alarm.AlarmIndex = -1;
            if (IsRaisingTheAlarm(agent))
            {
                Tasks.End(agent, how, context, influence, walk);
            }
            else
            {
                walk.Forget(agent);
            }
        }
    }
}

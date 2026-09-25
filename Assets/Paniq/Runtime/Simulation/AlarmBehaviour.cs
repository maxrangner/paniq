namespace Paniq.Simulation
{
    /// <summary>
    /// Raising the alarm. Somebody who has taken in that there is a fire, who
    /// is brave, thinks of other people or is used to being listened to, and
    /// who is not in immediate danger, breaks off to hit the alarm on the wall
    /// of the room they are in before running. Everyone else leaves it to
    /// somebody else.
    /// <para>
    /// Built like <see cref="ExtinguisherBehaviour"/>: one step in the panic
    /// decision that returns what the body should do, or nothing at all when
    /// this person is not raising any alarm.
    /// </para>
    /// </summary>
    internal sealed class AlarmBehaviour : IPanicOption
    {
        private readonly SimulationContext context;

        /// <summary>How wide a person is, for asking which way round something to go.</summary>
        private readonly int bodyRadius;
        private readonly WorldGeometry geometry;
        private readonly AlarmSystem alarms;
        private readonly Locomotion locomotion;
        private readonly AlarmSettings settings;
        private readonly PanicSettings panic;

        public AlarmBehaviour(SimulationContext context, WorldGeometry geometry, AlarmSystem alarms, Locomotion locomotion)
        {
            this.context = context;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
            this.geometry = geometry;
            this.alarms = alarms;
            this.locomotion = locomotion;
            settings = context.Scenario.Alarm;
            panic = context.Scenario.Panic;
        }

        public static bool IsRaisingTheAlarm(Agent agent)
        {
            AgentActivityState activity = agent.Intent.Activity;
            return activity == AgentActivityState.GoingToAlarm || activity == AgentActivityState.PullingAlarm;
        }

        /// <summary>Who thinks of it: a leader, somebody who thinks of other people, or somebody brave (the owner asked for the brave, 2026-09-25).</summary>
        private bool WouldRaiseIt(Agent agent)
        {
            if (agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Body.State != AgentBodyState.Upright)
            {
                return false;
            }

            return agent.Traits.Leadership >= settings.PullMinimumLeadership ||
                   agent.Traits.Compassion >= settings.PullMinimumCompassion ||
                   agent.Traits.Bravery >= settings.PullMinimumBravery;
        }

        /// <summary>
        /// Considered in the panic decision. Returns no intent when this person
        /// is not going for an alarm.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsRaisingTheAlarm(agent))
            {
                return Update(agent, inDanger);
            }

            if (inDanger || alarms.Ringing || !alarms.Enabled || !WouldRaiseIt(agent))
            {
                return null;
            }

            int alarm = alarms.NearestUnpulledWithin(
                agent.Body.Position,
                geometry.RoomOf(agent),
                geometry.Routes.ReachFrom(agent.Body.Position, bodyRadius),
                geometry.Routes);
            if (alarm < 0)
            {
                return null;
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

            // Somebody else got there first, the fire arrived, or it is taking
            // too long: forget it and run.
            bool onTheWay = agent.Intent.Activity == AgentActivityState.GoingToAlarm;
            if (alarm < 0 || inDanger || !agent.Body.IsOnTheirFeet ||
                (onTheWay && alarms.Ringing) ||
                (onTheWay && context.Tick >= agent.Intent.ActivityEndTick) ||
                (onTheWay && agent.Body.BlockedTicks >= panic.BlockedGiveUpTicks))
            {
                GiveUp(agent);
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
                GiveUp(agent);
                return null;
            }

            long reach = settings.ArrivalMillimetres;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, spot) <= reach * reach)
            {
                agent.Intent.Activity = AgentActivityState.PullingAlarm;
                agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(settings.PressTicks));
                return FaceIt(agent, spot);
            }

            agent.Intent.Target = spot;

            // Round what is in the way. The alarm is chosen by how far it is to
            // walk to it, which may be through a doorway, so walking straight
            // at it would pick one it then cannot reach.
            int heading = geometry.Routes.HeadingToward(agent.Body.Position, spot, bodyRadius, agent.Body.Heading);
            heading = locomotion.Steer(agent, heading, TraitEffects.PanicPeopleAvoidPercent(agent, context.Scenario),
                panic.WallAvoidPercent, panic.ObjectAvoidPercent, 0L, 0L);
            return PanicIntent.WalkTowards(agent, heading, panic);
        }

        private MotorIntent FaceIt(Agent agent, LogicalPosition spot)
        {
            int heading = IntegerMath.HeadingBetween(agent.Body.Position, spot, agent.Body.Heading);
            return PanicIntent.StandAndFace(agent, heading, panic);
        }

        /// <summary>
        /// Done with the alarm, one way or the other. They pick a way out on the
        /// very next tick rather than standing at the wall they were just facing.
        /// </summary>
        private void GiveUp(Agent agent)
        {
            agent.Alarm.AlarmIndex = -1;
            if (IsRaisingTheAlarm(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
                context.ThinkAgainSoon(agent.Intent);
                agent.Body.BlockedTicks = 0;
            }
        }
    }
}

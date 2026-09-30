namespace Paniq.Simulation
{
    /// <summary>
    /// The frightened answer the player's hand (2026-09-30, the owner: "when
    /// panicked, the agents still run around too much"). Somebody frightened
    /// whose goal is a hand on the floor, or on a thing with no use to them,
    /// goes to a spot of their own in a ring round it, through the doors on
    /// the way, and stands there facing it. The hand beats a leader's call,
    /// swerving and following other runners; the flames at their danger
    /// distance still send them off.
    /// <para>
    /// Since the fourth pass (2026-09-30) the goal is theirs
    /// (<see cref="AgentHand"/>): they set about it once their conviction
    /// passes the answer line, keep it when the hand comes off once it has
    /// passed the commit line, and drift off it only as that conviction
    /// fades on their own beat, the strong-willed first. Nobody breaks away
    /// while the hand is on them; a hand dragged along carries them with it
    /// (the owner: "so agents can be guided with this"); somebody who can
    /// find no way there gives up for a beat and tries again.
    /// </para>
    /// <para>
    /// A push (the right button) sends them walking away from it, out past its
    /// full strength, and then they run on as they would; a push dragged along
    /// herds them. A push is never kept.
    /// </para>
    /// <para>
    /// Only the hand's places nothing else answers: the floor, or a thing with
    /// no use to somebody frightened. The bottle is the fighters', the card and
    /// its door the card's, fallen crates the heave's, a pull station the
    /// alarm's; a door is weighed in the choice of door, and a hand landing on
    /// one brings that choice forward. Draws nothing of its own.
    /// </para>
    /// </summary>
    internal sealed class HandGatherBehaviour : IPanicOption, IBindable
    {
        /// <summary>Within this of their spot they stand; beyond it they walk.</summary>
        private const int AtTheSpotMillimetres = 450;

        /// <summary>Further off than this they run; nearer, they walk up.</summary>
        private const int RunUntilMillimetres = 2500;

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly FrightenedWalk walk;
        private InfluenceSystem influence;
        private DoorBehaviour doorBehaviour;
        private HandHeaveBehaviour handHeave;
        private AlarmSystem alarms;
        private PhysicsObjectSystem objects;
        private KeycardSystem keycards;

        public HandGatherBehaviour(SimulationContext context, WorldGeometry geometry, FrightenedWalk walk)
        {
            this.context = context;
            this.geometry = geometry;
            this.walk = walk;
        }

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
            doorBehaviour = systems.DoorBehaviour;
            handHeave = systems.HandHeave;
            alarms = systems.Alarms;
            objects = systems.Objects;
            keycards = systems.Keycards;
        }

        public static bool IsAnswering(Agent agent) => agent.Intent.Activity == AgentActivityState.AnsweringTheHand;

        /// <summary>
        /// Considered in the panic decision after the keycard and the heave,
        /// and before the leaders: the hand is asked before a leader's call,
        /// or nobody following one would ever answer it.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsAnswering(agent))
            {
                return Update(agent, inDanger);
            }

            if (inDanger || influence == null || agent.Body.State != AgentBodyState.Upright ||
                agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Burning.IsBurning)
            {
                return null;
            }

            // A push first: it is never a goal, and it sends them off once.
            if (influence.TryGetLivePush(agent, out InfluenceSystem.Place push, out int pushed))
            {
                return TryStartPush(agent, push, pushed, inDanger);
            }

            if (!influence.TryGetPull(agent, out InfluenceSystem.Place place, out int drive))
            {
                return null;
            }

            if (!IsTheGatherersOwn(place))
            {
                RethinkForADoor(agent, place, drive);
                return null;
            }

            // Already going out of the building: a pull does not turn them
            // back from the way out. Between rooms it does.
            InfluenceSettings rules = context.Scenario.Influence;
            if ((doorBehaviour.IsLeaving(agent) && geometry.DoorLeadsOutside(agent.Doors.ExitDoorIndex)) ||
                drive < rules.ActsAgainstNatureFromPerMille || !influence.MayAnswer(agent))
            {
                return null;
            }

            // They answer it: the gold hand goes up over them, and nobody's
            // call takes them off it.
            LeaderBehaviour.StopFollowing(agent);
            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            agent.Intent.SwerveEndTick = 0;
            influence.Answer(agent, place, drive);
            return Update(agent, inDanger);
        }

        /// <summary>
        /// Whether a pull is this behaviour's to answer: on the floor (not
        /// beside a pull station, not over fallen crates), or on a thing with
        /// no use to somebody frightened. Draws nothing.
        /// </summary>
        private bool IsTheGatherersOwn(in InfluenceSystem.Place place)
        {
            if (place.Door >= 0 || (handHeave != null && handHeave.IsClearing(place)) ||
                (alarms != null && alarms.StationAt(influence, place) >= 0))
            {
                return false;
            }

            if (place.Thing < 0)
            {
                return true;
            }

            return !objects.IsEquipment(place.Thing) && (keycards == null || place.Thing != keycards.Card);
        }

        /// <summary>
        /// A goal on a door, driven hard: their next choice of door is brought
        /// forward, once a press (2026-09-30; they used to go on toward the
        /// door they had chosen for up to a second and a half before the hand
        /// was weighed at all).
        /// </summary>
        private void RethinkForADoor(Agent agent, in InfluenceSystem.Place place, int drive)
        {
            if (place.Door < 0 || agent.Hand.RethoughtForPress == place.EventId ||
                drive < context.Scenario.Influence.ActsAgainstNatureFromPerMille || !influence.MayAnswer(agent))
            {
                return;
            }

            agent.Hand.RethoughtForPress = place.EventId;
            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// A push, felt strongly and taken in, by somebody inside its full
        /// strength: they set off away from it. Written once a press.
        /// </summary>
        private MotorIntent? TryStartPush(Agent agent, in InfluenceSystem.Place push, int felt, bool inDanger)
        {
            if (!influence.InsideThePush(agent, push) || felt < context.Scenario.Influence.ActsAgainstNatureFromPerMille)
            {
                return null;
            }

            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            if (agent.Hand.PushedByPress != push.EventId)
            {
                agent.Hand.PushedByPress = push.EventId;
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentPushedAwayByInfluence,
                    agent.Body.Position, felt, 0, push.EventId, push.Target);
            }

            return Update(agent, inDanger);
        }

        /// <summary>Going to the hand and standing at it, or walking away from a push.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            if (influence == null || inDanger || agent.Body.State != AgentBodyState.Upright || agent.Burning.IsBurning ||
                agent.Carry.ItemIndex >= 0)
            {
                // The flames came near; they went down or caught. On their
                // own again at once.
                Stop(agent);
                return null;
            }

            PanicSettings panic = context.Scenario.Panic;
            if (influence.TryGetLivePush(agent, out InfluenceSystem.Place push, out int pushed) &&
                agent.Hand.PushedByPress == push.EventId)
            {
                if (!influence.InsideThePush(agent, push) || pushed <= 0)
                {
                    // Out of it: they run on as they would.
                    Stop(agent);
                    return null;
                }

                LogicalPosition away = influence.AwayFromThePush(agent, push);
                if (walk.TryStep(agent, away, agent.Personality.PanicSpeed, push.EventId, out MotorIntent flee))
                {
                    return flee;
                }

                Stop(agent);
                return null;
            }

            if (!influence.TryGetPull(agent, out InfluenceSystem.Place place, out int drive) || !agent.Hand.Acting ||
                drive <= 0)
            {
                // The goal is over: the hand came off before they were sure
                // of it, their conviction faded, or something else took them.
                Stop(agent);
                return null;
            }

            if (!IsTheGatherersOwn(place))
            {
                // Dragged onto something with its own answer (a pull station,
                // fallen crates): that answer takes over, the goal kept.
                StopStanding(agent);
                return null;
            }

            LogicalPosition spot = influence.GatherSpotFor(agent, place);
            long distance = IntegerMath.Distance(agent.Body.Position, spot);
            if (distance <= AtTheSpotMillimetres)
            {
                // At their spot: facing the hand, feet planted, frightened still.
                int facing = IntegerMath.HeadingBetween(agent.Body.Position, place.At, agent.Body.Heading);
                return PanicIntent.StandAndFace(agent, facing, panic);
            }

            int speed = distance > RunUntilMillimetres
                ? agent.Personality.PanicSpeed
                : System.Math.Max(agent.Personality.CalmSpeed,
                    (int)(agent.Personality.PanicSpeed * distance / RunUntilMillimetres));
            if (walk.TryStep(agent, spot, speed, place.EventId, out MotorIntent step))
            {
                return step;
            }

            // No way there (a door on the way that will not open): a beat,
            // and less sure of it, then they try again.
            influence.GiveUp(agent);
            StopStanding(agent);
            return null;
        }

        /// <summary>Back to running, thinking again a beat later; whatever they hold for the hand is over.</summary>
        private void Stop(Agent agent)
        {
            InfluenceSystem.Interrupted(agent);
            StopStanding(agent);
        }

        /// <summary>Back to running, thinking again a beat later, the goal untouched.</summary>
        private void StopStanding(Agent agent)
        {
            walk.Forget(agent);
            if (IsAnswering(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
            }

            context.ThinkAgainSoon(agent.Intent);
        }
    }
}

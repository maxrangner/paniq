namespace Paniq.Simulation
{
    /// <summary>
    /// The frightened answer the player's hand (2026-09-30, the owner: "when
    /// panicked, the agents still run around too much"). Until now a hand on
    /// the floor was only a compass to somebody frightened: it tilted which
    /// door they chose, or which random spot in their room they sprinted to,
    /// and on arriving they picked another. Nothing ever brought them to it.
    /// <para>
    /// Now somebody frightened who feels the hand strongly enough, and has
    /// taken it in, answers it on their own beat -- the nervous first, the
    /// steady later, the strongest wills never -- and goes to a spot of their
    /// own in a ring round it, through the doors on the way, and stands there,
    /// facing it. The hand beats a leader's call, swerving and following other
    /// runners; the flames at their danger distance still send them off. Their
    /// character stays: once a second somebody who feels it less than fully
    /// may break away from it, and does not come back to the same press. A
    /// hand dragged along carries them with it (the owner: "so agents can be
    /// guided with this"). The moment the hand comes off they are on their
    /// own again.
    /// </para>
    /// <para>
    /// A push (the right button) sends them walking away from it, out past its
    /// full strength, and then they run on as they would; a push dragged along
    /// herds them.
    /// </para>
    /// <para>
    /// Only the hand's places nothing else answers: the floor, or a thing with
    /// no use to somebody frightened. The bottle is the fighters', the card and
    /// its door the card's, fallen crates the heave's, a pull station the
    /// alarm's; a door is weighed in the choice of door, and a hand landing on
    /// one brings that choice forward. Draws numbers only for people who feel
    /// the hand, so a hand nobody is near changes no run.
    /// </para>
    /// </summary>
    internal sealed class HandGatherBehaviour : IPanicOption, IBindable
    {
        /// <summary>Within this of their spot they stand; beyond it they walk.</summary>
        private const int AtTheSpotMillimetres = 450;

        /// <summary>Further off than this they run; nearer, they walk up.</summary>
        private const int RunUntilMillimetres = 2500;

        /// <summary>How often somebody standing at the hand weighs breaking away from it: once a second.</summary>
        private const int BreakAwayCheckTicks = 50;

        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly FrightenedWalk walk;
        private InfluenceSystem influence;
        private DoorBehaviour doorBehaviour;
        private HandHeaveBehaviour handHeave;
        private AlarmSystem alarms;
        private PhysicsObjectSystem objects;
        private KeycardSystem keycards;
        private DoorSystem doors;

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
            doors = systems.Doors;
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

            if (inDanger || influence == null || influence.Count == 0 || agent.Body.State != AgentBodyState.Upright ||
                agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0 || agent.Burning.IsBurning)
            {
                return null;
            }

            InfluenceSystem.Place place = influence[0];
            if (agent.Intent.HandGaveUpOnPress == place.EventId)
            {
                return null;
            }

            if (place.Repels)
            {
                return TryStartPush(agent, place, inDanger);
            }

            if (!IsTheGatherersOwn(place))
            {
                RethinkForADoor(agent, place);
                return null;
            }

            // Already going out of the building: a pull does not turn them
            // back from the way out. Between rooms it does.
            InfluenceSettings rules = context.Scenario.Influence;
            if ((doorBehaviour.IsLeaving(agent) && geometry.DoorLeadsOutside(agent.Doors.ExitDoorIndex)) ||
                (context.Tick + agent.Index) % rules.LeaveTaskCheckTicks != 0)
            {
                return null;
            }

            int felt = influence.FeltBy(agent, 0);
            if (felt < rules.ActsAgainstNatureFromPerMille || !influence.HasNoticed(agent) ||
                context.Random.NextIntInclusive(0, 999) >=
                System.Math.Min(1000, felt) * rules.FrightenedAnswerChancePerMille / 1000)
            {
                return null;
            }

            // They answer it: the gold hand goes up over them, and nobody's
            // call takes them off it.
            LeaderBehaviour.StopFollowing(agent);
            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            agent.Intent.AnsweringPress = place.EventId;
            agent.Intent.ForTheHandPress = place.EventId;
            agent.Intent.SwerveEndTick = 0;
            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentDrawnByInfluence, agent.Body.Position,
                felt, 0, place.EventId, place.Target);
            return Update(agent, inDanger);
        }

        /// <summary>
        /// Whether a pull is this behaviour's to answer: on the floor (not
        /// beside a pull station, not over fallen crates), or on a thing with
        /// no use to somebody frightened. Draws nothing.
        /// </summary>
        private bool IsTheGatherersOwn(InfluenceSystem.Place place)
        {
            if (place.Door >= 0 || (handHeave != null && handHeave.IsClearing()) ||
                (alarms != null && alarms.StationTheHandIsOn(influence) >= 0))
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
        /// A hand landing on a door, felt strongly and taken in: their next
        /// choice of door is brought forward, once a press (2026-09-30; they
        /// used to go on toward the door they had chosen for up to a second and
        /// a half before the hand was weighed at all).
        /// </summary>
        private void RethinkForADoor(Agent agent, InfluenceSystem.Place place)
        {
            if (place.Door < 0 || agent.Intent.RethoughtForPress == place.EventId ||
                influence.FeltBy(agent, 0) < context.Scenario.Influence.ActsAgainstNatureFromPerMille ||
                !influence.HasNoticed(agent))
            {
                return;
            }

            agent.Intent.RethoughtForPress = place.EventId;
            context.ThinkAgainSoon(agent.Intent);
        }

        /// <summary>
        /// A push, felt strongly and taken in, by somebody inside its full
        /// strength: they set off away from it. Written once a press.
        /// </summary>
        private MotorIntent? TryStartPush(Agent agent, InfluenceSystem.Place place, bool inDanger)
        {
            InfluenceSettings rules = context.Scenario.Influence;
            if (!InsideThePush(agent, place) || influence.FeltBy(agent, 0) < rules.ActsAgainstNatureFromPerMille ||
                !influence.HasNoticed(agent))
            {
                return null;
            }

            walk.Forget(agent);
            agent.Intent.Activity = AgentActivityState.AnsweringTheHand;
            agent.Intent.AnsweringPress = place.EventId;
            if (agent.Intent.PushedByPress != place.EventId)
            {
                agent.Intent.PushedByPress = place.EventId;
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentPushedAwayByInfluence,
                    agent.Body.Position, influence.FeltBy(agent, 0), 0, place.EventId, place.Target);
            }

            return Update(agent, inDanger);
        }

        /// <summary>Whether they stand inside a push's full strength and a little more: the ground it clears.</summary>
        private bool InsideThePush(Agent agent, InfluenceSystem.Place place)
        {
            InfluenceSettings rules = context.Scenario.Influence;
            long clear = (long)rules.ReachMillimetres * rules.FullWithinPercent / 100L + InfluenceSystem.PushClearanceMillimetres / 2;
            return LogicalPosition.DistanceSquared(agent.Body.Position, place.At) < clear * clear;
        }

        /// <summary>Going to the hand and standing at it, or walking away from a push.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger)
        {
            if (influence == null || influence.Count == 0 || influence.CurrentPress != agent.Intent.AnsweringPress ||
                inDanger || agent.Body.State != AgentBodyState.Upright || agent.Burning.IsBurning ||
                agent.Carry.ItemIndex >= 0)
            {
                // The hand came off, or is somewhere else now; the flames came
                // near; they went down or caught. On their own again at once.
                Stop(agent, gaveUp: false);
                return null;
            }

            InfluenceSystem.Place place = influence[0];
            int felt = influence.FeltBy(agent, 0);
            PanicSettings panic = context.Scenario.Panic;
            if (place.Repels)
            {
                if (!InsideThePush(agent, place) || felt <= 0)
                {
                    // Out of it: they run on as they would.
                    Stop(agent, gaveUp: false);
                    return null;
                }

                LogicalPosition away = influence.AwayFromThePush(agent, 0);
                if (walk.TryStep(agent, away, agent.Personality.PanicSpeed, place.EventId, out MotorIntent flee))
                {
                    return flee;
                }

                Stop(agent, gaveUp: true);
                return null;
            }

            if (!IsTheGatherersOwn(place))
            {
                // Dragged onto something with its own answer (a pull station,
                // fallen crates): that answer takes over.
                Stop(agent, gaveUp: false);
                return null;
            }

            InfluenceSettings rules = context.Scenario.Influence;
            if (felt <= 0 || ((context.Tick + agent.Index) % BreakAwayCheckTicks == 0 &&
                              context.Random.NextIntInclusive(0, 999) <
                              rules.BreakAwayPerMille * (1000 - System.Math.Min(1000, felt)) / 1000))
            {
                // Out of its reach, or their own mind again: they break away,
                // and this press does not ask them twice.
                Stop(agent, gaveUp: true);
                return null;
            }

            LogicalPosition spot = influence.GatherSpotFor(agent);
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

            // No way there (a door on the way that will not open): not this press.
            Stop(agent, gaveUp: true);
            return null;
        }

        /// <summary>
        /// On their own again: running, and thinking again a beat later. Given
        /// up (broke away, no way there), this press does not ask them again.
        /// </summary>
        private void Stop(Agent agent, bool gaveUp)
        {
            if (gaveUp)
            {
                agent.Intent.HandGaveUpOnPress = agent.Intent.AnsweringPress;
            }

            agent.Intent.AnsweringPress = 0UL;
            InfluenceSystem.StopActing(agent);
            walk.Forget(agent);
            if (IsAnswering(agent))
            {
                agent.Intent.Activity = AgentActivityState.Fleeing;
            }

            context.ThinkAgainSoon(agent.Intent);
        }
    }
}

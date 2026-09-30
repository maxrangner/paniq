namespace Paniq.Simulation
{
    /// <summary>
    /// The player's hand on a box too heavy to carry (2026-09-30, the owner:
    /// "I held the blocking boxes, and none were moved"; and "agents acted
    /// upon should be stuff they normally wouldn't"). Whoever feels the hand
    /// on a fallen crate -- calm or frightened, strong or not -- goes to it
    /// and heaves it aside. The strong do it at once, as they would anyway;
    /// the weak strain at it first, visibly, for longer the weaker they are,
    /// and two straining at one crate get it moving in half the time.
    /// <para>
    /// Only a thing on the map people steer by that is loose on the floor: a
    /// crate off the fallen tower, held still where it lies. The tower still
    /// standing and the stockroom's walls are off limits, as they are to
    /// everybody (the owner's "no hand on the tower"). The pull's use is spent
    /// on the heave: a press moves one crate, and the next is pressed afresh.
    /// </para>
    /// <para>
    /// Everything here asks the hand, the crate and the clock; the one draw is
    /// the jitter on how long the straining takes, drawn once a heave.
    /// </para>
    /// </summary>
    internal sealed class HandHeaveBehaviour : IPanicOption, IBindable
    {
        /// <summary>How far around the crate to look for floor to stand on beside it.</summary>
        private const int StandingRoomMillimetres = 2000;

        /// <summary>How far past touching a person's hands reach to set their shoulder to a crate.</summary>
        private const int ReachMillimetres = 350;

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly FrightenedWalk walk;
        private readonly int bodyRadius;
        private InfluenceSystem influence;

        public HandHeaveBehaviour(SimulationContext context, Crowd crowd, WorldGeometry geometry,
            PhysicsObjectSystem objects, FrightenedWalk walk)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.walk = walk;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
        }

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
        }

        public static bool IsHeaving(Agent agent) => agent.Intent.Activity == AgentActivityState.HeavingForTheHand;

        /// <summary>
        /// Whether the hand is on a crate that can be heaved: an unspent pull
        /// on a loose thing on the map. Asks nothing about who.
        /// </summary>
        public bool CanHeave(int thing) =>
            thing >= 0 && influence != null && influence.PlaceOfThing(thing) >= 0 && objects.CanHeaveForTheHand(thing);

        /// <summary>
        /// Sets off to heave the crate the hand is on. The press is what they
        /// answered, so the rest of the round knows they are doing it for the
        /// hand. False when the crate is no longer there to heave.
        /// </summary>
        public bool Start(Agent agent, int thing, ulong press)
        {
            // A press whose crate they set off for and gave up on is not
            // tried again: until the hand is pressed afresh, it is somebody
            // else's crate.
            if (!CanHeave(thing) || agent.Carry.ItemIndex >= 0 || agent.Intent.HeaveGaveUpOnPress == press)
            {
                return false;
            }

            agent.Intent.Activity = AgentActivityState.HeavingForTheHand;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(context.Scenario.Calm.StrollTimeoutTicks));
            agent.Intent.HeavingThing = thing;
            agent.Intent.HeavingUntilTick = 0;
            agent.Intent.ForTheHandPress = press;
            return true;
        }

        /// <summary>
        /// Considered in the panic decision, first after the keycard: a crate
        /// the hand is on, felt strongly enough, and hands free. The flames
        /// inside their danger distance put it out of their head.
        /// </summary>
        public MotorIntent? Decide(Agent agent, bool inDanger, bool eager)
        {
            if (IsHeaving(agent))
            {
                return Update(agent, inDanger, frightened: true);
            }

            if (inDanger || influence == null || influence.Count == 0 || agent.Body.State != AgentBodyState.Upright ||
                agent.Carry.ItemIndex >= 0 || agent.Help.TargetIndex >= 0)
            {
                return null;
            }

            // What the hand is on first, which costs nothing: only then how
            // strongly they feel it, which walks the doors.
            InfluenceSystem.Place pull = influence[0];
            if (pull.Repels || !CanHeave(pull.Thing) || agent.Intent.HeaveGaveUpOnPress == pull.EventId)
            {
                return null;
            }

            int felt = influence.FeltBy(agent, 0);
            if (felt < context.Scenario.Influence.ActsAgainstNatureFromPerMille || !influence.HasNoticed(agent) ||
                !Start(agent, pull.Thing, pull.EventId))
            {
                return null;
            }

            context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentDrawnByInfluence, agent.Body.Position,
                felt, 0, pull.EventId, pull.Target);
            return Update(agent, inDanger, frightened: true);
        }

        /// <summary>
        /// One tick of it for somebody calm, as the calm behaviour's other
        /// activities are: false once it is over, and they choose afresh.
        /// </summary>
        public bool UpdateCalm(Agent agent, out int goalHeading, out int goalSpeed)
        {
            MotorIntent? step = Update(agent, false, frightened: false);
            goalHeading = step.HasValue ? step.Value.GoalHeading : agent.Body.Heading;
            goalSpeed = step.HasValue ? step.Value.GoalSpeed : 0;
            return step.HasValue;
        }

        /// <summary>Walking to the crate, straining at it, and heaving it.</summary>
        private MotorIntent? Update(Agent agent, bool inDanger, bool frightened)
        {
            int tick = context.Tick;
            int thing = agent.Intent.HeavingThing;
            bool handStillOn = thing >= 0 && influence != null && influence.IsActingFor(agent) &&
                               influence.PlaceOfThing(thing) >= 0;
            if (!handStillOn || !objects.CanHeaveForTheHand(thing) || inDanger || !agent.Body.IsOnTheirFeet ||
                agent.Burning.IsBurning || tick >= agent.Intent.ActivityEndTick)
            {
                Stop(agent, frightened, gaveUp: handStillOn);
                return null;
            }

            LogicalPosition crate = objects.PositionOf(thing);
            long reach = bodyRadius + (long)objects.RadiusOf(thing) + ReachMillimetres;
            int speed = frightened ? agent.Personality.PanicSpeed : agent.Personality.CalmSpeed;
            int turn = frightened ? agent.Personality.PanicTurnRate : agent.Personality.CalmTurnRate;
            int acceleration = frightened ? context.Scenario.Panic.Acceleration : context.Scenario.Calm.Acceleration;
            if (LogicalPosition.DistanceSquared(agent.Body.Position, crate) > reach * reach)
            {
                // On the way, to floor beside it.
                agent.Intent.HeavingUntilTick = 0;
                LogicalPosition beside = geometry.Navigation.NearestStandableTo(crate, bodyRadius, StandingRoomMillimetres);
                if (frightened)
                {
                    if (walk.TryStep(agent, beside, speed, agent.Intent.ForTheHandPress, out MotorIntent step))
                    {
                        return step;
                    }

                    Stop(agent, true, gaveUp: true);
                    return null;
                }

                if (agent.Body.BlockedTicks > context.Scenario.Calm.BlockedGiveUpTicks)
                {
                    Stop(agent, false, gaveUp: true);
                    return null;
                }

                int heading = geometry.Routes.HeadingToward(agent.Body.Position, beside, bodyRadius, agent.Body.Heading);
                return new MotorIntent(heading, speed, turn, acceleration);
            }

            int facing = IntegerMath.HeadingBetween(agent.Body.Position, crate, agent.Body.Heading);
            if (agent.Intent.HeavingUntilTick == 0)
            {
                // Shoulder to it. How long it takes is their weakness; a beat
                // at least, so nobody heaves on the tick they arrive.
                agent.Intent.HeavingUntilTick = checked(tick + System.Math.Max(context.ReactionLag(),
                    context.Jittered(System.Math.Max(1, StrainTicks(agent)))));

                // Too weak for it of their own accord: they tremble at it.
                agent.Intent.AgainstTheirNature = agent.Traits.Strength < context.Scenario.Blockades.ShoveMinimumStrength;
            }
            else if (SomebodyElseStrainingAt(agent, thing))
            {
                // Two at one crate: it comes twice as fast.
                agent.Intent.HeavingUntilTick--;
            }

            if (tick < agent.Intent.HeavingUntilTick)
            {
                // Straining at it: face it, feet planted.
                return new MotorIntent(facing, 0, turn, acceleration);
            }

            ulong press = agent.Intent.ForTheHandPress;
            ulong spent = influence.Spend(agent, -1, thing);
            objects.HeaveForTheHand(agent, thing, facing, spent != 0UL ? spent : press);
            if (agent.Traits.Strength < context.Scenario.Blockades.ShoveMinimumStrength)
            {
                influence.ActedAgainstNature(agent, AgainstTheirNature.HeavedTheBox, press, objects.IdOf(thing),
                    agent.Body.Position);
            }

            Stop(agent, frightened);
            return frightened ? (MotorIntent?)null : new MotorIntent(facing, 0, turn, acceleration);
        }

        /// <summary>
        /// How long somebody strains before the crate gives: nothing for the
        /// strong enough to heave it anyway, and up to
        /// <see cref="InfluenceSettings.HeaveStrainTicksAtNoStrength"/> for the
        /// weakest, evenly between.
        /// </summary>
        private int StrainTicks(Agent agent)
        {
            int enough = context.Scenario.Blockades.ShoveMinimumStrength;
            int strength = agent.Traits.Strength;
            if (strength >= enough || enough <= 0)
            {
                return 1;
            }

            return context.Scenario.Influence.HeaveStrainTicksAtNoStrength * (enough - strength) / enough;
        }

        private bool SomebodyElseStrainingAt(Agent agent, int thing)
        {
            using Crowd.Nearby near = crowd.Within(objects.PositionOf(thing), StandingRoomMillimetres);
            for (int i = 0; i < near.Count; i++)
            {
                Agent other = crowd.All[near[i]];
                if (other != agent && IsHeaving(other) && other.Intent.HeavingThing == thing &&
                    other.Intent.HeavingUntilTick > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Done, or given up: back to their day, or back to running, and no
        /// longer doing anything for the hand. Given up on a press still held,
        /// they do not set off for its crate again.
        /// </summary>
        private void Stop(Agent agent, bool frightened, bool gaveUp = false)
        {
            if (gaveUp)
            {
                agent.Intent.HeaveGaveUpOnPress = agent.Intent.ForTheHandPress;
            }

            agent.Intent.HeavingThing = -1;
            agent.Intent.HeavingUntilTick = 0;
            InfluenceSystem.StopActing(agent);
            if (!IsHeaving(agent))
            {
                return;
            }

            if (frightened)
            {
                walk.Forget(agent);
                agent.Intent.Activity = AgentActivityState.Fleeing;
                context.ThinkAgainSoon(agent.Intent);
                return;
            }

            agent.Intent.Activity = AgentActivityState.Standing;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.ReactionLag());
        }
    }
}

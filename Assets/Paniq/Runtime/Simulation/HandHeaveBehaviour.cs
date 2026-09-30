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
    /// Since the third pass (2026-09-30, the owner: "when influenced they
    /// should often switch to that specific task, like clearing boxes for a
    /// path") the hand <em>clears</em>: a hand on a crate, on the floor beside
    /// some, or on a doorway heaped with the fallen tower, has whoever answers
    /// it heave every crate within <see cref="InfluenceSettings.ClearReachMillimetres"/>
    /// of it, one after another, each away from the hand, until none is left.
    /// It used to move one crate a press. A crate somebody has set off for is
    /// finished even if the hand comes off meanwhile (a click's beacon lasts
    /// three seconds; the walk and the straining can take longer).
    /// </para>
    /// <para>
    /// Only a thing on the map people steer by that is loose on the floor: a
    /// crate off the fallen tower, held still where it lies. The tower still
    /// standing and the stockroom's walls are off limits, as they are to
    /// everybody (the owner's "no hand on the tower").
    /// </para>
    /// <para>
    /// Everything here asks the hand, the crates and the clock; the one draw is
    /// the jitter on how long the straining takes, drawn once a heave.
    /// </para>
    /// </summary>
    internal sealed class HandHeaveBehaviour : IPanicOption, IBindable
    {
        /// <summary>How far around the crate to look for floor to stand on beside it.</summary>
        private const int StandingRoomMillimetres = 2000;

        /// <summary>How far past touching a person's hands reach to set their shoulder to a crate.</summary>
        private const int ReachMillimetres = 350;

        /// <summary>A crate this near the middle of the hand is the hand's own: it is heaved away from whoever heaves it, having no "away from the hand".</summary>
        private const int OnTheHandMillimetres = 300;

        /// <summary>How much longer than an ordinary calm walk somebody heaving for the hand keeps at it while blocked by the crowd the hand has gathered.</summary>
        private const int BlockedPatienceTimes = 5;

        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly WorldGeometry geometry;
        private readonly PhysicsObjectSystem objects;
        private readonly DoorSystem doors;
        private readonly FrightenedWalk walk;
        private readonly int bodyRadius;
        private InfluenceSystem influence;

        public HandHeaveBehaviour(SimulationContext context, Crowd crowd, WorldGeometry geometry,
            PhysicsObjectSystem objects, DoorSystem doors, FrightenedWalk walk)
        {
            this.context = context;
            this.crowd = crowd;
            this.geometry = geometry;
            this.objects = objects;
            this.doors = doors;
            this.walk = walk;
            bodyRadius = context.Scenario.World.OccupancyRadiusMillimetres;
        }

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
        }

        public static bool IsHeaving(Agent agent) => agent.Intent.Activity == AgentActivityState.HeavingForTheHand;

        /// <summary>
        /// Where the hand is clearing, if it is: a pull on the floor, on a
        /// crate, or on a doorway heaped with fallen boxes, with a crate still
        /// to heave within reach of it. Asks nothing about who.
        /// </summary>
        public bool IsClearing(out LogicalPosition centre)
        {
            centre = default;
            if (influence == null || influence.Count == 0)
            {
                return false;
            }

            InfluenceSystem.Place place = influence[0];
            if (place.Repels || (place.Thing >= 0 && !objects.CanHeaveForTheHand(place.Thing)) ||
                (place.Door >= 0 && !doors.IsPiled(place.Door) && !doors.IsObstructed(place.Door)))
            {
                // A push; a thing that is not a crate (the bottle, the card, a
                // chair); a door that only wants opening or shutting.
                return false;
            }

            centre = place.At;
            return NearestCrate(centre, centre, null) >= 0;
        }

        /// <summary>Whether the hand is clearing at all: what the calm behaviour asks before sending somebody.</summary>
        public bool IsClearing() => IsClearing(out _);

        /// <summary>
        /// Whether this thing is one the hand wants heaved: a crate within the
        /// clearing reach of a clearing hand.
        /// </summary>
        public bool CanHeave(int thing)
        {
            if (thing < 0 || !objects.CanHeaveForTheHand(thing) || !IsClearing(out LogicalPosition centre))
            {
                return false;
            }

            long reach = context.Scenario.Influence.ClearReachMillimetres;
            return LogicalPosition.DistanceSquared(objects.PositionOf(thing), centre) <= reach * reach;
        }

        /// <summary>
        /// The crate within the clearing reach of <paramref name="centre"/> for
        /// <paramref name="agent"/> to heave: the nearest to them that nobody
        /// else is already at, else the nearest. -1 when none is left. With no
        /// agent, the nearest to <paramref name="from"/>.
        /// </summary>
        private int NearestCrate(LogicalPosition centre, LogicalPosition from, Agent agent)
        {
            long reach = context.Scenario.Influence.ClearReachMillimetres;
            int best = -1;
            long bestScore = long.MaxValue;
            for (int t = 0; t < objects.Count; t++)
            {
                if (!objects.CanHeaveForTheHand(t))
                {
                    continue;
                }

                LogicalPosition at = objects.PositionOf(t);
                if (LogicalPosition.DistanceSquared(at, centre) > reach * reach)
                {
                    continue;
                }

                // A crate somebody else is already at costs as much as ten
                // metres more: two pairs of hands on one is for when every
                // crate has one.
                long score = LogicalPosition.DistanceSquared(at, from);
                if (agent != null && SomebodyElseAt(agent, t))
                {
                    score += ClaimedPenaltySquared;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        private const long ClaimedPenaltySquared = 10000L * 10000L;

        /// <summary>
        /// Sets off to heave a crate the hand wants heaved. The press is what
        /// they answered, so the rest of the round knows they are doing it for
        /// the hand. False when the crate is no longer there to heave.
        /// </summary>
        public bool Start(Agent agent, int thing, ulong press)
        {
            // A press whose crates they set off for and could not reach is not
            // tried again: until the hand is pressed afresh, it is somebody
            // else's heap.
            if (!CanHeave(thing) || agent.Carry.ItemIndex >= 0 || agent.Intent.HeaveGaveUpOnPress == press)
            {
                return false;
            }

            agent.Intent.Activity = AgentActivityState.HeavingForTheHand;
            agent.Intent.ActivityEndTick = checked(context.Tick + context.Jittered(context.Scenario.Calm.StrollTimeoutTicks));
            agent.Intent.HeavingThing = thing;
            agent.Intent.HeavingUntilTick = 0;
            agent.Intent.ForTheHandPress = press;
            agent.Body.BlockedTicks = 0;
            return true;
        }

        /// <summary>
        /// Sets off for the crate the clearing hand wants heaved that suits
        /// them best (the calm behaviour's way in). False when there is none,
        /// or it cannot be heaved after all.
        /// </summary>
        public bool StartNearest(Agent agent, ulong press)
        {
            if (!IsClearing(out LogicalPosition centre))
            {
                return false;
            }

            int thing = NearestCrate(centre, agent.Body.Position, agent);
            return thing >= 0 && Start(agent, thing, press);
        }

        /// <summary>
        /// Considered in the panic decision, first after the keycard: a
        /// clearing hand, felt strongly enough, and hands free. The flames
        /// inside their danger distance put it out of their head -- but a
        /// crate they are already at is finished.
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
            if (pull.Repels || agent.Intent.HeaveGaveUpOnPress == pull.EventId || !IsClearing(out LogicalPosition centre))
            {
                return null;
            }

            int felt = influence.FeltBy(agent, 0);
            if (felt < context.Scenario.Influence.ActsAgainstNatureFromPerMille || !influence.HasNoticed(agent))
            {
                return null;
            }

            int thing = NearestCrate(centre, agent.Body.Position, agent);
            bool answering = agent.Intent.ForTheHandPress == pull.EventId;
            if (thing < 0 || !Start(agent, thing, pull.EventId))
            {
                return null;
            }

            if (!answering)
            {
                // Written once a press: the next crate of the same heap is the
                // same answer.
                context.Events.Append(context.Tick, agent.Id, CausalEventType.AgentDrawnByInfluence, agent.Body.Position,
                    felt, 0, pull.EventId, pull.Target);
            }

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
            if (thing < 0 || !objects.CanHeaveForTheHand(thing) || !agent.Body.IsOnTheirFeet ||
                agent.Burning.IsBurning || tick >= agent.Intent.ActivityEndTick)
            {
                // Gone (somebody else heaved it, it went up), down, alight, or
                // too long at it. Not given up: the next crate is still theirs.
                Stop(agent, frightened);
                return null;
            }

            LogicalPosition crate = objects.PositionOf(thing);
            long reach = bodyRadius + (long)objects.RadiusOf(thing) + ReachMillimetres;
            bool atIt = LogicalPosition.DistanceSquared(agent.Body.Position, crate) <= reach * reach;
            if (inDanger && (!atIt || agent.Intent.HeavingUntilTick == 0))
            {
                // The flames at their danger distance: a crate not yet started
                // on is out of their head.
                Stop(agent, frightened);
                return null;
            }

            int speed = frightened ? agent.Personality.PanicSpeed : agent.Personality.CalmSpeed;
            int turn = frightened ? agent.Personality.PanicTurnRate : agent.Personality.CalmTurnRate;
            int acceleration = frightened ? context.Scenario.Panic.Acceleration : context.Scenario.Calm.Acceleration;
            if (!atIt)
            {
                // On the way, to floor beside it on their own side (2026-09-30:
                // the first standable floor the grid found could be on the far
                // side of the heap, or of the archway).
                agent.Intent.HeavingUntilTick = 0;
                int back = IntegerMath.HeadingBetween(crate, agent.Body.Position, agent.Body.Heading);
                LogicalPosition near = crate + IntegerMath.Displacement(back, (int)reach - ReachMillimetres / 2);
                LogicalPosition beside = geometry.Navigation.NearestStandableTo(near, bodyRadius, StandingRoomMillimetres);
                if (frightened)
                {
                    if (walk.TryStep(agent, beside, speed, agent.Intent.ForTheHandPress, out MotorIntent step))
                    {
                        return step;
                    }

                    Stop(agent, true, gaveUp: true);
                    return null;
                }

                if (agent.Body.BlockedTicks > context.Scenario.Calm.BlockedGiveUpTicks * BlockedPatienceTimes)
                {
                    // Hemmed in by the crowd the hand gathered: another crate
                    // of the heap, if there is one, else give the heap up.
                    if (!TryAnotherCrate(agent, thing))
                    {
                        Stop(agent, false, gaveUp: true);
                        return null;
                    }

                    return new MotorIntent(agent.Body.Heading, 0, turn, acceleration);
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
            objects.HeaveForTheHand(agent, thing, HeaveHeading(agent, crate, facing), press);
            if (agent.Traits.Strength < context.Scenario.Blockades.ShoveMinimumStrength)
            {
                influence.ActedAgainstNature(agent, AgainstTheirNature.HeavedTheBox, press, objects.IdOf(thing),
                    agent.Body.Position);
            }

            Stop(agent, frightened, keepAnswering: true);
            return frightened ? (MotorIntent?)null : new MotorIntent(facing, 0, turn, acceleration);
        }

        /// <summary>
        /// Which way a crate goes (2026-09-30): away from the middle of the
        /// hand, which clears the spot the player marked; a crate on the
        /// middle itself away from whoever heaves it. It used to go from the
        /// heaver toward the crate whatever side they stood on, so a crate
        /// heaved from the far side went deeper into the heap.
        /// </summary>
        private int HeaveHeading(Agent agent, LogicalPosition crate, int facing)
        {
            if (influence == null || influence.Count == 0)
            {
                return facing;
            }

            LogicalPosition centre = influence[0].At;
            return LogicalPosition.DistanceSquared(crate, centre) > (long)OnTheHandMillimetres * OnTheHandMillimetres
                ? IntegerMath.HeadingBetween(centre, crate, facing)
                : facing;
        }

        /// <summary>Another crate of the same heap for somebody hemmed in on the way to theirs: true when they have one.</summary>
        private bool TryAnotherCrate(Agent agent, int thing)
        {
            if (!IsClearing(out LogicalPosition centre))
            {
                return false;
            }

            long reach = context.Scenario.Influence.ClearReachMillimetres;
            int best = -1;
            long bestDistance = long.MaxValue;
            for (int t = 0; t < objects.Count; t++)
            {
                if (t == thing || !objects.CanHeaveForTheHand(t) ||
                    LogicalPosition.DistanceSquared(objects.PositionOf(t), centre) > reach * reach)
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(objects.PositionOf(t), agent.Body.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = t;
                }
            }

            if (best < 0)
            {
                return false;
            }

            agent.Intent.HeavingThing = best;
            agent.Intent.HeavingUntilTick = 0;
            agent.Body.BlockedTicks = 0;
            return true;
        }

        /// <summary>
        /// How long somebody strains before the crate gives: nothing for the
        /// strong enough to heave it anyway, and up to
        /// <see cref="InfluenceSettings.HeaveStrainTicksAtNoStrength"/> for the
        /// weakest, evenly between. The same straining clears a thing wedged
        /// in a door for the hand.
        /// </summary>
        internal static int StrainTicks(Agent agent, ScenarioData scenario)
        {
            int enough = scenario.Blockades.ShoveMinimumStrength;
            int strength = agent.Traits.Strength;
            if (strength >= enough || enough <= 0)
            {
                return 1;
            }

            return scenario.Influence.HeaveStrainTicksAtNoStrength * (enough - strength) / enough;
        }

        private int StrainTicks(Agent agent) => StrainTicks(agent, context.Scenario);

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

        /// <summary>Whether somebody else has set off for this crate, or is at it.</summary>
        private bool SomebodyElseAt(Agent agent, int thing)
        {
            using Crowd.Nearby near = crowd.Within(objects.PositionOf(thing),
                context.Scenario.Influence.ClearReachMillimetres + StandingRoomMillimetres);
            for (int i = 0; i < near.Count; i++)
            {
                Agent other = crowd.All[near[i]];
                if (other != agent && IsHeaving(other) && other.Intent.HeavingThing == thing)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Done, or given up: back to their day, or back to running. Given up
        /// on a press still held, they do not set off for its crates again.
        /// A crate heaved, they are still answering the press, so the next
        /// crate of the heap is theirs if they want it (and the gold hand
        /// stays over them while it does).
        /// </summary>
        private void Stop(Agent agent, bool frightened, bool gaveUp = false, bool keepAnswering = false)
        {
            if (gaveUp)
            {
                agent.Intent.HeaveGaveUpOnPress = agent.Intent.ForTheHandPress;
            }

            agent.Intent.HeavingThing = -1;
            agent.Intent.HeavingUntilTick = 0;
            if (!keepAnswering)
            {
                InfluenceSystem.StopActing(agent);
            }
            else
            {
                agent.Intent.AgainstTheirNature = false;
            }

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

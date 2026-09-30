namespace Paniq.Simulation
{
    /// <summary>
    /// Tells (2026-09-30, the owner: "the visible agent tells"): the building
    /// has its creak -- the tower sways for three seconds before it falls, and
    /// that window is where the play is. People had nothing like it: somebody
    /// who froze with fear, dashed through the heat or went back toward the
    /// flames decided and did it on the same tick. Now each of those gets a
    /// short, visible wind-up first, in which one click saves them:
    /// <list type="bullet">
    /// <item><b>Going stiff</b>: about to freeze. Caught, they run instead.</item>
    /// <item><b>Gathering nerve</b>: about to dash for a door through the heat.
    /// Caught, the dash is off and the door is given up for a while.</item>
    /// <item><b>Turning back</b>: about to head back toward the danger for the
    /// card, a pull station, somebody down, or the fire with a bottle. Caught,
    /// they stay out of it for a while.</item>
    /// </list>
    /// A poke or a tug catches a tell, and so does the hand felt strongly and
    /// taken in, pull or push. The catch takes hold a beat later, like every
    /// reaction (the owner's rule). A tell is called off, uncaught, if the
    /// flames come inside their danger distance, or they go down or catch
    /// fire. Everything lives on the person; this system holds no state.
    /// </summary>
    internal sealed class TellSystem : IBindable
    {
        /// <summary>The target a tell names when it is about the fire itself rather than a door or a thing.</summary>
        public const int TheFire = -2;

        /// <summary>What a tell came to, this tick.</summary>
        public enum Outcome
        {
            /// <summary>No tell.</summary>
            None,

            /// <summary>Still winding up: they stand, facing the way they mean to go.</summary>
            Telling,

            /// <summary>Caught by the player: what they meant to do is off.</summary>
            Caught,

            /// <summary>It ran its course uncaught: what they meant to do goes ahead.</summary>
            Passed,

            /// <summary>Called off by the world (the flames close, down, alight).</summary>
            CalledOff
        }

        private readonly SimulationContext context;
        private readonly Threats threats;
        private InfluenceSystem influence;

        public TellSystem(SimulationContext context, Threats threats)
        {
            this.context = context;
            this.threats = threats;
        }

        public void Bind(Systems systems)
        {
            influence = systems.Influence;
        }

        private TellSettings Rules => context.Scenario.Tells;

        /// <summary>Whether the level gives people tells at all.</summary>
        public bool Enabled => Rules.Enabled;

        public static bool IsTelling(Agent agent) => agent.Intent.Tell != AgentTell.None;

        /// <summary>
        /// A tell begins: they stand facing <paramref name="heading"/> for its
        /// length, jittered, and the log says so. <paramref name="target"/> is
        /// the door or thing it is about (or <see cref="TheFire"/>), for the
        /// commit that follows to recognise it.
        /// </summary>
        public void Start(Agent agent, AgentTell kind, int heading, int target, ulong cause)
        {
            int tick = context.Tick;
            int length = context.Jittered(LengthOf(kind));
            AgentIntent intent = agent.Intent;
            intent.Tell = kind;
            intent.TellStartTick = tick;
            intent.TellEndTick = checked(tick + length);
            intent.TellHeading = heading;
            intent.TellTarget = target;
            intent.TellCaughtAtTick = 0;
            intent.TellCauseEventId = 0UL;
            context.Events.Append(tick, agent.Id, CausalEventType.AgentBeganATell, agent.Body.Position, (int)kind, length,
                cause);
        }

        private int LengthOf(AgentTell kind) =>
            kind == AgentTell.GoingStiff ? Rules.GoingStiffTicks
            : kind == AgentTell.GatheringNerve ? Rules.GatheringNerveTicks
            : Rules.TurningBackTicks;

        /// <summary>
        /// The player's poke or tug reaches somebody winding up: caught, taking
        /// hold a beat later. Nothing for somebody not telling, or already caught.
        /// </summary>
        public static void Catch(SimulationContext context, Agent agent, ulong cause)
        {
            if (agent.Intent.Tell == AgentTell.None || agent.Intent.TellCaughtAtTick != 0)
            {
                return;
            }

            agent.Intent.TellCaughtAtTick = context.ReactionTick();
            agent.Intent.TellCauseEventId = cause;
        }

        /// <summary>
        /// This tick of a tell. The hand felt strongly and taken in catches it
        /// here; a catch already made resolves once its beat has come (and
        /// holds the tell open until then); the flames close, down or alight,
        /// it is called off; at its end it has passed, which the commit that
        /// follows consumes (<see cref="TryPass"/>). <paramref name="kind"/>
        /// and <paramref name="target"/> say what it was about, and
        /// <paramref name="cause"/> what caught it.
        /// </summary>
        public Outcome Update(Agent agent, bool inDanger, out AgentTell kind, out int target, out ulong cause)
        {
            AgentIntent intent = agent.Intent;
            kind = intent.Tell;
            target = intent.TellTarget;
            cause = 0UL;
            if (kind == AgentTell.None)
            {
                return Outcome.None;
            }

            int tick = context.Tick;
            if (intent.TellCaughtAtTick == 0 && influence != null && influence.Count > 0 &&
                !HandIsOnWhatTheyWant(kind, target) &&
                influence.FeltBy(agent, 0) >= context.Scenario.Influence.ActsAgainstNatureFromPerMille &&
                influence.HasNoticed(agent))
            {
                Catch(context, agent, influence.CurrentPress);
            }

            if (intent.TellCaughtAtTick != 0)
            {
                if (tick < intent.TellCaughtAtTick)
                {
                    return Outcome.Telling;
                }

                cause = intent.TellCauseEventId;
                context.Events.Append(tick, agent.Id, CausalEventType.AgentCaughtInTime, agent.Body.Position, (int)kind, 0,
                    cause);
                Clear(agent);
                return Outcome.Caught;
            }

            // The flames inside their danger distance call a tell off -- except
            // gathering nerve, which is how somebody near the heat decides to
            // go through it: that is exactly where it happens.
            if ((inDanger && kind != AgentTell.GatheringNerve) || agent.Body.State != AgentBodyState.Upright ||
                agent.Burning.IsBurning)
            {
                Clear(agent);
                return Outcome.CalledOff;
            }

            if (tick >= intent.TellEndTick)
            {
                Clear(agent);
                intent.TellPassed = kind;
                intent.TellPassedTarget = target;
                return Outcome.Passed;
            }

            return Outcome.Telling;
        }

        /// <summary>
        /// Whether the hand is drawing them to the very door or thing they are
        /// winding up to go for: then it is no catch -- the player asked for it.
        /// A push, or a pull anywhere else, catches.
        /// </summary>
        private bool HandIsOnWhatTheyWant(AgentTell kind, int target)
        {
            InfluenceSystem.Place place = influence[0];
            return place.Pulls && target >= 0 &&
                   ((kind == AgentTell.GatheringNerve && place.Door == target) ||
                    (kind == AgentTell.TurningBack && place.Thing == target));
        }

        /// <summary>Standing still through a tell, facing the way they mean to go.</summary>
        public MotorIntent StandIntent(Agent agent) =>
            PanicIntent.StandAndFace(agent, agent.Intent.TellHeading, context.Scenario.Panic);

        /// <summary>
        /// Whether a tell of this kind about this target has just run its
        /// course uncaught: true once, and it is used up.
        /// </summary>
        public static bool TryPass(Agent agent, AgentTell kind, int target)
        {
            if (agent.Intent.TellPassed != kind || agent.Intent.TellPassedTarget != target)
            {
                return false;
            }

            agent.Intent.TellPassed = AgentTell.None;
            agent.Intent.TellPassedTarget = -1;
            return true;
        }

        /// <summary>What a person about to go somewhere makes of the walk there.</summary>
        public enum GoingBack
        {
            /// <summary>Off they go: the walk is safe, or the tell has run its course.</summary>
            Go,

            /// <summary>A tell has begun (or runs on): they stand, winding up.</summary>
            Wait,

            /// <summary>Caught a moment ago: they will not go back toward the flames yet.</summary>
            Refuse
        }

        /// <summary>
        /// Somebody frightened about to set off for <paramref name="target"/>:
        /// if the walk there passes near the flames (or ends near them) it earns
        /// a turning-back tell first. Safe walks go at once. Draws a number only
        /// when a tell begins.
        /// </summary>
        public GoingBack BeforeGoingBack(Agent agent, LogicalPosition target, int targetIndex, ulong cause,
            bool sentByTheHand)
        {
            // Sent by the player's hand: the player has already chosen, and
            // nobody winds up to what they were asked to do.
            if (!Enabled || sentByTheHand)
            {
                return GoingBack.Go;
            }

            LogicalPosition from = agent.Body.Position;
            int clearance = Rules.DangerousWalkClearanceMillimetres;
            if (!threats.AnyCloserThan(target, clearance) && !threats.RoutePassesNear(from, target, clearance))
            {
                return GoingBack.Go;
            }

            if (context.Tick < agent.Intent.TurnBackRefusedUntilTick)
            {
                return GoingBack.Refuse;
            }

            if (TryPass(agent, AgentTell.TurningBack, targetIndex))
            {
                return GoingBack.Go;
            }

            if (!IsTelling(agent))
            {
                Start(agent, AgentTell.TurningBack, IntegerMath.HeadingBetween(from, target, agent.Body.Heading), targetIndex,
                    cause);
            }

            return GoingBack.Wait;
        }

        /// <summary>
        /// Somebody about to go at the fire with a bottle: the fire itself is
        /// the danger, so it always earns a turning-back tell, unless the
        /// player's hand sent them.
        /// </summary>
        public GoingBack BeforeGoingAtTheFire(Agent agent, ulong cause, bool sentByTheHand)
        {
            if (threats.NearestDistanceSquared(agent.Body.Position, out LogicalPosition flames, out _) == long.MaxValue)
            {
                return GoingBack.Go;
            }

            return BeforeGoingBack(agent, flames, TheFire, cause, sentByTheHand);
        }

        /// <summary>
        /// A tell is dropped without an outcome: they snapped out of a freeze
        /// some other way, or calmed down. Nothing is written.
        /// </summary>
        public static void Forget(Agent agent)
        {
            Clear(agent);
            agent.Intent.TellPassed = AgentTell.None;
            agent.Intent.TellPassedTarget = -1;
        }

        /// <summary>The target a tell about a pull station names: apart from things' indices.</summary>
        public static int AlarmTarget(int alarm) => 1000000 + alarm;

        /// <summary>The target a tell about somebody down names: apart from things' indices.</summary>
        public static int PersonTarget(int index) => 2000000 + index;

        /// <summary>
        /// A turning back caught: they will not head back toward the flames for
        /// a while (<see cref="TellSettings.RefusesToGoBackTicks"/>, jittered).
        /// </summary>
        public void RefuseToGoBack(Agent agent)
        {
            agent.Intent.TurnBackRefusedUntilTick = checked(context.Tick + context.Jittered(Rules.RefusesToGoBackTicks));
        }

        private static void Clear(Agent agent)
        {
            AgentIntent intent = agent.Intent;
            intent.Tell = AgentTell.None;
            intent.TellCaughtAtTick = 0;
            intent.TellCauseEventId = 0UL;
        }

        /// <summary>How far through their tell somebody is, per mille, for the drawing: 0 at its start, 1000 at its end.</summary>
        public static int ProgressOf(Agent agent, int tick)
        {
            AgentIntent intent = agent.Intent;
            if (intent.Tell == AgentTell.None)
            {
                return 0;
            }

            int length = System.Math.Max(1, intent.TellEndTick - intent.TellStartTick);
            return System.Math.Max(0, System.Math.Min(1000, (tick - intent.TellStartTick) * 1000 / length));
        }
    }
}

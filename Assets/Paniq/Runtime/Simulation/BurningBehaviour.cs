namespace Paniq.Simulation
{
    /// <summary>
    /// People on fire. They do not drop dead on touching the flames: they
    /// run around wildly and screaming, changing direction every few tenths
    /// of a second and paying no attention to doors or other people, until
    /// they collapse. Anyone they touch may catch fire too. Someone on the
    /// floor when they catch fire burns where they lie.
    /// </summary>
    internal sealed class BurningBehaviour
    {
        private readonly SimulationContext context;
        private readonly Crowd crowd;
        private readonly BodySystem body;
        private readonly SoundSystem sound;
        private readonly Locomotion locomotion;
        private readonly FireSettings settings;

        public BurningBehaviour(SimulationContext context, Crowd crowd, BodySystem body, SoundSystem sound, Locomotion locomotion)
        {
            this.context = context;
            this.crowd = crowd;
            this.body = body;
            this.sound = sound;
            this.locomotion = locomotion;
            settings = context.Scenario.Fire;
        }

        /// <summary>This tick's wild run: scream now and then, lurch a new way now and then, full speed.</summary>
        public MotorIntent Decide(Agent agent)
        {
            int tick = context.Tick;
            AgentBurning burning = agent.Burning;
            if (tick >= burning.NextScreamTick)
            {
                sound.Yell(agent, burning.EventId);
                burning.NextScreamTick = checked(tick + context.Random.NextIntInclusive(
                    settings.BurningScreamMinimumTicks, settings.BurningScreamMaximumTicks));
            }

            if (tick >= burning.NextTurnTick || agent.Body.BlockedTicks >= settings.BurningBlockedTurnTicks)
            {
                // Some of them remember what they were told as children: down,
                // and roll. It is the one thing that can save them, and it is
                // not a decision so much as a reflex that some people have.
                // Only somebody on their feet can throw themselves down, so
                // being knocked over while alight never turns into a roll (and
                // never stretches out how long they are on the floor).
                if (agent.Body.State == AgentBodyState.Upright &&
                    context.Random.NextPercent(settings.DropAndRollChancePercent))
                {
                    body.DropAndRoll(agent, context.Random.NextIntInclusive(
                        settings.RollMinimumTicks, settings.RollMaximumTicks), burning.EventId);
                    return new MotorIntent(agent.Body.Heading, 0, agent.Personality.PanicTurnRate,
                        context.Scenario.Panic.Acceleration);
                }

                agent.Intent.LookHeading = context.Random.NextIntInclusive(0, 359);
                burning.NextTurnTick = checked(tick + context.Random.NextIntInclusive(
                    settings.BurningTurnMinimumTicks, settings.BurningTurnMaximumTicks));
                agent.Body.BlockedTicks = 0;
            }

            // Walls still turn them; people and boxes do not.
            int goalHeading = locomotion.Steer(agent, agent.Intent.LookHeading, 0, context.Scenario.Panic.WallAvoidPercent, 0);
            return new MotorIntent(goalHeading, agent.Personality.PanicSpeed, agent.Personality.PanicTurnRate,
                context.Scenario.Panic.Acceleration);
        }

        /// <summary>
        /// Phase 6, with the flames spreading: a roll that has run its course
        /// either worked or it did not. One draw at the end of the roll rather
        /// than one a tick, so the odds are the number in the scenario and you
        /// can read them: a third of rolls save the person. Ascending ID order,
        /// and only for people who went down on purpose — somebody knocked over
        /// while alight goes on burning.
        /// </summary>
        public void RollToPutItOut()
        {
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                Agent agent = agents[i];
                AgentBurning burning = agent.Burning;
                if (!agent.IsParticipating || !burning.IsBurning ||
                    burning.RollingUntilTick == 0 || context.Tick != burning.RollingUntilTick - 1 ||
                    agent.Body.State != AgentBodyState.Fallen)
                {
                    continue;
                }

                burning.RollingUntilTick = 0;
                if (context.Random.NextPercent(settings.RollPutsOutChancePercent))
                {
                    body.PutOutPerson(agent, burning.RollEventId);
                }
            }
        }

        /// <summary>
        /// Phase 6, after movement: each person who was already burning (in
        /// ascending ID order) may set alight anyone whose body is within a
        /// hand's breadth of theirs.
        /// </summary>
        public void SpreadFlames()
        {
            Agent[] agents = crowd.All;
            int radius = context.Scenario.World.OccupancyRadiusMillimetres;
            long reach = radius * 2L + settings.BurningSpreadGapMillimetres;
            long reachSquared = reach * reach;
            int burningCount = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Burning.IsBurning)
                {
                    burningCount++;
                }
            }

            if (burningCount == 0)
            {
                return;
            }

            // Only people burning at the start of this pass spread fire in it.
            var burners = new Agent[burningCount];
            int next = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Burning.IsBurning)
                {
                    burners[next++] = agents[i];
                }
            }

            for (int b = 0; b < burners.Length; b++)
            {
                Agent burner = burners[b];
                for (int i = 0; i < agents.Length; i++)
                {
                    Agent other = agents[i];
                    if (other == burner || !other.IsParticipating || other.Burning.IsBurning ||
                        LogicalPosition.DistanceSquared(burner.Body.Position, other.Body.Position) > reachSquared)
                    {
                        continue;
                    }

                    if (context.Random.NextPercent(settings.BurningSpreadChancePercent))
                    {
                        body.CatchFire(other, burner.Burning.EventId);
                    }
                }
            }
        }
    }
}

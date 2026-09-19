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

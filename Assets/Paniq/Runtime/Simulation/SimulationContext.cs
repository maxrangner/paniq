namespace Paniq.Simulation
{
    /// <summary>
    /// What every system in one run shares: the scenario it runs on, the
    /// clock, the one seeded random generator, and the causal event log.
    /// Systems receive this instead of reaching into each other's state.
    /// </summary>
    internal sealed class SimulationContext
    {
        public SimulationContext(FireReactionScenarioData scenario, ulong seed)
        {
            Scenario = scenario;
            Random = new Pcg32(seed);
        }

        public readonly FireReactionScenarioData Scenario;
        public readonly CausalEventLog Events = new CausalEventLog();

        /// <summary>
        /// The run's only random generator. It is a field, not a property, so
        /// every draw advances this one generator in place.
        /// </summary>
        public Pcg32 Random;

        /// <summary>The tick being processed; advanced only by FireReactionSimulation.Step.</summary>
        public int Tick;
    }
}

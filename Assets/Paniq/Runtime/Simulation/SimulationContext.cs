namespace Paniq.Simulation
{
    /// <summary>
    /// What every system in one run shares: the scenario it runs on, the
    /// clock, the one seeded random generator, and the causal event log.
    /// Systems receive this instead of reaching into each other's state.
    /// </summary>
    internal sealed class SimulationContext
    {
        public SimulationContext(ScenarioData scenario, ulong seed)
        {
            Scenario = scenario;
            Seed = seed;
            Random = new Pcg32(seed);
        }

        /// <summary>
        /// The run's seed, kept so a system that owns a stream of its own can
        /// derive it. See <see cref="DeckSystem"/>, which is the only one.
        /// </summary>
        public readonly ulong Seed;

        public readonly ScenarioData Scenario;
        public readonly CausalEventLog Events = new CausalEventLog();

        /// <summary>
        /// The run's only random generator. It is a field, not a property, so
        /// every draw advances this one generator in place.
        /// </summary>
        public Pcg32 Random;

        /// <summary>The tick being processed; advanced only by Run.Step.</summary>
        public int Tick;
    }
}

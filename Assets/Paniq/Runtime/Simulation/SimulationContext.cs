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

        /// <summary>
        /// A fixed length of time for one person, stretched or squeezed by a
        /// seeded amount of up to <see cref="WorldSettings.TimingJitterPercent"/>
        /// either way, and always at least a tick. Every moment somebody spends
        /// on something goes through this, so that two people who begin the
        /// same thing on the same tick finish it on different ones: the
        /// owner's rule is that nothing happens to a whole group on exactly
        /// the same tick. Draws one number from the run's generator, in
        /// whatever order it is called, like any other decision.
        /// </summary>
        public int Jittered(int ticks)
        {
            int spread = System.Math.Max(1, ticks * Scenario.World.TimingJitterPercent / 100);
            return System.Math.Max(1, ticks + Random.NextIntInclusive(-spread, spread));
        }

        /// <summary>
        /// How many ticks late one person's reaction to something begins: a
        /// few, drawn from the seed each time. Nobody reacts on the tick a
        /// thing happens (the owner's rule); every "think again now" and every
        /// startle goes through this. At least one tick, always.
        /// </summary>
        public int ReactionLag()
        {
            PerceptionSettings perception = Scenario.Perception;
            return System.Math.Max(1,
                Random.NextIntInclusive(perception.ReactionLagMinimumTicks, perception.ReactionLagMaximumTicks));
        }

        /// <summary>The tick a reaction begun now actually starts: now plus a person's own lag.</summary>
        public int ReactionTick() => checked(Tick + ReactionLag());

        /// <summary>
        /// Something has happened that this person should think again about:
        /// their next panicked decision is brought forward to a few ticks from
        /// now, their own lag late. Nothing happens if one is already due
        /// within the lag, so a cause that repeats every tick (a corner they
        /// keep seeing, a door they keep being near) cannot keep pushing the
        /// decision away from them -- which it did, and nobody ever decided.
        /// </summary>
        public void ThinkAgainSoon(AgentIntent intent)
        {
            if (intent.NextPanicDecisionTick > checked(Tick + Scenario.Perception.ReactionLagMaximumTicks))
            {
                intent.NextPanicDecisionTick = ReactionTick();
            }
        }
    }
}

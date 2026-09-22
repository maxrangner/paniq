namespace Paniq.Simulation
{
    /// <summary>
    /// What the player has left to spend. They start with a set amount, every
    /// card takes a bite out of it, and the only thing that pays any back is a
    /// person getting out of the building alive. Spend it all and save nobody,
    /// and there is nothing left to do but watch.
    /// <para>
    /// Influence is bookkeeping rather than something that happens in the
    /// world, so it is not an event in the causal log — for the same reason
    /// sitting down is not. Each card's own event records what it cost, so the
    /// log still accounts for where the influence went.
    /// </para>
    /// </summary>
    internal sealed class InfluenceSystem
    {
        private readonly InfluenceSettings settings;

        public InfluenceSystem(SimulationContext context)
        {
            settings = context.Scenario.Influence;
            Influence = settings.Starting;
        }

        /// <summary>What is left to spend.</summary>
        public int Influence { get; private set; }

        /// <summary>How much has been earned back by saving people, for the display.</summary>
        public int Earned { get; private set; }

        /// <summary>How much has been spent on cards, for the display.</summary>
        public int Spent { get; private set; }

        public int CostOf(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return settings.BeefcakeCost;
                case PlayerCommandType.SpawnFire: return settings.SpawnFireCost;
                case PlayerCommandType.SpawnExtinguisher: return settings.SpawnExtinguisherCost;
                case PlayerCommandType.BlastWall: return settings.BlastWallCost;

                // Clicking a door is not a card and costs nothing.
                default: return 0;
            }
        }

        public bool CanAfford(PlayerCommandType card) => Influence >= CostOf(card);

        /// <summary>
        /// Takes the price of a card. Call it only once the card has actually
        /// done something, so a refused card is free.
        /// </summary>
        public void Spend(PlayerCommandType card)
        {
            int cost = CostOf(card);
            Influence -= cost;
            Spent += cost;
        }

        /// <summary>Somebody got out alive.</summary>
        public void CreditPersonSaved()
        {
            int before = Influence;
            Influence = System.Math.Min(settings.Maximum, Influence + settings.PerPersonSaved);
            Earned += Influence - before;
        }
    }
}

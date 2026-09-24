using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The cards the player is holding, and where they come from: the dead deal
    /// them. The round opens with one card drawn at random (the owner's call,
    /// 2026-09-24: "start with one random card"), and every time somebody is
    /// killed one more is drawn and put on the player's bar.
    /// <para>
    /// This is the other half of the purse (see <see cref="InfluenceSystem"/>).
    /// The uproar fills the meter and the dead deal the cards, so the two
    /// currencies have one source each and neither pays twice for the same
    /// thing. A round nobody dies in leaves the player with money and nothing
    /// to spend it on but doors, which is a fair price for playing well.
    /// </para>
    /// <para>
    /// The draw comes from the run's one random generator, so a replay of the
    /// same seed deals the same cards in the same order.
    /// </para>
    /// </summary>
    internal sealed class DeckSystem
    {
        /// <summary>
        /// Every card that can be dealt. Not the whole command list: working a
        /// door is not a card, and setting the disaster going is the start
        /// button.
        /// </summary>
        private static readonly PlayerCommandType[] Deck =
        {
            // Three cards, by the owner's choice (2026-09-24): Beefcake, TNT
            // and the fire extinguisher. The trait cards, the fire and the
            // fuse box still exist as commands, and a level that wants them
            // deals them through its StartingHand; the office does not.
            PlayerCommandType.PlayBeefcake,
            PlayerCommandType.BlastWall,
            PlayerCommandType.SpawnExtinguisher
        };

        /// <summary>
        /// The deck's own stream selector. The run's crowd draws from the
        /// shared generator on sequence 54; the deck derives a second PCG32
        /// from the same scenario seed on sequence 55 and owns its state
        /// outright.
        /// <para>
        /// It has a stream of its own so that dealing a card does not shift
        /// everybody else's randomness. Sharing the one generator made the deck
        /// retune the whole game: a death would draw a number, and from then on
        /// every person in the building panicked, tripped and froze differently
        /// than they had before the deck existed. The simulation contract
        /// allows a derived stream on exactly these terms -- a named
        /// derivation, state owned here, and no effect on any run that does not
        /// deal a card.
        /// </para>
        /// </summary>
        private const ulong DeckSequence = 55UL;

        private readonly SimulationContext context;
        private readonly List<PlayerCommandType> hand = new List<PlayerCommandType>();
        private readonly List<PlayerCommandType> drawable = new List<PlayerCommandType>();
        private Pcg32 draws;

        public DeckSystem(SimulationContext context)
        {
            this.context = context;
            draws = new Pcg32(context.Seed, DeckSequence);
            PlayerCommandType[] opening = context.Scenario.Influence.StartingHand;
            if (opening != null)
            {
                hand.AddRange(opening);
            }

            // The opening draw: from the deck's own stream, so it moves no
            // other random number in the run, and written down as dealt by
            // nobody. Both finite cards are still in supply at the start.
            for (int i = 0; i < context.Scenario.Influence.OpeningDrawCount; i++)
            {
                Deal(default, default, 0UL, true, true);
            }
        }

        /// <summary>What the player is holding, in the order it was dealt.</summary>
        public IReadOnlyList<PlayerCommandType> Hand => hand;

        /// <summary>How many cards have been dealt all told, for the display.</summary>
        public int Dealt { get; private set; }

        public bool Holds(PlayerCommandType card) => hand.Contains(card);

        /// <summary>
        /// Takes one card off the bar. Called only once the card has actually
        /// done something, for the same reason its price is: a card that caught
        /// nobody was a miss, and a miss costs neither influence nor the card.
        /// </summary>
        public void Discard(PlayerCommandType card)
        {
            int at = hand.IndexOf(card);
            if (at >= 0)
            {
                hand.RemoveAt(at);
            }
        }

        /// <summary>
        /// Somebody has been killed: deal one card. The two cards backed by a
        /// finite supply drop out of the draw once that supply is gone, so the
        /// player is never handed a card that cannot be played.
        /// </summary>
        public void DealForDeath(
            SimulationId who,
            LogicalPosition where,
            ulong deathEventId,
            bool extinguishersLeft,
            bool blastChargesLeft)
        {
            Deal(who, where, deathEventId, extinguishersLeft, blastChargesLeft);
        }

        private void Deal(
            SimulationId who,
            LogicalPosition where,
            ulong deathEventId,
            bool extinguishersLeft,
            bool blastChargesLeft)
        {
            drawable.Clear();
            for (int i = 0; i < Deck.Length; i++)
            {
                PlayerCommandType card = Deck[i];
                if (card == PlayerCommandType.SpawnExtinguisher && !extinguishersLeft)
                {
                    continue;
                }

                if (card == PlayerCommandType.BlastWall && !blastChargesLeft)
                {
                    continue;
                }

                drawable.Add(card);
            }

            if (drawable.Count == 0)
            {
                return;
            }

            PlayerCommandType dealt = drawable[draws.NextIntInclusive(0, drawable.Count - 1)];
            hand.Add(dealt);
            Dealt++;
            context.Events.Append(
                context.Tick, who, CausalEventType.CardDealt, where, (int)dealt, 0, deathEventId);
        }
    }
}

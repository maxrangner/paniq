namespace Paniq.Simulation
{
    /// <summary>
    /// What the player has left to spend. The round opens with nothing, and
    /// the meter fills from the uproar: people shouting, running into each
    /// other, going down, catching fire, doors coming off their hinges,
    /// appliances going off. Saving somebody pays too. So the player cannot do
    /// anything at all until the building is in trouble, and the worse it gets
    /// the more they can do about it.
    /// <para>
    /// Deaths are deliberately not in here. A death deals a card instead (see
    /// <see cref="DeckSystem"/>), so it pays once rather than twice and the two
    /// currencies keep one source each: the uproar fills the meter, the dead
    /// deal the cards.
    /// </para>
    /// <para>
    /// Influence is bookkeeping rather than something that happens in the
    /// world, so it is not an event in the causal log — for the same reason
    /// sitting down is not. Each card's own event records what it cost, so the
    /// log still accounts for where the influence went.
    /// </para>
    /// </summary>
    /// <summary>How much of a commotion one kind of event is, to the player's meter.</summary>
    public enum UproarTier
    {
        Nothing,
        Small,
        Middling,
        Big
    }

    internal sealed class InfluenceSystem
    {
        private readonly InfluenceSettings settings;
        private readonly SimulationContext context;
        private int eventsRead;

        public InfluenceSystem(SimulationContext context)
        {
            this.context = context;
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
                case PlayerCommandType.PlayBeefcake:
                case PlayerCommandType.PlayCourage:
                case PlayerCommandType.PlayTerror:
                case PlayerCommandType.PlayBastard:
                case PlayerCommandType.PlayColdHeart:
                case PlayerCommandType.SpawnFire:
                case PlayerCommandType.SpawnExtinguisher:
                case PlayerCommandType.BlastWall:
                case PlayerCommandType.PopFuseBox:
                    return settings.CardCost;

                // A door click is priced by what the door is doing, not by the
                // command, so it is asked for separately; setting the disaster
                // going is the start button and is not spent on at all.
                default: return 0;
            }
        }

        /// <summary>
        /// What one click on a door in this state would cost. Turning the key
        /// costs most, walking it open costs less, pulling it shut costs least,
        /// and a door somebody has already broken down is past charging for.
        /// </summary>
        public int CostOfDoorClick(DoorState state)
        {
            switch (state)
            {
                case DoorState.Locked: return settings.UnlockDoorCost;
                case DoorState.Unlocked: return settings.OpenDoorCost;
                case DoorState.Open: return settings.CloseDoorCost;
                default: return 0;
            }
        }

        public bool CanAfford(PlayerCommandType card) => Influence >= CostOf(card);

        public bool CanAfford(int cost) => Influence >= cost;

        /// <summary>
        /// Takes the price of a card. Call it only once the card has actually
        /// done something, so a refused card is free.
        /// </summary>
        public void Spend(PlayerCommandType card) => Spend(CostOf(card));

        /// <summary>
        /// Takes a price worked out elsewhere, for the things whose cost
        /// depends on what they found rather than on which button was pressed.
        /// Same rule: only once it has actually done something.
        /// </summary>
        public void Spend(int cost)
        {
            Influence -= cost;
            Spent += cost;
        }

        /// <summary>Somebody got out alive.</summary>
        public void CreditPersonSaved()
        {
            Credit(settings.PerPersonSaved);
        }

        /// <summary>
        /// Everything that has happened since the last time this was asked,
        /// priced by how much of a commotion it is. Read off the causal log
        /// rather than reported by the behaviours, so nothing in the simulation
        /// has to know the player's purse exists — the same arrangement the
        /// head count uses.
        /// </summary>
        public void CreditUproar()
        {
            var log = context.Events.Events;
            for (int i = eventsRead; i < log.Count; i++)
            {
                Credit(UproarValueOf(log[i].EventType));
            }

            eventsRead = log.Count;
        }

        /// <summary>What one thing happening is worth to the meter: nothing, or one of three sizes.</summary>
        private int UproarValueOf(FireReactionEventType what)
        {
            switch (UproarTierOf(what))
            {
                case UproarTier.Big: return settings.UproarBig;
                case UproarTier.Middling: return settings.UproarMiddling;
                case UproarTier.Small: return settings.UproarSmall;
                default: return 0;
            }
        }

        /// <summary>
        /// Which size of commotion each kind of event is. Three sizes: somebody
        /// shouting or tripping is small, somebody going down or a door coming
        /// off its hinges is middling, and somebody catching fire or an
        /// appliance going off is big.
        /// <para>
        /// Every event type is named here, including the ones that pay nothing,
        /// and an event type left out is an error rather than a silent zero. It
        /// used to be a switch with a default of nothing, so a new event landed
        /// in the wrong tier by omission and no test could tell. A test now
        /// walks every value of the enum through this.
        /// </para>
        /// <para>
        /// The groups that pay nothing, each for its own reason. A death deals
        /// a card instead. Somebody escaping is already paid for by the head
        /// count. The player's own cards would otherwise refund themselves.
        /// Fire spreading square by square fires dozens of times a second in a
        /// room nobody is standing in: the fire pays through what it does to
        /// people and things, not through its own arithmetic. Somebody thinking
        /// (looking for a way out, finding one) is not a commotion. And the
        /// rest are bookkeeping.
        /// </para>
        /// </summary>
        internal static UproarTier UproarTierOf(FireReactionEventType what)
        {
            switch (what)
            {
                case FireReactionEventType.AgentCaughtFire:
                case FireReactionEventType.AgentPassedOut:
                case FireReactionEventType.AgentCrushed:
                case FireReactionEventType.ObjectExploded:
                case FireReactionEventType.DoorBrokenDown:
                    return UproarTier.Big;

                case FireReactionEventType.AgentKnockedDown:
                case FireReactionEventType.AgentShoved:
                case FireReactionEventType.AgentGrabbed:
                case FireReactionEventType.AgentForcedDoor:
                case FireReactionEventType.AgentBarricadedDoor:
                case FireReactionEventType.ObjectBroke:
                case FireReactionEventType.DoorBurntThrough:
                case FireReactionEventType.BoxHitAgent:
                case FireReactionEventType.AlarmPulled:
                    return UproarTier.Middling;

                case FireReactionEventType.AgentYelled:
                case FireReactionEventType.AgentScared:
                case FireReactionEventType.AgentTripped:
                case FireReactionEventType.AgentFroze:
                case FireReactionEventType.AgentsCollided:
                case FireReactionEventType.AgentShovedObstruction:
                case FireReactionEventType.ObjectCaughtFire:
                case FireReactionEventType.ItemThrown:
                case FireReactionEventType.BoxBumped:
                    return UproarTier.Small;

                // A death deals a card; the head count pays for an escape.
                case FireReactionEventType.AgentLost:
                case FireReactionEventType.AgentEscaped:
                case FireReactionEventType.AgentRescued:
                case FireReactionEventType.AgentSurvived:
                case FireReactionEventType.CardDealt:

                // The player's own doing.
                case FireReactionEventType.PowerBeefcake:
                case FireReactionEventType.PowerCourage:
                case FireReactionEventType.PowerTerror:
                case FireReactionEventType.PowerBastard:
                case FireReactionEventType.PowerColdHeart:
                case FireReactionEventType.PowerSpawnedFire:
                case FireReactionEventType.PowerSpawnedExtinguisher:
                case FireReactionEventType.PowerBlastedWall:
                case FireReactionEventType.PowerPoppedFuseBox:
                case FireReactionEventType.DoorUnlocked:
                case FireReactionEventType.RoundEventTriggered:
                case FireReactionEventType.RoundEnded:

                // The hazard's own arithmetic.
                case FireReactionEventType.FireActivated:
                case FireReactionEventType.FireSpread:
                case FireReactionEventType.FireDoused:
                case FireReactionEventType.ObjectBurntOut:
                case FireReactionEventType.PowerSparkStarted:
                case FireReactionEventType.PowerSparkArrived:

                // Somebody thinking, or somebody being told.
                case FireReactionEventType.AgentAlerted:
                case FireReactionEventType.AgentNoticedSound:
                case FireReactionEventType.AgentUnfroze:
                case FireReactionEventType.AgentLookedForAWayOut:
                case FireReactionEventType.AgentFoundADeadEnd:
                case FireReactionEventType.AgentFoundTheWayOut:
                case FireReactionEventType.LeaderCalledPeopleOn:
                case FireReactionEventType.LeaderOrderedDoorBroken:
                case FireReactionEventType.LeaderOrderedFireFought:

                // Bookkeeping: things happening quietly to people, things and doors.
                case FireReactionEventType.AgentGotUp:
                case FireReactionEventType.AgentCameTo:
                case FireReactionEventType.AgentRolled:
                case FireReactionEventType.AgentDoused:
                case FireReactionEventType.AgentBlasted:
                case FireReactionEventType.AgentShookAwake:
                case FireReactionEventType.AgentDropped:
                case FireReactionEventType.AgentTriedDoor:
                case FireReactionEventType.AgentGaveUpOnDoor:
                case FireReactionEventType.AgentTookExtinguisher:
                case FireReactionEventType.ExtinguisherSprayed:
                case FireReactionEventType.ExtinguisherEmptied:
                case FireReactionEventType.ItemDropped:
                case FireReactionEventType.BoxesCollided:
                case FireReactionEventType.DoorOpened:
                case FireReactionEventType.DoorClosed:
                case FireReactionEventType.DoorLocked:
                case FireReactionEventType.DoorBlocked:
                case FireReactionEventType.DoorUnblocked:
                case FireReactionEventType.AlarmRang:
                    return UproarTier.Nothing;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(what),
                        $"{what} has no uproar tier. Every event type must say what it pays, even if that is nothing.");
            }
        }

        /// <summary>
        /// Puts influence in the purse for a test whose subject is something
        /// else. A round opens with nothing, so a test that wants to work a
        /// door in its first tick -- checking the scene is wired up, say --
        /// cannot get there by playing properly.
        /// </summary>
        public void GiveForTests(int amount) => Credit(amount);

        private void Credit(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int before = Influence;
            Influence = System.Math.Min(settings.Maximum, Influence + amount);
            Earned += Influence - before;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The cards you throw into the crowd. Each one is aimed at a patch of
    /// floor, not at a chosen person, and slams one dial to the end of its
    /// scale for everybody standing inside it, for the rest of the round.
    /// <para>
    /// A throw that catches nobody is a miss and costs neither the purse nor
    /// the card. A throw that catches the wrong person is spent: that is the
    /// whole of the player's accuracy, and it is why the game draws a circle on
    /// the floor before they let go.
    /// </para>
    /// </summary>
    public sealed class TraitCardsEditModeTests
    {
        private ScenarioAsset scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = ScenarioAsset.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private static List<CausalEvent> EventsOfType(Run simulation, CausalEventType type)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        /// <summary>
        /// A quiet office with people standing exactly where the test puts
        /// them, nothing happening, and every card in hand.
        /// </summary>
        private ScenarioData QuietRoomWith(params LogicalPosition[] people)
        {
            ScenarioData data =
                TheBuilding.WithThePlayerAbleToAct(TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData()));

            var crowd = new AgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                crowd[i] = new AgentDefinition(new SimulationId((ulong)(1 + i)), people[i],
                    CardinalDirection.North, AgentTraitValues.AllOrdinary);
            }

            data.Agents = crowd;
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        [TestCase(PlayerCommandType.PlayBeefcake, AgentTrait.Strength, AgentTraitValues.Maximum)]
        [TestCase(PlayerCommandType.PlayCourage, AgentTrait.Bravery, AgentTraitValues.Maximum)]
        [TestCase(PlayerCommandType.PlayTerror, AgentTrait.Nervousness, AgentTraitValues.Maximum)]
        [TestCase(PlayerCommandType.PlayBastard, AgentTrait.Evil, AgentTraitValues.Maximum)]
        [TestCase(PlayerCommandType.PlayColdHeart, AgentTrait.Compassion, AgentTraitValues.Minimum)]
        public void EachCard_MovesItsOwnDialAndLeavesTheRestAlone(
            PlayerCommandType card, AgentTrait dial, int end)
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            using (var simulation = new Run(data))
            {
                AgentTraitValues before = simulation.GetAgent(0).Traits;
                simulation.QueueCommand(card, new LogicalPosition(0, 0), 1);
                simulation.Step();
                simulation.Step();

                AgentTraitValues after = simulation.GetAgent(0).Traits;
                Assert.That(after.Of(dial), Is.EqualTo(end), "The card's own dial goes to the end of its scale.");
                Assert.That(after, Is.EqualTo(before.With(dial, end)), "And nothing else about them changes.");
            }
        }

        [Test]
        public void AThrow_CatchesEverybodyInsideThePatchAndNobodyOutsideIt()
        {
            // Three people in a line: two well inside a 1.5 m patch thrown at
            // the origin, one well outside it.
            ScenarioData data = QuietRoomWith(
                new LogicalPosition(0, 0),
                new LogicalPosition(1000, 0),
                new LogicalPosition(4000, 0));

            using (var simulation = new Run(data))
            {
                Assume.That(data.Purse.CardPatchRadiusMillimetres, Is.EqualTo(1500));
                simulation.QueueCommand(PlayerCommandType.PlayCourage, new LogicalPosition(0, 0), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.GetAgent(0).Traits.Bravery, Is.EqualTo(AgentTraitValues.Maximum));
                Assert.That(simulation.GetAgent(1).Traits.Bravery, Is.EqualTo(AgentTraitValues.Maximum));
                Assert.That(simulation.GetAgent(2).Traits.Bravery, Is.EqualTo(AgentTraitValues.Ordinary),
                    "Four metres away is outside the patch.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerCourage).Count, Is.EqualTo(2),
                    "One line in the log for each person it caught.");
            }
        }

        [Test]
        public void ThePatch_IsARoundOneRatherThanASquare()
        {
            // 1200 mm on both axes is 1697 mm away, which is outside a 1500 mm
            // patch but inside the box the spatial index gathers. Without the
            // exact test the corners would catch people the circle never
            // touched -- and the circle is what the player was shown.
            ScenarioData data = QuietRoomWith(new LogicalPosition(1200, 1200));
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PlayCourage, new LogicalPosition(0, 0), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.GetAgent(0).Traits.Bravery, Is.EqualTo(AgentTraitValues.Ordinary));
                Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting), "So the throw was a miss.");
            }
        }

        [Test]
        public void AThrowThatCatchesNobody_CostsNeitherPurseNorTheCard()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            data.Purse.StartingHand = new[] { PlayerCommandType.PlayCourage };

            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PlayCourage, new LogicalPosition(5000, 5000), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting));
                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1), "The card is still in hand.");
            }
        }

        [Test]
        public void AThrowThatCatchesSomebodyAlreadyAtThatEnd_StillCountsIfItMovedAnybody()
        {
            // One person already fearless, one not. The card changed somebody,
            // so it is paid for and gone.
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0), new LogicalPosition(700, 0));
            data.Agents[0] = new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
                CardinalDirection.North, AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, AgentTraitValues.Maximum));
            data.Purse.StartingHand = new[] { PlayerCommandType.PlayCourage };

            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PlayCourage, new LogicalPosition(0, 0), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Purse,
                    Is.EqualTo(data.Purse.Starting - data.Purse.CardCost), "It is paid for.");
                Assert.That(simulation.GetSnapshot().Hand, Is.Empty, "And it leaves the hand.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerCourage).Count, Is.EqualTo(1),
                    "Only the person it actually changed is in the log.");
            }
        }

        [Test]
        public void AThrowThatCatchesOnlyPeopleAlreadyAtThatEnd_IsFree()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            data.Agents[0] = new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
                CardinalDirection.North, AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, AgentTraitValues.Maximum));
            data.Purse.StartingHand = new[] { PlayerCommandType.PlayCourage };

            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PlayCourage, new LogicalPosition(0, 0), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting),
                    "A card that moved nobody's dial did nothing, and a card that does nothing is free.");
                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void AThrow_DoesNotReachTheDead()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            data.Fire.ActivationTick = 10;
            TheBuilding.FireAt(data, new LogicalPosition(0, 0));
            data.Purse.StartingHand = new[] { PlayerCommandType.PlayCourage };

            using (var simulation = new Run(data))
            {
                for (int tick = 0; tick < 2000 && simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Lost; tick++)
                {
                    simulation.Step();
                }

                Assume.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Lost),
                    "Standing in the fire is meant to be fatal.");

                int purse = simulation.Purse;
                simulation.QueueCommand(PlayerCommandType.PlayCourage, simulation.GetAgent(0).Position,
                    simulation.Tick + 1);
                simulation.Step();
                simulation.Step();

                Assert.That(EventsOfType(simulation, CausalEventType.PowerCourage), Is.Empty,
                    "The dead are out of the run and out of the patch.");
                Assert.That(simulation.Purse, Is.GreaterThanOrEqualTo(purse), "So the throw was a miss.");
            }
        }

        [Test]
        public void ACardStaysPlayedForTheRestOfTheRound()
        {
            ScenarioData data = QuietRoomWith(new LogicalPosition(0, 0));
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PlayTerror, new LogicalPosition(0, 0), 1);
                for (int tick = 0; tick < 1000; tick++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.GetAgent(0).Traits.Nervousness, Is.EqualTo(AgentTraitValues.Maximum),
                    "A dial the player moved stays where they put it.");
            }
        }
    }
}

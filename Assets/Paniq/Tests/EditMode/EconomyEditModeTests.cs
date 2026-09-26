using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The two currencies: the uproar fills the meter, and the dead deal the
    /// cards. A round opens with neither, so the first move is to watch, and
    /// the building has to get into trouble before there is anything to spend
    /// or anything to spend it on.
    /// </summary>
    public sealed class EconomyEditModeTests
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

        /// <summary>A quiet room, one ordinary person, no fire due, nothing happening.</summary>
        private ScenarioData QuietRoom()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
                    CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        /// <summary>
        /// The floor with the fire pinned to one square and somebody standing
        /// on it, so the run is certain to kill at least one person.
        /// <para>
        /// These tests used to run the shipped scenario on a seed that happened
        /// to kill somebody. That is not a property of the seed, it is a
        /// property of how the crowd behaves, so the day the crowd learned to
        /// find its own way out the deaths went away and three tests about
        /// dealing cards had nothing to deal. A death this test needs is a
        /// death this test arranges.
        /// </para>
        /// </summary>
        private ScenarioData FloorWithACertainDeath()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            TheBuilding.FireAt(data, TheBuilding.Office);
            data.Fire.ActivationTick = 1;

            // No opening card, so every card in hand was dealt by a death.
            data.Purse.OpeningDrawCount = 0;

            // Nobody puts it out, and nobody hauls them clear.
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;

            var people = new List<AgentDefinition>(data.Agents)
            {
                new AgentDefinition(new SimulationId(1999UL), TheBuilding.Office,
                    CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Agents = people.ToArray();
            return data;
        }

        /// <summary>
        /// Prototype 3 (2026-09-25, the owner's call): the office level has
        /// no purse. With the purse switched off every price is nought,
        /// nothing is paid in, a door and an alarm work for nothing, and the
        /// cards are still dealt and still played. The rules are all still
        /// there for a level that turns it back on.
        /// </summary>
        [Test]
        public void WithThePurseSwitchedOff_EverythingIsFree_AndNothingIsPaidIn()
        {
            ScenarioData data = QuietRoom();
            data.Purse.Enabled = false;
            data.Purse.Starting = 0;
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.GetSnapshot().PurseEnabled, Is.False);
                Assert.That(simulation.GetSnapshot().CostOfDoorClick(DoorState.Locked, true), Is.Zero, "The way out is free to unlock.");
                Assert.That(simulation.GetSnapshot().CostOf(PlayerCommandType.PullAlarm), Is.Zero);
                Assert.That(simulation.GetSnapshot().Hand, Has.Count.EqualTo(1), "The opening card is still dealt.");

                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 1);
                simulation.QueueCommand(PlayerCommandType.PullAlarm, TheBuilding.TheAlarm, 2);
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOfType(simulation, CausalEventType.DoorUnlocked), Has.Count.EqualTo(1), "Unlocked, with an empty purse.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerPulledAlarm), Has.Count.EqualTo(1), "Pulled, with an empty purse.");
                Assert.That(simulation.Purse, Is.Zero, "Nothing paid in by the uproar of the bells.");
                Assert.That(simulation.PurseSpent, Is.Zero);
            }
        }

        /// <summary>
        /// A round opens with thirty and one card drawn from the three-card
        /// deck (the owner's call, 2026-09-24). The same seed opens with the
        /// same card; the draw comes from the deck's own stream, so it moves
        /// nothing else in the run. It used to open with nothing at all.
        /// </summary>
        [Test]
        public void ARoundOpens_WithThirtyAndOneOfTheThreeCards()
        {
            ScenarioData data = QuietRoom();
            PlayerCommandType first;
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.Purse, Is.EqualTo(30), "One move's worth to start.");
                Assert.That(simulation.GetSnapshot().Hand, Has.Count.EqualTo(1), "And one card to spend it on.");
                first = simulation.GetSnapshot().Hand[0];
                Assert.That(first, Is.EqualTo(PlayerCommandType.PlayBeefcake)
                    .Or.EqualTo(PlayerCommandType.BlastWall)
                    .Or.EqualTo(PlayerCommandType.SpawnExtinguisher)
                    .Or.EqualTo(PlayerCommandType.StickTogether), "The deck is Beefcake, TNT, the fire extinguisher and Stick together.");
                List<CausalEvent> dealt = EventsOfType(simulation, CausalEventType.CardDealt);
                Assert.That(dealt, Has.Count.EqualTo(1));
                Assert.That(dealt[0].SourceId.Value, Is.EqualTo(0UL), "Dealt by nobody's death.");
                Assert.That((PlayerCommandType)dealt[0].Strength, Is.EqualTo(first));
            }

            using (var again = new Run(QuietRoom(), 42UL))
            {
                Assert.That(again.GetSnapshot().Hand[0], Is.EqualTo(first), "The same seed opens with the same card.");
            }
        }

        [Test]
        public void WithAnEmptyHand_ACardDoesNothingHoweverRichThePlayerIs()
        {
            ScenarioData data = QuietRoom();
            data.Purse.Starting = 100000;
            data.Purse.Maximum = 100000;
            data.Purse.StartingHand = new PlayerCommandType[0];

            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Purse, Is.EqualTo(100000), "A card they are not holding costs nothing.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerSpawnedFire), Is.Empty,
                    "And does nothing.");
            }
        }

        [Test]
        public void PlayingACard_TakesItOutOfTheHand()
        {
            ScenarioData data = QuietRoom();
            data.Purse.Starting = 100000;
            data.Purse.Maximum = 100000;
            data.Purse.UproarSmall = 0;
            data.Purse.UproarMiddling = 0;
            data.Purse.UproarBig = 0;
            data.Purse.StartingHand = new[] { PlayerCommandType.SpawnFire, PlayerCommandType.SpawnFire };
            data.Purse.OpeningDrawCount = 0;

            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1), "One of the two was spent.");
                Assert.That(simulation.Purse, Is.EqualTo(100000 - data.Purse.CardCost));
            }
        }

        [Test]
        public void ACardThatDoesNothing_CostsNeitherPurseNorTheCard()
        {
            ScenarioData data = QuietRoom();
            data.Purse.Starting = 100000;
            data.Purse.Maximum = 100000;
            data.Purse.UproarSmall = 0;
            data.Purse.UproarMiddling = 0;
            data.Purse.UproarBig = 0;
            data.Purse.StartingHand = new[] { PlayerCommandType.SpawnFire, PlayerCommandType.SpawnFire };
            data.Purse.OpeningDrawCount = 0;

            using (var simulation = new Run(data, 42UL))
            {
                // The same square twice. The second is refused, because that
                // square is already alight.
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();
                int afterTheFirst = simulation.Purse;

                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000),
                    simulation.Tick + 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Purse, Is.EqualTo(afterTheFirst), "A miss costs nothing.");
                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1), "And the card is still in hand.");
            }
        }

        [Test]
        public void TheUproar_FillsTheMeter()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting));
                for (int tick = 0; tick < 1500; tick++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.Purse, Is.GreaterThan(data.Purse.Starting),
                    "A building well alight should have paid the player something.");
            }
        }

        [Test]
        public void ADeath_DealsExactlyOneCardAndNamesItselfAsTheCause()
        {
            ScenarioData data = FloorWithACertainDeath();
            using (var simulation = new Run(data, 42UL))
            {
                for (int tick = 0; tick < 3000; tick++)
                {
                    simulation.Step();
                }

                List<CausalEvent> deaths = EventsOfType(simulation, CausalEventType.AgentLost);
                List<CausalEvent> dealt = EventsOfType(simulation, CausalEventType.CardDealt);
                Assume.That(deaths, Is.Not.Empty, "The floor is arranged so somebody burns.");

                Assert.That(dealt.Count, Is.EqualTo(deaths.Count), "One card per death, no more and no fewer.");
                foreach (CausalEvent card in dealt)
                {
                    Assert.That(card.CausalParentEventId, Is.Not.EqualTo(0UL),
                        "A dealt card names the death that dealt it.");
                }
            }
        }

        [Test]
        public void ADeath_PaysInCardsRatherThanInPurse()
        {
            ScenarioData data = FloorWithACertainDeath();

            // Nothing pays but a death, so any purse points at all would have had
            // to come from one.
            data.Purse.UproarSmall = 0;
            data.Purse.UproarMiddling = 0;
            data.Purse.UproarBig = 0;
            data.Purse.PerPersonSaved = 0;

            using (var simulation = new Run(data, 42UL))
            {
                for (int tick = 0; tick < 3000; tick++)
                {
                    simulation.Step();
                }

                Assume.That(EventsOfType(simulation, CausalEventType.AgentLost), Is.Not.Empty,
                    "The floor is arranged so somebody burns.");
                Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting), "A death fills no meter.");
                Assert.That(simulation.GetSnapshot().Hand, Is.Not.Empty, "It deals a card instead.");
            }
        }

        // TheSameSeed_DealsTheSameCardsInTheSameOrder ran the building twice
        // to check the deal repeated. Every replay fingerprint hashes the whole
        // event log, CardDealt lines included, so a deal that wandered would
        // move twenty recorded numbers before it got here.

    }
}

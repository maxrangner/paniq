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
    public sealed class FireReactionEconomyEditModeTests
    {
        private FireReactionScenario scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = FireReactionScenario.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private static List<CausalEvent> EventsOfType(FireReactionSimulation simulation, FireReactionEventType type)
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
        private FireReactionScenarioData QuietRoom()
        {
            FireReactionScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
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
        private FireReactionScenarioData FloorWithACertainDeath()
        {
            FireReactionScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            TheBuilding.FireAt(data, TheBuilding.Office);
            data.Fire.ActivationTick = 1;

            // Nobody puts it out, and nobody hauls them clear.
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;

            var people = new List<FireReactionAgentDefinition>(data.Agents)
            {
                new FireReactionAgentDefinition(new SimulationId(1999UL), TheBuilding.Office,
                    CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Agents = people.ToArray();
            return data;
        }

        [Test]
        public void ARoundOpens_WithAnEmptyPurseAndAnEmptyHand()
        {
            FireReactionScenarioData data = QuietRoom();
            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                Assert.That(simulation.Influence, Is.EqualTo(0), "The player starts with nothing to spend.");
                Assert.That(simulation.GetSnapshot().Hand, Is.Empty, "And nothing to spend it on.");
            }
        }

        [Test]
        public void WithAnEmptyHand_ACardDoesNothingHoweverRichThePlayerIs()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Influence.Starting = 100000;
            data.Influence.Maximum = 100000;
            data.Influence.StartingHand = new PlayerCommandType[0];

            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Influence, Is.EqualTo(100000), "A card they are not holding costs nothing.");
                Assert.That(EventsOfType(simulation, FireReactionEventType.PowerSpawnedFire), Is.Empty,
                    "And does nothing.");
            }
        }

        [Test]
        public void PlayingACard_TakesItOutOfTheHand()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Influence.Starting = 100000;
            data.Influence.Maximum = 100000;
            data.Influence.UproarSmall = 0;
            data.Influence.UproarMiddling = 0;
            data.Influence.UproarBig = 0;
            data.Influence.StartingHand = new[] { PlayerCommandType.SpawnFire, PlayerCommandType.SpawnFire };

            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1), "One of the two was spent.");
                Assert.That(simulation.Influence, Is.EqualTo(100000 - data.Influence.CardCost));
            }
        }

        [Test]
        public void ACardThatDoesNothing_CostsNeitherInfluenceNorTheCard()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Influence.Starting = 100000;
            data.Influence.Maximum = 100000;
            data.Influence.UproarSmall = 0;
            data.Influence.UproarMiddling = 0;
            data.Influence.UproarBig = 0;
            data.Influence.StartingHand = new[] { PlayerCommandType.SpawnFire, PlayerCommandType.SpawnFire };

            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                // The same square twice. The second is refused, because that
                // square is already alight.
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000), 1);
                simulation.Step();
                simulation.Step();
                int afterTheFirst = simulation.Influence;

                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(1000, 1000),
                    simulation.Tick + 1);
                simulation.Step();
                simulation.Step();

                Assert.That(simulation.Influence, Is.EqualTo(afterTheFirst), "A miss costs no influence.");
                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(1), "And the card is still in hand.");
            }
        }

        [Test]
        public void TheUproar_FillsTheMeter()
        {
            FireReactionScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                Assert.That(simulation.Influence, Is.EqualTo(0));
                for (int tick = 0; tick < 1500; tick++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.Influence, Is.GreaterThan(0),
                    "A building well alight should have paid the player something.");
            }
        }

        [Test]
        public void ADeath_DealsExactlyOneCardAndNamesItselfAsTheCause()
        {
            FireReactionScenarioData data = FloorWithACertainDeath();
            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                for (int tick = 0; tick < 3000; tick++)
                {
                    simulation.Step();
                }

                List<CausalEvent> deaths = EventsOfType(simulation, FireReactionEventType.AgentLost);
                List<CausalEvent> dealt = EventsOfType(simulation, FireReactionEventType.CardDealt);
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
        public void ADeath_PaysInCardsRatherThanInInfluence()
        {
            FireReactionScenarioData data = FloorWithACertainDeath();

            // Nothing pays but a death, so any influence at all would have had
            // to come from one.
            data.Influence.UproarSmall = 0;
            data.Influence.UproarMiddling = 0;
            data.Influence.UproarBig = 0;
            data.Influence.PerPersonSaved = 0;

            using (var simulation = new FireReactionSimulation(data, 42UL))
            {
                for (int tick = 0; tick < 3000; tick++)
                {
                    simulation.Step();
                }

                Assume.That(EventsOfType(simulation, FireReactionEventType.AgentLost), Is.Not.Empty,
                    "The floor is arranged so somebody burns.");
                Assert.That(simulation.Influence, Is.EqualTo(0), "A death fills no meter.");
                Assert.That(simulation.GetSnapshot().Hand, Is.Not.Empty, "It deals a card instead.");
            }
        }

        // TheSameSeed_DealsTheSameCardsInTheSameOrder ran the building twice
        // to check the deal repeated. Every replay fingerprint hashes the whole
        // event log, CardDealt lines included, so a deal that wandered would
        // move twenty recorded numbers before it got here.

    }
}

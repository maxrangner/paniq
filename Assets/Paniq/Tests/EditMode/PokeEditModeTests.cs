using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Poking people (prototype 3, 2026-09-25): a click on somebody makes
    /// them lurch, look round a beat later, and after a few pokes in a row
    /// get annoyed. A poke is free, not a card, and frightens nobody.
    /// </summary>
    public sealed class PokeEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);

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

        /// <summary>One ordinary person standing still in the middle of the office, facing north, with no fire due.</summary>
        private ScenarioData QuietRoom()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, TheBuilding.Office, CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Tables = Array.Empty<TableDefinition>();
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void APoke_IsFree_AndShovesThemBackwards_AndTheyLookRoundABeatLater()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                LogicalPosition before = simulation.GetAgent(Somebody).Position;
                int purse = simulation.Influence;
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1);
                Advance(simulation, 20);

                List<CausalEvent> poked = EventsOfType(simulation, CausalEventType.PowerPoked);
                Assert.That(poked, Has.Count.EqualTo(1));
                Assert.That(poked[0].TargetId, Is.EqualTo(Somebody));
                Assert.That(poked[0].HasCausalParent, Is.False, "The player is the root cause.");
                Assert.That(simulation.Influence, Is.EqualTo(purse), "A poke costs nothing.");

                LogicalPosition after = simulation.GetAgent(Somebody).Position;
                Assert.That(after.Z, Is.LessThan(before.Z - 50), "Facing north, the poke sends them south.");

                List<CausalEvent> looked = EventsOfType(simulation, CausalEventType.AgentPoked);
                Assert.That(looked, Has.Count.EqualTo(1), "They look round for whoever did it.");
                Assert.That(looked[0].Tick, Is.GreaterThan(poked[0].Tick), "Not on the tick of the poke: every reaction comes a beat late.");
                Assert.That(looked[0].CausalParentEventId, Is.EqualTo(poked[0].EventId));
                Assert.That(simulation.GetAgent(Somebody).FearState, Is.EqualTo(AgentFearState.Calm), "A poke frightens nobody.");
            }
        }

        [Test]
        public void ThreePokesInARow_AnnoyThem()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1);
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 60);
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 120);
                Advance(simulation, 150);

                Assert.That(EventsOfType(simulation, CausalEventType.AgentPoked), Has.Count.EqualTo(2), "The first two only turn their head.");
                List<CausalEvent> annoyed = EventsOfType(simulation, CausalEventType.AgentAnnoyed);
                Assert.That(annoyed, Has.Count.EqualTo(1), "The third in a row is one too many.");
                Assert.That(annoyed[0].SourceId, Is.EqualTo(Somebody));
                Assert.That(annoyed[0].Tick, Is.GreaterThan(120));
            }
        }

        [Test]
        public void ThreePokesFarApart_AnnoyNobody()
        {
            ScenarioData data = QuietRoom();
            int apart = data.Poke.AnnoyedWindowTicks + 10;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1);
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1 + apart);
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1 + 2 * apart);
                Advance(simulation, 3 * apart);

                Assert.That(EventsOfType(simulation, CausalEventType.AgentAnnoyed), Is.Empty);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentPoked), Has.Count.EqualTo(3));
            }
        }

        /// <summary>Somebody sitting down feels the poke and looks round, but keeps their seat.</summary>
        [Test]
        public void SomebodySeated_KeepsTheirSeat()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            SimulationId seated = default;
            foreach (AgentDefinition person in data.Agents)
            {
                if (person.StartsSeated)
                {
                    seated = person.AgentId;
                    break;
                }
            }

            Assert.That(seated.Value, Is.Not.Zero, "The office has people who start seated.");
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                LogicalPosition before = simulation.GetAgent(seated).Position;
                simulation.QueueCommand(PlayerCommandType.PokePerson, seated, simulation.Tick + 1);
                Advance(simulation, 20);

                Assert.That(simulation.GetAgent(seated).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Still in the chair.");
                Assert.That(IntegerMath.Distance(before, simulation.GetAgent(seated).Position), Is.LessThan(50), "Not shoved out of it.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentPoked), Has.Count.EqualTo(1), "But they felt it.");
            }
        }

        [Test]
        public void PokingNobody_IsRefused()
        {
            using (var simulation = new Run(QuietRoom(), 42UL))
            {
                Assert.Throws<ArgumentException>(() =>
                    simulation.QueueCommand(PlayerCommandType.PokePerson, new SimulationId(999UL), 1));
            }
        }

        [Test]
        public void TheStory_SaysWhoPokedWhom()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PokePerson, Somebody, 1);
                Advance(simulation, 20);
                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.PowerPoked)[0]), Is.EqualTo("you poked person 1"));
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.AgentPoked)[0]),
                    Is.EqualTo("person 1 looked round for whoever poked them"));
            }
        }
    }
}

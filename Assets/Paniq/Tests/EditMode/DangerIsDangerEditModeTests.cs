using System;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// People sense danger, never "floor on fire" (the owner's rule,
    /// 2026-09-26): a thing on fire and a person on fire frighten whoever
    /// sees them, the way burning floor always has, so the next danger -- a
    /// zombie, say -- is one more of the same. Before this, a waste bin
    /// blazing in a room full of people went unnoticed until the carpet
    /// caught, and somebody alight frightened nobody who only saw them.
    /// </summary>
    public sealed class DangerIsDangerEditModeTests
    {
        private static readonly SimulationId BinByTheDoor = new SimulationId(3205UL);

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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        private ScenarioData Quiet(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        [Test]
        public void ABinOnFire_FrightensWhoeverSeesIt_BeforeAnyFloorIsAlight()
        {
            // Three metres from the bin by the meeting room's door, facing its way.
            ScenarioData data = Quiet(new AgentDefinition(new SimulationId(1UL), new LogicalPosition(1200, 11900),
                CardinalDirection.South, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                int bin = objects.IndexOf(BinByTheDoor);
                simulation.FireForTests.StartWithoutFlames(objects.PositionOf(bin), 0UL);
                Assert.That(simulation.FlammablesForTests.IgniteObject(bin, 0UL), "The bin caught.");
                Advance(simulation, 50);

                Assert.That(simulation.FireForTests.BurningCount, Is.Zero, "Not a square of floor alight.");
                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                    "A bin in flames three metres away is a fright.");
            }
        }

        [Test]
        public void SomebodyOnFire_FrightensWhoeverSeesThem()
        {
            ScenarioData data = Quiet(
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, -4000), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(1000, -4000), CardinalDirection.West,
                    AgentTraitValues.AllOrdinary));
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, TheBuilding.Maintenance);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                simulation.SetAlightForTests(1);
                Advance(simulation, 2);

                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                    "Somebody alight four metres in front of you is a fright, before any scream.");
            }
        }
    }
}

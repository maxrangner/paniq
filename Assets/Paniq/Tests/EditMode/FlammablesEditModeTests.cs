using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Boxes, chairs and tables heating up, catching fire, spreading it and burning out.</summary>
    public sealed class FlammablesEditModeTests
    {
        private static readonly SimulationId BoxId = new SimulationId(3001UL);

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
        /// A fire that starts at once in the square from (0, 0) to (0.5, 0.5) m
        /// and barely spreads, one box 0.5 m east of it, no tables, and one
        /// person far away (or the people given).
        /// </summary>
        private ScenarioData BoxBesideTheFire(params AgentDefinition[] people)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = people.Length > 0
                ? people
                : new[] { new AgentDefinition(new SimulationId(1UL), new LogicalPosition(5000, -5000), CardinalDirection.North) };
            data.Tables = new TableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(BoxId, PhysicsObjectKind.Box, new LogicalPosition(1000, 250), 400, 6000)
            };
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(250, 250, 250, 250);
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            return data;
        }

        [Test]
        public void BoxNearFire_HeatsUpCatchesLightsItsSquareAndBurnsOut()
        {
            ScenarioData data = BoxBesideTheFire();
            var simulation = new Run(data);
            int heatSeen = 0;
            while (simulation.GetPhysicsObject(0).BurnState == ObjectBurnState.Intact && simulation.Tick < 500)
            {
                simulation.Step();
                heatSeen = System.Math.Max(heatSeen, simulation.GetPhysicsObject(0).HeatPercent);
            }

            List<CausalEvent> caught = EventsOfType(simulation, CausalEventType.ObjectCaughtFire);
            Assert.That(caught, Has.Count.EqualTo(1), "The box never caught fire.");
            Assert.That(caught[0].SourceId, Is.EqualTo(BoxId));
            Assert.That(caught[0].CausalParentEventId, Is.EqualTo(simulation.FireActivationEventId));

            // Heat builds from the very tick the fire lights (tick 1).
            Assert.That(caught[0].Tick, Is.EqualTo(data.Flammables.BoxIgniteTicks), "It takes the set time to heat up.");
            Assert.That(heatSeen, Is.EqualTo(100));

            for (int t = 0; t < data.Flammables.BoxBurnMaximumTicks + 10; t++)
            {
                simulation.Step();
            }

            // Resting in one square, it set that square alight after a moment.
            List<CausalEvent> spread = EventsOfType(simulation, CausalEventType.FireSpread);
            Assert.That(spread, Has.Count.EqualTo(1));
            Assert.That(spread[0].CausalParentEventId, Is.EqualTo(caught[0].EventId));
            Assert.That(spread[0].Tick, Is.EqualTo(caught[0].Tick + data.Flammables.FloorIgniteRestTicks - 1));

            List<CausalEvent> burntOut = EventsOfType(simulation, CausalEventType.ObjectBurntOut);
            Assert.That(burntOut, Has.Count.EqualTo(1));
            Assert.That(burntOut[0].CausalParentEventId, Is.EqualTo(caught[0].EventId));
            Assert.That(burntOut[0].Tick - caught[0].Tick, Is.EqualTo(caught[0].DurationTicks));
            Assert.That(simulation.GetPhysicsObject(0).BurnState, Is.EqualTo(ObjectBurnState.Burnt));
        }

        [Test]
        public void KickedBurningBox_LightsTheFloorOnlyWhereItStops()
        {
            ScenarioData data = BoxBesideTheFire();
            var simulation = new Run(data);
            while (simulation.GetPhysicsObject(0).BurnState == ObjectBurnState.Intact)
            {
                simulation.Step();
            }

            // Sent sliding west, far across the room, before it can light its first square.
            simulation.LaunchObjectForTests(0, -115, -30);
            for (int t = 0; t < 250; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> spread = EventsOfType(simulation, CausalEventType.FireSpread);
            Assert.That(spread, Has.Count.EqualTo(1), "Only the square where the box came to rest should burn.");
            LogicalPosition stoppedAt = simulation.GetPhysicsObject(0).Position;
            Assert.That(simulation.GetPhysicsObject(0).SpeedMillimetresPerTick, Is.EqualTo(0));
            Assert.That(LogicalPosition.DistanceSquared(spread[0].Position, stoppedAt), Is.LessThan(500L * 500L));
            Assert.That(stoppedAt.X, Is.LessThan(-1000), "The box should have slid well away from the fire.");
        }

        [Test]
        public void PersonTouchingABurningBox_CatchesFire()
        {
            // Stands right against the box's north side, frozen with fear so they stay there.
            ScenarioData data = BoxBesideTheFire(new AgentDefinition(new SimulationId(1UL),
                new LogicalPosition(1000, 730), CardinalDirection.West, AgentTraitValues.AllOrdinary));
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new Run(data);
            for (int t = 0; t < 200 && !simulation.GetAgent(0).IsBurning; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).IsBurning, Is.True, "Touching the burning box never set them alight.");
            List<CausalEvent> caught = EventsOfType(simulation, CausalEventType.AgentCaughtFire);
            Assert.That(simulation.EventLog.Get(caught[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.ObjectCaughtFire));
        }

        [Test]
        public void TableNextToFire_CatchesFire()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(5000, -5000), CardinalDirection.North)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-1650, -1650, -1500, -1500);
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            var simulation = new Run(data);
            for (int t = 0; t < data.Flammables.TableIgniteTicks + 5; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetTable(0).BurnState, Is.EqualTo(ObjectBurnState.Burning));
            Assert.That(simulation.GetTable(1).BurnState, Is.EqualTo(ObjectBurnState.Intact), "Far tables stay cool.");
            List<CausalEvent> caught = EventsOfType(simulation, CausalEventType.ObjectCaughtFire);
            Assert.That(caught[0].SourceId, Is.EqualTo(new SimulationId(4001UL)));
        }

        [Test]
        public void DefaultRoom_FurnitureAndBoxesBurnInMostRuns()
        {
            int caught = 0;
            int squaresLitByThings = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                var simulation = new Run(scenario.ToRuntimeData(), seed);
                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (record.EventType == CausalEventType.ObjectCaughtFire)
                    {
                        caught++;
                    }
                    else if (record.EventType == CausalEventType.FireSpread &&
                             simulation.EventLog.Get(record.CausalParentEventId).EventType == CausalEventType.ObjectCaughtFire)
                    {
                        squaresLitByThings++;
                    }
                }
            }

            Assert.That(caught, Is.GreaterThan(7));
            TestContext.WriteLine($"Seeds 40-46, 60 s: {caught} things caught fire; they lit {squaresLitByThings} floor squares.");
        }
    }
}

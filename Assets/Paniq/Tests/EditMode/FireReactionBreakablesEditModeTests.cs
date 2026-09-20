using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Furniture that breaks, and electrical things that go off. A hard enough
    /// blow leaves a chair as wreckage nobody can sit on, and collapses a table
    /// so people run straight across ground they had been walking around all
    /// game. A microwave the flames reach goes off with a bang: it flings loose
    /// things away from it, knocks people off their feet, and scatters fresh
    /// fire on the floor.
    /// </summary>
    public sealed class FireReactionBreakablesEditModeTests
    {
        private static readonly SimulationId TheChair = new SimulationId(4001UL);
        private static readonly SimulationId TheBox = new SimulationId(4002UL);
        private static readonly SimulationId TheTable = new SimulationId(4100UL);
        private static readonly SimulationId TheMicrowave = new SimulationId(4200UL);
        private static readonly SimulationId Bystander = new SimulationId(1UL);

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

        private static int IndexOf(FireReactionSimulation simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).ObjectId == id)
                {
                    return i;
                }
            }

            throw new KeyNotFoundException(id.ToString());
        }

        /// <summary>An empty room with nobody in it but one bystander well out of the way.</summary>
        private FireReactionScenarioData BareRoom()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Bystander, new LogicalPosition(-5000, -5000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        // ------------------------------------------------------------ breaking

        [Test]
        public void AHardEnoughHit_SmashesAChairIntoWreckage()
        {
            FireReactionScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheChair, PhysicsObjectKind.Chair, new LogicalPosition(2000, 0), 450, 5000),
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-1000, 0), 500, 20000)
            };
            var simulation = new FireReactionSimulation(data);
            int box = IndexOf(simulation, TheBox);
            int chair = IndexOf(simulation, TheChair);
            Assert.That(simulation.GetPhysicsObject(chair).Wrecked, Is.False);

            // A heavy box hurled east, straight into the chair.
            simulation.LaunchObjectForTests(box, 110, 0);
            for (int t = 0; t < 4 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(chair).Wrecked, Is.True, "The chair should have been smashed.");
            List<CausalEvent> broke = EventsOfType(simulation, FireReactionEventType.ObjectBroke);
            Assert.That(broke, Is.Not.Empty);
            Assert.That(broke[0].SourceId, Is.EqualTo(TheChair), "The event names what broke.");
            Assert.That(broke[0].TargetId, Is.EqualTo(TheBox), "And what hit it.");
        }

        [Test]
        public void AGentleNudge_LeavesAChairInOnePiece()
        {
            FireReactionScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheChair, PhysicsObjectKind.Chair, new LogicalPosition(2000, 0), 450, 5000),
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(1000, 0), 500, 3000)
            };
            var simulation = new FireReactionSimulation(data);
            simulation.LaunchObjectForTests(IndexOf(simulation, TheBox), 12, 0);
            for (int t = 0; t < 4 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(IndexOf(simulation, TheChair)).Wrecked, Is.False,
                "A light knock should not smash anything.");
        }

        [Test]
        public void ASmashedChair_CannotBeSatOn()
        {
            FireReactionScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheChair, PhysicsObjectKind.Chair, new LogicalPosition(2000, 0), 450, 5000),
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-1000, 0), 500, 20000)
            };

            // One person who would sit down at the first opportunity.
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Bystander, new LogicalPosition(2000, 2000), CardinalDirection.South,
                    AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 25;
            data.Calm.DecisionMaximumTicks = 25;
            data.Items.SitChancePercent = 100;
            var simulation = new FireReactionSimulation(data);
            simulation.LaunchObjectForTests(IndexOf(simulation, TheBox), 110, 0);
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(IndexOf(simulation, TheChair)).Wrecked, Is.True);
            Assert.That(simulation.GetAgent(0).ActivityState, Is.Not.EqualTo(AgentActivityState.Sitting),
                "Nobody sits on wreckage.");
        }

        [Test]
        public void ASmashedTable_StopsBeingSomethingToWalkAround()
        {
            FireReactionScenarioData data = BareRoom();
            data.Tables = new[]
            {
                new FireReactionTableDefinition(TheTable, new LogicalPosition(2000, 0), 1200, 700)
            };
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-1000, 0), 500, 20000)
            };
            var simulation = new FireReactionSimulation(data);

            // Before: a straight walk east from the west wall runs into the table.
            Assert.That(simulation.RouteCrossesTableForTests(new LogicalPosition(-4000, 0), new LogicalPosition(5000, 0)),
                Is.True, "The table should be in the way to begin with.");

            simulation.LaunchObjectForTests(IndexOf(simulation, TheBox), 110, 0);
            for (int t = 0; t < 4 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.ObjectBroke), Is.Not.Empty,
                "The table should have collapsed.");
            Assert.That(simulation.RouteCrossesTableForTests(new LogicalPosition(-4000, 0), new LogicalPosition(5000, 0)),
                Is.False, "Once it has collapsed, people can walk straight across where it stood.");
        }

        // ------------------------------------------------------------ popping

        /// <summary>A microwave with a fire started right beside it, and a person just within the blast.</summary>
        private FireReactionScenarioData MicrowaveBesideAFire()
        {
            FireReactionScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheMicrowave, PhysicsObjectKind.Microwave, new LogicalPosition(0, 0), 450, 14000),
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(1400, 0), 400, 4000)
            };
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Bystander, new LogicalPosition(0, 1300), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-600, -600, 0, 0);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;

            // The bystander roots to the spot, so they are still standing beside
            // the microwave when it goes off rather than halfway to a door.
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;
            return data;
        }

        [Test]
        public void AMicrowaveTheFlamesReach_GoesOff()
        {
            var simulation = new FireReactionSimulation(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> bang = EventsOfType(simulation, FireReactionEventType.ObjectExploded);
            Assert.That(bang, Is.Not.Empty, "The microwave never went off.");
            Assert.That(bang[0].SourceId, Is.EqualTo(TheMicrowave));
            Assert.That(simulation.EventLog.Get(bang[0].CausalParentEventId).EventType,
                Is.EqualTo(FireReactionEventType.ObjectCaughtFire), "It goes off because it caught fire.");
        }

        [Test]
        public void AnExplosion_FlingsWhateverIsLooseNearby()
        {
            var simulation = new FireReactionSimulation(MicrowaveBesideAFire());
            int box = IndexOf(simulation, TheBox);
            LogicalPosition before = simulation.GetPhysicsObject(box).Position;
            for (int t = 0; t < 15 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            for (int t = 0; t < FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            LogicalPosition after = simulation.GetPhysicsObject(box).Position;
            Assert.That(after.X, Is.GreaterThan(before.X), "The blast should have sent the box away from it.");
        }

        [Test]
        public void AnExplosion_KnocksPeopleNearItOffTheirFeet()
        {
            var simulation = new FireReactionSimulation(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> down = EventsOfType(simulation, FireReactionEventType.AgentKnockedDown);
            Assert.That(down, Is.Not.Empty, "Somebody standing a metre away should be knocked over.");
            Assert.That(simulation.EventLog.Get(down[0].CausalParentEventId).EventType,
                Is.EqualTo(FireReactionEventType.ObjectExploded));
        }

        [Test]
        public void AnExplosion_ScattersFreshFireAround()
        {
            var simulation = new FireReactionSimulation(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            int lit = 0;
            foreach (CausalEvent spread in EventsOfType(simulation, FireReactionEventType.FireSpread))
            {
                lit += simulation.EventLog.Get(spread.CausalParentEventId).EventType == FireReactionEventType.ObjectExploded
                    ? 1
                    : 0;
            }

            Assert.That(lit, Is.GreaterThan(0), "The blast should have set some floor alight.");
        }

        [Test]
        public void TheDefaultOffice_HasElectricalThingsInIt()
        {
            var simulation = new FireReactionSimulation(scenario.ToRuntimeData());
            int electrical = 0;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectKind kind = simulation.GetPhysicsObject(i).Kind;
                electrical += kind == PhysicsObjectKind.Microwave || kind == PhysicsObjectKind.WallSocket ? 1 : 0;
            }

            Assert.That(electrical, Is.GreaterThan(0), "The office should have something electrical to go off.");
        }
    }
}

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
    public sealed class BreakablesEditModeTests
    {
        private static readonly SimulationId TheChair = new SimulationId(4001UL);
        private static readonly SimulationId TheBox = new SimulationId(4002UL);
        private static readonly SimulationId TheTable = new SimulationId(4100UL);
        private static readonly SimulationId TheMicrowave = new SimulationId(4200UL);
        private static readonly SimulationId TheLaptop = new SimulationId(4300UL);
        private static readonly SimulationId Bystander = new SimulationId(1UL);

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

        private static int IndexOf(Run simulation, SimulationId id)
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
        private ScenarioData BareRoom()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(Bystander, new LogicalPosition(-5000, -5000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        // ------------------------------------------------------------ breaking

        /// <summary>
        /// Furniture is knocked about, not destroyed. A chair hit as hard as
        /// anything in this building can hit one goes flying, tips and rolls,
        /// and is still a chair when it stops.
        /// </summary>
        [Test]
        public void AHardEnoughHit_SendsAChairFlyingWithoutBreakingIt()
        {
            ScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheChair, PhysicsObjectKind.Chair, new LogicalPosition(2000, 0), 450, 5000),
                new PhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-1000, 0), 500, 20000)
            };
            var simulation = new Run(data);
            int box = IndexOf(simulation, TheBox);
            int chair = IndexOf(simulation, TheChair);
            LogicalPosition before = simulation.GetPhysicsObject(chair).Position;

            // A heavy box hurled east, straight into the chair.
            simulation.LaunchObjectForTests(box, 110, 0);
            for (int t = 0; t < 4 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            PhysicsObjectSnapshot hit = simulation.GetPhysicsObject(chair);
            Assert.That(hit.Wrecked, Is.False, "A chair is shoved about, never smashed.");
            Assert.That(EventsOfType(simulation, CausalEventType.ObjectBroke), Is.Empty,
                "Nothing in this room is breakable any more.");
            Assert.That(hit.Position.X, Is.GreaterThan(before.X + 200),
                "It should have been driven well across the floor.");
        }

        [Test]
        public void AGentleNudge_LeavesAChairInOnePiece()
        {
            ScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheChair, PhysicsObjectKind.Chair, new LogicalPosition(2000, 0), 450, 5000),
                new PhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(1000, 0), 500, 3000)
            };
            var simulation = new Run(data);
            simulation.LaunchObjectForTests(IndexOf(simulation, TheBox), 12, 0);
            for (int t = 0; t < 4 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(IndexOf(simulation, TheChair)).Wrecked, Is.False,
                "A light knock should not smash anything.");
        }

        /// <summary>
        /// A table takes the same blow and is shoved across the floor. It is
        /// still solid where it ends up, so people still walk round it -- they
        /// just have to walk round it somewhere else.
        /// </summary>
        [Test]
        public void ATableHitHard_IsShovedAcrossTheFloorRatherThanSmashed()
        {
            ScenarioData data = BareRoom();
            data.Tables = new[]
            {
                new TableDefinition(TheTable, new LogicalPosition(2000, 0), 1200, 700)
            };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-1000, 0), 500, 20000)
            };
            var simulation = new Run(data);
            LogicalPosition before = simulation.GetTable(0).Bounds.Centre;

            simulation.LaunchObjectForTests(IndexOf(simulation, TheBox), 110, 0);
            for (int t = 0; t < 4 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.ObjectBroke), Is.Empty,
                "A table is furniture being knocked about, not something that shatters.");
            Assert.That(simulation.GetTable(0).Bounds.Centre.X, Is.Not.EqualTo(before.X),
                "The blow should have moved it.");
        }

        // ------------------------------------------------------------ popping

        /// <summary>A microwave with a fire started right beside it, and a person just within the blast.</summary>
        private ScenarioData MicrowaveBesideAFire()
        {
            ScenarioData data = BareRoom();
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheMicrowave, PhysicsObjectKind.Microwave, new LogicalPosition(0, 0), 450, 14000),
                new PhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(1400, 0), 400, 4000)
            };
            data.Agents = new[]
            {
                new AgentDefinition(Bystander, new LogicalPosition(0, 1300), CardinalDirection.North,
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

        /// <summary>
        /// A laptop battery going off: a smaller bang than a microwave, but a
        /// bang, and the thing the owner watches for when a desk catches.
        /// </summary>
        [Test]
        public void ALaptopTheFlamesReach_GoesOff()
        {
            ScenarioData data = MicrowaveBesideAFire();
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheLaptop, PhysicsObjectKind.Laptop, new LogicalPosition(0, 0), 300, 1500)
            };

            var simulation = new Run(data);
            for (int t = 0; t < 30 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> bang = EventsOfType(simulation, CausalEventType.ObjectExploded);
            Assert.That(bang, Is.Not.Empty, "The laptop never went off.");
            Assert.That(bang[0].SourceId, Is.EqualTo(TheLaptop));
            Assert.That(bang[0].Strength, Is.GreaterThan(0), "The bang carries how big it is, which the display draws.");
        }

        [Test]
        public void AMicrowaveTheFlamesReach_GoesOff()
        {
            var simulation = new Run(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> bang = EventsOfType(simulation, CausalEventType.ObjectExploded);
            Assert.That(bang, Is.Not.Empty, "The microwave never went off.");
            Assert.That(bang[0].SourceId, Is.EqualTo(TheMicrowave));
            Assert.That(simulation.EventLog.Get(bang[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.ObjectCaughtFire), "It goes off because it caught fire.");
        }

        [Test]
        public void AnExplosion_FlingsWhateverIsLooseNearby()
        {
            var simulation = new Run(MicrowaveBesideAFire());
            int box = IndexOf(simulation, TheBox);
            LogicalPosition before = simulation.GetPhysicsObject(box).Position;
            for (int t = 0; t < 15 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            for (int t = 0; t < Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            LogicalPosition after = simulation.GetPhysicsObject(box).Position;
            Assert.That(after.X, Is.GreaterThan(before.X), "The blast should have sent the box away from it.");
        }

        [Test]
        public void AnExplosion_KnocksPeopleNearItOffTheirFeet()
        {
            var simulation = new Run(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> down = EventsOfType(simulation, CausalEventType.AgentKnockedDown);
            Assert.That(down, Is.Not.Empty, "Somebody standing a metre away should be knocked over.");
            Assert.That(simulation.EventLog.Get(down[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.ObjectExploded));
        }

        [Test]
        public void AnExplosion_ScattersFreshFireAround()
        {
            var simulation = new Run(MicrowaveBesideAFire());
            for (int t = 0; t < 15 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ObjectExploded).Count == 0; t++)
            {
                simulation.Step();
            }

            int lit = 0;
            foreach (CausalEvent spread in EventsOfType(simulation, CausalEventType.FireSpread))
            {
                lit += simulation.EventLog.Get(spread.CausalParentEventId).EventType == CausalEventType.ObjectExploded
                    ? 1
                    : 0;
            }

            Assert.That(lit, Is.GreaterThan(0), "The blast should have set some floor alight.");
        }

        [Test]
        public void TheDefaultOffice_HasElectricalThingsInIt()
        {
            var simulation = new Run(scenario.ToRuntimeData());
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

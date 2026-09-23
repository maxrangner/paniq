using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The office's loose things: each kind slides its own distance when
    /// kicked, and a potted plant never catches fire however long it stands
    /// in the flames.
    /// </summary>
    public sealed class OfficeItemsEditModeTests
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

        /// <summary>One object of the given kind alone in the office, with nobody about.</summary>
        private ScenarioData OneThing(PhysicsObjectKind kind, int sizeMillimetres, int massGrams)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-5000, -5000), CardinalDirection.North)
            };
            data.Tables = new TableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3001UL), kind, new LogicalPosition(-4000, 0),
                    sizeMillimetres, massGrams)
            };
            data.Fire.ActivationTick = int.MaxValue;
            return data;
        }

        /// <summary>How far one kick sends a thing, in millimetres.</summary>
        private int SlideDistance(PhysicsObjectKind kind)
        {
            ScenarioData data = OneThing(kind, 400, 5000);
            var simulation = new Run(data);
            LogicalPosition start = simulation.GetPhysicsObject(0).Position;
            simulation.LaunchObjectForTests(0, 80, 0);
            for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            return simulation.GetPhysicsObject(0).Position.X - start.X;
        }

        [Test]
        public void EachKindOfThing_SlidesItsOwnDistance()
        {
            int box = SlideDistance(PhysicsObjectKind.Box);
            int officeChair = SlideDistance(PhysicsObjectKind.OfficeChair);
            int plant = SlideDistance(PhysicsObjectKind.PottedPlant);
            int laptop = SlideDistance(PhysicsObjectKind.Laptop);

            Assert.That(box, Is.GreaterThan(0), "A kicked box should slide.");
            Assert.That(officeChair, Is.GreaterThan(box * 2), "Castors: an office chair rolls far.");
            Assert.That(laptop, Is.GreaterThan(box), "A laptop skitters across the floor.");
            Assert.That(plant, Is.LessThan(box), "A heavy pot of earth barely shifts.");
        }

        [Test]
        public void APottedPlant_NeverCatchesFireHoweverLongItStandsInTheFlames()
        {
            ScenarioData data = OneThing(PhysicsObjectKind.PottedPlant, 450, 25000);
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-4000, -4000, 0, 0);
            var simulation = new Run(data);
            for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            PhysicsObjectSnapshot plant = simulation.GetPhysicsObject(0);
            Assert.That(simulation.FireCellCount, Is.GreaterThan(1), "The fire should have spread around it.");
            Assert.That(plant.BurnState, Is.EqualTo(ObjectBurnState.Intact), "A potted plant never burns.");
            Assert.That(plant.HeatPercent, Is.EqualTo(0), "It never even heats up.");
        }

        [Test]
        public void AWasteBin_CatchesSoonerThanAChair()
        {
            Assert.That(IgnitionTick(PhysicsObjectKind.WasteBin), Is.LessThan(IgnitionTick(PhysicsObjectKind.Chair)));
        }

        /// <summary>The tick a thing sitting in the fire bursts into flames, or int.MaxValue.</summary>
        private int IgnitionTick(PhysicsObjectKind kind)
        {
            ScenarioData data = OneThing(kind, 400, 5000);
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-4000, -4000, 0, 0);
            var simulation = new Run(data);
            for (int t = 0; t < 30 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
                if (simulation.GetPhysicsObject(0).BurnState == ObjectBurnState.Burning)
                {
                    return simulation.Tick;
                }
            }

            return int.MaxValue;
        }
    }
}

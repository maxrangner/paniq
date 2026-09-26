using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The rest of the office (2026-09-24): a vending machine, filing
    /// cabinets, shelves, a copier on castors, a whiteboard on wheels, a
    /// standing lamp that pops and sheds its shade when it goes over, and a
    /// robot vacuum that trundles about by itself and burns like plastic.
    /// Everything but the last two is knocked about by the physics like any
    /// other thing; the lamp and the vacuum are the two rows in the table of
    /// kinds that do something of their own.
    /// </summary>
    public sealed class NewPropsEditModeTests
    {
        private static readonly SimulationId TheThing = new SimulationId(3001UL);
        private static readonly SimulationId TheShade = new SimulationId(3002UL);

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

        /// <summary>One thing of the given kind alone in the office, with nobody about and no fire due.</summary>
        private ScenarioData OneThing(PhysicsObjectKind kind, int sizeMillimetres, int massGrams,
            params PhysicsObjectDefinition[] more)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-5000, -5000), CardinalDirection.North)
            };
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            var things = new List<PhysicsObjectDefinition>
            {
                new PhysicsObjectDefinition(TheThing, kind, new LogicalPosition(-4000, 0), sizeMillimetres, massGrams)
            };
            things.AddRange(more);
            data.PhysicsObjects = things.ToArray();
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        /// <summary>How upright a thing stands: 1 on its feet, 0 on its side, -1 upside down.</summary>
        private static float Uprightness(PhysicsObjectSnapshot thing)
        {
            if (!thing.Pose.IsKnown)
            {
                return 1f;
            }

            var turn = new Quaternion(
                thing.Pose.RotationX / (float)BodyPose.RotationScale, thing.Pose.RotationY / (float)BodyPose.RotationScale,
                thing.Pose.RotationZ / (float)BodyPose.RotationScale, thing.Pose.RotationW / (float)BodyPose.RotationScale);
            return (turn * Vector3.up).y;
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

        // ---------------------------------------------------------------- every kind

        /// <summary>
        /// A new kind of thing is a row in the table, a solid shape, a top to
        /// stack on and a name in the story. This is what catches a kind added
        /// to the enum and forgotten anywhere else.
        /// </summary>
        [Test]
        public void EveryKindOfThing_HasARowAShapeATopAndAName()
        {
            ObjectKindSettings[] rows = ObjectKindSettings.Defaults();
            Assert.That(rows, Has.Length.EqualTo(ObjectKindSettings.KindCount));
            foreach (PhysicsObjectKind kind in Enum.GetValues(typeof(PhysicsObjectKind)))
            {
                Assert.That((int)kind, Is.LessThan(rows.Length), $"{kind} has no row in the table of kinds.");
                Assert.That(rows[(int)kind].Kind, Is.EqualTo(kind), "Rows are in enum order.");
                Assert.That(ObjectShapes.For(kind, 400), Is.Not.Empty, $"{kind} has no solid shape.");
                Assert.That(ObjectShapes.TopHeight(kind, 400), Is.GreaterThan(0), $"{kind} has no top.");
                Assert.That(ObjectShapes.FootprintHalfWidth(kind, 400), Is.GreaterThan(0), $"{kind} stands on nothing.");

                // Thrown, it is written down by name: "a whiteboard flung ..."
                // rather than "something flung ...".
                ScenarioData data = OneThing(kind, 400, 5000);
                using (var simulation = new Run(data))
                {
                    simulation.LaunchObjectForTests(0, 20, 0);
                    simulation.Step();
                    var story = new EventStory(simulation.GetSnapshot());
                    List<CausalEvent> thrown = EventsOfType(simulation, CausalEventType.ItemThrown);
                    Assert.That(thrown, Is.Not.Empty);
                    Assert.That(story.Describe(thrown[0]), Does.Not.Contain("something"), $"{kind} has no name in the story.");
                }
            }
        }

        /// <summary>The shipped building, with everything new in it, loads and runs.</summary>
        [Test]
        public void TheShippedOffice_HasTheNewThingsInItAndStillLoads()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var counts = new Dictionary<PhysicsObjectKind, int>();
            foreach (PhysicsObjectDefinition thing in data.PhysicsObjects)
            {
                counts[thing.Kind] = counts.TryGetValue(thing.Kind, out int n) ? n + 1 : 1;
            }

            Assert.That(counts[PhysicsObjectKind.VendingMachine], Is.EqualTo(1));
            Assert.That(counts[PhysicsObjectKind.Cabinet], Is.EqualTo(3));
            Assert.That(counts[PhysicsObjectKind.Shelves], Is.EqualTo(4));
            Assert.That(counts[PhysicsObjectKind.CopyMachine], Is.EqualTo(2));
            Assert.That(counts[PhysicsObjectKind.Whiteboard], Is.EqualTo(2));
            Assert.That(counts[PhysicsObjectKind.StandingLamp], Is.EqualTo(2));
            Assert.That(counts[PhysicsObjectKind.LampShade], Is.EqualTo(2));
            Assert.That(counts[PhysicsObjectKind.RobotVacuum], Is.EqualTo(2));
            Assert.That(counts[PhysicsObjectKind.Box], Is.EqualTo(48), "Eight in the office, thirty-two in the stockroom and eight in the tower at the junction.");
            Assert.That(counts[PhysicsObjectKind.AlarmSounder], Is.EqualTo(7), "A bell in every room people use.");
            using (var simulation = new Run(data))
            {
                for (int t = 0; t < 5 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }
            }
        }

        // ---------------------------------------------------------------- knocked about

        /// <summary>How far one kick sends a thing along the floor, in millimetres.</summary>
        private int SlideDistance(PhysicsObjectKind kind, int size, int mass)
        {
            var simulation = new Run(OneThing(kind, size, mass));
            LogicalPosition start = simulation.GetPhysicsObject(0).Position;
            simulation.LaunchObjectForTests(0, 60, 0);
            for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            return simulation.GetPhysicsObject(0).Position.X - start.X;
        }

        /// <summary>The copier is on castors: the same shove sends it much further than a cabinet.</summary>
        [Test]
        public void TheCopier_RollsWhereACabinetStops()
        {
            int copier = SlideDistance(PhysicsObjectKind.CopyMachine, 800, 100000);
            int cabinet = SlideDistance(PhysicsObjectKind.Cabinet, 500, 60000);
            Assert.That(copier, Is.GreaterThan(cabinet * 2), $"Copier {copier} mm, cabinet {cabinet} mm.");
        }

        /// <summary>
        /// A blast beside it: the whiteboard, tall and light on its wheels,
        /// goes over; the vending machine, at 160 kg, stays on its feet.
        /// </summary>
        [TestCase(PhysicsObjectKind.Whiteboard, 1000, 15000, false)]
        [TestCase(PhysicsObjectKind.Shelves, 900, 45000, false)]
        [TestCase(PhysicsObjectKind.VendingMachine, 700, 160000, true)]
        public void ABlastBesideIt_TipsTheTallLightThingsAndNotTheHeavyOne(PhysicsObjectKind kind, int size, int mass,
            bool staysUp)
        {
            ScenarioData data = OneThing(kind, size, mass);
            data.Purse.Starting = 1000;
            data.Purse.Maximum = 1000;
            using (var simulation = new Run(data))
            {
                simulation.Step();
                // A hole blown in the west wall, two metres from the thing.
                simulation.QueueCommand(PlayerCommandType.BlastWall, new LogicalPosition(-5900, 0), simulation.Tick + 1);
                float lowest = 1f;
                for (int t = 0; t < 4 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    lowest = Math.Min(lowest, Uprightness(simulation.GetPhysicsObject(0)));
                }

                if (staysUp)
                {
                    Assert.That(lowest, Is.GreaterThan(0.7f), $"A {kind} should stand through a blast; it leaned to {lowest}.");
                }
                else
                {
                    Assert.That(lowest, Is.LessThan(0.5f), $"A {kind} should go over in a blast; it only leaned to {lowest}.");
                }
            }
        }

        // ---------------------------------------------------------------- the lamp

        private ScenarioData ALampWithItsShade()
        {
            return OneThing(PhysicsObjectKind.StandingLamp, 300, 6000,
                new PhysicsObjectDefinition(TheShade, PhysicsObjectKind.LampShade, new LogicalPosition(-4000, 0), 350, 1000,
                    startsDormant: true, partOfObjectId: TheThing));
        }

        /// <summary>
        /// Knocked sideways, the lamp goes over, its bulb pops once, and its
        /// shade -- nowhere until then -- drops to the floor beside it.
        /// </summary>
        [Test]
        public void ALampKnockedOver_PopsOnceAndItsShadeComesOff()
        {
            using (var simulation = new Run(ALampWithItsShade()))
            {
                int lamp = IndexOf(simulation, TheThing);
                int shade = IndexOf(simulation, TheShade);
                simulation.Step();
                Assert.That(simulation.GetPhysicsObject(shade).Dormant, Is.True, "The shade is part of the lamp to begin with.");

                simulation.LaunchObjectForTests(lamp, 60, 0);
                for (int t = 0; t < 4 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(Uprightness(simulation.GetPhysicsObject(lamp)), Is.LessThan(0.5f), "The lamp should have gone over.");
                List<CausalEvent> popped = EventsOfType(simulation, CausalEventType.ObjectPopped);
                Assert.That(popped, Has.Count.EqualTo(1), "Going over pops the bulb, once.");
                Assert.That(popped[0].SourceId, Is.EqualTo(TheThing), "The pop names the lamp.");

                PhysicsObjectSnapshot loose = simulation.GetPhysicsObject(shade);
                Assert.That(loose.Dormant, Is.False, "The shade should have come off.");
                Assert.That(loose.Pose.HeightMillimetres, Is.LessThan(300), "And be down on the floor.");
                Assert.That(IntegerMath.Distance(loose.Position, simulation.GetPhysicsObject(lamp).Position), Is.LessThan(2500L),
                    "Beside the lamp, not across the room.");

                // Knocked again, it does not pop again: the bulb is gone.
                simulation.LaunchObjectForTests(lamp, 60, 0);
                for (int t = 0; t < 2 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOfType(simulation, CausalEventType.ObjectPopped), Has.Count.EqualTo(1));
            }
        }

        /// <summary>A shade authored as part of a lamp has to start dormant, and its lamp has to exist.</summary>
        [Test]
        public void AShadeThatIsNotDormant_OrWhoseLampIsMissing_IsRefused()
        {
            ScenarioData awake = OneThing(PhysicsObjectKind.StandingLamp, 300, 6000,
                new PhysicsObjectDefinition(TheShade, PhysicsObjectKind.LampShade, new LogicalPosition(-3000, 0), 350, 1000,
                    partOfObjectId: TheThing));
            Assert.Throws<InvalidOperationException>(() => awake.Validate());

            ScenarioData orphan = OneThing(PhysicsObjectKind.StandingLamp, 300, 6000,
                new PhysicsObjectDefinition(TheShade, PhysicsObjectKind.LampShade, new LogicalPosition(-4000, 0), 350, 1000,
                    startsDormant: true, partOfObjectId: new SimulationId(9999UL)));
            Assert.Throws<InvalidOperationException>(() => orphan.Validate());
        }

        // ---------------------------------------------------------------- the robot vacuum

        /// <summary>The office as shipped, desks and all, with one robot vacuum in the middle of it and nobody about.</summary>
        private ScenarioData ARoverInTheOffice()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-5000, -5000), CardinalDirection.North)
            };
            data.Alarms = new AlarmDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheThing, PhysicsObjectKind.RobotVacuum, new LogicalPosition(0, -3000), 330, 4000)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        /// <summary>How far the vacuum travelled, tick by tick, over this many ticks; and where it went.</summary>
        private static long PathLength(Run simulation, int ticks, Action<PhysicsObjectSnapshot> everyTick = null)
        {
            long path = 0L;
            LogicalPosition previous = simulation.GetPhysicsObject(0).Position;
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
                PhysicsObjectSnapshot rover = simulation.GetPhysicsObject(0);
                path += IntegerMath.Distance(previous, rover.Position);
                previous = rover.Position;
                everyTick?.Invoke(rover);
            }

            return path;
        }

        /// <summary>
        /// Left alone, the vacuum trundles about the office: metres of floor in
        /// ten seconds, never out of the room, never into a desk.
        /// </summary>
        [Test]
        public void TheRobotVacuum_TrundlesAboutTheOfficeOnItsOwn()
        {
            ScenarioData data = ARoverInTheOffice();
            LogicalBounds office = data.Rooms[0].Bounds;
            using (var simulation = new Run(data))
            {
                long path = PathLength(simulation, 10 * Run.TicksPerSecond, rover =>
                {
                    Assert.That(office.ContainsCircle(rover.Position, 0), Is.True,
                        $"Tick {simulation.Tick}: the vacuum left the office at {rover.Position}.");
                    foreach (TableDefinition desk in data.Tables)
                    {
                        Assert.That(desk.Bounds.DistanceSquaredTo(rover.Position), Is.GreaterThanOrEqualTo(100L * 100L),
                            $"Tick {simulation.Tick}: the vacuum drove into desk {desk.TableId}.");
                    }
                });

                Assert.That(path, Is.GreaterThan(3000L), $"Ten seconds of trundling should cover metres, not {path} mm.");
            }
        }

        /// <summary>Kicked into the air it stops driving, and once it has landed on its wheels it sets off again.</summary>
        [Test]
        public void TheRobotVacuum_StopsWhileTumblingAndDrivesAgainOnceDown()
        {
            using (var simulation = new Run(ARoverInTheOffice()))
            {
                PathLength(simulation, Run.TicksPerSecond);
                simulation.TossObjectUpForTests(0, 60);
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                PhysicsObjectSnapshot down = simulation.GetPhysicsObject(0);
                Assert.That(down.Pose.HeightMillimetres, Is.LessThan(100), "It should have landed.");
                Assume.That(Uprightness(down), Is.GreaterThan(0.7f), "Landed on its wheels this time; otherwise nothing to test.");

                long path = PathLength(simulation, 5 * Run.TicksPerSecond);
                Assert.That(path, Is.GreaterThan(1000L), $"Back on its wheels it should trundle off again, not {path} mm.");
            }
        }

        /// <summary>
        /// Driven into the flames it catches, keeps trundling while it burns --
        /// a burning thing on the move, heating whatever it passes -- and stops
        /// for good once it has burnt out.
        /// </summary>
        [Test]
        public void TheRobotVacuum_CatchesFireKeepsGoingAndStopsWhenBurntOut()
        {
            ScenarioData data = ARoverInTheOffice();

            // The flames are under it from the first tick and never spread,
            // and it catches at a touch, so the test is about what a burning
            // vacuum does rather than how long plastic takes to catch.
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -3000, -3000);
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;
            data.Flammables.Of(PhysicsObjectKind.RobotVacuum).IgniteTicks = 1;
            data.Flammables.Of(PhysicsObjectKind.RobotVacuum).BurnMinimumTicks = 150;
            data.Flammables.Of(PhysicsObjectKind.RobotVacuum).BurnMaximumTicks = 150;
            using (var simulation = new Run(data))
            {
                for (int t = 0; t < 3 * Run.TicksPerSecond && simulation.GetPhysicsObject(0).BurnState != ObjectBurnState.Burning; t++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.GetPhysicsObject(0).BurnState, Is.EqualTo(ObjectBurnState.Burning), "It never caught.");
                long whileBurning = PathLength(simulation, 100);
                Assert.That(whileBurning, Is.GreaterThan(500L), $"Alight, it should keep trundling, not {whileBurning} mm.");

                for (int t = 0; t < 6 * Run.TicksPerSecond && simulation.GetPhysicsObject(0).BurnState != ObjectBurnState.Burnt; t++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.GetPhysicsObject(0).BurnState, Is.EqualTo(ObjectBurnState.Burnt), "It never burnt out.");

                // Its battery goes at the end of the burn, not the moment it
                // catches: one bang, after it stopped burning.
                List<CausalEvent> bangs = EventsOfType(simulation, CausalEventType.ObjectExploded);
                Assert.That(bangs, Has.Count.EqualTo(1), "A burnt-out vacuum goes off once.");
                Assert.That(bangs[0].SourceId, Is.EqualTo(TheThing));
                Assert.That(bangs[0].Tick, Is.GreaterThan(EventsOfType(simulation, CausalEventType.ObjectCaughtFire)[0].Tick + 100),
                    "It rides about alight for a while first; the bang is not the catching.");

                PathLength(simulation, Run.TicksPerSecond);
                long afterwards = PathLength(simulation, 3 * Run.TicksPerSecond);
                Assert.That(afterwards, Is.LessThan(150L), $"Burnt out, it should stand still, not {afterwards} mm.");
            }
        }
    }
}

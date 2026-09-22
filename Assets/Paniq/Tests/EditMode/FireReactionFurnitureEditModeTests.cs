using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Tables that people walk around and objects bounce off, and chairs that get kicked about.</summary>
    public sealed class FireReactionFurnitureEditModeTests
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

        private FireReactionScenarioData DefaultData() => scenario.ToRuntimeData();

        /// <summary>How far one solid thing may press into another, in millimetres, as the physics engine settles contacts.</summary>
        private const int PhysicsTolerance = 15;

        /// <summary>
        /// Whether a round footprint this size overlaps the table: nearer to
        /// the table's rectangle than its radius. Measured to the rectangle
        /// itself, so somebody standing off a corner is not counted as in it.
        /// </summary>
        private static bool InsideTable(LogicalPosition position, int radius, LogicalBounds table)
        {
            long dx = Math.Max(0L, Math.Max((long)table.MinX - position.X, (long)position.X - table.MaxX));
            long dz = Math.Max(0L, Math.Max((long)table.MinZ - position.Z, (long)position.Z - table.MaxZ));
            return dx * dx + dz * dz < (long)radius * radius;
        }

        [Test]
        public void DefaultBuilding_HasTablesAndChairsInBothRooms()
        {
            FireReactionScenarioData data = DefaultData();
            Assert.That(data.Tables, Has.Length.EqualTo(4), "Three in the office, and the meeting room’s long table.");
            int chairs = 0;
            int officeChairs = 0;
            foreach (FireReactionPhysicsObjectDefinition item in data.PhysicsObjects)
            {
                chairs += item.Kind == PhysicsObjectKind.Chair ? 1 : 0;
                officeChairs += item.Kind == PhysicsObjectKind.OfficeChair ? 1 : 0;
            }

            Assert.That(chairs, Is.EqualTo(8), "Wooden chairs around the office tables.");
            Assert.That(officeChairs, Is.EqualTo(9), "Office chairs on castors: four down each side of the meeting table and one at its head.");
            Assert.That(data.Tables[0].Bounds.MaxX - data.Tables[0].Bounds.MinX, Is.EqualTo(1200));
        }

        [Test]
        public void PersonStartingInsideATable_IsRejected()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), data.Tables[0].Centre, CardinalDirection.North)
            };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }

        [Test]
        public void NobodyAndNothing_EverEndsUpInsideATable()
        {
            int chairBumps = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                var simulation = new FireReactionSimulation(data, seed);
                var presses = new PressWatch();
                simulation.QueueCommand(PlayerCommandType.ClickDoor, FireReactionDoorsEditModeTests.TheWayOut, 600);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, FireReactionDoorsEditModeTests.TheWayOut, 601);
                int radius = data.World.OccupancyRadiusMillimetres;
                for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
                {
                    simulation.Step();
                    FireReactionSnapshot snapshot = simulation.GetSnapshot();
                    foreach (FireReactionTableSnapshot table in snapshot.Tables)
                    {
                        if (table.Broken)
                        {
                            // Collapsed: it is wreckage, and the floor it stood
                            // on is walkable again.
                            continue;
                        }

                        foreach (FireReactionAgentSnapshot agent in snapshot.Agents)
                        {
                            // Flung up onto a table top is not inside it.
                            bool onTheFloor = agent.Pose.HeightMillimetres < 100;
                            Assert.That(agent.Participation == AgentParticipation.Participating && onTheFloor &&
                                        InsideTable(agent.Position, radius - PhysicsTolerance, table.Bounds), Is.False,
                                $"Seed {seed}: agent {agent.AgentId} inside table {table.TableId} at tick {snapshot.Tick}.");
                        }

                    }

                    // Loose things: nothing solid stays sunk into a table (or
                    // anything else) further than a squeeze goes.
                    presses.Check(simulation, $"Seed {seed}");
                }

                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (record.EventType == FireReactionEventType.BoxBumped && record.TargetId.Value >= 3101UL &&
                        record.TargetId.Value <= 3108UL)
                    {
                        chairBumps++;
                    }
                }
            }

            Assert.That(chairBumps, Is.GreaterThan(0), "Nobody ever kicked a chair.");
        }

        [Test]
        public void KickedChair_BouncesOffATable()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(5000, -5000), CardinalDirection.North)
            };
            data.Fire.ActivationTick = int.MaxValue;
            LogicalBounds table = data.Tables[0].Bounds;

            // One chair 1 m to the west of the first table's middle, sliding east into it.
            var start = new LogicalPosition(table.MinX - 1000, (table.MinZ + table.MaxZ) / 2);
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(new SimulationId(3101UL), PhysicsObjectKind.Chair, start, 450, 5000)
            };
            var simulation = new FireReactionSimulation(data);
            simulation.LaunchObjectForTests(0, 100, 0);
            int legs = ObjectShapes.FootprintHalfWidth(PhysicsObjectKind.Chair, 450);
            int closest = int.MinValue;
            for (int t = 0; t < 60; t++)
            {
                simulation.Step();
                FireReactionPhysicsObjectSnapshot chair = simulation.GetPhysicsObject(0);

                // The seat overhangs the legs, and it is the legs that reach
                // the floor: the chair can tip until they meet the table's
                // side, to within the few millimetres the engine allows.
                Assert.That(InsideTable(chair.Position, legs - PhysicsTolerance, table), Is.False,
                    $"The chair went into the table at tick {t}.");
                closest = Math.Max(closest, chair.Position.X);
            }

            Assert.That(closest, Is.InRange(table.MinX - 225 - 30, table.MinX - legs + PhysicsTolerance),
                "The chair should reach the table's edge.");
            Assert.That(simulation.GetPhysicsObject(0).Position.X, Is.LessThan(closest), "The chair should bounce back off the table.");
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>Tables that people walk around and objects bounce off, and chairs that get kicked about.</summary>
    public sealed class FurnitureEditModeTests
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

        /// <summary>
        /// The shipped floor with the fire in the office, and a purse behind
        /// the player. Tests here click the way out open to see people use it;
        /// a round opens with nothing to spend, so without this those clicks
        /// are refused and the tests quietly check a sealed building.
        /// </summary>
        private ScenarioData DefaultData() =>
            TheBuilding.WithThePlayerAbleToAct(TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData()));

        /// <summary>How far one solid thing may press into another, in millimetres, as the physics engine settles contacts.</summary>
        private const int PhysicsTolerance = 15;

        /// <summary>
        /// How far a person may press into a table, in millimetres. More than
        /// the engine's own slack: tables are bodies now, so somebody walking
        /// into one leans on it and shoves it along, and while they are pushing
        /// they are up against it. Still far less than standing in it.
        /// </summary>
        private const int LeaningOnATableTolerance = 40;

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

        /// <summary>
        /// The same question for a table that has been shoved round: tables are
        /// bodies now, so one turned even a little covers a rectangle at an
        /// angle, and the box drawn round it takes in floor it does not cover.
        /// The point is turned back into the table's own frame and measured
        /// against the rectangle it was authored as. A table tipped over is
        /// skipped: on its side it is no longer the thing this asks about.
        /// </summary>
        private static bool InsideTable(LogicalPosition position, int radius, TableSnapshot table,
            ScenarioData data)
        {
            if (!table.Pose.IsKnown)
            {
                return InsideTable(position, radius, table.Bounds);
            }

            var turn = new Quaternion(
                table.Pose.RotationX / (float)BodyPose.RotationScale, table.Pose.RotationY / (float)BodyPose.RotationScale,
                table.Pose.RotationZ / (float)BodyPose.RotationScale, table.Pose.RotationW / (float)BodyPose.RotationScale);
            if ((turn * Vector3.up).y < 0.7f)
            {
                // On its side or its back: not a table top any more.
                return false;
            }

            int width = 0;
            int depth = 0;
            foreach (TableDefinition authored in data.Tables)
            {
                if (authored.TableId == table.TableId)
                {
                    width = authored.WidthMillimetres;
                    depth = authored.DepthMillimetres;
                }
            }

            Vector3 local = Quaternion.Inverse(turn) * new Vector3(
                position.X - table.Pose.Origin.X, 0f, position.Z - table.Pose.Origin.Z);
            float dx = Math.Max(0f, Math.Abs(local.x) - width * 0.5f);
            float dz = Math.Max(0f, Math.Abs(local.z) - depth * 0.5f);
            return dx * dx + dz * dz < (float)radius * radius;
        }

        [Test]
        public void DefaultBuilding_HasTablesAndChairsInBothRooms()
        {
            ScenarioData data = DefaultData();
            Assert.That(data.Tables, Has.Length.EqualTo(6),
                "Three desks in the office, the meeting room's long table, and two in the cafeteria.");
            int chairs = 0;
            int officeChairs = 0;
            foreach (PhysicsObjectDefinition item in data.PhysicsObjects)
            {
                chairs += item.Kind == PhysicsObjectKind.Chair ? 1 : 0;
                officeChairs += item.Kind == PhysicsObjectKind.OfficeChair ? 1 : 0;
            }

            Assert.That(chairs, Is.EqualTo(8), "Wooden chairs around the office tables.");
            Assert.That(officeChairs, Is.EqualTo(8),
                "Office chairs on castors: six round the meeting table and two in the cafeteria.");
            Assert.That(data.Tables[0].Bounds.MaxX - data.Tables[0].Bounds.MinX, Is.EqualTo(1200));
        }

        [Test]
        public void PersonStartingInsideATable_IsRejected()
        {
            ScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), data.Tables[0].Centre, CardinalDirection.North)
            };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }

        [Test]
        public void NobodyAndNothing_EverEndsUpInsideATable()
        {
            int chairBumps = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                ScenarioData data = DefaultData();
                var simulation = new Run(data, seed);
                var presses = new PressWatch();
                simulation.QueueCommand(PlayerCommandType.ClickDoor, DoorsEditModeTests.TheWayOut, 600);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, DoorsEditModeTests.TheWayOut, 601);
                int radius = data.World.OccupancyRadiusMillimetres;
                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    RunSnapshot snapshot = simulation.GetSnapshot();
                    foreach (TableSnapshot table in snapshot.Tables)
                    {
                        foreach (AgentSnapshot agent in snapshot.Agents)
                        {
                            // Flung up onto a table top is not inside it.
                            bool onTheFloor = agent.Pose.HeightMillimetres < 100;
                            Assert.That(agent.Participation == AgentParticipation.Participating && onTheFloor &&
                                        InsideTable(agent.Position, radius - LeaningOnATableTolerance, table, data), Is.False,
                                $"Seed {seed}: agent {agent.AgentId} inside table {table.TableId} at tick {snapshot.Tick}.");
                        }

                    }

                    // Loose things: nothing solid stays sunk into a table (or
                    // anything else) further than a squeeze goes.
                    presses.Check(simulation, $"Seed {seed}");
                }

                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (record.EventType == CausalEventType.BoxBumped && record.TargetId.Value >= 3101UL &&
                        record.TargetId.Value <= 3108UL)
                    {
                        chairBumps++;
                    }
                }
            }

            Assert.That(chairBumps, Is.GreaterThan(0), "Nobody ever kicked a chair.");
        }

        /// <summary>How upright a table stands: 1 on its legs, 0 on its side, -1 on its back.</summary>
        private static float Uprightness(TableSnapshot table)
        {
            if (!table.Pose.IsKnown)
            {
                return 1f;
            }

            var turn = new Quaternion(
                table.Pose.RotationX / (float)BodyPose.RotationScale, table.Pose.RotationY / (float)BodyPose.RotationScale,
                table.Pose.RotationZ / (float)BodyPose.RotationScale, table.Pose.RotationW / (float)BodyPose.RotationScale);
            return (turn * Vector3.up).y;
        }

        /// <summary>The first table's index in the snapshot, by its ID.</summary>
        private static int IndexOfTable(Run simulation, SimulationId tableId)
        {
            RunSnapshot snapshot = simulation.GetSnapshot();
            for (int i = 0; i < snapshot.Tables.Count; i++)
            {
                if (snapshot.Tables[i].TableId == tableId)
                {
                    return i;
                }
            }

            throw new KeyNotFoundException(tableId.ToString());
        }

        /// <summary>One person standing at the west edge of a table, facing it, with nothing else going on.</summary>
        private ScenarioData OnePersonAtATable(SimulationId tableId, out int table)
        {
            ScenarioData data = DefaultData();
            data.Fire.ActivationTick = int.MaxValue;
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            TableDefinition authored = Array.Find(data.Tables, t => t.TableId == tableId);
            var beside = new LogicalPosition(authored.Centre.X - authored.WidthMillimetres / 2 - 300, authored.Centre.Z);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), beside, CardinalDirection.East, AgentTraitValues.AllOrdinary)
            };
            using (var probe = new Run(data))
            {
                table = IndexOfTable(probe, tableId);
            }

            return data;
        }

        /// <summary>
        /// A heave at a desk sends it over. The push lands on the top edge
        /// nearest the hands, so it tips away from whoever heaved it rather
        /// than sliding off on its legs the way a blast shoves it.
        /// </summary>
        [Test]
        public void AHeaveAtADesk_SendsItOver()
        {
            ScenarioData data = OnePersonAtATable(new SimulationId(4001UL), out int table);
            using (var simulation = new Run(data))
            {
                simulation.Step();
                Assert.That(Uprightness(simulation.GetSnapshot().Tables[table]), Is.GreaterThan(0.9f), "The desk starts on its legs.");
                simulation.HeaveTableForTests(0, table);
                float lowest = 1f;
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    lowest = Math.Min(lowest, Uprightness(simulation.GetSnapshot().Tables[table]));
                }

                Assert.That(lowest, Is.LessThan(0.7f), "A 21 kg desk heaved at its top edge should go over.");
                bool logged = false;
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    logged |= record.EventType == CausalEventType.TableHeaved && record.TargetId == new SimulationId(4001UL);
                }

                Assert.That(logged, Is.True, "Heaving a table is something that happened, so it names the table in the log.");
            }
        }

        /// <summary>
        /// The same heave at the 135 kg meeting table shifts it and no more:
        /// heavy tables get a smaller share of a heave, as they do of a blast,
        /// so the meeting room's table stays a table you can hide behind.
        /// </summary>
        [Test]
        public void AHeaveAtTheMeetingTable_ShiftsItWithoutTippingIt()
        {
            ScenarioData data = OnePersonAtATable(new SimulationId(4004UL), out int table);
            using (var simulation = new Run(data))
            {
                simulation.Step();
                LogicalBounds before = simulation.GetSnapshot().Tables[table].Bounds;
                simulation.HeaveTableForTests(0, table);
                float lowest = 1f;
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    lowest = Math.Min(lowest, Uprightness(simulation.GetSnapshot().Tables[table]));
                }

                LogicalBounds after = simulation.GetSnapshot().Tables[table].Bounds;
                Assert.That(lowest, Is.GreaterThan(0.9f), "The meeting table is far too heavy for one heave to tip.");
                Assert.That(after.MinX, Is.GreaterThan(before.MinX), "It should still have shifted along the floor.");
                Assert.That(after.MinX - before.MinX, Is.LessThan(500), "Shifted, not sent across the room.");
            }
        }

        /// <summary>
        /// Somebody running from the fire with a desk across their way, and no
        /// way round it, heaves it out of the way instead of grinding against
        /// it. Tables used to be kept off exactly like walls and nobody ever
        /// touched one, so no table was ever seen to move in a panic.
        /// </summary>
        [Test]
        public void SomebodyStuckBehindADesk_HeavesItOutOfTheWay()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var room = new SimulationId(5001UL);

            // A corridor barely wider than a desk, with the way out at the
            // north end, a desk across the middle and the fire at the south end.
            data.Rooms = new[] { new RoomDefinition(room, new LogicalBounds(-700, 700, -3000, 3000)) };
            data.Doors = new[] { new DoorDefinition(new SimulationId(2001UL), room, WallSide.North, 0, 800, startsLocked: false) };
            data.Tables = new[] { new TableDefinition(new SimulationId(4001UL), new LogicalPosition(0, 0), 1200, 700) };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.ExitSigns = new ExitSignDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.PowerLines = new PowerLineDefinition[0];
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, -1500), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = 1;
            // Near enough to frighten them, far enough that the moment they take
            // to turn and look does not put them in it.
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -2700, -2700);
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Round.HazardWaitsForTrigger = false;
            using (var simulation = new Run(data))
            {
                bool heaved = false;
                for (int t = 0; t < 10 * Run.TicksPerSecond && !heaved; t++)
                {
                    simulation.Step();
                    foreach (CausalEvent record in simulation.EventLog.Events)
                    {
                        heaved |= record.EventType == CausalEventType.TableHeaved;
                    }
                }

                Assert.That(heaved, Is.True, "Stuck behind a desk with the fire behind them, they should have heaved it.");
            }
        }

        [Test]
        public void KickedChair_ShovesATableRatherThanGoingThroughIt()
        {
            ScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(5000, -5000), CardinalDirection.North)
            };
            data.Fire.ActivationTick = int.MaxValue;
            LogicalBounds table = data.Tables[0].Bounds;

            // One chair 1 m to the west of the first table's middle, sliding east into it.
            var start = new LogicalPosition(table.MinX - 1000, (table.MinZ + table.MaxZ) / 2);
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3101UL), PhysicsObjectKind.Chair, start, 450, 5000)
            };
            var simulation = new Run(data);
            simulation.LaunchObjectForTests(0, 100, 0);
            int legs = ObjectShapes.FootprintHalfWidth(PhysicsObjectKind.Chair, 450);
            for (int t = 0; t < 60; t++)
            {
                simulation.Step();
                PhysicsObjectSnapshot chair = simulation.GetPhysicsObject(0);
                TableSnapshot hit = simulation.GetSnapshot().Tables[0];

                // The seat overhangs the legs, and it is the legs that reach
                // the floor: the chair can tip until they meet the table's
                // side, to within the few millimetres the engine allows. The
                // table itself gives, because it is a body like any other.
                Assert.That(InsideTable(chair.Position, legs - PhysicsTolerance, hit, data), Is.False,
                    $"The chair went into the table at tick {t}.");
            }

            Assert.That(simulation.GetSnapshot().Tables[0].Bounds.MinX, Is.GreaterThan(table.MinX),
                "A kicked chair should shove the table along, not bounce off a table nailed to the floor.");

        }
    }
}

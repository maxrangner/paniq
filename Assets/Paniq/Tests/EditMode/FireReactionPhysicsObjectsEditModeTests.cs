using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    public sealed class FireReactionPhysicsObjectsEditModeTests
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

        private static FireReactionPhysicsObjectDefinition Box(ulong id, int x, int z, int size, int massGrams)
        {
            return new FireReactionPhysicsObjectDefinition(
                new SimulationId(id), PhysicsObjectKind.Box, new LogicalPosition(x, z), size, massGrams);
        }

        /// <summary>One calm person standing at the origin, one box, and no fire.</summary>
        private FireReactionScenarioData PersonAndBox(int boxX, int size, int massGrams)
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0), CardinalDirection.North)
            };
            data.PhysicsObjects = new[] { Box(3001UL, boxX, 0, size, massGrams) };
            data.Fire.ActivationTick = int.MaxValue;
            return data;
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

        /// <summary>Whether something of this size stands wholly inside one of the building's rooms.</summary>
        private static bool InAnyRoom(FireReactionScenarioData data, LogicalPosition position, int radius)
        {
            foreach (FireReactionRoomDefinition room in data.Rooms)
            {
                if (room.Bounds.ContainsCircle(position, radius))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a thing just outside the building got there through a way out
        /// standing open (or battered down, or blasted): its middle is in that
        /// doorway's gap, within a metre of the wall line. Anything else outside
        /// went through a wall.
        /// </summary>
        private static bool ThroughAnOpenWayOut(FireReactionSnapshot snapshot, LogicalPosition position)
        {
            foreach (FireReactionDoorSnapshot door in snapshot.Doors)
            {
                if (!door.LeadsOutside ||
                    !(door.IsHole || door.State == DoorState.Open || door.State == DoorState.Broken))
                {
                    continue;
                }

                bool eastOrWest = door.Side == WallSide.East || door.Side == WallSide.West;
                long along = eastOrWest ? position.Z - door.Centre.Z : position.X - door.Centre.X;
                long across = eastOrWest ? position.X - door.Centre.X : position.Z - door.Centre.Z;
                if (Math.Abs(along) <= door.WidthMillimetres / 2 && Math.Abs(across) <= 1000L)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertNothingOverlaps(FireReactionSimulation simulation, FireReactionScenarioData data, string context,
            PressWatch presses, HashSet<SimulationId> wentOutside = null)
        {
            FireReactionSnapshot snapshot = simulation.GetSnapshot();
            presses.Check(simulation, context);
            for (int b = 0; b < snapshot.PhysicsObjects.Count; b++)
            {
                FireReactionPhysicsObjectSnapshot box = snapshot.PhysicsObjects[b];
                if (box.IsHeld || box.Dormant || box.Pose.HeightMillimetres >= 100)
                {
                    // Carried, not yet in the world, or up off the floor.
                    continue;
                }

                // Its middle is in a room, or in a doorway between two: things
                // can slide through an open door now. With real physics a thing
                // kicked through a way out that stands open ends up outside,
                // which is fine; the test is here to catch things going through
                // walls.
                if (wentOutside != null && !InAnyRoom(data, box.Position, 0) &&
                    (wentOutside.Contains(box.ObjectId) || ThroughAnOpenWayOut(snapshot, box.Position)))
                {
                    wentOutside.Add(box.ObjectId);
                    continue;
                }

                Assert.That(InAnyRoom(data, box.Position, 0), Is.True,
                    $"{context}: box {box.ObjectId} left the rooms at tick {snapshot.Tick}.");
            }
        }

        // ---------------------------------------------------------------- sliding

        [Test]
        public void LowerGravity_KeepsATossedBoxInTheAirLonger()
        {
            int TicksInTheAir(int gravityPercent)
            {
                FireReactionScenarioData data = PersonAndBox(-3000, 300, 3000);
                data.Agents = new[]
                {
                    new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(4000, 4000), CardinalDirection.North)
                };
                data.PhysicsFeel.GravityPercent = gravityPercent;
                var simulation = new FireReactionSimulation(data);
                simulation.TossObjectUpForTests(0, 60);
                int airborne = 0;
                for (int t = 0; t < 200; t++)
                {
                    simulation.Step();
                    if (simulation.GetPhysicsObject(0).Pose.HeightMillimetres > 20)
                    {
                        airborne++;
                    }
                }

                return airborne;
            }

            int snappy = TicksInTheAir(150);
            int floaty = TicksInTheAir(75);
            Assert.That(snappy, Is.GreaterThan(5), "A box tossed up at 3 m/s should leave the floor.");
            Assert.That(floaty, Is.GreaterThan(snappy * 3 / 2),
                $"Half the gravity should keep it up much longer ({snappy} ticks at 150%, {floaty} at 75%).");
        }

        [Test]
        public void KickedBox_SlidesAboutAMetreAndStops()
        {
            FireReactionScenarioData data = PersonAndBox(-3000, 300, 3000);
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(4000, 4000), CardinalDirection.North)
            };
            var simulation = new FireReactionSimulation(data);
            simulation.LaunchObjectForTests(0, 60, 0);
            for (int t = 0; t < 100; t++)
            {
                simulation.Step();
            }

            FireReactionPhysicsObjectSnapshot box = simulation.GetPhysicsObject(0);
            Assert.That(box.SpeedMillimetresPerTick, Is.EqualTo(0), "Friction never stopped the box.");
            Assert.That(box.Position.Z, Is.InRange(-10, 10), "It slides straight.");
            Assert.That(box.Position.X + 3000, Is.InRange(800, 1500), "A box kicked at 3 m/s should slide about a metre.");
        }

        [Test]
        public void FastBox_BouncesOffTheWallsAndStaysInside()
        {
            FireReactionScenarioData data = PersonAndBox(4000, 400, 6000);
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-4000, 4000), CardinalDirection.North)
            };
            var simulation = new FireReactionSimulation(data);
            var presses = new PressWatch();
            simulation.LaunchObjectForTests(0, 100, -100);
            int highestX = int.MinValue;
            for (int t = 0; t < 200; t++)
            {
                simulation.Step();
                AssertNothingOverlaps(simulation, data, "wall bounce", presses);
                highestX = Math.Max(highestX, simulation.GetPhysicsObject(0).Position.X);
            }

            // Square on, its middle stops half its width from the wall; spun by
            // an earlier bounce it can meet the wall corner first, as much as
            // half its diagonal (283 mm) away.
            Assert.That(highestX, Is.InRange(data.Rooms[0].Bounds.MaxX - 290, data.Rooms[0].Bounds.MaxX - 200),
                "The box should have reached the east wall.");

            Assert.That(simulation.GetPhysicsObject(0).Position.X, Is.LessThan(highestX), "The box did not bounce off the wall.");
        }

        // ---------------------------------------------------------------- boxes into people

        [Test]
        public void HeavyFastBox_TripsAStandingPerson()
        {
            FireReactionScenarioData data = PersonAndBox(-1000, 600, 20000);
            var simulation = new FireReactionSimulation(data);
            var presses = new PressWatch();
            simulation.LaunchObjectForTests(0, 100, 0);
            for (int t = 0; t < 30; t++)
            {
                simulation.Step();
                AssertNothingOverlaps(simulation, data, "heavy box", presses);
            }

            List<CausalEvent> hits = EventsOfType(simulation, FireReactionEventType.BoxHitAgent);
            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].SourceId, Is.EqualTo(new SimulationId(3001UL)));
            List<CausalEvent> trips = EventsOfType(simulation, FireReactionEventType.AgentTripped);
            Assert.That(trips, Has.Count.EqualTo(1));
            Assert.That(trips[0].CausalParentEventId, Is.EqualTo(hits[0].EventId));
            FireReactionAgentSnapshot person = simulation.GetAgent(0);
            Assert.That(person.IsDown, Is.True);
            Assert.That(person.FearState, Is.Not.EqualTo(AgentFearState.Calm), "Being bowled over by a box is alarming.");
            Assert.That(simulation.GetPhysicsObject(0).Position.X, Is.LessThan(-550 + 5), "The box should stop at the person.");
        }

        [Test]
        public void LightSlowBox_BouncesOffWithoutHurtingAnyone()
        {
            FireReactionScenarioData data = PersonAndBox(-1000, 300, 3000);
            var simulation = new FireReactionSimulation(data);
            var presses = new PressWatch();
            simulation.LaunchObjectForTests(0, 60, 0);
            int peakX = int.MinValue;

            for (int t = 0; t < 60; t++)
            {
                simulation.Step();
                AssertNothingOverlaps(simulation, data, "light box", presses);
                peakX = Math.Max(peakX, simulation.GetPhysicsObject(0).Position.X);
            }

            Assert.That(peakX, Is.GreaterThan(-600), "The box never reached the person.");
            Assert.That(simulation.GetPhysicsObject(0).Position.X, Is.LessThan(peakX), "The box did not bounce back.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.BoxHitAgent), Is.Empty);
            Assert.That(simulation.GetAgent(0).BodyState, Is.EqualTo(AgentBodyState.Upright));
            Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm));
        }

        // ---------------------------------------------------------------- people into boxes

        [Test]
        public void PanickedCrowds_KickBoxesAroundWithoutOverlapping()
        {
            int bumps = 0;
            int moved = 0;
            for (ulong seed = 40UL; seed <= 49UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                var simulation = new FireReactionSimulation(data, seed);
                var presses = new PressWatch();
                var wentOutside = new HashSet<SimulationId>();
                var start = new LogicalPosition[simulation.PhysicsObjectCount];
                for (int b = 0; b < start.Length; b++)
                {
                    start[b] = simulation.GetPhysicsObject(b).Position;
                }

                int endTick = data.Fire.ActivationTick + 30 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Tick < endTick)
                {
                    simulation.Step();
                    AssertNothingOverlaps(simulation, data, $"seed {seed}", presses, wentOutside);
                }

                foreach (CausalEvent bump in EventsOfType(simulation, FireReactionEventType.BoxBumped))
                {
                    bumps++;
                    // Kicked by someone scared, or by someone who was calm until they caught fire.
                    Assert.That(simulation.EventLog.Get(bump.CausalParentEventId).EventType,
                        Is.EqualTo(FireReactionEventType.AgentScared).Or.EqualTo(FireReactionEventType.AgentCaughtFire));
                }

                for (int b = 0; b < start.Length; b++)
                {
                    moved += simulation.GetPhysicsObject(b).Position.Equals(start[b]) ? 0 : 1;
                }
            }

            Assert.That(bumps, Is.GreaterThan(0), "Panicked people never ran into a box.");
            Assert.That(moved, Is.GreaterThan(0), "No box was ever moved.");
        }

        [Test]
        public void RunnersCanTripOverBoxes()
        {
            int boxTrips = 0;
            for (ulong seed = 40UL; seed <= 49UL && boxTrips == 0; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                data.ObjectPhysics.TripScale = 1;
                data.ObjectPhysics.TripMaximumChancePercent = 100;
                var simulation = new FireReactionSimulation(data, seed);
                int endTick = data.Fire.ActivationTick + 30 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Tick < endTick)
                {
                    simulation.Step();
                }

                foreach (CausalEvent trip in EventsOfType(simulation, FireReactionEventType.AgentTripped))
                {
                    if (simulation.EventLog.Get(trip.CausalParentEventId).EventType == FireReactionEventType.BoxBumped)
                    {
                        boxTrips++;
                    }
                }
            }

            Assert.That(boxTrips, Is.GreaterThan(0), "Nobody sprinting into a box ever tripped over it.");
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void Validation_RejectsBadBoxes()
        {
            FireReactionScenarioData overlapping = DefaultData();
            overlapping.PhysicsObjects = new[] { Box(9001UL, -2000, 2000, 400, 5000), Box(9002UL, -1800, 2000, 400, 5000) };
            Assert.Throws<InvalidOperationException>(() => overlapping.Validate());

            FireReactionScenarioData onAPerson = DefaultData();
            onAPerson.PhysicsObjects = new[] { Box(9001UL, 0, -5000, 400, 5000) };
            Assert.Throws<InvalidOperationException>(() => onAPerson.Validate());

            FireReactionScenarioData outside = DefaultData();
            outside.PhysicsObjects = new[] { Box(9001UL, 5900, 0, 400, 5000) };
            Assert.Throws<InvalidOperationException>(() => outside.Validate());
        }
    }
}

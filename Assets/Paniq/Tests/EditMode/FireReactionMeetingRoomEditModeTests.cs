using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The meeting that is already under way when the fire starts: people who
    /// begin the run in a chair, chairs that face the table they are pulled up
    /// to, and the things standing on that table.
    /// </summary>
    public sealed class FireReactionMeetingRoomEditModeTests
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

        /// <summary>Whether a point is inside any table top.</summary>
        private static bool OnATable(FireReactionSnapshot snapshot, LogicalPosition point, int radius)
        {
            foreach (FireReactionTableSnapshot table in snapshot.Tables)
            {
                if (table.Bounds.ContainsCircle(point, radius))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void NinePeople_StartTheRunSeatedAndOneIsOnTheirFeet()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            FireReactionSnapshot snapshot = simulation.GetSnapshot();

            int seated = 0;
            int seatedInTheMeetingRoom = 0;
            foreach (FireReactionAgentSnapshot person in snapshot.Agents)
            {
                if (person.ActivityState != AgentActivityState.Sitting)
                {
                    continue;
                }

                seated++;
                seatedInTheMeetingRoom += person.Position.X > 9000 ? 1 : 0;
            }

            Assert.That(seated, Is.EqualTo(9), "Nine people are sitting at the meeting table before anything happens.");
            Assert.That(seatedInTheMeetingRoom, Is.EqualTo(9), "All of them are in the meeting room.");

            int onTheirFeetInTheMeetingRoom = 0;
            foreach (FireReactionAgentSnapshot person in snapshot.Agents)
            {
                if (person.ActivityState != AgentActivityState.Sitting && person.Position.X > 9000)
                {
                    onTheirFeetInTheMeetingRoom++;
                }
            }

            Assert.That(onTheirFeetInTheMeetingRoom, Is.EqualTo(1), "One person stands at the end of the table.");
        }

        [Test]
        public void EverySeatedPerson_FacesTheTableTheyAreSittingAt()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            FireReactionSnapshot snapshot = simulation.GetSnapshot();

            foreach (FireReactionAgentSnapshot person in snapshot.Agents)
            {
                if (person.ActivityState != AgentActivityState.Sitting)
                {
                    continue;
                }

                // Half a metre in front of them is table: a desk is only 0.7 m
                // deep, so a longer reach would overshoot it altogether.
                LogicalPosition ahead = person.Position + IntegerMath.Displacement(person.HeadingDegrees, 500);
                Assert.That(OnATable(snapshot, ahead, 0), Is.True,
                    $"Person {person.AgentId} is sitting but not looking at a table.");
            }
        }

        [Test]
        public void EveryChair_FacesATableAndEveryLaptop_StandsOnOne()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            FireReactionSnapshot snapshot = simulation.GetSnapshot();

            int chairs = 0;
            int laptops = 0;
            foreach (FireReactionPhysicsObjectSnapshot thing in snapshot.PhysicsObjects)
            {
                if (thing.Kind == PhysicsObjectKind.Chair || thing.Kind == PhysicsObjectKind.OfficeChair)
                {
                    chairs++;
                    LogicalPosition ahead = thing.Position + IntegerMath.Displacement(thing.HeadingDegrees, 500);
                    Assert.That(OnATable(snapshot, ahead, 0), Is.True,
                        $"Chair {thing.ObjectId} is not pulled up to a table.");
                }

                if (thing.Kind == PhysicsObjectKind.Laptop)
                {
                    laptops++;
                    Assert.That(thing.Resting, Is.True, $"Laptop {thing.ObjectId} is not standing on anything.");
                    Assert.That(OnATable(snapshot, thing.Position, thing.SizeMillimetres / 2), Is.True,
                        $"Laptop {thing.ObjectId} is not on a table.");
                }
            }

            Assert.That(chairs, Is.EqualTo(17), "Eight chairs at the office desks and nine at the meeting table.");
            Assert.That(laptops, Is.GreaterThan(0));
        }

        [Test]
        public void ALaptopOnADesk_IsInNobodysWayButBlocksOnceItIsOnTheFloor()
        {
            FireReactionScenarioData data = DefaultData();
            data.Fire.ActivationTick = int.MaxValue;
            var simulation = new FireReactionSimulation(data);

            int laptop = -1;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).Kind == PhysicsObjectKind.Laptop)
                {
                    laptop = i;
                    break;
                }
            }

            Assert.That(laptop, Is.GreaterThanOrEqualTo(0));
            FireReactionPhysicsObjectSnapshot before = simulation.GetPhysicsObject(laptop);
            Assert.That(before.Resting, Is.True, "It starts on a desk.");

            // Knocked off: it comes loose and lands clear of the table it stood on.
            simulation.LaunchObjectForTests(laptop, 0, 60);
            simulation.Step();

            FireReactionPhysicsObjectSnapshot after = simulation.GetPhysicsObject(laptop);
            Assert.That(after.Resting, Is.False, "Once it is sent flying it is on the floor, not on the desk.");
            Assert.That(OnATable(simulation.GetSnapshot(), after.Position, after.SizeMillimetres / 2), Is.False,
                "A laptop on the floor is never left inside the table it fell off.");
        }
    }
}

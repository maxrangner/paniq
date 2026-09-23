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

        /// <summary>North of the corridor: the meeting room and the cafeteria.</summary>
        private static bool AcrossTheCorridor(LogicalPosition where) => where.Z > 9000;

        [Test]
        public void EightPeople_StartTheRunSeated_SixOfThemInTheMeeting()
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
                seatedInTheMeetingRoom += AcrossTheCorridor(person.Position) && person.Position.X < 2000 ? 1 : 0;
            }

            Assert.That(seated, Is.EqualTo(8),
                "Six round the meeting table and two at a cafeteria table, before anything happens.");
            Assert.That(seatedInTheMeetingRoom, Is.EqualTo(6), "Six of them are in the meeting.");
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

        /// <summary>
        /// The whole meeting gets up without anybody gliding backwards. Each of
        /// the six used to be shoved 800 mm straight back from the way they
        /// faced in the single tick they stopped being seated, and since three
        /// of them face north and three face south, the table emptied outwards
        /// into both walls at once.
        /// </summary>
        [Test]
        public void WhenTheMeetingIsStartled_NobodyGlidesBackwardsOutOfTheirChair()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            var wasAt = new LogicalPosition[simulation.AgentCount];
            var seated = new bool[simulation.AgentCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                FireReactionAgentSnapshot person = simulation.GetAgent(i);
                wasAt[i] = person.Position;
                seated[i] = person.ActivityState == AgentActivityState.Sitting &&
                            AcrossTheCorridor(person.Position) && person.Position.X < 2000;
            }

            Assert.That(System.Array.FindAll(seated, s => s).Length, Is.EqualTo(6), "Six are in the meeting.");

            // Long enough for the fire to break out, the bell to go and every
            // one of them to be up and running.
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    if (!seated[i])
                    {
                        continue;
                    }

                    LogicalPosition now = simulation.GetAgent(i).Position;
                    long step = IntegerMath.Distance(wasAt[i], now);
                    Assert.That(step, Is.LessThan(200L),
                        $"Person {simulation.GetAgent(i).AgentId} crossed {step} mm in one tick: that is a teleport, not a step.");
                    wasAt[i] = now;
                }
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

            Assert.That(chairs, Is.EqualTo(16),
                "Eight at the office desks, six at the meeting table and two in the cafeteria.");
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
            Assert.That(before.Pose.HeightMillimetres, Is.GreaterThan(700), "Up at desk height.");

            // Knocked hard across the desk: it skids off the edge, drops and
            // lands on the floor clear of the table it stood on.
            simulation.LaunchObjectForTests(laptop, 0, 60);
            simulation.Step();
            Assert.That(simulation.GetPhysicsObject(laptop).Resting, Is.False, "Sent sliding, it is no longer at rest on the desk.");
            for (int t = 0; t < 2 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            FireReactionPhysicsObjectSnapshot after = simulation.GetPhysicsObject(laptop);
            // Down on the floor, perhaps on its side with the screen propping it
            // up a little, but nowhere near desk height.
            Assert.That(after.Pose.HeightMillimetres, Is.LessThan(300), "It fell to the floor.");
            Assert.That(OnATable(simulation.GetSnapshot(), after.Position, after.SizeMillimetres / 2 - 20), Is.False,
                "A laptop on the floor is never left inside the table it fell off.");

        }
    }
}

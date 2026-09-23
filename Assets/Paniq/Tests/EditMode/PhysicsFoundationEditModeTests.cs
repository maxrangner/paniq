using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The promises the 3D physics has to keep before anything is built on
    /// it: a run's physics is its own, collisions are reported straight
    /// after the step they happened in, and the same seed plays out the same
    /// way every time.
    /// </summary>
    [Category("UnityPhysics")]
    public sealed class PhysicsFoundationEditModeTests
    {
        private static FireReactionScenarioData DefaultData()
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                return scenario.ToRuntimeData();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        private static readonly LogicalBounds Yard = new LogicalBounds(-5000, 5000, -5000, 5000);

        private static ObjectShapes.Part[] Crate() => ObjectShapes.For(PhysicsObjectKind.Box, 400);

        [Test]
        public void TwoBodiesThatMeet_AreReportedStraightAfterTheStep()
        {
            using var world = new PhysicsWorld(new PhysicsFeelSettings(), Yard, 30);
            int left = world.AddBody("left", Crate(), -50000L, 0L, 0L, 0, 5000, 40, 40);
            int right = world.AddBody("right", Crate(), 50000L, 0L, 0L, 0, 5000, 40, 40);
            world.SetVelocity(left, 10000L, 0L, 0L);

            bool reported = false;
            for (int tick = 0; tick < 50 && !reported; tick++)
            {
                world.Step();
                foreach (PhysicsWorld.Contact contact in world.Contacts)
                {
                    reported |= contact.BodyA == left && contact.BodyB == right && contact.Began;
                }
            }

            Assert.That(reported, Is.True, "A crate slid into another was never reported as hitting it.");
            Assert.That(world.Read(right).VelocityX, Is.GreaterThan(0L), "The crate that was hit did not move off.");
        }

        [Test]
        public void ABodyDroppedFromAHeight_FallsAndLandsOnTheFloor()
        {
            using var world = new PhysicsWorld(new PhysicsFeelSettings(), Yard, 30);
            int crate = world.AddBody("crate", Crate(), 0L, 150000L, 0L, 0, 5000, 40, 40);
            for (int tick = 0; tick < 150; tick++)
            {
                world.Step();
            }

            Assert.That(world.Read(crate).BottomMillimetres, Is.InRange(-5, 5),
                "A crate let go 1.5 m up did not end up standing on the floor.");
        }

        [Test]
        public void TwoWorldsAtOnce_NeverTouchEachOther()
        {
            using var first = new PhysicsWorld(new PhysicsFeelSettings(), Yard, 30);
            using var second = new PhysicsWorld(new PhysicsFeelSettings(), Yard, 30);
            int mover = first.AddBody("mover", Crate(), 0L, 0L, 0L, 0, 5000, 40, 40);
            int bystander = second.AddBody("bystander", Crate(), 30000L, 0L, 0L, 0, 5000, 40, 40);
            first.SetVelocity(mover, 10000L, 0L, 0L);
            for (int tick = 0; tick < 40; tick++)
            {
                first.Step();
                second.Step();
            }

            Assert.That(first.Read(mover).X, Is.GreaterThan(30000L), "The crate in the first world never got past the second's.");
            Assert.That(second.Read(bystander).X, Is.EqualTo(30000L),
                "A crate in one world was moved by a crate in another.");
        }

        // TheSameSeed_PlaysOutTheSameWayTwice was three narrower versions of
        // TheBusiestRun_PlaysOutTheSameWayThreeTimes below, which replays the
        // same seed with the doors worked, the boxes kicked and the cards
        // played all at once. Anything that made the run wander would have to
        // get past that first.

        /// <summary>
        /// Three times over, with every door opened, every card played and
        /// every box kicked: the busiest run there is, so that anything in the
        /// engine that is only nearly repeatable (collision reports after a
        /// thing is smashed, say) has the most chances to show itself.
        /// </summary>
        [Test]
        public void TheBusiestRun_PlaysOutTheSameWayThreeTimes()
        {
            ulong first = ReplayFingerprint.Run(DefaultData(), 41UL, openDoors: true, kickBoxes: true, playCards: true);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                ulong again = ReplayFingerprint.Run(DefaultData(), 41UL, openDoors: true, kickBoxes: true, playCards: true);
                Assert.That(again, Is.EqualTo(first), $"Repeat {repeat + 2} of the same run came out different.");
            }
        }

        [Test]
        public void DifferentSeeds_PlayOutDifferently()
        {
            ulong first = ReplayFingerprint.Run(DefaultData(), 42UL, openDoors: true);
            ulong second = ReplayFingerprint.Run(DefaultData(), 43UL, openDoors: true);
            Assert.That(second, Is.Not.EqualTo(first));
        }
    }
}

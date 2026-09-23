using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Whether a person can read a given sign from where they stand, asked of
    /// the sign reader directly rather than through a whole run. What a crowd
    /// then does with the answer is emergent and belongs in its own tests;
    /// this is the flat question underneath it.
    /// </summary>
    public sealed class ExitSignReadingEditModeTests
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

        /// <summary>A sign reader for a floor carrying exactly these signs, and one person to do the reading.</summary>
        private static (ExitSignBehaviour Signs, Agent Person) Set(
            ScenarioData data, LogicalPosition standing, int facing)
        {
            DoorRuntime[] doors = DoorSystem.CreateDoors(data);
            var context = new SimulationContext(data, 1UL);
            var geometry = new WorldGeometry(context, doors);
            var person = new Agent(0, new SimulationId(1UL), doors.Length);
            var crowd = new Crowd(new[] { person }, data.World.OccupancyRadiusMillimetres, geometry.FireArea);
            crowd.MoveTo(person, standing);
            person.Body.Heading = facing;
            return (new ExitSignBehaviour(context, geometry), person);
        }

        private ScenarioData FloorWithOneSign(LogicalPosition at, int pointing)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.ExitSigns = new[] { new ExitSignDefinition(at, pointing) };
            return data;
        }

        [Test]
        public void ASignStraightAhead_IsRead()
        {
            const int pointing = 90;
            (ExitSignBehaviour signs, Agent person) =
                Set(FloorWithOneSign(new LogicalPosition(0, 2500), pointing), new LogicalPosition(0, 0), 0);

            Assert.That(signs.TryRead(person, out int read), Is.True, "A sign 2.5 m straight ahead should be readable.");
            Assert.That(read, Is.EqualTo(pointing));
        }

        [Test]
        public void ASignBehindThem_IsNotRead()
        {
            (ExitSignBehaviour signs, Agent person) =
                Set(FloorWithOneSign(new LogicalPosition(0, 2500), 90), new LogicalPosition(0, 0), 180);

            Assert.That(signs.TryRead(person, out _), Is.False, "Nobody reads a sign behind their own head.");
        }

        [Test]
        public void ASignTooFarOff_IsNotRead()
        {
            // The office is 12 m across, so a sign in the far corner of it is
            // well past the 8 m a sign can be read at.
            (ExitSignBehaviour signs, Agent person) =
                Set(FloorWithOneSign(new LogicalPosition(0, 5500), 90), new LogicalPosition(0, -5500), 0);

            Assert.That(signs.TryRead(person, out _), Is.False, "11 m is further than a sign can be read.");
        }

        [Test]
        public void ASignInAClosedRoomBesideThem_IsNotRead()
        {
            // In the storage closet, with the office's door into it shut.
            (ExitSignBehaviour signs, Agent person) =
                Set(FloorWithOneSign(TheBuilding.Closet, 90), new LogicalPosition(5000, 2500), 90);

            Assert.That(signs.TryRead(person, out _), Is.False, "Nobody reads a sign through a wall.");
        }

        [Test]
        public void ASpotTheSignPointsAt_ScoresBetterThanOneBehindThem()
        {
            const int east = 90;
            var standing = new LogicalPosition(0, 0);
            (ExitSignBehaviour signs, Agent _) =
                Set(FloorWithOneSign(new LogicalPosition(0, 2500), east), standing, 0);

            long eastward = signs.ScoreToward(standing, new LogicalPosition(4000, 0), east);
            long westward = signs.ScoreToward(standing, new LogicalPosition(-4000, 0), east);

            Assert.That(eastward, Is.GreaterThan(0L), "The way the sign points is worth something.");
            Assert.That(westward, Is.EqualTo(-eastward), "And the opposite way costs the same.");
        }
    }
}

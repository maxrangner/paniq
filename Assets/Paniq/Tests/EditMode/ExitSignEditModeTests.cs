using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The exit signs as content: where they hang and what they point at.
    /// Whether a person can read one from where they stand is
    /// <see cref="ExitSignReadingEditModeTests"/>.
    /// <para>
    /// Somebody who works here still runs a route search across the whole
    /// building and never needs a sign. A visitor does: what a sign teaches
    /// them, and a stranger following one out, are in
    /// <see cref="WayfindingEditModeTests"/> and
    /// <see cref="WayfindingRunEditModeTests"/>. See the decision log.
    /// </para>
    /// </summary>
    public sealed class ExitSignEditModeTests
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
        /// The signs the floor really ships with hang in a room and point at
        /// floor somebody could actually walk on, rather than at a wall.
        /// </summary>
        [Test]
        public void EverySignOnTheFloor_PointsAlongOpenFloor()
        {
            ScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.ExitSigns.Length, Is.GreaterThan(0), "The floor has signs.");

            foreach (ExitSignDefinition sign in data.ExitSigns)
            {
                Assert.That(InSomeRoom(data, sign.At), Is.True,
                    $"The sign at {sign.At} hangs somewhere that is not a room.");
                LogicalPosition ahead = sign.At + IntegerMath.Displacement(sign.PointingDegrees, 1000);
                Assert.That(InSomeRoom(data, ahead), Is.True,
                    $"The sign at {sign.At} points at something that is not floor.");
            }
        }

        private static bool InSomeRoom(ScenarioData data, LogicalPosition point)
        {
            foreach (RoomDefinition room in data.Rooms)
            {
                if (room.Bounds.ContainsCircle(point, 0))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using NUnit.Framework;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The physics feel dials: live tuning in the editor copies only the dials
    /// a running world can take, and a bad set of values is caught rather
    /// than thrown at the engine.
    /// </summary>
    public sealed class PhysicsFeelEditModeTests
    {
        [Test]
        public void LiveTuning_CopiesOnlyTheDialsARunningWorldCanTake()
        {
            var running = new PhysicsFeelSettings();
            var tuned = new PhysicsFeelSettings
            {
                GravityPercent = 90,
                BlastStrengthPercent = 50,
                CrushPressure = 900,
                WallHeightMillimetres = 2500,
                TableHeightMillimetres = 700,
                FloorGripPercent = 150,
                MaximumSpeedMillimetresPerTick = 300
            };

            running.TakeLiveValuesFrom(tuned);

            Assert.That(running.GravityPercent, Is.EqualTo(90));
            Assert.That(running.BlastStrengthPercent, Is.EqualTo(50));
            Assert.That(running.CrushPressure, Is.EqualTo(900));
            var untouched = new PhysicsFeelSettings();
            Assert.That(running.WallHeightMillimetres, Is.EqualTo(untouched.WallHeightMillimetres),
                "The walls are already built; their height cannot change mid-run.");
            Assert.That(running.TableHeightMillimetres, Is.EqualTo(untouched.TableHeightMillimetres));
            Assert.That(running.FloorGripPercent, Is.EqualTo(untouched.FloorGripPercent));
            Assert.That(running.MaximumSpeedMillimetresPerTick, Is.EqualTo(untouched.MaximumSpeedMillimetresPerTick));
        }

        [Test]
        public void ABadSetOfDials_IsCaughtWithAReason()
        {
            Assert.That(new PhysicsFeelSettings().IsValid(out _), Is.True);
            var bad = new PhysicsFeelSettings { GravityPercent = 0 };
            Assert.That(bad.IsValid(out string error), Is.False);
            Assert.That(error, Does.Contain("physics feel"));
        }
    }
}

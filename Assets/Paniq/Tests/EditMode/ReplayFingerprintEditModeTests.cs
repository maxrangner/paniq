using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Guards against invisible behaviour changes. Each case runs one minute
    /// of the default scenario and compares the run's fingerprint with the
    /// value recorded for the current compatibility version. If a change is
    /// meant to alter behaviour, bump the compatibility version and content
    /// revision and re-record these values in the same commit.
    /// </summary>
    public sealed class ReplayFingerprintEditModeTests
    {
        [TestCase(42UL, false, 0xE5299A0AA77F4054UL)]
        [TestCase(42UL, true, 0x1CF6F00A802A1B7DUL)]
        [TestCase(40UL, false, 0xD62D0519BD9DD190UL)]
        [TestCase(40UL, true, 0x9E6B220AEF5125B1UL)]
        [TestCase(46UL, false, 0xD6A0E59864782E37UL)]
        [TestCase(46UL, true, 0x40BDACA56AE466F4UL)]
        public void DefaultScenario_ReplaysToTheRecordedFingerprint(ulong seed, bool openDoors, ulong expected)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Run(scenario.ToRuntimeData(), seed, openDoors);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, doors {(openDoors ? "opened" : "locked")}: fingerprint is 0x{actual:X16}UL. " +
                    "If behaviour was meant to change, bump the compatibility version and re-record.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        [TestCase(42UL, 0x9474348BBEB28807UL)]
        [TestCase(40UL, 0x48D53B6CCAA7734FUL)]
        public void KickedBoxes_ReplayToTheRecordedFingerprint(ulong seed, ulong expected)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Run(scenario.ToRuntimeData(), seed, false, true);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, boxes kicked: fingerprint is 0x{actual:X16}UL. " +
                    "If behaviour was meant to change, bump the compatibility version and re-record.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }
    }
}

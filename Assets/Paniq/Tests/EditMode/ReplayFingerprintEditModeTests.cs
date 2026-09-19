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
        [TestCase(42UL, false, 0x8ECA624E5D919252UL)]
        [TestCase(42UL, true, 0xE48001C3E44C81CCUL)]
        [TestCase(40UL, false, 0xAC3113228BDB6FF9UL)]
        [TestCase(40UL, true, 0x28E414D8535B07C3UL)]
        [TestCase(46UL, false, 0x0032B45583433610UL)]
        [TestCase(46UL, true, 0xE437CCA8510F86F8UL)]
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

        [TestCase(42UL, 0x95C52DB3DA8DE6D4UL)]
        [TestCase(40UL, 0x2D564F1CDEA8EC9FUL)]
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

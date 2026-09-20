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
        [TestCase(42UL, false, 0xE719728EEAFD0C74UL)]
        [TestCase(42UL, true, 0xB81F7EF99F36B5C9UL)]
        [TestCase(40UL, false, 0x966C5787DF8A792FUL)]
        [TestCase(40UL, true, 0x1C774B237F1452CBUL)]
        [TestCase(46UL, false, 0x7EFD0329458F139CUL)]
        [TestCase(46UL, true, 0xFBCBB6C5D3B0BDB7UL)]
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

        /// <summary>
        /// The player's cards, which are the one thing that reaches the run from
        /// outside it. Guarded here as well as by their own tests, so the whole
        /// command path is covered by replay.
        /// </summary>
        [TestCase(42UL, 0xA969A6639E336E87UL)]
        [TestCase(40UL, 0xB99419A1B548DDFFUL)]
        public void CardsPlayed_ReplayToTheRecordedFingerprint(ulong seed, ulong expected)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Run(scenario.ToRuntimeData(), seed, false, false, true);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, cards played: fingerprint is 0x{actual:X16}UL. " +
                    "If behaviour was meant to change, bump the compatibility version and re-record.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        [TestCase(42UL, 0x3DF1BBA6A0EDE85AUL)]
        [TestCase(40UL, 0x86BBA3C074812453UL)]
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

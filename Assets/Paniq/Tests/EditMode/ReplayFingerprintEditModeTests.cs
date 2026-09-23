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
        [TestCase(42UL, false, 0x49162F9F13BA4CBFUL)]
        [TestCase(42UL, true, 0x6DC565E335AB9E01UL)]
        [TestCase(40UL, false, 0x95DF1C58279F0F21UL)]
        [TestCase(40UL, true, 0xB74C9C8F99F6089DUL)]
        [TestCase(46UL, false, 0x3B28DD6AC685CDC6UL)]
        [TestCase(46UL, true, 0x914E0A641ABC350DUL)]
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
        /// The player's cards and the trigger that sets the disaster going:
        /// everything that reaches the run from outside it. Guarded here as
        /// well as by their own tests, so the whole command path is covered by
        /// replay. This run waits to be triggered, as a played level does.
        /// </summary>
        [TestCase(42UL, 0xFE49824B5192FB70UL)]
        [TestCase(40UL, 0x0D03EB255D128428UL)]
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

        [TestCase(42UL, 0x90A31B82ED821E28UL)]
        [TestCase(40UL, 0x1B00BBF08DFB91F1UL)]
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

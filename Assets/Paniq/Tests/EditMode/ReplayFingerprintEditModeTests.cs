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
        // TO RE-RECORD: compatibility version 41 (visitors) changes every run
        // below, but these could only be recorded inside the Unity editor, and
        // the change was written without one. Until they are re-recorded they
        // still hold version 40's values and fail. Run them in the editor, paste
        // in the value each failure message prints, and delete this note. The
        // WithNoVisitors test at the bottom must pass before and after.
        [TestCase(42UL, false, 0x390203F0F6720385UL)]
        [TestCase(42UL, true, 0xE67C0708659BD136UL)]
        [TestCase(40UL, false, 0xA840F980F57E70ECUL)]
        [TestCase(40UL, true, 0x0BE208AA8F2101CFUL)]
        [TestCase(46UL, false, 0xC0D916341F6E24CCUL)]
        [TestCase(46UL, true, 0x30ABB1F51C684582UL)]
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
        [TestCase(42UL, 0xEC7769BD687259ACUL)]
        [TestCase(40UL, 0xFB3B49308CBCFE42UL)]
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

        [TestCase(42UL, 0xC36274F7296CA5FFUL)]
        [TestCase(40UL, 0x189616C48F4AA7E9UL)]
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

        /// <summary>
        /// Every run above, with the meeting's visitors made staff again: the
        /// fingerprints recorded before anybody could be a stranger, kept
        /// exactly as they were. This is what proves that somebody who knows
        /// the building walks exactly as everybody used to -- that the whole of
        /// finding your way (knowing doors, looking round, searching, being
        /// told) costs a person who knows the floor nothing at all, not so
        /// much as one random number.
        /// <para>
        /// Unlike the fingerprints above these should never be re-recorded:
        /// a change that moves them has changed how staff behave.
        /// </para>
        /// </summary>
        [TestCase(42UL, Run.DoorsLocked, 0x390203F0F6720385UL)]
        [TestCase(42UL, Run.DoorsOpened, 0xE67C0708659BD136UL)]
        [TestCase(40UL, Run.DoorsLocked, 0xA840F980F57E70ECUL)]
        [TestCase(40UL, Run.DoorsOpened, 0x0BE208AA8F2101CFUL)]
        [TestCase(46UL, Run.DoorsLocked, 0xC0D916341F6E24CCUL)]
        [TestCase(46UL, Run.DoorsOpened, 0x30ABB1F51C684582UL)]
        [TestCase(42UL, Run.CardsPlayed, 0xEC7769BD687259ACUL)]
        [TestCase(40UL, Run.CardsPlayed, 0xFB3B49308CBCFE42UL)]
        [TestCase(42UL, Run.BoxesKicked, 0xC36274F7296CA5FFUL)]
        [TestCase(40UL, Run.BoxesKicked, 0x189616C48F4AA7E9UL)]
        public void WithNoVisitors_TheFloorReplaysExactlyAsItDidBefore(ulong seed, Run run, ulong expected)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                FireReactionScenarioData data = scenario.ToRuntimeData();
                for (int i = 0; i < data.Agents.Length; i++)
                {
                    data.Agents[i] = data.Agents[i].WithFamiliarity(AgentFamiliarity.KnowsTheBuilding);
                }

                ulong actual = ReplayFingerprint.Run(data, seed, run == Run.DoorsOpened, run == Run.BoxesKicked,
                    run == Run.CardsPlayed);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, {run}, nobody a visitor: fingerprint is 0x{actual:X16}UL. " +
                    "Somebody who knows the building no longer behaves as they did before visitors existed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        /// <summary>Which of the recorded runs to play.</summary>
        public enum Run
        {
            DoorsLocked,
            DoorsOpened,
            CardsPlayed,
            BoxesKicked
        }
    }
}

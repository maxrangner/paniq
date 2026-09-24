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
        [TestCase(42UL, false, 0x49B42643D5AD1178UL)]
        [TestCase(42UL, true, 0x9E4BD7A9C67BCF5BUL)]
        [TestCase(40UL, false, 0x305766F9D5B192E5UL)]
        [TestCase(40UL, true, 0xAEE0989B5A97EFF8UL)]
        [TestCase(46UL, false, 0x0589B48D3191CE9FUL)]
        [TestCase(46UL, true, 0xA31DF32598E18628UL)]
        public void DefaultScenario_ReplaysToTheRecordedFingerprint(ulong seed, bool openDoors, ulong expected)
        {
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Of(scenario.ToRuntimeData(), seed, openDoors);
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
        [TestCase(42UL, 0x0C9C335132A5D075UL)]
        [TestCase(40UL, 0xB942394094EF3A54UL)]
        public void CardsPlayed_ReplayToTheRecordedFingerprint(ulong seed, ulong expected)
        {
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Of(scenario.ToRuntimeData(), seed, false, false, true);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, cards played: fingerprint is 0x{actual:X16}UL. " +
                    "If behaviour was meant to change, bump the compatibility version and re-record.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        [TestCase(42UL, 0x8F9CE3392741D5C3UL)]
        [TestCase(40UL, 0xEC3EAD92CD8195C0UL)]
        public void KickedBoxes_ReplayToTheRecordedFingerprint(ulong seed, ulong expected)
        {
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                ulong actual = ReplayFingerprint.Of(scenario.ToRuntimeData(), seed, false, true);
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
        /// Every run above, with the meeting's visitors made staff again. This
        /// is what proves that somebody who knows the building walks exactly as
        /// everybody used to -- that the whole of finding your way (knowing
        /// doors, looking round, searching, being told) costs a person who
        /// knows the floor nothing at all, not so much as one random number.
        /// <para>
        /// These were recorded before anybody could be a stranger, and were
        /// meant never to be re-recorded. They were re-recorded exactly once,
        /// when the economy landed: the dead now deal the player a card, which
        /// writes a line into the log of every run somebody dies in, so the
        /// numbers moved for a reason that has nothing to do with wayfinding.
        /// From here they hold the meaning they were written for -- a change
        /// that moves them has changed how staff behave -- and should not be
        /// re-recorded again.
        /// </para>
        /// <para>
        /// Three cases, not ten, and on the seeds where it means something. It
        /// used to run seeds 40, 42 and 46, where the meeting room is never
        /// frightened inside the minute: the visitors never looked for
        /// anything, so nine of the ten cases were running a simulation
        /// identical to the test above them and proving only that a run equals
        /// itself. Seed 41 frightens the meeting room -- five people find the
        /// way out and four go looking, measured -- and cards played on seed 42
        /// frightens it too, so all three now genuinely compare a floor of
        /// staff against a floor with strangers on it.
        /// </para>
        /// </summary>
        [TestCase(41UL, RecordedRun.DoorsLocked, 0x32734715286596DCUL)]
        [TestCase(41UL, RecordedRun.DoorsOpened, 0x9F4B9F4D5FDCBDAEUL)]
        [TestCase(42UL, RecordedRun.CardsPlayed, 0xBF3D7CBE9E12F15AUL)]
        public void WithNoVisitors_TheFloorReplaysExactlyAsItDidBefore(ulong seed, RecordedRun run, ulong expected)
        {
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                ScenarioData data = scenario.ToRuntimeData();
                for (int i = 0; i < data.Agents.Length; i++)
                {
                    data.Agents[i] = data.Agents[i].WithFamiliarity(AgentFamiliarity.KnowsTheBuilding);
                }

                ulong actual = ReplayFingerprint.Of(data, seed, run == RecordedRun.DoorsOpened, run == RecordedRun.BoxesKicked,
                    run == RecordedRun.CardsPlayed);
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
        public enum RecordedRun
        {
            DoorsLocked,
            DoorsOpened,
            CardsPlayed,
            BoxesKicked
        }
    }
}

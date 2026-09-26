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
        [TestCase(42UL, false, 0x123C823A0F9EB0ADUL)]
        [TestCase(42UL, true, 0xD94FA15E2B66B267UL)]
        [TestCase(40UL, false, 0x15C31BF39523A38FUL)]
        [TestCase(40UL, true, 0x6ADFF0018A8EDC83UL)]
        [TestCase(46UL, false, 0x0DA67E73C8152F86UL)]
        [TestCase(46UL, true, 0x01E30AF680DB41DFUL)]
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
        [TestCase(42UL, 0x5883C4FDDDDEBCDBUL)]
        [TestCase(40UL, 0xC7BC841E957A3EB4UL)]
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

        /// <summary>
        /// The office as the owner plays it (2026-09-26): the Director's
        /// ladder on, so the round opens with a waste bin catching in the
        /// meeting room -- at tick 250 here, rather than somewhere in the first
        /// minute and a half, so the minute recorded is spent on the fire.
        /// Guards the bin, its smoulder, a young fire's slow spread and the
        /// crowd calming down.
        /// </summary>
        [TestCase(42UL, 0x424C816E1A8A8AE5UL)]
        [TestCase(40UL, 0xD88EAE4E3E2FA94EUL)]
        public void TheLadder_ReplaysToTheRecordedFingerprint(ulong seed, ulong expected)
        {
            ScenarioAsset scenario = ScenarioAsset.CreateDefault();
            try
            {
                ScenarioData data = scenario.ToRuntimeData();
                data.Director.ClimbsTheLadder = true;
                data.Director.FirstIncidentMinimumTicks = 250;
                data.Director.FirstIncidentMaximumTicks = 250;
                data.Round.HazardWaitsForTrigger = true;
                ulong actual = ReplayFingerprint.Of(data, seed, false);
                Assert.That(actual, Is.EqualTo(expected),
                    $"Seed {seed}, the ladder: fingerprint is 0x{actual:X16}UL. " +
                    "If behaviour was meant to change, bump the compatibility version and re-record.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        [TestCase(42UL, 0x0726EF18C9627B77UL)]
        [TestCase(40UL, 0x480C7F1366E2D461UL)]
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
        /// They were re-recorded a second time when the building got a day
        /// (2026-09-24): the meeting ends by the timetable instead of a sit
        /// timer, calm people go home, to the toilet and over to talk, and
        /// every calm decision draws differently, for staff exactly as for
        /// strangers. That is a change to how everybody behaves and has
        /// nothing to do with wayfinding; a calm visitor asks the way like
        /// anybody else. From here the rule above holds again.
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
        /// <para>
        /// A change to the building moves them as well, staff and strangers
        /// alike, and that is not a change to how anybody behaves: when the
        /// pull station beside the way out came out (2026-09-25, content
        /// revision 79), the seed 42 cases were re-recorded because somebody
        /// who used to run for that station now runs for another. Seeds 40,
        /// 41 and 46 did not move at all.
        /// </para>
        /// <para>
        /// Prototype 3 (2026-09-25, version 67, content 80): the fire now
        /// always starts in the meeting room, the tower of boxes stands at the
        /// junction and comes down across the archway, and the one pull
        /// station is at the corridor's west end. Every seed is a different
        /// day, so all three cases were re-recorded along with the other ten;
        /// how somebody who knows the building finds their way did not change.
        /// </para>
        /// <para>
        /// Version 68 (2026-09-26): nobody may take a box from the standing
        /// tower any more. The two seed 41 cases moved because somebody used to
        /// take or knock a box off it; the seed 42 cards case did not move.
        /// </para>
        /// </summary>
        [TestCase(41UL, RecordedRun.DoorsLocked, 0x812610C9014B144AUL)]
        [TestCase(41UL, RecordedRun.DoorsOpened, 0xEF3E182DE3F18D4CUL)]
        [TestCase(42UL, RecordedRun.CardsPlayed, 0x3FA16F777B28FB4CUL)]
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

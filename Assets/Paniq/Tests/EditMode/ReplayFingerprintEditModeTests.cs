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
        [TestCase(42UL, false, 0xA126E14BB1784C28UL)]
        [TestCase(42UL, true, 0x2A276BAAABAA4032UL)]
        [TestCase(40UL, false, 0x3FC82188BC6EB53CUL)]
        [TestCase(40UL, true, 0xD0F29C0CFA2B47C9UL)]
        [TestCase(46UL, false, 0xB386CC8E46855056UL)]
        [TestCase(46UL, true, 0x06F7C8F446F6AC0DUL)]
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
        [TestCase(42UL, 0x9DEB89DD119CF1B4UL)]
        [TestCase(40UL, 0x2312666625F70B21UL)]
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
        [TestCase(42UL, 0x687FF354E82AD0B6UL)]
        [TestCase(40UL, 0x6FAFFA7FC0D5DFC5UL)]
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

        [TestCase(42UL, 0xDB3513D7C8507EFAUL)]
        [TestCase(40UL, 0x11A51A6149C706C5UL)]
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
        /// Version 75 (2026-09-30): the hand, second pass. The tower by the
        /// archway now falls on the spot where the first person to run past it
        /// in the crossbar stood, not across the archway, so every recorded run
        /// in which it falls moved; the "cards played" run also holds a door, a
        /// thing and the floor, which everybody now answers far more readily.
        /// How somebody who knows the building finds their way did not change.
        /// </para>
        /// <para>
        /// Version 74 (2026-09-29): the hand. Every trap creaks for about
        /// three seconds before it falls, jittered from the run's stream, so
        /// every recorded run in which the tower comes down moved, staff and
        /// strangers alike; the "cards played" run also holds and lets go of
        /// a door, a thing and the floor, and takes somebody by the shirt.
        /// Then seven moved again for two rules the creak's timing exposed on
        /// seed 41: an escape spot is never one they could not walk to, and
        /// somebody creeping against a pinned thing counts as blocked. How
        /// somebody who knows the building finds their way did not change.
        /// </para>
        /// <para>
        /// Version 73 (2026-09-28): a box held where it lies is shoved on
        /// only by a heaved thing. Fourteen of the fifteen cases moved (seed
        /// 46 opened held), because in every run the fallen tower's boxes
        /// used to un-hold each other as they settled; proven by putting the
        /// old rule back alone, under which all fifteen held again. Then all
        /// fifteen moved once more, because the boxes now hold and people
        /// meeting one in their way heave it at their next thought or go
        /// round it, where they used to press at it. The Director's cap is
        /// off in these runs and drew nothing from the run's stream.
        /// </para>
        /// <para>
        /// Version 72 (2026-09-27): the keycard. Every case moved, because the
        /// way out is now opened by whoever has the card, or not at all, and
        /// everybody who catches sight of the card draws a reaction lag. The
        /// "opened" runs put the card away (see <c>ReplayFingerprint.Of</c>),
        /// so there the change is the building's: one more thing in it.
        /// </para>
        /// <para>
        /// Version 68 (2026-09-26): nobody may take a box from the standing
        /// tower any more. The two seed 41 cases moved because somebody used to
        /// take or knock a box off it; the seed 42 cards case did not move.
        /// </para>
        /// </summary>
        [TestCase(41UL, RecordedRun.DoorsLocked, 0xED2E34409966C5D9UL)]
        [TestCase(41UL, RecordedRun.DoorsOpened, 0x03C4DE08ED49E649UL)]
        [TestCase(42UL, RecordedRun.CardsPlayed, 0xAAE78A23917FE214UL)]
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

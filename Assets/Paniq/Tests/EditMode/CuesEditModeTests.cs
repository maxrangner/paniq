using System;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Cues: the small things that happen in the building's day, and the
    /// three ways one is called -- the Director from the level's timetable,
    /// a person's own idea, and the player. What people then do about a cue
    /// is <see cref="ErrandsEditModeTests"/>' business.
    /// </summary>
    public sealed class CuesEditModeTests
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

        /// <summary>A calm day: the hazard waits to be triggered and nobody presses anything.</summary>
        private ScenarioData CalmDay()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = true;
            return data;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void TheDirector_CallsATimetableEntryOnce_OnItsTick()
        {
            ScenarioData data = CalmDay();
            data.Timetable = new[] { new ScheduledCue(CueKind.MeetingEnds, 300, 100, PrototypeBuilding.MeetingRoom) };
            using (var simulation = new Run(data))
            {
                Advance(simulation, 20 * Run.TicksPerSecond);

                int called = 0;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    // People's own ideas (a chat, a toilet trip) are cues too;
                    // only the timetable's is counted here.
                    if (record.EventType != CausalEventType.CueCalled || (CueKind)record.Strength != CueKind.MeetingEnds)
                    {
                        continue;
                    }

                    Assert.That(record.Tick, Is.EqualTo(300), "On the tick the timetable says, not before and not again.");
                    Assert.That(record.TargetId, Is.EqualTo(PrototypeBuilding.MeetingRoom), "Named the room.");
                    called++;
                }

                Assert.That(called, Is.EqualTo(1), "A timetable entry is called exactly once.");
            }
        }

        [Test]
        public void TheMeetingEnding_ReachesEverybodyCalmInTheRoom_NobodyOnTheTickItIsCalled()
        {
            ScenarioData data = CalmDay();
            const int at = 200;
            data.Timetable = new[] { new ScheduledCue(CueKind.MeetingEnds, at, 400, PrototypeBuilding.MeetingRoom) };
            using (var simulation = new Run(data))
            {
                Advance(simulation, at);

                ulong cue = 0UL;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    if (record.EventType == CausalEventType.CueCalled && (CueKind)record.Strength == CueKind.MeetingEnds)
                    {
                        cue = record.EventId;
                    }
                }

                Assert.That(cue, Is.Not.Zero, "The meeting ending is written down.");

                // Only the people this cue reached: an office worker wandering
                // back to their own desk is going home by their own idea.
                int reached = 0;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentErrand errand = simulation.ErrandForTests(i);
                    if (!errand.Has || errand.Cue != CueKind.MeetingEnds || errand.CauseEventId != cue)
                    {
                        continue;
                    }

                    reached++;
                    Assert.That(errand.Pending, Is.True, "Handed to them, not yet taken up: " + simulation.DescribeForTests(i));
                    Assert.That(errand.StartTick, Is.GreaterThan(at),
                        $"Person {simulation.GetAgent(i).AgentId} would take it up on the tick it was called.");
                    Assert.That(errand.StartTick, Is.LessThanOrEqualTo(at + data.Perception.ReactionLagMaximumTicks + 400));
                }

                Assert.That(reached, Is.EqualTo(6), "The six at the meeting, and nobody outside the room.");
            }
        }

        [Test]
        public void ThePlayerCanCallHomeTime_AndTheStoryNamesThemAsTheCause()
        {
            using (var simulation = new Run(CalmDay()))
            {
                simulation.QueueCommand(PlayerCommandType.CallHomeTime, default(SimulationId), 5);
                Advance(simulation, 6);

                ulong called = 0UL;
                ulong cue = 0UL;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    if (record.EventType == CausalEventType.PowerCalledHomeTime)
                    {
                        Assert.That(record.HasCausalParent, Is.False, "The player is the cause: a root event.");
                        called = record.EventId;
                    }

                    if (record.EventType == CausalEventType.CueCalled)
                    {
                        Assert.That((CueKind)record.Strength, Is.EqualTo(CueKind.HomeTime));
                        Assert.That(record.CausalParentEventId, Is.EqualTo(called), "The cue names the player's command as its cause.");
                        Assert.That(record.Tick, Is.EqualTo(5));
                        cue = record.EventId;
                    }
                }

                Assert.That(called, Is.Not.Zero, "The player calling it a day is a line in the story.");
                Assert.That(cue, Is.Not.Zero, "And it called the cue.");

                var startTicks = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentErrand errand = simulation.ErrandForTests(i);
                    Assert.That(errand.Has && errand.Cue == CueKind.HomeTime, Is.True,
                        $"Person {simulation.GetAgent(i).AgentId} was not told it was home time.");
                    Assert.That(errand.CauseEventId, Is.EqualTo(cue));
                    Assert.That(errand.StartTick, Is.GreaterThan(5), "Nobody reacts on the tick a thing happens.");
                    startTicks.Add(errand.StartTick);
                }

                Assert.That(startTicks.Count, Is.GreaterThanOrEqualTo(5),
                    "People take it up each their own while later, never the whole building at once.");
            }
        }

        [Test]
        public void ATimetableEntry_NamingARoomTheBuildingLacks_IsRefused()
        {
            ScenarioData data = CalmDay();
            data.Timetable = new[] { new ScheduledCue(CueKind.MeetingEnds, 10, 0, new SimulationId(9999UL)) };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }

        [Test]
        public void ATimetable_CannotScheduleSomebodysOwnIdea()
        {
            ScenarioData data = CalmDay();
            data.Timetable = new[] { new ScheduledCue(CueKind.Chat, 10, 0) };
            Assert.Throws<InvalidOperationException>(() => data.Validate());

            data.Timetable = new[] { new ScheduledCue(CueKind.HomeTime, 10, 0, PrototypeBuilding.Office) };
            Assert.Throws<InvalidOperationException>(() => data.Validate(), "Home time is for the whole building, not a room.");
        }

        [Test]
        public void AHomeThatIsNotAChair_OrIsSomebodyElses_IsRefused()
        {
            ScenarioData data = CalmDay();
            data.Agents[0] = data.Agents[0].WithHome(new SimulationId(3001UL)); // a box
            Assert.Throws<InvalidOperationException>(() => data.Validate());

            data = CalmDay();
            data.Agents[0] = data.Agents[0].WithHome(data.Agents[1].HomeObjectId);
            Assert.Throws<InvalidOperationException>(() => data.Validate(), "One person to a chair.");

            data = CalmDay();
            data.Agents[0] = data.Agents[0].WithHome(new LogicalPosition(50000, 50000));
            Assert.Throws<InvalidOperationException>(() => data.Validate(), "A spot has to be inside a room.");
        }

        [Test]
        public void EveryNewEventType_HasALineInTheStory()
        {
            // A cue and a remark read back as words, not as an enum's name.
            using (var simulation = new Run(CalmDay()))
            {
                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                var chat = new CausalEvent(1UL, 10, new SimulationId(1001UL), CausalEventType.CueCalled, default,
                    (int)CueKind.Chat, 0, 0UL, new SimulationId(1002UL));
                Assert.That(story.Describe(chat), Does.Contain("had a chat"));
                var said = new CausalEvent(2UL, 11, new SimulationId(1001UL), CausalEventType.AgentSaid, default, 2500, 0, 1UL);
                Assert.That(story.Describe(said), Does.Contain("said"));
                Assert.That(Paniq.Presentation.EventStory.IsBackground(CausalEventType.AgentSaid), Is.True, "Remarks are chatter.");
                var day = new CausalEvent(3UL, 12, default, CausalEventType.PowerCalledHomeTime, default, 0, 0, 0UL);
                Assert.That(story.Describe(day), Does.Contain("you"));
            }
        }
    }
}

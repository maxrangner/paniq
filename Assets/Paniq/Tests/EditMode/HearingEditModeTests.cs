using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// What a calm person hears and what they do about it: a person keeps
    /// more than one noise in their head, a fire's crackle beats a thud, a
    /// fire is heard further as it grows, and somebody who hears a threat's
    /// noise from another room and sees nothing goes to look. Without the
    /// last, a bathroom ablaze was heard by eighteen people who sat on at
    /// their desks until it reached them (seeds 40 and 42, 2026-09-24).
    /// </summary>
    public sealed class HearingEditModeTests
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

        private ScenarioData Quiet(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Timetable = new ScheduledCue[0];
            data.Day.ToiletEveryTicks = 0;
            data.Fire.ActivationTick = 3;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            return data;
        }

        private static AgentDefinition Person(ulong id, int x, int z, CardinalDirection facing, int nervousness = 5)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(5, 5, 5, 5, 2, nervousness, 5));
        }

        private static List<CausalEvent> EventsOf(Run simulation, CausalEventType type, ulong who = 0UL)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type && (who == 0UL || record.SourceId.Value == who))
                {
                    found.Add(record);
                }
            }

            return found;
        }

        /// <summary>
        /// Looking toward a thud three metres north, a fire starts three
        /// metres east, out of the corner of their eye but well within
        /// earshot. The crackle takes over at once: they turn to it, see it,
        /// and take fright. They used to stare at the thud for up to two and
        /// a half seconds first, because a person could only hold one noise.
        /// </summary>
        [Test]
        public void TurnedToAThud_TheyStillTurnToAFireCracklingNearer()
        {
            ScenarioData data = Quiet(Person(1UL, 0, 0, CardinalDirection.North));
            data.Fire.ActivationTick = 40;
            TheBuilding.FireAt(data, new LogicalPosition(3000, -500));
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 5; t++)
                {
                    simulation.Step();
                }

                simulation.MakeANoiseForTests(new LogicalPosition(0, 3000));
                for (int t = 0; t < 15; t++)
                {
                    simulation.Step();
                }

                Agent person = simulation.AgentForTests(0);
                Assert.That(person.Intent.Activity, Is.EqualTo(AgentActivityState.Investigating));
                Assert.That(person.Hearing.SoundPoint.Z, Is.EqualTo(3000), "Looking toward the thud.");

                for (int t = 0; t < 80; t++)
                {
                    simulation.Step();
                }

                List<CausalEvent> noticed = EventsOf(simulation, CausalEventType.AgentNoticedSound, 1UL);
                CausalEvent? crackle = null;
                foreach (CausalEvent record in noticed)
                {
                    if (record.Position.X > 2000)
                    {
                        crackle = record;
                        break;
                    }
                }

                Assert.That(crackle.HasValue, Is.True, "They never heard the fire.");
                Assert.That(crackle.Value.Tick, Is.LessThanOrEqualTo(data.Fire.ActivationTick + 10),
                    "A fire crackling takes over from a thud at once, not when the thud has been stared at long enough.");
                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                    "Having turned to the crackle, they saw the flames.");
            }
        }

        /// <summary>A noise that was set aside is looked at when the first is done with, while it is still fresh.</summary>
        [Test]
        public void ASecondThud_IsLookedAtAfterTheFirst()
        {
            ScenarioData data = Quiet(Person(1UL, 0, 0, CardinalDirection.North));
            data.Fire.ActivationTick = 100000;
            data.Hearing.InvestigateMinimumTicks = 50;
            data.Hearing.InvestigateMaximumTicks = 50;
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 5; t++)
                {
                    simulation.Step();
                }

                simulation.MakeANoiseForTests(new LogicalPosition(0, 3000));
                simulation.Step();
                simulation.MakeANoiseForTests(new LogicalPosition(3000, 0));
                Agent person = simulation.AgentForTests(0);
                Assert.That(person.Hearing.SoundPoint.Z, Is.EqualTo(3000), "Still looking toward the first.");
                Assert.That(person.Hearing.PendingCount, Is.EqualTo(1), "The second waits.");

                for (int t = 0; t < 60; t++)
                {
                    simulation.Step();
                }

                Assert.That(person.Intent.Activity, Is.EqualTo(AgentActivityState.Investigating));
                Assert.That(person.Hearing.SoundPoint.X, Is.EqualTo(3000), "Now looking toward the second.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentNoticedSound, 1UL), Has.Count.EqualTo(2));
            }
        }

        /// <summary>One square of floor crackles; a room ablaze roars, up to a ceiling.</summary>
        [Test]
        public void AFire_IsHeardFurtherAsItGrows()
        {
            ScenarioData data = Quiet(Person(1UL, -5000, -5000, CardinalDirection.South));
            TheBuilding.FireAt(data, new LogicalPosition(2000, 2000));
            data.Fire.SpreadMinimumTicks = 5;
            data.Fire.SpreadMaximumTicks = 10;
            using (var simulation = new Run(data, 7UL))
            {
                IThreat fire = simulation.FireForTests;
                for (int t = 0; t < 10; t++)
                {
                    simulation.Step();
                }

                int small = fire.HeardWithinMillimetres;
                Assert.That(small, Is.EqualTo(data.Hearing.FireHearingRadiusMillimetres + fire.Count * data.Hearing.FireHearingPerCellMillimetres));

                for (int t = 0; t < 600; t++)
                {
                    simulation.Step();
                }

                int big = fire.HeardWithinMillimetres;
                Assert.That(big, Is.GreaterThan(small));
                Assert.That(big, Is.LessThanOrEqualTo(data.Hearing.FireHearingMaximumMillimetres));
            }
        }

        /// <summary>
        /// Deep in the closet with their back to its shut door, a fire roars
        /// in the office beyond it. They hear it, see nothing, and go to
        /// look: to the door, open it, and there it is. The story says so,
        /// and the door they opened says so.
        /// </summary>
        [Test]
        public void HearingAThreatBehindAShutDoor_TheyGoToLook_AndFindIt()
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 4500, CardinalDirection.East));
            TheBuilding.FireAt(data, new LogicalPosition(3000, 2500));
            data.Hearing.FireHearingRadiusMillimetres = 12000;
            data.Hearing.FireHearingMaximumMillimetres = 15000;
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 15 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                List<CausalEvent> cues = EventsOf(simulation, CausalEventType.CueCalled, 1UL);
                bool wentToLook = false;
                foreach (CausalEvent record in cues)
                {
                    wentToLook |= (CueKind)record.Strength == CueKind.GoAndLook;
                }

                Assert.That(wentToLook, Is.True, "They heard a fire roaring next door and went to see.");
                Assert.That(EventsOf(simulation, CausalEventType.DoorOpened, TheBuilding.ClosetDoor.Value), Is.Not.Empty,
                    "They opened the door to look.");
                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm), "And there it was.");
            }
        }

        /// <summary>The very nervous do not go and look. They stay put and keep glancing.</summary>
        [Test]
        public void TheVeryNervous_DoNotGoToLook()
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 4500, CardinalDirection.East, nervousness: 9));
            TheBuilding.FireAt(data, new LogicalPosition(3000, 2500));
            data.Hearing.FireHearingRadiusMillimetres = 12000;
            data.Hearing.FireHearingMaximumMillimetres = 15000;
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 15 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                foreach (CausalEvent record in EventsOf(simulation, CausalEventType.CueCalled, 1UL))
                {
                    Assert.That((CueKind)record.Strength, Is.Not.EqualTo(CueKind.GoAndLook));
                }

                Assert.That(EventsOf(simulation, CausalEventType.DoorOpened, TheBuilding.ClosetDoor.Value), Is.Empty);
                Assert.That(EventsOf(simulation, CausalEventType.AgentNoticedSound, 1UL), Is.Not.Empty, "They heard it, though.");
            }
        }
    }
}

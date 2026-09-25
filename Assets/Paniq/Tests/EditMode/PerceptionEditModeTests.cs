using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// What a calm person sees, and what it does to them: a threat twelve
    /// metres off in their own room or through an open doorway along their
    /// line of sight, and somebody bolting, which startles them -- or, if
    /// they are brave, makes them look. Fear spreading by sight, and the
    /// first shout coming with the first stride, are what let a meeting rise
    /// in a ragged wave rather than one seat at a time (the owner's second
    /// playtest, 2026-09-24).
    /// </summary>
    public sealed class PerceptionEditModeTests
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

        /// <summary>
        /// A quiet building: these people and nothing else in it, no day, a
        /// fire that starts at once and never spreads, nobody who freezes,
        /// and no reaction delay beyond the lag everybody has.
        /// </summary>
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

        private static AgentDefinition Person(ulong id, int x, int z, CardinalDirection facing, int bravery = 5, int nervousness = 5)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(5, 5, bravery, 5, 2, nervousness, 5));
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

        // ------------------------------------------------------------- sight

        /// <summary>
        /// In the closet, a metre inside its door and facing it; the fire
        /// seven metres off across the office, dead ahead through the doorway.
        /// Seen when the door stands open, hidden when it is shut. (It used
        /// to be three metres, so this fire was invisible either way.)
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void FlamesBeyondADoorway_AreSeenOnlyWhenItStandsOpen(bool doorOpen)
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 2500, CardinalDirection.West));
            TheBuilding.FireAt(data, new LogicalPosition(3000, 2500));
            using (var simulation = new Run(data, 7UL))
            {
                if (doorOpen)
                {
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                }

                for (int t = 0; t < 60; t++)
                {
                    simulation.Step();
                }

                AgentSnapshot person = simulation.GetAgent(0);
                if (doorOpen)
                {
                    Assert.That(person.FearState, Is.Not.EqualTo(AgentFearState.Calm),
                        "Seven metres through an open doorway is in plain sight.");
                    Assert.That(person.AlertSource, Is.EqualTo(AgentAlertSource.Visual));
                }
                else
                {
                    Assert.That(person.FearState, Is.EqualTo(AgentFearState.Calm), "A shut door hides the flames.");
                }
            }
        }

        /// <summary>
        /// The doorway has to lie on the line of sight. Somebody in the closet
        /// with the door open but standing well to one side of it, facing the
        /// wall the door is in, has the wall between them and the flames.
        /// </summary>
        [Test]
        public void AnOpenDoorway_OffTheLineOfSight_HidesTheFlamesLikeAWall()
        {
            // At the far north end of the closet, facing west: the office is
            // beyond that wall, and the open door is 2.5 m south of the line
            // they look along.
            ScenarioData data = Quiet(Person(1UL, 7000, 5500, CardinalDirection.West));
            TheBuilding.FireAt(data, new LogicalPosition(3000, 5500));
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                for (int t = 0; t < 30; t++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm),
                    "The flames are behind the wall beside the door, not through the door.");
            }
        }

        // -------------------------------------------------------- somebody bolts

        /// <summary>
        /// Four people in the office and nothing to hear (yells reach a
        /// millimetre). One is frightened by hand and runs. The two ordinary
        /// people facing them are startled by the sight, each at a tick of
        /// their own; the brave one facing them looks up instead.
        /// </summary>
        [Test]
        public void SeeingSomeoneBolt_StartlesTheOnesWhoSawIt_EachInTheirOwnTime()
        {
            ScenarioData data = Quiet(
                Person(1UL, 0, 0, CardinalDirection.North),
                Person(2UL, 0, 4000, CardinalDirection.South),
                Person(3UL, 1500, 4000, CardinalDirection.South, bravery: 9),
                Person(4UL, -1500, 4000, CardinalDirection.South));

            // The fire in the office's far corner, behind everybody who is
            // watching, so only the runner can be what startles them.
            TheBuilding.FireAt(data, new LogicalPosition(-5500, 5500));
            data.Hearing.YellAlarmRadiusMillimetres = 1;
            data.Hearing.YellHearingRadiusMillimetres = 1;
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 5; t++)
                {
                    simulation.Step();
                }

                simulation.FrightenForTests(0);
                for (int t = 0; t < 60; t++)
                {
                    simulation.Step();
                }

                AgentSnapshot watcher = simulation.GetAgent(1);
                AgentSnapshot brave = simulation.GetAgent(2);
                AgentSnapshot other = simulation.GetAgent(3);
                Assert.That(watcher.FearState, Is.Not.EqualTo(AgentFearState.Calm), "Person 2 saw person 1 bolt.");
                Assert.That(other.FearState, Is.Not.EqualTo(AgentFearState.Calm), "Person 4 saw person 1 bolt.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentAlerted, 2UL), Is.Not.Empty);
                Assert.That(simulation.AgentForTests(1).Fear.AlertSource == AgentAlertSource.SawSomeoneRun ||
                            watcher.AlertSource == AgentAlertSource.SawSomeoneRun || watcher.AlertSource == AgentAlertSource.Visual,
                    Is.True, "Startled by the sight of somebody running, not by a bump or a bell.");
                Assert.That(brave.FearState, Is.EqualTo(AgentFearState.Calm), "The brave look before they run.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentNoticedSound, 3UL), Is.Not.Empty,
                    "The brave one turned to look at the runner.");
                Assert.That(simulation.AgentForTests(1).Fear.ReactionEndTick,
                    Is.Not.EqualTo(simulation.AgentForTests(3).Fear.ReactionEndTick),
                    "Two people startled by the same sight finish being startled on ticks of their own.");
            }
        }

        /// <summary>
        /// The shipped meeting, with the fire in the office where nobody at
        /// the table can see it. The host is frightened by hand. The table
        /// rises in a wave -- most of them within two seconds, all of them
        /// within three -- and no two on the same tick. It used to go round
        /// the table one seat at a time, seconds apart, because a shout
        /// reached 2.5 m and came two to five seconds after the runner rose.
        /// </summary>
        [Test]
        public void AtTheMeetingTable_TheFrightSpreadsInARaggedWave()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()));
            data.Round.HazardWaitsForTrigger = true;
            ulong[] meeting = { 1009UL, 1010UL, 1011UL, 1012UL, 1013UL, 1014UL };
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 1);
                for (int t = 0; t < 20; t++)
                {
                    simulation.Step();
                }

                int host = -1;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    if (simulation.GetAgent(i).AgentId.Value == 1014UL)
                    {
                        host = i;
                    }
                }

                int frightTick = simulation.Tick;
                simulation.FrightenForTests(host);
                int withinTwoSeconds = -1;
                for (int t = 0; t < 150; t++)
                {
                    simulation.Step();
                    if (simulation.Tick == frightTick + 100)
                    {
                        withinTwoSeconds = CountFrightened(simulation, meeting);
                    }
                }

                Assert.That(withinTwoSeconds, Is.GreaterThanOrEqualTo(4), "Most of the table is up within two seconds.");
                Assert.That(CountFrightened(simulation, meeting), Is.EqualTo(6), "The whole table within three.");

                var scaredTicks = new HashSet<int>();
                foreach (ulong id in meeting)
                {
                    List<CausalEvent> scared = EventsOf(simulation, CausalEventType.AgentScared, id);
                    Assert.That(scared, Is.Not.Empty, $"Person {id} never took fright.");
                    Assert.That(scaredTicks.Add(scared[0].Tick), Is.True,
                        $"Person {id} took fright on tick {scared[0].Tick}, the same tick as somebody else.");
                }
            }
        }

        private static int CountFrightened(Run simulation, ulong[] ids)
        {
            int count = 0;
            foreach (ulong id in ids)
            {
                if (simulation.GetAgent(new SimulationId(id)).FearState != AgentFearState.Calm)
                {
                    count++;
                }
            }

            return count;
        }

        // ------------------------------------------------------------ shouting

        /// <summary>
        /// Somebody frightened shouts with their first stride, a few ticks
        /// late like every reaction, rather than two to five seconds in.
        /// </summary>
        [Test]
        public void TheFirstShout_ComesWithTheFirstStride()
        {
            ScenarioData data = Quiet(Person(1UL, 0, 0, CardinalDirection.North));
            TheBuilding.FireAt(data, new LogicalPosition(-5500, -5500));
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 5; t++)
                {
                    simulation.Step();
                }

                int frightTick = simulation.Tick;
                simulation.FrightenForTests(0);
                for (int t = 0; t < 40; t++)
                {
                    simulation.Step();
                }

                List<CausalEvent> yells = EventsOf(simulation, CausalEventType.AgentYelled, 1UL);
                Assert.That(yells, Is.Not.Empty, "Nobody who is frightened stays quiet for long.");
                int lag = yells[0].Tick - frightTick;
                Assert.That(lag, Is.GreaterThanOrEqualTo(data.Perception.ReactionLagMinimumTicks), "Never on the tick itself.");
                Assert.That(lag, Is.LessThanOrEqualTo(data.Perception.ReactionLagMaximumTicks + 1),
                    "The first shout comes with the first stride.");
            }
        }
    }
}

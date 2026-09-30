using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The crowd switch (2026-09-30), a button on the test levels. The
    /// owner's rule: "toggle button in UI, calm or panicked, toggling should
    /// set their states." Flicked to panicked, everybody takes fright, each
    /// a few ticks after the next, and stays frightened until it is flicked
    /// back; flicked to calm, everybody settles one at a time and the
    /// ordinary rules take over again. It is two player commands, so a
    /// replay carries it, and it goes through the same startle and settle
    /// as a bell does, so nothing happens on the tick it is pressed and
    /// nothing happens to two people on one tick.
    /// </summary>
    public sealed class CrowdSwitchEditModeTests
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
        /// The square room with every door locked and nobody able to batter
        /// one down, so the whole crowd stays inside for the whole test,
        /// and nobody trips: what is being watched is fear, not the doors.
        /// </summary>
        private ScenarioData ALockedSquare()
        {
            ScenarioData data = TestBuildings.SquareRoom(scenario.ToRuntimeData());
            var doors = new DoorDefinition[data.Doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                DoorDefinition door = data.Doors[i];
                doors[i] = new DoorDefinition(door.DoorId, door.RoomId, door.Side, door.CentreAlongWallMillimetres,
                    door.WidthMillimetres, startsLocked: true);
            }

            data.Doors = doors;
            data.Exits.DoorForceChancePercent = 0;
            data.Falls.TripChancePercent = 0;
            return data;
        }

        private static List<CausalEvent> EventsOfType(Run simulation, CausalEventType type)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        private static void Step(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void FlickedToPanicked_EverybodyTakesFright_NobodyOnTheTickItWasPressed_AndNoTwoOnOneTick()
        {
            ScenarioData data = TestBuildings.SquareRoom(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 11UL))
            {
                const int pressed = 10;
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), pressed);
                Step(simulation, pressed);

                // The press has gone through, and nobody is frightened yet:
                // every startle ends a few ticks late, on a tick of its own.
                Assert.That(simulation.EventLog.Events, Has.Some.Property("EventType").EqualTo(CausalEventType.PowerPanickedCrowd));
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    Assert.That(simulation.GetAgent(i).FearState, Is.Not.EqualTo(AgentFearState.Scared),
                        $"Person {i} panicked on the very tick the switch was pressed.");
                }

                var ends = new HashSet<int>();
                List<CausalEvent> alerts = EventsOfType(simulation, CausalEventType.AgentAlerted);
                Assert.That(alerts, Has.Count.EqualTo(40), "Everybody was startled by the press.");
                foreach (CausalEvent alert in alerts)
                {
                    int end = alert.Tick + alert.DurationTicks;
                    Assert.That(end, Is.GreaterThan(pressed), "A reaction begins after the press, never on it.");
                    Assert.That(ends.Add(end), $"Two people finish being startled on tick {end}.");
                }

                Step(simulation, 5 * Run.TicksPerSecond);
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot person = simulation.GetAgent(i);
                    if (person.Participation == AgentParticipation.Participating)
                    {
                        Assert.That(person.FearState, Is.EqualTo(AgentFearState.Scared), $"Person {i} is still not frightened five seconds on.");
                    }
                }

                RunSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.FireActive, Is.False, "The switch frightens; it lights nothing.");
                Assert.That(snapshot.CrowdHeldPanicked, Is.True);
                Assert.That(snapshot.RoundPhase, Is.EqualTo(RoundPhase.Running), "A crowd set off is a round begun.");
                Assert.That(snapshot.HazardRequested, Is.False, "But no hazard was asked for.");
            }
        }

        [Test]
        public void HeldAtPanicked_NobodySettles_HoweverQuietItGets()
        {
            ScenarioData data = ALockedSquare();

            // Calming tuned so that, left to the ordinary rules, the room
            // would settle within about ten seconds of the fright.
            data.Calming.QuietTicks = 50;
            data.Calming.DrainBasePerMillePerSecond = 200;
            data.Calming.FloorPerNervousness = 0;
            using (var simulation = new Run(data, 11UL))
            {
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), 5);
                Step(simulation, 30 * Run.TicksPerSecond);

                int inside = 0;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot person = simulation.GetAgent(i);
                    if (person.Participation != AgentParticipation.Participating)
                    {
                        continue;
                    }

                    inside++;
                    Assert.That(person.FearState, Is.Not.EqualTo(AgentFearState.Calm),
                        $"Person {i} settled while the switch held the crowd panicked.");
                }

                Assert.That(inside, Is.EqualTo(40), "Every door is locked and nobody batters one, so nobody left.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentCalmedDown), Is.Empty);
            }
        }

        [Test]
        public void FlickedBackToCalm_EverybodySettles_OneAtATime_AndNotOnTheTickItWasPressed()
        {
            ScenarioData data = ALockedSquare();
            using (var simulation = new Run(data, 11UL))
            {
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), 5);
                Step(simulation, 5 * Run.TicksPerSecond);

                const int pressed = 300;
                simulation.QueueCommand(PlayerCommandType.SetCrowdCalm, default(SimulationId), pressed);
                Step(simulation, pressed - 5 * Run.TicksPerSecond);
                Assert.That(simulation.EventLog.Events, Has.Some.Property("EventType").EqualTo(CausalEventType.PowerCalmedCrowd));
                Assert.That(EventsOfType(simulation, CausalEventType.AgentCalmedDown), Is.Empty,
                    "Nobody settles on the very tick the switch was pressed.");

                Step(simulation, 20 * Run.TicksPerSecond);
                Assert.That(simulation.GetSnapshot().CrowdHeldPanicked, Is.False);
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot person = simulation.GetAgent(i);
                    if (person.Participation == AgentParticipation.Participating && !person.IsDown)
                    {
                        Assert.That(person.FearState, Is.EqualTo(AgentFearState.Calm),
                            $"Person {i} is still frightened twenty seconds after the crowd was calmed.");
                    }
                }

                var ticks = new HashSet<int>();
                List<CausalEvent> settled = EventsOfType(simulation, CausalEventType.AgentCalmedDown);
                Assert.That(settled, Is.Not.Empty);
                foreach (CausalEvent calmed in settled)
                {
                    Assert.That(calmed.Tick, Is.GreaterThan(pressed));
                    Assert.That(ticks.Add(calmed.Tick), $"Two people settled on tick {calmed.Tick}.");
                    Assert.That(simulation.EventLog.Get(calmed.CausalParentEventId).EventType,
                        Is.EqualTo(CausalEventType.PowerCalmedCrowd), "Settling is blamed on the switch.");
                }
            }
        }

        [Test]
        public void FlickedToCalm_TheBellsFallSilentToo()
        {
            ScenarioData data = TestBuildings.SquareRoom(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 11UL))
            {
                simulation.QueueCommand(PlayerCommandType.PullAlarm, data.Alarms[0].AlarmId, 5);
                Step(simulation, 20);
                Assert.That(simulation.AlarmsRinging, Is.True, "The pull station rings the bells.");

                simulation.QueueCommand(PlayerCommandType.SetCrowdCalm, default(SimulationId), 30);
                Step(simulation, 20);
                Assert.That(simulation.AlarmsRinging, Is.False, "Bells left ringing would frighten everybody straight back.");
                Assert.That(EventsOfType(simulation, CausalEventType.AllClear), Is.Not.Empty);
            }
        }

        /// <summary>
        /// Calm is not a hold: once the crowd has been calmed, the ordinary
        /// rules decide who takes fright, so a fire lit afterwards frightens
        /// people as it always did -- and the trigger still lights it in a
        /// round the switch began.
        /// </summary>
        [Test]
        public void AfterCalm_AFireFrightensPeopleAgain_AndTheTriggerStillLightsOne()
        {
            ScenarioData data = TestBuildings.InteractionRoom(scenario.ToRuntimeData());
            data.Falls.TripChancePercent = 0;
            using (var simulation = new Run(data, 11UL))
            {
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), 5);
                Step(simulation, 3 * Run.TicksPerSecond);
                simulation.QueueCommand(PlayerCommandType.SetCrowdCalm, default(SimulationId), simulation.Tick + 1);
                Step(simulation, 15 * Run.TicksPerSecond);
                Assert.That(simulation.GetSnapshot().RoundPhase, Is.EqualTo(RoundPhase.Running));
                Assert.That(simulation.GetSnapshot().FireActive, Is.False);

                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                Step(simulation, 10 * Run.TicksPerSecond);
                Assert.That(simulation.GetSnapshot().FireActive, Is.True, "The trigger lights the fire in a round already begun.");

                int frightened = 0;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    frightened += simulation.GetAgent(i).FearState != AgentFearState.Calm ? 1 : 0;
                }

                Assert.That(frightened, Is.GreaterThan(0), "A fire in the middle of the room frightens somebody.");
            }
        }

        /// <summary>The office never sees the switch: nothing about it moves while the switch is never pressed.</summary>
        [Test]
        public void TheOffice_IsNotHeldByASwitchNobodyPressed()
        {
            using (var simulation = new Run(scenario.ToRuntimeData(), 42UL))
            {
                Step(simulation, 10);
                Assert.That(simulation.GetSnapshot().CrowdHeldPanicked, Is.False);
                Assert.That(simulation.FearForTests.HoldsPanicked, Is.False);
            }
        }
    }
}

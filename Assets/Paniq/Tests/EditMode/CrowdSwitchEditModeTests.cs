using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The Crowd button on a test level (2026-10-01): one press sets the
    /// whole crowd panicking, the next calms them down again. It is two
    /// replayed commands, and it obeys the owner's standing rule that nothing
    /// happens to a whole group on one tick: everybody is startled on a tick
    /// of their own, and settles on a tick of their own.
    /// </summary>
    public sealed class CrowdSwitchEditModeTests
    {
        private static readonly AgentTraitValues Hero = new AgentTraitValues(5, 5, 10, 5, 2, 1);
        private static readonly AgentTraitValues Worrier = new AgentTraitValues(5, 5, 3, 5, 2, 7);

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

        /// <summary>The office with these people standing in it, nothing burning, nothing on the timetable, nobody freezing or tripping.</summary>
        private ScenarioData Office(params AgentTraitValues[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            var agents = new AgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                agents[i] = new AgentDefinition(new SimulationId((ulong)(i + 1)),
                    new LogicalPosition(-4000 + i * 1500, -4000), CardinalDirection.North, people[i]);
            }

            data.Agents = agents;
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = true;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Falls.TripChancePercent = 0;
            return data;
        }

        private static AgentTraitValues[] Six() => new[] { Hero, Worrier, AgentTraitValues.AllOrdinary, Hero, Worrier, AgentTraitValues.AllOrdinary };

        private static List<CausalEvent> EventsOfType(Run simulation, CausalEventType type) =>
            simulation.EventLog.Events.Where(e => e.EventType == type).ToList();

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        private static void Flick(Run simulation, PlayerCommandType which)
        {
            simulation.QueueCommand(which, default(SimulationId), simulation.Tick + 1);
            simulation.Step();
        }

        [Test]
        public void CrowdPanicked_StartlesEverybody_EachOnTheirOwnTick_AndNobodyOnThePress()
        {
            using (var simulation = new Run(Office(Six()), 42UL))
            {
                Advance(simulation, 5);
                int pressed = simulation.Tick + 1;
                Flick(simulation, PlayerCommandType.SetCrowdPanicked);

                List<CausalEvent> flicks = EventsOfType(simulation, CausalEventType.PowerPanickedCrowd);
                Assert.That(flicks, Has.Count.EqualTo(1));
                Assert.That(simulation.FearForTests.HoldsPanicked, Is.True);
                Assert.That(simulation.GetSnapshot().CrowdHeldPanicked, Is.True);

                var ends = new HashSet<int>();
                for (int i = 0; i < 6; i++)
                {
                    Agent agent = simulation.AgentForTests(i);
                    Assert.That(agent.Fear.State, Is.Not.EqualTo(AgentFearState.Calm), $"Person {i} was startled by the switch.");
                    Assert.That(agent.Fear.ReactionEndTick, Is.GreaterThan(pressed), $"Person {i} finishes being startled after the press, never on it.");
                    ends.Add(agent.Fear.ReactionEndTick);
                }

                Assert.That(ends, Has.Count.EqualTo(6), "No two people finish being startled on one tick.");

                List<CausalEvent> alerted = EventsOfType(simulation, CausalEventType.AgentAlerted);
                Assert.That(alerted, Has.Count.EqualTo(6));
                Assert.That(alerted.All(e => e.CausalParentEventId == flicks[0].EventId), "Every fright is blamed on the switch.");
            }
        }

        [Test]
        public void CrowdPanicked_IsAHold_NobodySettlesWhileItStands()
        {
            using (var simulation = new Run(Office(Hero), 42UL))
            {
                Advance(simulation, 5);
                Flick(simulation, PlayerCommandType.SetCrowdPanicked);

                // A hero with nothing to fear settles in about ten seconds on
                // their own (CalmingDownEditModeTests). Held, they never do.
                Advance(simulation, 30 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm));
                Assert.That(EventsOfType(simulation, CausalEventType.AgentCalmedDown), Is.Empty);
            }
        }

        [Test]
        public void CrowdCalm_SettlesEverybody_EachOnTheirOwnTick_AndNobodyOnThePress()
        {
            using (var simulation = new Run(Office(Six()), 42UL))
            {
                Advance(simulation, 5);
                Flick(simulation, PlayerCommandType.SetCrowdPanicked);
                Advance(simulation, 3 * Run.TicksPerSecond);
                int pressed = simulation.Tick + 1;
                Flick(simulation, PlayerCommandType.SetCrowdCalm);

                List<CausalEvent> flicks = EventsOfType(simulation, CausalEventType.PowerCalmedCrowd);
                Assert.That(flicks, Has.Count.EqualTo(1));
                Assert.That(simulation.FearForTests.HoldsPanicked, Is.False);

                var ends = new HashSet<int>();
                for (int i = 0; i < 6; i++)
                {
                    Agent agent = simulation.AgentForTests(i);
                    Assert.That(agent.Fear.CalmOrdered, Is.True, $"Person {i} was told to settle.");
                    Assert.That(agent.Fear.CalmsAtTick, Is.GreaterThan(pressed), $"Person {i} settles after the press, never on it.");
                    ends.Add(agent.Fear.CalmsAtTick);
                }

                Assert.That(ends, Has.Count.EqualTo(6), "No two people settle on one tick.");

                // Somebody mid-way through a door finishes with it first and
                // settles after, so give them a few seconds. (Whoever settles
                // first may be alarmed again by a neighbour still shouting:
                // that is the ordinary rules, back in force, not the switch.)
                for (int t = 0; t < 15 * Run.TicksPerSecond && EventsOfType(simulation, CausalEventType.AgentCalmedDown).Count < 6; t++)
                {
                    simulation.Step();
                }

                List<CausalEvent> calmed = EventsOfType(simulation, CausalEventType.AgentCalmedDown);
                Assert.That(calmed.Select(e => e.SourceId).Distinct().Count(), Is.EqualTo(6), "Everybody settled once.");
                Assert.That(calmed.Select(e => e.Tick).Distinct().Count(), Is.EqualTo(6), "Each on a tick of their own.");
                Assert.That(calmed.All(e => e.Tick > pressed), "Nobody on the tick of the press.");
                Assert.That(calmed.All(e => e.CausalParentEventId == flicks[0].EventId), "Every settling is blamed on the switch.");
                for (int i = 0; i < 6; i++)
                {
                    Assert.That(simulation.AgentForTests(i).Fear.CalmOrdered, Is.False, $"Person {i}'s order is spent once they settled.");
                }
            }
        }

        [Test]
        public void CrowdCalm_SilencesTheBells()
        {
            ScenarioData data = Office(Six());
            data.Alarm.PlayerMayPull = true;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                simulation.QueueCommand(PlayerCommandType.PullAlarm, TheBuilding.TheAlarm, simulation.Tick + 1);
                simulation.Step();
                Assert.That(simulation.AlarmsRinging, Is.True, "The player pulled the alarm.");

                Flick(simulation, PlayerCommandType.SetCrowdCalm);
                Assert.That(simulation.AlarmsRinging, Is.False, "Calm means quiet: the bells stop with the switch.");
            }
        }

        [Test]
        public void AfterCrowdCalm_TheOrdinaryRulesReturn()
        {
            using (var simulation = new Run(Office(Worrier), 42UL))
            {
                Advance(simulation, 5);
                Flick(simulation, PlayerCommandType.SetCrowdPanicked);
                Advance(simulation, Run.TicksPerSecond);
                Flick(simulation, PlayerCommandType.SetCrowdCalm);
                Advance(simulation, 3 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm));

                // Frightened by something real afterwards, a worrier stays
                // frightened for a good while: no leftover order settles them.
                simulation.FrightenForTests(0);
                Advance(simulation, 4 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Scared));
                Assert.That(simulation.AgentForTests(0).Fear.CalmOrdered, Is.False);
            }
        }

        [Test]
        public void TheTrigger_StillLightsTheFire_InARoundTheSwitchBegan()
        {
            using (var simulation = new Run(Office(Six()), 42UL))
            {
                Advance(simulation, 5);
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.BeforeEvent));
                Flick(simulation, PlayerCommandType.SetCrowdPanicked);
                RunSnapshot afterTheSwitch = simulation.GetSnapshot();
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Running), "The switch begins the round, so an emptied level ends with a score.");
                Assert.That(afterTheSwitch.EventTriggered, Is.True);
                Assert.That(afterTheSwitch.HazardRequested, Is.False, "But nothing burns yet.");

                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                Advance(simulation, 3);
                Assert.That(simulation.GetSnapshot().HazardRequested, Is.True, "The red button still lights the fire in a round the switch began.");
                Assert.That(simulation.FireForTests.StartRequested, Is.True);

                List<CausalEvent> triggered = EventsOfType(simulation, CausalEventType.RoundEventTriggered);
                Assert.That(triggered, Has.Count.EqualTo(1), "One round, begun once.");
                Assert.That(triggered[0].CausalParentEventId,
                    Is.EqualTo(EventsOfType(simulation, CausalEventType.PowerPanickedCrowd)[0].EventId),
                    "The round began with the switch, and the log says so.");
            }
        }
    }
}

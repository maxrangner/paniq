using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Tells (2026-09-30, the owner: "the visible agent tells"): a second or
    /// so of visible wind-up before somebody freezes, dashes through the heat
    /// or heads back toward the flames, in which one click saves them. And the
    /// end card's tally of how much the player used their hand (the owner:
    /// "the stat at the end about how many clicks/influence you used").
    /// </summary>
    public sealed class TellsEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);
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

        private static List<CausalEvent> EventsOf(Run simulation, CausalEventType type)
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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>Steps until the event turns up, at most <paramref name="limit"/> ticks; the event, or null.</summary>
        private static CausalEvent? AdvanceUntil(Run simulation, Predicate<CausalEvent> wanted, int limit)
        {
            for (int t = 0; t <= limit; t++)
            {
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (wanted(record))
                    {
                        return record;
                    }
                }

                if (t < limit)
                {
                    simulation.Step();
                }
            }

            return null;
        }

        /// <summary>
        /// The office with one person in it, nothing burning and nothing on the
        /// timetable, and everybody who takes fright freezing and then running.
        /// </summary>
        private ScenarioData OfficeWithAFreezer()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(-3000, -3000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Keycard.Enabled = false;
            data.Day.ToiletEveryTicks = 0;
            data.Calming.Enabled = false;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 100;
            data.Temperament.FreezeMinimumTicks = 1000;
            data.Temperament.FreezeMaximumTicks = 1000;
            return data;
        }

        /// <summary>
        /// Going stiff: somebody about to freeze shivers first, for about a
        /// second, never on the tick they took fright -- and then is frozen as
        /// before, when nobody catches it.
        /// </summary>
        [Test]
        public void SomebodyAboutToFreeze_GoesStiffFirst_ThenFreezes()
        {
            using (var simulation = new Run(OfficeWithAFreezer(), 42UL))
            {
                Advance(simulation, 10);
                simulation.FrightenForTests(0);
                Agent agent = simulation.AgentForTests(0);
                Assert.That(agent.Intent.Tell, Is.EqualTo(AgentTell.GoingStiff), "The shiver comes first.");
                Assert.That(simulation.GetAgent(Somebody).ActivityState, Is.EqualTo(AgentActivityState.Frozen));
                List<CausalEvent> began = EventsOf(simulation, CausalEventType.AgentBeganATell);
                Assert.That(began, Has.Count.EqualTo(1));
                Assert.That(began[0].DurationTicks, Is.InRange(40, 80), "About a second and a fifth.");

                Advance(simulation, 20);
                Assert.That(simulation.GetAgent(Somebody).TellProgress, Is.InRange(1, 999), "The ring is closing.");

                Advance(simulation, 80);
                Assert.That(agent.Intent.Tell, Is.EqualTo(AgentTell.None), "The wind-up has run out.");
                Assert.That(simulation.GetAgent(Somebody).ActivityState, Is.EqualTo(AgentActivityState.Frozen),
                    "Nobody caught it: frozen, as before.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentCaughtInTime), Is.Empty);
            }
        }

        /// <summary>One poke while they go stiff, and they run instead of freezing; the log names the poke.</summary>
        [Test]
        public void OnePoke_WhileTheyGoStiff_AndTheyRunInstead()
        {
            using (var simulation = new Run(OfficeWithAFreezer(), 42UL))
            {
                Advance(simulation, 10);
                simulation.FrightenForTests(0);
                Advance(simulation, 10);
                simulation.QueueCommand(PlayerCommandType.NudgePersonFrom, Somebody, new LogicalPosition(-3500, -3000),
                    simulation.Tick + 1);
                CausalEvent? caught = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentCaughtInTime, 60);
                Assert.That(caught.HasValue, "Caught in time.");
                CausalEvent poke = EventsOf(simulation, CausalEventType.PowerNudged)[0];
                Assert.That(caught.Value.CausalParentEventId, Is.EqualTo(poke.EventId), "The poke did it.");
                Assert.That(caught.Value.Tick, Is.GreaterThan(poke.Tick), "A beat later, never on the tick (the owner's rule).");
                Advance(simulation, 5);
                Assert.That(simulation.GetAgent(Somebody).ActivityState, Is.Not.EqualTo(AgentActivityState.Frozen),
                    "They run instead of freezing.");
            }
        }

        /// <summary>
        /// Somebody brave in the closet with the fire in the office beyond its
        /// open door (the cornered tests' scene): they gather their nerve
        /// before they dash, and a tug in it calls the dash off.
        /// </summary>
        private ScenarioData ClosetWithTheFireBeyond()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(7000, 2500), CardinalDirection.West,
                    new AgentTraitValues(5, 5, 8, 5, 2, 5, 5))
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Timetable = new ScheduledCue[0];
            data.Keycard.Enabled = false;
            data.Day.ToiletEveryTicks = 0;
            data.Fire.ActivationTick = 3;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Panic.DangerDistanceMillimetres = 2500;
            TheBuilding.FireAt(data, new LogicalPosition(4750, 3250));
            return data;
        }

        [Test]
        public void TheBrave_GatherTheirNerve_BeforeTheyDashThroughTheHeat()
        {
            using (var simulation = new Run(ClosetWithTheFireBeyond(), 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                CausalEvent? dash = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentDashedThroughHeat,
                    12 * Run.TicksPerSecond);
                Assert.That(dash.HasValue, "They still run for it.");
                CausalEvent? nerve = EventsOf(simulation, CausalEventType.AgentBeganATell)
                    .Find(e => (AgentTell)e.Strength == AgentTell.GatheringNerve);
                Assert.That(nerve.HasValue, "Gathering their nerve first.");
                Assert.That(dash.Value.Tick - nerve.Value.Tick, Is.GreaterThanOrEqualTo(nerve.Value.DurationTicks),
                    "The dash comes once the wind-up has run out.");
            }
        }

        [Test]
        public void ATug_WhileTheyGatherTheirNerve_CallsTheDashOff()
        {
            using (var simulation = new Run(ClosetWithTheFireBeyond(), 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                CausalEvent? nerve = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentBeganATell &&
                                                                  (AgentTell)e.Strength == AgentTell.GatheringNerve,
                    12 * Run.TicksPerSecond);
                Assert.That(nerve.HasValue, "They gather their nerve.");
                simulation.QueueCommand(PlayerCommandType.TugPerson, Somebody, simulation.Tick + 1);
                Advance(simulation, 20);
                simulation.QueueCommand(PlayerCommandType.ReleaseTug, Somebody, simulation.Tick + 1);
                Assert.That(EventsOf(simulation, CausalEventType.AgentCaughtInTime), Has.Count.EqualTo(1), "Caught in time.");
                Advance(simulation, 4 * Run.TicksPerSecond);
                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat), Is.Empty,
                    "The dash is off: that door is given up for a while.");
            }
        }

        /// <summary>
        /// The corridor with the fire in it and the building's one pull station
        /// at its west end: somebody brave to the east, whose walk to the station
        /// passes the flames, turns back first; somebody to the west, whose walk
        /// does not, goes at once.
        /// </summary>
        private ScenarioData CorridorWithTheStation(int x)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(x, 7000), CardinalDirection.East,
                    new AgentTraitValues(5, 5, 8, 3, 2, 3, 3))
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Keycard.Enabled = false;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-1800, -1800, 7000, 7000);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Leadership.LeaderMinimum = AgentTraitValues.Maximum + 1;
            return data;
        }

        [Test]
        public void GoingForAStationPastTheFlames_TurnsBackFirst_ButASafeWalkGoesAtOnce()
        {
            using (var simulation = new Run(CorridorWithTheStation(-3000), 42UL))
            {
                CausalEvent? going = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AlarmPulled, 10 * Run.TicksPerSecond);
                Assert.That(going.HasValue, "West of the fire, they go for the station.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentBeganATell).Exists(e => (AgentTell)e.Strength == AgentTell.TurningBack),
                    Is.False, "A walk that keeps clear of the flames earns no tell.");
            }

            using (var simulation = new Run(CorridorWithTheStation(200), 42UL))
            {
                CausalEvent? back = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentBeganATell &&
                                                                 (AgentTell)e.Strength == AgentTell.TurningBack,
                    10 * Run.TicksPerSecond);
                Assert.That(back.HasValue, "East of the fire, the walk to the station passes it: they turn back first.");
            }
        }

        /// <summary>With tells switched off, somebody frightened freezes at once, as before.</summary>
        [Test]
        public void WithTellsOff_TheFreezeComesAtOnce()
        {
            ScenarioData data = OfficeWithAFreezer();
            data.Tells.Enabled = false;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                simulation.FrightenForTests(0);
                Assert.That(simulation.AgentForTests(0).Intent.Tell, Is.EqualTo(AgentTell.None));
                Assert.That(EventsOf(simulation, CausalEventType.AgentBeganATell), Is.Empty);
            }
        }

        /// <summary>
        /// The owner: "the stat at the end about how many clicks/influence you
        /// used this round". Two presses (one a click), a drag of three metres,
        /// a poke and a tug held a second: the tally counts each.
        /// </summary>
        [Test]
        public void TheHandsTally_CountsWhatThePlayerDid()
        {
            ScenarioData data = OfficeWithAFreezer();
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                simulation.QueueCommand(PlayerCommandType.InfluenceSpot, new LogicalPosition(0, -1500), simulation.Tick + 1);
                Advance(simulation, 1);
                simulation.QueueCommand(PlayerCommandType.MoveInfluence, new LogicalPosition(1500, -1500), simulation.Tick + 1);
                Advance(simulation, 5);
                simulation.QueueCommand(PlayerCommandType.MoveInfluence, new LogicalPosition(3000, -1500), simulation.Tick + 1);
                Advance(simulation, 44);
                simulation.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), simulation.Tick + 1);
                Advance(simulation, 5);
                simulation.QueueCommand(PlayerCommandType.InfluenceSpot, new LogicalPosition(0, 0), simulation.Tick + 1);
                simulation.QueueCommand(PlayerCommandType.LeaveInfluence, default(SimulationId), simulation.Tick + 2);
                Advance(simulation, 200);
                simulation.QueueCommand(PlayerCommandType.NudgePersonFrom, Somebody, new LogicalPosition(-3500, -3000),
                    simulation.Tick + 1);
                Advance(simulation, 20);
                simulation.QueueCommand(PlayerCommandType.TugPerson, Somebody, simulation.Tick + 1);
                Advance(simulation, Run.TicksPerSecond);
                simulation.QueueCommand(PlayerCommandType.ReleaseTug, Somebody, simulation.Tick + 1);
                Advance(simulation, 5);

                HandTally tally = HandTally.From(simulation.EventLog.Events, simulation.Commands, simulation.Tick);
                Assert.That(tally.Presses, Is.EqualTo(2));
                Assert.That(tally.Clicks, Is.EqualTo(1));
                Assert.That(tally.Pushes, Is.Zero);
                Assert.That(tally.Pokes, Is.EqualTo(1));
                Assert.That(tally.Tugs, Is.EqualTo(1));
                Assert.That(tally.Actions, Is.EqualTo(4));
                Assert.That(tally.DraggedMillimetres, Is.EqualTo(3000));
                int beacon = data.Influence.BeaconTicks;
                Assert.That(tally.HeldTicks, Is.InRange(50 + beacon + Run.TicksPerSecond - 5, 50 + beacon + Run.TicksPerSecond + 5),
                    "A second held, a click's three seconds, and a second's tug.");
                Assert.That(tally.PerMinute, Is.GreaterThan(0));
            }
        }
    }
}

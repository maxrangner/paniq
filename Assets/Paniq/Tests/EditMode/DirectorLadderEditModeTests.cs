using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The Director's ladder of small incidents (prototype 3, second batch,
    /// 2026-09-26; reordered 2026-09-27). The round opens with an ordinary
    /// day; a waste bin in the meeting room catches fire after half a minute
    /// to a minute and a half (or at once, on the trigger). Doused before
    /// the carpet under it caught, another bin catches a beat later. Since
    /// 2026-10-02 that is the whole ladder (the owner: the socket "should
    /// only pop late in a run", and asked what late means, "only as the
    /// building's counter-move"): a fire put out is followed by nothing --
    /// no socket, no fuse box -- and the tower of boxes is not the
    /// Director's at all. A fire that gets out of the room it started in
    /// is the real fire: the ladder stops. The socket and the fuse box as
    /// the cap's push are in <see cref="DirectorCapEditModeTests"/>.
    /// </summary>
    public sealed class DirectorLadderEditModeTests
    {
        private static readonly SimulationId BinByTheDoor = new SimulationId(3205UL);

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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>Steps until an event of this type has been written, or the limit; the first such event, or null.</summary>
        private static CausalEvent? AdvanceUntil(Run simulation, CausalEventType type, int limit) =>
            AdvanceUntilCount(simulation, type, 1, limit);

        /// <summary>Steps until this many events of the type have been written, or the limit; the last of them, or null.</summary>
        private static CausalEvent? AdvanceUntilCount(Run simulation, CausalEventType type, int count, int limit)
        {
            for (int t = 0; t < limit; t++)
            {
                List<CausalEvent> found = EventsOfType(simulation, type);
                if (found.Count >= count)
                {
                    return found[count - 1];
                }

                simulation.Step();
            }

            List<CausalEvent> last = EventsOfType(simulation, type);
            return last.Count >= count ? last[count - 1] : (CausalEvent?)null;
        }

        /// <summary>One ordinary person, far off in the office, so nobody reaches the bin: a building has to have somebody in it.</summary>
        private static void OnePersonFarOff(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), TheBuilding.OfficeFarCorner, CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Timetable = Array.Empty<ScheduledCue>();
        }

        /// <summary>One person standing in the crossbar two hand's breadths north of the tower's west stack, facing it, to be shoved into it when told to.</summary>
        private static void OnePersonBesideTheTower(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(10950, 8550), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
        }

        /// <summary>
        /// The office as the owner plays it -- the ladder on, the fire started
        /// by the Director -- with the bin beside the meeting room's door as
        /// the only one it can pick, so a test knows where the fire will be.
        /// </summary>
        private ScenarioData TheLadder()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Director.ClimbsTheLadder = true;
            data.Director.FirstIncidentThings = new[] { BinByTheDoor };
            data.Round.HazardWaitsForTrigger = true;
            return data;
        }

        [Test]
        public void TheBin_CatchesOnItsOwn_BetweenThirtyAndNinetySecondsIn_WithNoFloorAlightYet()
        {
            using (var simulation = new Run(TheLadder(), 42UL))
            {
                CausalEvent? started = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 4600);
                Assert.That(started.HasValue, "The bin caught without anybody pressing anything.");
                Assert.That(started.Value.Tick, Is.InRange(1500, 4501), "Somewhere in the day's first minute and a half.");
                Assert.That(started.Value.SourceId, Is.EqualTo(BinByTheDoor));

                Advance(simulation, 2);
                Assert.That(EventsOfType(simulation, CausalEventType.ObjectCaughtFire).Exists(e => e.SourceId == BinByTheDoor),
                    "The bin itself is what is burning.");
                Assert.That(EventsOfType(simulation, CausalEventType.FireSpread), Is.Empty, "Not one square of carpet yet.");
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Running), "The round is under way.");
            }
        }

        [Test]
        public void TheTrigger_BringsTheBinForward()
        {
            using (var simulation = new Run(TheLadder(), 42UL))
            {
                Advance(simulation, 20);
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                CausalEvent? started = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 10);
                Assert.That(started.HasValue, "The button skips the rest of the ordinary day.");
                Assert.That(started.Value.Tick, Is.LessThanOrEqualTo(23));
            }
        }

        [Test]
        public void TheBin_Smoulders_ForAboutTenSeconds_BeforeTheFloorUnderItCatches()
        {
            ScenarioData data = TheLadder();
            data.Agents = new[]
            {
                // Somebody, because a building has to have somebody in it:
                // far off in the office, where they cannot reach the bin.
                new AgentDefinition(new SimulationId(1UL), TheBuilding.OfficeFarCorner, CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                CausalEvent? started = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                Assert.That(started.HasValue);
                CausalEvent? floor = AdvanceUntil(simulation, CausalEventType.FireSpread, 1000);
                Assert.That(floor.HasValue, "The carpet catches in the end.");
                Assert.That(floor.Value.Tick - started.Value.Tick, Is.InRange(390, 610),
                    "About ten seconds of smouldering first: time for somebody brave to reach it.");
            }
        }

        [Test]
        public void AYoungFire_SpreadsSlowly()
        {
            ScenarioData data = TheLadder();
            data.Agents = new[]
            {
                // Somebody, because a building has to have somebody in it:
                // far off in the office, where they cannot reach the bin.
                new AgentDefinition(new SimulationId(1UL), TheBuilding.OfficeFarCorner, CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.FireSpread, 1000);
                Advance(simulation, 10 * Run.TicksPerSecond);

                // A grown fire's squares each light a neighbour every one to
                // two and a half seconds; a young one's every three to seven.
                // Ten seconds on, it is still a patch round the bin.
                Assert.That(simulation.FireForTests.BurningCount, Is.LessThan(12),
                    "Ten seconds after the carpet caught it is still small enough to fight.");
            }
        }

        /// <summary>
        /// The owner (2026-10-02): the socket popping after the boxes fell
        /// was "an instant game over. That should only pop late in a run",
        /// and asked what late should mean: "only as the building's
        /// counter-move". So a bin put out is the end of the ladder: no
        /// socket follows it, no fuse box, and the round may end.
        /// </summary>
        [Test]
        public void WhenTheBinIsPutOut_TheLadderAddsNothingMore_NoSocket_NoFuseBox()
        {
            ScenarioData data = TheLadder();
            OnePersonFarOff(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                CausalEvent? floor = AdvanceUntil(simulation, CausalEventType.FireSpread, 1000);
                Assert.That(floor.HasValue, "The carpet caught: a real fire, not a bin to relight.");
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue, "Nothing burning, and it never left the meeting room: put out.");
                Assert.That(simulation.DirectorForTests.HasSomethingComing, Is.False,
                    "Nothing is on its way, so the round is free to end.");

                // A minute on: longer than any wait the ladder ever had.
                Advance(simulation, 60 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty, "No socket crackles.");
                Assert.That(EventsOfType(simulation, CausalEventType.ObjectExploded), Is.Empty, "Nothing pops.");
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorStartedIncident), Has.Count.EqualTo(1),
                    "And no second bin: the carpet had caught.");
            }
        }

        [Test]
        public void AFireThatGetsOutOfItsRoom_EndsTheLadder()
        {
            ScenarioData data = TheLadder();
            OnePersonFarOff(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                FireSystem fire = simulation.FireForTests;
                Assert.That(fire.TryIgniteForPlayer(fire.CellIndexAt(TheBuilding.Corridor), 0UL, out _), "A square alight in the corridor.");
                CausalEvent? escaped = AdvanceUntil(simulation, CausalEventType.FireEscapedItsRoom, 5);
                Assert.That(escaped.HasValue, "The Director sees the fire has got loose.");

                simulation.PutEverythingOutForTests();

                // Longer than any wait the ladder ever had (forty seconds).
                Advance(simulation, 2100);
                Assert.That(EventsOfType(simulation, CausalEventType.IncidentPutOut), Is.Empty,
                    "Once it has got loose it is the real fire, not an incident to be put out.");
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty,
                    "And the Director adds nothing more to it.");
            }
        }

        /// <summary>
        /// The tower is not the Director's (2026-10-02, the owner: "No
        /// director trigger"). The bin burning does not arm it, and
        /// somebody beside it all the while brings nothing down; a body
        /// that runs into it does, and that knock, not the bin, is the
        /// cause.
        /// </summary>
        [Test]
        public void TheTower_IsNotArmedByTheBin_ItFallsWhenSomebodyRunsIntoIt()
        {
            ScenarioData data = TheLadder();
            OnePersonBesideTheTower(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                Advance(simulation, 600);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorStartedIncident), Has.Count.EqualTo(1), "The bin is alight.");
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody standing beside the tower through the bin's fire brings nothing down.");

                simulation.ShoveAgentForTests(0, 90, 100);
                CausalEvent? trap = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 60);
                Assert.That(trap.HasValue, "Shoved into it at a run, they knock it.");
                Assert.That(trap.Value.TargetId, Is.EqualTo(new SimulationId(1UL)));
                CausalEvent started = EventsOfType(simulation, CausalEventType.DirectorStartedIncident)[0];
                Assert.That(trap.Value.CausalParentEventId, Is.Not.EqualTo(started.EventId),
                    "The bin is not why it fell: the knock is.");
                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 60);
                Assert.That(fell.HasValue);
                Assert.That(fell.Value.Tick - trap.Value.Tick,
                    Is.InRange(data.Perception.ReactionLagMinimumTicks, data.Perception.ReactionLagMaximumTicks),
                    "A beat after the knock.");

                // And the fall brings no socket with it, as it used to five
                // seconds on.
                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty,
                    "No socket crackles because the boxes fell.");
            }
        }

        /// <summary>
        /// "If fire in meeting room is put out too quickly, light another
        /// trashcan straight away" (the owner, 2026-09-27). Too quickly is:
        /// the carpet under the bin never caught. The meeting room has three
        /// bins, so it can happen twice; then the ladder has nothing more.
        /// </summary>
        [Test]
        public void ABinDousedBeforeTheCarpetCaught_LightsAnotherBinABeatLater_UntilAllThreeAreUsed()
        {
            ScenarioData data = TheLadder();
            data.Director.FirstIncidentThings = PrototypeBuilding.MeetingRoomBins();
            OnePersonFarOff(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                for (int bin = 1; bin <= 3; bin++)
                {
                    CausalEvent? started = AdvanceUntilCount(simulation, CausalEventType.DirectorStartedIncident, bin, 30);
                    Assert.That(started.HasValue, $"Bin {bin} caught.");
                    Assert.That(started.Value.Strength, Is.EqualTo(bin), "The story counts the bins.");
                    Advance(simulation, 20);
                    simulation.PutEverythingOutForTests();
                    CausalEvent? putOut = AdvanceUntilCount(simulation, CausalEventType.IncidentPutOut, bin, 5);
                    Assert.That(putOut.HasValue, $"Bin {bin} doused, with the carpet never alight.");
                }

                Advance(simulation, 2100);
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty,
                    "With every bin used the ladder is over: no socket follows a put-out (2026-10-02).");
                Assert.That(simulation.DirectorForTests.HasSomethingComing, Is.False, "Nothing is on its way: the round may end.");

                List<CausalEvent> bins = EventsOfType(simulation, CausalEventType.DirectorStartedIncident);
                List<CausalEvent> putOuts = EventsOfType(simulation, CausalEventType.IncidentPutOut);
                Assert.That(bins, Has.Count.EqualTo(3), "Three bins, and no fourth.");
                Assert.That(putOuts, Has.Count.EqualTo(3));
                Assert.That(new HashSet<SimulationId> { bins[0].SourceId, bins[1].SourceId, bins[2].SourceId }, Has.Count.EqualTo(3),
                    "A different bin each time.");
                Assert.That(EventsOfType(simulation, CausalEventType.FireSpread), Is.Empty, "Not one square of carpet in all that.");
                for (int bin = 1; bin < 3; bin++)
                {
                    Assert.That(bins[bin].Tick - putOuts[bin - 1].Tick, Is.InRange(1, data.Perception.ReactionLagMaximumTicks),
                        "Straight away, but never on the tick it was put out.");
                    Assert.That(bins[bin].CausalParentEventId, Is.EqualTo(putOuts[bin - 1].EventId), "Lit because the last was doused too soon.");
                }
            }
        }

        [Test]
        public void ABinWhoseCarpetCaught_WasAFire_AndIsNotRelit()
        {
            ScenarioData data = TheLadder();
            data.Director.FirstIncidentThings = PrototypeBuilding.MeetingRoomBins();
            OnePersonFarOff(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                CausalEvent? floor = AdvanceUntil(simulation, CausalEventType.FireSpread, 1000);
                Assert.That(floor.HasValue, "The carpet caught.");
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue);
                Advance(simulation, 2100);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorStartedIncident), Has.Count.EqualTo(1),
                    "Two bins were left unused: a fire that burnt the carpet is not relit.");
            }
        }
    }
}

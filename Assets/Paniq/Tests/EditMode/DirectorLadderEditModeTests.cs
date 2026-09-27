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
    /// the carpet under it caught, another bin catches a beat later. The
    /// tower of boxes is armed from the bin: the first frightened person to
    /// run along the corridor brings it down. A fire put out is followed a
    /// while later -- after the boxes fell, if they fell -- by a socket
    /// crackling and popping in the busiest calm room; put that out and the
    /// fuse box goes, taking every socket with it. A fire that gets out of
    /// the room it started in is the real fire: the ladder stops.
    /// </summary>
    public sealed class DirectorLadderEditModeTests
    {
        private static readonly SimulationId BinByTheDoor = new SimulationId(3205UL);
        private static readonly SimulationId OfficeWestSocket = new SimulationId(3271UL);
        private static readonly SimulationId OfficeEastSocket = new SimulationId(3272UL);
        private static readonly SimulationId CafeteriaSocket = new SimulationId(3273UL);

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

        /// <summary>One person standing at the corridor's east end, near the tower, who runs when told to.</summary>
        private static void OnePersonInTheCorridor(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(12500, 6800), CardinalDirection.East,
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

        [Test]
        public void WhenTheBinIsPutOut_ASocketCrackles_FiveToTenSecondsLater_InTheBusiestRoom_ThenPops()
        {
            ScenarioData data = TheLadder();
            OnePersonFarOff(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                Advance(simulation, 50);
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue, "Nothing burning, and it never left the meeting room: put out.");

                CausalEvent? crackle = AdvanceUntil(simulation, CausalEventType.SocketCrackling, 2100);
                Assert.That(crackle.HasValue, "The next rung came.");
                Assert.That(crackle.Value.Tick - putOut.Value.Tick, Is.InRange(250, 501), "Five to ten seconds after the put-out (the owner, 2026-09-27).");
                Assert.That(new[] { OfficeWestSocket, OfficeEastSocket, CafeteriaSocket }, Does.Contain(crackle.Value.SourceId));

                // The room it chose had the most people in it, calm or not, of
                // any room with a socket, the meeting room aside (it has none):
                // here the office, where the one person is.
                int chosen = RoomOf(simulation, crackle.Value.Position);
                int[] people = PeoplePerRoom(simulation);
                foreach (SimulationId socket in new[] { OfficeWestSocket, OfficeEastSocket, CafeteriaSocket })
                {
                    int room = RoomOf(simulation, PositionOf(simulation, socket));
                    Assert.That(people[chosen], Is.GreaterThanOrEqualTo(people[room]),
                        "No socket's room had more people in it than the chosen one.");
                }

                CausalEvent? bang = AdvanceUntil(simulation, CausalEventType.ObjectExploded, 300);
                Assert.That(bang.HasValue);
                Assert.That(bang.Value.SourceId, Is.EqualTo(crackle.Value.SourceId), "The crackling socket is what went.");
                Assert.That(bang.Value.Tick - crackle.Value.Tick, Is.InRange(248, 252), "Five seconds of crackle first.");

                Advance(simulation, 5);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerSparkStarted), Is.Empty,
                    "A socket going off lights no cable: nothing climbs up to the fuse box.");
            }
        }

        [Test]
        public void WhenTheSocketFireIsPutOutToo_TheFuseBoxGoes_AndEverySocketAfterIt()
        {
            using (var simulation = new Run(TheLadder(), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                Advance(simulation, 50);
                simulation.PutEverythingOutForTests();
                AdvanceUntil(simulation, CausalEventType.ObjectExploded, 2400);

                // At once, rather than waiting for it to burn itself out.
                simulation.PutEverythingOutForTests();

                int putOuts = 0;
                for (int t = 0; t < 2500 && putOuts < 2; t++)
                {
                    simulation.Step();
                    putOuts = EventsOfType(simulation, CausalEventType.IncidentPutOut).Count;
                }

                Assert.That(putOuts, Is.EqualTo(2), "The socket's fire went out too.");
                CausalEvent? crackle = null;
                for (int t = 0; t < 2100 && crackle == null; t++)
                {
                    simulation.Step();
                    List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                    crackle = crackles.Count >= 2 ? crackles[1] : (CausalEvent?)null;
                }

                Assert.That(crackle.HasValue, "The last rung came.");
                Assert.That(crackle.Value.SourceId, Is.EqualTo(PrototypeBuilding.FuseBox), "The fuse box crackles.");

                // Crackle, go, and the spark down the cable to the last socket:
                // fires in half the building at once, all of them the fuse
                // box's own doing and none of them a fire that got loose.
                Advance(simulation, 250 + 4 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.FireEscapedItsRoom), Is.Empty,
                    "The fuse box's own cascade is the incident, not a fire getting out of its room.");

                Advance(simulation, Run.TicksPerSecond);
                var gone = new HashSet<SimulationId>();
                foreach (CausalEvent bang in EventsOfType(simulation, CausalEventType.ObjectExploded))
                {
                    gone.Add(bang.SourceId);
                }

                Assert.That(gone, Does.Contain(PrototypeBuilding.FuseBox));
                Assert.That(gone, Is.SupersetOf(new[] { OfficeWestSocket, OfficeEastSocket, CafeteriaSocket }),
                    "The spark ran down the cable and took every socket with it, including the one already wrecked on the way.");
            }
        }

        [Test]
        public void AFireThatGetsOutOfItsRoom_EndsTheLadder()
        {
            // One person far off, so nobody runs along the corridor: the
            // tower falling would bring the socket whatever the fire did.
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

                // Longer than the longest wait for a next rung (forty seconds).
                Advance(simulation, 2100);
                Assert.That(EventsOfType(simulation, CausalEventType.IncidentPutOut), Is.Empty,
                    "Once it has got loose it is the real fire, not an incident to be put out.");
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty,
                    "And the Director adds nothing more to it.");
            }
        }

        /// <summary>
        /// The owner's rule (2026-09-27): the boxes come down "once people
        /// start running down the corridor", whatever the fire is doing. A
        /// calm person beside the tower all day, bin or no bin, brings
        /// nothing down.
        /// </summary>
        [Test]
        public void TheTower_ComesDown_WhenTheFirstFrightenedPersonRunsAlongTheCorridor_WhateverTheFireIsDoing()
        {
            ScenarioData data = TheLadder();
            OnePersonInTheCorridor(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                Advance(simulation, 600);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody calm standing beside the tower all day brings nothing down, bin or no bin.");

                simulation.FrightenForTests(0);
                CausalEvent? trap = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 100);
                Assert.That(trap.HasValue, "Frightened, they run for the archway, and the tower comes down.");
                Assert.That(trap.Value.TargetId, Is.EqualTo(new SimulationId(1UL)), "Sprung by the runner.");
                CausalEvent started = EventsOfType(simulation, CausalEventType.DirectorStartedIncident)[0];
                Assert.That(trap.Value.CausalParentEventId, Is.EqualTo(started.EventId), "The bin the Director lit is why the tower was armed.");
                Assert.That(EventsOfType(simulation, CausalEventType.FireEscapedItsRoom), Is.Empty,
                    "The fire never left the meeting room: that is no longer what arms the tower.");
                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 20);
                Assert.That(fell.HasValue);
                Assert.That(fell.Value.Tick - trap.Value.Tick, Is.InRange(1, data.Perception.ReactionLagMaximumTicks), "A beat later.");
            }
        }

        /// <summary>
        /// "If fire in meeting room is put out too quickly, light another
        /// trashcan straight away" (the owner, 2026-09-27). Too quickly is:
        /// the carpet under the bin never caught. The meeting room has three
        /// bins, so it can happen twice; then the ladder goes on as usual.
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

                CausalEvent? crackle = AdvanceUntil(simulation, CausalEventType.SocketCrackling, 2100);
                Assert.That(crackle.HasValue, "With every bin used, the ladder goes on: the socket.");

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

                Assert.That(crackle.Value.Tick - putOuts[2].Tick, Is.InRange(250, 501), "The socket's usual wait after the last put-out.");
            }
        }

        [Test]
        public void ABinWhoseCarpetCaught_WasAFire_AndTheSocketFollowsItsPutOut()
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
                CausalEvent? crackle = AdvanceUntil(simulation, CausalEventType.SocketCrackling, 2100);
                Assert.That(crackle.HasValue, "The socket, not another bin.");
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorStartedIncident), Has.Count.EqualTo(1),
                    "Two bins were left unused: a fire that burnt the carpet is not relit.");
            }
        }

        /// <summary>
        /// The owner's order (2026-09-27): bin, boxes, outlet. Five seconds
        /// after the tower falls the socket crackles, whatever the bin fire
        /// is doing; and once the socket's fire is put out, the fuse box
        /// comes five to ten seconds later.
        /// </summary>
        [Test]
        public void TheSocket_ComesFiveSecondsAfterTheBoxesFell_WhateverTheBinIsDoing_AndTheFuseBoxFollowsItsPutOut()
        {
            ScenarioData data = TheLadder();
            OnePersonInTheCorridor(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                simulation.FrightenForTests(0);
                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 100);
                Assert.That(fell.HasValue, "The runner brought the tower down while the bin was still smouldering.");

                CausalEvent? crackle = AdvanceUntil(simulation, CausalEventType.SocketCrackling, 400);
                Assert.That(crackle.HasValue, "The socket crackles with the bin still burning.");
                Assert.That(EventsOfType(simulation, CausalEventType.IncidentPutOut), Is.Empty, "Nothing was put out first.");
                Assert.That(crackle.Value.Tick - fell.Value.Tick, Is.InRange(200, 300), "About five seconds after the boxes fell.");

                CausalEvent? bang = AdvanceUntil(simulation, CausalEventType.ObjectExploded, 300);
                Assert.That(bang.HasValue, "And pops.");
                Advance(simulation, 10);
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue, "Bin and socket fire out together: one put-out.");
                Assert.That(EventsOfType(simulation, CausalEventType.FireEscapedItsRoom), Is.Empty,
                    "The socket's fire joined the bin's incident rather than counting as fire that got loose.");

                CausalEvent? fuseBox = null;
                for (int t = 0; t < 700 && fuseBox == null; t++)
                {
                    simulation.Step();
                    List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                    fuseBox = crackles.Count >= 2 ? crackles[1] : (CausalEvent?)null;
                }

                Assert.That(fuseBox.HasValue, "The fuse box crackles next.");
                Assert.That(fuseBox.Value.SourceId, Is.EqualTo(PrototypeBuilding.FuseBox));
                Assert.That(fuseBox.Value.Tick - putOut.Value.Tick, Is.InRange(250, 501), "Five to ten seconds after the put-out.");
            }
        }

        private static int RoomOf(Run simulation, LogicalPosition where) => simulation.GeometryForTests.RoomAtPoint(where);

        private static LogicalPosition PositionOf(Run simulation, SimulationId thing)
        {
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).ObjectId == thing)
                {
                    return simulation.GetPhysicsObject(i).Position;
                }
            }

            throw new KeyNotFoundException(thing.ToString());
        }

        private static int[] PeoplePerRoom(Run simulation)
        {
            var people = new int[simulation.GeometryForTests.RoomCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                int room = simulation.GeometryForTests.RoomAtPoint(agent.Position);
                if (agent.Participation == AgentParticipation.Participating && room >= 0)
                {
                    people[room]++;
                }
            }

            return people;
        }
    }
}

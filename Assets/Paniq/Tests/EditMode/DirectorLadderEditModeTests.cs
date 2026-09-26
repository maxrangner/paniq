using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The Director's ladder of small incidents (prototype 3, second batch,
    /// 2026-09-26). The round opens with an ordinary day; a waste bin in the
    /// meeting room catches fire after half a minute to a minute and a half
    /// (or at once, on the trigger). Put out, it is followed a while later by
    /// a socket crackling and popping in the busiest calm room; put that out
    /// and the fuse box goes, taking every socket with it. A fire that gets
    /// out of the room it started in is the real fire: the ladder stops, and
    /// the tower of boxes is armed by it.
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
        private static CausalEvent? AdvanceUntil(Run simulation, CausalEventType type, int limit)
        {
            for (int t = 0; t < limit; t++)
            {
                List<CausalEvent> found = EventsOfType(simulation, type);
                if (found.Count > 0)
                {
                    return found[0];
                }

                simulation.Step();
            }

            List<CausalEvent> last = EventsOfType(simulation, type);
            return last.Count > 0 ? last[0] : (CausalEvent?)null;
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
        public void WhenTheBinIsPutOut_ASocketCrackles_TwentyToFortySecondsLater_InTheBusiestCalmRoom_ThenPops()
        {
            using (var simulation = new Run(TheLadder(), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                Advance(simulation, 50);
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue, "Nothing burning, and it never left the meeting room: put out.");

                CausalEvent? crackle = AdvanceUntil(simulation, CausalEventType.SocketCrackling, 2100);
                Assert.That(crackle.HasValue, "The next rung came.");
                Assert.That(crackle.Value.Tick - putOut.Value.Tick, Is.InRange(1000, 2001));
                Assert.That(new[] { OfficeWestSocket, OfficeEastSocket, CafeteriaSocket }, Does.Contain(crackle.Value.SourceId));

                // The room it chose had the most calm people in it of any room
                // with a socket, the meeting room aside (it has none).
                int chosen = RoomOf(simulation, crackle.Value.Position);
                int[] calm = CalmPerRoom(simulation);
                foreach (SimulationId socket in new[] { OfficeWestSocket, OfficeEastSocket, CafeteriaSocket })
                {
                    int room = RoomOf(simulation, PositionOf(simulation, socket));
                    Assert.That(calm[chosen], Is.GreaterThanOrEqualTo(calm[room]),
                        "No socket's room had more calm people in it than the chosen one.");
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
            using (var simulation = new Run(TheLadder(), 42UL))
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

        [Test]
        public void TheTower_StaysStanding_WhileTheFireIsInTheRoomItStartedIn()
        {
            ScenarioData data = TheLadder();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(12500, 6800), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Timetable = Array.Empty<ScheduledCue>();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                Advance(simulation, 600);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "A bin burning in the meeting room is no reason for the tower to come down.");

                FireSystem fire = simulation.FireForTests;
                fire.TryIgniteForPlayer(fire.CellIndexAt(TheBuilding.Corridor), 0UL, out _);
                CausalEvent? trap = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 50);
                Assert.That(trap.HasValue, "Once the fire has got loose, the first person near the tower brings it down.");
                CausalEvent escaped = EventsOfType(simulation, CausalEventType.FireEscapedItsRoom)[0];
                Assert.That(trap.Value.CausalParentEventId, Is.EqualTo(escaped.EventId), "The fire getting loose is why.");
            }
        }

        [Test]
        public void ARungStillToCome_KeepsTheRoundFromEndingAsStalled()
        {
            ScenarioData data = TheLadder();
            data.Round.StallTicks = 100;
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), TheBuilding.OfficeFarCorner, CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Timetable = Array.Empty<ScheduledCue>();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                Advance(simulation, 20);
                simulation.PutEverythingOutForTests();
                AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                AdvanceUntil(simulation, CausalEventType.SocketCrackling, 2100);
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Not.Empty);
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Running),
                    "Twenty seconds and more of a quiet office, and the round waited for the socket.");
            }
        }

        [Test]
        public void TheAllClear_SilencesTheBells_AFewSecondsAfterAPutOut()
        {
            ScenarioData data = TheLadder();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
                simulation.QueueCommand(PlayerCommandType.PullAlarm, TheBuilding.TheAlarm, simulation.Tick + 1);
                Advance(simulation, 5);
                Assert.That(simulation.GetSnapshot().AlarmsRinging, "The bells are ringing.");

                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                CausalEvent? allClear = AdvanceUntil(simulation, CausalEventType.AllClear, 700);
                Assert.That(allClear.HasValue, "The all-clear came.");
                Assert.That(allClear.Value.Tick - putOut.Value.Tick, Is.InRange(390, 610), "About ten seconds after.");
                Assert.That(simulation.GetSnapshot().AlarmsRinging, Is.False, "And the bells stopped.");

                // Somebody still frightened pulls it again: the all-clear
                // stands, and the bells fall silent again a little later.
                simulation.QueueCommand(PlayerCommandType.PullAlarm, TheBuilding.TheAlarm, simulation.Tick + 1);
                Advance(simulation, 5);
                Assert.That(simulation.GetSnapshot().AlarmsRinging, "Ringing again.");
                Advance(simulation, 700);
                Assert.That(EventsOfType(simulation, CausalEventType.AllClear), Has.Count.EqualTo(2),
                    "A second all-clear for the second pull.");
                Assert.That(simulation.GetSnapshot().AlarmsRinging, Is.False, "Nothing is burning, so the bells stay quiet.");
            }
        }

        [Test]
        public void ABravePerson_PutsOutABurningBin_BeforeTheFloorCatches()
        {
            ScenarioData data = TheLadder();
            AgentTraitValues brave = AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, 10);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, 10200), CardinalDirection.East, brave)
            };
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 1500);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookExtinguisher), Is.Not.Empty,
                    "They saw the bin burning and went for the bottle on the wall.");
                Assert.That(putOut.HasValue, "And put it out.");
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

        private static int[] CalmPerRoom(Run simulation)
        {
            var calm = new int[simulation.GeometryForTests.RoomCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot agent = simulation.GetAgent(i);
                int room = simulation.GeometryForTests.RoomAtPoint(agent.Position);
                if (agent.Participation == AgentParticipation.Participating && agent.FearState == AgentFearState.Calm && room >= 0)
                {
                    calm[room]++;
                }
            }

            return calm;
        }
    }
}

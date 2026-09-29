using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The Director caps the round (2026-09-28, the owner's rule: left
    /// alone, about a quarter live and never more than half). Before the
    /// curtain it draws an allowance from a stream of its own; every half
    /// second it reads how many are on course to get out -- out, plus
    /// everybody who has set out and could walk to the way out, while it
    /// stands open or the card is in a frightened pocket that can reach it
    /// -- and, once the way out is open and more are on course than the
    /// allowance, pushes: a trap still standing with somebody on course in
    /// its room, the socket in the room with the most of them, the fuse box
    /// once a socket has gone, another bin; one at a time with a rest
    /// between. A round that is already a massacre gets nothing more. It
    /// reads the crowd and the card, never the player.
    /// </summary>
    public sealed class DirectorCapEditModeTests
    {
        private static readonly SimulationId BinByTheDoor = new SimulationId(3205UL);
        private static readonly SimulationId TheFuseBox = new SimulationId(3281UL);

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

        // ---------------------------------------------------------------- scenes

        /// <summary>
        /// The office as the owner plays it -- the ladder and the cap on, the
        /// bin beside the meeting room's door the only one -- with the
        /// allowance pinned to one share of the crowd, the fire never
        /// spreading across the floor, and nobody calming down or freezing.
        /// </summary>
        private ScenarioData TheCap(int allowancePercent)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Director.ClimbsTheLadder = true;
            data.Director.CapsTheRound = true;
            data.Director.AllowanceMinimumPercent = allowancePercent;
            data.Director.AllowanceMaximumPercent = allowancePercent;
            data.Director.FirstIncidentThings = new[] { BinByTheDoor };
            data.Round.HazardWaitsForTrigger = true;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calming.Enabled = false;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            return data;
        }

        private ScenarioData TheCapOff()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Director.ClimbsTheLadder = true;
            data.Director.FirstIncidentThings = new[] { BinByTheDoor };
            return data;
        }

        /// <summary>The way out the player's to open, the card put away: <see cref="OpenTheWayOut"/> then puts the crowd on course from its first fright.</summary>
        private static ScenarioData WithTheWayOutOpenable(ScenarioData data) => TheBuilding.WithThePlayerAbleToAct(data);

        /// <summary>Six ordinary members of staff standing about the south end of the office, clear of the robot vacuum at (0, -3000), deciding nothing of their own.</summary>
        private static void SixInTheOffice(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, -4500), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(1500, -4500), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(3UL), new LogicalPosition(-1500, -4500), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(4UL), new LogicalPosition(0, -2000), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(5UL), new LogicalPosition(1500, -2000), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(6UL), new LogicalPosition(-1500, -2000), CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
        }

        /// <summary>Two people: one at the corridor's east end near the tower, who runs when told to, and one far off in the office.</summary>
        private static void TwoPeople(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(12500, 6800), CardinalDirection.East, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(2UL), TheBuilding.OfficeFarCorner, CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
        }

        private static void LightTheBin(Run simulation)
        {
            simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
            CausalEvent? started = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 20);
            Assert.That(started.HasValue, "The bin caught on the trigger.");
        }

        private static void OpenTheWayOut(Run simulation)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, simulation.Tick + 1);
            simulation.Step();
            Assert.That(Door(simulation, TheBuilding.TheWayOut).State, Is.Not.EqualTo(DoorState.Locked), "The way out stands open.");
        }

        private static void FrightenEverybody(Run simulation)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                simulation.FrightenForTests(i);
            }
        }

        // ---------------------------------------------------------------- the allowance

        [Test]
        public void TheAllowance_IsDrawnFromItsOwnStream_SoTheLadderIsUntouched_AndItIsTwoToEightOfTwenty()
        {
            int binTickWithout;
            using (var simulation = new Run(TheCapOff(), 42UL))
            {
                binTickWithout = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 4600).Value.Tick;
                Assert.That(simulation.DirectorForTests.CapsForTests, Is.False);
            }

            var allowances = new List<int>();
            for (ulong seed = 40UL; seed < 50UL; seed++)
            {
                ScenarioData data = scenario.ToRuntimeData();
                data.Director.ClimbsTheLadder = true;
                data.Director.CapsTheRound = true;
                data.Director.FirstIncidentThings = new[] { BinByTheDoor };
                using (var simulation = new Run(data, seed))
                {
                    Assert.That(simulation.DirectorForTests.CapsForTests, Is.True);
                    int allowance = simulation.DirectorForTests.AllowanceForTests;
                    Assert.That(allowance, Is.InRange(2, 8), $"Seed {seed}: ten to forty percent of twenty.");
                    allowances.Add(allowance);
                    if (seed == 42UL)
                    {
                        int binTick = AdvanceUntil(simulation, CausalEventType.DirectorStartedIncident, 4600).Value.Tick;
                        Assert.That(binTick, Is.EqualTo(binTickWithout),
                            "The bin catches on the same tick with the cap on: the allowance came from a stream of its own.");
                    }
                }
            }

            Assert.That(allowances, Is.Not.All.EqualTo(allowances[0]), "Over ten seeds the building's temper varies.");
        }

        // ---------------------------------------------------------------- ahead

        [Test]
        public void Ahead_WithATrapStandingOnTheCrowdsWay_TheDirectorSpringsItItself_ABeatLater()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheOffice(data);

            // One of the six stands in the corridor, in the tower's own room;
            // and no runner may bring the tower down, so only the Director can.
            data.Agents[5] = new AgentDefinition(new SimulationId(6UL), new LogicalPosition(9000, 7500), CardinalDirection.East,
                AgentTraitValues.AllOrdinary);
            data.Traps.TriggerSpeedMillimetresPerTick = 100000;
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.DirectorForTests.AllowanceForTests, Is.EqualTo(1), "A tenth of six, rounded: one.");
                OpenTheWayOut(simulation);
                LightTheBin(simulation);
                Advance(simulation, 60);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Is.Empty,
                    "The door open and six calm people: nobody has set out, so nobody is on course and nothing is pushed.");

                FrightenEverybody(simulation);
                CausalEvent? pushed = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(pushed.HasValue, "Six frightened people able to reach the open door, one allowed: the Director pushes.");
                Assert.That(pushed.Value.Strength, Is.EqualTo(6), "Six on course.");
                Assert.That(pushed.Value.DurationTicks, Is.EqualTo(1), "Against an allowance of one.");

                List<CausalEvent> triggered = EventsOfType(simulation, CausalEventType.TrapTriggered);
                Assert.That(triggered, Has.Count.EqualTo(1), "The tower, with somebody on course in the corridor, is sprung by the push.");
                Assert.That(triggered[0].SourceId, Is.EqualTo(TheBuilding.TheTrap));
                Assert.That(triggered[0].HasTarget, Is.False, "By the Director, not by a runner.");
                Assert.That(triggered[0].CausalParentEventId, Is.EqualTo(pushed.Value.EventId));
                Assert.That(triggered[0].Tick, Is.EqualTo(pushed.Value.Tick));

                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 300);
                Assert.That(fell.HasValue);
                Assert.That(fell.Value.Tick - triggered[0].Tick, Is.InRange(data.Traps.CreakTicks * 4 / 5, data.Traps.CreakTicks * 6 / 5),
                    "Never on the tick it was decided: it creaks first, as for a runner (2026-09-29).");
            }
        }

        [Test]
        public void Ahead_WithNoTrapLeft_TheSocketInTheRoomWithTheMostOnCourseCrackles_AndPops_AndThenTheDirectorRests()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheOffice(data);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            using (var simulation = new Run(data, 42UL))
            {
                OpenTheWayOut(simulation);
                LightTheBin(simulation);
                FrightenEverybody(simulation);

                CausalEvent? first = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(first.HasValue, "Ahead: a push.");
                List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                Assert.That(crackles, Has.Count.EqualTo(1), "The socket is the push when no trap stands.");
                Assert.That(crackles[0].CausalParentEventId, Is.EqualTo(first.Value.EventId), "Because of the push, not a put-out.");
                int office = simulation.GeometryForTests.RoomAtPoint(TheBuilding.Office);
                Assert.That(simulation.GeometryForTests.RoomAtPoint(crackles[0].Position), Is.EqualTo(office),
                    "In the room with the most people on course: the office, where all six are.");

                CausalEvent? bang = AdvanceUntil(simulation, CausalEventType.ObjectExploded, 300);
                Assert.That(bang.HasValue, "And it pops.");
                Assert.That(bang.Value.SourceId, Is.EqualTo(crackles[0].SourceId));

                Advance(simulation, data.Director.PushMinimumTicks - 300);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Has.Count.EqualTo(1),
                    "No second push inside the rest, whatever the round is doing.");
            }
        }

        [Test]
        public void Ahead_OnceASocketHasGone_AndNoneIsLeftWhereTheCrowdIs_TheFuseBoxIsThePush()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheOffice(data);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            using (var simulation = new Run(data, 42UL))
            {
                // The ladder's own socket first: the bin put out, the socket
                // pops five to ten seconds later in the busiest room, the
                // office. That is the socket rung spent.
                LightTheBin(simulation);
                Advance(simulation, 50);
                simulation.PutEverythingOutForTests();
                CausalEvent? bang = AdvanceUntil(simulation, CausalEventType.ObjectExploded, 2400);
                Assert.That(bang.HasValue, "The ladder's socket went.");
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Is.Empty, "Not a push: the ladder.");

                OpenTheWayOut(simulation);
                FrightenEverybody(simulation);
                CausalEvent? pushed = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(pushed.HasValue, "Ahead: a push.");
                Assert.That(pushed.Value.TargetId, Is.EqualTo(TheFuseBox),
                    "The office's other socket is in the incident's room and the cafeteria has nobody on course; a socket has gone, so the fuse box.");
                List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                Assert.That(crackles[crackles.Count - 1].SourceId, Is.EqualTo(TheFuseBox));
                Assert.That(crackles[crackles.Count - 1].CausalParentEventId, Is.EqualTo(pushed.Value.EventId));
            }
        }

        [Test]
        public void BeforeTheWayOutIsOpen_AFrightenedHolderPutsPeopleOnCourse_ButNothingIsPushedYet()
        {
            ScenarioData data = TheCap(10);
            SixInTheOffice(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.GiveKeycardForTests(0);
                LightTheBin(simulation);
                FrightenEverybody(simulation);
                Advance(simulation, 60);
                Assert.That(simulation.DirectorForTests.OnCourseForTests, Is.GreaterThan(1),
                    "The card in a frightened pocket that can reach the door: the crowd is on course.");
                Assert.That(simulation.DirectorForTests.IsAheadForTests, Is.False,
                    "But the door is still shut: the holder's walk is the round's own suspense, and the Director waits.");
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Is.Empty);
            }
        }

        [Test]
        public void WithTheDoorShut_AndTheCardOnADesk_NobodyIsOnCourse_AndNothingIsPushed()
        {
            ScenarioData data = TheCap(10);
            SixInTheOffice(data);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                LightTheBin(simulation);
                FrightenEverybody(simulation);
                Advance(simulation, 600);
                Assert.That(simulation.DirectorForTests.OnCourseForTests, Is.EqualTo(0),
                    "Six people running at a shut card door with the card on a desk: nobody on course.");
                Assert.That(simulation.DirectorForTests.IsAheadForTests, Is.False);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Is.Empty, "So nothing to push back against.");
            }
        }

        // ---------------------------------------------------------------- a massacre

        [Test]
        public void AMassacre_GetsNothingMore_NoTrapArmed_NoSocketAfterAPutOut()
        {
            // Two people and an allowance of the whole crowd: from the first
            // reading, only the allowance's worth are left, so the Director
            // lets up -- the gate is what is tested, not how they died.
            ScenarioData data = TheCap(100);
            TwoPeople(data);
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.DirectorForTests.AllowanceForTests, Is.EqualTo(2));
                LightTheBin(simulation);
                Advance(simulation, 30);
                Assert.That(simulation.DirectorForTests.IsMassacreForTests, Is.True);

                simulation.FrightenForTests(0);
                Advance(simulation, 200);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody running along the corridor brings nothing down: the tower is unarmed.");

                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue);
                Advance(simulation, 700);
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty,
                    "No socket five to ten seconds after the put-out: nothing more is added.");
                Assert.That(simulation.DirectorForTests.HasSomethingComing, Is.False,
                    "And the round may end: nothing is on its way.");
            }
        }

        // ---------------------------------------------------------------- helpers

        private static DoorSnapshot Door(Run simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == id)
                {
                    return simulation.GetDoor(i);
                }
            }

            throw new KeyNotFoundException(id.ToString());
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
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

        private static CausalEvent? AdvanceUntil(Run simulation, CausalEventType type, int limit) =>
            AdvanceUntilCount(simulation, type, 1, limit);

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
    }
}

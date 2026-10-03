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
        public void TheAllowance_IsDrawnFromItsOwnStream_SoTheLadderIsUntouched_AndItIsATenthToTwoFifthsOfTheCrowd()
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
                int crowd = data.Agents.Length;
                using (var simulation = new Run(data, seed))
                {
                    Assert.That(simulation.DirectorForTests.CapsForTests, Is.True);
                    int allowance = simulation.DirectorForTests.AllowanceForTests;
                    Assert.That(allowance, Is.InRange((crowd * 10 + 50) / 100, (crowd * 40 + 50) / 100),
                        $"Seed {seed}: ten to forty percent of the {crowd} in the building.");
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

        /// <summary>
        /// The Director no longer springs the tower (2026-10-02, the owner:
        /// "No director trigger"). With it standing on the crowd's way and
        /// the round ahead, the push is the socket, and nothing the
        /// Director does brings the boxes down.
        /// </summary>
        [Test]
        public void Ahead_WithTheTowerStillStanding_TheDirectorLeavesItAlone_AndTheSocketIsThePush()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheOffice(data);
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
                Assert.That(pushed.Value.TargetId, Is.Not.EqualTo(TheBuilding.TheTrap), "Not the tower.");

                List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                Assert.That(crackles, Has.Count.EqualTo(1), "The socket where the crowd is, though the tower stands.");
                Assert.That(crackles[0].CausalParentEventId, Is.EqualTo(pushed.Value.EventId));
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered).Exists(
                        knock => knock.CausalParentEventId == pushed.Value.EventId), Is.False,
                    "Nothing the Director does knocks the tower: only a body running into it.");
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
        public void Ahead_OnceASocketHasGone_AndNoneIsLeftWhereTheCrowdIs_TheFuseBoxIsThePush_AndTakesEverySocket()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheOffice(data);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();

            // A short crackle and no rest between pushes, so the second
            // push comes while the six are still on their way out.
            data.Director.CrackleTicks = 50;
            data.Director.PushMinimumTicks = 1;
            data.Director.PushMaximumTicks = 1;
            using (var simulation = new Run(data, 42UL))
            {
                // The first push is the socket in the office, where all six
                // are: since 2026-10-02 a socket goes only as a push.
                OpenTheWayOut(simulation);
                LightTheBin(simulation);
                FrightenEverybody(simulation);
                CausalEvent? first = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(first.HasValue, "Ahead: a push.");
                Assert.That(first.Value.TargetId, Is.Not.EqualTo(TheFuseBox), "A socket first: the fuse box is the last resort.");
                CausalEvent? bang = AdvanceUntil(simulation, CausalEventType.ObjectExploded, 100);
                Assert.That(bang.HasValue, "The socket went.");

                CausalEvent? second = AdvanceUntilCount(simulation, CausalEventType.DirectorPushed, 2, 100);
                Assert.That(second.HasValue, "Still ahead: a second push.");
                Assert.That(second.Value.TargetId, Is.EqualTo(TheFuseBox),
                    "The office's other socket is in the incident's room and no other socket has anybody on course beside it; a socket has gone, so the fuse box.");
                List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                Assert.That(crackles[crackles.Count - 1].SourceId, Is.EqualTo(TheFuseBox));
                Assert.That(crackles[crackles.Count - 1].CausalParentEventId, Is.EqualTo(second.Value.EventId));

                // Crackle, go, and the spark down the cable to the last
                // socket, in the cubicle landscape: fires in half the
                // building at once, all of them the fuse box's own doing
                // and none of them a fire that got loose.
                Advance(simulation, data.Director.CrackleTicks + data.Director.BangSettlesTicks + Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.FireEscapedItsRoom), Is.Empty,
                    "The fuse box's own cascade is the incident, not a fire getting out of its room.");
                var gone = new HashSet<SimulationId>();
                foreach (CausalEvent pop in EventsOfType(simulation, CausalEventType.ObjectExploded))
                {
                    gone.Add(pop.SourceId);
                }

                Assert.That(gone, Does.Contain(TheFuseBox));
                foreach (PowerLineDefinition cable in data.PowerLines)
                {
                    Assert.That(gone, Does.Contain(cable.ToObjectId),
                        $"The spark ran down the cable and took socket {cable.ToObjectId} with it.");
                }
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

        /// <summary>Six ordinary members of staff standing in the arm that leads to the way out, at its south end, deciding nothing of their own.</summary>
        private static void SixInTheArmToTheWayOut(ScenarioData data)
        {
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(13700, 9800), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(14500, 9800), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(3UL), new LogicalPosition(15300, 9800), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(4UL), new LogicalPosition(13700, 10800), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(5UL), new LogicalPosition(14500, 10800), CardinalDirection.North, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(6UL), new LogicalPosition(15300, 10800), CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
        }

        /// <summary>
        /// The building may strike in the last stretch (2026-10-02, the
        /// owner: "yes, with a way round"): with the crowd in the arm that
        /// leads to the way out, the socket on that arm's wall is the one
        /// that crackles. Before it had one, the Director reached for a
        /// socket in a room the crowd had already left.
        /// </summary>
        [Test]
        public void Ahead_WithTheCrowdInTheArmToTheWayOut_TheSocketThereIsThePush()
        {
            ScenarioData data = WithTheWayOutOpenable(TheCap(10));
            SixInTheArmToTheWayOut(data);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            using (var simulation = new Run(data, 42UL))
            {
                OpenTheWayOut(simulation);
                LightTheBin(simulation);
                FrightenEverybody(simulation);

                CausalEvent? pushed = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(pushed.HasValue, "Six frightened people a few strides from an open door, one allowed: the Director pushes.");
                Assert.That(pushed.Value.TargetId, Is.EqualTo(PrototypeBuilding.ExitArmSocket), "With the socket where they are.");
                List<CausalEvent> crackles = EventsOfType(simulation, CausalEventType.SocketCrackling);
                Assert.That(crackles, Has.Count.EqualTo(1));
                Assert.That(crackles[0].SourceId, Is.EqualTo(PrototypeBuilding.ExitArmSocket));
                Assert.That(crackles[0].Tick, Is.GreaterThan(pushed.Value.Tick - 1), "It crackles first: five seconds to get people clear.");
                Assert.That(EventsOfType(simulation, CausalEventType.ObjectExploded), Is.Empty, "And has not gone yet.");
            }
        }

        /// <summary>
        /// A card door pounded down under the player's hand is a way out
        /// standing open. Until 2026-10-02 the Director read it as shut,
        /// because a broken card door still "needs the card" on paper: the
        /// building never turned on a crowd that had broken its way out, and
        /// a hand held on the way out and nothing else saved two thirds of
        /// the office.
        /// </summary>
        [Test]
        public void ACardDoorPoundedDownUnderTheHand_IsAWayOutOpen_AndTheBuildingTurnsOnTheCrowd()
        {
            ScenarioData data = TheCap(10);
            SixInTheArmToTheWayOut(data);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();

            // A door that gives in a few seconds, so the test is about what
            // the Director makes of a broken card door and not about how
            // long the pounding takes.
            data.Exits.CardDoorPoundTicks = 150;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                LightTheBin(simulation);
                FrightenEverybody(simulation);
                simulation.QueueCommand(PlayerCommandType.InfluenceDoor, TheBuilding.TheWayOut, simulation.Tick + 1);

                CausalEvent? broken = AdvanceUntil(simulation, CausalEventType.DoorBrokenDown, 60 * Run.TicksPerSecond);
                Assert.That(broken.HasValue, "Pounded for the hand, the card door gives.");
                Assert.That(Door(simulation, TheBuilding.TheWayOut).State, Is.EqualTo(DoorState.Broken));
                foreach (CausalEvent early in EventsOfType(simulation, CausalEventType.DirectorPushed))
                {
                    Assert.That(early.Tick, Is.GreaterThanOrEqualTo(broken.Value.Tick), "Nothing is pushed while the door still holds.");
                }

                CausalEvent? pushed = AdvanceUntil(simulation, CausalEventType.DirectorPushed, 100);
                Assert.That(pushed.HasValue, "The way out stands open and more are on course than the one allowed: the building turns on them.");
                Assert.That(simulation.DirectorForTests.IsAheadForTests, Is.True);
            }
        }

        // ---------------------------------------------------------------- a massacre

        [Test]
        public void AMassacre_GetsNothingMore_NoPush_AndNoRelitBin()
        {
            // Two people and an allowance of the whole crowd: from the first
            // reading, only the allowance's worth are left, so the Director
            // lets up -- the gate is what is tested, not how they died. All
            // three bins are there to relight, and none is.
            ScenarioData data = TheCap(100);
            data.Director.FirstIncidentThings = PrototypeBuilding.MeetingRoomBins();
            TwoPeople(data);
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(simulation.DirectorForTests.AllowanceForTests, Is.EqualTo(2));
                LightTheBin(simulation);
                Advance(simulation, 30);
                Assert.That(simulation.DirectorForTests.IsMassacreForTests, Is.True);

                // Doused before the carpet caught: on an ordinary day that
                // lights another bin a beat later.
                simulation.PutEverythingOutForTests();
                CausalEvent? putOut = AdvanceUntil(simulation, CausalEventType.IncidentPutOut, 5);
                Assert.That(putOut.HasValue);
                Assert.That(simulation.DirectorForTests.HasSomethingComing, Is.False,
                    "The round may end: nothing is on its way.");
                Advance(simulation, 700);
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorStartedIncident), Has.Count.EqualTo(1),
                    "No second bin: nothing more is added to a massacre.");
                Assert.That(EventsOfType(simulation, CausalEventType.DirectorPushed), Is.Empty, "And no push.");
                Assert.That(EventsOfType(simulation, CausalEventType.SocketCrackling), Is.Empty);
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

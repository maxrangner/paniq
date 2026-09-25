using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Errands: what a calm person does about a cue, carried out with the
    /// behaviours that already exist. Going home to their own chair through
    /// the doors on the way, a toilet trip behind a shut stall door, a chat
    /// that neighbours glance at and a fright ends, and home time -- the
    /// building leaving calmly through an open way out, or queueing at a
    /// locked one.
    /// </summary>
    public sealed class ErrandsEditModeTests
    {
        /// <summary>Person 1001's own desk chair, in the office.</summary>
        private static readonly SimulationId OfficeChair = new SimulationId(3101UL);

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
        /// A calm day: the hazard waits to be triggered and nobody presses
        /// anything. Nobody cruel enough to shut and lock a door behind them
        /// either: the bully strolling through the office door and locking
        /// it locks everybody in the corridor out, which is the game, and its
        /// own thing to watch, not what these errands are about.
        /// </summary>
        private ScenarioData CalmDay()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = true;
            data.Exits.EvilCloseMinimum = 11;
            data.Exits.EvilLockMinimum = 11;

            // And nobody takes a fancy to somebody else's desk chair while
            // they are away from it: a chair found taken is its own thing.
            data.Items.SitChancePercent = 0;
            return data;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void AnErrandThroughTheWayOut_IsNotWalledOffByADoorStrolledThroughEarlier()
        {
            // Home time with the way out unlocked late: person 1018 reached
            // the open front door with nobody near and circled in front of it
            // for half a minute. They had strolled through a cafeteria door
            // earlier (its shortcut onto the crossbar, since taken out; the
            // meeting room's door stands in for it here), that was still the
            // one doorway they counted as lined up with, and the wall beside
            // the way out pushed them off it every time they came near.
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(CalmDay());
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 1);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 2);
                int person = IndexOf(simulation, 1018UL);
                Agent walker = simulation.AgentForTests(person);
                walker.Doors.StrollDoorIndex = DoorIndex(simulation, TheBuilding.MeetingRoomToCafeteria);
                simulation.CuesForTests.CallHomeTime(0, 0UL);

                bool walking = false;
                for (int t = 0; t < 10 * Run.TicksPerSecond && !walking; t++)
                {
                    simulation.Step();
                    walking = simulation.GetAgent(person).ActivityState == AgentActivityState.RunningAnErrand;
                }

                Assert.That(walking, Is.True, simulation.DescribeForTests(person));
                Assert.That(walker.DoorwayInUse, Is.EqualTo(AgentDoorMemory.AnyDoorway),
                    "On an errand, any open doorway is theirs to walk through, whatever door they last strolled through.");

                // Standing just inside the open way out, wanting to walk
                // straight out of it: the wall it sits in must not push back.
                WorldGeometry geometry = simulation.GeometryForTests;
                int wayOut = DoorIndex(simulation, TheBuilding.TheWayOut);
                Assert.That(geometry.IsDoorOpen(wayOut), Is.True, "The player opened the way out.");
                LogicalPosition justInside = geometry.DoorCentre(wayOut) + new LogicalPosition(-70, -310);
                long steerX = 0L;
                long steerZ = IntegerMath.TrigScale;
                geometry.AddWallRepulsion(justInside, walker.DoorwayInUse, data.Steering.WallAvoidDistanceMillimetres,
                    data.Calm.WallAvoidPercent, data.Calm.TableAvoidPercent, ref steerX, ref steerZ);
                Assert.That(steerZ, Is.GreaterThan(0L), "The wall beside the open way out pushed them back from it.");
            }
        }

        private static int DoorIndex(Run simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == id)
                {
                    return i;
                }
            }

            Assert.Fail($"No door {id}.");
            return -1;
        }

        private static int IndexOf(Run simulation, ulong agentId)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (simulation.GetAgent(i).AgentId.Value == agentId)
                {
                    return i;
                }
            }

            Assert.Fail($"No person {agentId}.");
            return -1;
        }

        private static int ChairIndexOf(Run simulation, SimulationId chairId)
        {
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).ObjectId == chairId)
                {
                    return i;
                }
            }

            Assert.Fail($"No chair {chairId}.");
            return -1;
        }

        private static bool IsInAStall(LogicalPosition where)
        {
            // The three stalls run the full width of the bathroom's south wall.
            return where.X >= 8000 && where.X <= 13000 && where.Z >= -500 && where.Z <= 1000;
        }

        private static DoorState StateOf(Run simulation, SimulationId doorId)
        {
            RunSnapshot snapshot = simulation.GetSnapshot();
            for (int i = 0; i < snapshot.Doors.Count; i++)
            {
                if (snapshot.Doors[i].DoorId == doorId)
                {
                    return snapshot.Doors[i].State;
                }
            }

            Assert.Fail($"No door {doorId}.");
            return DoorState.Locked;
        }

        [Test]
        public void SentHomeAcrossTheBuilding_SomebodyOpensTheDoorsOnTheWay_AndSitsOnTheirOwnChair()
        {
            ScenarioData data = CalmDay();

            // Person 1019 stands in the bathroom; their desk chair is 1001's,
            // in the office, three doors away. 1001 loses it so it is nobody
            // else's.
            data.Agents[0] = data.Agents[0].WithHome(default(SimulationId));
            data.Agents[18] = data.Agents[18].WithHome(OfficeChair);
            using (var simulation = new Run(data))
            {
                int person = IndexOf(simulation, 1019UL);
                int chair = ChairIndexOf(simulation, OfficeChair);
                simulation.CuesForTests.SendHome(simulation.AgentForTests(person));

                bool seated = false;
                bool bathroomDoorOpened = false;
                bool officeDoorOpened = false;
                for (int t = 0; t < 90 * Run.TicksPerSecond && !seated; t++)
                {
                    simulation.Step();
                    Assert.That(simulation.GetAgent(person).FearState, Is.EqualTo(AgentFearState.Calm), "Nothing frightens them.");
                    Agent agent = simulation.AgentForTests(person);
                    seated = agent.Sitting.OnIt && agent.Sitting.ChairIndex == chair &&
                             agent.Intent.Activity == AgentActivityState.Sitting;
                    bathroomDoorOpened |= StateOf(simulation, TheBuilding.BathroomDoor) == DoorState.Open;
                    officeDoorOpened |= StateOf(simulation, TheBuilding.OfficeDoor) == DoorState.Open;
                }

                Assert.That(seated, Is.True, "They should be sitting on their own chair: " + simulation.DescribeForTests(person));

                // They opened both doors on the way, and shut the office door
                // behind them once through, nobody being near it. (The
                // bathroom door may stay open: the two standing in the
                // bathroom are near enough that it is left for them.)
                Assert.That(bathroomDoorOpened, Is.True, "They opened the bathroom door on the way.");
                Assert.That(officeDoorOpened, Is.True, "And the office door.");
                bool shutBehindThem = false;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    shutBehindThem |= record.EventType == CausalEventType.DoorClosed && record.SourceId.Value == 1019UL &&
                                      record.TargetId == TheBuilding.OfficeDoor;
                }

                Assert.That(shutBehindThem, Is.True, "And shut the office door behind them.");
                Assert.That(StateOf(simulation, TheBuilding.OfficeDoor), Is.EqualTo(DoorState.Unlocked), "Shut, not locked.");
            }
        }

        [Test]
        public void AToiletTrip_ShutsTheStallDoor_StaysAWhile_AndComesBackToTheirDesk()
        {
            ScenarioData data = CalmDay();
            data.Day.ToiletEveryTicks = 0;
            TheBuilding.WithToiletStay(data, 100, 150);
            using (var simulation = new Run(data))
            {
                int person = IndexOf(simulation, 1001UL);
                int chair = ChairIndexOf(simulation, OfficeChair);
                simulation.CuesForTests.StartToiletTrip(simulation.AgentForTests(person));

                int wentIn = 0;
                int cameOut = 0;
                int seated = 0;
                for (int t = 0; t < 120 * Run.TicksPerSecond && seated == 0; t++)
                {
                    simulation.Step();
                    Agent agent = simulation.AgentForTests(person);
                    bool inside = IsInAStall(agent.Body.Position);
                    if (wentIn == 0 && inside)
                    {
                        wentIn = simulation.Tick;
                    }

                    if (wentIn > 0 && cameOut == 0 && !inside)
                    {
                        cameOut = simulation.Tick;
                    }

                    if (cameOut > 0 && agent.Sitting.OnIt && agent.Sitting.ChairIndex == chair &&
                        agent.Intent.Activity == AgentActivityState.Sitting)
                    {
                        seated = simulation.Tick;
                    }
                }

                Assert.That(wentIn, Is.GreaterThan(0), "They never reached a stall: " + simulation.DescribeForTests(person));
                Assert.That(cameOut, Is.GreaterThan(wentIn + 100), "They should stay a while with the door shut.");
                Assert.That(seated, Is.GreaterThan(cameOut), "And come back to their own chair: " + simulation.DescribeForTests(person));

                bool shut = false;
                bool opened = false;
                bool cue = false;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    if (record.SourceId.Value != 1001UL)
                    {
                        continue;
                    }

                    bool aStallDoor = record.TargetId.Value >= 2013UL && record.TargetId.Value <= 2015UL;
                    shut |= record.EventType == CausalEventType.DoorClosed && aStallDoor && record.Tick >= wentIn && record.Tick < cameOut;
                    cue |= record.EventType == CausalEventType.CueCalled && (CueKind)record.Strength == CueKind.ToiletTrip;
                }

                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    bool aStallDoor = record.SourceId.Value >= 2013UL && record.SourceId.Value <= 2015UL;
                    opened |= record.EventType == CausalEventType.DoorOpened && aStallDoor && record.Tick > wentIn + 100;
                }

                Assert.That(cue, Is.True, "Going to the toilet is a line in the story.");
                Assert.That(shut, Is.True, "They shut the stall door behind them.");
                Assert.That(opened, Is.True, "And opened it again to come out.");
            }
        }

        [Test]
        public void FrightenedInTheStall_TheErrandIsDropped()
        {
            ScenarioData data = CalmDay();
            data.Day.ToiletEveryTicks = 0;
            TheBuilding.WithToiletStay(data, 3000, 3000);
            using (var simulation = new Run(data))
            {
                int person = IndexOf(simulation, 1019UL);
                simulation.CuesForTests.StartToiletTrip(simulation.AgentForTests(person));
                bool staying = false;
                for (int t = 0; t < 60 * Run.TicksPerSecond && !staying; t++)
                {
                    simulation.Step();
                    staying = simulation.ErrandForTests(person).Phase == ErrandPhase.Standing;
                }

                Assert.That(staying, Is.True, "They should be in the stall: " + simulation.DescribeForTests(person));
                simulation.FrightenForTests(person);
                Assert.That(simulation.ErrandForTests(person).Has, Is.False, "Fear has its own rules; the errand is gone.");
                Advance(simulation, 5);
                Assert.That(simulation.GetAgent(person).FearState, Is.Not.EqualTo(AgentFearState.Calm));
            }
        }

        [Test]
        public void AChat_HasBothFacingEachOther_ANeighbourGlancing_AndEndsWhenOneIsFrightened()
        {
            ScenarioData data = CalmDay();
            TheBuilding.WithChatLength(data, 1500, 1500);

            // Loud enough that the rest of the office is sure to look over,
            // and talkative enough that both have spoken inside a few seconds.
            data.Day.RemarkHearingRadiusMillimetres = 6000;
            data.Day.RemarkEveryMinimumTicks = 50;
            data.Day.RemarkEveryMaximumTicks = 100;
            using (var simulation = new Run(data))
            {
                int a = IndexOf(simulation, 1005UL);
                int b = IndexOf(simulation, 1006UL);
                LogicalPosition bStood = simulation.GetAgent(b).Position;
                Assert.That(simulation.CuesForTests.StartChat(simulation.AgentForTests(a), simulation.AgentForTests(b)), Is.True);

                bool talking = false;
                for (int t = 0; t < 20 * Run.TicksPerSecond && !talking; t++)
                {
                    simulation.Step();
                    talking = simulation.GetAgent(a).ActivityState == AgentActivityState.Chatting &&
                              simulation.GetAgent(b).ActivityState == AgentActivityState.Chatting &&
                              simulation.GetAgent(a).SpeedMillimetresPerTick == 0;
                }

                Assert.That(talking, Is.True, "Both should be stood talking: " + simulation.DescribeForTests(a) + " / " + simulation.DescribeForTests(b));
                Assert.That(IntegerMath.Distance(bStood, simulation.GetAgent(b).Position), Is.GreaterThan(500L),
                    "The one hailed walks over too; they meet in the middle rather than one being summoned.");

                // A few seconds of talk: close, facing, and saying things. One
                // of them may be glancing at a noise on the tick we look, which
                // is a glance mid-chat, not the end of it: the chat itself holds.
                Advance(simulation, 6 * Run.TicksPerSecond);
                AgentSnapshot one = simulation.GetAgent(a);
                AgentSnapshot other = simulation.GetAgent(b);
                Assert.That(simulation.ErrandForTests(a).Phase, Is.EqualTo(ErrandPhase.Talking), simulation.DescribeForTests(a));
                Assert.That(simulation.ErrandForTests(b).Phase, Is.EqualTo(ErrandPhase.Talking), simulation.DescribeForTests(b));
                Assert.That(IntegerMath.Distance(one.Position, other.Position),
                    Is.LessThanOrEqualTo(data.Calm.SocialStopDistanceMillimetres + 400), "Within arm's reach of each other.");
                int oneToOther = IntegerMath.HeadingBetween(one.Position, other.Position, one.HeadingDegrees);
                int otherToOne = IntegerMath.HeadingBetween(other.Position, one.Position, other.HeadingDegrees);
                Assert.That(System.Math.Abs(IntegerMath.SignedAngleDifference(one.HeadingDegrees, oneToOther)), Is.LessThan(35), "Facing each other.");
                Assert.That(System.Math.Abs(IntegerMath.SignedAngleDifference(other.HeadingDegrees, otherToOne)), Is.LessThan(35), "Facing each other.");

                var remarks = new HashSet<ulong>();
                int glances = 0;
                bool partnersGlanced = false;
                var speakers = new HashSet<ulong>();
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    // Other people chat too; only this pair's remarks count.
                    if (record.EventType == CausalEventType.AgentSaid &&
                        (record.SourceId.Value == 1005UL || record.SourceId.Value == 1006UL))
                    {
                        remarks.Add(record.EventId);
                        speakers.Add(record.SourceId.Value);
                    }

                    if (record.EventType == CausalEventType.AgentNoticedSound && remarks.Contains(record.CausalParentEventId))
                    {
                        glances++;
                        partnersGlanced |= record.SourceId.Value == 1005UL || record.SourceId.Value == 1006UL;
                    }
                }

                Assert.That(speakers.Count, Is.EqualTo(2), "Both of them said something.");
                Assert.That(glances, Is.GreaterThan(0), "Somebody nearby glanced over at the talking.");
                Assert.That(partnersGlanced, Is.False, "The two talking do not wonder what each other's voice was.");

                // One of them is frightened: the other notices a moment later
                // and the chat is over for both.
                simulation.FrightenForTests(a);
                Assert.That(simulation.ErrandForTests(a).Has, Is.False);
                Advance(simulation, data.Perception.ReactionLagMaximumTicks + 2);
                Assert.That(simulation.GetAgent(b).ActivityState, Is.Not.EqualTo(AgentActivityState.Chatting),
                    "Nobody stands talking to somebody who has run off screaming: " + simulation.DescribeForTests(b));
                Assert.That(simulation.ErrandForTests(b).Has, Is.False);
            }
        }

        [Test]
        public void HomeTime_WithTheWayOutOpen_EverybodyLeavesCalmly_AndNobodyIsPaidFor()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(CalmDay());
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 5);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 6);
                simulation.QueueCommand(PlayerCommandType.CallHomeTime, default(SimulationId), 10);

                var setOff = new Dictionary<int, int>();
                int escaped = 0;
                for (int t = 0; t < 150 * Run.TicksPerSecond && escaped < simulation.AgentCount; t++)
                {
                    simulation.Step();
                    escaped = 0;
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot person = simulation.GetAgent(i);
                        escaped += person.Outcome == AgentTerminalOutcome.Escaped ? 1 : 0;
                        if (person.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(person.FearState, Is.EqualTo(AgentFearState.Calm),
                                $"Tick {simulation.Tick}: person {person.AgentId} is frightened on a calm day.");
                        }

                        if (!setOff.ContainsKey(i) && person.ActivityState == AgentActivityState.RunningAnErrand)
                        {
                            setOff[i] = simulation.Tick;
                        }
                    }
                }

                Assert.That(escaped, Is.EqualTo(simulation.AgentCount), "Everybody, visitors included, is out of the building.");
                Assert.That(new HashSet<int>(setOff.Values).Count, Is.GreaterThanOrEqualTo(5),
                    "People set off each in their own time, never the whole building at once.");
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.BeforeEvent), "Nothing has gone wrong, so no round has begun, let alone ended.");
                Assert.That(simulation.InfluenceEarned, Is.Zero, "The purse pays for people saved, not for people who went home.");

                // Setting the disaster off on an empty building ends the round
                // on the spot, with everybody accounted for.
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                Advance(simulation, 3);
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Over));
            }
        }

        [Test]
        public void HomeTime_WithTheWayOutLocked_AQueueFormsAndNobodyPanics()
        {
            ScenarioData data = CalmDay();
            data.Day.PlayerHomeTimeSpreadTicks = 200;
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.CallHomeTime, default(SimulationId), 10);
                Advance(simulation, 50 * Run.TicksPerSecond);

                int atTheDoor = 0;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    AgentSnapshot person = simulation.GetAgent(i);
                    Assert.That(person.Outcome, Is.Not.EqualTo(AgentTerminalOutcome.Escaped), "The way out is locked.");
                    Assert.That(person.FearState, Is.EqualTo(AgentFearState.Calm), $"Person {person.AgentId} is frightened on a calm day.");
                    atTheDoor += IntegerMath.Distance(person.Position, TheBuilding.InsideTheWayOut) <= 5000 ? 1 : 0;
                }

                Assert.That(atTheDoor, Is.GreaterThanOrEqualTo(8), "A queue at the locked front door.");

                bool tried = false;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    tried |= record.EventType == CausalEventType.AgentTriedDoor && record.TargetId == TheBuilding.TheWayOut;
                }

                Assert.That(tried, Is.True, "Somebody tried the handle, which is worth a line in the story.");
            }
        }

        [Test]
        public void HomeTime_WithTheWayOutUnlockedLate_StillEmptiesTheBuilding_WithoutTryingTheHandleAllDay()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(CalmDay());
            data.Day.PlayerHomeTimeSpreadTicks = 200;
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.CallHomeTime, default(SimulationId), 10);

                // The front of the queue gives up on the locked door after
                // half a minute; the player opens it a little after that.
                int opened = 45 * Run.TicksPerSecond;
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, opened);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, opened + 1);

                int escaped = 0;
                for (int t = 0; t < 200 * Run.TicksPerSecond && escaped < simulation.AgentCount; t++)
                {
                    simulation.Step();
                    escaped = 0;
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot person = simulation.GetAgent(i);
                        escaped += person.Outcome == AgentTerminalOutcome.Escaped ? 1 : 0;
                        if (person.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(person.FearState, Is.EqualTo(AgentFearState.Calm),
                                $"Tick {simulation.Tick}: person {person.AgentId} is frightened on a calm day.");
                        }
                    }
                }

                string left = "";
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    if (simulation.GetAgent(i).Outcome != AgentTerminalOutcome.Escaped)
                    {
                        left += simulation.DescribeForTests(i) + "\n";
                    }
                }

                Assert.That(escaped, Is.EqualTo(simulation.AgentCount), "Home time stands: everybody who gave up on the locked door tries again and gets out.\n" + left);

                int tried = 0;
                foreach (CausalEvent record in simulation.GetSnapshot().Events)
                {
                    tried += record.EventType == CausalEventType.AgentTriedDoor && record.TargetId == TheBuilding.TheWayOut ? 1 : 0;
                }

                Assert.That(tried, Is.LessThanOrEqualTo(2 * simulation.AgentCount),
                    "A door found locked is remembered for a while, not tried again every few seconds.");
            }
        }

        [Test]
        public void AGlanceAtANoise_InterruptsAnErrand_WhichThenCarriesOn()
        {
            ScenarioData data = CalmDay();
            data.Agents[0] = data.Agents[0].WithHome(default(SimulationId));
            data.Agents[18] = data.Agents[18].WithHome(OfficeChair);
            using (var simulation = new Run(data))
            {
                int person = IndexOf(simulation, 1019UL);
                int chair = ChairIndexOf(simulation, OfficeChair);
                simulation.CuesForTests.SendHome(simulation.AgentForTests(person));

                // A few seconds into the walk, a thud right beside them.
                bool walking = false;
                for (int t = 0; t < 10 * Run.TicksPerSecond && !walking; t++)
                {
                    simulation.Step();
                    walking = simulation.GetAgent(person).ActivityState == AgentActivityState.RunningAnErrand &&
                              simulation.GetAgent(person).SpeedMillimetresPerTick > 0;
                }

                Assert.That(walking, Is.True, simulation.DescribeForTests(person));
                LogicalPosition beside = simulation.GetAgent(person).Position + new LogicalPosition(1000, 0);
                simulation.MakeANoiseForTests(beside);
                Assert.That(simulation.GetAgent(person).ActivityState, Is.EqualTo(AgentActivityState.Investigating), "They turn to look.");
                Assert.That(simulation.ErrandForTests(person).Has && simulation.ErrandForTests(person).Cue == CueKind.GoHome, Is.True, "But the errand is not forgotten.");

                bool seated = false;
                for (int t = 0; t < 90 * Run.TicksPerSecond && !seated; t++)
                {
                    simulation.Step();
                    Agent agent = simulation.AgentForTests(person);
                    seated = agent.Sitting.OnIt && agent.Sitting.ChairIndex == chair;
                }

                Assert.That(seated, Is.True, "They carry on home after the glance: " + simulation.DescribeForTests(person));
            }
        }
    }
}

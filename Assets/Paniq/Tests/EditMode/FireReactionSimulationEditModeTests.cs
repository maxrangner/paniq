using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEditor;

namespace Paniq.Tests.EditMode
{
    public sealed class FireReactionSimulationEditModeTests
    {
        private const string ScenarioAssetPath = "Assets/Paniq/Content/FireReactionScenario.asset";

        private FireReactionScenario scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = FireReactionScenario.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private FireReactionScenarioData DefaultData() => scenario.ToRuntimeData();

        private static FireReactionAgentDefinition Agent(ulong id, int x, int z, CardinalDirection facing)
        {
            return new FireReactionAgentDefinition(new StableAgentId(id), new LogicalPosition(x, z), facing);
        }

        // ---------------------------------------------------------------- data

        [Test]
        public void DefaultScenario_IsValidWithReplayIdentity()
        {
            Assert.That(scenario.IsValid(out string error), Is.True, error);
            FireReactionScenarioData data = DefaultData();
            Assert.That(data.Agents, Has.Length.EqualTo(10));
            Assert.That(data.DefaultSeed, Is.EqualTo(42UL));
            Assert.That(data.ContentRevision, Is.EqualTo("12"));
            Assert.That(data.SimulationCompatibilityVersion, Is.EqualTo(4));
            Assert.That(data.FireActivationTick, Is.EqualTo(250));
            Assert.That(data.FireCellSizeMillimetres, Is.EqualTo(500));
            Assert.That(data.PanicSpeedMinimum, Is.GreaterThan(data.CalmSpeedMaximum * 2));
            Assert.That(data.PanicSpeedMaximum, Is.LessThanOrEqualTo(data.MaximumStepDistanceMillimetres));
        }

        [Test]
        public void ScenarioAsset_MatchesTheCodeDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<FireReactionScenario>(ScenarioAssetPath);
            Assert.That(asset, Is.Not.Null, $"Missing {ScenarioAssetPath}.");
            Assert.That(asset.IsValid(out string error), Is.True, error);
            FireReactionScenarioData fromAsset = asset.ToRuntimeData();
            FireReactionScenarioData fromCode = DefaultData();
            foreach (var property in typeof(FireReactionScenarioData).GetProperties())
            {
                if (property.Name == nameof(FireReactionScenarioData.Agents))
                {
                    continue;
                }

                Assert.That(property.GetValue(fromAsset), Is.EqualTo(property.GetValue(fromCode)),
                    $"Asset and CreateDefault disagree on {property.Name}.");
            }

            Assert.That(fromAsset.Agents, Is.EqualTo(fromCode.Agents));
        }

        // ---------------------------------------------------------------- maths

        [Test]
        public void Pcg32_UsesTheReferenceVector()
        {
            var random = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xA15C02B7U, 0x7B47F409U, 0xBA1D3330U, 0x83D2F293U, 0xBFA4784BU, 0xCBED606EU };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void IntegerTrig_MatchesFloatingPointWithinOneUnit()
        {
            for (int degrees = -720; degrees <= 720; degrees++)
            {
                double radians = degrees * Math.PI / 180.0;
                Assert.That(IntegerMath.Sin(degrees), Is.EqualTo(Math.Sin(radians) * 10000.0).Within(1.0), $"sin {degrees}");
                Assert.That(IntegerMath.Cos(degrees), Is.EqualTo(Math.Cos(radians) * 10000.0).Within(1.0), $"cos {degrees}");
            }
        }

        [Test]
        public void IntegerHeading_RoundTripsEveryWholeDegree()
        {
            for (int heading = 0; heading < 360; heading++)
            {
                LogicalPosition direction = IntegerMath.Direction(heading);
                int measured = IntegerMath.HeadingOf(direction.X * 7L, direction.Z * 7L, -1);
                Assert.That(Math.Abs(IntegerMath.SignedAngleDifference(heading, measured)), Is.LessThanOrEqualTo(1),
                    $"heading {heading} measured as {measured}");
            }

            Assert.That(IntegerMath.HeadingOf(0, 5, 0), Is.EqualTo(0));
            Assert.That(IntegerMath.HeadingOf(5, 0, 0), Is.EqualTo(90));
            Assert.That(IntegerMath.HeadingOf(0, -5, 0), Is.EqualTo(180));
            Assert.That(IntegerMath.HeadingOf(-5, 0, 0), Is.EqualTo(270));
        }

        [Test]
        public void SweptContact_IsCorrectForDiagonalMoves()
        {
            var start = new LogicalPosition(0, 0);
            var end = new LogicalPosition(1000, 1000);

            // About 70.7 mm from the diagonal.
            Assert.That(IntegerMath.SegmentPassesWithin(start, end, new LogicalPosition(500, 600), 100L * 100L), Is.True);
            Assert.That(IntegerMath.SegmentPassesWithin(start, end, new LogicalPosition(500, 600), 50L * 50L), Is.False);

            // 707 mm from the diagonal; a bounding-box test would wrongly say 0.
            Assert.That(IntegerMath.SegmentPassesWithin(start, end, new LogicalPosition(1000, 0), 500L * 500L), Is.False);

            // Beyond the end of the segment only the end point counts.
            Assert.That(IntegerMath.SegmentPassesWithin(start, end, new LogicalPosition(1300, 1300), 400L * 400L), Is.False);

            // Exact touching is allowed.
            Assert.That(IntegerMath.SegmentPassesWithin(start, start, new LogicalPosition(500, 0), 500L * 500L), Is.False);
        }

        // ---------------------------------------------------------------- fire

        [Test]
        public void Fire_StartsAsOneCellAtTick250()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            for (int i = 0; i < 249; i++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireActive, Is.False);
            Assert.That(simulation.FireCellCount, Is.EqualTo(0));
            simulation.Step();
            Assert.That(simulation.FireActive, Is.True);
            FireReactionSnapshot snapshot = simulation.GetSnapshot();
            Assert.That(snapshot.FireCells.Count, Is.EqualTo(1));
            Assert.That(snapshot.FireCells[0].Centre, Is.EqualTo(simulation.FireOrigin));
            Assert.That(simulation.EventLog.Get(snapshot.FireCells[0].EventId).EventType,
                Is.EqualTo(FireReactionEventType.FireActivated));
        }

        [Test]
        public void Fire_OnlySpreadsFromCellsAlreadyBurning()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            for (int i = 0; i < 2500; i++)
            {
                simulation.Step();
            }

            IReadOnlyList<FireCellSnapshot> cells = simulation.GetSnapshot().FireCells;
            Assert.That(cells.Count, Is.GreaterThan(50));
            var cellByEvent = new Dictionary<ulong, FireCellSnapshot>();
            for (int i = 0; i < cells.Count; i++)
            {
                FireCellSnapshot cell = cells[i];
                CausalEvent ignition = simulation.EventLog.Get(cell.EventId);
                if (i == 0)
                {
                    Assert.That(ignition.EventType, Is.EqualTo(FireReactionEventType.FireActivated));
                }
                else
                {
                    Assert.That(ignition.EventType, Is.EqualTo(FireReactionEventType.FireSpread));
                    Assert.That(cellByEvent.TryGetValue(ignition.CausalParentEventId, out FireCellSnapshot parent), Is.True,
                        $"Cell {i} was lit by something other than an earlier burning cell.");
                    Assert.That(Math.Abs(parent.CellX - cell.CellX) + Math.Abs(parent.CellZ - cell.CellZ), Is.EqualTo(1),
                        $"Cell {i} is not a direct neighbour of the cell that lit it.");
                    Assert.That(parent.IgnitionTick, Is.LessThan(cell.IgnitionTick));
                }

                cellByEvent.Add(cell.EventId, cell);
            }
        }

        [Test]
        public void Fire_FillsTheRoomWithinAMinute()
        {
            FireReactionScenarioData data = DefaultData();
            data.FireActivationTick = 1;
            var simulation = new FireReactionSimulation(data);
            int totalCells = simulation.FireGridColumns * simulation.FireGridRows;
            Assert.That(totalCells, Is.EqualTo(24 * 24));
            for (int i = 0; i < 60 * FireReactionSimulation.TicksPerSecond && simulation.FireCellCount < totalCells; i++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.EqualTo(totalCells));
        }

        // ---------------------------------------------------------------- replay and space

        [Test]
        public void FixedSeedRuns_ProduceIdenticalStateAndEvents()
        {
            var first = new FireReactionSimulation(DefaultData());
            var second = new FireReactionSimulation(DefaultData());
            for (int tick = 0; tick < 3000; tick++)
            {
                first.Step();
                second.Step();
                for (int i = 0; i < first.AgentCount; i++)
                {
                    FireReactionAgentSnapshot a = first.GetAgent(i);
                    FireReactionAgentSnapshot b = second.GetAgent(i);
                    Assert.That(a.Position, Is.EqualTo(b.Position));
                    Assert.That(a.HeadingDegrees, Is.EqualTo(b.HeadingDegrees));
                    Assert.That(a.SpeedMillimetresPerTick, Is.EqualTo(b.SpeedMillimetresPerTick));
                    Assert.That(a.FearState, Is.EqualTo(b.FearState));
                    Assert.That(a.ActivityState, Is.EqualTo(b.ActivityState));
                    Assert.That(a.Outcome, Is.EqualTo(b.Outcome));
                }
            }

            Assert.That(first.FireCellCount, Is.EqualTo(second.FireCellCount));
            Assert.That(first.EventLog.Count, Is.EqualTo(second.EventLog.Count));
            for (int i = 0; i < first.EventLog.Count; i++)
            {
                CausalEvent a = first.EventLog.Events[i];
                CausalEvent b = second.EventLog.Events[i];
                Assert.That(a.EventType, Is.EqualTo(b.EventType));
                Assert.That(a.Tick, Is.EqualTo(b.Tick));
                Assert.That(a.SourceId, Is.EqualTo(b.SourceId));
                Assert.That(a.Position, Is.EqualTo(b.Position));
                Assert.That(a.CausalParentEventId, Is.EqualTo(b.CausalParentEventId));
            }
        }

        [Test]
        public void AlternateSeed_ChangesTheRun()
        {
            var first = new FireReactionSimulation(DefaultData(), 42UL);
            var second = new FireReactionSimulation(DefaultData(), 43UL);
            bool differs = false;
            for (int tick = 0; tick < 500 && !differs; tick++)
            {
                first.Step();
                second.Step();
                for (int i = 0; i < first.AgentCount; i++)
                {
                    differs |= !first.GetAgent(i).Position.Equals(second.GetAgent(i).Position);
                }
            }

            Assert.That(differs, Is.True);
        }

        [Test]
        public void Movement_KeepsParticipatingAgentsInsideTheRoomAndSeparated()
        {
            FireReactionScenarioData data = DefaultData();
            var simulation = new FireReactionSimulation(data);
            long touching = data.OccupancyRadiusMillimetres * 2L;
            for (int tick = 0; tick < 3000; tick++)
            {
                simulation.Step();
                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                for (int i = 0; i < snapshot.Agents.Count; i++)
                {
                    FireReactionAgentSnapshot agent = snapshot.Agents[i];
                    if (agent.Participation != AgentParticipation.Participating)
                    {
                        continue;
                    }

                    Assert.That(data.RoomBounds.ContainsCircle(agent.Position, data.OccupancyRadiusMillimetres), Is.True,
                        $"Agent {agent.AgentId} left the room at tick {snapshot.Tick}.");
                    for (int j = 0; j < i; j++)
                    {
                        FireReactionAgentSnapshot other = snapshot.Agents[j];
                        if (other.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(LogicalPosition.DistanceSquared(agent.Position, other.Position),
                                Is.GreaterThanOrEqualTo(touching * touching),
                                $"Agents {agent.AgentId} and {other.AgentId} overlapped at tick {snapshot.Tick}.");
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- calm behaviour

        [Test]
        public void CalmAgents_WanderOnCurvedPathsPauseAndAvoidWalls()
        {
            FireReactionScenarioData data = DefaultData();
            data.FireActivationTick = int.MaxValue;
            var simulation = new FireReactionSimulation(data);
            int count = simulation.AgentCount;
            var paused = new bool[count];
            var wasMoving = new bool[count];
            var travelled = new long[count];
            var previous = new LogicalPosition[count];
            var wallRun = new int[count];
            var activities = new HashSet<AgentActivityState>();
            int movingSamples = 0;
            int offGridSamples = 0;
            long speedTotal = 0;
            for (int i = 0; i < count; i++)
            {
                previous[i] = simulation.GetAgent(i).Position;
            }

            for (int tick = 0; tick < 60 * FireReactionSimulation.TicksPerSecond; tick++)
            {
                simulation.Step();
                for (int i = 0; i < count; i++)
                {
                    FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                    activities.Add(agent.ActivityState);
                    Assert.That(agent.SpeedMillimetresPerTick, Is.LessThanOrEqualTo(agent.CalmSpeedMillimetresPerTick));
                    bool moving = agent.SpeedMillimetresPerTick > 0;
                    if (moving)
                    {
                        movingSamples++;
                        speedTotal += agent.SpeedMillimetresPerTick;
                        offGridSamples += agent.HeadingDegrees % 90 != 0 ? 1 : 0;
                    }

                    paused[i] |= wasMoving[i] && !moving;
                    wasMoving[i] = moving;
                    travelled[i] += (long)Math.Sqrt(LogicalPosition.DistanceSquared(previous[i], agent.Position));
                    previous[i] = agent.Position;

                    // Footprint edge within 0.3 m of any wall.
                    LogicalBounds room = data.RoomBounds;
                    int gap = Math.Min(
                        Math.Min(agent.Position.X - room.MinX, room.MaxX - agent.Position.X),
                        Math.Min(agent.Position.Z - room.MinZ, room.MaxZ - agent.Position.Z)) - data.OccupancyRadiusMillimetres;
                    wallRun[i] = gap < 300 ? wallRun[i] + 1 : 0;
                    Assert.That(wallRun[i], Is.LessThan(3 * FireReactionSimulation.TicksPerSecond),
                        $"Calm agent {agent.AgentId} hugged a wall for 3 s.");
                }
            }

            for (int i = 0; i < count; i++)
            {
                Assert.That(paused[i], Is.True, $"Agent {i} never paused.");
                Assert.That(travelled[i], Is.GreaterThan(3000), $"Agent {i} barely moved.");
            }

            Assert.That(offGridSamples, Is.GreaterThan(movingSamples / 2), "Headings are still mostly compass-aligned.");
            double averageSpeed = speedTotal / (double)movingSamples;
            Assert.That(averageSpeed, Is.InRange(data.CalmSpeedMinimum * 0.6, data.CalmSpeedMaximum));
            Assert.That(activities, Is.SupersetOf(new[]
            {
                AgentActivityState.Standing,
                AgentActivityState.LookingAround,
                AgentActivityState.Strolling,
                AgentActivityState.Socialising
            }));
        }

        // ---------------------------------------------------------------- panic behaviour

        [Test]
        public void PanickedAgents_SprintZigZagAndDoNotStayPinned()
        {
            FireReactionScenarioData data = DefaultData();
            var simulation = new FireReactionSimulation(data);
            int count = simulation.AgentCount;
            var lastHeading = new int[count];
            var lastPosition = new LogicalPosition[count];
            var stillTicks = new int[count];
            long calmSpeedTotal = 0;
            long calmSamples = 0;
            long scaredSpeedTotal = 0;
            long scaredSamples = 0;
            int bigTurns = 0;
            int headingSamples = 0;

            // The first 20 seconds after the fire starts, while open space remains.
            int endTick = data.FireActivationTick + 20 * FireReactionSimulation.TicksPerSecond;
            while (simulation.Tick < endTick)
            {
                simulation.Step();
                int tick = simulation.Tick;
                for (int i = 0; i < count; i++)
                {
                    FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                    if (agent.Participation != AgentParticipation.Participating)
                    {
                        continue;
                    }

                    if (agent.FearState == AgentFearState.Calm && agent.SpeedMillimetresPerTick > 0)
                    {
                        calmSpeedTotal += agent.SpeedMillimetresPerTick;
                        calmSamples++;
                    }

                    // Frozen, staggering and fallen people are meant to stand still,
                    // and so are people opening, rattling or forcing a door.
                    bool fleeing = agent.FearState == AgentFearState.Scared &&
                                   agent.BodyState == AgentBodyState.Upright &&
                                   agent.ActivityState != AgentActivityState.Frozen &&
                                   agent.ActivityState != AgentActivityState.OpeningDoor &&
                                   agent.ActivityState != AgentActivityState.TryingDoor &&
                                   agent.ActivityState != AgentActivityState.ForcingDoor;
                    if (!fleeing)
                    {
                        stillTicks[i] = 0;
                    }
                    else
                    {
                        scaredSpeedTotal += agent.SpeedMillimetresPerTick;
                        scaredSamples++;
                        if (tick % 10 == 0)
                        {
                            bigTurns += Math.Abs(IntegerMath.SignedAngleDifference(lastHeading[i], agent.HeadingDegrees)) > 30 ? 1 : 0;
                            headingSamples++;
                        }

                        stillTicks[i] = agent.Position.Equals(lastPosition[i]) ? stillTicks[i] + 1 : 0;
                        Assert.That(stillTicks[i], Is.LessThanOrEqualTo(75),
                            $"Panicked agent {agent.AgentId} stood pinned for 1.5 s at tick {tick}.");
                    }

                    if (tick % 10 == 0)
                    {
                        lastHeading[i] = agent.HeadingDegrees;
                    }

                    lastPosition[i] = agent.Position;
                }
            }

            Assert.That(scaredSamples, Is.GreaterThan(30 * FireReactionSimulation.TicksPerSecond),
                "Too few agents ran in panic to judge.");
            double calmSpeed = calmSpeedTotal / (double)calmSamples;
            double scaredSpeed = scaredSpeedTotal / (double)scaredSamples;
            Assert.That(scaredSpeed, Is.GreaterThanOrEqualTo(calmSpeed * 2.5),
                $"Panic speed {scaredSpeed:0.0} is not much faster than calm {calmSpeed:0.0} mm/tick.");

            // Headings are sampled five times a second; count sharp changes per panicked second.
            double sharpTurnsPerSecond = bigTurns / (headingSamples / 5.0);
            Assert.That(sharpTurnsPerSecond, Is.GreaterThan(0.5), "Panicked agents run in straight lines.");
        }

        // ---------------------------------------------------------------- perception and causes

        [Test]
        public void Vision_SeesFireAheadButNotBehind()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[] { Agent(1UL, 0, 0, CardinalDirection.East) };
            data.FireActivationTick = 1;
            data.MaximumReactionDelayTicks = 0;
            data.FireSpawnBounds = new LogicalBounds(2100, 2100, 100, 100);
            data.FreezeThenRunPercent = 0;
            data.FreezeForeverPercent = 0;
            data.TripChancePercent = 0;

            var simulation = new FireReactionSimulation(data);
            simulation.Step();
            Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Alert));
            Assert.That(simulation.GetAgent(0).AlertSource, Is.EqualTo(AgentAlertSource.Visual));

            for (int i = 0; i < 50; i++)
            {
                simulation.Step();
            }

            FireReactionAgentSnapshot fled = simulation.GetAgent(0);
            Assert.That(fled.FearState, Is.EqualTo(AgentFearState.Scared));
            long startGap = (long)Math.Sqrt(LogicalPosition.DistanceSquared(new LogicalPosition(0, 0), simulation.FireOrigin));
            long endGap = (long)Math.Sqrt(LogicalPosition.DistanceSquared(fled.Position, simulation.FireOrigin));
            Assert.That(endGap, Is.GreaterThan(startGap + 500), "The agent did not run away from the fire in front of it.");

            data.FireSpawnBounds = new LogicalBounds(-2600, -2600, 100, 100);
            simulation = new FireReactionSimulation(data);
            simulation.Step();
            Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm));
        }

        [Test]
        public void VisualAlert_ProducesYellAndPropagatesReactionWithCausalParents()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                Agent(1UL, 0, 0, CardinalDirection.East),
                Agent(2UL, 1000, 0, CardinalDirection.North)
            };
            data.FireActivationTick = 1;
            data.MaximumReactionDelayTicks = 0;
            data.FireSpawnBounds = new LogicalBounds(2100, 2100, 100, 100);
            data.YellRadiusMillimetres = 1500;

            var simulation = new FireReactionSimulation(data);
            simulation.Step();

            Assert.That(simulation.GetAgent(0).AlertSource, Is.EqualTo(AgentAlertSource.Visual));
            Assert.That(simulation.GetAgent(1).AlertSource, Is.EqualTo(AgentAlertSource.Yell));
            IReadOnlyList<CausalEvent> events = simulation.EventLog.Events;
            Assert.That(events[0].EventType, Is.EqualTo(FireReactionEventType.FireActivated));
            Assert.That(events[1].EventType, Is.EqualTo(FireReactionEventType.AgentAlerted));
            Assert.That(events[1].CausalParentEventId, Is.EqualTo(events[0].EventId));
            Assert.That(events[2].EventType, Is.EqualTo(FireReactionEventType.AgentYelled));
            Assert.That(events[2].CausalParentEventId, Is.EqualTo(events[1].EventId));
            Assert.That(events[3].EventType, Is.EqualTo(FireReactionEventType.AgentAlerted));
            Assert.That(events[3].SourceId, Is.EqualTo(new StableAgentId(2UL)));
            Assert.That(events[3].CausalParentEventId, Is.EqualTo(events[2].EventId));
        }

        [Test]
        public void LostAgents_NameTheFireCellThatCaughtThem()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            for (int i = 0; i < 60 * FireReactionSimulation.TicksPerSecond; i++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetSnapshot().LostCount, Is.GreaterThan(0));
            int lostEvents = 0;
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType != FireReactionEventType.AgentLost)
                {
                    continue;
                }

                lostEvents++;
                FireReactionEventType parent = simulation.EventLog.Get(record.CausalParentEventId).EventType;
                Assert.That(parent == FireReactionEventType.FireActivated || parent == FireReactionEventType.FireSpread,
                    Is.True, $"Agent {record.SourceId} was lost with parent {parent}.");
            }

            Assert.That(lostEvents, Is.EqualTo(simulation.GetSnapshot().LostCount));
            for (int i = 1; i < simulation.EventLog.Count; i++)
            {
                Assert.That(simulation.EventLog.Events[i].HasCausalParent, Is.True,
                    $"Event {i} ({simulation.EventLog.Events[i].EventType}) has no cause.");
            }
        }

        // ---------------------------------------------------------------- hearing

        [Test]
        public void FireCrackle_TurnsSomeoneWithTheirBackToItUntilTheySeeIt()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[] { Agent(1UL, 0, 0, CardinalDirection.East) };
            data.FireActivationTick = 1;
            data.MaximumReactionDelayTicks = 0;

            // The burning cell's nearest edge is 3.5 m behind: audible, but beyond the 3 m sight range.
            data.FireSpawnBounds = new LogicalBounds(-3700, -3700, 100, 100);

            var simulation = new FireReactionSimulation(data);
            simulation.Step();
            FireReactionAgentSnapshot first = simulation.GetAgent(0);
            Assert.That(first.FearState, Is.EqualTo(AgentFearState.Calm));
            Assert.That(first.ActivityState, Is.EqualTo(AgentActivityState.Investigating));
            CausalEvent noticed = simulation.EventLog.Events[1];
            Assert.That(noticed.EventType, Is.EqualTo(FireReactionEventType.AgentNoticedSound));
            Assert.That(noticed.CausalParentEventId, Is.EqualTo(simulation.FireActivationEventId));

            for (int i = 0; i < 5 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).FearState == AgentFearState.Calm; i++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                "The agent heard the fire but never turned to see it.");
            Assert.That(simulation.GetAgent(0).AlertSource, Is.EqualTo(AgentAlertSource.Visual));
        }

        [Test]
        public void Yells_AlarmNearbyAndTurnHeadsFurtherAway()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                Agent(1UL, 0, 0, CardinalDirection.East),
                Agent(2UL, -4000, 0, CardinalDirection.West),
                Agent(3UL, 0, -2000, CardinalDirection.South)
            };
            data.FireActivationTick = 1;
            data.MaximumReactionDelayTicks = 0;
            data.FireSpawnBounds = new LogicalBounds(2100, 2100, 100, 100);

            var simulation = new FireReactionSimulation(data);
            simulation.Step();

            ulong yellId = 0UL;
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == FireReactionEventType.AgentYelled)
                {
                    yellId = record.EventId;
                    Assert.That(record.StrengthMillimetres, Is.EqualTo(data.YellHearingRadiusMillimetres));
                }
            }

            Assert.That(yellId, Is.Not.EqualTo(0UL), "The detector did not yell.");
            FireReactionAgentSnapshot far = simulation.GetAgent(1);
            FireReactionAgentSnapshot near = simulation.GetAgent(2);
            Assert.That(far.FearState, Is.EqualTo(AgentFearState.Calm), "A yell 4 m away should only draw attention.");
            Assert.That(far.ActivityState, Is.EqualTo(AgentActivityState.Investigating));
            Assert.That(near.FearState, Is.Not.EqualTo(AgentFearState.Calm), "A yell 2 m away should alarm.");
            Assert.That(near.AlertSource, Is.EqualTo(AgentAlertSource.Yell));
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.SourceId == new StableAgentId(2UL))
                {
                    Assert.That(record.EventType, Is.EqualTo(FireReactionEventType.AgentNoticedSound));
                    Assert.That(record.CausalParentEventId, Is.EqualTo(yellId));
                }
                else if (record.SourceId == new StableAgentId(3UL) && record.EventType == FireReactionEventType.AgentAlerted)
                {
                    Assert.That(record.CausalParentEventId, Is.EqualTo(yellId));
                }
            }

            // Turning from west to east toward the yell takes well under a second.
            bool farTurned = false;
            for (int i = 0; i < 30; i++)
            {
                simulation.Step();
                farTurned |= Math.Abs(IntegerMath.SignedAngleDifference(simulation.GetAgent(1).HeadingDegrees, 90)) <= 10;
            }

            Assert.That(farTurned, Is.True, "The distant listener never turned toward the yell.");
        }

        [Test]
        public void YellAlarmedListener_TurnsTowardTheYellerWhileStartled()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                Agent(1UL, 0, 0, CardinalDirection.East),
                Agent(2UL, 0, -2000, CardinalDirection.West)
            };
            data.FireActivationTick = 1;
            data.FireSpawnBounds = new LogicalBounds(2100, 2100, 100, 100);

            // Reaction delays are seeded; try a few seeds so at least one
            // listener stays startled long enough to finish turning.
            data.MaximumReactionDelayTicks = 200;
            bool turned = false;
            for (ulong seed = 1UL; seed <= 10UL && !turned; seed++)
            {
                var run = new FireReactionSimulation(data, seed);
                for (int i = 0; i < 40; i++)
                {
                    run.Step();
                    FireReactionAgentSnapshot listener = run.GetAgent(1);
                    if (listener.FearState != AgentFearState.Alert || listener.AlertSource != AgentAlertSource.Yell)
                    {
                        continue;
                    }

                    // The yeller stood due north of the listener.
                    turned |= Math.Abs(IntegerMath.SignedAngleDifference(listener.HeadingDegrees, 0)) <= 10;
                }
            }

            Assert.That(turned, Is.True, "An alarmed listener never turned toward the yeller.");
        }

        // ---------------------------------------------------------------- temperaments

        [Test]
        public void Temperaments_RunFreezeForAWhileOrFreezeForGood()
        {
            FireReactionScenarioData data = DefaultData();
            var crowd = new List<FireReactionAgentDefinition>();
            ulong id = 1UL;
            for (int z = -5000; z <= 5000; z += 2500)
            {
                for (int x = -5000; x <= 5000; x += 2000)
                {
                    crowd.Add(Agent(id, x, z, (CardinalDirection)(id % 4UL)));
                    id++;
                }
            }

            data.Agents = crowd.ToArray();
            var simulation = new FireReactionSimulation(data);
            var temperaments = new HashSet<AgentPanicTemperament>();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                temperaments.Add(simulation.GetAgent(i).Temperament);
            }

            Assert.That(temperaments, Is.EquivalentTo(new[]
            {
                AgentPanicTemperament.Runner,
                AgentPanicTemperament.FreezeThenRun,
                AgentPanicTemperament.FreezeForever
            }));

            int count = simulation.AgentCount;
            var frozenAt = new LogicalPosition?[count];
            int endTick = data.FireActivationTick + 40 * FireReactionSimulation.TicksPerSecond;
            while (simulation.Tick < endTick)
            {
                simulation.Step();
                for (int i = 0; i < count; i++)
                {
                    FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                    if (agent.Temperament != AgentPanicTemperament.FreezeForever ||
                        agent.Participation != AgentParticipation.Participating)
                    {
                        continue;
                    }

                    if (agent.ActivityState == AgentActivityState.Frozen)
                    {
                        frozenAt[i] ??= agent.Position;
                        Assert.That(agent.Position, Is.EqualTo(frozenAt[i].Value),
                            $"Permanently frozen agent {agent.AgentId} moved.");
                    }
                    else
                    {
                        Assert.That(frozenAt[i].HasValue, Is.False,
                            $"Permanently frozen agent {agent.AgentId} snapped out of it.");
                    }
                }
            }

            var froze = new Dictionary<StableAgentId, CausalEvent>();
            var lostTick = new Dictionary<StableAgentId, int>();
            var unfroze = new Dictionary<StableAgentId, CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                switch (record.EventType)
                {
                    case FireReactionEventType.AgentFroze:
                        froze[record.SourceId] = record;
                        break;
                    case FireReactionEventType.AgentUnfroze:
                        unfroze[record.SourceId] = record;
                        Assert.That(record.CausalParentEventId, Is.EqualTo(froze[record.SourceId].EventId));
                        break;
                    case FireReactionEventType.AgentLost:
                        lostTick[record.SourceId] = record.Tick;
                        break;
                }
            }

            int thawed = 0;
            foreach (KeyValuePair<StableAgentId, CausalEvent> pair in froze)
            {
                AgentPanicTemperament temperament = simulation.GetAgent(pair.Key).Temperament;
                Assert.That(temperament, Is.Not.EqualTo(AgentPanicTemperament.Runner));
                if (temperament == AgentPanicTemperament.FreezeForever)
                {
                    Assert.That(unfroze.ContainsKey(pair.Key), Is.False);
                    continue;
                }

                int deadline = pair.Value.Tick + data.FreezeMaximumTicks;
                bool lostFirst = lostTick.TryGetValue(pair.Key, out int lost) && lost <= deadline;
                if (!lostFirst && deadline < simulation.Tick)
                {
                    Assert.That(unfroze.TryGetValue(pair.Key, out CausalEvent thaw), Is.True,
                        $"Agent {pair.Key} stayed frozen past its freeze time.");
                    Assert.That(thaw.Tick, Is.LessThanOrEqualTo(deadline));
                    thawed++;
                }
            }

            Assert.That(thawed, Is.GreaterThan(0), "Nobody froze and then ran.");
        }

        // ---------------------------------------------------------------- collisions and falls

        [Test]
        public void PanickedCrowds_BumpKnockDownTripAndGetBackUp()
        {
            int collisions = 0;
            int knockdowns = 0;
            int trips = 0;
            for (ulong seed = 1UL; seed <= 20UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                var simulation = new FireReactionSimulation(data, seed);
                int count = simulation.AgentCount;
                var previous = new FireReactionAgentSnapshot[count];
                var downTicks = new int[count];
                int longestDown = Math.Max(data.KnockdownMaximumTicks, data.TripMaximumTicks) + data.GetUpTicks + 1;
                long touching = data.OccupancyRadiusMillimetres * 2L;
                int endTick = data.FireActivationTick + 30 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Tick < endTick)
                {
                    simulation.Step();
                    for (int i = 0; i < count; i++)
                    {
                        FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                        if (agent.Participation != AgentParticipation.Participating)
                        {
                            continue;
                        }

                        // Someone can finish staggering, take a step and be bumped again in one
                        // tick, but nobody gets from the floor to their feet and back that fast.
                        if (agent.IsDown && agent.BodyState == previous[i].BodyState)
                        {
                            Assert.That(agent.Position, Is.EqualTo(previous[i].Position),
                                $"Seed {seed}: agent {agent.AgentId} moved while not on its feet.");
                        }

                        downTicks[i] = agent.IsDown ? downTicks[i] + 1 : 0;
                        Assert.That(downTicks[i], Is.LessThanOrEqualTo(longestDown),
                            $"Seed {seed}: agent {agent.AgentId} never got back up.");

                        for (int j = 0; j < i; j++)
                        {
                            FireReactionAgentSnapshot other = simulation.GetAgent(j);
                            if (other.Participation == AgentParticipation.Participating)
                            {
                                Assert.That(LogicalPosition.DistanceSquared(agent.Position, other.Position),
                                    Is.GreaterThanOrEqualTo(touching * touching),
                                    $"Seed {seed}: agents overlapped at tick {simulation.Tick}.");
                            }
                        }

                        previous[i] = agent;
                    }
                }

                CausalEventLog log = simulation.EventLog;
                var knockdownsPerCollision = new Dictionary<ulong, int>();
                foreach (CausalEvent record in log.Events)
                {
                    switch (record.EventType)
                    {
                        case FireReactionEventType.AgentsCollided:
                            collisions++;
                            knockdownsPerCollision[record.EventId] = 0;
                            Assert.That(log.Get(record.CausalParentEventId).EventType,
                                Is.EqualTo(FireReactionEventType.AgentScared));
                            break;
                        case FireReactionEventType.AgentKnockedDown:
                            knockdowns++;
                            knockdownsPerCollision[record.CausalParentEventId]++;
                            break;
                        case FireReactionEventType.AgentTripped:
                            trips++;
                            break;
                        case FireReactionEventType.AgentGotUp:
                            FireReactionEventType cause = log.Get(record.CausalParentEventId).EventType;
                            Assert.That(cause == FireReactionEventType.AgentKnockedDown ||
                                        cause == FireReactionEventType.AgentTripped, Is.True,
                                $"Seed {seed}: got up after {cause}.");
                            break;
                    }
                }

                foreach (int pair in knockdownsPerCollision.Values)
                {
                    Assert.That(pair == 0 || pair == 2, Is.True,
                        $"Seed {seed}: a collision knocked down {pair} people; it should be none or both.");
                }
            }

            Assert.That(collisions, Is.GreaterThan(0), "Panicked people never ran into each other.");
            Assert.That(knockdowns, Is.GreaterThan(0), "No collision was hard enough to knock anyone down.");
            Assert.That(trips, Is.GreaterThan(0), "Nobody ever tripped.");
        }
    }
}

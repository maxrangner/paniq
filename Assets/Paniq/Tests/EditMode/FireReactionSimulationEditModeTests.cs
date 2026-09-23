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

        private FireReactionScenarioData DefaultData() =>
            TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());

        private static FireReactionAgentDefinition Agent(ulong id, int x, int z, CardinalDirection facing)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing);
        }

        // ---------------------------------------------------------------- data

        [Test]
        public void DefaultScenario_IsValidWithReplayIdentity()
        {
            Assert.That(scenario.IsValid(out string error), Is.True, error);
            FireReactionScenarioData data = DefaultData();
            Assert.That(data.Agents, Has.Length.EqualTo(20));
            Assert.That(data.DefaultSeed, Is.EqualTo(42UL));
            Assert.That(data.ContentRevision, Is.EqualTo("54"));
            Assert.That(data.SimulationCompatibilityVersion, Is.EqualTo(42));
            Assert.That(data.Fire.ActivationTick, Is.EqualTo(250));
            Assert.That(data.Fire.CellSizeMillimetres, Is.EqualTo(500));
            Assert.That(data.Panic.SpeedMinimum - data.Traits.PanicSpeedJitter,
                Is.GreaterThan((data.Calm.SpeedMaximum + data.Traits.CalmSpeedJitter) * 3 / 2),
                "Even the slowest sprinter clearly outruns the fastest walker.");
            Assert.That(data.Panic.SpeedMaximum + data.Traits.PanicSpeedJitter,
                Is.LessThanOrEqualTo(data.World.MaximumStepDistanceMillimetres));
        }

        [Test]
        public void ScenarioAsset_MatchesTheCodeDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<FireReactionScenario>(ScenarioAssetPath);
            Assert.That(asset, Is.Not.Null, $"Missing {ScenarioAssetPath}.");
            Assert.That(asset.IsValid(out string error), Is.True, error);
            // The untouched defaults, not this class's DefaultData: that one
            // pins the fire to the office so the crowd tests always have a fire
            // where the crowd is, and comparing against it would say the saved
            // asset was wrong about the very thing it is right about.
            int compared = AssertSameValues(asset.ToRuntimeData(), scenario.ToRuntimeData(), "scenario");
            Assert.That(compared, Is.GreaterThan(100), "The comparison walked too few values; it may have stopped finding the settings.");
        }

        /// <summary>Walks every field, descending into settings groups, and returns how many values it compared.</summary>
        private static int AssertSameValues(object fromAsset, object fromCode, string path)
        {
            int compared = 0;
            foreach (System.Reflection.FieldInfo field in fromAsset.GetType().GetFields())
            {
                object assetValue = field.GetValue(fromAsset);
                object codeValue = field.GetValue(fromCode);
                string fieldPath = $"{path}.{field.Name}";
                if (field.FieldType.IsClass && field.FieldType != typeof(string) && !field.FieldType.IsArray)
                {
                    compared += AssertSameValues(assetValue, codeValue, fieldPath);
                    continue;
                }

                // An array of settings objects (the per-kind table): compare entry by entry.
                if (field.FieldType.IsArray && field.FieldType.GetElementType()?.IsClass == true &&
                    field.FieldType.GetElementType() != typeof(string))
                {
                    var assetEntries = (Array)assetValue;
                    var codeEntries = (Array)codeValue;
                    Assert.That(assetEntries?.Length, Is.EqualTo(codeEntries?.Length),
                        $"Asset and code defaults disagree on how many {fieldPath} there are.");
                    for (int i = 0; assetEntries != null && i < assetEntries.Length; i++)
                    {
                        compared += AssertSameValues(assetEntries.GetValue(i), codeEntries.GetValue(i), $"{fieldPath}[{i}]");
                    }

                    continue;
                }

                Assert.That(assetValue, Is.EqualTo(codeValue), $"Asset and code defaults disagree on {fieldPath}.");
                compared++;
            }

            return compared;
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

        /// <summary>
        /// The same scenario with nobody brave or kind enough to pick up an
        /// extinguisher, for tests about the fire itself.
        /// </summary>
        private FireReactionScenarioData NobodyFightsTheFire()
        {
            FireReactionScenarioData data = DefaultData();
            data.Extinguishers.FightMinimumBravery = 11;
            data.Extinguishers.SaveMinimumCompassion = 11;

            // And nobody sends anyone else at it either.
            data.Leadership.LeaderMinimum = 11;
            return data;
        }

        [Test]
        public void Fire_OnlySpreadsFromCellsAlreadyBurning()
        {
            var simulation = new FireReactionSimulation(NobodyFightsTheFire());
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
                    FireReactionEventType cause = simulation.EventLog.Get(ignition.CausalParentEventId).EventType;
                    if (cause == FireReactionEventType.ObjectCaughtFire ||
                        cause == FireReactionEventType.ObjectExploded)
                    {
                        // Lit by a burning box, chair or table resting on it, or
                        // scattered by something electrical going off. Both are
                        // those things' own rules, not the fire spreading.
                        cellByEvent.Add(cell.EventId, cell);
                        continue;
                    }

                    Assert.That(cellByEvent.TryGetValue(ignition.CausalParentEventId, out FireCellSnapshot parent), Is.True,
                        $"Cell {i} was lit by something other than an earlier burning cell.");
                    Assert.That(Math.Abs(parent.CellX - cell.CellX) + Math.Abs(parent.CellZ - cell.CellZ), Is.EqualTo(1),
                        $"Cell {i} is not a direct neighbour of the cell that lit it.");
                    Assert.That(parent.IgnitionTick, Is.LessThan(cell.IgnitionTick));
                }

                cellByEvent.Add(cell.EventId, cell);
            }
        }

        /// <summary>The room whose walls hold this position, or the first room.</summary>
        private static LogicalBounds RoomHolding(FireReactionScenarioData data, LogicalPosition position)
        {
            foreach (FireReactionRoomDefinition room in data.Rooms)
            {
                if (room.Bounds.ContainsCircle(position, data.World.OccupancyRadiusMillimetres))
                {
                    return room.Bounds;
                }
            }

            return data.Rooms[0].Bounds;
        }

        /// <summary>Whether someone is standing in a doorway, between two rooms.</summary>
        private static bool InADoorway(FireReactionSnapshot snapshot, LogicalPosition position)
        {
            foreach (FireReactionDoorSnapshot door in snapshot.Doors)
            {
                if (LogicalPosition.DistanceSquared(position, door.Centre) < 1000L * 1000L)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a whole body stands inside one of the building's rooms.</summary>
        private static bool InAnyRoom(FireReactionScenarioData data, LogicalPosition position)
        {
            foreach (FireReactionRoomDefinition room in data.Rooms)
            {
                if (room.Bounds.ContainsCircle(position, data.World.OccupancyRadiusMillimetres))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void Fire_FillsTheRoomWithinAMinute()
        {
            FireReactionScenarioData data = NobodyFightsTheFire();
            data.Fire.ActivationTick = 1;
            var simulation = new FireReactionSimulation(data);
            // The office is 24 x 24 squares of the fire's half-metre grid.
            int officeCells = 24 * 24;

            // The rest of the floor -- the closet, the corridor and its arm,
            // the cafeteria, the meeting room, the bathroom, its three stalls
            // and the maintenance room -- is a good deal more again. The exact
            // total is not pinned here: it is a property of the floor plan,
            // and a test about the fire filling a room should not fail because
            // somebody moved a wall.
            Assert.That(simulation.FireFloorCellCount, Is.GreaterThan(officeCells),
                "The building is bigger than the office it starts in.");
            for (int i = 0; i < 60 * FireReactionSimulation.TicksPerSecond && simulation.FireCellCount < officeCells; i++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.GreaterThanOrEqualTo(officeCells),
                "The office should be full of fire within a minute.");
        }

        /// <summary>
        /// The fire only checks nearby grid cells to answer "where is the
        /// nearest fire", "is fire this close" and "can I see fire". Each
        /// answer must equal checking every burning cell, ties included, for
        /// small, medium and large fires and for points outside the room.
        /// </summary>
        [TestCase(260)]
        [TestCase(900)]
        [TestCase(1800)]
        public void FireQueries_MatchCheckingEveryBurningCell(int ticks)
        {
            var simulation = new FireReactionSimulation(NobodyFightsTheFire());
            for (int i = 0; i < ticks; i++)
            {
                simulation.Step();
            }

            IReadOnlyList<FireCellSnapshot> cells = simulation.GetSnapshot().FireCells;
            FireSystem fire = simulation.FireForTests;
            for (int x = -7000; x <= 7000; x += 350)
            {
                for (int z = -7000; z <= 7000; z += 350)
                {
                    var position = new LogicalPosition(x, z);

                    // Nearest: lowest distance, then earliest-lit cell.
                    long expected = long.MaxValue;
                    ulong expectedEvent = 0UL;
                    LogicalPosition expectedPoint = position;
                    foreach (FireCellSnapshot cell in cells)
                    {
                        if (cell.IsOut)
                        {
                            continue;
                        }

                        LogicalPosition point = cell.Bounds.ClosestPoint(position);
                        long distance = LogicalPosition.DistanceSquared(position, point);
                        if (distance < expected || (distance == expected && cell.EventId < expectedEvent))
                        {
                            expected = distance;
                            expectedEvent = cell.EventId;
                            expectedPoint = point;
                        }
                    }

                    long actual = fire.NearestDistanceSquared(position, out LogicalPosition actualPoint, out int actualCell);
                    Assert.That(actual, Is.EqualTo(expected), $"nearest distance from {position}");
                    Assert.That(actualPoint, Is.EqualTo(expectedPoint), $"nearest point from {position}");
                    Assert.That(fire.CellEventId(actualCell), Is.EqualTo(expectedEvent), $"nearest cell from {position}");

                    foreach (int reach in new[] { 500, 1000, 1500, 2600 })
                    {
                        bool anyCloser = false;
                        foreach (FireCellSnapshot cell in cells)
                        {
                            anyCloser |= !cell.IsOut && cell.Bounds.DistanceSquaredTo(position) < (long)reach * reach;
                        }

                        Assert.That(fire.AnyCloserThan(position, reach), Is.EqualTo(anyCloser), $"fire within {reach} of {position}");
                    }

                    // Sight is checked inside the office, where the fire is;
                    // walls hiding fire from the other rooms is covered by the
                    // room tests.
                    LogicalBounds office = simulation.Scenario.Rooms[0].Bounds;
                    if (position.X <= office.MinX || position.X >= office.MaxX ||
                        position.Z <= office.MinZ || position.Z >= office.MaxZ)
                    {
                        continue;
                    }

                    for (int heading = 0; heading < 360; heading += 45)
                    {
                        Assert.That(fire.IsVisibleFrom(position, heading, 3000),
                            Is.EqualTo(SeesAnyCell(cells, position, heading, 3000, simulation.GeometryForTests)),
                            $"vision from {position} facing {heading}");
                    }
                }
            }
        }

        /// <summary>The vision rule, checked against every burning cell: nearest point, centre or a corner inside a 90-degree cone.</summary>
        private static bool SeesAnyCell(IReadOnlyList<FireCellSnapshot> cells, LogicalPosition eye, int heading,
            int range, WorldGeometry geometry)
        {
            long rangeSquared = (long)range * range;
            LogicalPosition direction = IntegerMath.Direction(heading);
            int eyeRoom = geometry.RoomAtPoint(eye);
            foreach (FireCellSnapshot cell in cells)
            {
                if (cell.IsOut)
                {
                    continue;
                }

                // A wall hides fire. This used to be left out, because all the
                // fire was in the office and the check was only made from
                // inside it -- an assumption that stopped holding the moment
                // fire could reach another room by the time this runs.
                int cellRoom = geometry.RoomAtPoint(cell.Bounds.Centre);
                if (eyeRoom >= 0 && cellRoom >= 0 && !geometry.RoomsOpenToEachOther(eyeRoom, cellRoom))
                {
                    continue;
                }

                LogicalBounds b = cell.Bounds;
                LogicalPosition closest = b.ClosestPoint(eye);
                if (LogicalPosition.DistanceSquared(eye, closest) > rangeSquared)
                {
                    continue;
                }

                foreach (LogicalPosition point in new[]
                         {
                             closest, b.Centre, new LogicalPosition(b.MinX, b.MinZ), new LogicalPosition(b.MaxX, b.MinZ),
                             new LogicalPosition(b.MinX, b.MaxZ), new LogicalPosition(b.MaxX, b.MaxZ)
                         })
                {
                    long dx = (long)point.X - eye.X;
                    long dz = (long)point.Z - eye.Z;
                    long forward = dx * direction.X + dz * direction.Z;
                    long lateral = dx * direction.Z - dz * direction.X;
                    if (dx * dx + dz * dz <= rangeSquared && forward >= 0L && Math.Abs(lateral) <= forward)
                    {
                        return true;
                    }
                }
            }

            return false;
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

            // A sealed room: nobody may break a door down here.
            data.Traits.DoorDamagePerPoint = 0;
            var simulation = new FireReactionSimulation(data);
            // Bodies give a little: in a packed, shoving crowd two people on
                // their feet may press a few centimetres into each other, and
                // no further. People knocked down can end up in a heap, one
                // sprawled across another, which is a pile, not an overlap.
                long touching = data.World.OccupancyRadiusMillimetres * 2L - 50L;
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

                    Assert.That(InAnyRoom(data, agent.Position) || InADoorway(snapshot, agent.Position), Is.True,
                        $"Agent {agent.AgentId} left the building at tick {snapshot.Tick}.");
                    for (int j = 0; j < i; j++)
                    {
                        FireReactionAgentSnapshot other = snapshot.Agents[j];
                        if (other.Participation == AgentParticipation.Participating && !agent.IsDown && !other.IsDown)
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
            data.Fire.ActivationTick = int.MaxValue;

            // Nobody sits down here: this is about how people walk about, and
            // sitting is covered by its own tests. The meeting that starts
            // seated breaks up at once, so they walk about like everyone else.
            data.Items.SitChancePercent = 0;
            data.Items.SeatedAtStartTicks = 1;
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

                    // Footprint edge within 0.3 m of any wall of the room they
                    // are in. Somebody who has stopped on purpose -- standing,
                    // looking about, or stood talking to somebody -- is not
                    // hugging the wall, they are standing near one, which people
                    // do. This is about walking: drifting along a wall, or being
                    // pinned against one while trying to get somewhere.
                    bool standingOnPurpose = agent.ActivityState == AgentActivityState.Standing ||
                                             agent.ActivityState == AgentActivityState.LookingAround ||
                                             agent.ActivityState == AgentActivityState.Socialising;
                    LogicalBounds room = RoomHolding(data, agent.Position);
                    int gap = Math.Min(
                        Math.Min(agent.Position.X - room.MinX, room.MaxX - agent.Position.X),
                        Math.Min(agent.Position.Z - room.MinZ, room.MaxZ - agent.Position.Z)) - data.World.OccupancyRadiusMillimetres;
                    wallRun[i] = gap < 300 && !standingOnPurpose ? wallRun[i] + 1 : 0;
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
            Assert.That(averageSpeed, Is.InRange(data.Calm.SpeedMinimum * 0.6, data.Calm.SpeedMaximum));
            Assert.That(activities, Is.SupersetOf(new[]
            {
                AgentActivityState.Standing,
                AgentActivityState.LookingAround,
                AgentActivityState.Strolling,
                AgentActivityState.Socialising
            }));
        }

        // ---------------------------------------------------------------- panic behaviour

        /// <summary>
        /// A body is moved once per tick, so nobody on their feet turns faster
        /// than the fastest turn rate. Seeds 40 and 46 once broke this when a
        /// runner stopped trying a door and turned twice in one tick.
        /// </summary>
        [TestCase(40UL)]
        [TestCase(46UL)]
        public void UprightPeople_NeverTurnFasterThanTheirTurnRate(ulong seed)
        {
            FireReactionScenarioData data = DefaultData();
            var simulation = new FireReactionSimulation(data, seed);
            var before = new FireReactionAgentSnapshot[simulation.AgentCount];
            for (int i = 0; i < before.Length; i++)
            {
                before[i] = simulation.GetAgent(i);
            }

            for (int tick = 0; tick < 3000; tick++)
            {
                simulation.Step();
                for (int i = 0; i < before.Length; i++)
                {
                    FireReactionAgentSnapshot now = simulation.GetAgent(i);
                    if (before[i].BodyState == AgentBodyState.Upright && now.BodyState == AgentBodyState.Upright)
                    {
                        int turn = Math.Abs(IntegerMath.SignedAngleDifference(before[i].HeadingDegrees, now.HeadingDegrees));
                        Assert.That(turn, Is.LessThanOrEqualTo(data.Panic.TurnRateMaximum),
                            $"Agent {now.AgentId} turned {turn} degrees at tick {simulation.Tick} " +
                            $"({before[i].ActivityState} to {now.ActivityState}).");
                    }

                    before[i] = now;
                }
            }
        }

        [Test]
        public void PanickedAgents_SprintZigZagAndDoNotStayPinned()
        {
            FireReactionScenarioData data = DefaultData();

            // A sealed room: nobody may break a door down here.
            data.Traits.DoorDamagePerPoint = 0;
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
            int endTick = data.Fire.ActivationTick + 20 * FireReactionSimulation.TicksPerSecond;
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

                    // Frozen, staggering and fallen people are meant to stand
                    // still, and so are people working a door handle or
                    // crouching over someone they are helping.
                    bool fleeing = agent.FearState == AgentFearState.Scared &&
                                   agent.BodyState == AgentBodyState.Upright &&
                                   agent.ActivityState != AgentActivityState.Frozen &&
                                   agent.ActivityState != AgentActivityState.OpeningDoor &&
                                   agent.ActivityState != AgentActivityState.TryingDoor &&
                                   agent.ActivityState != AgentActivityState.ForcingDoor &&
                                   agent.ActivityState != AgentActivityState.ShakingAwake &&
                                   agent.ActivityState != AgentActivityState.Grabbing &&
                                   agent.ActivityState != AgentActivityState.Spraying &&
                                   agent.ActivityState != AgentActivityState.FetchingExtinguisher;
                    if (!fleeing)
                    {
                        stillTicks[i] = 0;
                    }
                    else
                    {
                        // Counted the same way as the calm pace above: how
                        // fast they move while moving, not counting the ticks
                        // they spend stopped by a wall or by each other.
                        if (agent.SpeedMillimetresPerTick > 0)
                        {
                            scaredSpeedTotal += agent.SpeedMillimetresPerTick;
                            scaredSamples++;
                        }
                        if (tick % 10 == 0)
                        {
                            bigTurns += Math.Abs(IntegerMath.SignedAngleDifference(lastHeading[i], agent.HeadingDegrees)) > 30 ? 1 : 0;
                            headingSamples++;
                        }

                        // Waiting your turn in the queue at a doorway is not
                        // being pinned, it is the whole point of one way out.
                        // FireReactionRoomsEditModeTests owns that case and
                        // holds it to its own, looser limit; what this guards
                        // against is somebody stuck against a wall in open floor.
                        bool queueing = FireReactionRoomsEditModeTests.NearAnOpenDoor(simulation, agent.Position);
                        stillTicks[i] = agent.Position.Equals(lastPosition[i]) && !queueing ? stillTicks[i] + 1 : 0;
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
            // A way out of the office, so somebody who sees the fire has
            // somewhere to run from it.
            FireReactionScenarioData data = FireReactionDoorsEditModeTests.WithAWayOutOfTheOffice(DefaultData());
            data.Agents = new[] { Agent(1UL, 0, 0, CardinalDirection.East) };
            data.Fire.ActivationTick = 1;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Fire.SpawnBounds = new LogicalBounds(2100, 2100, 100, 100);
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Falls.TripChancePercent = 0;

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

            data.Fire.SpawnBounds = new LogicalBounds(-2600, -2600, 100, 100);
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
            data.Fire.ActivationTick = 1;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Fire.SpawnBounds = new LogicalBounds(2100, 2100, 100, 100);
            data.Hearing.YellAlarmRadiusMillimetres = 1500;

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
            Assert.That(events[3].SourceId, Is.EqualTo(new SimulationId(2UL)));
            Assert.That(events[3].CausalParentEventId, Is.EqualTo(events[2].EventId));
        }

        [Test]
        public void LostAgents_TraceBackThroughTheFlamesToABurningSquare()
        {
            FireReactionScenarioData data = NobodyFightsTheFire();

            // Somebody standing exactly where the fire starts, so there is
            // always a death to trace back however well the rest get out. This
            // test is about the trail of causes behind a death, not about how
            // deadly the building is.
            //
            // The flames go up on them at once rather than after the usual five
            // seconds. They used to be lit five seconds in, by which time this
            // person -- calm, and free to stroll like anybody else -- had often
            // wandered off the spot, and whether the run killed anybody at all
            // came down to luck. Any change to what a frightened crowd does
            // could take the death away and leave this test with nothing to
            // trace.
            //
            // The fire is pinned to one named square rather than left to the
            // spawn area, and the person put on that square. Standing them at
            // the middle of the area was not the same thing: the fire is drawn
            // from the seed anywhere inside it, so whether they were in the
            // flames at all depended on where that draw landed -- and any
            // change that shifts the run's randomness, as adding wayfinding
            // did, moves the fire off them and the death disappears.
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, TheBuilding.Office);
            var people = new List<FireReactionAgentDefinition>(data.Agents)
            {
                new FireReactionAgentDefinition(new SimulationId(1999UL), TheBuilding.Office,
                    CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Agents = people.ToArray();
            var simulation = new FireReactionSimulation(data);
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
                CausalEvent caught = simulation.EventLog.Get(record.CausalParentEventId);
                Assert.That(caught.EventType, Is.EqualTo(FireReactionEventType.AgentCaughtFire),
                    $"Agent {record.SourceId} was lost without catching fire first.");
                Assert.That(caught.SourceId, Is.EqualTo(record.SourceId));
                Assert.That(record.Tick - caught.Tick, Is.EqualTo(caught.DurationTicks), "They burn for the drawn time.");

                // Set alight by a burning square, a burning thing, or someone else who was on fire,
                // and so on back to a square.
                CausalEvent cause = simulation.EventLog.Get(caught.CausalParentEventId);
                while (cause.EventType == FireReactionEventType.AgentCaughtFire ||
                       cause.EventType == FireReactionEventType.ObjectCaughtFire)
                {
                    cause = simulation.EventLog.Get(cause.CausalParentEventId);
                }

                Assert.That(cause.EventType == FireReactionEventType.FireActivated || cause.EventType == FireReactionEventType.FireSpread,
                    Is.True, $"Agent {record.SourceId}'s flames trace back to {cause.EventType}.");
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
            data.Fire.ActivationTick = 1;
            data.Perception.MaximumReactionDelayTicks = 0;

            // The burning cell's nearest edge is 3.5 m behind: audible, but beyond the 3 m sight range.
            data.Fire.SpawnBounds = new LogicalBounds(-3700, -3700, 100, 100);

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
            data.Fire.ActivationTick = 1;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Fire.SpawnBounds = new LogicalBounds(2100, 2100, 100, 100);

            var simulation = new FireReactionSimulation(data);
            simulation.Step();

            ulong yellId = 0UL;
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == FireReactionEventType.AgentYelled)
                {
                    yellId = record.EventId;
                    Assert.That(record.Strength, Is.EqualTo(data.Hearing.YellHearingRadiusMillimetres));
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
                if (record.SourceId == new SimulationId(2UL))
                {
                    Assert.That(record.EventType, Is.EqualTo(FireReactionEventType.AgentNoticedSound));
                    Assert.That(record.CausalParentEventId, Is.EqualTo(yellId));
                }
                else if (record.SourceId == new SimulationId(3UL) && record.EventType == FireReactionEventType.AgentAlerted)
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
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(2100, 2100, 100, 100);

            // Reaction delays are seeded; try a few seeds so at least one
            // listener stays startled long enough to finish turning.
            data.Perception.MaximumReactionDelayTicks = 200;
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

            // A bare room, so the grid of people fits, and nobody who shakes the
            // frozen awake. Nobody cruel enough to heave them aside either:
            // this test is about never moving of one's own accord, and being
            // shoved is somebody else's doing (see the shoving tests).
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Falls.ShoveMinimumEvil = AgentTraitValues.Maximum + 1;
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];

            // A fire that starts and does not grow. This test is about who
            // freezes and who comes out of it, and in a packed room with no way
            // out a spreading fire reaches the frozen before their freeze is
            // over -- which proves nothing about temperaments either way.
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;

            // And nobody wandering off next door, for the same reason.
            data.Calm.StrollNextDoorPercent = 0;

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
            // Long enough that a freeze which starts late still has time to end
            // inside the run; otherwise there is nothing to count.
            int endTick = data.Fire.ActivationTick + 90 * FireReactionSimulation.TicksPerSecond;
            while (simulation.Tick < endTick)
            {
                simulation.Step();
                for (int i = 0; i < count; i++)
                {
                    FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                    if (agent.Temperament != AgentPanicTemperament.FreezeForever ||
                        agent.Participation != AgentParticipation.Participating || agent.IsBurning)
                    {
                        // Even the frozen run once they are on fire. Forget where
                        // they were rooted, too: if the flames are put out they
                        // freeze again, but somewhere else entirely.
                        frozenAt[i] = null;
                        continue;
                    }

                    if (agent.ActivityState == AgentActivityState.Frozen)
                    {
                        // Frozen to the spot, give or take being jostled by the
                        // people running past. Eighty centimetres rather than
                        // sixty: the building now funnels everybody down one
                        // corridor, so somebody rooted in a doorway takes a far
                        // harder shoving than they did in two big rooms. It is
                        // still nothing like walking away, which is what this
                        // is guarding against.
                        frozenAt[i] ??= agent.Position;
                        Assert.That(LogicalPosition.DistanceSquared(agent.Position, frozenAt[i].Value),
                            Is.LessThanOrEqualTo(800L * 800L),
                            $"Permanently frozen agent {agent.AgentId} moved.");
                    }
                    else
                    {
                        Assert.That(frozenAt[i].HasValue, Is.False,
                            $"Permanently frozen agent {agent.AgentId} snapped out of it.");
                    }
                }
            }

            var froze = new Dictionary<SimulationId, CausalEvent>();
            var lostTick = new Dictionary<SimulationId, int>();
            var unfroze = new Dictionary<SimulationId, CausalEvent>();
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
                    case FireReactionEventType.AgentCaughtFire:
                        lostTick[record.SourceId] = record.Tick;
                        break;
                }
            }

            int thawed = 0;
            foreach (KeyValuePair<SimulationId, CausalEvent> pair in froze)
            {
                AgentPanicTemperament temperament = simulation.GetAgent(pair.Key).Temperament;
                Assert.That(temperament, Is.Not.EqualTo(AgentPanicTemperament.Runner));
                if (temperament == AgentPanicTemperament.FreezeForever)
                {
                    Assert.That(unfroze.ContainsKey(pair.Key), Is.False);
                    continue;
                }

                // Someone knocked to the floor while frozen snaps out of it once back on their feet.
                int longestDown = data.Falls.UnconsciousMaximumTicks + data.Falls.ComeToGetUpTicks;
                int deadline = pair.Value.Tick + data.Temperament.FreezeMaximumTicks + longestDown;
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

        private static bool SomeoneIsDragging(FireReactionSimulation simulation)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (simulation.GetAgent(i).ActivityState == AgentActivityState.Dragging)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- collisions and falls

        [Test]
        public void PanickedCrowds_BumpKnockDownTripAndGetBackUp()
        {
            int collisions = 0;
            int knockdowns = 0;
            int trips = 0;
            // Eight seeds, not twenty. This one checks invariants on every
            // tick of every run -- nobody overlapping, nobody sliding about on
            // the floor -- rather than asking whether something happens
            // sometimes, so eight runs is still tens of thousands of checks and
            // a violation has nowhere to hide. Twenty took twenty-five seconds,
            // an eighth of the whole suite, for the same answer.
            for (ulong seed = 1UL; seed <= 8UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                var simulation = new FireReactionSimulation(data, seed);
                int count = simulation.AgentCount;
                var previous = new FireReactionAgentSnapshot[count];
                var downTicks = new int[count];
                int longestDown = Math.Max(
                    Math.Max(
                        Math.Max(data.Falls.KnockdownMaximumTicks, data.Falls.TripMaximumTicks),

                        // Somebody alight can also put themselves on the floor.
                        data.Fire.RollMaximumTicks) + data.Falls.GetUpTicks,
                    data.Falls.UnconsciousMaximumTicks + data.Falls.ComeToGetUpTicks) + 1;
                // Bodies give a little: in a packed, shoving crowd two people on
                // their feet may press a few centimetres into each other, and
                // no further. People knocked down can end up in a heap, one
                // sprawled across another, which is a pile, not an overlap.
                long touching = data.World.OccupancyRadiusMillimetres * 2L - 50L;
                int endTick = data.Fire.ActivationTick + 30 * FireReactionSimulation.TicksPerSecond;
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
                        // (Someone knocked out can be dragged along by a helper.)
                        // Somebody on the floor goes nowhere by themselves. They
                        // can be shoved along by the crowd, or slide on from the
                        // knock that floored them, but never at more than a
                        // stumble's pace.
                        if (agent.IsDown && agent.BodyState == previous[i].BodyState && !SomeoneIsDragging(simulation))
                        {
                            Assert.That(LogicalPosition.DistanceSquared(agent.Position, previous[i].Position),
                                Is.LessThanOrEqualTo(150L * 150L),
                                $"Seed {seed}: agent {agent.AgentId} moved too fast while not on its feet.");
                        }

                        // One spell on the floor at a time. Somebody hauled up
                        // and knocked straight down again in a crush is two
                        // spells, not one that never ended, and in a real crush
                        // that happens -- so the count starts again whenever the
                        // body changes what it is doing.
                        downTicks[i] = agent.IsDown && agent.BodyState == previous[i].BodyState
                            ? downTicks[i] + 1
                            : 0;
                        Assert.That(downTicks[i], Is.LessThanOrEqualTo(longestDown),
                            $"Seed {seed}: agent {agent.AgentId} never got back up.");

                        for (int j = 0; j < i; j++)
                        {
                            FireReactionAgentSnapshot other = simulation.GetAgent(j);
                            if (other.Participation == AgentParticipation.Participating && !agent.IsDown && !other.IsDown)
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
                                Is.EqualTo(FireReactionEventType.AgentScared).Or.EqualTo(FireReactionEventType.AgentCaughtFire));
                            break;
                        case FireReactionEventType.AgentKnockedDown:
                            knockdowns++;

                            // A jet of water floors people too; only the ones a
                            // collision caused are counted against that collision.
                            if (knockdownsPerCollision.ContainsKey(record.CausalParentEventId))
                            {
                                knockdownsPerCollision[record.CausalParentEventId]++;
                            }

                            break;
                        case FireReactionEventType.AgentTripped:
                            trips++;
                            break;
                        case FireReactionEventType.AgentGotUp:
                            FireReactionEventType cause = log.Get(record.CausalParentEventId).EventType;
                            Assert.That(cause == FireReactionEventType.AgentKnockedDown ||
                                        cause == FireReactionEventType.AgentTripped ||
                                        cause == FireReactionEventType.AgentCrushed ||
                                        cause == FireReactionEventType.AgentCameTo ||

                                        // Somebody alight who threw themselves down
                                        // to roll gets up the same way.
                                        cause == FireReactionEventType.AgentRolled, Is.True,
                                $"Seed {seed}: got up after {cause}.");
                            break;
                    }
                }

                foreach (KeyValuePair<ulong, int> pair in knockdownsPerCollision)
                {
                    if (pair.Value == 1)
                    {
                        // Only a much stronger person stays up when the other is floored.
                        CausalEvent collision = log.Get(pair.Key);
                        int gap = Math.Abs(simulation.GetAgent(collision.SourceId).Traits.Strength -
                                           simulation.GetAgent(collision.TargetId).Traits.Strength);
                        Assert.That(gap, Is.GreaterThanOrEqualTo(data.Traits.StrengthShrugOffGap),
                            $"Seed {seed}: a collision floored only one of two people of similar strength.");
                        continue;
                    }

                    Assert.That(pair.Value == 0 || pair.Value == 2, Is.True,
                        $"Seed {seed}: a collision knocked down {pair.Value} people; it should be none, one or both.");
                }
            }

            Assert.That(collisions, Is.GreaterThan(0), "Panicked people never ran into each other.");
            Assert.That(knockdowns, Is.GreaterThan(0), "No collision was hard enough to knock anyone down.");
            Assert.That(trips, Is.GreaterThan(0), "Nobody ever tripped.");
        }
    }
}

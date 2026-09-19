using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    public sealed class FireReactionDoorsEditModeTests
    {
        private static readonly SimulationId NorthDoor = new SimulationId(2001UL);

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
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing);
        }

        private static FireReactionDoorSnapshot Door(FireReactionSimulation simulation, SimulationId id)
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

        private static void Click(FireReactionSimulation simulation, SimulationId door)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, door, simulation.Tick + 1);
        }

        private static void OpenEveryDoor(FireReactionSimulation simulation)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                SimulationId id = simulation.GetDoor(i).DoorId;
                simulation.QueueCommand(PlayerCommandType.ClickDoor, id, 1);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, id, 2);
            }
        }

        private static List<CausalEvent> EventsOfType(FireReactionSimulation simulation, FireReactionEventType type)
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

        // ---------------------------------------------------------------- clicks

        [Test]
        public void DefaultRoom_HasOneLockedDoorInEveryWall()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            Assert.That(simulation.DoorCount, Is.EqualTo(4));
            var sides = new HashSet<WallSide>();
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                FireReactionDoorSnapshot door = simulation.GetDoor(i);
                Assert.That(door.State, Is.EqualTo(DoorState.Locked));
                sides.Add(door.Side);
            }

            Assert.That(sides, Is.EquivalentTo(new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West }));
        }

        [Test]
        public void DoorClicks_UnlockThenOpenThenCloseThenOpenAgain()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            Click(simulation, NorthDoor);
            simulation.Step();
            Assert.That(Door(simulation, NorthDoor).State, Is.EqualTo(DoorState.Unlocked));
            List<CausalEvent> unlocked = EventsOfType(simulation, FireReactionEventType.DoorUnlocked);
            Assert.That(unlocked, Has.Count.EqualTo(1));
            Assert.That(unlocked[0].SourceId, Is.EqualTo(NorthDoor));
            Assert.That(unlocked[0].HasCausalParent, Is.False, "A player click is a root cause.");

            Click(simulation, NorthDoor);
            simulation.Step();
            Assert.That(Door(simulation, NorthDoor).State, Is.EqualTo(DoorState.Open));
            List<CausalEvent> opened = EventsOfType(simulation, FireReactionEventType.DoorOpened);
            Assert.That(opened, Has.Count.EqualTo(1));
            Assert.That(opened[0].CausalParentEventId, Is.EqualTo(unlocked[0].EventId));

            Click(simulation, NorthDoor);
            simulation.Step();
            Assert.That(Door(simulation, NorthDoor).State, Is.EqualTo(DoorState.Unlocked), "A third click closes it.");
            List<CausalEvent> closed = EventsOfType(simulation, FireReactionEventType.DoorClosed);
            Assert.That(closed, Has.Count.EqualTo(1));
            Assert.That(closed[0].HasCausalParent, Is.False, "The player closing a door is a root cause.");
            Assert.That(closed[0].TargetId, Is.EqualTo(NorthDoor));

            Click(simulation, NorthDoor);
            simulation.Step();
            Assert.That(Door(simulation, NorthDoor).State, Is.EqualTo(DoorState.Open));
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorOpened), Has.Count.EqualTo(2));
            Assert.That(simulation.Commands, Has.Count.EqualTo(4));
        }

        [Test]
        public void Commands_CannotTargetStartedTicksOrUnknownDoors()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            simulation.Step();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, simulation.Tick));
            Assert.Throws<ArgumentException>(() =>
                simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(1001UL), simulation.Tick + 1));
        }

        [Test]
        public void ReplayingTheSameClicks_GivesTheSameRun()
        {
            var first = new FireReactionSimulation(DefaultData());
            for (int t = 0; t < 1500; t++)
            {
                if (first.Tick == 300 || first.Tick == 330)
                {
                    Click(first, new SimulationId(2002UL));
                }

                if (first.Tick == 400)
                {
                    Click(first, new SimulationId(2003UL));
                    Click(first, new SimulationId(2003UL));
                }

                first.Step();
            }

            var second = new FireReactionSimulation(DefaultData());
            foreach (PlayerCommand command in first.Commands)
            {
                second.QueueCommand(command.CommandType, command.TargetId, command.TargetTick);
            }

            for (int t = 0; t < 1500; t++)
            {
                second.Step();
            }

            Assert.That(second.EventLog.Count, Is.EqualTo(first.EventLog.Count));
            for (int i = 0; i < first.EventLog.Count; i++)
            {
                CausalEvent a = first.EventLog.Events[i];
                CausalEvent b = second.EventLog.Events[i];
                Assert.That(b.EventType, Is.EqualTo(a.EventType), $"Event {i}");
                Assert.That(b.Tick, Is.EqualTo(a.Tick), $"Event {i}");
                Assert.That(b.SourceId, Is.EqualTo(a.SourceId), $"Event {i}");
                Assert.That(b.Position, Is.EqualTo(a.Position), $"Event {i}");
                Assert.That(b.CausalParentEventId, Is.EqualTo(a.CausalParentEventId), $"Event {i}");
            }

            for (int i = 0; i < first.AgentCount; i++)
            {
                Assert.That(second.GetAgent(i).Position, Is.EqualTo(first.GetAgent(i).Position));
            }
        }

        // ---------------------------------------------------------------- locked and open

        [Test]
        public void LockedDoors_KeepEveryoneInsideButGetTried()
        {
            int tried = 0;
            int forcedOrGaveUp = 0;
            for (ulong seed = 40UL; seed <= 44UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();

                // Nobody here is strong enough to break a door down.
                data.Traits.DoorDamagePerPoint = 0;
                var simulation = new FireReactionSimulation(data, seed);
                for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                        if (agent.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(data.World.RoomBounds.ContainsCircle(agent.Position, data.World.OccupancyRadiusMillimetres), Is.True,
                                $"Seed {seed}: agent {agent.AgentId} got through a locked door at tick {simulation.Tick}.");
                        }
                    }
                }

                Assert.That(simulation.GetSnapshot().EscapedCount, Is.EqualTo(0));
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    switch (record.EventType)
                    {
                        case FireReactionEventType.AgentTriedDoor:
                            tried++;
                            Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                                Is.EqualTo(FireReactionEventType.AgentScared));
                            break;
                        case FireReactionEventType.AgentForcedDoor:
                        case FireReactionEventType.AgentGaveUpOnDoor:
                            forcedOrGaveUp++;
                            Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                                Is.EqualTo(FireReactionEventType.AgentTriedDoor));
                            break;
                        case FireReactionEventType.DoorOpened:
                        case FireReactionEventType.AgentEscaped:
                            Assert.Fail($"Seed {seed}: {record.EventType} with every door locked.");
                            break;
                    }
                }
            }

            Assert.That(tried, Is.GreaterThan(0), "Nobody tried a locked door.");
            Assert.That(forcedOrGaveUp, Is.GreaterThan(0), "Nobody forced or gave up on a locked door.");
        }

        [Test]
        public void OpenDoors_LetPanickedPeopleEscape()
        {
            int escaped = 0;
            for (ulong seed = 40UL; seed <= 44UL; seed++)
            {
                FireReactionScenarioData data = DefaultData();
                var simulation = new FireReactionSimulation(data, seed);
                OpenEveryDoor(simulation);
                long touching = data.World.OccupancyRadiusMillimetres * 2L;
                int endTick = data.Fire.ActivationTick + 30 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Tick < endTick)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                        if (agent.Participation != AgentParticipation.Participating)
                        {
                            continue;
                        }

                        Assert.That(IsInRoomOrDoorway(simulation, data, agent.Position), Is.True,
                            $"Seed {seed}: agent {agent.AgentId} walked through a wall at tick {simulation.Tick}.");
                        for (int j = 0; j < i; j++)
                        {
                            FireReactionAgentSnapshot other = simulation.GetAgent(j);
                            if (other.Participation == AgentParticipation.Participating)
                            {
                                Assert.That(LogicalPosition.DistanceSquared(agent.Position, other.Position),
                                    Is.GreaterThanOrEqualTo(touching * touching));
                            }
                        }
                    }
                }

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                foreach (CausalEvent record in EventsOfType(simulation, FireReactionEventType.AgentEscaped))
                {
                    Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                        Is.EqualTo(FireReactionEventType.DoorOpened));
                    Assert.That(simulation.GetAgent(record.SourceId).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
                }

                // Safe means out through one of the three outside doors, or sheltering behind the fourth.
                int safe = snapshot.EscapedCount + snapshot.ShelteringCount;
                Assert.That(safe, Is.GreaterThanOrEqualTo(3),
                    $"Seed {seed}: only {snapshot.EscapedCount} escaped and {snapshot.ShelteringCount} sheltered with every door open.");
                escaped += snapshot.EscapedCount;
            }

            Assert.That(escaped, Is.GreaterThan(0));
        }

        private static bool IsInRoomOrDoorway(FireReactionSimulation simulation, FireReactionScenarioData data, LogicalPosition position)
        {
            int radius = data.World.OccupancyRadiusMillimetres;
            if (data.World.RoomBounds.ContainsCircle(position, radius))
            {
                return true;
            }

            foreach (FireReactionSideRoomDefinition side in data.SideRooms)
            {
                if (side.Bounds.ContainsCircle(position, radius))
                {
                    return true;
                }
            }

            for (int d = 0; d < simulation.DoorCount; d++)
            {
                FireReactionDoorSnapshot door = simulation.GetDoor(d);
                bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
                long along = alongX ? position.X - door.Centre.X : position.Z - door.Centre.Z;
                if (door.State == DoorState.Open && Math.Abs(along) <= door.WidthMillimetres / 2 - radius)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- one runner at one door

        /// <summary>One runner right by the north door, facing a fire that starts in front of them.</summary>
        /// <summary>One runner (an ordinary person unless traits are given) who panics right beside the locked north door.</summary>
        internal static FireReactionScenarioData RunnerByTheNorthDoor(
            FireReactionScenarioData data, int forceChancePercent, AgentTraitValues traits)
        {
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, 4700), CardinalDirection.South, traits)
            };
            data.PhysicsObjects = Array.Empty<FireReactionPhysicsObjectDefinition>();
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-2500, -2500, 2100, 2100);
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Falls.TripChancePercent = 0;
            data.Panic.HesitateChancePercent = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Exits.DoorForceChancePercent = forceChancePercent;
            return data;
        }

        private FireReactionScenarioData RunnerByTheNorthDoor(int forceChancePercent)
        {
            return RunnerByTheNorthDoor(DefaultData(), forceChancePercent, AgentTraitValues.AllOrdinary);
        }

        [Test]
        public void RunnerAtALockedDoor_ForcesItInVainUntilItIsUnlocked()
        {
            var simulation = new FireReactionSimulation(RunnerByTheNorthDoor(100));
            CausalEvent firstShove = default;
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                List<CausalEvent> shoves = EventsOfType(simulation, FireReactionEventType.AgentForcedDoor);
                if (shoves.Count > 0)
                {
                    firstShove = shoves[0];
                    break;
                }
            }

            Assert.That(firstShove.EventId, Is.Not.EqualTo(0UL), "The runner never tried to force the locked door.");
            CausalEvent tried = simulation.EventLog.Get(firstShove.CausalParentEventId);
            Assert.That(tried.EventType, Is.EqualTo(FireReactionEventType.AgentTriedDoor));
            Assert.That(tried.Position, Is.EqualTo(Door(simulation, NorthDoor).Centre));
            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.ForcingDoor));
            Assert.That(Door(simulation, NorthDoor).State, Is.EqualTo(DoorState.Locked), "Forcing must never work.");

            // The player unlocks it mid-shove: the runner gets it open at once and leaves.
            Click(simulation, NorthDoor);
            for (int t = 0; t < 3 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            List<CausalEvent> opened = EventsOfType(simulation, FireReactionEventType.DoorOpened);
            Assert.That(opened, Has.Count.EqualTo(1));
            Assert.That(opened[0].CausalParentEventId, Is.EqualTo(tried.EventId), "The runner, not the player, opened it.");
            CausalEvent escape = EventsOfType(simulation, FireReactionEventType.AgentEscaped)[0];
            Assert.That(escape.CausalParentEventId, Is.EqualTo(opened[0].EventId));
        }

        [Test]
        public void RunnerAtALockedDoor_CanGiveUpAndLookForAnotherWay()
        {
            var simulation = new FireReactionSimulation(RunnerByTheNorthDoor(0));
            LogicalPosition doorCentre = Door(simulation, NorthDoor).Centre;
            int gaveUpTick = -1;
            long farthest = 0L;
            for (int t = 0; t < 8 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                if (gaveUpTick < 0 && EventsOfType(simulation, FireReactionEventType.AgentGaveUpOnDoor).Count > 0)
                {
                    gaveUpTick = simulation.Tick;
                }

                if (gaveUpTick >= 0)
                {
                    farthest = Math.Max(farthest, LogicalPosition.DistanceSquared(simulation.GetAgent(0).Position, doorCentre));
                }
            }

            Assert.That(gaveUpTick, Is.GreaterThan(0), "The runner never gave up on the locked door.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentForcedDoor), Is.Empty);
            Assert.That(farthest, Is.GreaterThan(2000L * 2000L), "After giving up the runner stayed at the locked door.");
            Assert.That(simulation.GetAgent(0).Outcome, Is.Not.EqualTo(AgentTerminalOutcome.Escaped));
        }

        // ---------------------------------------------------------------- event targets

        /// <summary>
        /// Events say what they affected, so the display never has to guess
        /// (for example by picking the nearest box or door).
        /// </summary>
        [Test]
        public void Events_NameWhatTheyAffected()
        {
            var simulation = new FireReactionSimulation(DefaultData(), 40UL);
            foreach ((ulong doorId, int tick) in ReplayFingerprint.OpeningClicks)
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(doorId), tick);
            }

            for (int t = 0; t < ReplayFingerprint.Ticks; t++)
            {
                simulation.Step();
            }

            var agents = new HashSet<SimulationId>();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                agents.Add(simulation.GetAgent(i).AgentId);
            }

            var boxes = new HashSet<SimulationId>();
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                boxes.Add(simulation.GetPhysicsObject(i).ObjectId);
            }

            var doorCentres = new Dictionary<SimulationId, LogicalPosition>();
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                doorCentres.Add(simulation.GetDoor(i).DoorId, simulation.GetDoor(i).Centre);
            }

            var seen = new HashSet<FireReactionEventType>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                seen.Add(record.EventType);
                switch (record.EventType)
                {
                    case FireReactionEventType.AgentsCollided:
                        Assert.That(agents, Does.Contain(record.TargetId), "A collision names the person run into.");
                        Assert.That(record.TargetId, Is.Not.EqualTo(record.SourceId));
                        break;
                    case FireReactionEventType.BoxBumped:
                        Assert.That(boxes, Does.Contain(record.TargetId), "A bump names the box.");
                        break;
                    case FireReactionEventType.BoxHitAgent:
                        Assert.That(agents, Does.Contain(record.TargetId), "A box hit names the person hit.");
                        break;
                    case FireReactionEventType.BoxesCollided:
                        Assert.That(boxes, Does.Contain(record.TargetId), "A box-on-box hit names the other box.");
                        break;
                    case FireReactionEventType.AgentTriedDoor:
                    case FireReactionEventType.AgentForcedDoor:
                    case FireReactionEventType.AgentGaveUpOnDoor:
                    case FireReactionEventType.DoorBrokenDown:
                    case FireReactionEventType.DoorClosed:
                    case FireReactionEventType.DoorLocked:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True, $"{record.EventType} names the door.");
                        Assert.That(record.Position, Is.EqualTo(doorCentres[record.TargetId]));
                        break;
                    case FireReactionEventType.AgentEscaped:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True, "An escape names the door used.");
                        break;
                    case FireReactionEventType.AgentShookAwake:
                    case FireReactionEventType.AgentGrabbed:
                    case FireReactionEventType.AgentDropped:
                    case FireReactionEventType.AgentRescued:
                        Assert.That(agents, Does.Contain(record.TargetId), $"{record.EventType} names the person helped.");
                        break;
                    case FireReactionEventType.ItemThrown:
                    case FireReactionEventType.ItemDropped:
                        Assert.That(boxes, Does.Contain(record.TargetId), $"{record.EventType} names the item.");
                        break;
                    default:
                        Assert.That(record.HasTarget, Is.False, $"{record.EventType} should not name a target.");
                        break;
                }
            }

            Assert.That(seen, Does.Contain(FireReactionEventType.AgentsCollided));
            Assert.That(seen, Does.Contain(FireReactionEventType.AgentTriedDoor));
            Assert.That(seen, Does.Contain(FireReactionEventType.AgentEscaped));
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void Validation_RejectsDoorsThatDoNotFit()
        {
            FireReactionScenarioData narrow = DefaultData();
            narrow.Doors = new[] { new FireReactionDoorDefinition(new SimulationId(9001UL), WallSide.North, 0, 400) };
            Assert.Throws<InvalidOperationException>(() => narrow.Validate());

            FireReactionScenarioData offTheEnd = DefaultData();
            offTheEnd.Doors = new[] { new FireReactionDoorDefinition(new SimulationId(9001UL), WallSide.East, 5800, 1000) };
            Assert.Throws<InvalidOperationException>(() => offTheEnd.Validate());

            FireReactionScenarioData sharedId = DefaultData();
            sharedId.Doors = new[] { new FireReactionDoorDefinition(new SimulationId(1001UL), WallSide.East, 0, 1000) };
            Assert.Throws<InvalidOperationException>(() => sharedId.Validate());
        }
    }
}

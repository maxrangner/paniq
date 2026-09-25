using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    public sealed class DoorsEditModeTests
    {
        private static readonly SimulationId OfficeWayOut = new SimulationId(2001UL);

        /// <summary>The building's one way out, in the meeting room.</summary>
        internal static readonly SimulationId TheWayOut = new SimulationId(2008UL);

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

        private ScenarioData DefaultData() =>
            TheBuilding.WithTheFireInTheOffice(TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()));

        private static AgentDefinition Agent(ulong id, int x, int z, CardinalDirection facing)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing);
        }

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

        private static void Click(Run simulation, SimulationId door)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, door, simulation.Tick + 1);
        }

        private static void OpenEveryDoor(Run simulation)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                SimulationId id = simulation.GetDoor(i).DoorId;
                simulation.QueueCommand(PlayerCommandType.ClickDoor, id, 1);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, id, 2);
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

        // ---------------------------------------------------------------- clicks

        [Test]
        public void DefaultBuilding_LocksItsWaysOutAndLeavesItsInsideDoorsShut()
        {
            ScenarioData data = DefaultData();
            var simulation = new Run(data);
            // Four rooms onto the corridor, the closet, the meeting room's
            // door into the cafeteria, the maintenance room, three stalls,
            // the archway where the corridor Ts, the stockroom's two doors,
            // and the one way out.
            Assert.That(simulation.DoorCount, Is.EqualTo(14));
            var sides = new HashSet<WallSide>();
            int locked = 0;
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                DoorSnapshot door = simulation.GetDoor(i);
                DoorDefinition definition = Array.Find(data.Doors, d => d.DoorId == door.DoorId);
                if (definition.IsOpening)
                {
                    // An archway is a gap rather than a door: nothing shuts it,
                    // and the rules already describe a gap in a wall as broken.
                    Assert.That(door.State, Is.EqualTo(DoorState.Broken),
                        $"The archway {door.DoorId} should stand open from the start.");
                    continue;
                }

                if (definition.Swings)
                {
                    // Swing doors stand open to people from the start; only
                    // the fire has to get through them.
                    Assert.That(door.State, Is.EqualTo(DoorState.Open), $"The swing doors {door.DoorId} should stand open from the start.");
                    Assert.That(door.Swings, Is.True);
                    continue;
                }

                // The ways out start locked; the inside doors are shut but not locked.
                Assert.That(door.State, Is.EqualTo(definition.StartsLocked ? DoorState.Locked : DoorState.Unlocked));
                locked += definition.StartsLocked ? 1 : 0;
                if (definition.RoomId == data.Rooms[0].RoomId && definition.StartsLocked)
                {
                    sides.Add(door.Side);
                }
            }

            Assert.That(locked, Is.EqualTo(1), "One way out of the building, and it is the player's to open.");
            Assert.That(sides, Is.Empty, "The office has no way out of its own: everybody leaves through the corridor.");
        }

        [Test]
        public void DoorClicks_UnlockThenOpenThenCloseThenOpenAgain()
        {
            ScenarioData data = WithAWayOutOfTheOffice(DefaultData());

            // Working one door four times over costs more than a round's purse
            // holds, and this test is about what the clicks do rather than what
            // they cost: PowersEditModeTests owns the prices.
            data.Influence.Starting = 1000;
            data.Influence.Maximum = 1000;
            var simulation = new Run(data);
            Click(simulation, OfficeWayOut);
            simulation.Step();
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Unlocked));
            List<CausalEvent> unlocked = EventsOfType(simulation, CausalEventType.DoorUnlocked);
            Assert.That(unlocked, Has.Count.EqualTo(1));
            Assert.That(unlocked[0].SourceId, Is.EqualTo(OfficeWayOut));
            Assert.That(unlocked[0].HasCausalParent, Is.False, "A player click is a root cause.");

            Click(simulation, OfficeWayOut);
            simulation.Step();
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Open));
            List<CausalEvent> opened = EventsOfType(simulation, CausalEventType.DoorOpened);
            Assert.That(opened, Has.Count.EqualTo(1));
            Assert.That(opened[0].CausalParentEventId, Is.EqualTo(unlocked[0].EventId));

            Click(simulation, OfficeWayOut);
            simulation.Step();
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Unlocked), "A third click closes it.");
            List<CausalEvent> closed = EventsOfType(simulation, CausalEventType.DoorClosed);
            Assert.That(closed, Has.Count.EqualTo(1));
            Assert.That(closed[0].HasCausalParent, Is.False, "The player closing a door is a root cause.");
            Assert.That(closed[0].TargetId, Is.EqualTo(OfficeWayOut));

            Click(simulation, OfficeWayOut);
            simulation.Step();
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Open));
            Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened), Has.Count.EqualTo(2));
            Assert.That(simulation.Commands, Has.Count.EqualTo(4));
        }

        [Test]
        public void Commands_CannotTargetStartedTicksOrUnknownDoors()
        {
            var simulation = new Run(DefaultData());
            simulation.Step();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, simulation.Tick));
            Assert.Throws<ArgumentException>(() =>
                simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(1001UL), simulation.Tick + 1));
        }

        // ReplayingTheSameClicks_GivesTheSameRun queued door clicks into a
        // fresh run and checked it came out the same. That is what
        // TheBusiestRun_PlaysOutTheSameWayThreeTimes does, with the boxes and
        // the cards going too.

        // ---------------------------------------------------------------- locked and open

        [Test]
        public void LockedDoors_KeepEveryoneInsideButGetTried()
        {
            int tried = 0;
            int forcedOrGaveUp = 0;
            for (ulong seed = 40UL; seed <= 44UL; seed++)
            {
                ScenarioData data = DefaultData();

                // Every door is locked here, inside doors included. An archway
                // stays an archway: it is a gap in a wall rather than a door,
                // so there is nothing there to turn a key in, and pretending
                // otherwise would wall off half the building.
                data.Doors = Array.ConvertAll(data.Doors, d => new DoorDefinition(
                    d.DoorId, d.RoomId, d.Side, d.CentreAlongWallMillimetres, d.WidthMillimetres,
                    startsLocked: true, isOpening: d.IsOpening));

                // Nobody here is strong enough to break a door down.
                data.Traits.DoorDamagePerPoint = 0;
                var simulation = new Run(data, seed);
                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot agent = simulation.GetAgent(i);
                        if (agent.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(IsInRoomOrDoorway(simulation, data, agent.Position), Is.True,
                                $"Seed {seed}: agent {agent.AgentId} got out of the building at tick {simulation.Tick}.");
                        }
                    }
                }

                Assert.That(simulation.GetSnapshot().EscapedCount, Is.EqualTo(0));
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    switch (record.EventType)
                    {
                        case CausalEventType.AgentTriedDoor:
                            tried++;

                            // Somebody tries a door because they are frightened.
                            // Catching fire is its own reason to be frightened,
                            // and somebody alight rattling a locked handle is
                            // the same behaviour with a worse cause. Or, calm,
                            // because the day sent them through it: a toilet
                            // trip, home time.
                            Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                                Is.EqualTo(CausalEventType.AgentScared)
                                    .Or.EqualTo(CausalEventType.AgentCaughtFire)
                                    .Or.EqualTo(CausalEventType.CueCalled));
                            break;
                        case CausalEventType.AgentForcedDoor:
                        case CausalEventType.AgentGaveUpOnDoor:
                            forcedOrGaveUp++;
                            Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                                Is.EqualTo(CausalEventType.AgentTriedDoor));
                            break;
                        case CausalEventType.DoorOpened:
                        case CausalEventType.AgentEscaped:
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
            int safe = 0;
            for (ulong seed = 40UL; seed <= 44UL; seed++)
            {
                ScenarioData data = DefaultData();

                // Enough in the purse to actually work every door. Twelve
                // doors at thirty apiece is far past the hundred a round
                // starts with, so "with every door open" was quietly untrue:
                // the money ran out around the third door and the way out was
                // never touched. This test is about what open doors do, not
                // about what they cost -- PowersEditModeTests owns
                // the prices.
                data.Influence.Starting = 2000;
                data.Influence.Maximum = 2000;
                var simulation = new Run(data, seed);
                OpenEveryDoor(simulation);
                // Bodies give a little: in a packed, shoving crowd two people on
                // their feet may press a few centimetres into each other, and
                // no further. People knocked down can end up in a heap, one
                // sprawled across another, which is a pile, not an overlap.
                long touching = data.World.OccupancyRadiusMillimetres * 2L - 50L;
                // A minute, not thirty seconds. The way out is at the far end
                // of a corridor that runs the length of the building now, so
                // somebody starting in the office has twenty-odd metres and a
                // queue between them and the street.
                int endTick = data.Fire.ActivationTick + 60 * Run.TicksPerSecond;
                List<NavigationGrid.Wall> walls = simulation.GeometryForTests.SolidWalls();
                var before = new LogicalPosition[simulation.AgentCount];
                for (int i = 0; i < before.Length; i++)
                {
                    before[i] = simulation.GetAgent(i).Position;
                }

                while (simulation.Tick < endTick)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot agent = simulation.GetAgent(i);
                        LogicalPosition from = before[i];
                        before[i] = agent.Position;
                        if (agent.Participation != AgentParticipation.Participating)
                        {
                            continue;
                        }

                        // Nobody's middle ever crosses a solid stretch of wall
                        // between one tick and the next. Doorways are gaps in
                        // the walls, so going through one is fine, and once out
                        // in the street people may wander where they like.
                        Assert.That(CrossesAWall(walls, from, agent.Position), Is.False,
                            $"Seed {seed}: agent {agent.AgentId} walked through a wall at tick {simulation.Tick}.");
                        for (int j = 0; j < i; j++)
                        {
                            AgentSnapshot other = simulation.GetAgent(j);
                            if (other.Participation == AgentParticipation.Participating && !agent.IsDown && !other.IsDown)
                            {
                                Assert.That(LogicalPosition.DistanceSquared(agent.Position, other.Position),
                                    Is.GreaterThanOrEqualTo(touching * touching));
                            }
                        }
                    }
                }

                RunSnapshot snapshot = simulation.GetSnapshot();
                foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.AgentEscaped))
                {
                    // Out through a door somebody opened, or one battered off
                    // its hinges after somebody shut it again.
                    Assert.That(simulation.EventLog.Get(record.CausalParentEventId).EventType,
                        Is.EqualTo(CausalEventType.DoorOpened).Or.EqualTo(CausalEventType.DoorBrokenDown));
                    Assert.That(simulation.GetAgent(record.SourceId).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
                }

                // Somebody always gets out. How many is the dice's business:
                // a seed where a thrown chair wedges the way out loses most of
                // the building however wide the doors stood, so the count that
                // means anything is the one over all five seeds together.
                Assert.That(snapshot.EscapedCount, Is.GreaterThanOrEqualTo(1),
                    $"Seed {seed}: nobody escaped with every door open.");
                safe += snapshot.EscapedCount + snapshot.ClearOfFireCount;
                escaped += snapshot.EscapedCount;
            }

            // Safe means out of the building, or at least in a room with no
            // fire in it. Five seeds of twenty people: a quarter of them safe
            // is the floor, and most runs are far above it.
            Assert.That(safe, Is.GreaterThanOrEqualTo(25),
                $"Only {safe} of a hundred people over five seeds were out or clear of the fire with every door open.");

            Assert.That(escaped, Is.GreaterThan(0));
        }

        /// <summary>Whether a whole body stands inside one of the building's rooms.</summary>
        private static bool InAnyRoom(ScenarioData data, LogicalPosition position)
        {
            foreach (RoomDefinition room in data.Rooms)
            {
                if (room.Bounds.ContainsCircle(position, data.World.OccupancyRadiusMillimetres))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a move from one point to another cuts across any of these stretches of wall.</summary>
        private static bool CrossesAWall(List<NavigationGrid.Wall> walls, LogicalPosition from, LogicalPosition to)
        {
            foreach (NavigationGrid.Wall wall in walls)
            {
                if (Straddles(wall.From, wall.To, from, to) && Straddles(from, to, wall.From, wall.To))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the two points lie strictly on opposite sides of the line through a and b.</summary>
        private static bool Straddles(LogicalPosition a, LogicalPosition b, LogicalPosition p, LogicalPosition q)
        {
            long abx = (long)b.X - a.X;
            long abz = (long)b.Z - a.Z;
            long sideP = abx * ((long)p.Z - a.Z) - abz * ((long)p.X - a.X);
            long sideQ = abx * ((long)q.Z - a.Z) - abz * ((long)q.X - a.X);
            return (sideP > 0L && sideQ < 0L) || (sideP < 0L && sideQ > 0L);
        }

        private static bool IsInRoomOrDoorway(Run simulation, ScenarioData data, LogicalPosition position)
        {
            if (InAnyRoom(data, position))
            {
                return true;
            }

            // Anywhere in the gap of a doorway that is open (or broken down),
            // including just through it on the way out of the building. People
            // are bodies now: somebody rounding the doorframe is off the door's
            // centre line, with their shoulder past the wall line, and that is
            // not walking through the wall.
            int radius = data.World.OccupancyRadiusMillimetres;
            for (int d = 0; d < simulation.DoorCount; d++)
            {
                DoorSnapshot door = simulation.GetDoor(d);
                bool alongX = door.Side == WallSide.North || door.Side == WallSide.South;
                long along = alongX ? position.X - door.Centre.X : position.Z - door.Centre.Z;
                long across = alongX ? position.Z - door.Centre.Z : position.X - door.Centre.X;
                bool gap = door.State == DoorState.Open || door.State == DoorState.Broken;
                // Once through, a step or two to one side of the door's own
                // line is still just outside it.
                if (gap && Math.Abs(along) <= door.WidthMillimetres / 2 + radius &&
                    Math.Abs(across) <= data.Exits.EscapeDepthMillimetres + radius)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- one runner at one door

        /// <summary>
        /// The building plus a locked way out of the office's north wall. The
        /// office has none of its own, and these tests are about how a door
        /// behaves rather than about the floor plan, so they bring their own.
        /// </summary>
        /// <summary>
        /// The owner's rule: with an unobstructed way out standing open, most
        /// people trying to escape should be heading for it. Somebody stuck in a
        /// queue behind somebody else still counts — being obstructed is the
        /// other half of the game. So the excuses are all switched off here:
        /// nobody freezes, nobody turns back to help, nobody fights the fire.
        /// What is left is willingness, and nearly all of it should point at the
        /// door.
        /// </summary>
        [Test]
        public void WithAWayOutOpen_NearlyEverybodyHeadsForIt()
        {
            ScenarioData data = WithAWayOutOfTheOffice(DefaultData(), startsLocked: false);

            // Eight ordinary people spread down the office, all of whom can see
            // the fire when it starts.
            var crowd = new List<AgentDefinition>();
            ulong id = 1UL;
            for (int x = -4000; x <= 4000; x += 2000)
            {
                crowd.Add(new AgentDefinition(new SimulationId(id++), new LogicalPosition(x, 2500),
                    CardinalDirection.South, AgentTraitValues.AllOrdinary));
            }

            for (int x = -3000; x <= 3000; x += 2000)
            {
                crowd.Add(new AgentDefinition(new SimulationId(id++), new LogicalPosition(x, 4000),
                    CardinalDirection.South, AgentTraitValues.AllOrdinary));
            }

            data.Agents = crowd.ToArray();
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -1000, -1000);
            data.Fire.SpreadMinimumTicks = 2000;
            data.Fire.SpreadMaximumTicks = 3000;

            var simulation = new Run(data);

            // The player throws the door open, so there is nothing to work out
            // and nothing in the way: just a way out, standing open.
            simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, 2);
            int wayOut = -1;
            for (int d = 0; d < simulation.DoorCount; d++)
            {
                if (simulation.GetDoor(d).DoorId == OfficeWayOut)
                {
                    wayOut = d;
                }
            }

            Assert.That(wayOut, Is.GreaterThanOrEqualTo(0));
            for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetDoor(wayOut).State, Is.EqualTo(DoorState.Open), "The way out should be open.");

            int frightened = 0;
            int heading = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot person = simulation.GetAgent(i);

                // Somebody already outside was plainly heading for it, so they
                // count on both sides rather than quietly leaving the sum.
                if (person.Outcome == AgentTerminalOutcome.Escaped)
                {
                    frightened++;
                    heading++;
                    continue;
                }

                if (person.Participation != AgentParticipation.Participating ||
                    person.FearState != AgentFearState.Scared || person.IsDown || person.IsBurning)
                {
                    continue;
                }

                frightened++;
                heading += simulation.IsHeadingForWayOut(person.AgentId, wayOut) ? 1 : 0;
            }

            Assert.That(frightened, Is.GreaterThan(4), "Hardly anybody was frightened, so this proves nothing.");
            Assert.That(heading * 4, Is.GreaterThanOrEqualTo(frightened * 3),
                $"Only {heading} of {frightened} frightened people were heading for the open way out.");
        }

        /// <summary>
        /// A meeting room with the way out standing open, a slow fire back in
        /// the office, and none of the excuses (no freezing, no tripping, no
        /// dithering) that would make a test of where they walk pass or fail on
        /// luck. The people are frightened by hand once the fire has started;
        /// see <see cref="Frighten"/>.
        /// </summary>
        private ScenarioData FleeingTowardTheWayOut(params AgentDefinition[] people)
        {
            ScenarioData data = DefaultData();
            data.Agents = people;
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Tables = Array.Empty<TableDefinition>();
            data.Alarms = Array.Empty<AlarmDefinition>();
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Falls.TripChancePercent = 0;
            data.Panic.HesitateChancePercent = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            return data;
        }

        /// <summary>Runs until the fire has started, then frightens everybody at once.</summary>
        private static void Frighten(Run simulation)
        {
            simulation.Step();
            simulation.Step();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                simulation.FrightenForTests(i);
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

            throw new KeyNotFoundException(id.ToString());
        }

        /// <summary>
        /// The owner's report (seed 42): somebody steps through a doorway, is
        /// knocked off their line a moment later, and walks back through the
        /// same door to go through it again -- and, being close to it, never
        /// thinks again. Once through, the next leg starts from the room they
        /// are standing in.
        /// <para>
        /// It was first seen on the corridor door into the meeting room. The
        /// meeting room is a dead end now, so the same trap is set the other
        /// way round: out of the meeting room into the corridor, which is the
        /// direction somebody heading for the way out actually goes.
        /// </para>
        /// </summary>
        [Test]
        public void SomebodyKnockedAsideJustThroughADoor_CarriesOnRatherThanGoingBack()
        {
            ScenarioData data = FleeingTowardTheWayOut(
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2000, 8600),
                    CardinalDirection.South, AgentTraitValues.AllOrdinary));
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(2006UL), 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 2);
            Frighten(simulation);
            while (simulation.GetAgent(0).ActivityState != AgentActivityState.Fleeing)
            {
                Assert.That(simulation.Tick, Is.LessThan(200), "They never ran.");
                simulation.Step();
            }

            // Exactly the shape of what the owner saw: still set on the door
            // they have just used, from the side they came from, but standing
            // in the room beyond it.
            WorldGeometry geometry = simulation.GeometryForTests;
            int meetingRoom = geometry.RoomAt(TheBuilding.MeetingRoom);
            Agent person = simulation.AgentForTests(0);
            person.Doors.ExitDoorIndex = DoorIndex(simulation, new SimulationId(2006UL));
            person.Doors.ApproachRoom = meetingRoom;
            person.Intent.NextPanicDecisionTick = simulation.Tick + 10000;

            for (int t = 0; t < 5 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
                AgentSnapshot now = simulation.GetAgent(0);
                if (now.Outcome == AgentTerminalOutcome.Escaped)
                {
                    break;
                }

                Assert.That(geometry.RoomAt(now.Position), Is.Not.EqualTo(meetingRoom),
                    $"They walked back into the meeting room at tick {simulation.Tick}.");
            }

            if (simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped)
            {
                Assert.That(simulation.AgentForTests(0).Doors.ExitDoorIndex, Is.EqualTo(DoorIndex(simulation, TheWayOut)),
                    "They should be making for the way out of the meeting room.");
            }
        }

        /// <summary>
        /// The owner's report (seed 42): a knot of people pressed beside the open
        /// way out, every one of them standing aside for somebody lined up with
        /// the gap -- except nobody was, so the gap stood empty and they waited
        /// there politely until the end. Standing aside is only for somebody
        /// actually going through.
        /// </summary>
        [Test]
        public void AKnotBesideAnOpenWayOut_DoesNotWaitForNobody()
        {
            AgentDefinition Person(ulong id, int x, int z) =>
                new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z),
                    CardinalDirection.South, AgentTraitValues.AllOrdinary);
            ScenarioData data = FleeingTowardTheWayOut(
                Person(1UL, 14000, 16200), Person(2UL, 14550, 16200), Person(3UL, 14275, 15650),
                Person(4UL, 14825, 15650), Person(5UL, 14550, 15100));
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 2);
            Frighten(simulation);
            for (int t = 0; t < 20 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot person = simulation.GetAgent(i);
                Assert.That(person.Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                    $"Person {person.AgentId} was still at ({person.Position.X}, {person.Position.Z}) after 20 s " +
                    "beside an open way out.");
            }
        }

        /// <summary>
        /// The owner's report (seed 42): an office chair ends up wedged in the
        /// way out and the whole meeting room waits behind it while the fire
        /// comes. Grabbing it and throwing it clear is self-preservation, so an
        /// ordinary person does it -- nobody strong, and no leader, needed.
        /// </summary>
        [Test]
        public void AChairWedgedInTheWayOut_IsThrownClearByAnOrdinaryPerson()
        {
            var ordinary = new AgentTraitValues(5, 5, 5, 5, 2, 5, 4);
            ScenarioData data = FleeingTowardTheWayOut(
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(14500, 15400),
                    CardinalDirection.North, ordinary));
            Assert.That(ordinary.Strength, Is.LessThan(data.Blockades.ShoveMinimumStrength),
                "The point is somebody too weak to heave it along the wall.");
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3900UL), PhysicsObjectKind.OfficeChair,
                    new LogicalPosition(14500, 16600), 500, 9000)
            };

            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 1);
            Frighten(simulation);
            for (int t = 0; t < 20 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
                if (simulation.GetAgent(0).Outcome == AgentTerminalOutcome.Escaped)
                {
                    break;
                }
            }

            Assert.That(EventsOfType(simulation, CausalEventType.DoorBlocked), Is.Not.Empty,
                "The chair should have jammed the way out to begin with.");
            Assert.That(EventsOfType(simulation, CausalEventType.ItemThrown), Is.Not.Empty,
                "Nobody threw the chair clear.");
            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                "They should have thrown the chair clear and got out.");
        }

        internal static ScenarioData WithAWayOutOfTheOffice(
            ScenarioData data, bool startsLocked = true)
        {
            var doors = new List<DoorDefinition>(data.Doors)
            {
                // The south wall, not the north one: the office's north wall
                // opens onto the corridor now, and a way *out* has to face the
                // street or nobody can ever leave through it.
                new DoorDefinition(OfficeWayOut, PrototypeBuilding.Office, WallSide.South, -2500, 1000,
                    startsLocked)
            };
            data.Doors = doors.ToArray();
            return data;
        }

        /// <summary>One runner (an ordinary person unless traits are given) who panics right beside the locked north door.</summary>
        internal static ScenarioData RunnerByTheWayOut(
            ScenarioData data, int forceChancePercent, AgentTraitValues traits)
        {
            WithAWayOutOfTheOffice(data);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, -4700), CardinalDirection.North, traits)
            };
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-2500, -2500, -2100, -2100);
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Falls.TripChancePercent = 0;
            data.Panic.HesitateChancePercent = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Exits.DoorForceChancePercent = forceChancePercent;
            return data;
        }

        private ScenarioData RunnerByTheWayOut(int forceChancePercent)
        {
            return RunnerByTheWayOut(DefaultData(), forceChancePercent, AgentTraitValues.AllOrdinary);
        }

        /// <summary>
        /// Somebody knocked down inside the open way out, with three people
        /// pressed up against them from inside the office: the press carries
        /// them on through the doorway and out, an escape on their back. They
        /// used to lie in the gap as a plug -- on seed 41 the opened exit
        /// stood blocked for fifteen seconds by the people the cruel had
        /// shoved to the floor in it. The three are calm and stand where they
        /// are for the whole test, so the press is steady; frightened people
        /// give a blocked doorway up within a second and go elsewhere.
        /// </summary>
        [Test]
        public void SomebodyDownInTheDoorway_IsCarriedThroughByThePress()
        {
            ScenarioData data = RunnerByTheWayOut(DefaultData(), 0, AgentTraitValues.AllOrdinary);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, -5700), CardinalDirection.South, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(-2500, -5100), CardinalDirection.South, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(3UL), new LogicalPosition(-1950, -5250), CardinalDirection.South, AgentTraitValues.AllOrdinary),
                new AgentDefinition(new SimulationId(4UL), new LogicalPosition(-3050, -5250), CardinalDirection.South, AgentTraitValues.AllOrdinary)
            };
            data.Tables = new TableDefinition[0];
            data.Fire.ActivationTick = 100000;
            data.Calm.DecisionMinimumTicks = 5000;
            data.Calm.DecisionMaximumTicks = 5000;
            data.Falls.KnockdownMinimumTicks = 600;
            data.Falls.KnockdownMaximumTicks = 600;
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, 1);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, 2);
                for (int t = 0; t < 5; t++)
                {
                    simulation.Step();
                }

                // Down in the doorway for a long while.
                Agent fallen = simulation.AgentForTests(0);
                fallen.Body.State = AgentBodyState.Fallen;
                fallen.Body.EndTick = simulation.Tick + 600;

                var trace = new System.Text.StringBuilder();
                for (int t = 0; t < 10 * Run.TicksPerSecond && simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
                {
                    simulation.Step();
                    if (t % 10 == 0 && t < 200)
                    {
                        trace.Append($"t={simulation.Tick} fallen at {simulation.GetAgent(0).Position} {simulation.GetAgent(0).BodyState} carried={fallen.Doors.CarriedThroughDoor}; ");
                    }
                }

                Assert.That(EventsOfType(simulation, CausalEventType.AgentCarriedThroughDoorway), Is.Not.Empty,
                    "The press should have carried them through. " + trace);
                Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                    $"Carried out through the way out; instead at {simulation.GetAgent(0).Position}. " + trace);
            }
        }

        // ---------------------------------------------------------------- event targets

        /// <summary>
        /// Events say what they affected, so the display never has to guess
        /// (for example by picking the nearest box or door).
        /// </summary>
        [Test]
        public void Events_NameWhatTheyAffected()
        {
            var simulation = new Run(DefaultData(), 40UL);
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

            var alarms = new HashSet<SimulationId>();
            for (int i = 0; i < simulation.AlarmCount; i++)
            {
                alarms.Add(simulation.AlarmId(i));
            }

            var doorCentres = new Dictionary<SimulationId, LogicalPosition>();
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                doorCentres.Add(simulation.GetDoor(i).DoorId, simulation.GetDoor(i).Centre);
            }

            var seen = new HashSet<CausalEventType>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                seen.Add(record.EventType);
                switch (record.EventType)
                {
                    case CausalEventType.AgentsCollided:
                        Assert.That(agents, Does.Contain(record.TargetId), "A collision names the person run into.");
                        Assert.That(record.TargetId, Is.Not.EqualTo(record.SourceId));
                        break;
                    case CausalEventType.DoorBlocked:
                    case CausalEventType.DoorUnblocked:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True,
                            "A jammed doorway names the door.");
                        Assert.That(boxes, Does.Contain(record.SourceId), "And the thing wedged in it.");
                        break;
                    case CausalEventType.AgentBarricadedDoor:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True,
                            "Barricading names the door.");
                        break;
                    case CausalEventType.AgentShovedObstruction:
                        Assert.That(boxes, Does.Contain(record.TargetId), "Heaving names the thing heaved.");
                        break;
                    case CausalEventType.ObjectBroke:
                        Assert.That(record.TargetId, Is.Not.EqualTo(record.SourceId),
                            "Breaking names both what broke and what hit it.");
                        break;
                    case CausalEventType.AlarmPulled:
                    case CausalEventType.PowerPulledAlarm:
                        Assert.That(alarms, Does.Contain(record.TargetId), "Raising the alarm names the alarm.");
                        break;
                    case CausalEventType.AgentShoved:
                        Assert.That(agents, Does.Contain(record.TargetId), "A shove names the person shoved aside.");
                        Assert.That(record.TargetId, Is.Not.EqualTo(record.SourceId));
                        break;
                    case CausalEventType.BoxBumped:
                        Assert.That(boxes, Does.Contain(record.TargetId), "A bump names the box.");
                        break;
                    case CausalEventType.BoxHitAgent:
                        Assert.That(agents, Does.Contain(record.TargetId), "A box hit names the person hit.");
                        break;
                    case CausalEventType.BoxesCollided:
                        Assert.That(boxes, Does.Contain(record.TargetId), "A box-on-box hit names the other box.");
                        break;
                    case CausalEventType.AgentTriedDoor:
                    case CausalEventType.AgentForcedDoor:
                    case CausalEventType.AgentGaveUpOnDoor:
                    case CausalEventType.DoorBrokenDown:
                    case CausalEventType.DoorBurntThrough:
                    case CausalEventType.DoorClosed:
                    case CausalEventType.DoorLocked:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True, $"{record.EventType} names the door.");
                        Assert.That(record.Position, Is.EqualTo(doorCentres[record.TargetId]));
                        break;
                    case CausalEventType.AgentEscaped:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True, "An escape names the door used.");
                        break;
                    case CausalEventType.AgentDashedThroughHeat:
                    case CausalEventType.AgentHidFromTheHeat:
                    case CausalEventType.AgentCarriedThroughDoorway:
                        Assert.That(doorCentres.ContainsKey(record.TargetId), Is.True, $"{record.EventType} names the door.");
                        break;
                    case CausalEventType.AgentShookAwake:
                    case CausalEventType.AgentGrabbed:
                    case CausalEventType.AgentDropped:
                    case CausalEventType.AgentRescued:
                        Assert.That(agents, Does.Contain(record.TargetId), $"{record.EventType} names the person helped.");
                        break;
                    case CausalEventType.ItemThrown:
                    case CausalEventType.ItemDropped:
                    case CausalEventType.AgentTookExtinguisher:
                    case CausalEventType.ExtinguisherSprayed:
                    case CausalEventType.ExtinguisherEmptied:
                        Assert.That(boxes, Does.Contain(record.TargetId), $"{record.EventType} names the item.");
                        break;
                    case CausalEventType.LeaderOrderedDoorBroken:
                    case CausalEventType.LeaderOrderedFireFought:
                        Assert.That(agents, Does.Contain(record.TargetId), $"{record.EventType} names the person sent.");
                        break;
                    case CausalEventType.AgentBlasted:
                    case CausalEventType.AgentDoused:
                        Assert.That(agents, Does.Contain(record.TargetId), $"{record.EventType} names the person hit.");
                        break;

                    case CausalEventType.PowerSparkStarted:
                    case CausalEventType.PowerSparkArrived:
                        Assert.That(boxes, Does.Contain(record.TargetId),
                            $"{record.EventType} names the socket or fuse box at the far end.");
                        break;
                    case CausalEventType.TableHeaved:
                        Assert.That(record.HasTarget, Is.True, "A heave names the table.");
                        break;
                    case CausalEventType.CueCalled:
                        // A cue names the room it was called in or the person
                        // it was called to, or nothing for the whole building.
                        Assert.That(!record.HasTarget || agents.Contains(record.TargetId) ||
                                    System.Array.Exists(simulation.Scenario.Rooms, room => room.RoomId == record.TargetId),
                            Is.True, "A cue names a room, a person, or nobody.");
                        break;
                    default:
                        Assert.That(record.HasTarget, Is.False, $"{record.EventType} should not name a target.");
                        break;
                }
            }

            Assert.That(seen, Does.Contain(CausalEventType.AgentsCollided));
            Assert.That(seen, Does.Contain(CausalEventType.AgentTriedDoor));
            Assert.That(seen, Does.Contain(CausalEventType.AgentEscaped));
        }

        // ---------------------------------------------------------------- validation

        [Test]
        public void Validation_RejectsDoorsThatDoNotFit()
        {
            SimulationId office = DefaultData().Rooms[0].RoomId;

            ScenarioData narrow = DefaultData();
            narrow.Doors = new[] { new DoorDefinition(new SimulationId(9001UL), office, WallSide.North, 0, 400) };
            Assert.Throws<InvalidOperationException>(() => narrow.Validate());

            ScenarioData offTheEnd = DefaultData();
            offTheEnd.Doors = new[] { new DoorDefinition(new SimulationId(9001UL), office, WallSide.East, 5800, 1000) };
            Assert.Throws<InvalidOperationException>(() => offTheEnd.Validate());

            ScenarioData sharedId = DefaultData();
            sharedId.Doors = new[] { new DoorDefinition(new SimulationId(1001UL), office, WallSide.East, 0, 1000) };
            Assert.Throws<InvalidOperationException>(() => sharedId.Validate());

            ScenarioData unknownRoom = DefaultData();
            unknownRoom.Doors = new[]
            {
                new DoorDefinition(new SimulationId(9001UL), new SimulationId(9999UL), WallSide.North, 0, 1000)
            };
            Assert.Throws<InvalidOperationException>(() => unknownRoom.Validate());

            // A door opening half into the closet and half into the stockroom
            // below it: two rooms behind one gap is a gap nobody can build.
            ScenarioData halfIntoAWall = DefaultData();
            halfIntoAWall.Doors = new[]
            {
                new DoorDefinition(new SimulationId(9001UL), office, WallSide.East, -500, 1000)
            };
            Assert.Throws<InvalidOperationException>(() => halfIntoAWall.Validate());
        }
    }
}

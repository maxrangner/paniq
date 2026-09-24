using System;
using System.Linq;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// People who do not know the building, asked of the rules directly rather
    /// than through a whole run: what somebody standing in a given spot can
    /// see, what a sign tells them, what a leader tells them, when a room is a
    /// dead end, and where somebody with no way out in mind goes looking.
    /// <para>
    /// None of these needs the physics engine, so they run outside the editor
    /// too. What a whole crowd of visitors then does is in
    /// <see cref="WayfindingRunEditModeTests"/>.
    /// </para>
    /// </summary>
    public sealed class WayfindingEditModeTests
    {
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
        /// The default floor, with nobody on it but the people named here,
        /// and everything a door choice needs -- none of it the physics engine.
        /// </summary>
        private sealed class Floor
        {
            public SimulationContext Context;
            public DoorRuntime[] DoorStates;
            public WorldGeometry Geometry;
            public DoorSystem Doors;
            public Agent[] People;
            public ExitSignBehaviour Signs;
            public WayfindingSystem Wayfinding;
            public DoorBehaviour DoorChoice;

            public int Door(SimulationId id) => Doors.IndexOf(id);

            public int Room(LogicalPosition where) => Geometry.RoomAtPoint(where);

            /// <summary>Makes a person a visitor, knowing only the room they stand in.</summary>
            public Agent Visitor(int index)
            {
                Agent person = People[index];
                int room = Geometry.RoomAtPoint(person.Body.Position);
                person.Knowledge.StartAsVisitor(room, Geometry.RoomDoors(room));
                return person;
            }

            public int EventsOf(CausalEventType type) =>
                Context.Events.Events.Count(e => e.EventType == type);
        }

        private Floor Build(ScenarioData data, params (LogicalPosition At, int Facing)[] people)
        {
            var floor = new Floor();
            floor.DoorStates = DoorSystem.CreateDoors(data);
            floor.Context = new SimulationContext(data, 1UL);
            floor.Geometry = new WorldGeometry(floor.Context, floor.DoorStates);
            floor.People = new Agent[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                floor.People[i] = new Agent(i, new SimulationId(1001UL + (ulong)i), floor.DoorStates.Length,
                    floor.Geometry.RoomCount);
                floor.People[i].Participation = AgentParticipation.Participating;
                floor.People[i].Traits = new AgentTraitValues(5, 5, 5, 5, 2, 5, 4);
            }

            var crowd = new Crowd(floor.People, data.World.OccupancyRadiusMillimetres, floor.Geometry.FireArea);
            for (int i = 0; i < people.Length; i++)
            {
                crowd.MoveTo(floor.People[i], people[i].At);
                floor.People[i].Body.Heading = people[i].Facing;
            }

            var fire = new FireSystem(floor.Context, floor.Geometry);
            var threats = new Threats(fire);
            var fear = new FearSystem(floor.Context, threats);
            var sound = new SoundSystem(floor.Context, crowd, threats, fear, floor.Geometry);
            floor.Doors = new DoorSystem(floor.Context, floor.DoorStates, floor.Geometry);
            floor.Doors.Bind(new Systems { Crowd = crowd });
            floor.Signs = new ExitSignBehaviour(floor.Context, floor.Geometry);
            floor.Wayfinding = new WayfindingSystem(floor.Context, floor.Geometry, floor.Signs);
            floor.DoorChoice = new DoorBehaviour(floor.Context, crowd, floor.Geometry, floor.Doors, threats, sound,
                floor.Signs, floor.Wayfinding);
            return floor;
        }

        private Floor Build(params (LogicalPosition At, int Facing)[] people) =>
            Build(scenario.ToRuntimeData(), people);

        private static void Frighten(Agent person) => person.Fear.State = AgentFearState.Scared;

        // ------------------------------------------------------------ who knows what

        [Test]
        public void SomebodyWhoWorksHere_KnowsEveryDoorAndEveryRoom_EvenAHoleNotYetBlown()
        {
            Floor floor = Build((TheBuilding.MeetingRoom, 0));
            AgentKnowledge knows = floor.People[0].Knowledge;

            Assert.That(knows.KnowsEverything, Is.True);
            for (int door = 0; door < floor.DoorStates.Length; door++)
            {
                Assert.That(knows.Knows(door), Is.True, $"Door slot {door} should be known to staff.");
            }

            for (int room = 0; room < floor.Geometry.RoomCount; room++)
            {
                Assert.That(knows.HasLookedOver(room), Is.True);
            }
        }

        [Test]
        public void AVisitor_KnowsOnlyTheDoorOfTheRoomTheyStartIn()
        {
            Floor floor = Build((TheBuilding.MeetingRoom, 0));
            AgentKnowledge knows = floor.Visitor(0).Knowledge;

            Assert.That(knows.KnowsEverything, Is.False);
            Assert.That(knows.Knows(floor.Door(TheBuilding.MeetingRoomDoor)), Is.True);
            Assert.That(knows.Knows(floor.Door(TheBuilding.TheWayOut)), Is.False);
            Assert.That(knows.Knows(floor.Door(TheBuilding.Archway)), Is.False);
            Assert.That(knows.HasLookedOver(floor.Room(TheBuilding.MeetingRoom)), Is.True);
            Assert.That(knows.HasLookedOver(floor.Room(TheBuilding.Corridor)), Is.False);
        }

        [Test]
        public void TheDefaultMeeting_IsFiveVisitorsAndTheirHost()
        {
            AgentDefinition[] cast = PrototypeBuilding.DefaultAgents();
            ulong[] visitors = cast.Where(a => a.Familiarity == AgentFamiliarity.Visitor)
                .Select(a => a.AgentId.Value).OrderBy(id => id).ToArray();

            Assert.That(visitors, Is.EqualTo(new[] { 1009UL, 1010UL, 1011UL, 1012UL, 1013UL }));
            Assert.That(cast.Single(a => a.AgentId.Value == 1014UL).Familiarity,
                Is.EqualTo(AgentFamiliarity.KnowsTheBuilding), "The host works here.");
        }

        [Test]
        public void AFamiliarityThatIsNotOne_IsRefused()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents[0] = data.Agents[0].WithFamiliarity((AgentFamiliarity)7);

            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }

        // ------------------------------------------------------------ seeing

        /// <summary>
        /// Stood at the end of the corridor looking into the T: the dead-end
        /// arm is in sight, the way out at the far end of the other arm is not.
        /// A few steps into the T, it is.
        /// </summary>
        [Test]
        public void AtTheT_TheArchIsSeenButTheWayOutIsNot_UntilTheyStepIntoIt()
        {
            Floor floor = Build((new LogicalPosition(12500, 7500), 90));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);
            int wayOut = floor.Door(TheBuilding.TheWayOut);

            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(floor.Door(TheBuilding.Archway)), Is.True);
            Assert.That(visitor.Knowledge.Knows(wayOut), Is.False, "9.7 m off is out of sight.");

            visitor.Body.MoveWithoutTellingTheCrowd(new LogicalPosition(14500, 9500));
            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(wayOut), Is.True, "7.5 m off is in sight.");
            CausalEvent found = floor.Context.Events.Events.Single(
                e => e.EventType == CausalEventType.AgentFoundTheWayOut);
            Assert.That(found.SourceId, Is.EqualTo(visitor.Id));
            Assert.That((WayLearned)found.Strength, Is.EqualTo(WayLearned.Saw));
        }

        [Test]
        public void ThroughAnOpenDoor_TheNextRoomsDoorsAreSeen_ButNotThroughAShutOne()
        {
            Floor floor = Build((new LogicalPosition(-2000, 9500), 180));
            Agent visitor = floor.Visitor(0);
            int officeDoor = floor.Door(TheBuilding.OfficeDoor);

            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(officeDoor), Is.False, "Nobody sees through a shut door.");

            floor.DoorStates[floor.Door(TheBuilding.MeetingRoomDoor)].State = DoorState.Open;
            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(officeDoor), Is.True,
                "With the meeting room door open, the office door across the corridor is in plain sight.");
        }

        [Test]
        public void SomebodyWhoWorksHere_LearnsNothingAndLogsNothing()
        {
            Floor floor = Build((new LogicalPosition(14500, 9500), 0));
            Frighten(floor.People[0]);

            floor.Wayfinding.Look(floor.People[0]);

            Assert.That(floor.Context.Events.Events, Is.Empty);
        }

        // ------------------------------------------------------------ signs

        [Test]
        public void EveryDefaultSign_TeachesTheWalkToTheWayOut()
        {
            Floor floor = Build((TheBuilding.MeetingRoom, 0));
            int wayOut = floor.Door(TheBuilding.TheWayOut);
            int archway = floor.Door(TheBuilding.Archway);
            int corridor = floor.Room(TheBuilding.Corridor);

            for (int sign = 0; sign < floor.Signs.Count; sign++)
            {
                int[] teaches = floor.Wayfinding.WhatSignTeaches(sign);
                Assert.That(teaches, Is.Not.Empty, $"Sign {sign} points at the way out.");
                Assert.That(teaches[teaches.Length - 1], Is.EqualTo(wayOut), $"Sign {sign} ends at the way out.");
                if (floor.Room(floor.Signs.At(sign)) == corridor)
                {
                    Assert.That(teaches, Is.EqualTo(new[] { archway, wayOut }),
                        $"Sign {sign} is in the corridor: the way is through the arch and up the arm.");
                }
            }
        }

        [Test]
        public void ASignPointingAwayFromEveryWayOut_TeachesNothing()
        {
            ScenarioData data = scenario.ToRuntimeData();

            // In the middle of the office, pointing south: the only way out of
            // the office is north, through its door onto the corridor.
            data.ExitSigns = new[] { new ExitSignDefinition(TheBuilding.Office, 180) };
            Floor floor = Build(data, (TheBuilding.MeetingRoom, 0));

            Assert.That(floor.Wayfinding.WhatSignTeaches(0), Is.Empty);
        }

        /// <summary>
        /// In the corridor facing east with a sign three metres ahead: somebody
        /// frightened takes it in, somebody wandering past does not.
        /// </summary>
        [Test]
        public void ASign_TeachesTheFrightened_AndIsLostOnTheCalm()
        {
            Floor floor = Build((new LogicalPosition(0, 8000), 90));
            floor.Context.Tick = 7;
            Agent visitor = floor.Visitor(0);
            visitor.Intent.NextPanicDecisionTick = 60;
            int wayOut = floor.Door(TheBuilding.TheWayOut);

            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(wayOut), Is.False, "Nobody strolling reads the exit signs.");

            Frighten(visitor);
            floor.Wayfinding.Look(visitor);
            Assert.That(visitor.Knowledge.Knows(wayOut), Is.True);
            Assert.That(visitor.Knowledge.Knows(floor.Door(TheBuilding.Archway)), Is.True,
                "A sign tells them the whole way, not just the last door.");
            CausalEvent found = floor.Context.Events.Events.Single(
                e => e.EventType == CausalEventType.AgentFoundTheWayOut);
            Assert.That((WayLearned)found.Strength, Is.EqualTo(WayLearned.Sign));
            // A few ticks late, like every reaction: nobody thinks again on
            // the tick a thing happens.
            Assert.That(visitor.Intent.NextPanicDecisionTick,
                Is.InRange(floor.Context.Tick + 1, floor.Context.Tick + floor.Context.Scenario.Perception.ReactionLagMaximumTicks),
                "Learning of a way out makes them think again, a moment later.");
        }

        // ------------------------------------------------------------ routes

        /// <summary>
        /// A long table across the cafeteria between its two doors. Somebody
        /// by the corridor door who wants the shortcut cannot walk the line to
        /// it any more: they go round the table's far end, or out by the
        /// corridor door and round through the corridor, whichever is the
        /// shorter walk, and either is far longer than the line. Only the
        /// shortcut is known, so the route has to name it, and its cost is
        /// that walk: the same squares the feet will follow.
        /// </summary>
        [Test]
        public void ARoute_CostsTheWalkRoundATable_NotTheLine()
        {
            var byTheCorridorDoor = new LogicalPosition(6000, 9600);
            long line = IntegerMath.Distance(byTheCorridorDoor, new LogicalPosition(13000, 12000));

            long CostOfTheShortcut(ScenarioData data)
            {
                Floor floor = Build(data, (byTheCorridorDoor, 0));
                Agent visitor = floor.Visitor(0);
                int shortcut = floor.Door(TheBuilding.CafeteriaShortcut);
                int from = floor.Room(TheBuilding.Cafeteria);
                int to = floor.Geometry.RoomBeyond(shortcut, from);
                Assert.That(floor.Geometry.TryFindKnownRoute(from, byTheCorridorDoor, to, visitor,
                        out int first, out _, out long cost), Is.True);
                Assert.That(first, Is.EqualTo(shortcut));
                return cost;
            }

            long clear = CostOfTheShortcut(scenario.ToRuntimeData());
            Assert.That(clear, Is.GreaterThan(line * 9 / 10).And.LessThan(line * 13 / 10),
                "With the floor clear, the walk is about the straight line.");

            ScenarioData blocked = scenario.ToRuntimeData();
            blocked.Tables = blocked.Tables.Concat(new[]
            {
                new TableDefinition(new SimulationId(4999UL), new LogicalPosition(7900, 10700), 9800, 1000),
            }).ToArray();
            long round = CostOfTheShortcut(blocked);
            Assert.That(round, Is.GreaterThan(line * 3 / 2),
                "With the table in the way, the route costs a real walk round it, not the line through it.");
        }

        [Test]
        public void ARoute_OnlyCrossesDoorsTheyKnow()
        {
            Floor floor = Build((TheBuilding.MeetingRoom, 0));
            Agent visitor = floor.Visitor(0);
            int from = floor.Room(TheBuilding.MeetingRoom);
            int to = floor.Room(TheBuilding.Crossbar);

            Assert.That(floor.Geometry.TryFindRoute(from, TheBuilding.MeetingRoom, to, visitor, out _, out _, out _),
                Is.True, "The building has a way.");
            Assert.That(floor.Geometry.TryFindKnownRoute(from, TheBuilding.MeetingRoom, to, visitor, out _, out _, out _),
                Is.False, "They do not know the arch is there.");

            floor.Wayfinding.Learn(visitor, floor.Door(TheBuilding.Archway), WayLearned.Told, 0UL);
            Assert.That(floor.Geometry.TryFindKnownRoute(from, TheBuilding.MeetingRoom, to, visitor,
                    out int first, out _, out _),
                Is.True);
            Assert.That(first, Is.EqualTo(floor.Door(TheBuilding.MeetingRoomDoor)));
        }

        // ------------------------------------------------------------ being told

        [Test]
        public void ALeader_TellsAFollowerEverythingTheyKnow()
        {
            Floor floor = Build((TheBuilding.MeetingRoom, 0), (new LogicalPosition(-2000, 10500), 0));
            Agent host = floor.People[0];
            Agent client = floor.Visitor(1);
            Frighten(client);
            ulong rally = floor.Context.Events.Append(0, host.Id, CausalEventType.LeaderCalledPeopleOn,
                host.Body.Position).EventId;

            floor.Wayfinding.Share(host, client, rally);

            for (int door = 0; door < floor.Geometry.DoorCount; door++)
            {
                Assert.That(client.Knowledge.Knows(door), Is.True, $"Door {door} was not passed on.");
            }

            Assert.That(client.Knowledge.HasLookedOver(floor.Room(TheBuilding.Maintenance)), Is.True,
                "Nor would they bother looking in a room the host knows is a cupboard.");
            CausalEvent told = floor.Context.Events.Events.Single(
                e => e.EventType == CausalEventType.AgentFoundTheWayOut);
            Assert.That((WayLearned)told.Strength, Is.EqualTo(WayLearned.Told));
            Assert.That(told.CausalParentEventId, Is.EqualTo(rally));
        }

        [Test]
        public void ADoorOpeningBesideThem_IsNews()
        {
            // Arrived in the T from the corridor, ten metres short of the way
            // out: too far off to have noticed a shut door at the end of it.
            Floor floor = Build((new LogicalPosition(12500, 7500), 90), (TheBuilding.MeetingRoom, 0));
            Agent nearby = floor.Visitor(0);
            Agent faraway = floor.Visitor(1);
            nearby.Body.MoveWithoutTellingTheCrowd(new LogicalPosition(14500, 7000));
            Frighten(nearby);
            Frighten(faraway);
            int wayOut = floor.Door(TheBuilding.TheWayOut);
            Assert.That(nearby.Knowledge.Knows(wayOut), Is.False);

            floor.Doors.Open(wayOut, 0UL);
            floor.DoorChoice.AnnounceWaysOut();

            Assert.That(nearby.Knowledge.Knows(wayOut), Is.True, "It opened in the room they are standing in.");
            Assert.That(faraway.Knowledge.Knows(wayOut), Is.False, "Two rooms and a shut door away, nobody hears it.");
            CausalEvent found = floor.Context.Events.Events.Single(
                e => e.EventType == CausalEventType.AgentFoundTheWayOut);
            Assert.That((WayLearned)found.Strength, Is.EqualTo(WayLearned.SawItOpen));
        }

        // ------------------------------------------------------------ dead ends

        [Test]
        public void ACupboardWithOnlyTheWayTheyCameIn_IsADeadEnd()
        {
            Floor floor = Build((TheBuilding.CorridorWestEnd, 270));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);
            floor.Wayfinding.Look(visitor);
            visitor.Knowledge.Searching = true;
            visitor.Knowledge.SearchEventId = floor.Context.Events.Append(0, visitor.Id,
                CausalEventType.AgentLookedForAWayOut, visitor.Body.Position).EventId;

            visitor.Body.MoveWithoutTellingTheCrowd(TheBuilding.Maintenance);
            floor.Wayfinding.Look(visitor);

            Assert.That(visitor.Knowledge.HasLookedOver(floor.Room(TheBuilding.Maintenance)), Is.True);
            CausalEvent deadEnd = floor.Context.Events.Events.Single(
                e => e.EventType == CausalEventType.AgentFoundADeadEnd);
            Assert.That(deadEnd.CausalParentEventId, Is.EqualTo(visitor.Knowledge.SearchEventId));
        }

        /// <summary>The bathroom has three stalls off it nobody has looked in yet, so it is not a dead end -- yet.</summary>
        [Test]
        public void ARoomWithDoorsToPlacesNotYetSeen_IsNoDeadEnd()
        {
            Floor floor = Build((new LogicalPosition(10500, 7000), 180));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);
            floor.Wayfinding.Look(visitor);
            visitor.Knowledge.Searching = true;

            visitor.Body.MoveWithoutTellingTheCrowd(new LogicalPosition(10500, 3500));
            floor.Wayfinding.Look(visitor);

            Assert.That(visitor.Knowledge.HasLookedOver(floor.Room(TheBuilding.Bathroom)), Is.True);
            Assert.That(floor.EventsOf(CausalEventType.AgentFoundADeadEnd), Is.Zero);
        }

        // ------------------------------------------------------------ searching

        [Test]
        public void AVisitorWhoKnowsNoWayOut_GoesLookingThroughTheOneDoorTheyKnow()
        {
            Floor floor = Build((new LogicalPosition(-2000, 10500), 180));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);

            int door = floor.DoorChoice.ChooseExitDoor(visitor);

            Assert.That(door, Is.EqualTo(floor.Door(TheBuilding.MeetingRoomDoor)));
            Assert.That(visitor.Doors.WayOutDoorIndex, Is.EqualTo(-1), "They know of no way out.");
            Assert.That(visitor.Knowledge.Searching, Is.True);
            Assert.That(floor.EventsOf(CausalEventType.AgentLookedForAWayOut), Is.EqualTo(1));
        }

        [Test]
        public void SomebodyWhoWorksHere_RunsStraightForTheWayOut_AndNeverSearches()
        {
            Floor floor = Build((new LogicalPosition(-2000, 10500), 180));
            Agent host = floor.People[0];
            Frighten(host);

            int door = floor.DoorChoice.ChooseExitDoor(host);

            Assert.That(door, Is.EqualTo(floor.Door(TheBuilding.MeetingRoomDoor)));
            Assert.That(host.Doors.WayOutDoorIndex, Is.EqualTo(floor.Door(TheBuilding.TheWayOut)));
            Assert.That(host.Knowledge.Searching, Is.False);
            Assert.That(floor.Context.Events.Events, Is.Empty);
        }

        /// <summary>
        /// In the corridor with the far end of it unseen, and every room off
        /// it already looked round: the only place left to look is further
        /// along.
        /// </summary>
        [Test]
        public void WithEveryRoomOffItLookedRound_TheySearchFurtherAlongTheCorridor()
        {
            // Facing the wall at the west end, so the sign down the corridor
            // -- which would simply tell them the way -- is behind them.
            Floor floor = Build((TheBuilding.CorridorWestEnd, 270));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);
            int corridor = floor.Room(TheBuilding.Corridor);
            for (int room = 0; room < floor.Geometry.RoomCount; room++)
            {
                visitor.Knowledge.CornersSeen[room] = room == corridor ? (byte)0 : AgentKnowledge.AllCorners;
            }

            // Stood at the west end, they can see the corners there; the east
            // end is nineteen metres off.
            floor.Wayfinding.Look(visitor);
            int door = floor.DoorChoice.ChooseExitDoor(visitor);

            Assert.That(door, Is.EqualTo(-1));
            Assert.That(visitor.Knowledge.HasSearchSpot, Is.True);
            Assert.That(visitor.Knowledge.SearchSpot.X, Is.GreaterThan(TheBuilding.CorridorWestEnd.X),
                "The unseen end of the corridor is east of them.");
        }

        [Test]
        public void WithNowhereLeftToLook_TheyStopSearchingAndHide()
        {
            Floor floor = Build((new LogicalPosition(-2000, 10500), 180));
            Agent visitor = floor.Visitor(0);
            Frighten(visitor);
            for (int room = 0; room < floor.Geometry.RoomCount; room++)
            {
                visitor.Knowledge.CornersSeen[room] = AgentKnowledge.AllCorners;
            }

            floor.DoorChoice.ChooseExitDoor(visitor);

            Assert.That(visitor.Knowledge.HasSearchSpot, Is.False);
            Assert.That(floor.EventsOf(CausalEventType.AgentLookedForAWayOut), Is.Zero,
                "With every room looked round there is nothing to search; they fall back to hiding.");
        }
    }
}

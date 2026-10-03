using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The tower of boxes (prototype 3, 2026-09-25). It stands in the
    /// junction's south-west corner all day; since 2026-10-02 it comes down
    /// only when somebody's body runs into it, a beat later, and the boxes
    /// tumble by physics into a heap a stride ahead of the knocked stack,
    /// the way the bumper was going (the owner: "purely dynamic so if an
    /// agent actually bumps into it, it falls. No director trigger").
    /// While enough of them lie still in the gap the archway is shut for
    /// people and fire, exactly as a doorway with something wedged in it:
    /// the strong throw a box clear, everybody else gives up and goes round
    /// through the stockroom, and when enough boxes are gone the way is
    /// open again. Where the fall is not the point, a test lays the heap
    /// itself, so it does not depend on where the physics lands eight boxes.
    /// </summary>
    public sealed class BoxTowerEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);
        private static readonly SimulationId TheRunner = new SimulationId(2UL);

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

        private static bool IsATowerBox(SimulationId id) => id.Value >= 3701UL && id.Value <= 3708UL;

        private static List<PhysicsObjectSnapshot> TowerBoxes(Run simulation)
        {
            var boxes = new List<PhysicsObjectSnapshot>();
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                if (IsATowerBox(thing.ObjectId))
                {
                    boxes.Add(thing);
                }
            }

            return boxes;
        }

        /// <summary>
        /// The archway's heap strip: 300 mm plus a box's 300 mm half-width
        /// either side of the archway's faces, which stand 100 mm (half the
        /// wall's thickness, 2026-09-30) off the wall line at x 13000, along
        /// its 2.4 m.
        /// </summary>
        private static bool IsInTheArchway(LogicalPosition where) =>
            Math.Abs(where.X - 13000) <= 700 && where.Z > 6000 && where.Z < 9000;

        /// <summary>Whether walking up an event's causes reaches this one.</summary>
        private static bool IsCausedBy(Run simulation, CausalEvent record, ulong cause)
        {
            var byId = new Dictionary<ulong, CausalEvent>();
            foreach (CausalEvent e in simulation.EventLog.Events)
            {
                byId[e.EventId] = e;
            }

            ulong at = record.CausalParentEventId;
            for (int hops = 0; hops < 20 && at != 0UL; hops++)
            {
                if (at == cause)
                {
                    return true;
                }

                if (!byId.TryGetValue(at, out CausalEvent parent))
                {
                    return false;
                }

                at = parent.CausalParentEventId;
            }

            return false;
        }

        private static int BoxesInTheArchway(Run simulation)
        {
            int count = 0;
            foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
            {
                if (!box.IsHeld && IsInTheArchway(box.Position))
                {
                    count++;
                }
            }

            return count;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

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
        /// The shipped building with only the tower's boxes in it, one person
        /// standing still where the test puts them, a second standing two
        /// hand's breadths west of the tower's west stack, facing it, to be
        /// shoved into it when told to, and the fire due in the meeting room
        /// at the tick given. Nobody decides anything of their own.
        /// </summary>
        private ScenarioData TwoPeopleAndTheTower(LogicalPosition where, AgentTraitValues traits, int fireTick)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, where, CardinalDirection.East, traits),
                new AgentDefinition(TheRunner, BesideTheTower, CardinalDirection.East, new AgentTraitValues(1, 5, 5, 5, 2, 5, 4))
            };
            data.PhysicsObjects = Array.FindAll(data.PhysicsObjects, thing => IsATowerBox(thing.ObjectId));
            data.Tables = Array.Empty<TableDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Fire.ActivationTick = fireTick;
            TheBuilding.FireAt(data, TheBuilding.MeetingRoom);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        /// <summary>Just inside the corridor's east end, near the tower and out of the archway.</summary>
        private static readonly LogicalPosition NearTheTower = new LogicalPosition(12500, 6800);

        /// <summary>
        /// The tower's west stack: its boxes stand one on another here,
        /// against the corridor's north wall short of the archway (since
        /// 2026-10-02; it stood in the junction's south-west corner).
        /// </summary>
        private static readonly LogicalPosition TheWestStack = new LogicalPosition(11700, 8550);

        /// <summary>In the corridor, two hand's breadths west of the west stack's face: a shove east meets it, as a runner making for the archway would.</summary>
        private static readonly LogicalPosition BesideTheTower = new LogicalPosition(10950, 8550);

        /// <summary>A hand's breadth south of the east stack's face: a nudge north brushes it.</summary>
        private static readonly LogicalPosition BrushingTheTower = new LogicalPosition(12300, 7900);

        private const int North = 0;
        private const int East = 90;

        /// <summary>
        /// The one beside the tower is shoved into it at a sprinter's pace
        /// (2026-10-02: only a body running into it brings it down), and it
        /// comes down a beat later.
        /// </summary>
        private static CausalEvent BringTheTowerDown(Run simulation)
        {
            simulation.ShoveAgentForTests(1, East, 100);
            CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 60);
            Assert.That(fell.HasValue, "Somebody shoved into the tower at a run should have brought it down.");
            return fell.Value;
        }

        [Test]
        public void OnceAFallenBoxIsHeldWhereItLies_ItNeverMovesAgainByItself()
        {
            // The owner's note (2026-09-28): "when the tower tips, usually it
            // moves by itself after a few seconds without input, glides on
            // the floor". The real fall, on the office's own seed, watched
            // for fifteen seconds: a box that has been held where it lies
            // stays there, whatever the boxes still tumbling do to it. Nobody
            // here is strong enough to heave one, which is the one thing that
            // may still move a held box.
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, AgentTraitValues.AllOrdinary, 200);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 201);
                BringTheTowerDown(simulation);
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                var heldAt = new Dictionary<int, LogicalPosition>();
                var moved = new List<string>();
                for (int t = 0; t < 15 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < 8; i++)
                    {
                        int box = objects.IndexOf(new SimulationId(3701UL + (ulong)i));
                        bool held = objects.IsPinned(box);
                        if (heldAt.TryGetValue(box, out LogicalPosition where))
                        {
                            if (!held || IntegerMath.Distance(objects.PositionOf(box), where) > 50)
                            {
                                moved.Add($"box {3701 + i} at tick {simulation.Tick}: held at {where}, now {objects.PositionOf(box)}, held {held}");
                            }
                        }
                        else if (held)
                        {
                            heldAt[box] = objects.PositionOf(box);
                        }
                    }
                }

                Assert.That(heldAt, Is.Not.Empty, "Some of the fallen boxes came to rest and were held where they lay.");
                Assert.That(moved, Is.Empty, "A box held where it lies stays there: " + string.Join("; ", moved));
            }
        }

        /// <summary>
        /// Lays the fallen boxes exactly where the old placed fall put them
        /// -- a row of four along the wall line 350 mm inside the corridor,
        /// and four more on top -- so a test about the heap does not depend
        /// on where the physics lands them. With <paramref name="inTheGap"/>
        /// fewer than eight, the rest are stood out of the way in the crossbar.
        /// </summary>
        private static void LayTheHeap(Run simulation, int inTheGap = 8)
        {
            PhysicsObjectSystem objects = simulation.ObjectsForTests;
            for (int i = 0; i < 8; i++)
            {
                int box = objects.IndexOf(new SimulationId(3701UL + (ulong)i));
                if (i < inTheGap)
                {
                    simulation.PlaceObjectForTests(box, new LogicalPosition(12650, 6600 + 600 * (i % 4)), i < 4 ? 0 : 600, 0);
                }
                else
                {
                    simulation.PlaceObjectForTests(box, new LogicalPosition(15500, 11000 + 800 * i), 0, 0);
                }
            }
        }

        private static CausalEvent LayTheHeapAndLetItSettle(Run simulation, int inTheGap = 8)
        {
            LayTheHeap(simulation, inTheGap);
            CausalEvent? settled = AdvanceUntil(simulation, CausalEventType.BoxHeapSettled, 200);
            Assert.That(settled.HasValue, "Enough boxes lying still in the gap shut the archway.");
            Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.True);
            return settled.Value;
        }

        [Test]
        public void TheTower_StandsUntilSomebodyRunsIntoIt_AndTumblesABeatLater_WithNoCreak()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, AgentTraitValues.AllOrdinary, 200);
            using (var simulation = new Run(data, 42UL))
            {
                var standing = new Dictionary<SimulationId, LogicalPosition>();
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    standing[box.ObjectId] = box.Position;
                }

                // Calm, and then with the fire lit down the corridor: nothing
                // arms it, and somebody standing right beside it brings
                // nothing down.
                Advance(simulation, 250);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody standing beside it, fire or no fire, brings nothing down.");
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    Assert.That(IntegerMath.Distance(box.Position, standing[box.ObjectId]), Is.LessThan(50),
                        $"Box {box.ObjectId} should still be standing against the wall.");
                }

                Assert.That(Door(simulation, TheBuilding.Archway).State, Is.EqualTo(DoorState.Broken), "An archway: open.");

                CausalEvent fell = BringTheTowerDown(simulation);
                List<CausalEvent> triggered = EventsOfType(simulation, CausalEventType.TrapTriggered);
                Assert.That(triggered, Has.Count.EqualTo(1), "One knock.");
                Assert.That(triggered[0].SourceId, Is.EqualTo(TheBuilding.TheTrap));
                Assert.That(triggered[0].TargetId, Is.EqualTo(TheRunner), "Knocked by whoever ran into it.");
                Assert.That(triggered[0].Strength, Is.GreaterThanOrEqualTo(data.Traps.BumpSpeedMillimetresPerTick),
                    "At a running pace or more.");
                Assert.That(EventsOfType(simulation, CausalEventType.TrapCreaked), Is.Empty, "No creak: a knock is a knock.");
                Assert.That(fell.Tick - triggered[0].Tick,
                    Is.InRange(data.Perception.ReactionLagMinimumTicks, data.Perception.ReactionLagMaximumTicks),
                    "Never on the tick it was knocked: a beat later.");
                Assert.That(fell.CausalParentEventId, Is.EqualTo(triggered[0].EventId));
                Assert.That(fell.TargetId, Is.EqualTo(TheBuilding.Archway));

                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.That(objects.IsPinned(i), Is.False, $"Box {objects.IdOf(i)} is a loose box from the fall on.");
                }

                // Four seconds later the boxes have flown, bounced and come
                // to rest somewhere: most have left where they stood (the
                // bottom of each column, with the others landing on it, may
                // barely shift, and against the wall one more may drop
                // almost straight down), and the archway is shut exactly
                // when enough of them lie in it.
                Advance(simulation, 4 * Run.TicksPerSecond);
                int tumbled = 0;
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    if (IntegerMath.Distance(standing[box.ObjectId], box.Position) > 300)
                    {
                        tumbled++;
                    }

                    Assert.That(box.Pose.IsKnown, Is.True, $"Box {box.ObjectId} is drawn where the engine has it.");
                }

                Assert.That(tumbled, Is.GreaterThanOrEqualTo(5), "Most of the boxes tumbled well away from where they stood.");

                int inTheGap = BoxesInTheArchway(simulation);
                DoorSnapshot archway = Door(simulation, TheBuilding.Archway);
                Assert.That(archway.IsPiled, Is.EqualTo(inTheGap >= data.Traps.PileHoldsAtBoxes),
                    $"{inTheGap} boxes lie in the archway: shut only while three or more do.");
                if (archway.IsPiled)
                {
                    Assert.That(archway.State, Is.EqualTo(DoorState.Unlocked), "Shut for people and fire.");
                    Assert.That(archway.IsJammed, Is.True, "With the boxes wedged in it.");
                }

                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(fell), Is.EqualTo("the tower of boxes came down by door 2016"));
            }
        }

        /// <summary>
        /// The owner's rule (2026-10-02): "purely dynamic". The boxes come
        /// down in a heap a stride ahead of the stack that was knocked, the
        /// way the bumper was going: shoved east into the west stack, as a
        /// runner making for the archway is, the heap lies east of where it
        /// stood -- toward the archway, which is why the tower stands here.
        /// </summary>
        [Test]
        public void TheTower_ComesDownAheadOfTheKnockedStack_TheWayTheBumperWasGoing()
        {
            ScenarioData data = TwoPeopleAndTheTower(new LogicalPosition(-5000, -5000), AgentTraitValues.AllOrdinary, int.MaxValue);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                CausalEvent fell = BringTheTowerDown(simulation);
                var ahead = new LogicalPosition(TheWestStack.X + data.Traps.HeapAheadMillimetres, TheWestStack.Z);
                Assert.That(IntegerMath.Distance(fell.Position, ahead), Is.LessThanOrEqualTo(400),
                    "It comes down a stride ahead of the knocked stack, the way the bumper was going.");

                Advance(simulation, 4 * Run.TicksPerSecond);
                int nearTheSpot = 0;
                int eastOfTheStack = 0;
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    if (IntegerMath.Distance(box.Position, ahead) <= 2000)
                    {
                        nearTheSpot++;
                    }

                    if (box.Position.X > TheWestStack.X + 300)
                    {
                        eastOfTheStack++;
                    }
                }

                Assert.That(nearTheSpot, Is.GreaterThanOrEqualTo(5),
                    $"Most of the boxes lie in a heap on the spot: {nearTheSpot} of 8 within two metres.");
                Assert.That(eastOfTheStack, Is.GreaterThanOrEqualTo(5),
                    $"And they went the bumper's way: {eastOfTheStack} of 8 lie east of where the stack stood.");
            }
        }

        /// <summary>
        /// A calm walk tops out well under the bump speed, so somebody
        /// brushing the tower at a walk brings nothing down; a body at a
        /// running pace does, whoever they are -- calm, with nothing
        /// burning anywhere -- because it is the knock that topples it.
        /// </summary>
        [Test]
        public void ABodyAtAWalk_BringsNothingDown_AndABodyAtARunDoes_FireOrNoFire()
        {
            ScenarioData data = TwoPeopleAndTheTower(BrushingTheTower, AgentTraitValues.AllOrdinary, int.MaxValue);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                simulation.ShoveAgentForTests(0, North, data.Traps.BumpSpeedMillimetresPerTick - 8);
                Advance(simulation, 100);
                Assert.That(simulation.GetAgent(0).Position.Z, Is.GreaterThan(BrushingTheTower.Z),
                    "They were pushed up against the east stack.");
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Brushed at a walking pace, the tower stands.");
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.That(objects.IsPinned(i), Is.True, $"Box {objects.IdOf(i)} still stands in the tower.");
                }

                simulation.ShoveAgentForTests(1, East, 100);
                CausalEvent? knocked = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 60);
                Assert.That(knocked.HasValue, "Run into at a sprinter's pace, it is knocked.");
                Assert.That(knocked.Value.TargetId, Is.EqualTo(TheRunner));
                Assert.That(simulation.GetAgent(1).FearState, Is.EqualTo(AgentFearState.Calm),
                    "By somebody calm, with nothing burning: nothing arms it but the knock.");
                Assert.That(knocked.Value.HasCausalParent, Is.False, "A calm bumper's knock is nobody's fault but their own.");
                Assert.That(AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 60).HasValue, "And down it comes.");
            }
        }

        /// <summary>
        /// Nobody may take a box from the standing tower, however strong:
        /// every way of taking a thing -- tidying it away, wedging it in a
        /// door, throwing it clear, hurling it aside at a run -- asks whether
        /// it can be lifted, and the answer for a standing tower box is no.
        /// Once it has fallen the boxes are ordinary boxes, held by nothing
        /// -- and, since 2026-09-27, too heavy for anybody to carry (the
        /// owner: "bigger boxes need to be heavier"), so the heap is cleared
        /// by the strong heaving them aside, or by fire.
        /// </summary>
        [Test]
        public void NobodyMayTakeABoxFromTheStandingTower_AndOnceItHasFallenTheBoxesAreLooseButTooHeavyToCarry()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, new AgentTraitValues(10, 5, 9, 5, 2, 3, 4), 60);
            using (var simulation = new Run(data, 42UL))
            {
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                Agent strongest = simulation.AgentForTests(0);
                simulation.Step();
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.That(objects.CanLift(strongest, i), Is.False,
                        $"Box {objects.IdOf(i)} of the standing tower could be taken by somebody as strong as anyone can be.");
                    Assert.That(objects.CanThrowClear(strongest, i), Is.False);
                }

                Advance(simulation, 60);
                BringTheTowerDown(simulation);
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.That(objects.IsPinned(i), Is.False, $"Box {objects.IdOf(i)} is held by nothing now.");
                    Assert.That(objects.CanLift(strongest, i), Is.False,
                        $"Box {objects.IdOf(i)} off the fallen tower weighs more than anybody can carry.");
                }
            }
        }

        /// <summary>
        /// A calm person with every chance to tidy things away never picks up
        /// a box from the standing tower, all day long.
        /// </summary>
        [Test]
        public void ATidyPerson_NeverCarriesOffABoxFromTheStandingTower()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, new AgentTraitValues(8, 5, 5, 5, 2, 5, 4), int.MaxValue);
            data.Calm.DecisionMinimumTicks = 20;
            data.Calm.DecisionMaximumTicks = 40;
            using (var simulation = new Run(data, 42UL))
            {
                var standing = new Dictionary<SimulationId, LogicalPosition>();
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    standing[box.ObjectId] = box.Position;
                }

                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                    {
                        Assert.That(box.IsHeld, Is.False, $"Tick {simulation.Tick}: box {box.ObjectId} was picked up off the standing tower.");
                    }
                }

                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    Assert.That(IntegerMath.Distance(box.Position, standing[box.ObjectId]), Is.LessThan(50),
                        $"Box {box.ObjectId} should still be standing against the wall.");
                }
            }
        }

        /// <summary>
        /// The boxes are thrown things while they fly: whoever stands in
        /// their way is hit by them, as by any box hurled across a room.
        /// </summary>
        [Test]
        public void SomebodyStandingWhereTheBoxesLand_IsHitByThemAsTheyComeDown()
        {
            // Where the heap is aimed: a stride east of the west stack, the
            // way the bumper is shoved, three boxes to a row across the
            // corridor; this is the south end of that row, just short of the
            // archway.
            ScenarioData data = TwoPeopleAndTheTower(new LogicalPosition(12750, 7750), AgentTraitValues.AllOrdinary, 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 11);
                CausalEvent fell = BringTheTowerDown(simulation);
                Advance(simulation, 2 * Run.TicksPerSecond);
                List<CausalEvent> hits = EventsOfType(simulation, CausalEventType.BoxHitAgent);
                Assert.That(hits.Exists(hit => IsATowerBox(hit.SourceId) && hit.TargetId == Somebody), Is.True,
                    "A box off the tower should have hit the person standing where it fell.");
                CausalEvent hit = hits.Find(record => IsATowerBox(record.SourceId) && record.TargetId == Somebody);
                Assert.That(IsCausedBy(simulation, hit, fell.EventId), Is.True,
                    "Traced to the tower coming down, through whatever the box bounced off on the way.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTripped).Exists(record => record.SourceId == Somebody) ||
                            EventsOfType(simulation, CausalEventType.AgentKnockedDown).Exists(record => record.SourceId == Somebody),
                    "Thirteen kilograms at two and a half metres a second is enough to knock them off their feet.");
            }
        }

        [Test]
        public void AFrightenedPersonInTheCorridor_CannotGetThroughTheArchway_AndGivesItUp()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, new AgentTraitValues(1, 5, 5, 5, 2, 5, 4), 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 11);
                BringTheTowerDown(simulation);
                LayTheHeapAndLetItSettle(simulation);

                simulation.FrightenForTests(0);
                LogicalPosition last = simulation.GetAgent(Somebody).Position;
                for (int t = 0; t < 40 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    LogicalPosition now = simulation.GetAgent(Somebody).Position;
                    if (last.X < 13000 && now.X >= 13000 && now.Z >= 6000 && now.Z <= 9000)
                    {
                        Assert.Fail($"Tick {simulation.Tick}: they walked through the archway with the boxes across it.");
                    }

                    last = now;
                }

                // Too weak to throw a box clear, they see the heap for what it
                // is and go round: the way through another door.
                List<CausalEvent> tried = EventsOfType(simulation, CausalEventType.AgentTriedDoor);
                Assert.That(tried.Exists(record => record.SourceId == Somebody && record.TargetId != TheBuilding.Archway), Is.True,
                    "They gave the archway up and went for another door.");
                Assert.That(EventsOfType(simulation, CausalEventType.BoxPileCleared), Is.Empty);
            }
        }

        [Test]
        public void SomebodyStrong_HeavesABoxAside()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, new AgentTraitValues(9, 5, 9, 5, 2, 3, 4), 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 11);
                BringTheTowerDown(simulation);
                LayTheHeapAndLetItSettle(simulation);
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);

                List<CausalEvent> heaved = EventsOfType(simulation, CausalEventType.AgentShovedObstruction);
                Assert.That(heaved.Exists(record => record.SourceId == Somebody && IsATowerBox(record.TargetId)), Is.True,
                    "Too heavy to throw, a box off the heap is heaved aside by somebody strong.");
            }
        }

        /// <summary>
        /// The heap is a fact about where the boxes lie: it counts once
        /// enough of them have come to rest in the gap, it stops counting
        /// when too few are left there, and it counts again if they are put
        /// back. A box kicked about inside the gap opens nothing.
        /// </summary>
        [Test]
        public void TheHeap_CountsOnceThreeBoxesRestInTheGap_ClearsWhenFewerDo_AndCountsAgainWhenTheyAreBack()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, AgentTraitValues.AllOrdinary, 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 11);
                BringTheTowerDown(simulation);
                LayTheHeap(simulation, inTheGap: 3);
                Advance(simulation, 5);
                Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.False, "Not until they have lain still for half a second.");
                CausalEvent? settled = AdvanceUntil(simulation, CausalEventType.BoxHeapSettled, 100);
                Assert.That(settled.HasValue, "Three boxes at rest in the gap shut it.");
                Assert.That(settled.Value.Strength, Is.EqualTo(3));
                Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.True);

                // Kicked along the gap: still in it, so the way stays shut.
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                int first = objects.IndexOf(new SimulationId(3701UL));
                simulation.LaunchObjectForTests(first, 0, 20);
                for (int t = 0; t < 10; t++)
                {
                    simulation.Step();
                    Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.True, $"Tick {simulation.Tick}: a box kicked about inside the gap opens nothing.");
                }

                // Put out of the gap: two left, and half a second later the
                // way is open.
                simulation.PlaceObjectForTests(first, new LogicalPosition(11000, 7500), 0, 0);
                Advance(simulation, 5);
                Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.True, "Not the moment a box leaves: it may be coming straight back.");
                CausalEvent? cleared = AdvanceUntil(simulation, CausalEventType.BoxPileCleared, 60);
                Assert.That(cleared.HasValue, "Fewer than three in the gap for half a second: the way is open.");
                Assert.That(cleared.Value.CausalParentEventId, Is.EqualTo(settled.Value.EventId));
                Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.False);

                // Put back: three again, and it shuts again once they have settled.
                simulation.PlaceObjectForTests(first, new LogicalPosition(12650, 6600), 0, 0);
                for (int t = 0; t < 100 && EventsOfType(simulation, CausalEventType.BoxHeapSettled).Count < 2; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOfType(simulation, CausalEventType.BoxHeapSettled), Has.Count.EqualTo(2), "Shut again.");
                Assert.That(Door(simulation, TheBuilding.Archway).IsPiled, Is.True);
            }
        }

        /// <summary>
        /// The fire is held at the archway: it cannot cross a shut doorway,
        /// and the boxes lying in the corridor catch, burn and are gone
        /// before it is a doorway again. Cardboard burns fast here so the
        /// test is short.
        /// </summary>
        [Test]
        public void TheFire_IsHeldAtTheArchway_UntilTheBoxesThemselvesBurn()
        {
            ScenarioData data = TwoPeopleAndTheTower(new LogicalPosition(12600, 6800), AgentTraitValues.AllOrdinary, 1);
            data.Fire.SpawnBounds = new LogicalBounds(8250, 8250, 7250, 7250);
            data.Fire.SpreadMinimumTicks = 8;
            data.Fire.SpreadMaximumTicks = 12;
            data.Flammables.BoxIgniteTicks = 25;
            data.Flammables.BoxBurnMinimumTicks = 60;
            data.Flammables.BoxBurnMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2);
                BringTheTowerDown(simulation);
                LayTheHeapAndLetItSettle(simulation);

                int firstJunctionFire = -1;
                for (int t = 0; t < 40 * Run.TicksPerSecond && firstJunctionFire < 0; t++)
                {
                    simulation.Step();
                    foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
                    {
                        if (cell.Bounds.MinX >= 13000 && cell.Bounds.MinZ >= 6000 && cell.Bounds.MaxZ <= 9000)
                        {
                            firstJunctionFire = simulation.Tick;
                            break;
                        }
                    }
                }

                List<CausalEvent> caught = EventsOfType(simulation, CausalEventType.ObjectCaughtFire);
                Assert.That(caught.Exists(record => IsATowerBox(record.SourceId)), Is.True, "The flames in the corridor should light the boxes.");
                List<CausalEvent> cleared = EventsOfType(simulation, CausalEventType.BoxPileCleared);
                Assert.That(cleared, Is.Not.Empty, "Burnt, the boxes no longer hold the way shut.");
                Assert.That(firstJunctionFire, Is.GreaterThan(0), "And then the fire comes through.");
                Assert.That(firstJunctionFire, Is.GreaterThanOrEqualTo(cleared[0].Tick),
                    "Nothing burns beyond the archway while the boxes hold it: the doorway holds the fire.");
            }
        }

        [Test]
        public void TheDefaultBuilding_HasTheTowerAgainstTheCorridorsNorthWall_ShortOfTheArchway()
        {
            ScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.TrapDefinitions, Has.Length.EqualTo(2), "The tower, and the stockroom's stack.");
            Assert.That(data.TrapDefinitions[0].TrapId, Is.EqualTo(TheBuilding.TheTrap));
            Assert.That(data.TrapDefinitions[0].DoorId, Is.EqualTo(TheBuilding.Archway));
            Assert.That(data.TrapDefinitions[0].BoxIds, Has.Length.EqualTo(8));
            foreach (SimulationId id in data.TrapDefinitions[0].BoxIds)
            {
                PhysicsObjectDefinition box = Array.Find(data.PhysicsObjects, thing => thing.ObjectId == id);
                Assert.That(box.Kind, Is.EqualTo(PhysicsObjectKind.Box));
                Assert.That(box.InitialPosition.X, Is.InRange(11000, 13000).And.LessThan(12550),
                    "In the corridor, the inside of the turn toward the way out, clear of the archway's wall line by more than the 150 mm that would wedge it.");
                Assert.That(box.InitialPosition.Z, Is.InRange(8300, 8600), "Against the corridor's north wall.");
            }
        }

        [Test]
        public void ATrapNamingADoorThatIsNotAnArchway_IsRefused()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.TrapDefinitions = new[]
            {
                new TrapDefinition(TheBuilding.TheTrap, TheBuilding.OfficeDoor, data.TrapDefinitions[0].BoxIds)
            };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }
    }
}

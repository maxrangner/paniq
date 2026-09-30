using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The tower of boxes (prototype 3, 2026-09-25): the Director's first
    /// trap. It stands in the junction's south-west corner all day; once the
    /// fire is lit, the first frightened person to run along the corridor
    /// brings it down a beat later (2026-09-27), and the boxes tumble by
    /// physics toward the archway between the corridor and the crossbar.
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
        /// standing still at the corridor's east end near the tower, a weak
        /// runner at the corridor's far west end to bring the tower down
        /// when told to, and the fire due in the meeting room at the tick
        /// given. Nobody decides anything of their own.
        /// </summary>
        private ScenarioData TwoPeopleAndTheTower(LogicalPosition where, AgentTraitValues traits, int fireTick)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, where, CardinalDirection.East, traits),
                new AgentDefinition(TheRunner, TheBuilding.CorridorWestEnd, CardinalDirection.East, new AgentTraitValues(1, 5, 5, 5, 2, 5, 4))
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

            // The heap tests are about the heap, not the creak (2026-09-29):
            // the tower comes down a tick after it is sprung here, before the
            // runner can reach the archway and stand in the gap. The one test
            // of the timing puts the creak back.
            data.Traps.CreakTicks = 1;
            return data;
        }

        /// <summary>Just inside the corridor's east end, within reach of the tower and out of the archway.</summary>
        private static readonly LogicalPosition NearTheTower = new LogicalPosition(12500, 6800);

        /// <summary>
        /// Once the fire is lit: the runner takes fright and runs, and the
        /// tower comes down a beat later -- since 2026-09-30 once somebody runs
        /// past it in the crossbar, so the runner's whole length of corridor
        /// is allowed for, and the creak.
        /// </summary>
        private static CausalEvent BringTheTowerDown(Run simulation)
        {
            simulation.FrightenForTests(1);
            CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 600);
            Assert.That(fell.HasValue, "The runner should have run past the tower and brought it down.");
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
        public void TheTower_StandsWhileTheBuildingIsCalm_AndCreaksAndTumblesAFewSecondsAfterSomebodyRunsAlongTheCorridorOnceTheFireIsLit()
        {
            ScenarioData data = TwoPeopleAndTheTower(NearTheTower, AgentTraitValues.AllOrdinary, 200);
            data.Traps.CreakTicks = new TrapSettings().CreakTicks;
            using (var simulation = new Run(data, 42UL))
            {
                var standing = new Dictionary<SimulationId, LogicalPosition>();
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    standing[box.ObjectId] = box.Position;
                }

                Advance(simulation, 199);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody standing right beside it all day brings nothing down while the building is calm.");
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    Assert.That(box.Position.X, Is.GreaterThan(13300), $"Box {box.ObjectId} should still be standing in the corner.");
                }

                Assert.That(Door(simulation, TheBuilding.Archway).State, Is.EqualTo(DoorState.Broken), "An archway: open.");

                Advance(simulation, 2);
                CausalEvent fell = BringTheTowerDown(simulation);
                List<CausalEvent> triggered = EventsOfType(simulation, CausalEventType.TrapTriggered);
                Assert.That(triggered, Has.Count.EqualTo(1));
                Assert.That(triggered[0].Tick, Is.GreaterThanOrEqualTo(200), "Armed by the fire.");
                // Sprung by somebody running past it (2026-09-30): whoever ran
                // within reach of it first -- here the one beside it, who takes
                // fright when the runner comes shouting down the corridor.
                Assert.That(triggered[0].HasTarget, Is.True, "Sprung by somebody running past it.");
                Assert.That(IntegerMath.Distance(triggered[0].Position, new LogicalPosition(13900, 6350)),
                    Is.LessThanOrEqualTo(data.Traps.TriggerReachMillimetres + 400), "Close to it.");

                // Sprung, it creaks first (2026-09-29): a few seconds of
                // swaying, jittered, heard in its room, before it comes down.
                List<CausalEvent> creaked = EventsOfType(simulation, CausalEventType.TrapCreaked);
                Assert.That(creaked, Has.Count.EqualTo(1), "The tower creaks before it falls.");
                Assert.That(creaked[0].Tick, Is.EqualTo(triggered[0].Tick), "The creak begins the moment it is sprung.");
                Assert.That(creaked[0].CausalParentEventId, Is.EqualTo(triggered[0].EventId));
                int jitter = data.Traps.CreakTicks * data.World.TimingJitterPercent / 100;
                Assert.That(fell.Tick - triggered[0].Tick,
                    Is.InRange(data.Traps.CreakTicks - jitter, data.Traps.CreakTicks + jitter),
                    "Never on the tick it was sprung: about three seconds of creaking later.");
                Assert.That(fell.Tick - triggered[0].Tick, Is.EqualTo(creaked[0].Strength), "The creak says how long it has.");
                Assert.That(fell.CausalParentEventId, Is.EqualTo(triggered[0].EventId));
                Assert.That(fell.TargetId, Is.EqualTo(TheBuilding.Archway));

                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                for (int i = 0; i < objects.Count; i++)
                {
                    Assert.That(objects.IsPinned(i), Is.False, $"Box {objects.IdOf(i)} is a loose box from the fall on.");
                }

                // Four seconds later the boxes have flown, bounced and come
                // to rest somewhere: nearly every one has left the corner
                // (the bottom of each column, with the others landing on it,
                // may barely shift), and the archway is shut exactly when
                // enough of them lie in it.
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

                Assert.That(tumbled, Is.GreaterThanOrEqualTo(6), "Nearly every box tumbled well away from where it stood.");

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
                Assert.That(story.Describe(fell), Is.EqualTo("the tower of boxes came down toward door 2016"));
            }
        }

        /// <summary>
        /// The owner's rule (2026-09-30): "box tower should fall next to the
        /// first person running past, not in the corridor"; asked how close,
        /// "where they were". The runner alone in the corridor, the tower
        /// creaking its full three seconds: the boxes come down in a heap on
        /// the spot where the runner stood when it began to creak, and the
        /// runner, who kept running, is well clear of it.
        /// </summary>
        [Test]
        public void TheTower_ComesDownWhereTheRunnerStoodWhenItBeganToCreak()
        {
            ScenarioData data = TwoPeopleAndTheTower(new LogicalPosition(-5000, -5000), AgentTraitValues.AllOrdinary, 200);
            data.Traps.CreakTicks = new TrapSettings().CreakTicks;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 201);
                simulation.FrightenForTests(1);
                CausalEvent? triggered = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 600);
                Assert.That(triggered.HasValue, "The runner ran past the tower.");
                Assert.That(triggered.Value.TargetId, Is.EqualTo(TheRunner));
                LogicalPosition stood = simulation.GetAgent(1).Position;
                Assert.That(IntegerMath.Distance(stood, new LogicalPosition(13900, 6350)),
                    Is.LessThanOrEqualTo(data.Traps.TriggerReachMillimetres + 400), "Sprung by running past it, close.");

                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 300);
                Assert.That(fell.HasValue, "It came down after its creak.");
                Assert.That(IntegerMath.Distance(fell.Value.Position, stood), Is.LessThanOrEqualTo(150),
                    "It comes down where the runner stood at the creak.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(1).Position, stood), Is.GreaterThan(1500),
                    "The runner kept running and is clear of it.");

                Advance(simulation, 4 * Run.TicksPerSecond);
                int nearTheSpot = 0;
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    if (IntegerMath.Distance(box.Position, stood) <= 2000)
                    {
                        nearTheSpot++;
                    }
                }

                Assert.That(nearTheSpot, Is.GreaterThanOrEqualTo(5),
                    $"Most of the boxes lie in a heap on the spot: {nearTheSpot} of 8 within two metres.");
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
                    Assert.That(box.Position.X, Is.GreaterThan(13300), $"Box {box.ObjectId} should still be standing in the corner.");
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
            // In the gap itself, just inside the wall line on the corridor's side.
            ScenarioData data = TwoPeopleAndTheTower(new LogicalPosition(12700, 7500), AgentTraitValues.AllOrdinary, 10);
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
        public void TheDefaultBuilding_HasTheTowerInTheJunctionsSouthWestCorner()
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
                Assert.That(box.InitialPosition.X, Is.InRange(13000, 16000).And.GreaterThan(13450), "In the crossbar, clear of the archway's wall line.");
                Assert.That(box.InitialPosition.Z, Is.InRange(6000, 6700), "At the corridor's south wall line: the junction's south-west corner.");
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

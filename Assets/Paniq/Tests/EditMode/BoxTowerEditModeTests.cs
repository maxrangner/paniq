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
    /// fire is lit, the first person to come near brings it down a beat
    /// later, and the boxes land wedged across the archway between the
    /// corridor and the crossbar. The archway is then shut for people and
    /// fire, exactly as a doorway with something wedged in it: the strong
    /// throw a box clear, everybody else gives up and goes round through
    /// the stockroom, and when enough boxes are gone the way is open again.
    /// </summary>
    public sealed class BoxTowerEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);

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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>
        /// The shipped building with only the tower's boxes in it, one
        /// person standing still at the corridor's east end near the tower,
        /// and the fire due in the meeting room at the tick given.
        /// </summary>
        private ScenarioData OnePersonByTheTower(LogicalPosition where, AgentTraitValues traits, int fireTick)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[] { new AgentDefinition(Somebody, where, CardinalDirection.East, traits) };
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

        /// <summary>Just inside the corridor's east end, within reach of the tower and out of the archway.</summary>
        private static readonly LogicalPosition NearTheTower = new LogicalPosition(12500, 6800);

        [Test]
        public void TheTower_StandsWhileTheBuildingIsCalm_AndComesDownABeatAfterSomebodyComesNearOnceTheFireIsLit()
        {
            ScenarioData data = OnePersonByTheTower(NearTheTower, AgentTraitValues.AllOrdinary, 200);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 199);
                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty,
                    "Somebody standing right beside it all day brings nothing down while the building is calm.");
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    Assert.That(box.Position.X, Is.GreaterThan(13300), $"Box {box.ObjectId} should still be standing in the corner.");
                }

                Assert.That(Door(simulation, TheBuilding.Archway).State, Is.EqualTo(DoorState.Broken), "An archway: open.");

                Advance(simulation, 60);
                List<CausalEvent> triggered = EventsOfType(simulation, CausalEventType.TrapTriggered);
                Assert.That(triggered, Has.Count.EqualTo(1));
                Assert.That(triggered[0].Tick, Is.GreaterThanOrEqualTo(200), "Armed by the fire.");
                Assert.That(triggered[0].TargetId, Is.EqualTo(Somebody), "Sprung by the person near it.");
                List<CausalEvent> fell = EventsOfType(simulation, CausalEventType.BoxTowerFell);
                Assert.That(fell, Has.Count.EqualTo(1));
                Assert.That(fell[0].Tick - triggered[0].Tick, Is.InRange(1, data.Perception.ReactionLagMaximumTicks),
                    "Never on the tick it was sprung: a beat later.");
                Assert.That(fell[0].CausalParentEventId, Is.EqualTo(triggered[0].EventId));
                Assert.That(fell[0].TargetId, Is.EqualTo(TheBuilding.Archway));

                DoorSnapshot archway = Door(simulation, TheBuilding.Archway);
                Assert.That(archway.IsPiled, Is.True);
                Assert.That(archway.State, Is.EqualTo(DoorState.Unlocked), "Shut for people and fire.");
                Assert.That(archway.IsJammed, Is.True, "With the boxes wedged in it.");
                foreach (PhysicsObjectSnapshot box in TowerBoxes(simulation))
                {
                    Assert.That(box.Position.X, Is.InRange(12350, 12950), $"Box {box.ObjectId} should lie just inside the wall line, on the corridor's side.");
                    Assert.That(box.Position.Z, Is.InRange(6300, 8700), $"Box {box.ObjectId} should lie across the gap.");
                }

                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(fell[0]), Is.EqualTo("the tower of boxes came down across door 2016"));
            }
        }

        [Test]
        public void SomebodyStandingWhereTheBoxesLand_IsKnockedClearAsTheyComeDown()
        {
            // In the gap itself, just inside the wall line on the corridor's side.
            ScenarioData data = OnePersonByTheTower(new LogicalPosition(12700, 7500), AgentTraitValues.AllOrdinary, 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 3 * Run.TicksPerSecond);
                List<CausalEvent> fell = EventsOfType(simulation, CausalEventType.BoxTowerFell);
                Assert.That(fell, Has.Count.EqualTo(1));
                List<CausalEvent> down = EventsOfType(simulation, CausalEventType.AgentKnockedDown);
                Assert.That(down, Is.Not.Empty, "Nobody can stand where the boxes now lie.");
                Assert.That(down[0].SourceId, Is.EqualTo(Somebody));
                Assert.That(down[0].CausalParentEventId, Is.EqualTo(fell[0].EventId));
                Assert.That(simulation.GetAgent(Somebody).Position.X, Is.LessThan(12350), "Knocked west, out from under them.");
            }
        }

        [Test]
        public void AFrightenedPersonInTheCorridor_CannotGetThroughTheArchway_AndGivesItUp()
        {
            ScenarioData data = OnePersonByTheTower(NearTheTower, new AgentTraitValues(1, 5, 5, 5, 2, 5, 4), 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.BoxTowerFell), Has.Count.EqualTo(1));

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

                List<CausalEvent> gaveUp = EventsOfType(simulation, CausalEventType.AgentGaveUpOnDoor);
                Assert.That(gaveUp.Exists(record => record.TargetId == TheBuilding.Archway), Is.True,
                    "Too weak to throw a box clear, they give the archway up and look elsewhere.");
                Assert.That(EventsOfType(simulation, CausalEventType.BoxPileCleared), Is.Empty);
            }
        }

        [Test]
        public void SomebodyStrong_ThrowsABoxClear()
        {
            ScenarioData data = OnePersonByTheTower(NearTheTower, new AgentTraitValues(9, 5, 9, 5, 2, 3, 4), 10);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);

                List<CausalEvent> thrown = EventsOfType(simulation, CausalEventType.ItemThrown);
                Assert.That(thrown.Exists(record => record.SourceId == Somebody && IsATowerBox(record.TargetId)), Is.True,
                    "Somebody strong enough grabs a box off the heap and throws it clear.");
            }
        }

        /// <summary>
        /// The heap holds while enough boxes lie in the doorway. Here it is
        /// told that every box must stay, so the first one thrown clear
        /// opens the way again.
        /// </summary>
        [Test]
        public void WhenEnoughBoxesAreGone_TheArchwayIsAnArchwayAgain()
        {
            ScenarioData data = OnePersonByTheTower(NearTheTower, new AgentTraitValues(9, 5, 9, 5, 2, 3, 4), 10);
            data.Traps.PileHoldsAtBoxes = 8;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);

                List<CausalEvent> cleared = EventsOfType(simulation, CausalEventType.BoxPileCleared);
                Assert.That(cleared, Has.Count.EqualTo(1), "One box gone was enough here.");
                Assert.That(cleared[0].CausalParentEventId, Is.EqualTo(EventsOfType(simulation, CausalEventType.BoxTowerFell)[0].EventId));
                DoorSnapshot archway = Door(simulation, TheBuilding.Archway);
                Assert.That(archway.IsPiled, Is.False);
                Assert.That(archway.State, Is.EqualTo(DoorState.Broken), "Open for good again.");
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
            ScenarioData data = OnePersonByTheTower(new LogicalPosition(12600, 6800), AgentTraitValues.AllOrdinary, 1);
            data.Fire.SpawnBounds = new LogicalBounds(10250, 10250, 7250, 7250);
            data.Fire.SpreadMinimumTicks = 8;
            data.Fire.SpreadMaximumTicks = 12;
            data.Flammables.BoxIgniteTicks = 25;
            data.Flammables.BoxBurnMinimumTicks = 60;
            data.Flammables.BoxBurnMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
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

                List<CausalEvent> fell = EventsOfType(simulation, CausalEventType.BoxTowerFell);
                Assert.That(fell, Has.Count.EqualTo(1), "The fire in the corridor should have sprung the trap.");
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
            Assert.That(data.TrapDefinitions, Has.Length.EqualTo(1));
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

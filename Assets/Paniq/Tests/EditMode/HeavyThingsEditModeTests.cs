using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Heavy things on the map (2026-09-27): a loose thing too heavy for
    /// anybody to carry, or one pinned where it stands, is on the map people
    /// steer by like a table -- the squares under it are floor nobody can
    /// stand on, and routes go round it -- once it has lain still for half a
    /// second; lifted or moved, the map follows. Light things stay clutter
    /// people dodge at the last moment.
    /// </summary>
    public sealed class HeavyThingsEditModeTests
    {
        private static readonly SimulationId TheThing = new SimulationId(3001UL);

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

        /// <summary>One empty room with one box in it and one person far from it.</summary>
        private ScenarioData OneRoomWithABox(int size, int massGrams, bool pinned)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.ExitSigns = Array.Empty<ExitSignDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Rooms = new[] { new RoomDefinition(PrototypeBuilding.Office, new LogicalBounds(-6000, 6000, -6000, 6000)) };
            data.Doors = new[] { new DoorDefinition(new SimulationId(2001UL), PrototypeBuilding.Office, WallSide.North, 0, 1000, false) };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheThing, PhysicsObjectKind.Box, new LogicalPosition(0, 0), size, massGrams,
                    startsPinned: pinned)
            };
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-5000, -5000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        private static bool IsFloor(Run simulation, LogicalPosition at)
        {
            NavigationGrid grid = simulation.GeometryForTests.Navigation;
            return grid.RoomOfCell(grid.CellAt(at)) != NavigationGrid.Outside;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void AHeavyBox_IsOnTheMapFromTheStart_AndTheMapFollowsItWhenItIsMoved()
        {
            using (var simulation = new Run(OneRoomWithABox(600, 40000, false), 42UL))
            {
                simulation.Step();
                Assert.That(IsFloor(simulation, new LogicalPosition(0, 0)), Is.False, "The square under a 40 kg crate is nobody's floor.");
                Assert.That(IsFloor(simulation, new LogicalPosition(2000, 0)), Is.True, "Two metres off it is floor.");
                Assert.That(simulation.ObjectsForTests.IsOnTheMap(0), Is.True);

                int box = simulation.ObjectsForTests.IndexOf(TheThing);
                simulation.PlaceObjectForTests(box, new LogicalPosition(3000, 0), 0, 0);
                Advance(simulation, 40);
                Assert.That(IsFloor(simulation, new LogicalPosition(0, 0)), Is.True, "Where it lay is floor again.");
                Assert.That(IsFloor(simulation, new LogicalPosition(3000, 0)), Is.False, "And where it lies now is not, once it has settled.");
            }
        }

        [Test]
        public void ALightBox_IsNeverOnTheMap()
        {
            using (var simulation = new Run(OneRoomWithABox(400, 6000, false), 42UL))
            {
                Advance(simulation, 40);
                Assert.That(IsFloor(simulation, new LogicalPosition(0, 0)), Is.True, "A 6 kg box is clutter, not a wall.");
                Assert.That(simulation.ObjectsForTests.IsOnTheMap(0), Is.False);
            }
        }

        /// <summary>
        /// One room whose only door is walled off by a row of three heavy
        /// crates lying still across the approach, with a hand's width of
        /// floor at either end of the row (too little for a body), and one
        /// frightened person on the wrong side of them. The fire is far away
        /// and never spreads.
        /// </summary>
        private ScenarioData CratesAcrossTheOnlyDoor(AgentTraitValues traits)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.ExitSigns = Array.Empty<ExitSignDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Rooms = new[] { new RoomDefinition(PrototypeBuilding.Office, new LogicalBounds(-1400, 1400, -6000, 6000)) };
            data.Doors = new[] { new DoorDefinition(new SimulationId(2001UL), PrototypeBuilding.Office, WallSide.North, 0, 1000, false) };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Box, new LogicalPosition(-700, 3500), 700, 55000),
                new PhysicsObjectDefinition(new SimulationId(3002UL), PhysicsObjectKind.Box, new LogicalPosition(0, 3500), 700, 55000),
                new PhysicsObjectDefinition(new SimulationId(3003UL), PhysicsObjectKind.Box, new LogicalPosition(700, 3500), 700, 55000)
            };
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 1500), CardinalDirection.North, traits)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        private static List<CausalEvent> Heaves(Run simulation)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == CausalEventType.AgentShovedObstruction)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        [Test]
        public void SomebodyStrong_HeavesACrateInTheirWayAside_AndGetsToTheDoor()
        {
            // Strong (9) and not brave, so it is self-preservation, not heroics.
            var strong = new AgentTraitValues(9, 5, 5, 5, 5, 5, 5);
            using (var simulation = new Run(CratesAcrossTheOnlyDoor(strong), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(2001UL), 5);
                Advance(simulation, 100);
                Assert.That(simulation.ObjectsForTests.IsPinned(1), Is.True, "The crates have settled and are held where they lie.");
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);

                Assert.That(Heaves(simulation), Is.Not.Empty, "A crate lying still across their way is heaved aside.");
                Assert.That(simulation.AgentForTests(0).Body.Position.Z, Is.GreaterThan(3500),
                    "And, the row shoved up to one wall and then the other, they are past the crates.");
            }
        }

        [Test]
        public void SomebodyOrdinary_CannotShiftTheCrates_AndGivesThatDoorUp_RatherThanPushAtItForEver()
        {
            using (var simulation = new Run(CratesAcrossTheOnlyDoor(AgentTraitValues.AllOrdinary), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(2001UL), 5);
                Advance(simulation, 100);
                Assert.That(simulation.ObjectsForTests.IsPinned(1), Is.True, "The crates have settled and are held where they lie.");
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);

                Assert.That(Heaves(simulation), Is.Empty, "Not strong enough to shift a crate.");
                Agent person = simulation.AgentForTests(0);
                Assert.That(person.Body.Position.Z, Is.LessThan(3150), "So they are still on their side of them.");
                Assert.That(person.Doors.ExitDoorIndex, Is.EqualTo(-1), "The door beyond the crates is no way out to them.");
            }
        }

        [Test]
        public void APinnedLightBox_IsOnTheMap_WhateverItWeighs()
        {
            using (var simulation = new Run(OneRoomWithABox(400, 6000, true), 42UL))
            {
                simulation.Step();
                Assert.That(IsFloor(simulation, new LogicalPosition(0, 0)), Is.False, "Pinned where it stands, it is a wall.");
                Assert.That(simulation.ObjectsForTests.IsPinned(0), Is.True);
            }
        }

        /// <summary>A crate in the middle of the empty office, and a light box a couple of metres south of it.</summary>
        private ScenarioData ACrateAndALightBox()
        {
            ScenarioData data = OneRoomWithABox(700, 55000, false);
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheThing, PhysicsObjectKind.Box, new LogicalPosition(0, 0), 700, 55000),
                new PhysicsObjectDefinition(new SimulationId(3002UL), PhysicsObjectKind.Box, new LogicalPosition(0, -2500), 400, 6000)
            };
            return data;
        }

        [Test]
        public void ALightBoxKickedIntoAHeldCrate_LeavesItWhereItLies()
        {
            // The owner saw a fallen tower box glide off across the floor by
            // itself a few seconds after landing (2026-09-28): a box still
            // sliding from the fall touched one already held where it lay,
            // and the rule that lets a heaved crate shove the next along
            // un-held it and handed it the slider's speed. Only a heave may.
            using (var simulation = new Run(ACrateAndALightBox(), 42UL))
            {
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                Advance(simulation, 100);
                Assert.That(objects.IsPinned(0), Is.True, "The crate has lain still and is held where it lies.");
                LogicalPosition before = objects.PositionOf(0);

                // As a runner's kick sends the light box: fast, with a cause.
                simulation.LaunchObjectForTests(1, 0, 150);
                Advance(simulation, 150);
                Assert.That(IntegerMath.Distance(objects.PositionOf(1), before), Is.LessThan(1500), "The light box reached the crate.");
                Assert.That(objects.IsPinned(0), Is.True, "Still held: only a heave shoves a held crate on.");
                Assert.That(IntegerMath.Distance(objects.PositionOf(0), before), Is.LessThan(20),
                    "And it has not moved; it used to take off at the box's speed.");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The stockroom's stack (2026-09-27): the Director's second trap. Three
    /// crates against the north wall at the winding lane's first bend come
    /// down across the gap once somebody frightened runs through the
    /// stockroom, and, too heavy to carry, lie where they land and cut the
    /// lane on the map people steer by: the office's way out through the
    /// stockroom stops being one.
    /// </summary>
    public sealed class StockroomTrapEditModeTests
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

        private static bool IsAStackCrate(SimulationId id) => id.Value >= 3581UL && id.Value <= 3584UL;

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

        private static int RoomIndex(Run simulation, SimulationId room)
        {
            WorldGeometry geometry = simulation.GeometryForTests;
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (geometry.RoomId(r) == room)
                {
                    return r;
                }
            }

            throw new KeyNotFoundException(room.ToString());
        }

        /// <summary>The shipped building with one person standing in the stockroom's middle lane, the fire due in the meeting room.</summary>
        private ScenarioData OnePersonInTheStockroom(LogicalPosition where)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[] { new AgentDefinition(Somebody, where, CardinalDirection.North, AgentTraitValues.AllOrdinary) };
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Fire.ActivationTick = 10;
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

        [Test]
        public void TheStack_ComesDown_ABeatAfterSomebodyFrightenedRunsThroughTheStockroom()
        {
            ScenarioData data = OnePersonInTheStockroom(TheBuilding.Stockroom);
            using (var simulation = new Run(data, 42UL))
            {
                for (int t = 0; t < 60; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOfType(simulation, CausalEventType.TrapTriggered), Is.Empty, "Somebody calm in the stockroom brings nothing down.");
                simulation.FrightenForTests(0);
                CausalEvent? trap = AdvanceUntil(simulation, CausalEventType.TrapTriggered, 100);
                Assert.That(trap.HasValue, "Frightened, they run, and the stack is sprung.");
                Assert.That(trap.Value.SourceId, Is.EqualTo(TheBuilding.TheStockroomTrap), "The stockroom's stack, not the tower.");
                Assert.That(trap.Value.TargetId, Is.EqualTo(Somebody));
                CausalEvent? fell = AdvanceUntil(simulation, CausalEventType.BoxTowerFell, 300);
                Assert.That(fell.HasValue);
                Assert.That(fell.Value.Tick - trap.Value.Tick, Is.InRange(data.Traps.CreakTicks * 4 / 5, data.Traps.CreakTicks * 6 / 5), "It creaks for about three seconds first (2026-09-29).");
                Assert.That(fell.Value.HasTarget, Is.False, "No doorway: it fell across a lane.");

                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(fell.Value), Is.EqualTo("the crates came down across the lane"));

                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                int moved = 0;
                for (ulong id = 3581UL; id <= 3584UL; id++)
                {
                    int crate = objects.IndexOf(new SimulationId(id));
                    if (IntegerMath.Distance(new LogicalPosition(9500, -950), objects.PositionOf(crate)) > 300)
                    {
                        moved++;
                    }
                }

                Assert.That(moved, Is.GreaterThanOrEqualTo(3), "The crates tumbled south across the gap.");
            }
        }

        /// <summary>
        /// Laid across the gap at the bend and left to settle, the crates cut
        /// the lane on the map: the walk from the west lane to the east lane
        /// is no longer the lane itself but the long way round by the office,
        /// the corridor and the crossbar, so the office's own route to the
        /// crossbar goes by the corridor rather than through the stockroom.
        /// </summary>
        [Test]
        public void AFallenStack_CutsTheLaneOnTheMap_SoTheOfficeGoesRoundByTheCorridor()
        {
            ScenarioData data = OnePersonInTheStockroom(TheBuilding.Stockroom);
            using (var simulation = new Run(data, 42UL))
            {
                var westLane = new LogicalPosition(7000, -4000);
                var eastLane = new LogicalPosition(14500, -1000);
                WorldGeometry geometry = simulation.GeometryForTests;
                int office = RoomIndex(simulation, PrototypeBuilding.Office);
                int crossbar = RoomIndex(simulation, PrototypeBuilding.Crossbar);

                // The fields on the map are built a few a tick, so each
                // question is asked over a few ticks and the last answer is
                // the one.
                long before = long.MaxValue;
                for (int t = 0; t < 10; t++)
                {
                    simulation.Step();
                    before = geometry.Routes.WalkingDistance(westLane, eastLane, 250);
                }

                Assert.That(before, Is.LessThan(20000), $"Along the winding lane the east lane is a {before} mm walk from the west lane.");
                Agent walker = simulation.AgentForTests(0);
                Assert.That(geometry.TryFindRoute(office, TheBuilding.OfficeSouthEastCorner, crossbar, walker, out int first, out _, out _), Is.True);
                Assert.That(simulation.GetDoor(first).DoorId, Is.EqualTo(TheBuilding.StockroomDoor), "From the office's south-east corner the short way to the crossbar is through the stockroom.");

                // Four crates do not fit flat in a row across the 2.6 m gap
                // (they tumble and scatter when they really fall), so three
                // lie in a row from the north wall, a hand's width apart, and
                // the fourth lies just east of the row's end, closing the
                // last half-metre to wall A.
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                simulation.PlaceObjectForTests(objects.IndexOf(new SimulationId(3581UL)), new LogicalPosition(9500, -860), 0, 0);
                simulation.PlaceObjectForTests(objects.IndexOf(new SimulationId(3582UL)), new LogicalPosition(9500, -1580), 0, 0);
                simulation.PlaceObjectForTests(objects.IndexOf(new SimulationId(3583UL)), new LogicalPosition(9500, -2300), 0, 0);
                simulation.PlaceObjectForTests(objects.IndexOf(new SimulationId(3584UL)), new LogicalPosition(10210, -2900), 0, 0);
                long after = long.MaxValue;
                for (int t = 0; t < 80; t++)
                {
                    simulation.Step();
                    after = geometry.Routes.WalkingDistance(westLane, eastLane, 250);
                }

                for (ulong id = 3581UL; id <= 3584UL; id++)
                {
                    Assert.That(objects.IsPinned(objects.IndexOf(new SimulationId(id))), Is.True, $"Crate {id} lies still and is held there.");
                }

                Assert.That(after, Is.GreaterThan(before + 10000),
                    $"With the crates across the bend the walk is the long way round, {after} mm against {before} mm along the lane.");
                Assert.That(geometry.TryFindRoute(office, TheBuilding.OfficeSouthEastCorner, crossbar, walker, out first, out _, out _), Is.True);
                Assert.That(simulation.GetDoor(first).DoorId, Is.EqualTo(TheBuilding.OfficeDoor), "So the office goes round by the corridor.");
            }
        }

        [Test]
        public void ALaneTrap_LandingOutsideTheRoomItWatches_IsRefused()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.TrapDefinitions = new[]
            {
                data.TrapDefinitions[0],
                new TrapDefinition(TheBuilding.TheStockroomTrap, data.TrapDefinitions[1].BoxIds, PrototypeBuilding.Stockroom,
                    TheBuilding.Corridor, 90, 2100)
            };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }
    }
}

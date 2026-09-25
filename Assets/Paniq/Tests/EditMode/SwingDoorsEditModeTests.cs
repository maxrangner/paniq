using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The cafeteria's corridor door is a pair of swing doors (the owner asked,
    /// 2026-09-25): people push straight through without stopping to work a
    /// handle, nobody can shut, lock or batter it, clicking it does nothing and
    /// costs nothing, and the fire burns through it in half a shut door's time
    /// -- unless something lying in the gap props it open, and then the fire
    /// walks through.
    /// </summary>
    public sealed class SwingDoorsEditModeTests
    {
        private static readonly SimulationId SwingDoors = TheBuilding.CafeteriaDoor;

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

        /// <summary>The building with only these people, no clutter, and a fire that starts at once and never spreads.</summary>
        private ScenarioData Quiet(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Timetable = new ScheduledCue[0];
            data.Day.ToiletEveryTicks = 0;
            data.Fire.ActivationTick = 3;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Panic.DangerDistanceMillimetres = 2500;
            return data;
        }

        private static AgentDefinition Someone(ulong id, LogicalPosition at, CardinalDirection facing, int evil = 2)
        {
            return new AgentDefinition(new SimulationId(id), at, facing, new AgentTraitValues(5, 5, 5, 5, evil, 5, 5));
        }

        /// <summary>Somebody in the cafeteria by its corridor door, with the fire in the east of the room so the corridor is the way to go.</summary>
        private ScenarioData SomebodyLeavingByTheCorridorDoor(int evil = 2)
        {
            ScenarioData data = Quiet(Someone(1UL, new LogicalPosition(6000, 10500), CardinalDirection.East, evil));
            TheBuilding.FireAt(data, new LogicalPosition(12250, 12250));
            return data;
        }

        /// <summary>The same building with the swing doors swapped for an ordinary 1 m door in the same wall.</summary>
        private static void SwapForAnOrdinaryDoor(ScenarioData data)
        {
            for (int i = 0; i < data.Doors.Length; i++)
            {
                if (data.Doors[i].DoorId == SwingDoors)
                {
                    data.Doors[i] = new DoorDefinition(SwingDoors, data.Doors[i].RoomId, data.Doors[i].Side,
                        data.Doors[i].CentreAlongWallMillimetres, 1000, false);
                }
            }
        }

        private static DoorSnapshot DoorOf(Run simulation, SimulationId door)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == door)
                {
                    return simulation.GetDoor(i);
                }
            }

            throw new KeyNotFoundException(door.ToString());
        }

        private static List<CausalEvent> EventsAbout(Run simulation, CausalEventType type, SimulationId door)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type && (record.TargetId == door || record.SourceId == door))
                {
                    found.Add(record);
                }
            }

            return found;
        }

        /// <summary>The tick the first person reaches the corridor, or -1 if they never do inside the limit.</summary>
        private static int TickTheyReachTheCorridor(Run simulation, ScenarioData data, int limit)
        {
            int corridor = System.Array.FindIndex(data.Rooms, r => r.RoomId == PrototypeBuilding.Corridor);
            for (int t = 0; t < limit; t++)
            {
                simulation.Step();
                if (simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position) == corridor)
                {
                    return t;
                }
            }

            return -1;
        }

        private static int CorridorFireCells(Run simulation, ScenarioData data)
        {
            int corridor = System.Array.FindIndex(data.Rooms, r => r.RoomId == PrototypeBuilding.Corridor);
            int count = 0;
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (simulation.GeometryForTests.RoomAt(cell.Centre) == corridor)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void TheSwingDoors_StandOpenFromTheStart_AndNobodyWorksThem()
        {
            ScenarioData data = SomebodyLeavingByTheCorridorDoor(evil: 9);
            using (var simulation = new Run(data, 7UL))
            {
                Assert.That(DoorOf(simulation, SwingDoors).Swings, Is.True);
                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Open));

                int reached = TickTheyReachTheCorridor(simulation, data, 15 * Run.TicksPerSecond);
                Assert.That(reached, Is.GreaterThan(0), "They should be out in the corridor.\n" + simulation.DescribeForTests(0));
                Assert.That(EventsAbout(simulation, CausalEventType.AgentTriedDoor, SwingDoors), Is.Empty, "Nobody tries a handle.");
                Assert.That(EventsAbout(simulation, CausalEventType.DoorOpened, SwingDoors), Is.Empty, "Nobody opens it: it was never shut.");

                // The cruel slam and lock doors behind them; these they cannot.
                for (int t = 0; t < 5 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsAbout(simulation, CausalEventType.DoorClosed, SwingDoors), Is.Empty);
                Assert.That(EventsAbout(simulation, CausalEventType.DoorLocked, SwingDoors), Is.Empty);
                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Open));
            }
        }

        [Test]
        public void PushingThrough_IsQuickerThanWorkingAnOrdinaryDoor()
        {
            ScenarioData swing = SomebodyLeavingByTheCorridorDoor();
            ScenarioData ordinary = SomebodyLeavingByTheCorridorDoor();
            SwapForAnOrdinaryDoor(ordinary);
            int throughTheSwingDoors;
            int throughTheOrdinaryDoor;
            using (var simulation = new Run(swing, 7UL))
            {
                throughTheSwingDoors = TickTheyReachTheCorridor(simulation, swing, 15 * Run.TicksPerSecond);
            }

            using (var simulation = new Run(ordinary, 7UL))
            {
                throughTheOrdinaryDoor = TickTheyReachTheCorridor(simulation, ordinary, 15 * Run.TicksPerSecond);
                Assert.That(EventsAbout(simulation, CausalEventType.DoorOpened, SwingDoors), Is.Not.Empty,
                    "The ordinary door had to be opened.");
            }

            Assert.That(throughTheSwingDoors, Is.GreaterThan(0));
            Assert.That(throughTheOrdinaryDoor, Is.GreaterThan(0));
            Assert.That(throughTheSwingDoors, Is.LessThan(throughTheOrdinaryDoor),
                $"Swing doors {throughTheSwingDoors} ticks, an ordinary door {throughTheOrdinaryDoor}.");
        }

        [Test]
        public void AClickOnSwingDoors_DoesNothing_AndCostsNothing()
        {
            ScenarioData data = SomebodyLeavingByTheCorridorDoor();
            using (var simulation = new Run(data, 7UL))
            {
                int purse = simulation.Influence;
                simulation.QueueCommand(PlayerCommandType.ClickDoor, SwingDoors, 1);
                for (int t = 0; t < 3; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Open));
                Assert.That(simulation.Influence, Is.EqualTo(purse), "Nothing happened, so nothing is charged.");
                Assert.That(EventsAbout(simulation, CausalEventType.DoorClosed, SwingDoors), Is.Empty);
            }
        }

        /// <summary>
        /// Flames against the swing doors in the cafeteria, and nobody near:
        /// the corridor stays clear until the leaves burn through, in half a
        /// shut door's time, and then the fire comes on.
        /// </summary>
        [Test]
        public void TheFire_BurnsThroughInHalfAShutDoorsTime_AndNotBefore()
        {
            ScenarioData data = Quiet(Someone(1UL, TheBuilding.MeetingRoom, CardinalDirection.East));
            data.Fire.SpreadMinimumTicks = 5;
            data.Fire.SpreadMaximumTicks = 5;
            TheBuilding.FireAt(data, new LogicalPosition(5750, 9250));
            int through = data.Exits.SwingDoorBurnThroughTicks;
            Assert.That(through * 2, Is.EqualTo(data.Exits.DoorBurnThroughTicks), "Half as good as a shut door.");
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < through - 20; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Open), "Still a door.");
                Assert.That(CorridorFireCells(simulation, data), Is.Zero, "The fire has not got into the corridor yet.");

                for (int t = 0; t < 60 + 100; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Broken), "Burnt through.");
                Assert.That(EventsAbout(simulation, CausalEventType.DoorBurntThrough, SwingDoors), Is.Not.Empty);
                Assert.That(CorridorFireCells(simulation, data), Is.GreaterThan(0), "And now the fire is through.");
            }
        }

        /// <summary>A box lying in the gap props the leaves open, and the fire walks through long before it could have burnt through.</summary>
        [Test]
        public void SomethingWedgedInTheGap_PropsThemOpen_AndTheFireWalksThrough()
        {
            ScenarioData data = Quiet(Someone(1UL, TheBuilding.MeetingRoom, CardinalDirection.East));
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(9001UL), PhysicsObjectKind.Box, new LogicalPosition(6000, 8700), 400, 6000)
            };
            data.Fire.SpreadMinimumTicks = 5;
            data.Fire.SpreadMaximumTicks = 5;
            TheBuilding.FireAt(data, new LogicalPosition(5750, 9250));
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 200; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, SwingDoors).IsBlocked, Is.True, "The box is in the gap.");
                Assert.That(DoorOf(simulation, SwingDoors).State, Is.EqualTo(DoorState.Open), "Propped, not burnt.");
                Assert.That(CorridorFireCells(simulation, data), Is.GreaterThan(0), "The fire came through the propped doors.");
            }
        }
    }
}

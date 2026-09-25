using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The only way out is through the heat. The brave, and anyone whose own
    /// room is alight, run for it while the floor is still walkable; the
    /// timid, and anyone whose route crosses burning floor, give that door up
    /// and hide in the nearest dead end. People used to shuttle at the edge
    /// of the heat, running back from it and picking the same door again,
    /// until it reached them (the bathroom pair on seed 42). The owner's
    /// choice, 2026-09-24: "dash past or hide, by bravery".
    /// </summary>
    public sealed class CorneredEditModeTests
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

        /// <summary>The building with only these people in it, a fire that starts at once and never spreads, and a wide danger distance so the band of walkable-but-hot floor is real.</summary>
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

        private static AgentDefinition Person(ulong id, int x, int z, CardinalDirection facing, int bravery)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing,
                new AgentTraitValues(5, 5, bravery, 5, 2, 5, 5));
        }

        private static List<CausalEvent> EventsOf(Run simulation, CausalEventType type, ulong who)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type && record.SourceId.Value == who)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        private static int RoomIndex(ScenarioData data, SimulationId room)
        {
            return System.Array.FindIndex(data.Rooms, r => r.RoomId == room);
        }

        private static bool InAStall(ScenarioData data, int room)
        {
            return room >= 0 && data.Rooms[room].Use == RoomUse.Stall;
        }

        /// <summary>
        /// Alone in the bathroom's east corner with the flames between them
        /// and its only door: the straight line to the door runs right past
        /// the fire, so however desperate they are they will not cross it.
        /// They give the door up, hide in a stall and shut its door. (With
        /// somebody else there to open the bathroom door, the news of it
        /// opening would send them through the heat after all.)
        /// </summary>
        [Test]
        public void WithTheFlamesBetweenThemAndTheOnlyDoor_TheyHideInAStall_AndShutIt()
        {
            ScenarioData data = Quiet(Person(1UL, 12700, 5000, CardinalDirection.North, bravery: 2));
            TheBuilding.FireAt(data, new LogicalPosition(12250, 5750));
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 15 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOf(simulation, CausalEventType.AgentHidFromTheHeat, 1UL), Is.Not.Empty, "They gave the hot door up.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat, 1UL), Is.Empty,
                    "Nobody runs across burning floor, however desperate.");
                int room = simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position);
                Assert.That(InAStall(data, room), Is.True, $"They should be hiding in a stall, not in room {room}.");
                Assert.That(EventsOf(simulation, CausalEventType.DoorClosed, 1UL), Is.Not.Empty, "And they shut the stall door behind them.");
            }
        }

        /// <summary>
        /// Somebody brave in the closet, with the same fire in the office
        /// beyond the open door as the timid person below faces: they run for
        /// it through the heat and are out of the closet and away.
        /// </summary>
        [Test]
        public void WithTheFireInTheNextRoom_TheBraveRunForItThroughTheHeat()
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 2500, CardinalDirection.West, bravery: 8));
            TheBuilding.FireAt(data, new LogicalPosition(4750, 3250));
            int closet = RoomIndex(data, PrototypeBuilding.Closet);
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                for (int t = 0; t < 12 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat, 1UL), Is.Not.Empty, "They ran for it.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentHidFromTheHeat, 1UL), Is.Empty);
                Assert.That(simulation.GetAgent(0).Outcome, Is.Not.EqualTo(AgentTerminalOutcome.Lost));
                Assert.That(simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position), Is.Not.EqualTo(closet),
                    "They should be out of the closet and away.");
            }
        }

        /// <summary>Somebody timid whose own room is alight runs for the door all the same, while the floor to it is walkable: staying is worse.</summary>
        [Test]
        public void WithTheirOwnRoomAlight_EvenTheTimidRunForIt_WhileTheFloorIsWalkable()
        {
            ScenarioData data = Quiet(Person(1UL, 9000, 4500, CardinalDirection.East, bravery: 2));
            TheBuilding.FireAt(data, new LogicalPosition(12250, 5750));
            int bathroom = RoomIndex(data, PrototypeBuilding.Bathroom);
            using (var simulation = new Run(data, 7UL))
            {
                for (int t = 0; t < 12 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat, 1UL), Is.Not.Empty);
                int room = simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position);
                Assert.That(room == bathroom || InAStall(data, room), Is.False, $"They should be out of the bathroom, not in room {room}.");
            }
        }

        /// <summary>
        /// Somebody timid in the closet, a metre inside its open door and
        /// facing it, with the fire in the office beyond: in sight through
        /// the doorway and near the door's approach, but off the path to it.
        /// Their own room is not alight, so there is no desperation in it:
        /// they give the door up, stay in the closet, which is the dead end
        /// they are already in, and pull the door shut on the fire.
        /// </summary>
        [Test]
        public void WithTheFireInTheNextRoom_TheTimidGiveTheDoorUp_AndShutThemselvesIn()
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 2500, CardinalDirection.West, bravery: 2));
            TheBuilding.FireAt(data, new LogicalPosition(4750, 3250));
            int closet = RoomIndex(data, PrototypeBuilding.Closet);
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                for (int t = 0; t < 12 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm), "They saw the fire through the open door.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentHidFromTheHeat, 1UL), Is.Not.Empty, "They gave the hot door up.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat, 1UL), Is.Empty);
                Assert.That(simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position), Is.EqualTo(closet),
                    "They stayed in the closet.");
                Assert.That(EventsOf(simulation, CausalEventType.DoorClosed, 1UL), Is.Not.Empty, "And shut the door on the fire.");
            }
        }

        /// <summary>However brave, nobody runs across burning floor: with the flames in the doorway itself, even bravery 9 hides.</summary>
        [Test]
        public void WithTheFlamesInTheDoorwayItself_EvenTheBraveHide()
        {
            ScenarioData data = Quiet(Person(1UL, 7000, 4500, CardinalDirection.South, bravery: 9));
            TheBuilding.FireAt(data, new LogicalPosition(6250, 2250));
            int closet = RoomIndex(data, PrototypeBuilding.Closet);
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.ClosetDoor, 1);
                for (int t = 0; t < 12 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOf(simulation, CausalEventType.AgentDashedThroughHeat, 1UL), Is.Empty,
                    "Nobody runs across burning floor.");
                Assert.That(EventsOf(simulation, CausalEventType.AgentHidFromTheHeat, 1UL), Is.Not.Empty);
                Assert.That(simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position), Is.EqualTo(closet));
            }
        }
    }
}

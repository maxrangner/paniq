using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Three places used to answer "room zero" when they could not tell which
    /// room something was in: which room a person counts as being in, where a
    /// step gets pulled back to, and where a sliding object gets kept. Room
    /// zero is the office the fire starts in, so in a bigger building a box
    /// knocked into a doorway at the far end could be shoved back inside the
    /// office's walls, right across the floor plan.
    ///
    /// The prototype never showed it, because almost everything happens in room
    /// zero -- which is exactly why it survived. This builds a floor plan where
    /// it would show, and pins the answer.
    /// </summary>
    public sealed class FireReactionFarRoomsEditModeTests
    {
        private static readonly SimulationId NearRoom = new SimulationId(7001UL);
        private static readonly SimulationId FarRoom = new SimulationId(7002UL);
        private static readonly SimulationId BetweenThem = new SimulationId(7101UL);
        private static readonly SimulationId Somebody = new SimulationId(7201UL);

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

        /// <summary>
        /// Two rooms side by side with a door between them. Room zero is on the
        /// left; the far room's own doorway is twenty metres from it.
        /// </summary>
        private WorldGeometry TwoRoomsFarApart()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.BlastHoles = new SimulationId[0];
            data.Rooms = new[]
            {
                new FireReactionRoomDefinition(NearRoom, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new FireReactionRoomDefinition(FarRoom, new LogicalBounds(6000, 26000, -6000, 6000))
            };
            data.Doors = new[]
            {
                new FireReactionDoorDefinition(BetweenThem, NearRoom, WallSide.East, 0, 1000, false)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Somebody, new LogicalPosition(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };

            return new FireReactionSimulation(data).GeometryForTests;
        }

        [Test]
        public void SomethingInTheFarRoom_IsNotSaidToBeInTheFirstRoom()
        {
            WorldGeometry geometry = TwoRoomsFarApart();
            Assert.That(geometry.RoomStoodIn(new LogicalPosition(20000, 0)), Is.EqualTo(1),
                "A point well inside the second room was attributed to the first.");
        }

        [Test]
        public void SomethingStandingExactlyOnAWallLine_BelongsToAWallItIsTouching()
        {
            // Rooms meet at x = 6000, and a point exactly on that line is
            // strictly inside neither. It used to be called room zero wherever
            // it was; now it belongs to one of the two rooms that meet there.
            WorldGeometry geometry = TwoRoomsFarApart();
            int room = geometry.RoomStoodIn(new LogicalPosition(6000, 0));
            Assert.That(room, Is.EqualTo(0).Or.EqualTo(1));
        }

        [Test]
        public void SomethingOutsideTheBuildingBeyondTheFarRoom_IsKeptByTheFarRoom()
        {
            // The case that used to teleport things across the building: a spot
            // outside every room, at the far end. The honest answer is the room
            // it is nearest to, which is the far one -- never room zero, twenty
            // metres away in the other direction.
            WorldGeometry geometry = TwoRoomsFarApart();
            Assert.That(geometry.RoomStoodIn(new LogicalPosition(30000, 0)), Is.EqualTo(1),
                "A spot just outside the far room was attributed to the first room instead.");
        }

        [Test]
        public void AStepPulledBackFromOutsideTheFarRoom_StaysAtTheFarEnd()
        {
            // The same fault seen through the rule that actually used it: a
            // position being clamped back into walkable space must land in the
            // room it is beside, not in room zero.
            WorldGeometry geometry = TwoRoomsFarApart();
            var justOutside = new LogicalPosition(26000, 0);
            LogicalPosition kept = geometry.ClampIntoWalkable(justOutside, -1, new LogicalPosition(30000, 0));
            Assert.That(kept.X, Is.GreaterThan(6000),
                $"The step was pulled back to x={kept.X}, which is in the first room at the other end of the building.");
        }
    }
}

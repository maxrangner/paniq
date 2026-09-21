using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// A building nobody laid out by hand: thirty rooms in a grid, joined by
    /// doorways, with one way out at the far corner from where the fire starts.
    ///
    /// Everything else is tested against the four-room prototype, which has
    /// been tuned and re-tuned until it works. That is exactly why it cannot
    /// show whether the rules are general: a floor plan somebody has been
    /// fixing bugs against for weeks will hide any rule that only works on it.
    /// This one has never been looked at.
    /// </summary>
    public sealed class FireReactionBigBuildingEditModeTests
    {
        private const int Columns = 6;
        private const int Rows = 5;
        private const int RoomSizeMillimetres = 6000;

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

        private static int RoomAt(int column, int row) => row * Columns + column;

        private static LogicalPosition MiddleOf(int column, int row)
        {
            return new LogicalPosition(
                column * RoomSizeMillimetres + RoomSizeMillimetres / 2,
                row * RoomSizeMillimetres + RoomSizeMillimetres / 2);
        }

        /// <summary>
        /// Thirty rooms in a grid. Every room is joined to the one east of it
        /// and the one north of it, and the far corner has a way out.
        /// </summary>
        private FireReactionScenarioData BigBuilding()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.BlastHoles = new SimulationId[0];

            var rooms = new List<FireReactionRoomDefinition>();
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    rooms.Add(new FireReactionRoomDefinition(
                        new SimulationId((ulong)(50000 + RoomAt(column, row))),
                        new LogicalBounds(
                            column * RoomSizeMillimetres,
                            (column + 1) * RoomSizeMillimetres,
                            row * RoomSizeMillimetres,
                            (row + 1) * RoomSizeMillimetres)));
                }
            }

            var doors = new List<FireReactionDoorDefinition>();
            ulong doorId = 20000UL;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    SimulationId room = rooms[RoomAt(column, row)].RoomId;
                    if (column + 1 < Columns)
                    {
                        doors.Add(new FireReactionDoorDefinition(
                            new SimulationId(doorId++), room, WallSide.East,
                            row * RoomSizeMillimetres + RoomSizeMillimetres / 2, 1000, false));
                    }

                    if (row + 1 < Rows)
                    {
                        doors.Add(new FireReactionDoorDefinition(
                            new SimulationId(doorId++), room, WallSide.North,
                            column * RoomSizeMillimetres + RoomSizeMillimetres / 2, 1000, false));
                    }
                }
            }

            // The one way out, in the far corner from the fire.
            doors.Add(new FireReactionDoorDefinition(
                new SimulationId(doorId), rooms[RoomAt(Columns - 1, Rows - 1)].RoomId, WallSide.East,
                (Rows - 1) * RoomSizeMillimetres + RoomSizeMillimetres / 2, 1000, false));

            data.Rooms = rooms.ToArray();
            data.Doors = doors.ToArray();
            data.Fire.SpawnBounds = new LogicalBounds(1000, 1000, 1000, 1000);
            data.Fire.ActivationTick = 50;
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(
                    new SimulationId(10001UL), MiddleOf(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };

            return data;
        }

        [Test]
        public void ABuildingOfThirtyRooms_LoadsAtAll()
        {
            FireReactionScenarioData data = BigBuilding();
            Assert.That(data.Rooms, Has.Length.EqualTo(Columns * Rows));
            Assert.DoesNotThrow(() => new FireReactionSimulation(data),
                "A plain grid of rooms was refused, which means the rules still assume the prototype's shape.");
        }

        [Test]
        public void EveryRoom_CanBeWalkedToFromEveryOther()
        {
            // The real check on the navigation. With thirty rooms and fifty
            // doorways there is no chance of a hand-tuned answer: either
            // routing works on any floor plan or it does not.
            FireReactionScenarioData data = BigBuilding();
            var simulation = new FireReactionSimulation(data);
            WorldGeometry geometry = simulation.GeometryForTests;
            int radius = data.World.OccupancyRadiusMillimetres;

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Assert.That(
                        geometry.Routes.CanGetFromHereToThere(MiddleOf(0, 0), MiddleOf(column, row), radius),
                        Is.True,
                        $"There is no way from the first room to the room at column {column}, row {row}.");
                }
            }
        }

        [Test]
        public void WalkingAcrossTheBuilding_IsFurtherThanTheStraightLine()
        {
            // Rooms are only joined at their doorways, so crossing the building
            // means threading through them. A route as short as the straight
            // line would mean it was going through the walls.
            FireReactionScenarioData data = BigBuilding();
            var simulation = new FireReactionSimulation(data);
            int radius = data.World.OccupancyRadiusMillimetres;

            LogicalPosition from = MiddleOf(0, 0);
            LogicalPosition to = MiddleOf(Columns - 1, Rows - 1);
            long walking = simulation.GeometryForTests.Routes.WalkingDistance(from, to, radius);

            Assert.That(walking, Is.Not.EqualTo(long.MaxValue), "The far corner could not be reached at all.");
            Assert.That(walking, Is.GreaterThan(IntegerMath.Distance(from, to)));
        }

        [Test]
        public void SomebodyInTheFarCorner_MakesProgressTowardsTheWayOut()
        {
            // Not "escapes": thirty rooms is a long walk and this is about
            // whether they set off the right way and keep going, which is the
            // thing that was impossible before.
            FireReactionScenarioData data = BigBuilding();
            var simulation = new FireReactionSimulation(data);

            LogicalPosition start = simulation.GetAgent(0).Position;
            long nearest = long.MaxValue;
            LogicalPosition wayOut = MiddleOf(Columns - 1, Rows - 1);
            for (int tick = 0; tick < 120 * FireReactionSimulation.TicksPerSecond; tick++)
            {
                simulation.Step();
                if (simulation.GetAgent(0).Participation != AgentParticipation.Participating)
                {
                    return;
                }

                long distance = IntegerMath.Distance(simulation.GetAgent(0).Position, wayOut);
                nearest = distance < nearest ? distance : nearest;
            }

            Assert.That(nearest, Is.LessThan(IntegerMath.Distance(start, wayOut) / 2),
                "In two minutes they did not get even halfway across the building towards the way out.");
        }
    }
}

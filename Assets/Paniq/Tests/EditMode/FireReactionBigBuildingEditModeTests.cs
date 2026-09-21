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

        /// <summary>
        /// Eight long rooms stacked up, joined at alternating ends, so getting
        /// from the bottom to the top means walking the length of every one of
        /// them: a route far longer than the building is wide plus tall.
        /// </summary>
        private FireReactionScenarioData SnakeOfRooms(int count, int width, int height)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.BlastHoles = new SimulationId[0];

            var rooms = new List<FireReactionRoomDefinition>();
            for (int i = 0; i < count; i++)
            {
                rooms.Add(new FireReactionRoomDefinition(
                    new SimulationId((ulong)(51000 + i)),
                    new LogicalBounds(0, width, i * height, (i + 1) * height)));
            }

            var doors = new List<FireReactionDoorDefinition>();
            for (int i = 0; i + 1 < count; i++)
            {
                // Alternating ends, so nobody can go straight up.
                int along = i % 2 == 0 ? width - 1000 : 1000;
                doors.Add(new FireReactionDoorDefinition(
                    new SimulationId((ulong)(21000 + i)), rooms[i].RoomId, WallSide.North, along, 1000, false));
            }

            data.Rooms = rooms.ToArray();
            data.Doors = doors.ToArray();
            data.Fire.SpawnBounds = new LogicalBounds(1000, 1000, 1000, 1000);
            data.Fire.ActivationTick = int.MaxValue;
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(
                    new SimulationId(10002UL), new LogicalPosition(1500, height / 2), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };

            return data;
        }

        [Test]
        public void ARouteThatWindsBackAndForth_IsStillFound()
        {
            // The cost of a route is held in buckets while it is worked out.
            // Sizing those buckets for a route no longer than the building is
            // wide plus tall quietly threw away everything past the end of
            // them, so a floor plan that wound about reported whole wings as
            // having no way to them -- and nothing was logged.
            FireReactionScenarioData data = SnakeOfRooms(8, 12000, 3000);
            var simulation = new FireReactionSimulation(data);
            int radius = data.World.OccupancyRadiusMillimetres;

            var bottom = new LogicalPosition(1500, 1500);
            var top = new LogicalPosition(1500, 7 * 3000 + 1500);
            long walking = simulation.GeometryForTests.Routes.WalkingDistance(bottom, top, radius);

            Assert.That(walking, Is.Not.EqualTo(long.MaxValue),
                "The far end of a winding building was reported as having no way to it.");
            Assert.That(walking, Is.GreaterThan(80000L),
                $"The route measured {walking} mm, which is too short to have gone the long way round every room.");
        }

        [Test]
        public void AHoleBlownThroughAWall_BecomesAWayToWalk()
        {
            // The floor squares are worked out when the building loads, from
            // the doorways it was authored with. A hole blown later is a new
            // way through a wall, and if the squares are never told, every
            // route goes on treating that wall as solid: somebody dragging an
            // unconscious body would haul them around for ever rather than use
            // the only way out there is.
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.Rooms = new[]
            {
                new FireReactionRoomDefinition(new SimulationId(52001UL), new LogicalBounds(-6000, 6000, -6000, 6000)),
                new FireReactionRoomDefinition(new SimulationId(52002UL), new LogicalBounds(6000, 18000, -6000, 6000))
            };

            // No doorway at all between them: the only way through is one blown.
            data.Doors = new FireReactionDoorDefinition[0];
            data.Fire.ActivationTick = int.MaxValue;
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(
                    new SimulationId(10003UL), new LogicalPosition(-3000, 0), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };

            var simulation = new FireReactionSimulation(data);
            WorldGeometry geometry = simulation.GeometryForTests;
            int radius = data.World.OccupancyRadiusMillimetres;
            var thisSide = new LogicalPosition(-3000, 0);
            var farSide = new LogicalPosition(12000, 0);

            Assert.That(geometry.Routes.CanGetFromHereToThere(thisSide, farSide, radius), Is.False,
                "The two rooms have no doorway between them, so there should be no way through.");

            Assert.That(geometry.TryPlaceHole(0, new LogicalPosition(6000, 0), out LogicalPosition centre), Is.True,
                "The wall between the rooms would not take a hole.");

            Assert.That(geometry.Routes.CanGetFromHereToThere(thisSide, farSide, radius), Is.True,
                $"A hole was blown at {centre} and the route still says the wall is solid.");
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

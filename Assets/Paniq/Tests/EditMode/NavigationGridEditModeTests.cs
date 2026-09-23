using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The floor drawn as small squares is what people will find their way
    /// across, so it has to describe the same building the rest of the rules
    /// believe in. These tests compare it with the geometry directly: which
    /// room each square belongs to, how much clear space is around it, and --
    /// the one that matters most -- that every doorway is still wide enough to
    /// walk through. A doorway wrongly read as solid would seal a room and burn
    /// everyone in it without raising a single error.
    /// </summary>
    public sealed class NavigationGridEditModeTests
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

        /// <summary>The biggest table in the building, which is the meeting room's.</summary>
        private static LogicalBounds LongestTable(ScenarioData data)
        {
            LogicalBounds longest = default;
            long biggest = 0;
            foreach (TableDefinition table in data.Tables)
            {
                long area = (long)(table.Bounds.MaxX - table.Bounds.MinX) * (table.Bounds.MaxZ - table.Bounds.MinZ);
                if (area > biggest)
                {
                    biggest = area;
                    longest = table.Bounds;
                }
            }

            return longest;
        }

        private WorldGeometry Geometry() =>
            new Run(scenario.ToRuntimeData()).GeometryForTests;

        /// <summary>Near enough to some doorway that its floor is expected to reach here.</summary>
        private static bool IsInADoorway(WorldGeometry geometry, ScenarioData data, LogicalPosition at)
        {
            for (int door = 0; door < data.Doors.Length; door++)
            {
                LogicalPosition centre = geometry.DoorCentre(door);
                int reach = data.Doors[door].WidthMillimetres + NavigationGrid.DoorwayReachMillimetres;
                if (System.Math.Abs(at.X - centre.X) <= reach && System.Math.Abs(at.Z - centre.Z) <= reach)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void EverySquare_BelongsToTheRoomTheGeometrySaysItIsIn()
        {
            ScenarioData data = scenario.ToRuntimeData();
            WorldGeometry geometry = new Run(data).GeometryForTests;
            NavigationGrid grid = geometry.Navigation;
            int checkedSquares = 0;

            for (int row = 0; row < grid.Rows; row++)
            {
                for (int column = 0; column < grid.Columns; column++)
                {
                    LogicalPosition centre = grid.CentreOf(column, row);
                    int cell = grid.CellAt(centre);
                    short saysGrid = grid.RoomOfCell(cell);
                    int saysGeometry = geometry.RoomAtPoint(centre);

                    if (saysGrid != NavigationGrid.Outside && saysGeometry >= 0)
                    {
                        Assert.That(saysGeometry, Is.EqualTo((int)saysGrid),
                            $"The square at ({centre.X}, {centre.Z}) says room {saysGrid}, the geometry says {saysGeometry}.");
                        checkedSquares++;
                    }
                    else if (saysGrid != NavigationGrid.Outside)
                    {
                        // Floor the room rectangles do not cover: the squares in
                        // a doorway, including the ground just outside a way out.
                        Assert.That(IsInADoorway(geometry, data, centre), Is.True,
                            $"The square at ({centre.X}, {centre.Z}) is called floor but is in no room and no doorway.");
                    }
                    else if (saysGeometry >= 0)
                    {
                        // The only squares inside a room that the grid calls
                        // outside are the ones under a table.
                        Assert.That(geometry.TableAt(centre, 0), Is.GreaterThanOrEqualTo(0),
                            $"The square at ({centre.X}, {centre.Z}) is in room {saysGeometry} with no table on it, " +
                            "but the grid says it is not floor.");
                    }
                }
            }

            Assert.That(checkedSquares, Is.GreaterThan(1000),
                "Too few squares were floor; the grid is probably not covering the building.");
        }

        [Test]
        public void Clearance_MatchesMeasuringEveryWallAndTableByHand()
        {
            // The brute-force oracle. Clearance decides whether a doorway is a
            // way through, so it is worth checking against the slow answer.
            WorldGeometry geometry = Geometry();
            NavigationGrid grid = geometry.Navigation;

            for (int row = 0; row < grid.Rows; row += 3)
            {
                for (int column = 0; column < grid.Columns; column += 3)
                {
                    LogicalPosition centre = grid.CentreOf(column, row);
                    int cell = grid.CellAt(centre);
                    if (grid.RoomOfCell(cell) == NavigationGrid.Outside)
                    {
                        continue;
                    }

                    long byHand = NavigationGrid.MaximumClearanceMillimetres;
                    foreach (NavigationGrid.Wall wall in geometry.WallsForTests)
                    {
                        long distance = wall.DistanceFrom(centre);
                        if (distance < byHand)
                        {
                            byHand = distance;
                        }
                    }

                    Assert.That(grid.ClearanceOfCell(cell), Is.EqualTo((int)byHand),
                        $"Clearance at ({centre.X}, {centre.Z}).");
                }
            }
        }

        [Test]
        public void TheFloorThroughEveryDoorway_IsSomewhereAPersonCanStand()
        {
            // A route that reached a doorway and stopped dead at it would leave
            // everybody shut in the room they started in, so the ground through
            // a doorway -- and just outside a way out -- has to be floor.
            ScenarioData data = scenario.ToRuntimeData();
            WorldGeometry geometry = new Run(data).GeometryForTests;
            NavigationGrid grid = geometry.Navigation;
            int radius = data.World.OccupancyRadiusMillimetres;

            for (int door = 0; door < data.Doors.Length; door++)
            {
                LogicalPosition centre = geometry.DoorCentre(door);
                Assert.That(grid.Fits(grid.CellAt(centre), radius), Is.True,
                    $"Nobody could stand in the middle of door {data.Doors[door].DoorId}.");
            }
        }

        [Test]
        public void EveryDoorway_IsWideEnoughToWalkThrough()
        {
            ScenarioData data = scenario.ToRuntimeData();
            WorldGeometry geometry = new Run(data).GeometryForTests;
            int radius = data.World.OccupancyRadiusMillimetres;

            for (int door = 0; door < data.Doors.Length; door++)
            {
                Assert.That(geometry.WidestBodyThroughDoor(door), Is.GreaterThanOrEqualTo(radius),
                    $"Door {data.Doors[door].DoorId} is too narrow for a person to fit through, " +
                    "which would seal the room behind it.");
            }
        }

        [Test]
        public void APointInsideATable_FindsFloorBesideIt()
        {
            // Things worth walking to are not always places anybody can stand:
            // flames burning on a desk sit on a square the grid calls solid.
            // Asking whether that square can be walked to always answers no,
            // which reads as "there is no way to the fire" and makes everybody
            // give up on fighting it.
            ScenarioData data = scenario.ToRuntimeData();
            int radius = data.World.OccupancyRadiusMillimetres;
            WorldGeometry geometry = new Run(data).GeometryForTests;
            NavigationGrid grid = geometry.Navigation;

            LogicalBounds table = LongestTable(data);
            var middleOfIt = new LogicalPosition((table.MinX + table.MaxX) / 2, (table.MinZ + table.MaxZ) / 2);
            Assert.That(grid.Fits(grid.CellAt(middleOfIt), radius), Is.False, "The test needs a spot nobody can stand on.");

            LogicalPosition beside = grid.NearestStandableTo(middleOfIt, radius, 2500);
            Assert.That(beside, Is.Not.EqualTo(middleOfIt), "It gave back the spot inside the table unchanged.");
            Assert.That(grid.Fits(grid.CellAt(beside), radius), Is.True, "The spot it gave back is not floor either.");
        }

        [Test]
        public void APointAlreadyOnClearFloor_IsLeftAlone()
        {
            ScenarioData data = scenario.ToRuntimeData();
            int radius = data.World.OccupancyRadiusMillimetres;
            NavigationGrid grid = new Run(data).GeometryForTests.Navigation;

            var openFloor = new LogicalPosition(-4000, -4000);
            Assert.That(grid.Fits(grid.CellAt(openFloor), radius), Is.True, "The test needs a spot somebody can stand on.");
            Assert.That(grid.NearestStandableTo(openFloor, radius, 2500), Is.EqualTo(openFloor));
        }

        [Test]
        public void TheMeetingRoomTable_ReadsAsOneSolidBlockDespiteBeingAuthoredAsTwo()
        {
            // The long table is authored as two touching rectangles, because the
            // room model only understands single rectangles. On the grid it is
            // simply floor nobody can stand on, seam and all -- which is the
            // first sign that the shape of the world is no longer limited to
            // what one rectangle can say.
            ScenarioData data = scenario.ToRuntimeData();
            WorldGeometry geometry = new Run(data).GeometryForTests;
            NavigationGrid grid = geometry.Navigation;

            LogicalBounds longest = LongestTable(data);

            int solid = 0;
            for (int x = longest.MinX + 125; x < longest.MaxX; x += NavigationGrid.CellSizeMillimetres)
            {
                for (int z = longest.MinZ + 125; z < longest.MaxZ; z += NavigationGrid.CellSizeMillimetres)
                {
                    var at = new LogicalPosition(x, z);
                    Assert.That(grid.RoomOfCell(grid.CellAt(at)), Is.EqualTo(NavigationGrid.Outside),
                        $"({x}, {z}) is on the table but the grid calls it floor.");
                    solid++;
                }
            }

            Assert.That(solid, Is.GreaterThan(20), "The table covered too few squares to be the long one.");
        }
    }
}

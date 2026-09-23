using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Finding the way across a room, which nothing could do before. The tests
    /// that matter here are the ones that would have failed under the old rule
    /// of "point yourself at it and walk": a goal behind a desk, and a goal in
    /// another room.
    /// </summary>
    public sealed class NavigationRoutesEditModeTests
    {
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

        private WorldGeometry Geometry(out int radius)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            radius = data.World.OccupancyRadiusMillimetres;
            return new FireReactionSimulation(data).GeometryForTests;
        }

        [Test]
        public void SomewhereInAnotherRoom_CanBeWalkedTo()
        {
            // The office and the meeting room are at opposite ends of the
            // building, joined by a corridor. Nothing in the simulation could
            // work out how to get from one to the other except door by door in
            // straight lines.
            WorldGeometry geometry = Geometry(out int radius);
            var inTheOffice = new LogicalPosition(-4000, 0);
            LogicalPosition inTheMeetingRoom = TheBuilding.MeetingRoom;

            Assert.That(geometry.Routes.CanGetFromHereToThere(inTheOffice, inTheMeetingRoom, radius), Is.True,
                "There is no way from the office to the meeting room.");
        }

        [Test]
        public void WalkingRound_IsFurtherThanTheStraightLineThroughTheWall()
        {
            // The honest measure of whether this is really routing: walking to
            // the meeting room means going along the corridor, so it has to be
            // further than the straight line that goes through two walls.
            WorldGeometry geometry = Geometry(out int radius);
            var inTheOffice = new LogicalPosition(-4000, 0);
            LogicalPosition inTheMeetingRoom = TheBuilding.MeetingRoom;

            long walking = geometry.Routes.WalkingDistance(inTheOffice, inTheMeetingRoom, radius);
            long asTheCrowFlies = IntegerMath.Distance(inTheOffice, inTheMeetingRoom);

            Assert.That(walking, Is.GreaterThan(asTheCrowFlies),
                $"Walking was measured at {walking} mm and the straight line at {asTheCrowFlies} mm; " +
                "the route is going through the walls.");
        }

        [Test]
        public void HeadingTowardsSomethingBehindADesk_DoesNotPointStraightAtTheDesk()
        {
            // The whole point. Standing on one side of the meeting room's long
            // table with a goal on the other side, the old answer was to face
            // the table and walk into it.
            FireReactionScenarioData data = scenario.ToRuntimeData();
            int radius = data.World.OccupancyRadiusMillimetres;
            WorldGeometry geometry = new FireReactionSimulation(data).GeometryForTests;

            LogicalBounds table = LongestTable(data);
            var thisSide = new LogicalPosition((table.MinX + table.MaxX) / 2, table.MinZ - 700);
            var farSide = new LogicalPosition((table.MinX + table.MaxX) / 2, table.MaxZ + 700);

            int straight = IntegerMath.HeadingBetween(thisSide, farSide, 0);
            int routed = geometry.Routes.HeadingToward(thisSide, farSide, radius, straight);

            Assert.That(geometry.Routes.CanGetFromHereToThere(thisSide, farSide, radius), Is.True,
                "There is no way round the table at all.");
            Assert.That(routed, Is.Not.EqualTo(straight),
                "The way to face is still straight at the table rather than round it.");
        }

        [Test]
        public void HeadingTowardsSomethingInPlainSight_IsStraightAtIt()
        {
            // Short, clear journeys must stay exactly as direct as they are
            // now. Routing round squares is only for when something is in the
            // way; otherwise people would walk in faint zig-zags.
            WorldGeometry geometry = Geometry(out int radius);
            var from = new LogicalPosition(-4000, -4000);
            var to = new LogicalPosition(-3000, -3000);

            int straight = IntegerMath.HeadingBetween(from, to, 0);
            Assert.That(geometry.Routes.HeadingToward(from, to, radius, straight), Is.EqualTo(straight));
        }

        [Test]
        public void SomewhereNobodyCouldStand_IsNotWalkedTo()
        {
            // A goal inside a table, or outside the building. The answer has to
            // be "no way", not a route that ends in a wall.
            FireReactionScenarioData data = scenario.ToRuntimeData();
            int radius = data.World.OccupancyRadiusMillimetres;
            WorldGeometry geometry = new FireReactionSimulation(data).GeometryForTests;

            LogicalBounds table = LongestTable(data);
            var insideTheTable = new LogicalPosition((table.MinX + table.MaxX) / 2, (table.MinZ + table.MaxZ) / 2);
            var inTheOffice = new LogicalPosition(-4000, 0);

            Assert.That(geometry.Routes.CanGetFromHereToThere(inTheOffice, insideTheTable, radius), Is.False,
                "The middle of a table was treated as somewhere to walk to.");
        }

        private static LogicalBounds LongestTable(FireReactionScenarioData data)
        {
            LogicalBounds longest = default;
            long biggest = 0;
            foreach (FireReactionTableDefinition table in data.Tables)
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
    }
}

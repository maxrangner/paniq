using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Places in the default building, by name rather than by coordinate.
    /// <para>
    /// Tests used to write the coordinates out longhand -- a person at
    /// (15000, 0) meaning "somebody in the meeting room, far from the fire".
    /// That reads as a magic number, and when the floor plan changed, seventy
    /// tests failed at once with "agent 2 starts outside every room" and no
    /// hint of which room they had meant. Naming the place says what the test
    /// is actually about, and the next time a room moves this file moves with
    /// it instead of all of them.
    /// </para>
    /// <para>
    /// The open office and the storage closet are deliberately unchanged from
    /// prototype 1, so tests that only ever used the office need nothing from
    /// here.
    /// </para>
    /// </summary>
    internal static class TheBuilding
    {
        /// <summary>The middle of the open-plan office, where the fire usually starts.</summary>
        public static readonly LogicalPosition Office = new LogicalPosition(0, 0);

        /// <summary>The office's south-west corner, about as far from the corridor as the room goes.</summary>
        public static readonly LogicalPosition OfficeFarCorner = new LogicalPosition(-4500, -4500);

        /// <summary>Just inside the office, below its door onto the corridor.</summary>
        public static readonly LogicalPosition OfficeByItsDoor = new LogicalPosition(0, 5000);

        /// <summary>The storage closet off the office's east wall. Unchanged from prototype 1.</summary>
        public static readonly LogicalPosition Closet = new LogicalPosition(7000, 2500);

        /// <summary>The middle of the long corridor every room opens onto.</summary>
        public static readonly LogicalPosition Corridor = new LogicalPosition(3500, 7500);

        /// <summary>The corridor's west end, outside the maintenance room's door.</summary>
        public static readonly LogicalPosition CorridorWestEnd = new LogicalPosition(-5000, 7500);

        /// <summary>
        /// Clear floor in the meeting room, across the corridor from the
        /// office. Deliberately not the middle of the room: the middle of the
        /// meeting room is the middle of the meeting table, which is not floor
        /// and which nothing can walk to.
        /// </summary>
        public static readonly LogicalPosition MeetingRoom = new LogicalPosition(-4000, 15500);

        /// <summary>The middle of the cafeteria, at the exit end of the corridor.</summary>
        public static readonly LogicalPosition Cafeteria = new LogicalPosition(7000, 13000);

        /// <summary>The bathroom, across the corridor from the cafeteria.</summary>
        public static readonly LogicalPosition Bathroom = new LogicalPosition(10500, 4000);

        /// <summary>The maintenance room at the dead west end of the floor.</summary>
        public static readonly LogicalPosition Maintenance = new LogicalPosition(-7500, 7500);

        /// <summary>The crossbar of the T, between the way out and the dead end.</summary>
        public static readonly LogicalPosition Crossbar = new LogicalPosition(14500, 9000);

        /// <summary>Just inside the building's one way out.</summary>
        public static readonly LogicalPosition InsideTheWayOut = new LogicalPosition(14500, 16000);

        /// <summary>Outside the building, through the way out.</summary>
        public static readonly LogicalPosition OutsideTheWayOut = new LogicalPosition(14500, 18500);

        /// <summary>The building's one way out. It starts locked: it is the player's to open.</summary>
        public static readonly SimulationId TheWayOut = new SimulationId(2008UL);

        /// <summary>The office's door onto the storage closet. Unchanged from prototype 1.</summary>
        public static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>The office's door onto the corridor.</summary>
        public static readonly SimulationId OfficeDoor = new SimulationId(2005UL);

        /// <summary>The meeting room's door onto the corridor.</summary>
        public static readonly SimulationId MeetingRoomDoor = new SimulationId(2006UL);

        /// <summary>The cafeteria's door onto the corridor.</summary>
        public static readonly SimulationId CafeteriaDoor = new SimulationId(2011UL);

        /// <summary>The cafeteria's second door, onto the arm nearer the way out.</summary>
        public static readonly SimulationId CafeteriaShortcut = new SimulationId(2009UL);

        /// <summary>The bathroom's door onto the corridor.</summary>
        public static readonly SimulationId BathroomDoor = new SimulationId(2012UL);

        /// <summary>The maintenance room's door onto the corridor's west end.</summary>
        public static readonly SimulationId MaintenanceDoor = new SimulationId(2010UL);

        /// <summary>The archway where the corridor Ts. It is an opening, not a door: nothing shuts it.</summary>
        public static readonly SimulationId Archway = new SimulationId(2016UL);

        /// <summary>
        /// The shipped building, with the fire pinned to the open office.
        /// <para>
        /// A played round draws the fire from one of four preset areas, so
        /// three runs in four start it somewhere other than the office. That is
        /// the point of it, and it is covered by its own tests -- but a test
        /// about what a frightened crowd does needs the fire where the crowd
        /// is, every time, or it is really a test of which room came up.
        /// </para>
        /// </summary>
        public static FireReactionScenarioData WithTheFireInTheOffice(FireReactionScenarioData data)
        {
            data.Fire.SpawnBounds = new LogicalBounds(-4500, 4500, -4500, 4500);
            return data;
        }

        /// <summary>A fire in one square, at a named place, that never spreads on its own.</summary>
        public static void FireAt(FireReactionScenarioData data, LogicalPosition where)
        {
            data.Fire.SpawnBounds = new LogicalBounds(where.X, where.X, where.Z, where.Z);
        }
    }
}

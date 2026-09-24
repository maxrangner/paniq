using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The shape of the world, and the only code that knows it: rectangular
    /// rooms that never overlap, doors set into their walls, and tables
    /// standing on their floors. Two rooms that share a wall line are joined
    /// by a door in it; a door with no room beyond leads outside, which is
    /// how people escape. Each open door adds a walkable strip through the
    /// wall. A table is a solid rectangle: bodies slide along its edges as
    /// they would along a wall. Every question about where a body may be
    /// (walking, steering off walls, getting from one room to the next,
    /// leaving the building) is answered here, so a new floor plan changes
    /// this class rather than every system.
    /// </summary>
    internal sealed class WorldGeometry
    {
        /// <summary>How close to a door's centre line a runner must be to head through it, beyond a full fit.</summary>
        private const int LinedUpTolerance = 50;

        /// <summary>Random spots are redrawn this many times at most to keep them clear of tables.</summary>
        private const int ClearSpotAttempts = 8;

        /// <summary>Random spots stay at least this far clear of a table's edge, beyond a body's radius.</summary>
        private const int TableSpotClearance = 300;

        private readonly SimulationContext context;
        private readonly DoorRuntime[] doors;
        private readonly int radius;
        private readonly ExitSettings exits;
        private readonly LogicalBounds[] tables;

        /// <summary>Where each table stands and how it is turned, as the engine last had it.</summary>
        private readonly BodyPose[] tablePoses;

        /// <summary>The floor each table covered when the walkable squares were last worked out.</summary>
        private readonly LogicalBounds[] tablesAsBaked;
        private readonly SimulationId[] tableIds;

        /// <summary>How many of the door slots are really there; the rest are spare.</summary>
        private int placedCount;

        /// <summary>The rooms as authored: rectangles that never overlap, in scenario order.</summary>
        private readonly LogicalBounds[] rooms;
        private readonly SimulationId[] roomIds;

        /// <summary>Per door: the room beyond it, or -1 when it leads outside.</summary>
        private readonly int[] doorNeighbour;

        /// <summary>Per room: every door in its walls, whichever room holds the door.</summary>
        private int[][] roomDoors;

        /// <summary>
        /// The floor drawn as small squares: which room each one is in, and how
        /// much clear space is around it. Built once from the rooms, walls and
        /// tables as authored.
        /// </summary>
        private readonly NavigationGrid navigationGrid;

        /// <summary>Which way to go to get anywhere, going round what is in the way.</summary>
        private readonly Navigation navigation;

        // Route-finding scratch space, reused every call so a run allocates nothing.
        private readonly long[] routeCost;
        private readonly int[] routeFirstDoor;
        private readonly int[] routeEntryDoor;
        private readonly bool[] routeSettled;

        /// <summary>
        /// How far it is to walk to each door from every square of floor,
        /// per door and per side of it (the door's own room, then the room
        /// beyond): what a route between rooms costs its legs with. Worked out
        /// the first time a door is asked about and thrown away when the floor
        /// changes shape, so a run pays for each once. Null until asked.
        /// </summary>
        private readonly int[][] doorWalks;

        /// <summary>The one field used to work the door walks out, kept so working one out allocates nothing.</summary>
        private FlowField doorWalkScratch;

        public WorldGeometry(SimulationContext context, DoorRuntime[] doors)
        {
            this.context = context;
            this.doors = doors;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;
            exits = context.Scenario.Exits;

            var definitions = (TableDefinition[])context.Scenario.Tables.Clone();
            Array.Sort(definitions, (left, right) => left.TableId.CompareTo(right.TableId));
            tables = new LogicalBounds[definitions.Length];
            tableIds = new SimulationId[definitions.Length];
            tablePoses = new BodyPose[definitions.Length];
            tablesAsBaked = new LogicalBounds[definitions.Length];
            for (int i = 0; i < tables.Length; i++)
            {
                tables[i] = definitions[i].Bounds;
                tablesAsBaked[i] = definitions[i].Bounds;
                tableIds[i] = definitions[i].TableId;
            }

            // Rooms keep their authored order: the first one is where the fire starts.
            RoomDefinition[] authored = context.Scenario.Rooms;
            rooms = new LogicalBounds[authored.Length];
            roomIds = new SimulationId[authored.Length];
            for (int r = 0; r < authored.Length; r++)
            {
                rooms[r] = authored[r].Bounds;
                roomIds[r] = authored[r].RoomId;
            }

            doorNeighbour = new int[doors.Length];

            // Doors the scenario authored come first; the spare slots a blast
            // hole can be placed in sit at the tail and are not in the world yet.
            placedCount = 0;
            for (int d = 0; d < doors.Length; d++)
            {
                placedCount += doors[d].Placed ? 1 : 0;
            }

            BuildRoomDoors();

            routeCost = new long[rooms.Length];
            routeFirstDoor = new int[rooms.Length];
            routeEntryDoor = new int[rooms.Length];
            routeSettled = new bool[rooms.Length];

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (LogicalBounds room in rooms)
            {
                minX = Math.Min(minX, room.MinX);
                maxX = Math.Max(maxX, room.MaxX);
                minZ = Math.Min(minZ, room.MinZ);
                maxZ = Math.Max(maxZ, room.MaxZ);
            }

            FireArea = new LogicalBounds(minX, maxX, minZ, maxZ);
            navigationGrid = new NavigationGrid(FireArea, rooms, tables, BuildWalls(), BuildDoorways());
            RefuseDoorwaysNobodyCanFitThrough();
            navigation = new Navigation(context, navigationGrid);
            doorWalks = new int[doors.Length * 2][];
        }

        /// <summary>
        /// A doorway too narrow for the people who have to use it seals the room
        /// behind it, and every one of them burns without anything going wrong
        /// that anybody could see. Better to refuse the floor plan when it is
        /// loaded, naming the door.
        /// </summary>
        private void RefuseDoorwaysNobodyCanFitThrough()
        {
            for (int door = 0; door < doors.Length; door++)
            {
                if (!doors[door].Placed)
                {
                    continue;
                }

                int widest = WidestBodyThroughDoor(door);
                if (widest < radius)
                {
                    throw new InvalidOperationException(
                        $"Door {doors[door].Id} leaves room for a body of only {widest} mm where {radius} mm is " +
                        "needed, so nobody could walk through it and the room behind it would be sealed.");
                }
            }
        }

        /// <summary>The floor drawn as small squares. See <see cref="NavigationGrid"/>.</summary>
        public NavigationGrid Navigation => navigationGrid;

        /// <summary>Which way to go to get anywhere. See <see cref="Simulation.Navigation"/>.</summary>
        public Navigation Routes => navigation;

        /// <summary>Every stretch of solid wall and table edge, for a test to measure against by hand.</summary>
        internal List<NavigationGrid.Wall> WallsForTests => AllSolidEdges();

        /// <summary>
        /// Every stretch of solid wall with the doorways in the world cut out
        /// of it, for the physics to build walls from. A spare slot for a blast
        /// hole is wall until the hole is made.
        /// </summary>
        internal List<NavigationGrid.Wall> SolidWalls() => BuildWalls();

        /// <summary>
        /// How many door slots there are in all, placed or spare. The physics
        /// keeps a plug for each, there while that doorway is shut.
        /// </summary>
        internal int DoorSlotCount => doors.Length;

        /// <summary>Where a door slot's gap is: its centre, which way the wall runs, and how wide it is.</summary>
        internal void DescribeDoorway(int door, out LogicalPosition centre, out bool alongX, out int width)
        {
            centre = DoorCentre(door);
            alongX = doors[door].Side == WallSide.North || doors[door].Side == WallSide.South;
            width = doors[door].Width;
        }

        /// <summary>
        /// Whether this slot's doorway is a gap anything can pass through right
        /// now: placed in the world, and open or broken.
        /// </summary>
        internal bool IsDoorwayClear(int door) => doors[door].Placed && IsDoorOpen(door);

        /// <summary>
        /// Whether this slot's doorway needs plugging: placed in the world and
        /// shut. A spare slot kept for a blast hole is plain wall until the hole
        /// is made, and a hole is never shut, so neither is ever plugged.
        /// </summary>
        internal bool IsDoorwayPlugged(int door) => doors[door].Placed && !IsDoorOpen(door);

        /// <summary>The walls with their doorways removed, plus the four sides of every table.</summary>
        private List<NavigationGrid.Wall> AllSolidEdges()
        {
            List<NavigationGrid.Wall> edges = BuildWalls();
            for (int t = 0; t < tables.Length; t++)
            {
                LogicalBounds b = tables[t];
                edges.Add(new NavigationGrid.Wall(new LogicalPosition(b.MinX, b.MinZ), new LogicalPosition(b.MaxX, b.MinZ)));
                edges.Add(new NavigationGrid.Wall(new LogicalPosition(b.MaxX, b.MinZ), new LogicalPosition(b.MaxX, b.MaxZ)));
                edges.Add(new NavigationGrid.Wall(new LogicalPosition(b.MaxX, b.MaxZ), new LogicalPosition(b.MinX, b.MaxZ)));
                edges.Add(new NavigationGrid.Wall(new LogicalPosition(b.MinX, b.MaxZ), new LogicalPosition(b.MinX, b.MinZ)));
            }

            return edges;
        }

        /// <summary>
        /// The widest body that could pass through a doorway, in millimetres of
        /// radius. Measured from the grid rather than from the door's stated
        /// width, so it accounts for anything else built close to the gap. A
        /// door narrower than the people who have to use it seals a room, and
        /// the scenario is refused rather than letting everyone burn in silence.
        /// </summary>
        public int WidestBodyThroughDoor(int door)
        {
            // Sampled half a square inside the doorway's own room rather than
            // on the wall line itself: a square sitting exactly on that line
            // belongs to whichever side its middle falls, and for a way out
            // that side is the street.
            int half = doors[door].Width / 2;
            int inset = -NavigationGrid.CellSizeMillimetres / 2;
            return navigationGrid.WidestBodyThroughGap(
                DoorPoint(door, -half + 1, inset),
                DoorPoint(door, half - 1, inset));
        }

        /// <summary>
        /// Every stretch of solid wall, with the doorways taken out of it. A
        /// room's four sides each become one or more pieces: a side with a door
        /// in it becomes the piece to one side of the gap and the piece to the
        /// other, and the gap itself is simply not there. That is what makes a
        /// doorway a way through rather than a thinner piece of wall.
        /// </summary>
        private List<NavigationGrid.Wall> BuildWalls()
        {
            var built = new List<NavigationGrid.Wall>();
            for (int room = 0; room < rooms.Length; room++)
            {
                foreach (WallSide side in new[] { WallSide.North, WallSide.South, WallSide.East, WallSide.West })
                {
                    AddWallWithItsDoorwaysRemoved(built, room, side);
                }
            }

            return built;
        }

        /// <summary>Where every doorway is, so the grid can mark the floor through it walkable.</summary>
        private List<NavigationGrid.Doorway> BuildDoorways()
        {
            var built = new List<NavigationGrid.Doorway>();
            for (int door = 0; door < doors.Length; door++)
            {
                if (!doors[door].Placed)
                {
                    continue;
                }

                bool alongX = doors[door].Side == WallSide.North || doors[door].Side == WallSide.South;
                built.Add(new NavigationGrid.Doorway(
                    DoorCentre(door), alongX, doors[door].Width / 2, doors[door].Room));
            }

            return built;
        }

        private void AddWallWithItsDoorwaysRemoved(List<NavigationGrid.Wall> into, int room, WallSide side)
        {
            LogicalBounds b = rooms[room];
            bool alongX = side == WallSide.North || side == WallSide.South;
            int from = alongX ? b.MinX : b.MinZ;
            int to = alongX ? b.MaxX : b.MaxZ;

            // The gaps in this side, in order along it.
            var gaps = new List<(int From, int To)>();
            int[] candidates = roomDoors[room];
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                if (!doors[door].Placed || WallSideFrom(door, room) != side)
                {
                    continue;
                }

                int half = doors[door].Width / 2;
                gaps.Add((doors[door].Centre - half, doors[door].Centre + half));
            }

            gaps.Sort((left, right) => left.From.CompareTo(right.From));

            int at = from;
            foreach ((int gapFrom, int gapTo) in gaps)
            {
                if (gapFrom > at)
                {
                    into.Add(PieceOfWall(b, side, alongX, at, gapFrom));
                }

                at = Math.Max(at, gapTo);
            }

            if (at < to)
            {
                into.Add(PieceOfWall(b, side, alongX, at, to));
            }
        }

        private static NavigationGrid.Wall PieceOfWall(LogicalBounds room, WallSide side, bool alongX, int from, int to)
        {
            int across = side switch
            {
                WallSide.North => room.MaxZ,
                WallSide.South => room.MinZ,
                WallSide.East => room.MaxX,
                _ => room.MinX
            };

            return alongX
                ? new NavigationGrid.Wall(new LogicalPosition(from, across), new LogicalPosition(to, across))
                : new NavigationGrid.Wall(new LogicalPosition(across, from), new LogicalPosition(across, to));
        }

        /// <summary>
        /// The room on the far side of a door's wall: the one flush against
        /// it whose wall covers the whole gap, or -1 for the outside. The
        /// scenario refuses a room that covers only part of a gap.
        /// </summary>
        private int FindNeighbour(int door)
        {
            DoorRuntime d = doors[door];
            LogicalBounds room = rooms[d.Room];
            bool alongX = d.Side == WallSide.North || d.Side == WallSide.South;
            int half = d.Width / 2;
            for (int r = 0; r < rooms.Length; r++)
            {
                if (r == d.Room)
                {
                    continue;
                }

                LogicalBounds other = rooms[r];
                bool flush;
                switch (d.Side)
                {
                    case WallSide.North:
                        flush = other.MinZ == room.MaxZ;
                        break;
                    case WallSide.South:
                        flush = other.MaxZ == room.MinZ;
                        break;
                    case WallSide.East:
                        flush = other.MinX == room.MaxX;
                        break;
                    default:
                        flush = other.MaxX == room.MinX;
                        break;
                }

                int otherMin = alongX ? other.MinX : other.MinZ;
                int otherMax = alongX ? other.MaxX : other.MaxZ;
                if (flush && otherMin <= d.Centre - half && otherMax >= d.Centre + half)
                {
                    return r;
                }
            }

            return -1;
        }

        // ---------------------------------------------------------------- rooms

        /// <summary>The rectangle around every room; the fire grid covers exactly this.</summary>
        public LogicalBounds FireArea { get; }

        public int RoomCount => rooms.Length;

        public LogicalBounds RoomBounds(int room) => rooms[room];

        public SimulationId RoomId(int room) => roomIds[room];

        /// <summary>Every door in this room's walls, whichever of the two rooms holds it.</summary>
        public int[] RoomDoors(int room) => roomDoors[room];

        /// <summary>The room a person's whole footprint is inside, or -1 (in a doorway, or out of the building).</summary>
        public int RoomAt(LogicalPosition position)
        {
            // The square under the point already knows its room. Trusted only
            // when the footprint really is inside that room: a square under a
            // table is marked as no room, a wall that does not sit on a square
            // edge can put a point in the room next door, and either way the
            // rooms are then walked as they always were.
            int guess = navigationGrid.RoomOfCell(navigationGrid.CellAt(position));
            if (guess >= 0 && rooms[guess].ContainsCircle(position, radius))
            {
                return guess;
            }

            for (int r = 0; r < rooms.Length; r++)
            {
                if (rooms[r].ContainsCircle(position, radius))
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>
        /// The patch of floor holding everybody who counts as being in a room:
        /// the room itself, and its doorways, which for a way out reach the
        /// doorway depth past the wall. What to ask the index for before the
        /// exact "which room are they in" test.
        /// </summary>
        public LogicalBounds RoomAreaWithDoorways(int room)
        {
            // Somebody in a doorway is within a footprint and a hand of the wall
            // line, or within the doorway depth of a way out; a spare body
            // width on top costs nothing and covers a person shoved into a wall.
            LogicalBounds b = rooms[room];
            int reach = Math.Max(radius + 100, exits.DoorwayDepthMillimetres) + 2 * radius;
            return new LogicalBounds(b.MinX - reach, b.MaxX + reach, b.MinZ - reach, b.MaxZ + reach);
        }

        /// <summary>
        /// The patch of floor holding every point <see cref="IsInDoorway"/>
        /// could say yes to, so a doorway can be checked for people by reading
        /// the index rather than everybody.
        /// </summary>
        public LogicalBounds PersonDoorwaySearchArea(int door)
        {
            int along = doors[door].Width / 2 + radius;
            int inside = radius + 100;
            int outside = doorNeighbour[door] < 0 ? Math.Max(inside, exits.DoorwayDepthMillimetres) : inside;
            LogicalPosition a = DoorPoint(door, -along, -inside);
            LogicalPosition b = DoorPoint(door, along, outside);
            return new LogicalBounds(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.Z, b.Z));
        }

        /// <summary>
        /// The room a person counts as being in: the one holding their whole
        /// body, or, while they are in a doorway, the last one they were in.
        /// </summary>
        public int RoomOf(Agent agent)
        {
            int room = RoomAt(agent.Body.Position);
            if (room >= 0)
            {
                return room;
            }

            return agent.Doors.CurrentRoom >= 0 ? agent.Doors.CurrentRoom : RoomStoodIn(agent.Body.Position);
        }

        /// <summary>
        /// The room a point is standing in, and if it is standing in none of
        /// them -- in a doorway, or outside the building -- the room it is
        /// nearest to.
        ///
        /// This replaces three places that used to answer "room zero" when they
        /// could not tell. Room zero is the office the fire starts in, so a box
        /// knocked into a doorway at the far end of the building was liable to
        /// be shoved back inside the office's walls, across the whole floor
        /// plan, with nothing reporting anything amiss. It only looked harmless
        /// because almost everything happens in room zero.
        /// </summary>
        internal int RoomStoodIn(LogicalPosition point)
        {
            short onTheGrid = navigationGrid.RoomOfCell(navigationGrid.CellAt(point));
            if (onTheGrid != NavigationGrid.Outside)
            {
                return onTheGrid;
            }

            int nearest = 0;
            long best = long.MaxValue;
            for (int r = 0; r < rooms.Length; r++)
            {
                long distance = rooms[r].DistanceSquaredTo(point);
                if (distance < best)
                {
                    best = distance;
                    nearest = r;
                }
            }

            return nearest;
        }

        /// <summary>The room a point is in, ignoring body size, or -1 (outside, or exactly on a wall line).</summary>
        public int RoomAtPoint(LogicalPosition point)
        {
            // The square's own room first, trusted only when the point really
            // is inside it; see RoomAt for why.
            int guess = navigationGrid.RoomOfCell(navigationGrid.CellAt(point));
            if (guess >= 0 && StrictlyInside(rooms[guess], point))
            {
                return guess;
            }

            for (int r = 0; r < rooms.Length; r++)
            {
                if (StrictlyInside(rooms[r], point))
                {
                    return r;
                }
            }

            return -1;
        }

        private static bool StrictlyInside(LogicalBounds b, LogicalPosition point) =>
            point.X > b.MinX && point.X < b.MaxX && point.Z > b.MinZ && point.Z < b.MaxZ;

        /// <summary>The room the other side of a door from <paramref name="room"/>, or -1 for outside.</summary>
        public int RoomBeyond(int door, int room)
        {
            return doors[door].Room == room ? doorNeighbour[door] : doors[door].Room;
        }

        /// <summary>True when this door leads out of the building.</summary>
        public bool DoorLeadsOutside(int door) => doorNeighbour[door] < 0;

        /// <summary>The room whose wall holds this door.</summary>
        public int DoorRoom(int door) => doors[door].Room;

        /// <summary>True when the door is in one of this room's walls.</summary>
        public bool DoorTouchesRoom(int door, int room)
        {
            return room >= 0 && (doors[door].Room == room || doorNeighbour[door] == room);
        }

        /// <summary>Which side of a door's wall line a point is on: 1 or -1. Used to work out which way a pushed-open leaf swings.</summary>
        public int SideOf(int door, LogicalPosition position) => BeyondDistance(door, position) > 0 ? 1 : -1;

        /// <summary>
        /// Two rooms can see and hear each other freely: the same room, or
        /// joined by an open door. Anything not in a room counts as connected.
        /// </summary>
        public bool RoomsOpenToEachOther(int roomA, int roomB)
        {
            if (roomA < 0 || roomB < 0 || roomA == roomB)
            {
                return true;
            }

            int[] candidates = roomDoors[roomA];
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsDoorOpen(candidates[i]) && RoomBeyond(candidates[i], roomA) == roomB)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a straight line of sight runs from a spot in one room to a
        /// spot in another: the same room; or through the gap of an open
        /// doorway (an archway counts) joining the two; or through two such
        /// gaps with one room between, the way somebody in the meeting room
        /// sees across the corridor into the office when both doors stand
        /// open. Walls hide everything else, including the room beyond an
        /// open door when the line does not pass through the door itself.
        /// A spot in no room (a doorway) sees and is seen freely.
        /// </summary>
        public bool CanSeeBetween(int eyeRoom, LogicalPosition eye, int targetRoom, LogicalPosition target)
        {
            if (eyeRoom < 0 || targetRoom < 0 || eyeRoom == targetRoom)
            {
                return true;
            }

            int[] first = roomDoors[eyeRoom];
            for (int i = 0; i < first.Length; i++)
            {
                int door = first[i];
                if (!IsDoorOpen(door) || !SightCrossesDoorway(door, eye, target))
                {
                    continue;
                }

                int between = RoomBeyond(door, eyeRoom);
                if (between == targetRoom)
                {
                    return true;
                }

                if (between < 0)
                {
                    continue;
                }

                int[] second = roomDoors[between];
                for (int j = 0; j < second.Length; j++)
                {
                    int next = second[j];
                    if (next != door && IsDoorOpen(next) && RoomBeyond(next, between) == targetRoom &&
                        SightCrossesDoorway(next, eye, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether the straight line from one point to the other crosses this door's wall inside the door's gap.</summary>
        private bool SightCrossesDoorway(int door, LogicalPosition a, LogicalPosition b)
        {
            DoorRuntime d = doors[door];
            LogicalPosition centre = DoorCentre(door);
            bool wallRunsAlongX = d.Side == WallSide.North || d.Side == WallSide.South;
            long wall = wallRunsAlongX ? centre.Z : centre.X;
            long aAcross = wallRunsAlongX ? a.Z : a.X;
            long bAcross = wallRunsAlongX ? b.Z : b.X;
            long aAlong = wallRunsAlongX ? a.X : a.Z;
            long bAlong = wallRunsAlongX ? b.X : b.Z;
            long fromA = aAcross - wall;
            long fromB = bAcross - wall;
            if ((fromA > 0L && fromB > 0L) || (fromA < 0L && fromB < 0L) || (fromA == 0L && fromB == 0L))
            {
                // Both on one side of the wall, or both on the wall line.
                return false;
            }

            long crossingAlong = aAlong + (bAlong - aAlong) * (wall - aAcross) / (bAcross - aAcross);
            long gapCentre = wallRunsAlongX ? centre.X : centre.Z;
            return Math.Abs(crossingAlong - gapCentre) <= d.Width / 2;
        }

        /// <summary>
        /// Whether fire may jump between two neighbouring grid cells in
        /// different rooms: only through the open door between them, where
        /// the edge the cells share lies across the door gap.
        /// </summary>
        public bool FireCanCross(int roomA, LogicalBounds cellA, int roomB, LogicalBounds cellB)
        {
            if (roomA < 0 || roomB < 0)
            {
                return false;
            }

            int[] candidates = roomDoors[roomA];
            for (int i = 0; i < candidates.Length; i++)
            {
                int door = candidates[i];
                if (!IsDoorOpen(door) || RoomBeyond(door, roomA) != roomB)
                {
                    continue;
                }

                DoorRuntime d = doors[door];
                bool alongX = d.Side == WallSide.North || d.Side == WallSide.South;
                int shareMin = alongX ? Math.Max(cellA.MinX, cellB.MinX) : Math.Max(cellA.MinZ, cellB.MinZ);
                int shareMax = alongX ? Math.Min(cellA.MaxX, cellB.MaxX) : Math.Min(cellA.MaxZ, cellB.MaxZ);
                int half = d.Width / 2;
                if (Math.Min(shareMax, d.Centre + half) > Math.Max(shareMin, d.Centre - half))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A random point in a room, at least <paramref name="margin"/> from
        /// its walls (less if the room is small), drawn X first then Z.
        /// </summary>
        public LogicalPosition RandomInteriorPoint(int room, int margin)
        {
            LogicalBounds b = rooms[room];
            margin = Math.Min(margin, Math.Min(b.MaxX - b.MinX, b.MaxZ - b.MinZ) / 2);
            LogicalPosition point = default;
            for (int attempt = 0; attempt < ClearSpotAttempts; attempt++)
            {
                // Redrawn while it lands on or right beside a table (at most a few times).
                int x = context.Random.NextIntInclusive(b.MinX + margin, b.MaxX - margin);
                int z = context.Random.NextIntInclusive(b.MinZ + margin, b.MaxZ - margin);
                point = new LogicalPosition(x, z);
                if (TableAt(point, radius + TableSpotClearance) < 0)
                {
                    break;
                }
            }

            return point;
        }

        // ---------------------------------------------------------------- routes

        /// <summary>
        /// How far it is to walk from one room to another through doors, and
        /// which door to head for first, and which door they would walk in
        /// through at the end. Distance is what it is to walk: from where the
        /// traveller stands to the first door round whatever is in the way,
        /// then door to door across each room the same way. A shut door still
        /// counts as a way through (people expect to open one), but not one
        /// this person has just given up on. False when there is no way at all.
        /// </summary>
        public bool TryFindRoute(int fromRoom, LogicalPosition from, int toRoom, Agent traveller,
            out int firstDoor, out int lastDoor, out long cost)
        {
            return FindRoute(fromRoom, from, toRoom, traveller, false, out firstDoor, out lastDoor, out cost);
        }

        /// <summary>
        /// The same walk, but only through doors this person knows are there
        /// (see <see cref="AgentKnowledge"/>). For somebody who knows the
        /// building it is exactly <see cref="TryFindRoute"/>. Asked wherever a
        /// frightened person plans how to get out, or where to hide: nobody
        /// runs for a door they have never seen.
        /// </summary>
        public bool TryFindKnownRoute(int fromRoom, LogicalPosition from, int toRoom, Agent traveller,
            out int firstDoor, out int lastDoor, out long cost)
        {
            return FindRoute(fromRoom, from, toRoom, traveller, true, out firstDoor, out lastDoor, out cost);
        }

        private bool FindRoute(int fromRoom, LogicalPosition from, int toRoom, Agent traveller, bool knownOnly,
            out int firstDoor, out int lastDoor, out long cost)
        {
            firstDoor = -1;
            lastDoor = -1;
            cost = 0L;
            if (fromRoom < 0 || toRoom < 0)
            {
                return false;
            }

            if (fromRoom == toRoom)
            {
                return true;
            }

            // Dijkstra over the rooms, scanning for the nearest unsettled one:
            // fine for a floor of a few dozen rooms, and each leg is a table
            // look-up, so the search itself is what costs.
            for (int r = 0; r < rooms.Length; r++)
            {
                routeCost[r] = long.MaxValue;
                routeFirstDoor[r] = -1;
                routeEntryDoor[r] = -1;
                routeSettled[r] = false;
            }

            routeCost[fromRoom] = 0L;
            while (true)
            {
                int room = -1;
                long best = long.MaxValue;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (!routeSettled[r] && routeCost[r] < best)
                    {
                        best = routeCost[r];
                        room = r;
                    }
                }

                if (room < 0)
                {
                    return false;
                }

                if (room == toRoom)
                {
                    firstDoor = routeFirstDoor[room];
                    lastDoor = routeEntryDoor[room];
                    cost = routeCost[room];
                    return true;
                }

                routeSettled[room] = true;

                // Where this leg starts: where they stand, or just inside the
                // door they came in by. The straight line from the door's
                // centre is what a leg costs when the squares cannot say.
                LogicalPosition here = routeEntryDoor[room] < 0
                    ? from
                    : DoorApproachPoint(routeEntryDoor[room], room);
                LogicalPosition line = routeEntryDoor[room] < 0 ? from : DoorCentre(routeEntryDoor[room]);
                int[] candidates = roomDoors[room];
                for (int i = 0; i < candidates.Length; i++)
                {
                    int door = candidates[i];
                    int next = RoomBeyond(door, room);
                    if (next < 0 || routeSettled[next] || !CanRouteThrough(door, traveller, knownOnly))
                    {
                        continue;
                    }

                    long leg = WalkFrom(here, door, room);
                    if (leg < 0L)
                    {
                        leg = IntegerMath.Distance(line, DoorCentre(door));
                    }

                    long total = routeCost[room] + leg;
                    if (total >= routeCost[next])
                    {
                        continue;
                    }

                    routeCost[next] = total;
                    routeEntryDoor[next] = door;
                    routeFirstDoor[next] = routeFirstDoor[room] < 0 ? door : routeFirstDoor[room];
                }
            }
        }

        /// <summary>
        /// How far it is to walk from a point to a door, on the side of it
        /// that faces <paramref name="room"/>, going round the furniture; or
        /// -1 when the squares cannot say (the point is off the grid, or on a
        /// square nobody could stand on, or nothing joins it to the door).
        /// </summary>
        private long WalkFrom(LogicalPosition from, int door, int room)
        {
            int cell = navigationGrid.CellAt(from);
            if (cell < 0)
            {
                return -1L;
            }

            int cost = WalkTo(door, room)[cell];
            return cost == FlowField.Unreachable
                ? -1L
                : (long)cost * NavigationGrid.CellSizeMillimetres / FlowField.StraightCost;
        }

        /// <summary>
        /// The walking cost of every square to a door from the side facing
        /// <paramref name="room"/>, worked out the first time it is wanted.
        /// </summary>
        private int[] WalkTo(int door, int room)
        {
            int key = door * 2 + (doors[door].Room == room ? 0 : 1);
            int[] walk = doorWalks[key];
            if (walk != null)
            {
                return walk;
            }

            if (doorWalkScratch == null)
            {
                doorWalkScratch = new FlowField(navigationGrid);
            }

            // Built to the spot just inside the doorway where somebody stands
            // to go through, or a step further in if that square is too tight
            // for a body; with no such square every cost is "no way".
            LogicalPosition spot = DoorApproachPoint(door, room);
            int goal = navigationGrid.CellAt(spot);
            doorWalkScratch.Build(navigationGrid.Fits(goal, radius) ? goal : -1, radius);
            walk = new int[navigationGrid.CellCount];
            doorWalkScratch.CopyCostsTo(walk);
            doorWalks[key] = walk;
            return walk;
        }

        /// <summary>
        /// The spot on the <paramref name="room"/> side of a door where a
        /// route's walk to it ends and the next leg starts: a body's width in
        /// from the wall, or a square or so further in when that square is
        /// too tight for a body. The same spot is the goal of the door's walk
        /// table and the start of the leg beyond it, so the two agree.
        /// </summary>
        private LogicalPosition DoorApproachPoint(int door, int room)
        {
            LogicalPosition first = DoorPointFrom(door, room, 0, -radius);
            for (int step = 0; step < 4; step++)
            {
                LogicalPosition spot = step == 0
                    ? first
                    : DoorPointFrom(door, room, 0, -(radius + step * NavigationGrid.CellSizeMillimetres));
                if (navigationGrid.Fits(navigationGrid.CellAt(spot), radius))
                {
                    return spot;
                }
            }

            return first;
        }

        /// <summary>
        /// Whether the route this person would walk from where they stand to
        /// <paramref name="toRoom"/> goes through this door.
        /// <para>
        /// Asked so that somebody running for a way out does not stop and shut
        /// the door they are about to need. The route search only reports the
        /// first and last door it used, which is all anybody needed until now,
        /// so this runs the same search and then walks back along the chain of
        /// doors it settled on.
        /// </para>
        /// </summary>
        public bool RouteUsesDoor(int fromRoom, LogicalPosition from, int toRoom, Agent traveller, int door,
            bool knownOnly = false)
        {
            if (fromRoom < 0 || toRoom < 0 || fromRoom == toRoom ||
                !FindRoute(fromRoom, from, toRoom, traveller, knownOnly, out _, out _, out _))
            {
                // Already in the room they are making for: no door between here
                // and there, so no door they could shut on themselves.
                return false;
            }

            // Back from the far room to the near one, one entry door at a time.
            // Bounded by the number of rooms, because a shortest route never
            // visits one twice.
            int room = toRoom;
            for (int step = 0; step < rooms.Length && room >= 0 && room != fromRoom; step++)
            {
                int entry = routeEntryDoor[room];
                if (entry < 0)
                {
                    break;
                }

                if (entry == door)
                {
                    return true;
                }

                room = RoomBeyond(entry, room);
            }

            return false;
        }

        /// <summary>
        /// Every door on the walk from one room to another, in the order they
        /// are walked through, added to <paramref name="into"/>. False, adding
        /// nothing, when there is no way at all. Nobody's knowledge or memory
        /// is consulted when <paramref name="traveller"/> is null: that is the
        /// building's own answer, which is what a sign on its wall gives.
        /// </summary>
        public bool RouteDoors(int fromRoom, LogicalPosition from, int toRoom, Agent traveller, List<int> into)
        {
            if (fromRoom < 0 || toRoom < 0 ||
                !FindRoute(fromRoom, from, toRoom, traveller, false, out _, out _, out _))
            {
                return false;
            }

            // Walked back from the far room, one entry door at a time, then
            // turned round. Bounded by the number of rooms, as above.
            int start = into.Count;
            int room = toRoom;
            for (int step = 0; step < rooms.Length && room != fromRoom; step++)
            {
                int entry = routeEntryDoor[room];
                if (entry < 0)
                {
                    break;
                }

                into.Add(entry);
                room = RoomBeyond(entry, room);
            }

            into.Reverse(start, into.Count - start);
            return true;
        }

        /// <summary>
        /// A door someone may plan a route through: open, or shut but not one
        /// they have just given up on. A broken door counts as open. When only
        /// what they know counts, it also has to be a door they know is there.
        /// </summary>
        private bool CanRouteThrough(int door, Agent traveller, bool knownOnly)
        {
            if (knownOnly && traveller != null && !traveller.Knowledge.Knows(door))
            {
                return false;
            }

            return IsDoorOpen(door) || traveller == null || context.Tick >= traveller.Doors.AvoidUntilTick[door];
        }

        // ---------------------------------------------------------------- tables

        public int TableCount => tables.Length;

        public LogicalBounds TableBounds(int table) => tables[table];

        public SimulationId TableId(int table) => tableIds[table];

        /// <summary>
        /// Blasts a hole through the wall nearest <paramref name="where"/> and
        /// fills in the spare slot <paramref name="slot"/> with it. Refuses when
        /// no wall is near enough, the wall is too short, or the hole would run
        /// over a corner or into an opening that is already there.
        /// <para>
        /// The wall is chosen in whole millimetres, and ties go to the lowest room
        /// index and then the wall order, so the same click always blasts the same
        /// wall however the rooms were authored.
        /// </para>
        /// </summary>
        public bool TryPlaceHole(int slot, LogicalPosition where, out LogicalPosition centre)
        {
            centre = default;
            BlastSettings blast = context.Scenario.Blast;
            int bestRoom = -1;
            WallSide bestSide = WallSide.North;
            long bestDistance = long.MaxValue;
            long bestAlong = 0L;

            for (int r = 0; r < rooms.Length; r++)
            {
                LogicalBounds room = rooms[r];
                for (int side = 0; side < 4; side++)
                {
                    var wall = (WallSide)side;
                    bool alongX = wall == WallSide.North || wall == WallSide.South;
                    long line = wall == WallSide.North ? room.MaxZ
                        : wall == WallSide.South ? room.MinZ
                        : wall == WallSide.East ? room.MaxX
                        : room.MinX;
                    long along = alongX ? where.X : where.Z;
                    long across = alongX ? where.Z : where.X;
                    long lowest = alongX ? room.MinX : room.MinZ;
                    long highest = alongX ? room.MaxX : room.MaxZ;
                    if (along < lowest || along > highest)
                    {
                        continue;
                    }

                    long distance = Math.Abs(across - line);
                    if (distance > blast.WallReachMillimetres || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestRoom = r;
                    bestSide = wall;
                    bestAlong = along;
                }
            }

            if (bestRoom < 0)
            {
                return false;
            }

            // The whole gap, plus a body's width of wall at each end, has to fit
            // along that wall.
            LogicalBounds owner = rooms[bestRoom];
            bool ownerAlongX = bestSide == WallSide.North || bestSide == WallSide.South;
            long wallLow = ownerAlongX ? owner.MinX : owner.MinZ;
            long wallHigh = ownerAlongX ? owner.MaxX : owner.MaxZ;
            long half = blast.HoleWidthMillimetres / 2L;
            long margin = half + radius;
            if (wallHigh - wallLow < margin * 2L)
            {
                return false;
            }

            long at = Math.Max(wallLow + margin, Math.Min(wallHigh - margin, bestAlong));
            if (!HoleFits(bestRoom, bestSide, (int)at, blast.HoleWidthMillimetres, blast.ClearanceMillimetres) ||
                WouldStraddle(bestRoom, bestSide, (int)at, blast.HoleWidthMillimetres))
            {
                return false;
            }

            doors[slot].Room = bestRoom;
            doors[slot].Side = bestSide;
            doors[slot].Centre = (int)at;
            doors[slot].Width = blast.HoleWidthMillimetres;
            doors[slot].State = DoorState.Broken;
            doors[slot].Placed = true;
            placedCount++;
            BuildRoomDoors();
            centre = DoorCentre(slot);

            // A hole is a new way through a wall, so the floor either side of
            // it is walkable now. Without telling the squares, routes would go
            // on treating the wall as solid and nobody would ever use it.
            int reach = blast.HoleWidthMillimetres + NavigationGrid.DoorwayReachMillimetres;
            TheBuildingChangedShape(new LogicalBounds(
                centre.X - reach, centre.X + reach, centre.Z - reach, centre.Z + reach));
            return true;
        }

        /// <summary>
        /// Whether a gap here would open half into the next room and half into
        /// solid wall, which the scenario refuses for authored doors and which
        /// would leave a hole that goes nowhere in particular.
        /// </summary>
        private bool WouldStraddle(int room, WallSide side, int at, int width)
        {
            LogicalBounds mine = rooms[room];
            bool alongX = side == WallSide.North || side == WallSide.South;
            int half = width / 2;
            for (int r = 0; r < rooms.Length; r++)
            {
                if (r == room)
                {
                    continue;
                }

                LogicalBounds other = rooms[r];
                bool flush;
                switch (side)
                {
                    case WallSide.North:
                        flush = other.MinZ == mine.MaxZ;
                        break;
                    case WallSide.South:
                        flush = other.MaxZ == mine.MinZ;
                        break;
                    case WallSide.East:
                        flush = other.MinX == mine.MaxX;
                        break;
                    default:
                        flush = other.MaxX == mine.MinX;
                        break;
                }

                if (!flush)
                {
                    continue;
                }

                int otherMin = alongX ? other.MinX : other.MinZ;
                int otherMax = alongX ? other.MaxX : other.MaxZ;
                bool overlapsAtAll = otherMax > at - half && otherMin < at + half;
                bool coversItAll = otherMin <= at - half && otherMax >= at + half;
                if (overlapsAtAll && !coversItAll)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a gap this wide, centred here, keeps clear of every opening
        /// already in the same wall line — whichever room owns it.
        /// </summary>
        private bool HoleFits(int room, WallSide side, int at, int width, int clearance)
        {
            for (int d = 0; d < placedCount; d++)
            {
                if (!SharesWallLine(d, room, side))
                {
                    continue;
                }

                long needed = (doors[d].Width + width) / 2L + clearance;
                if (Math.Abs((long)doors[d].Centre - at) < needed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a door sits in the same wall line as this room's side. Two rooms
        /// share a wall, so a door in the room on the far side of it counts too.
        /// </summary>
        private bool SharesWallLine(int door, int room, WallSide side)
        {
            LogicalBounds mine = rooms[room];
            long myLine = side == WallSide.North ? mine.MaxZ
                : side == WallSide.South ? mine.MinZ
                : side == WallSide.East ? mine.MaxX
                : mine.MinX;
            bool myAlongX = side == WallSide.North || side == WallSide.South;

            LogicalBounds theirs = rooms[doors[door].Room];
            WallSide their = doors[door].Side;
            long theirLine = their == WallSide.North ? theirs.MaxZ
                : their == WallSide.South ? theirs.MinZ
                : their == WallSide.East ? theirs.MaxX
                : theirs.MinX;
            bool theirAlongX = their == WallSide.North || their == WallSide.South;
            return myAlongX == theirAlongX && myLine == theirLine;
        }

        /// <summary>
        /// Which doors touch which room, and what lies beyond each door. The only
        /// two things this class caches per door, so placing a blast hole rebuilds
        /// exactly this and nothing else. Kept in one method because the order
        /// doors appear in per room decides the order behaviours consider them,
        /// which is part of the replay contract.
        /// </summary>
        private void BuildRoomDoors()
        {
            var counts = new int[rooms.Length];
            for (int d = 0; d < placedCount; d++)
            {
                doorNeighbour[d] = FindNeighbour(d);
                counts[doors[d].Room]++;
                if (doorNeighbour[d] >= 0)
                {
                    counts[doorNeighbour[d]]++;
                }
            }

            roomDoors = new int[rooms.Length][];
            for (int r = 0; r < rooms.Length; r++)
            {
                roomDoors[r] = new int[counts[r]];
                counts[r] = 0;
            }

            for (int d = 0; d < placedCount; d++)
            {
                int room = doors[d].Room;
                roomDoors[room][counts[room]++] = d;
                int beyond = doorNeighbour[d];
                if (beyond >= 0)
                {
                    roomDoors[beyond][counts[beyond]++] = d;
                }
            }
        }

        /// <summary>Where the table stands and how it is turned, for the display.</summary>
        public BodyPose TablePose(int table) => tablePoses[table];

        /// <summary>
        /// How far a table has to have shifted before the walkable floor is
        /// worked out again around it. A table nudged a centimetre by a passing
        /// crowd changes nobody's route; one heaved a stride across the room, or
        /// tipped onto its side, changes the shape of the room.
        /// </summary>
        private const int TableMovedMillimetres = 150;

        /// <summary>
        /// The engine moved this table: it covers a different patch of floor
        /// now, and stands or lies differently. Once it has shifted far enough
        /// to matter, the squares people walk on are worked out again over both
        /// the floor it left and the floor it now covers.
        /// </summary>
        public void MoveTable(int table, LogicalBounds footprint, BodyPose pose, bool settled)
        {
            tables[table] = footprint;
            tablePoses[table] = pose;

            // Only once it has come to rest. A crowd leaning on a desk shoves it
            // a millimetre at a time for seconds on end, and working the floor
            // out again on every one of those ticks costs more than everything
            // else in the tick put together.
            if (!settled)
            {
                return;
            }

            LogicalBounds baked = tablesAsBaked[table];
            if (Math.Abs(footprint.MinX - baked.MinX) < TableMovedMillimetres &&
                Math.Abs(footprint.MaxX - baked.MaxX) < TableMovedMillimetres &&
                Math.Abs(footprint.MinZ - baked.MinZ) < TableMovedMillimetres &&
                Math.Abs(footprint.MaxZ - baked.MaxZ) < TableMovedMillimetres)
            {
                return;
            }

            tablesAsBaked[table] = footprint;
            TheBuildingChangedShape(new LogicalBounds(
                Math.Min(baked.MinX, footprint.MinX), Math.Max(baked.MaxX, footprint.MaxX),
                Math.Min(baked.MinZ, footprint.MinZ), Math.Max(baked.MaxZ, footprint.MaxZ)));
        }

        /// <summary>
        /// The walkable floor has changed, so the squares covering that patch
        /// are worked out again and every route worked out so far is thrown
        /// away. Routes are cheap to work out again and wrong ones send people
        /// into walls that are no longer there, or round furniture that is now
        /// wreckage.
        /// </summary>
        private void TheBuildingChangedShape(LogicalBounds where)
        {
            navigationGrid.Rebuild(where, rooms, tables, BuildWalls(), BuildDoorways());
            navigation.Forget();
            Array.Clear(doorWalks, 0, doorWalks.Length);
        }

        /// <summary>
        /// The openings that are actually in the world. Spare slots for blast
        /// holes live past this, so every loop over doors skips them without
        /// having to know they exist.
        /// </summary>
        public int DoorCount => placedCount;

        /// <summary>Open, or broken down: either way there is a gap to walk through.</summary>
        public bool IsDoorOpen(int door) => doors[door].State == DoorState.Open || doors[door].State == DoorState.Broken;

        /// <summary>
        /// The lowest-index table a body of <paramref name="bodyRadius"/> at
        /// <paramref name="position"/> would overlap, or -1. A table is
        /// treated as its rectangle grown by the radius on every side, so
        /// standing exactly at that edge is allowed.
        /// </summary>
        public int TableAt(LogicalPosition position, int bodyRadius)
        {
            for (int t = 0; t < tables.Length; t++)
            {
                LogicalBounds b = tables[t];
                if (position.X > b.MinX - bodyRadius && position.X < b.MaxX + bodyRadius &&
                    position.Z > b.MinZ - bodyRadius && position.Z < b.MaxZ + bodyRadius)
                {
                    return t;
                }
            }

            return -1;
        }

        /// <summary>True when a person walking straight from one point to the other would run into a table.</summary>
        public bool RouteCrossesTable(LogicalPosition from, LogicalPosition to)
        {
            for (int t = 0; t < tables.Length; t++)
            {
                if (IntegerMath.SweptCircleOverlapsBounds(from, to, radius, tables[t]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves <paramref name="destination"/> out of any table it overlaps,
        /// onto the edge facing <paramref name="current"/>, so a body slides
        /// along a table as it would along a wall. <paramref name="hitX"/> is
        /// true when it was stopped along X, <paramref name="hitZ"/> along Z.
        /// </summary>
        public LogicalPosition PushOutOfTables(LogicalPosition current, LogicalPosition destination, int bodyRadius,
            out bool hitX, out bool hitZ)
        {
            hitX = false;
            hitZ = false;
            for (int t = 0; t < tables.Length; t++)
            {
                LogicalBounds b = tables[t];
                int minX = b.MinX - bodyRadius;
                int maxX = b.MaxX + bodyRadius;
                int minZ = b.MinZ - bodyRadius;
                int maxZ = b.MaxZ + bodyRadius;
                if (!(destination.X > minX && destination.X < maxX && destination.Z > minZ && destination.Z < maxZ))
                {
                    continue;
                }

                // Stop on the side the body came from: the X edge if it was
                // already beside the table, otherwise the Z edge. A body that
                // was somehow inside leaves by the nearest edge.
                bool besideX = current.X <= minX || current.X >= maxX;
                bool besideZ = current.Z <= minZ || current.Z >= maxZ;
                if (!besideX && !besideZ)
                {
                    int left = destination.X - minX;
                    int right = maxX - destination.X;
                    int below = destination.Z - minZ;
                    int above = maxZ - destination.Z;
                    besideX = Math.Min(left, right) <= Math.Min(below, above);
                    current = besideX
                        ? new LogicalPosition(left <= right ? minX : maxX, destination.Z)
                        : new LogicalPosition(destination.X, below <= above ? minZ : maxZ);
                }

                if (besideX)
                {
                    destination = new LogicalPosition(current.X <= minX ? minX : maxX, destination.Z);
                    hitX = true;
                }
                else
                {
                    destination = new LogicalPosition(destination.X, current.Z <= minZ ? minZ : maxZ);
                    hitZ = true;
                }
            }

            return destination;
        }

        // ---------------------------------------------------------------- doors

        /// <summary>
        /// A point near a door: <paramref name="along"/> millimetres along the
        /// wall from the door's centre, and <paramref name="outward"/>
        /// millimetres out of the door's own room (negative is inside it).
        /// </summary>
        public LogicalPosition DoorPoint(int door, int along, int outward)
        {
            DoorRuntime d = doors[door];
            LogicalBounds room = rooms[d.Room];
            switch (d.Side)
            {
                case WallSide.North:
                    return new LogicalPosition(d.Centre + along, room.MaxZ + outward);
                case WallSide.South:
                    return new LogicalPosition(d.Centre + along, room.MinZ - outward);
                case WallSide.East:
                    return new LogicalPosition(room.MaxX + outward, d.Centre + along);
                default:
                    return new LogicalPosition(room.MinX - outward, d.Centre + along);
            }
        }

        /// <summary>
        /// A point near a door as seen by someone in <paramref name="room"/>:
        /// <paramref name="outward"/> is away from that room, so the same call
        /// works from either side of the door.
        /// </summary>
        public LogicalPosition DoorPointFrom(int door, int room, int along, int outward)
        {
            return DoorPoint(door, along, doors[door].Room == room || room < 0 ? outward : -outward);
        }

        public LogicalPosition DoorCentre(int door) => DoorPoint(door, 0, 0);

        /// <summary>
        /// A person here would be in the way of the door swinging shut: in
        /// the gap itself, from either side, or (for a door leading outside)
        /// anywhere in the doorway beyond it.
        /// </summary>
        public bool IsInDoorway(int door, LogicalPosition position)
        {
            long along = Math.Abs(AlongOffset(door, position));
            long beyond = BeyondDistance(door, position);
            long clearance = radius + 100L;
            if (along >= doors[door].Width / 2 + (long)radius)
            {
                return false;
            }

            return Math.Abs(beyond) <= clearance ||
                   (doorNeighbour[door] < 0 && beyond > 0L && beyond <= exits.DoorwayDepthMillimetres);
        }

        /// <summary>
        /// A thing of this size resting here would jam the door: in front of the
        /// gap, and close enough to the wall line on either side to be in the
        /// leaf's way. Symmetric about the wall, because a bag wedged against a
        /// door stops it whichever side it is on.
        /// </summary>
        /// <summary>
        /// The patch of floor holding every point <see cref="IsObjectInDoorway"/>
        /// could say yes to, for a thing of this radius. Asking the index for
        /// this area rather than reading every object in the building is what
        /// keeps "is anything wedged in this doorway?" cheap, and because the
        /// area is exactly the one the test accepts, the answer cannot change.
        /// </summary>
        public LogicalBounds DoorwaySearchArea(int door, int objectRadius, int gap)
        {
            DoorRuntime d = doors[door];
            LogicalPosition centre = DoorCentre(door);
            int along = d.Width / 2 + objectRadius;
            int beyond = objectRadius + gap;
            bool alongX = d.Side == WallSide.North || d.Side == WallSide.South;
            int halfX = alongX ? along : beyond;
            int halfZ = alongX ? beyond : along;
            return new LogicalBounds(centre.X - halfX, centre.X + halfX, centre.Z - halfZ, centre.Z + halfZ);
        }

        public bool IsObjectInDoorway(int door, LogicalPosition where, int objectRadius, int gap)
        {
            long along = Math.Abs(AlongOffset(door, where));
            if (along >= doors[door].Width / 2 + (long)objectRadius)
            {
                return false;
            }

            return Math.Abs(BeyondDistance(door, where)) <= objectRadius + (long)gap;
        }

        /// <summary>How far past the door's wall a point is, out of the door's own room (negative inside it).</summary>
        private long BeyondDistance(int door, LogicalPosition position)
        {
            DoorRuntime d = doors[door];
            LogicalBounds room = rooms[d.Room];
            switch (d.Side)
            {
                case WallSide.North:
                    return (long)position.Z - room.MaxZ;
                case WallSide.South:
                    return (long)room.MinZ - position.Z;
                case WallSide.East:
                    return (long)position.X - room.MaxX;
                default:
                    return (long)room.MinX - position.X;
            }
        }

        /// <summary>How far past a door's wall a point is, away from <paramref name="room"/>.</summary>
        public long BeyondDistanceFrom(int door, int room, LogicalPosition position)
        {
            long beyond = BeyondDistance(door, position);
            return doors[door].Room == room || room < 0 ? beyond : -beyond;
        }

        /// <summary>
        /// The heading that runs along a door's wall, toward the positive side
        /// when <paramref name="side"/> is 1 and the other way when it is -1.
        /// Used to heave an obstruction out of a doorway sideways.
        /// </summary>
        public int AlongWallHeading(int door, int side)
        {
            bool alongX = doors[door].Side == WallSide.North || doors[door].Side == WallSide.South;
            int heading = alongX ? 90 : 0;
            return IntegerMath.NormalizeDegrees(side >= 0 ? heading : heading + 180);
        }

        /// <summary>How far along the wall a point is from the door's centre.</summary>
        public long AlongOffset(int door, LogicalPosition position)
        {
            DoorRuntime d = doors[door];
            return d.Side == WallSide.North || d.Side == WallSide.South
                ? (long)position.X - d.Centre
                : (long)position.Z - d.Centre;
        }

        /// <summary>The centre is within the door's width, measured along its wall.</summary>
        private bool IsInFrontOf(int door, LogicalPosition position)
        {
            return Math.Abs(AlongOffset(door, position)) <= doors[door].Width / 2;
        }

        /// <summary>Close enough to the door's centre line that the whole body fits through the gap.</summary>
        public bool IsLinedUpToPassThrough(int door, LogicalPosition position)
        {
            return Math.Abs(AlongOffset(door, position)) <= doors[door].Width / 2 - radius + LinedUpTolerance;
        }

        // ---------------------------------------------------------------- people

        /// <summary>
        /// Keeps a spot on the floor of the room somebody is standing in (or
        /// the nearest room, if they are in a doorway) and off its tables:
        /// where a helper drags a casualty to must be floor, not the wall
        /// behind them. A spot already on some room's floor stays where it is.
        /// </summary>
        public LogicalPosition ClampIntoRoom(LogicalPosition current, LogicalPosition position)
        {
            if (RoomAt(position) >= 0 && TableAt(position, radius) < 0)
            {
                return position;
            }

            int room = RoomAt(current);
            if (room < 0)
            {
                room = RoomStoodIn(current);
            }

            // Keep to the room, sliding along any table in the way as along a wall.
            LogicalPosition slid = PushOutOfTables(current, Clamp(position, rooms[room], radius), radius, out _, out _);
            return Clamp(slid, rooms[room], radius);
        }

        /// <summary>
        /// Adds a push away from each wall of the room the person is in,
        /// within <paramref name="range"/>, weighted as a percentage of a
        /// unit goal vector, and away from each table's edge, weighted by
        /// <paramref name="tablePercent"/>. The wall holding the door they are
        /// lined up with does not push, so they can walk through it. Nothing
        /// pushes while they are in a doorway.
        /// </summary>
        public void AddWallRepulsion(
            LogicalPosition position,
            int exitDoor,
            long range,
            int percent,
            int tablePercent,
            ref long steerX,
            ref long steerZ)
        {
            if (range <= 0L || (percent <= 0 && tablePercent <= 0))
            {
                return;
            }

            int room = RoomAt(position);
            if (room < 0)
            {
                return;
            }

            // Away from the nearest point of each nearby table. Tables used to
            // push exactly as hard as walls, so a running crowd never touched
            // one; now how hard is the caller's to say.
            for (int t = 0; tablePercent > 0 && t < tables.Length; t++)
            {
                LogicalPosition closest = tables[t].ClosestPoint(position);
                long dx = (long)position.X - closest.X;
                long dz = (long)position.Z - closest.Z;
                long distance = IntegerMath.Sqrt(dx * dx + dz * dz);
                if (distance == 0L)
                {
                    continue;
                }

                long push = WallPush(distance - radius, range, tablePercent);
                steerX += dx * push / distance;
                steerZ += dz * push / distance;
            }

            if (percent <= 0)
            {
                return;
            }

            LogicalBounds b = rooms[room];
            if (!IsLinedUpWithExit(exitDoor, room, WallSide.West, position))
            {
                steerX += WallPush(position.X - radius - b.MinX, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, room, WallSide.East, position))
            {
                steerX -= WallPush(b.MaxX - radius - position.X, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, room, WallSide.South, position))
            {
                steerZ += WallPush(position.Z - radius - b.MinZ, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, room, WallSide.North, position))
            {
                steerZ -= WallPush(b.MaxZ - radius - position.Z, range, percent);
            }
        }

        /// <summary>The wall of <paramref name="room"/> this door sits in, seen from inside that room.</summary>
        public WallSide WallSideFrom(int door, int room)
        {
            WallSide side = doors[door].Side;
            if (doors[door].Room == room)
            {
                return side;
            }

            switch (side)
            {
                case WallSide.North:
                    return WallSide.South;
                case WallSide.South:
                    return WallSide.North;
                case WallSide.East:
                    return WallSide.West;
                default:
                    return WallSide.East;
            }
        }

        /// <summary>
        /// Standing in front of a doorway they may use, in this wall. The wall
        /// then stops pushing them away from it, or they would slide along it
        /// rather than walk through the gap in it.
        ///
        /// Somebody on an errand may use any open doorway, so for them the
        /// question is whether any doorway in this wall is one they are lined
        /// up with.
        /// </summary>
        private bool IsLinedUpWithExit(int exitDoor, int room, WallSide side, LogicalPosition position)
        {
            if (exitDoor == AgentDoorMemory.AnyDoorway)
            {
                int[] candidates = roomDoors[room];
                for (int i = 0; i < candidates.Length; i++)
                {
                    int door = candidates[i];
                    if (IsDoorOpen(door) && WallSideFrom(door, room) == side && IsInFrontOf(door, position))
                    {
                        return true;
                    }
                }

                return false;
            }

            return exitDoor >= 0 && DoorTouchesRoom(exitDoor, room) &&
                   WallSideFrom(exitDoor, room) == side && IsInFrontOf(exitDoor, position);
        }

        private static long WallPush(long gap, long range, int percent)
        {
            if (gap >= range)
            {
                return 0L;
            }

            return IntegerMath.TrigScale * (range - Math.Max(0L, gap)) / range * percent / 100L;
        }

        /// <summary>
        /// The door out of the building this person has walked far enough
        /// through to count as escaped, or -1. Only someone out of every room
        /// can escape; a door between two rooms is never a way out.
        /// </summary>
        public int EscapedThrough(LogicalPosition position)
        {
            if (RoomAt(position) >= 0)
            {
                return -1;
            }

            for (int d = 0; d < placedCount; d++)
            {
                if (IsDoorOpen(d) && doorNeighbour[d] < 0 &&
                    BeyondDistance(d, position) >= exits.EscapeDepthMillimetres &&
                    IsInFrontOf(d, position))
                {
                    return d;
                }
            }

            return -1;
        }

        private static LogicalPosition Clamp(LogicalPosition position, LogicalBounds bounds, int radius)
        {
            return new LogicalPosition(
                Math.Max(bounds.MinX + radius, Math.Min(bounds.MaxX - radius, position.X)),
                Math.Max(bounds.MinZ + radius, Math.Min(bounds.MaxZ - radius, position.Z)));
        }
    }
}

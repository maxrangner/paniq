using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The shape of the world, and the only code that knows it: one
    /// rectangular main room with doors set into its walls and tables
    /// standing on its floor, and small side rooms behind some doors. A door
    /// with no side room leads outside. Each open door adds a walkable strip
    /// through the wall.
    /// A table is a solid rectangle: bodies slide along its edges as they
    /// would along a wall. Every question about where a body may
    /// be (walking, steering off walls, leaving through a door, a box hitting
    /// a wall) is answered here, so a later world with inner walls or several
    /// rooms changes this class rather than every system.
    /// </summary>
    internal sealed class WorldGeometry
    {
        /// <summary>How close to a door's centre line a runner must be to head out through it, beyond a full fit.</summary>
        private const int LinedUpTolerance = 50;

        /// <summary>Random spots are redrawn this many times at most to keep them clear of tables.</summary>
        private const int ClearSpotAttempts = 8;

        /// <summary>Random spots stay at least this far clear of a table's edge, beyond a body's radius.</summary>
        private const int TableSpotClearance = 300;

        private readonly SimulationContext context;
        private readonly DoorRuntime[] doors;
        private readonly LogicalBounds room;
        private readonly int radius;
        private readonly ExitSettings exits;
        private readonly LogicalBounds[] tables;
        private readonly SimulationId[] tableIds;
        private readonly LogicalBounds[] sideRooms;
        private readonly int[] sideRoomDoor;

        /// <summary>Per door: the side room behind it, or -1 when it leads outside.</summary>
        private readonly int[] doorSideRoom;

        public WorldGeometry(SimulationContext context, DoorRuntime[] doors)
        {
            this.context = context;
            this.doors = doors;
            room = context.Scenario.World.RoomBounds;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;
            exits = context.Scenario.Exits;

            var definitions = (FireReactionTableDefinition[])context.Scenario.Tables.Clone();
            Array.Sort(definitions, (left, right) => left.TableId.CompareTo(right.TableId));
            tables = new LogicalBounds[definitions.Length];
            tableIds = new SimulationId[definitions.Length];
            for (int i = 0; i < tables.Length; i++)
            {
                tables[i] = definitions[i].Bounds;
                tableIds[i] = definitions[i].TableId;
            }

            var sides = (FireReactionSideRoomDefinition[])context.Scenario.SideRooms.Clone();
            Array.Sort(sides, (left, right) => left.RoomId.CompareTo(right.RoomId));
            sideRooms = new LogicalBounds[sides.Length];
            sideRoomDoor = new int[sides.Length];
            doorSideRoom = new int[doors.Length];
            for (int d = 0; d < doors.Length; d++)
            {
                doorSideRoom[d] = -1;
            }

            for (int s = 0; s < sides.Length; s++)
            {
                sideRooms[s] = sides[s].Bounds;
                sideRoomDoor[s] = Array.FindIndex(doors, d => d.Id == sides[s].DoorId);
                doorSideRoom[sideRoomDoor[s]] = s;
            }

            int minX = room.MinX, maxX = room.MaxX, minZ = room.MinZ, maxZ = room.MaxZ;
            foreach (LogicalBounds side in sideRooms)
            {
                minX = Math.Min(minX, side.MinX);
                maxX = Math.Max(maxX, side.MaxX);
                minZ = Math.Min(minZ, side.MinZ);
                maxZ = Math.Max(maxZ, side.MaxZ);
            }

            FireArea = new LogicalBounds(minX, maxX, minZ, maxZ);
        }

        // ---------------------------------------------------------------- rooms

        /// <summary>The rectangle around every room; the fire grid covers exactly this.</summary>
        public LogicalBounds FireArea { get; }

        public int SideRoomCount => sideRooms.Length;

        public LogicalBounds SideRoomBounds(int sideRoom) => sideRooms[sideRoom];

        /// <summary>The door into a side room.</summary>
        public int SideRoomDoor(int sideRoom) => sideRoomDoor[sideRoom];

        /// <summary>The side room behind a door, or -1 when it leads outside.</summary>
        public int DoorSideRoom(int door) => doorSideRoom[door];

        /// <summary>Where people shelter in a side room: on the door's centre line, 0.6 m short of the far wall.</summary>
        public LogicalPosition SideRoomBackPoint(int sideRoom)
        {
            int door = sideRoomDoor[sideRoom];
            LogicalBounds b = sideRooms[sideRoom];
            int depth;
            switch (doors[door].Side)
            {
                case WallSide.North:
                    depth = b.MaxZ - room.MaxZ;
                    break;
                case WallSide.South:
                    depth = room.MinZ - b.MinZ;
                    break;
                case WallSide.East:
                    depth = b.MaxX - room.MaxX;
                    break;
                default:
                    depth = room.MinX - b.MinX;
                    break;
            }

            return DoorPoint(door, 0, Math.Max(radius, depth - 600));
        }

        /// <summary>The side room a person's whole footprint is inside, or -1.</summary>
        public int SideRoomAt(LogicalPosition position)
        {
            for (int s = 0; s < sideRooms.Length; s++)
            {
                if (sideRooms[s].ContainsCircle(position, radius))
                {
                    return s;
                }
            }

            return -1;
        }

        /// <summary>
        /// Which room a point is in: 0 for the main room, 1 + n for side room
        /// n, -1 for neither (outside, or exactly on a wall line).
        /// </summary>
        public int RoomAtPoint(LogicalPosition point)
        {
            if (point.X > room.MinX && point.X < room.MaxX && point.Z > room.MinZ && point.Z < room.MaxZ)
            {
                return 0;
            }

            for (int s = 0; s < sideRooms.Length; s++)
            {
                LogicalBounds b = sideRooms[s];
                if (point.X > b.MinX && point.X < b.MaxX && point.Z > b.MinZ && point.Z < b.MaxZ)
                {
                    return s + 1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Two rooms (as numbered by <see cref="RoomAtPoint"/>) can see and
        /// hear each other freely: the same room, or the main room and a side
        /// room whose door is open. Anything not in a room counts as connected.
        /// </summary>
        public bool RoomsOpenToEachOther(int roomA, int roomB)
        {
            if (roomA < 0 || roomB < 0 || roomA == roomB)
            {
                return true;
            }

            int side = roomA == 0 ? roomB - 1 : roomB == 0 ? roomA - 1 : -1;
            return side >= 0 && IsDoorOpen(sideRoomDoor[side]);
        }

        /// <summary>
        /// Whether fire may jump between two neighbouring grid cells in
        /// different rooms: only through the open door between them, where
        /// the edge the cells share lies across the door gap.
        /// </summary>
        public bool FireCanCross(int roomA, LogicalBounds cellA, int roomB, LogicalBounds cellB)
        {
            int side = roomA == 0 ? roomB - 1 : roomB == 0 ? roomA - 1 : -1;
            if (side < 0)
            {
                return false;
            }

            int door = sideRoomDoor[side];
            if (!IsDoorOpen(door))
            {
                return false;
            }

            DoorRuntime d = doors[door];
            bool alongX = d.Side == WallSide.North || d.Side == WallSide.South;
            int shareMin = alongX ? Math.Max(cellA.MinX, cellB.MinX) : Math.Max(cellA.MinZ, cellB.MinZ);
            int shareMax = alongX ? Math.Min(cellA.MaxX, cellB.MaxX) : Math.Min(cellA.MaxZ, cellB.MaxZ);
            int half = d.Width / 2;
            return Math.Min(shareMax, d.Centre + half) > Math.Max(shareMin, d.Centre - half);
        }

        public int TableCount => tables.Length;

        public LogicalBounds TableBounds(int table) => tables[table];

        public SimulationId TableId(int table) => tableIds[table];

        /// <summary>The main room's floor.</summary>
        public LogicalBounds Floor => room;

        public int DoorCount => doors.Length;

        /// <summary>Open, or broken down: either way there is a gap to walk through.</summary>
        public bool IsDoorOpen(int door) => doors[door].State == DoorState.Open || doors[door].State == DoorState.Broken;

        /// <summary>A person's whole footprint is inside the room (not in a doorway or outside).</summary>
        public bool IsInsideRoom(LogicalPosition position)
        {
            return room.ContainsCircle(position, radius);
        }

        /// <summary>
        /// A random point at least <paramref name="margin"/> from every wall
        /// (less if the room is too small), drawn X first then Z.
        /// </summary>
        public LogicalPosition RandomInteriorPoint(int margin)
        {
            margin = Math.Min(margin, Math.Min(room.MaxX - room.MinX, room.MaxZ - room.MinZ) / 2);
            LogicalPosition point = default;
            for (int attempt = 0; attempt < ClearSpotAttempts; attempt++)
            {
                // Redrawn while it lands on or right beside a table (at most a few times).
                int x = context.Random.NextIntInclusive(room.MinX + margin, room.MaxX - margin);
                int z = context.Random.NextIntInclusive(room.MinZ + margin, room.MaxZ - margin);
                point = new LogicalPosition(x, z);
                if (TableAt(point, radius + TableSpotClearance) < 0)
                {
                    break;
                }
            }

            return point;
        }

        // ---------------------------------------------------------------- tables

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
        /// wall from the door's centre and <paramref name="outward"/>
        /// millimetres out of the room (negative is inside).
        /// </summary>
        public LogicalPosition DoorPoint(int door, int along, int outward)
        {
            DoorRuntime d = doors[door];
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

        public LogicalPosition DoorCentre(int door) => DoorPoint(door, 0, 0);

        /// <summary>How far past the door's wall a point is (negative inside the room).</summary>
        private long OutsideDistance(int door, LogicalPosition position)
        {
            switch (doors[door].Side)
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

        /// <summary>How far along the wall a point is from the door's centre.</summary>
        private long AlongOffset(int door, LogicalPosition position)
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

        /// <summary>The walkable strip through an open door: from just inside the wall to the end of the doorway outside.</summary>
        private LogicalBounds DoorwayStrip(int door)
        {
            int width = doors[door].Width;
            LogicalPosition inner = DoorPoint(door, -width / 2, -exits.DoorwayInsetMillimetres);
            LogicalPosition outer = DoorPoint(door, width / 2, exits.DoorwayDepthMillimetres);
            return new LogicalBounds(
                Math.Min(inner.X, outer.X), Math.Max(inner.X, outer.X),
                Math.Min(inner.Z, outer.Z), Math.Max(inner.Z, outer.Z));
        }

        /// <summary>
        /// Only someone heading for this door, or already out of the room,
        /// may step into its doorway. Calm people treat every door as wall.
        /// </summary>
        private bool CanUseDoorway(int door, LogicalPosition current, int exitDoor)
        {
            return IsDoorOpen(door) && (exitDoor == door || !IsInsideRoom(current));
        }

        // ---------------------------------------------------------------- people

        /// <summary>
        /// A person standing at <paramref name="current"/> and heading for
        /// <paramref name="exitDoor"/> (or -1) may have their whole footprint
        /// at <paramref name="position"/>: inside the room or in a doorway they may use.
        /// </summary>
        public bool IsWalkable(LogicalPosition current, int exitDoor, LogicalPosition position)
        {
            if (IsInsideRoom(position))
            {
                return TableAt(position, radius) < 0;
            }

            if (SideRoomAt(position) >= 0)
            {
                return true;
            }

            for (int d = 0; d < doors.Length; d++)
            {
                if (CanUseDoorway(d, current, exitDoor) && DoorwayStrip(d).ContainsCircle(position, radius))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Keeps a step inside the space the person may walk in: if the
        /// destination is already fine it stays; otherwise it is clamped into
        /// the room, or into the doorway the person is standing in.
        /// </summary>
        public LogicalPosition ClampIntoWalkable(LogicalPosition current, int exitDoor, LogicalPosition position)
        {
            if (IsWalkable(current, exitDoor, position))
            {
                return position;
            }

            if (IsInsideRoom(current))
            {
                // Keep to the room, sliding along any table in the way as along a wall.
                LogicalPosition slid = PushOutOfTables(current, Clamp(position, room, radius), radius, out _, out _);
                return Clamp(slid, room, radius);
            }

            int side = SideRoomAt(current);
            if (side >= 0)
            {
                // Inside a side room: keep to its walls.
                return Clamp(position, sideRooms[side], radius);
            }

            // Outside the room (in a doorway): keep to the doorway strip.
            for (int d = 0; d < doors.Length; d++)
            {
                LogicalBounds strip = DoorwayStrip(d);
                if (CanUseDoorway(d, current, exitDoor) && strip.ContainsCircle(current, radius))
                {
                    return Clamp(position, strip, radius);
                }
            }

            return Clamp(position, room, radius);
        }

        /// <summary>True when a person's swept footprint clips the frame of any open door.</summary>
        public bool ClipsDoorFrame(LogicalPosition start, LogicalPosition destination)
        {
            long radiusSquared = (long)radius * radius;
            for (int d = 0; d < doors.Length; d++)
            {
                if (!IsDoorOpen(d))
                {
                    continue;
                }

                int half = doors[d].Width / 2;
                if (IntegerMath.SegmentPassesWithin(start, destination, DoorPoint(d, -half, 0), radiusSquared) ||
                    IntegerMath.SegmentPassesWithin(start, destination, DoorPoint(d, half, 0), radiusSquared))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a push away from each wall within <paramref name="range"/>,
        /// weighted as a percentage of a unit goal vector. The wall of the
        /// door the person is lined up with does not push, so they can walk
        /// through it. Nothing pushes once they are out of the room.
        /// </summary>
        public void AddWallRepulsion(
            LogicalPosition position,
            int exitDoor,
            long range,
            int percent,
            ref long steerX,
            ref long steerZ)
        {
            if (range <= 0L || percent <= 0)
            {
                return;
            }

            int side = SideRoomAt(position);
            if (side >= 0)
            {
                // Off a side room's walls, except the one with the door when heading out through it.
                LogicalBounds b = sideRooms[side];
                WallSide doorWall = doors[sideRoomDoor[side]].Side;
                bool leaving = exitDoor == sideRoomDoor[side] && IsInFrontOf(exitDoor, position);
                if (!(leaving && doorWall == WallSide.East))
                {
                    steerX += WallPush(position.X - radius - b.MinX, range, percent);
                }

                if (!(leaving && doorWall == WallSide.West))
                {
                    steerX -= WallPush(b.MaxX - radius - position.X, range, percent);
                }

                if (!(leaving && doorWall == WallSide.North))
                {
                    steerZ += WallPush(position.Z - radius - b.MinZ, range, percent);
                }

                if (!(leaving && doorWall == WallSide.South))
                {
                    steerZ -= WallPush(b.MaxZ - radius - position.Z, range, percent);
                }

                return;
            }

            if (!IsInsideRoom(position))
            {
                return;
            }

            // Away from the nearest point of each nearby table, like a wall.
            for (int t = 0; t < tables.Length; t++)
            {
                LogicalPosition closest = tables[t].ClosestPoint(position);
                long dx = (long)position.X - closest.X;
                long dz = (long)position.Z - closest.Z;
                long distance = IntegerMath.Sqrt(dx * dx + dz * dz);
                if (distance == 0L)
                {
                    continue;
                }

                long push = WallPush(distance - radius, range, percent);
                steerX += dx * push / distance;
                steerZ += dz * push / distance;
            }

            if (!IsLinedUpWithExit(exitDoor, WallSide.West, position))
            {
                steerX += WallPush(position.X - radius - room.MinX, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, WallSide.East, position))
            {
                steerX -= WallPush(room.MaxX - radius - position.X, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, WallSide.South, position))
            {
                steerZ += WallPush(position.Z - radius - room.MinZ, range, percent);
            }

            if (!IsLinedUpWithExit(exitDoor, WallSide.North, position))
            {
                steerZ -= WallPush(room.MaxZ - radius - position.Z, range, percent);
            }
        }

        private bool IsLinedUpWithExit(int exitDoor, WallSide side, LogicalPosition position)
        {
            return exitDoor >= 0 && doors[exitDoor].Side == side && IsInFrontOf(exitDoor, position);
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
        /// The open door this person has walked far enough out through to
        /// count as escaped, or -1. Only someone outside the room can escape.
        /// </summary>
        public int EscapedThrough(LogicalPosition position)
        {
            if (IsInsideRoom(position))
            {
                return -1;
            }

            for (int d = 0; d < doors.Length; d++)
            {
                // A door into a side room is shelter, not a way out.
                if (IsDoorOpen(d) && doorSideRoom[d] < 0 &&
                    OutsideDistance(d, position) >= exits.EscapeDepthMillimetres &&
                    IsInFrontOf(d, position))
                {
                    return d;
                }
            }

            return -1;
        }

        // ---------------------------------------------------------------- objects

        /// <summary>
        /// Keeps an object's next position (in <paramref name="scale"/> units
        /// per millimetre) inside the room walls and out of the tables, and
        /// says along which axes it hit something. Objects never use doorways.
        /// </summary>
        public void KeepObjectInRoom(int objectRadius, long scale, long fromX, long fromZ, ref long nextX, ref long nextZ,
            out bool hitX, out bool hitZ)
        {
            long minX = (long)(room.MinX + objectRadius) * scale;
            long maxX = (long)(room.MaxX - objectRadius) * scale;
            long minZ = (long)(room.MinZ + objectRadius) * scale;
            long maxZ = (long)(room.MaxZ - objectRadius) * scale;
            hitX = nextX < minX || nextX > maxX;
            hitZ = nextZ < minZ || nextZ > maxZ;
            if (hitX)
            {
                nextX = Math.Max(minX, Math.Min(maxX, nextX));
            }

            if (hitZ)
            {
                nextZ = Math.Max(minZ, Math.Min(maxZ, nextZ));
            }

            if (tables.Length == 0)
            {
                return;
            }

            var from = new LogicalPosition((int)FloorDivide(fromX, scale), (int)FloorDivide(fromZ, scale));
            var next = new LogicalPosition((int)FloorDivide(nextX, scale), (int)FloorDivide(nextZ, scale));
            LogicalPosition pushed = PushOutOfTables(from, next, objectRadius, out bool tableX, out bool tableZ);
            if (tableX)
            {
                nextX = (long)pushed.X * scale;
                hitX = true;
            }

            if (tableZ)
            {
                nextZ = (long)pushed.Z * scale;
                hitZ = true;
            }
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && (value < 0L) != (divisor < 0L) ? quotient - 1L : quotient;
        }

        private static LogicalPosition Clamp(LogicalPosition position, LogicalBounds bounds, int radius)
        {
            return new LogicalPosition(
                Math.Max(bounds.MinX + radius, Math.Min(bounds.MaxX - radius, position.X)),
                Math.Max(bounds.MinZ + radius, Math.Min(bounds.MaxZ - radius, position.Z)));
        }
    }
}

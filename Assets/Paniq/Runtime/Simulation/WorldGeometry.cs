using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The shape of the world, and the only code that knows it: one
    /// rectangular room with doors set into its walls and tables standing
    /// on its floor. Each open door adds a walkable strip through the wall.
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
            for (int i = 0; i < tables.Length; i++)
            {
                tables[i] = definitions[i].Bounds;
            }
        }

        public int TableCount => tables.Length;

        public LogicalBounds TableBounds(int table) => tables[table];

        /// <summary>The floor area; the fire grid covers exactly this.</summary>
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
            if (range <= 0L || percent <= 0 || !IsInsideRoom(position))
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
                if (IsDoorOpen(d) &&
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

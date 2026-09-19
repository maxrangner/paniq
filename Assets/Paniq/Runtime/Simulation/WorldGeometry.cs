using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The shape of the world, and the only code that knows it: one
    /// rectangular room with doors set into its walls. Each open door adds a
    /// walkable strip through the wall. Every question about where a body may
    /// be (walking, steering off walls, leaving through a door, a box hitting
    /// a wall) is answered here, so a later world with inner walls or several
    /// rooms changes this class rather than every system.
    /// </summary>
    internal sealed class WorldGeometry
    {
        /// <summary>How close to a door's centre line a runner must be to head out through it, beyond a full fit.</summary>
        private const int LinedUpTolerance = 50;

        private readonly SimulationContext context;
        private readonly DoorRuntime[] doors;
        private readonly LogicalBounds room;
        private readonly int radius;
        private readonly ExitSettings exits;

        public WorldGeometry(SimulationContext context, DoorRuntime[] doors)
        {
            this.context = context;
            this.doors = doors;
            room = context.Scenario.World.RoomBounds;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;
            exits = context.Scenario.Exits;
        }

        /// <summary>The floor area; the fire grid covers exactly this.</summary>
        public LogicalBounds Floor => room;

        public int DoorCount => doors.Length;

        public bool IsDoorOpen(int door) => doors[door].State == DoorState.Open;

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
            int x = context.Random.NextIntInclusive(room.MinX + margin, room.MaxX - margin);
            int z = context.Random.NextIntInclusive(room.MinZ + margin, room.MaxZ - margin);
            return new LogicalPosition(x, z);
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
            return doors[door].State == DoorState.Open && (exitDoor == door || !IsInsideRoom(current));
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

            if (!IsInsideRoom(current))
            {
                for (int d = 0; d < doors.Length; d++)
                {
                    LogicalBounds strip = DoorwayStrip(d);
                    if (CanUseDoorway(d, current, exitDoor) && strip.ContainsCircle(current, radius))
                    {
                        return Clamp(position, strip, radius);
                    }
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
                if (doors[d].State != DoorState.Open)
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
                if (doors[d].State == DoorState.Open &&
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
        /// per millimetre) inside the room walls, and says which walls it hit.
        /// Objects never use doorways.
        /// </summary>
        public void KeepObjectInRoom(int objectRadius, long scale, ref long nextX, ref long nextZ, out bool hitX, out bool hitZ)
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
        }

        private static LogicalPosition Clamp(LogicalPosition position, LogicalBounds bounds, int radius)
        {
            return new LogicalPosition(
                Math.Max(bounds.MinX + radius, Math.Min(bounds.MaxX - radius, position.X)),
                Math.Max(bounds.MinZ + radius, Math.Min(bounds.MaxZ - radius, position.Z)));
        }
    }
}

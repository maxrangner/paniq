using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// The floor of the building drawn as small squares, and for each square:
    /// whether a person could stand there, which room it belongs to, and how
    /// much clear space there is around it.
    ///
    /// This is what lets people find their way. Until now they knew how to get
    /// from room to room -- door to door in straight lines -- but nothing knew
    /// how to cross a room, so anyone whose goal was not in a straight line
    /// walked into the furniture. A square is a place a route can pass through,
    /// so a route can bend round a desk.
    ///
    /// Squares are 250 mm: a metre-wide doorway is four of them and a person is
    /// two across, which is fine enough that a doorway is never mistaken for a
    /// wall. Clearance is the real distance in millimetres to the nearest solid
    /// edge, not a count of squares. That distinction is load-bearing: rounding
    /// it to whole squares can report the middle of a 600 mm doorway as too
    /// narrow to pass, which would seal a room and burn everyone in it without
    /// a single error being raised.
    ///
    /// Doors open and shut and tables are smashed, so the walkable part of the
    /// building changes during a run. This holds the building as authored; what
    /// changes with a door is handled where routes are worked out.
    /// </summary>
    internal sealed class NavigationGrid
    {
        /// <summary>Squares are this wide. A 1000 mm doorway is four of them and a person two.</summary>
        public const int CellSizeMillimetres = 250;

        /// <summary>Clearance is not measured past this: nothing asks whether a hall is 30 m wide or 40.</summary>
        public const int MaximumClearanceMillimetres = 2000;

        /// <summary>How far past the rooms the grid reaches, to cover the ground outside a way out.</summary>
        private const int OutsideMarginMillimetres = 4000;

        /// <summary>How wide a square of the coarse grid used to find nearby wall segments is.</summary>
        private const int SegmentBucketMillimetres = 1000;

        /// <summary>How far past a wall a doorway's floor reaches, on both sides.</summary>
        public const int DoorwayReachMillimetres = 1500;

        /// <summary>A square no room covers.</summary>
        public const short Outside = -1;

        private readonly int minX;
        private readonly int minZ;
        private readonly int columns;
        private readonly int rows;

        /// <summary>Per square: the room whose floor it is, or <see cref="Outside"/>.</summary>
        private readonly short[] cellRoom;

        /// <summary>Per square: millimetres to the nearest wall or table edge, capped.</summary>
        private readonly ushort[] clearance;

        public NavigationGrid(LogicalBounds rooms, IReadOnlyList<LogicalBounds> roomBounds,
            IReadOnlyList<LogicalBounds> tables, IReadOnlyList<Wall> walls, IReadOnlyList<Doorway> doorways)
        {
            minX = FloorTo(rooms.MinX - OutsideMarginMillimetres, CellSizeMillimetres);
            minZ = FloorTo(rooms.MinZ - OutsideMarginMillimetres, CellSizeMillimetres);
            int maxX = rooms.MaxX + OutsideMarginMillimetres;
            int maxZ = rooms.MaxZ + OutsideMarginMillimetres;
            columns = Math.Max(1, (maxX - minX) / CellSizeMillimetres + 1);
            rows = Math.Max(1, (maxZ - minZ) / CellSizeMillimetres + 1);

            cellRoom = new short[columns * rows];
            clearance = new ushort[columns * rows];

            MarkRooms(roomBounds, tables);
            MeasureClearance(walls, tables);
            MarkDoorways(doorways, walls, tables);
        }

        public int Columns => columns;

        public int Rows => rows;

        /// <summary>How many squares the floor is drawn as.</summary>
        public int CellCount => cellRoom.Length;

        /// <summary>The middle of a square, by its number.</summary>
        public LogicalPosition CentreOfCell(int cell) => CentreOf(cell % columns, cell / columns);

        /// <summary>A read-only look at the grid, for a debugging overlay to draw.</summary>
        public NavigationGridReading Reading() => new NavigationGridReading(this);

        /// <summary>The middle of a square, which is the point it stands for.</summary>
        public LogicalPosition CentreOf(int column, int row)
        {
            return new LogicalPosition(
                minX + column * CellSizeMillimetres + CellSizeMillimetres / 2,
                minZ + row * CellSizeMillimetres + CellSizeMillimetres / 2);
        }

        /// <summary>Whether a point is on the part of the world this grid covers at all.</summary>
        public bool Covers(LogicalPosition point)
        {
            return point.X >= minX && point.Z >= minZ &&
                   point.X < minX + columns * CellSizeMillimetres &&
                   point.Z < minZ + rows * CellSizeMillimetres;
        }

        /// <summary>The square holding a point, or -1 when the point is off the grid.</summary>
        public int CellAt(LogicalPosition point)
        {
            if (!Covers(point))
            {
                return -1;
            }

            return (point.Z - minZ) / CellSizeMillimetres * columns + (point.X - minX) / CellSizeMillimetres;
        }

        /// <summary>The room a square's floor belongs to, or <see cref="Outside"/>.</summary>
        public short RoomOfCell(int cell) => cell < 0 ? Outside : cellRoom[cell];

        /// <summary>Millimetres of clear space around a square's middle, capped.</summary>
        public int ClearanceOfCell(int cell) => cell < 0 ? 0 : clearance[cell];

        /// <summary>
        /// Whether a body of this radius fits in this square. Measured against
        /// the real distance to the nearest edge, so a doorway only just wide
        /// enough still counts as a way through.
        /// </summary>
        public bool Fits(int cell, int radius) => cell >= 0 && clearance[cell] >= radius;

        /// <summary>The widest body that could pass anywhere along a door's gap.</summary>
        public int WidestBodyThroughGap(LogicalPosition from, LogicalPosition to)
        {
            int widest = 0;
            int steps = Math.Max(1, (int)(IntegerMath.Distance(from, to) / (CellSizeMillimetres / 2)));
            for (int i = 0; i <= steps; i++)
            {
                var at = new LogicalPosition(
                    from.X + (int)((long)(to.X - from.X) * i / steps),
                    from.Z + (int)((long)(to.Z - from.Z) * i / steps));
                widest = Math.Max(widest, ClearanceOfCell(CellAt(at)));
            }

            return widest;
        }

        /// <summary>
        /// The squares in a doorway are floor too, including the ones past the
        /// wall of a way out. Without this a route could reach a doorway and
        /// stop dead at it, because the ground on the far side belongs to no
        /// room -- and everyone would be trapped in the room they started in.
        /// </summary>
        private void MarkDoorways(IReadOnlyList<Doorway> doorways, IReadOnlyList<Wall> walls,
            IReadOnlyList<LogicalBounds> tables)
        {
            var edges = new List<Wall>(walls);
            foreach (LogicalBounds table in tables)
            {
                AddEdgesOf(edges, table);
            }

            foreach (Doorway doorway in doorways)
            {
                for (int along = -doorway.HalfWidth; along <= doorway.HalfWidth; along += CellSizeMillimetres / 2)
                {
                    for (int across = -DoorwayReachMillimetres; across <= DoorwayReachMillimetres;
                         across += CellSizeMillimetres / 2)
                    {
                        LogicalPosition at = doorway.PointAt(along, across);
                        int cell = CellAt(at);
                        if (cell < 0 || cellRoom[cell] != Outside)
                        {
                            continue;
                        }

                        cellRoom[cell] = (short)doorway.Room;
                        clearance[cell] = (ushort)NearestEdge(edges, CentreOfCell(cell));
                    }
                }
            }
        }

        private static long NearestEdge(List<Wall> edges, LogicalPosition point)
        {
            long nearest = MaximumClearanceMillimetres;
            for (int i = 0; i < edges.Count; i++)
            {
                long distance = edges[i].DistanceFrom(point);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private static void AddEdgesOf(List<Wall> into, LogicalBounds b)
        {
            into.Add(new Wall(new LogicalPosition(b.MinX, b.MinZ), new LogicalPosition(b.MaxX, b.MinZ)));
            into.Add(new Wall(new LogicalPosition(b.MaxX, b.MinZ), new LogicalPosition(b.MaxX, b.MaxZ)));
            into.Add(new Wall(new LogicalPosition(b.MaxX, b.MaxZ), new LogicalPosition(b.MinX, b.MaxZ)));
            into.Add(new Wall(new LogicalPosition(b.MinX, b.MaxZ), new LogicalPosition(b.MinX, b.MinZ)));
        }

        // ---------------------------------------------------------------- building

        /// <summary>
        /// Each square takes the room its middle sits in. Rooms are required to
        /// line up with the grid, so a square is never half in one room and half
        /// in the next, and the answer for the middle holds for the whole square.
        /// </summary>
        private void MarkRooms(IReadOnlyList<LogicalBounds> roomBounds, IReadOnlyList<LogicalBounds> tables)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int cell = row * columns + column;
                    LogicalPosition centre = CentreOf(column, row);
                    cellRoom[cell] = Outside;
                    for (int r = 0; r < roomBounds.Count; r++)
                    {
                        LogicalBounds b = roomBounds[r];
                        if (centre.X > b.MinX && centre.X < b.MaxX && centre.Z > b.MinZ && centre.Z < b.MaxZ)
                        {
                            cellRoom[cell] = (short)r;
                            break;
                        }
                    }

                    // A square under a table is floor nobody can stand on.
                    if (cellRoom[cell] == Outside)
                    {
                        continue;
                    }

                    for (int t = 0; t < tables.Count; t++)
                    {
                        LogicalBounds b = tables[t];
                        if (centre.X > b.MinX && centre.X < b.MaxX && centre.Z > b.MinZ && centre.Z < b.MaxZ)
                        {
                            cellRoom[cell] = Outside;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The real distance from each square's middle to the nearest solid
        /// edge. Segments are bucketed into a coarser grid first, so each square
        /// only measures against the handful of edges that could possibly be the
        /// nearest -- otherwise a fifty-room building would measure every square
        /// against every wall in it.
        /// </summary>
        private void MeasureClearance(IReadOnlyList<Wall> walls, IReadOnlyList<LogicalBounds> tables)
        {
            var segments = new List<Wall>(walls);
            foreach (LogicalBounds table in tables)
            {
                segments.Add(new Wall(new LogicalPosition(table.MinX, table.MinZ), new LogicalPosition(table.MaxX, table.MinZ)));
                segments.Add(new Wall(new LogicalPosition(table.MaxX, table.MinZ), new LogicalPosition(table.MaxX, table.MaxZ)));
                segments.Add(new Wall(new LogicalPosition(table.MaxX, table.MaxZ), new LogicalPosition(table.MinX, table.MaxZ)));
                segments.Add(new Wall(new LogicalPosition(table.MinX, table.MaxZ), new LogicalPosition(table.MinX, table.MinZ)));
            }

            int bucketColumns = Math.Max(1, columns * CellSizeMillimetres / SegmentBucketMillimetres + 1);
            int bucketRows = Math.Max(1, rows * CellSizeMillimetres / SegmentBucketMillimetres + 1);
            var buckets = new List<int>[bucketColumns * bucketRows];
            int halo = MaximumClearanceMillimetres / SegmentBucketMillimetres + 1;

            for (int s = 0; s < segments.Count; s++)
            {
                Wall wall = segments[s];
                int firstColumn = Bucket(Math.Min(wall.From.X, wall.To.X) - minX, bucketColumns) - halo;
                int lastColumn = Bucket(Math.Max(wall.From.X, wall.To.X) - minX, bucketColumns) + halo;
                int firstRow = Bucket(Math.Min(wall.From.Z, wall.To.Z) - minZ, bucketRows) - halo;
                int lastRow = Bucket(Math.Max(wall.From.Z, wall.To.Z) - minZ, bucketRows) + halo;
                for (int row = Math.Max(0, firstRow); row <= Math.Min(bucketRows - 1, lastRow); row++)
                {
                    for (int column = Math.Max(0, firstColumn); column <= Math.Min(bucketColumns - 1, lastColumn); column++)
                    {
                        int bucket = row * bucketColumns + column;
                        buckets[bucket] ??= new List<int>();
                        buckets[bucket].Add(s);
                    }
                }
            }

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int cell = row * columns + column;
                    if (cellRoom[cell] == Outside)
                    {
                        clearance[cell] = 0;
                        continue;
                    }

                    LogicalPosition centre = CentreOf(column, row);
                    int bucket = Bucket(centre.Z - minZ, bucketRows) * bucketColumns + Bucket(centre.X - minX, bucketColumns);
                    List<int> nearby = buckets[bucket];
                    long nearest = MaximumClearanceMillimetres;
                    if (nearby != null)
                    {
                        for (int i = 0; i < nearby.Count; i++)
                        {
                            long distance = segments[nearby[i]].DistanceFrom(centre);
                            if (distance < nearest)
                            {
                                nearest = distance;
                            }
                        }
                    }

                    clearance[cell] = (ushort)nearest;
                }
            }
        }

        private static int Bucket(int offset, int count)
        {
            int at = offset / SegmentBucketMillimetres;
            return at < 0 ? 0 : (at >= count ? count - 1 : at);
        }

        private static int FloorTo(int value, int step)
        {
            int down = value / step;
            return (value % step != 0 && value < 0 ? down - 1 : down) * step;
        }

        /// <summary>
        /// Where a doorway is, so the floor through it can be marked walkable:
        /// the middle of the gap, which way is along the wall, which way is
        /// through it, and how wide the gap is.
        /// </summary>
        internal readonly struct Doorway
        {
            public Doorway(LogicalPosition centre, bool alongX, int halfWidth, int room)
            {
                Centre = centre;
                AlongX = alongX;
                HalfWidth = halfWidth;
                Room = room;
            }

            public LogicalPosition Centre { get; }

            /// <summary>True when the wall runs east to west, so the gap is measured along X.</summary>
            public bool AlongX { get; }

            public int HalfWidth { get; }

            /// <summary>The room whose wall holds the doorway; its floor is the one squares in it join.</summary>
            public int Room { get; }

            /// <summary>A point so far along the gap and so far through the wall.</summary>
            public LogicalPosition PointAt(int along, int across)
            {
                return AlongX
                    ? new LogicalPosition(Centre.X + along, Centre.Z + across)
                    : new LogicalPosition(Centre.X + across, Centre.Z + along);
            }
        }

        /// <summary>
        /// A solid edge a body cannot cross: a stretch of wall between doorways,
        /// or one side of a table. Walls are handed in already broken around
        /// their door gaps, so a doorway simply has no edge across it.
        /// </summary>
        internal readonly struct Wall
        {
            public Wall(LogicalPosition from, LogicalPosition to)
            {
                From = from;
                To = to;
            }

            public LogicalPosition From { get; }

            public LogicalPosition To { get; }

            /// <summary>Millimetres from a point to the nearest place on this edge.</summary>
            public long DistanceFrom(LogicalPosition point)
            {
                long abx = (long)To.X - From.X;
                long abz = (long)To.Z - From.Z;
                long apx = (long)point.X - From.X;
                long apz = (long)point.Z - From.Z;
                long lengthSquared = abx * abx + abz * abz;
                if (lengthSquared == 0L)
                {
                    return IntegerMath.Sqrt(apx * apx + apz * apz);
                }

                long dot = apx * abx + apz * abz;
                if (dot <= 0L)
                {
                    return IntegerMath.Sqrt(apx * apx + apz * apz);
                }

                if (dot >= lengthSquared)
                {
                    long bpx = (long)point.X - To.X;
                    long bpz = (long)point.Z - To.Z;
                    return IntegerMath.Sqrt(bpx * bpx + bpz * bpz);
                }

                // Perpendicular distance, kept exact by squaring before dividing.
                long cross = abx * apz - abz * apx;
                return IntegerMath.Sqrt(cross * cross / lengthSquared);
            }
        }
    }

    /// <summary>
    /// What a debugging overlay is allowed to see of the floor: how many
    /// squares there are, where each one is, and whether a body of a given size
    /// would fit. Read-only, and nothing here can change the run.
    /// </summary>
    public sealed class NavigationGridReading
    {
        private readonly NavigationGrid grid;

        internal NavigationGridReading(NavigationGrid grid)
        {
            this.grid = grid;
        }

        public int Count => grid.CellCount;

        public LogicalPosition CentreOf(int cell) => grid.CentreOfCell(cell);

        public bool FitsABody(int cell, int radiusMillimetres) => grid.Fits(cell, radiusMillimetres);
    }
}

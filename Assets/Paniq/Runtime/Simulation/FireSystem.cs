using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Grid fire. The floor is split into square cells. The fire starts in
    /// one seeded cell; each burning cell periodically lights one random
    /// unburnt north/east/south/west neighbour, so it only ever grows outward
    /// from itself. Burning cells stay burning. This system owns the fire's
    /// state and answers every question about where the fire is.
    /// </summary>
    internal sealed class FireSystem
    {
        private static readonly int[] NeighbourOffsetX = { 0, 1, 0, -1 };
        private static readonly int[] NeighbourOffsetZ = { 1, 0, -1, 0 };

        private readonly SimulationContext context;
        private readonly FireSettings settings;
        private readonly LogicalBounds floor;
        private readonly int radius;
        private readonly List<int> burningCells = new List<int>();
        private readonly List<FireCellSnapshot> cellRecords = new List<FireCellSnapshot>();
        private readonly List<int> neighbourScratch = new List<int>(4);
        private readonly int gridColumns;
        private readonly int gridRows;
        private readonly ulong[] cellEventIds;
        private readonly int[] cellNextSpreadTicks;
        private readonly int originCell;
        private bool active;
        private ulong activationEventId;

        /// <summary>Lays out the grid and draws the seeded ignition point (two random draws: X then Z).</summary>
        public FireSystem(SimulationContext context, WorldGeometry geometry)
        {
            this.context = context;
            settings = context.Scenario.Fire;
            floor = geometry.Floor;
            radius = context.Scenario.World.OccupancyRadiusMillimetres;

            int cellSize = settings.CellSizeMillimetres;
            gridColumns = (floor.MaxX - floor.MinX + cellSize - 1) / cellSize;
            gridRows = (floor.MaxZ - floor.MinZ + cellSize - 1) / cellSize;
            int cellCount = checked(gridColumns * gridRows);
            cellEventIds = new ulong[cellCount];
            cellNextSpreadTicks = new int[cellCount];

            var origin = new LogicalPosition(
                context.Random.NextIntInclusive(settings.SpawnBounds.MinX, settings.SpawnBounds.MaxX),
                context.Random.NextIntInclusive(settings.SpawnBounds.MinZ, settings.SpawnBounds.MaxZ));
            originCell = CellAt(origin);
        }

        public bool Active => active;
        public int BurningCount => burningCells.Count;
        public int GridColumns => gridColumns;
        public int GridRows => gridRows;
        public LogicalPosition Origin => CellBounds(originCell).Centre;
        public ulong ActivationEventId => activationEventId;

        /// <summary>Phase 2: the fire starts on its tick, then spreads.</summary>
        public void Advance()
        {
            int tick = context.Tick;
            if (!active)
            {
                if (tick >= settings.ActivationTick)
                {
                    active = true;
                    activationEventId = Ignite(originCell, FireReactionEventType.FireActivated, 0UL);
                }

                return;
            }

            // Only cells that were burning at the start of this tick spread,
            // in the order they ignited.
            int burningAtStart = burningCells.Count;
            for (int i = 0; i < burningAtStart; i++)
            {
                int cell = burningCells[i];
                if (cellNextSpreadTicks[cell] > tick)
                {
                    continue;
                }

                CollectUnburntNeighbours(cell);
                if (neighbourScratch.Count == 0)
                {
                    // Surrounded by fire; it can never spread again.
                    cellNextSpreadTicks[cell] = int.MaxValue;
                    continue;
                }

                int chosen = neighbourScratch[context.Random.NextIntInclusive(0, neighbourScratch.Count - 1)];
                Ignite(chosen, FireReactionEventType.FireSpread, cellEventIds[cell]);
                cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            }
        }

        private ulong Ignite(int cell, FireReactionEventType eventType, ulong parentEventId)
        {
            int tick = context.Tick;
            CausalEvent ignition = context.Events.Append(
                tick,
                new SimulationId(FireReactionSimulation.FireHazardIdValue),
                eventType,
                CellBounds(cell).Centre,
                settings.CellSizeMillimetres,
                0,
                parentEventId);
            cellEventIds[cell] = ignition.EventId;
            cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            burningCells.Add(cell);
            cellRecords.Add(new FireCellSnapshot(cell % gridColumns, cell / gridColumns, CellBounds(cell), tick, ignition.EventId));
            return ignition.EventId;
        }

        private int NextSpreadDelay()
        {
            return context.Random.NextIntInclusive(settings.SpreadMinimumTicks, settings.SpreadMaximumTicks);
        }

        private void CollectUnburntNeighbours(int cell)
        {
            neighbourScratch.Clear();
            int cellX = cell % gridColumns;
            int cellZ = cell / gridColumns;
            for (int direction = 0; direction < 4; direction++)
            {
                int x = cellX + NeighbourOffsetX[direction];
                int z = cellZ + NeighbourOffsetZ[direction];
                if (x < 0 || z < 0 || x >= gridColumns || z >= gridRows)
                {
                    continue;
                }

                int neighbour = z * gridColumns + x;
                if (cellEventIds[neighbour] == 0UL)
                {
                    neighbourScratch.Add(neighbour);
                }
            }
        }

        private int CellAt(LogicalPosition position)
        {
            int cellSize = settings.CellSizeMillimetres;
            int x = Math.Max(0, Math.Min(gridColumns - 1, (position.X - floor.MinX) / cellSize));
            int z = Math.Max(0, Math.Min(gridRows - 1, (position.Z - floor.MinZ) / cellSize));
            return z * gridColumns + x;
        }

        private LogicalBounds CellBounds(int cell)
        {
            int cellSize = settings.CellSizeMillimetres;
            int minX = floor.MinX + cell % gridColumns * cellSize;
            int minZ = floor.MinZ + cell / gridColumns * cellSize;
            return new LogicalBounds(minX, Math.Min(floor.MaxX, minX + cellSize), minZ, Math.Min(floor.MaxZ, minZ + cellSize));
        }

        /// <summary>The ignition event of a burning cell.</summary>
        public ulong CellEventId(int cell) => cellEventIds[cell];

        /// <summary>The grid cell under a point (clamped to the grid).</summary>
        public int CellIndexAt(LogicalPosition position) => CellAt(position);

        /// <summary>
        /// Something burning (not the spreading fire itself) sets a cell
        /// alight: a <c>FireSpread</c> caused by <paramref name="causeEventId"/>.
        /// Nothing happens if it is already burning.
        /// </summary>
        public void IgniteCell(int cell, ulong causeEventId)
        {
            if (!active || cellEventIds[cell] != 0UL)
            {
                return;
            }

            Ignite(cell, FireReactionEventType.FireSpread, causeEventId);
        }

        /// <summary>Burning cells in ignition order, as a view that later ignitions do not change. Nothing is copied.</summary>
        public AppendOnlyView<FireCellSnapshot> GetCells() => new AppendOnlyView<FireCellSnapshot>(cellRecords);

        // ---------------------------------------------------------------- queries

        /// <summary>The event ID of the earliest-lit cell overlapping a person's footprint, or 0.</summary>
        public ulong FindTouching(LogicalPosition position)
        {
            return FindTouchingSweep(position, position);
        }

        /// <summary>The event ID of the earliest-lit cell a person's swept footprint overlaps, or 0.</summary>
        public ulong FindTouchingSweep(LogicalPosition start, LogicalPosition end)
        {
            return FindTouchingSweep(start, end, radius);
        }

        /// <summary>The event ID of the earliest-lit cell within <paramref name="reach"/> of a point, or 0.</summary>
        public ulong FindTouchingCircle(LogicalPosition centre, int reach)
        {
            return FindTouchingSweep(centre, centre, reach);
        }

        /// <summary>The event ID of the earliest-lit cell overlapping a rectangle, or 0.</summary>
        public ulong FindTouchingBounds(LogicalBounds bounds)
        {
            int firstX = ColumnOf(bounds.MinX);
            int lastX = ColumnOf(bounds.MaxX);
            int firstZ = RowOf(bounds.MinZ);
            int lastZ = RowOf(bounds.MaxZ);
            ulong earliest = 0UL;
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    ulong eventId = cellEventIds[z * gridColumns + x];
                    if (eventId != 0UL && (earliest == 0UL || eventId < earliest))
                    {
                        earliest = eventId;
                    }
                }
            }

            return earliest;
        }

        private ulong FindTouchingSweep(LogicalPosition start, LogicalPosition end, int radius)
        {
            int cellSize = settings.CellSizeMillimetres;
            int firstX = Math.Max(0, (Math.Min(start.X, end.X) - radius - floor.MinX) / cellSize);
            int lastX = Math.Min(gridColumns - 1, (Math.Max(start.X, end.X) + radius - floor.MinX) / cellSize);
            int firstZ = Math.Max(0, (Math.Min(start.Z, end.Z) - radius - floor.MinZ) / cellSize);
            int lastZ = Math.Min(gridRows - 1, (Math.Max(start.Z, end.Z) + radius - floor.MinZ) / cellSize);

            ulong earliest = 0UL;
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    int cell = z * gridColumns + x;
                    ulong eventId = cellEventIds[cell];
                    if (eventId == 0UL || (earliest != 0UL && eventId >= earliest))
                    {
                        continue;
                    }

                    if (IntegerMath.SweptCircleOverlapsBounds(start, end, radius, CellBounds(cell)))
                    {
                        earliest = eventId;
                    }
                }
            }

            return earliest;
        }

        // Every query below gives exactly the answer of checking every burning
        // cell, ties included (the earliest-lit cell wins), but only looks at
        // grid cells that could matter. When fewer cells burn than it would
        // have to look at, it checks the burning cells directly instead.

        /// <summary>
        /// Squared distance to the nearest burning point, or long.MaxValue
        /// when nothing burns. Also names that point and its cell (-1 when
        /// nothing burns). Ties go to the earliest-lit cell. Searches outward
        /// from the position in square rings of cells and stops once no
        /// closer cell is possible.
        /// </summary>
        public long NearestDistanceSquared(LogicalPosition position, out LogicalPosition nearestPoint, out int nearestCell)
        {
            nearestPoint = position;
            nearestCell = -1;
            if (burningCells.Count == 0)
            {
                return long.MaxValue;
            }

            int cellSize = settings.CellSizeMillimetres;
            int centreColumn = ColumnOf(position.X);
            int centreRow = RowOf(position.Z);
            int lastRing = Math.Max(gridColumns, gridRows);
            long nearest = long.MaxValue;
            ulong nearestEventId = 0UL;
            int visited = 0;
            for (int ring = 0; ring <= lastRing; ring++)
            {
                // Every cell in this ring is at least (ring - 1) cells away.
                long gap = (long)(ring - 1) * cellSize;
                if (nearestCell >= 0 && gap > 0L && gap * gap > nearest)
                {
                    break;
                }

                int minRow = centreRow - ring;
                int maxRow = centreRow + ring;
                for (int row = Math.Max(0, minRow); row <= Math.Min(gridRows - 1, maxRow); row++)
                {
                    bool edgeRow = row == minRow || row == maxRow;
                    int step = edgeRow ? 1 : Math.Max(1, ring * 2);
                    for (int column = centreColumn - ring; column <= centreColumn + ring; column += step)
                    {
                        if (column < 0 || column >= gridColumns)
                        {
                            continue;
                        }

                        if (++visited > burningCells.Count)
                        {
                            // Cheaper to check every burning cell after all.
                            return NearestByCheckingEveryCell(position, out nearestPoint, out nearestCell);
                        }

                        int cell = row * gridColumns + column;
                        ulong eventId = cellEventIds[cell];
                        if (eventId == 0UL)
                        {
                            continue;
                        }

                        LogicalPosition point = CellBounds(cell).ClosestPoint(position);
                        long distance = LogicalPosition.DistanceSquared(position, point);
                        if (distance < nearest || (distance == nearest && eventId < nearestEventId))
                        {
                            nearest = distance;
                            nearestEventId = eventId;
                            nearestPoint = point;
                            nearestCell = cell;
                        }
                    }
                }
            }

            return nearest;
        }

        private long NearestByCheckingEveryCell(LogicalPosition position, out LogicalPosition nearestPoint, out int nearestCell)
        {
            long nearest = long.MaxValue;
            nearestPoint = position;
            nearestCell = -1;
            for (int i = 0; i < burningCells.Count; i++)
            {
                LogicalBounds bounds = CellBounds(burningCells[i]);
                LogicalPosition point = bounds.ClosestPoint(position);
                long distance = LogicalPosition.DistanceSquared(position, point);
                if (distance < nearest)
                {
                    nearest = distance;
                    nearestPoint = point;
                    nearestCell = burningCells[i];
                }
            }

            return nearest;
        }

        public long NearestDistanceSquared(LogicalPosition position, out LogicalPosition nearestPoint)
        {
            return NearestDistanceSquared(position, out nearestPoint, out _);
        }

        public long NearestDistanceSquared(LogicalPosition position)
        {
            return NearestDistanceSquared(position, out _, out _);
        }

        /// <summary>True when the straight line between two points passes within <paramref name="clearance"/> of fire (checked at its quarter points).</summary>
        public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance)
        {
            long clearanceSquared = (long)clearance * clearance;
            for (int quarter = 1; quarter <= 3; quarter++)
            {
                var point = new LogicalPosition(
                    (int)(from.X + ((long)to.X - from.X) * quarter / 4),
                    (int)(from.Z + ((long)to.Z - from.Z) * quarter / 4));
                if (AnyWithin(point, clearance, clearanceSquared))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when some burning point is strictly closer than <paramref name="distance"/>.</summary>
        public bool AnyCloserThan(LogicalPosition position, int distance)
        {
            return AnyWithin(position, distance, (long)distance * distance);
        }

        private bool AnyWithin(LogicalPosition position, int reach, long reachSquared)
        {
            CellRange range = CellsWithin(position, reach);
            if (burningCells.Count <= range.Count)
            {
                for (int i = 0; i < burningCells.Count; i++)
                {
                    if (CellBounds(burningCells[i]).DistanceSquaredTo(position) < reachSquared)
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int row = range.FirstRow; row <= range.LastRow; row++)
            {
                for (int column = range.FirstColumn; column <= range.LastColumn; column++)
                {
                    int cell = row * gridColumns + column;
                    if (cellEventIds[cell] != 0UL && CellBounds(cell).DistanceSquaredTo(position) < reachSquared)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Integer test of every burning cell against a forward 90-degree
        /// vision cone. Presentation geometry never decides fear.
        /// </summary>
        public bool IsVisibleFrom(LogicalPosition eye, int heading, int range)
        {
            long rangeSquared = (long)range * range;
            LogicalPosition direction = IntegerMath.Direction(heading);
            CellRange cells = CellsWithin(eye, range);
            if (burningCells.Count <= cells.Count)
            {
                for (int i = 0; i < burningCells.Count; i++)
                {
                    if (CellIsVisible(burningCells[i], eye, direction, rangeSquared))
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int row = cells.FirstRow; row <= cells.LastRow; row++)
            {
                for (int column = cells.FirstColumn; column <= cells.LastColumn; column++)
                {
                    int cell = row * gridColumns + column;
                    if (cellEventIds[cell] != 0UL && CellIsVisible(cell, eye, direction, rangeSquared))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The nearest point, centre or a corner of the cell lies inside the vision cone.</summary>
        private bool CellIsVisible(int cell, LogicalPosition eye, LogicalPosition direction, long rangeSquared)
        {
            LogicalBounds bounds = CellBounds(cell);
            LogicalPosition closest = bounds.ClosestPoint(eye);
            if (LogicalPosition.DistanceSquared(eye, closest) > rangeSquared)
            {
                return false;
            }

            return InVisionCone(eye, direction, closest, rangeSquared) ||
                   InVisionCone(eye, direction, bounds.Centre, rangeSquared) ||
                   InVisionCone(eye, direction, new LogicalPosition(bounds.MinX, bounds.MinZ), rangeSquared) ||
                   InVisionCone(eye, direction, new LogicalPosition(bounds.MaxX, bounds.MinZ), rangeSquared) ||
                   InVisionCone(eye, direction, new LogicalPosition(bounds.MinX, bounds.MaxZ), rangeSquared) ||
                   InVisionCone(eye, direction, new LogicalPosition(bounds.MaxX, bounds.MaxZ), rangeSquared);
        }

        /// <summary>The grid cells that could hold a point within <paramref name="reach"/> of a position (a few extra are fine).</summary>
        private CellRange CellsWithin(LogicalPosition position, int reach)
        {
            // One millimetre extra on the low side, because a cell whose far
            // edge is exactly <paramref name="reach"/> away belongs to the
            // previous column or row.
            return new CellRange(
                ColumnOf((long)position.X - reach - 1),
                ColumnOf((long)position.X + reach),
                RowOf((long)position.Z - reach - 1),
                RowOf((long)position.Z + reach));
        }

        private int ColumnOf(long x)
        {
            return (int)Math.Max(0L, Math.Min(gridColumns - 1L, FloorDivide(x - floor.MinX, settings.CellSizeMillimetres)));
        }

        private int RowOf(long z)
        {
            return (int)Math.Max(0L, Math.Min(gridRows - 1L, FloorDivide(z - floor.MinZ, settings.CellSizeMillimetres)));
        }

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && value < 0L ? quotient - 1L : quotient;
        }

        private readonly struct CellRange
        {
            public CellRange(int firstColumn, int lastColumn, int firstRow, int lastRow)
            {
                FirstColumn = firstColumn;
                LastColumn = lastColumn;
                FirstRow = firstRow;
                LastRow = lastRow;
            }

            public int FirstColumn { get; }
            public int LastColumn { get; }
            public int FirstRow { get; }
            public int LastRow { get; }
            public int Count => (LastColumn - FirstColumn + 1) * (LastRow - FirstRow + 1);
        }

        private static bool InVisionCone(LogicalPosition eye, LogicalPosition direction, LogicalPosition point, long rangeSquared)
        {
            long offsetX = (long)point.X - eye.X;
            long offsetZ = (long)point.Z - eye.Z;
            if (checked(offsetX * offsetX + offsetZ * offsetZ) > rangeSquared)
            {
                return false;
            }

            long forward = checked(offsetX * direction.X + offsetZ * direction.Z);
            long lateral = checked(offsetX * direction.Z - offsetZ * direction.X);

            // A 45-degree half-angle means |sideways| <= forward.
            return forward >= 0L && Math.Abs(lateral) <= forward;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Grid fire. The room floor is split into square cells. The fire starts
    /// in one seeded cell; each burning cell periodically lights one random
    /// unburnt north/east/south/west neighbour, so it only ever grows outward
    /// from itself. Burning cells stay burning.
    /// </summary>
    public sealed partial class FireReactionSimulation
    {
        private static readonly int[] NeighbourOffsetX = { 0, 1, 0, -1 };
        private static readonly int[] NeighbourOffsetZ = { 1, 0, -1, 0 };

        private readonly List<int> burningCells = new List<int>();
        private readonly List<int> neighbourScratch = new List<int>(4);
        private int gridColumns;
        private int gridRows;
        private ulong[] cellEventIds;
        private int[] cellIgnitionTicks;
        private int[] cellNextSpreadTicks;
        private int fireOriginCell;
        private bool fireActive;
        private ulong fireActivationEventId;

        public bool FireActive => fireActive;
        public int FireCellCount => burningCells.Count;
        public int FireGridColumns => gridColumns;
        public int FireGridRows => gridRows;
        public LogicalPosition FireOrigin => CellBounds(fireOriginCell).Centre;
        public ulong FireActivationEventId => fireActivationEventId;

        private void InitializeFire()
        {
            int cellSize = scenario.FireCellSizeMillimetres;
            LogicalBounds room = scenario.RoomBounds;
            gridColumns = (room.MaxX - room.MinX + cellSize - 1) / cellSize;
            gridRows = (room.MaxZ - room.MinZ + cellSize - 1) / cellSize;
            int cellCount = checked(gridColumns * gridRows);
            cellEventIds = new ulong[cellCount];
            cellIgnitionTicks = new int[cellCount];
            cellNextSpreadTicks = new int[cellCount];

            var origin = new LogicalPosition(
                random.NextIntInclusive(scenario.FireSpawnBounds.MinX, scenario.FireSpawnBounds.MaxX),
                random.NextIntInclusive(scenario.FireSpawnBounds.MinZ, scenario.FireSpawnBounds.MaxZ));
            fireOriginCell = CellAt(origin);
        }

        private void AdvanceFire()
        {
            if (!fireActive)
            {
                if (tick >= scenario.FireActivationTick)
                {
                    fireActive = true;
                    fireActivationEventId = Ignite(fireOriginCell, FireReactionEventType.FireActivated, 0UL);
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

                int chosen = neighbourScratch[random.NextIntInclusive(0, neighbourScratch.Count - 1)];
                Ignite(chosen, FireReactionEventType.FireSpread, cellEventIds[cell]);
                cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            }
        }

        private ulong Ignite(int cell, FireReactionEventType eventType, ulong parentEventId)
        {
            CausalEvent ignition = eventLog.Append(
                tick,
                new StableAgentId(FireHazardIdValue),
                eventType,
                CellBounds(cell).Centre,
                scenario.FireCellSizeMillimetres,
                0,
                parentEventId);
            cellEventIds[cell] = ignition.EventId;
            cellIgnitionTicks[cell] = tick;
            cellNextSpreadTicks[cell] = checked(tick + NextSpreadDelay());
            burningCells.Add(cell);
            return ignition.EventId;
        }

        private int NextSpreadDelay()
        {
            return random.NextIntInclusive(scenario.FireSpreadMinimumTicks, scenario.FireSpreadMaximumTicks);
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
            int cellSize = scenario.FireCellSizeMillimetres;
            int x = Math.Max(0, Math.Min(gridColumns - 1, (position.X - scenario.RoomBounds.MinX) / cellSize));
            int z = Math.Max(0, Math.Min(gridRows - 1, (position.Z - scenario.RoomBounds.MinZ) / cellSize));
            return z * gridColumns + x;
        }

        private LogicalBounds CellBounds(int cell)
        {
            int cellSize = scenario.FireCellSizeMillimetres;
            LogicalBounds room = scenario.RoomBounds;
            int minX = room.MinX + cell % gridColumns * cellSize;
            int minZ = room.MinZ + cell / gridColumns * cellSize;
            return new LogicalBounds(minX, Math.Min(room.MaxX, minX + cellSize), minZ, Math.Min(room.MaxZ, minZ + cellSize));
        }

        private FireCellSnapshot[] GetFireCells()
        {
            var cells = new FireCellSnapshot[burningCells.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                int cell = burningCells[i];
                cells[i] = new FireCellSnapshot(
                    cell % gridColumns,
                    cell / gridColumns,
                    CellBounds(cell),
                    cellIgnitionTicks[cell],
                    cellEventIds[cell]);
            }

            return cells;
        }

        /// <summary>The event ID of the earliest-lit cell overlapping this footprint, or 0.</summary>
        private ulong FindFireTouching(LogicalPosition position)
        {
            return FindFireTouchingSweep(position, position);
        }

        /// <summary>The event ID of the earliest-lit cell the swept footprint overlaps, or 0.</summary>
        private ulong FindFireTouchingSweep(LogicalPosition start, LogicalPosition end)
        {
            int radius = scenario.OccupancyRadiusMillimetres;
            int cellSize = scenario.FireCellSizeMillimetres;
            LogicalBounds room = scenario.RoomBounds;
            int firstX = Math.Max(0, (Math.Min(start.X, end.X) - radius - room.MinX) / cellSize);
            int lastX = Math.Min(gridColumns - 1, (Math.Max(start.X, end.X) + radius - room.MinX) / cellSize);
            int firstZ = Math.Max(0, (Math.Min(start.Z, end.Z) - radius - room.MinZ) / cellSize);
            int lastZ = Math.Min(gridRows - 1, (Math.Max(start.Z, end.Z) + radius - room.MinZ) / cellSize);

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

        /// <summary>Squared distance to the nearest burning point, or long.MaxValue when nothing burns.</summary>
        private long NearestFireDistanceSquared(LogicalPosition position, out LogicalPosition nearestPoint)
        {
            long nearest = long.MaxValue;
            nearestPoint = position;
            for (int i = 0; i < burningCells.Count; i++)
            {
                LogicalBounds bounds = CellBounds(burningCells[i]);
                LogicalPosition point = bounds.ClosestPoint(position);
                long distance = LogicalPosition.DistanceSquared(position, point);
                if (distance < nearest)
                {
                    nearest = distance;
                    nearestPoint = point;
                }
            }

            return nearest;
        }

        private long NearestFireDistanceSquared(LogicalPosition position)
        {
            return NearestFireDistanceSquared(position, out _);
        }

        /// <summary>
        /// Integer test of every burning cell against the agent's forward
        /// 90-degree vision cone. Presentation geometry never decides fear.
        /// </summary>
        private bool SeesFire(AgentRuntime agent)
        {
            long range = scenario.VisionRangeMillimetres;
            long rangeSquared = range * range;
            for (int i = 0; i < burningCells.Count; i++)
            {
                LogicalBounds bounds = CellBounds(burningCells[i]);
                LogicalPosition closest = bounds.ClosestPoint(agent.Position);
                if (LogicalPosition.DistanceSquared(agent.Position, closest) > rangeSquared)
                {
                    continue;
                }

                if (InVisionCone(agent, closest, rangeSquared) ||
                    InVisionCone(agent, bounds.Centre, rangeSquared) ||
                    InVisionCone(agent, new LogicalPosition(bounds.MinX, bounds.MinZ), rangeSquared) ||
                    InVisionCone(agent, new LogicalPosition(bounds.MaxX, bounds.MinZ), rangeSquared) ||
                    InVisionCone(agent, new LogicalPosition(bounds.MinX, bounds.MaxZ), rangeSquared) ||
                    InVisionCone(agent, new LogicalPosition(bounds.MaxX, bounds.MaxZ), rangeSquared))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InVisionCone(AgentRuntime agent, LogicalPosition point, long rangeSquared)
        {
            long offsetX = (long)point.X - agent.Position.X;
            long offsetZ = (long)point.Z - agent.Position.Z;
            if (checked(offsetX * offsetX + offsetZ * offsetZ) > rangeSquared)
            {
                return false;
            }

            LogicalPosition direction = IntegerMath.Direction(agent.Heading);
            long forward = checked(offsetX * direction.X + offsetZ * direction.Z);
            long lateral = checked(offsetX * direction.Z - offsetZ * direction.X);

            // A 45-degree half-angle means |sideways| <= forward.
            return forward >= 0L && Math.Abs(lateral) <= forward;
        }
    }
}

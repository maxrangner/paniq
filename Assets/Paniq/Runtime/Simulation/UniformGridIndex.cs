using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Where things are, arranged so that "who is near here" stops meaning
    /// "look at everyone". The floor is divided into square cells and each
    /// item sits in the cell holding its position; a question about a patch of
    /// floor only reads the cells that patch touches.
    ///
    /// Two properties make it safe to put underneath rules that were written
    /// against a plain scan of everything:
    ///
    /// It answers with <em>candidates</em>, never with conclusions. A gather
    /// returns every item whose cell overlaps the area asked about, and the
    /// caller still applies its own exact test to each one. Cells are coarser
    /// than the questions asked of them, so the candidate list is a superset
    /// of the true answer and no exact test changes.
    ///
    /// It answers in ascending item order, the same order a scan of everything
    /// would visit. Each cell keeps its items sorted and a gather merges them,
    /// so a rule that stops at the first match, or draws a random number per
    /// item, behaves exactly as it did before. That ordering is not a
    /// convenience: the run's randomness is one shared stream, so visiting two
    /// people in the other order would change every later draw in the run.
    ///
    /// Positions change during a tick -- a movement resolved for one person is
    /// visible to the next -- so this is updated as things move rather than
    /// rebuilt once per tick. <see cref="Place"/> is the only way in, and it
    /// costs nothing when an item has not left its cell.
    ///
    /// Items outside the grid are held in the nearest edge cell. A question
    /// asked from inside the building therefore never reaches them, which is
    /// correct: they are further away than anything it could match.
    /// </summary>
    internal sealed class UniformGridIndex
    {
        private const int Unplaced = -1;

        private readonly int cellSize;
        private readonly int minX;
        private readonly int minZ;
        private readonly int columns;
        private readonly int rows;

        /// <summary>Per cell: its lowest-numbered item, or -1 when empty.</summary>
        private readonly int[] head;

        /// <summary>Per item: the next item in the same cell, ascending, or -1.</summary>
        private readonly int[] next;

        /// <summary>Per item: the cell holding it, or -1 when it is not in the grid.</summary>
        private readonly int[] itemCell;

        /// <summary>
        /// Covers <paramref name="area"/>, in cells of <paramref name="cellSize"/>
        /// millimetres, for up to <paramref name="capacity"/> items numbered
        /// from zero.
        /// </summary>
        public UniformGridIndex(LogicalBounds area, int cellSize, int capacity)
        {
            if (cellSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cells must have a size.");
            }

            this.cellSize = cellSize;
            minX = area.MinX;
            minZ = area.MinZ;
            columns = Math.Max(1, (int)FloorDivide((long)area.MaxX - area.MinX, cellSize) + 1);
            rows = Math.Max(1, (int)FloorDivide((long)area.MaxZ - area.MinZ, cellSize) + 1);

            head = new int[columns * rows];
            next = new int[Math.Max(1, capacity)];
            itemCell = new int[Math.Max(1, capacity)];
            Capacity = capacity;
            Clear();
        }

        /// <summary>How many items this grid was built to hold.</summary>
        public int Capacity { get; }

        /// <summary>Takes every item out of the grid.</summary>
        public void Clear()
        {
            for (int i = 0; i < head.Length; i++)
            {
                head[i] = Unplaced;
            }

            for (int i = 0; i < itemCell.Length; i++)
            {
                itemCell[i] = Unplaced;
                next[i] = Unplaced;
            }
        }

        /// <summary>
        /// Puts an item at a position, moving it between cells if it has left
        /// the one it was in. Doing this every time something moves is what
        /// keeps the grid true partway through a tick.
        /// </summary>
        public void Place(int item, LogicalPosition position)
        {
            int cell = CellAt(position);
            int was = itemCell[item];
            if (was == cell)
            {
                return;
            }

            if (was != Unplaced)
            {
                Unlink(item, was);
            }

            Link(item, cell);
        }

        /// <summary>Takes one item out of the grid; placing it again puts it back.</summary>
        public void Remove(int item)
        {
            int cell = itemCell[item];
            if (cell != Unplaced)
            {
                Unlink(item, cell);
                itemCell[item] = Unplaced;
            }
        }

        /// <summary>
        /// Every item whose cell overlaps <paramref name="area"/>, written into
        /// <paramref name="into"/> in ascending item order, and how many there
        /// were. The answer is a superset of the items actually inside the
        /// area, because a cell is coarser than the area asked about.
        /// </summary>
        public int Gather(LogicalBounds area, int[] into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int firstColumn = Column(area.MinX);
            int lastColumn = Column(area.MaxX);
            int firstRow = Row(area.MinZ);
            int lastRow = Row(area.MaxZ);

            int count = 0;
            for (int row = firstRow; row <= lastRow; row++)
            {
                int rowStart = row * columns;
                for (int column = firstColumn; column <= lastColumn; column++)
                {
                    for (int item = head[rowStart + column]; item != Unplaced; item = next[item])
                    {
                        if (count == into.Length)
                        {
                            throw new InvalidOperationException(
                                "The gather buffer is smaller than the number of items in range. " +
                                "Buffers are sized to hold every item, so this means one was sized wrong.");
                        }

                        // Each cell is already ascending, so the merge only has
                        // to slide this item back past the tail of the previous
                        // cells -- in practice a step or two.
                        int at = count++;
                        while (at > 0 && into[at - 1] > item)
                        {
                            into[at] = into[at - 1];
                            at--;
                        }

                        into[at] = item;
                    }
                }
            }

            return count;
        }

        /// <summary>The area a circle of this radius around a point could reach.</summary>
        public static LogicalBounds Around(LogicalPosition position, long radius)
        {
            return new LogicalBounds(
                Saturate((long)position.X - radius), Saturate((long)position.X + radius),
                Saturate((long)position.Z - radius), Saturate((long)position.Z + radius));
        }

        /// <summary>The area a circle of this radius swept between two points could reach.</summary>
        public static LogicalBounds Sweeping(LogicalPosition start, LogicalPosition end, long radius)
        {
            return new LogicalBounds(
                Saturate(Math.Min(start.X, end.X) - radius), Saturate(Math.Max(start.X, end.X) + radius),
                Saturate(Math.Min(start.Z, end.Z) - radius), Saturate(Math.Max(start.Z, end.Z) + radius));
        }

        // ---------------------------------------------------------------- cells

        private void Link(int item, int cell)
        {
            itemCell[item] = cell;
            int previous = Unplaced;
            int at = head[cell];
            while (at != Unplaced && at < item)
            {
                previous = at;
                at = next[at];
            }

            next[item] = at;
            if (previous == Unplaced)
            {
                head[cell] = item;
            }
            else
            {
                next[previous] = item;
            }
        }

        private void Unlink(int item, int cell)
        {
            int at = head[cell];
            if (at == item)
            {
                head[cell] = next[item];
                next[item] = Unplaced;
                return;
            }

            while (at != Unplaced && next[at] != item)
            {
                at = next[at];
            }

            if (at != Unplaced)
            {
                next[at] = next[item];
            }

            next[item] = Unplaced;
        }

        private int CellAt(LogicalPosition position)
        {
            return Row(position.Z) * columns + Column(position.X);
        }

        private int Column(int x)
        {
            return Clamp((int)FloorDivide((long)x - minX, cellSize), columns);
        }

        private int Row(int z)
        {
            return Clamp((int)FloorDivide((long)z - minZ, cellSize), rows);
        }

        private static int Clamp(int value, int count)
        {
            return value < 0 ? 0 : (value >= count ? count - 1 : value);
        }

        /// <summary>Rounds toward negative infinity, so cells west and south of the origin are not folded together.</summary>
        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return value % divisor != 0L && (value < 0L) != (divisor < 0L) ? quotient - 1L : quotient;
        }

        /// <summary>Keeps a widened coordinate inside the range a position can hold.</summary>
        private static int Saturate(long value)
        {
            return value < int.MinValue ? int.MinValue : (value > int.MaxValue ? int.MaxValue : (int)value);
        }
    }
}

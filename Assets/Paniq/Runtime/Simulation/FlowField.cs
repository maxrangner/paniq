using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// How far it is to walk to one place from everywhere else in the building.
    ///
    /// The cost of reaching the goal is worked out for every square of floor at
    /// once, spreading outward from the goal the way water finds its level.
    /// Somebody who wants to get there then reads the cost of the squares
    /// around them and walks downhill. They need no route of their own, and a
    /// hundred people heading for the same door all read the same field, which
    /// is what makes a big crowd cost no more to steer than a small one.
    ///
    /// Costs are whole numbers: five to step to a neighbour, seven to step
    /// diagonally, which is five times the square root of two to within a
    /// couple of percent. A diagonal step is only allowed when both squares
    /// beside it are clear, so nobody slips through the corner between two
    /// tables.
    ///
    /// The result does not depend on the order squares are visited in -- the
    /// cheapest way to somewhere is the cheapest way however you find it -- so
    /// two runs of the same scenario always produce the same field.
    /// </summary>
    internal sealed class FlowField
    {
        /// <summary>Cost of stepping to the next square along.</summary>
        public const int StraightCost = 5;

        /// <summary>Cost of stepping to a corner square: five times the square root of two, near enough.</summary>
        public const int DiagonalCost = 7;

        /// <summary>No way to the goal from here.</summary>
        public const int Unreachable = int.MaxValue;

        /// <summary>
        /// How many cost buckets the queue needs: one more than the dearest
        /// single step.
        ///
        /// Squares are always taken cheapest-first, and a step only ever costs
        /// five or seven, so everything still waiting is within seven of the
        /// cost being dealt with. Eight buckets used round and round therefore
        /// hold the whole queue, however long the route.
        ///
        /// This used to be one bucket per possible cost, sized for a route no
        /// longer than the building is wide plus the building is tall. Any
        /// route that wound about -- through a corridor and back, or around a
        /// bank of desks -- cost more than that, and the squares past the end
        /// were quietly dropped: whole wings of a building reported as having
        /// no way to them, with nothing logged.
        /// </summary>
        private const int Buckets = DiagonalCost + 1;

        private readonly NavigationGrid grid;
        private readonly int[] cost;

        // Squares waiting to be looked at, held in buckets by their cost so the
        // cheapest is always found without sorting anything.
        private readonly int[] bucketHead = new int[Buckets];

        // Linked both ways, and each square remembers which bucket it is in.
        //
        // A square is queued again whenever a cheaper way to it turns up, and
        // with a single link per square the second queueing overwrote the
        // first: the bucket's chain was left pointing into another bucket's,
        // which lost squares, double-counted others and could run forever.
        // Being able to take a square out of the bucket it is in, in one step,
        // is what makes queueing it again safe.
        private readonly int[] nextInBucket;
        private readonly int[] previousInBucket;
        private readonly int[] queuedIn;

        /// <summary>How many squares are waiting to be looked at.</summary>
        private int waiting;

        public FlowField(NavigationGrid grid)
        {
            this.grid = grid;
            cost = new int[grid.CellCount];
            nextInBucket = new int[grid.CellCount];
            previousInBucket = new int[grid.CellCount];
            queuedIn = new int[grid.CellCount];
        }

        /// <summary>The square this field leads to, for recognising a field that is already built.</summary>
        public int Goal { get; private set; } = -1;

        /// <summary>The size of body it was worked out for; a wider body cannot use a narrower body's field.</summary>
        public int Radius { get; private set; } = -1;

        /// <summary>The tick it was last asked for, so the least wanted field is the one replaced.</summary>
        public int LastWantedTick { get; set; }

        /// <summary>What it costs to walk to the goal from this square, or <see cref="Unreachable"/>.</summary>
        public int CostAt(int cell) => cell < 0 || cell >= cost.Length ? Unreachable : cost[cell];

        /// <summary>True when there is any way to the goal from this square.</summary>
        public bool Reaches(int cell) => CostAt(cell) != Unreachable;

        /// <summary>
        /// The cost of every square, copied out, for somebody who wants to keep
        /// the answer without keeping the scratch space a field carries.
        /// </summary>
        public void CopyCostsTo(int[] into) => Array.Copy(cost, into, cost.Length);

        /// <summary>
        /// Works out the cost of reaching <paramref name="goal"/> from every
        /// square a body of this size could stand on.
        /// </summary>
        public void Build(int goal, int radius)
        {
            Goal = goal;
            Radius = radius;

            for (int i = 0; i < cost.Length; i++)
            {
                cost[i] = Unreachable;
                nextInBucket[i] = -1;
                previousInBucket[i] = -1;
                queuedIn[i] = -1;
            }

            for (int i = 0; i < Buckets; i++)
            {
                bucketHead[i] = -1;
            }

            waiting = 0;
            if (goal < 0 || !grid.Fits(goal, radius))
            {
                return;
            }

            cost[goal] = 0;
            Push(goal, 0);

            for (int reached = 0; waiting > 0; reached++)
            {
                int bucket = reached % Buckets;
                while (bucketHead[bucket] >= 0)
                {
                    int cell = bucketHead[bucket];
                    Unqueue(cell);

                    // Everything waiting costs between this and seven more, and
                    // there are eight buckets, so nothing in this one belongs
                    // to a later round.
                    Spread(cell, reached, radius);
                }
            }
        }

        private void Spread(int cell, int here, int radius)
        {
            int column = cell % grid.Columns;
            int row = cell / grid.Columns;

            bool north = Step(column, row + 1, here + StraightCost, radius);
            bool south = Step(column, row - 1, here + StraightCost, radius);
            bool east = Step(column + 1, row, here + StraightCost, radius);
            bool west = Step(column - 1, row, here + StraightCost, radius);

            // A corner step only when both squares beside it are clear, so
            // nobody slips diagonally between two tables that touch.
            if (north && east)
            {
                Step(column + 1, row + 1, here + DiagonalCost, radius);
            }

            if (north && west)
            {
                Step(column - 1, row + 1, here + DiagonalCost, radius);
            }

            if (south && east)
            {
                Step(column + 1, row - 1, here + DiagonalCost, radius);
            }

            if (south && west)
            {
                Step(column - 1, row - 1, here + DiagonalCost, radius);
            }
        }

        /// <summary>Offers a cheaper way to a neighbour; returns whether a body fits there at all.</summary>
        private bool Step(int column, int row, int reached, int radius)
        {
            if (column < 0 || row < 0 || column >= grid.Columns || row >= grid.Rows)
            {
                return false;
            }

            int cell = row * grid.Columns + column;
            if (!grid.Fits(cell, radius))
            {
                return false;
            }

            if (reached < cost[cell])
            {
                cost[cell] = reached;
                Push(cell, reached);
            }

            return true;
        }

        /// <summary>Queues a square at a cost, taking it out of wherever it was queued before.</summary>
        private void Push(int cell, int at)
        {
            if (queuedIn[cell] >= 0)
            {
                Unqueue(cell);
            }

            int bucket = at % Buckets;
            nextInBucket[cell] = bucketHead[bucket];
            previousInBucket[cell] = -1;
            if (bucketHead[bucket] >= 0)
            {
                previousInBucket[bucketHead[bucket]] = cell;
            }

            bucketHead[bucket] = cell;
            queuedIn[cell] = bucket;
            waiting++;
        }

        private void Unqueue(int cell)
        {
            int bucket = queuedIn[cell];
            int before = previousInBucket[cell];
            int after = nextInBucket[cell];
            if (before >= 0)
            {
                nextInBucket[before] = after;
            }
            else
            {
                bucketHead[bucket] = after;
            }

            if (after >= 0)
            {
                previousInBucket[after] = before;
            }

            nextInBucket[cell] = -1;
            previousInBucket[cell] = -1;
            queuedIn[cell] = -1;
            waiting--;
        }

        /// <summary>
        /// The cheapest neighbour of a square, or -1 at the goal or nowhere.
        /// Neighbours are tried in a fixed order, so the same field always
        /// gives the same next square.
        /// </summary>
        public int NextDownhill(int cell)
        {
            int here = CostAt(cell);
            if (here == Unreachable || here == 0)
            {
                return -1;
            }

            int column = cell % grid.Columns;
            int row = cell / grid.Columns;
            int best = -1;
            int bestCost = here;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nextColumn = column + dx;
                    int nextRow = row + dz;
                    if (nextColumn < 0 || nextRow < 0 || nextColumn >= grid.Columns || nextRow >= grid.Rows)
                    {
                        continue;
                    }

                    int neighbour = nextRow * grid.Columns + nextColumn;
                    int there = CostAt(neighbour);
                    if (there < bestCost)
                    {
                        bestCost = there;
                        best = neighbour;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Which way is downhill from this square, as a whole-degree heading.
        /// Taken from how the cost falls away across all eight neighbours
        /// together rather than from whichever single neighbour is cheapest: a
        /// single neighbour would put everybody on one of eight compass
        /// headings and a crowd would form visible diagonal lanes.
        ///
        /// Where two ways down are equally good, the one closest to the way the
        /// person is already facing wins, so nobody twitches between them as
        /// they cross from one square to the next.
        /// </summary>
        public int DownhillHeading(int cell, int currentHeading, out bool found)
        {
            found = false;
            int here = CostAt(cell);
            if (here == Unreachable || here == 0)
            {
                return currentHeading;
            }

            int column = cell % grid.Columns;
            int row = cell / grid.Columns;
            long steerX = 0L;
            long steerZ = 0L;
            int bestDrop = 0;
            int bestHeading = currentHeading;
            int bestTurn = int.MaxValue;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    int nextColumn = column + dx;
                    int nextRow = row + dz;
                    if (nextColumn < 0 || nextRow < 0 || nextColumn >= grid.Columns || nextRow >= grid.Rows)
                    {
                        continue;
                    }

                    int there = CostAt(nextRow * grid.Columns + nextColumn);
                    if (there == Unreachable || there >= here)
                    {
                        continue;
                    }

                    int drop = here - there;
                    steerX += (long)dx * drop;
                    steerZ += (long)dz * drop;

                    int heading = IntegerMath.HeadingOf(dx * IntegerMath.TrigScale, dz * IntegerMath.TrigScale, currentHeading);
                    int turn = Math.Abs(IntegerMath.SignedAngleDifference(currentHeading, heading));
                    if (drop > bestDrop || (drop == bestDrop && turn < bestTurn))
                    {
                        bestDrop = drop;
                        bestTurn = turn;
                        bestHeading = heading;
                    }
                }
            }

            if (steerX == 0L && steerZ == 0L)
            {
                found = bestDrop > 0;
                return bestHeading;
            }

            found = true;
            return IntegerMath.HeadingOf(steerX, steerZ, bestHeading);
        }
    }
}

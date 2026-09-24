using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// The one place that answers "which way from here to there?".
    ///
    /// Before this, the answer was always "point yourself straight at it and
    /// walk", which is why anything not reachable in a straight line -- a fire
    /// on the other side of a desk, an alarm through a doorway, a way out
    /// around a corner -- had to be cut out of the behaviour rather than fixed.
    ///
    /// Two answers, in order. If the person can see the place they are going,
    /// they walk straight at it: that keeps short journeys as smooth and direct
    /// as they are now, and it is also how a route across squares stops looking
    /// like a route across squares. Otherwise they follow a <see cref="FlowField"/>
    /// downhill, which bends round whatever is in the way.
    ///
    /// Fields are shared and kept: everybody heading for the same door reads
    /// one field, so a crowd of two hundred costs no more to steer than a
    /// crowd of twenty. The least recently wanted field is the one thrown away
    /// when a new one is needed.
    /// </summary>
    internal sealed class Navigation
    {
        /// <summary>How many fields are kept at once. Goals cluster on a handful of doors and corners.</summary>
        private const int FieldsKept = 32;

        /// <summary>
        /// How many fields may be worked out afresh in one tick.
        ///
        /// Working one out reads every square of floor, so without a limit a
        /// crowd who all want somewhere different -- a room full of people each
        /// strolling to their own spot -- would each pay for their own, and the
        /// cost would climb with the size of the crowd. With a limit the cost
        /// of a tick has a ceiling no matter how many people are in the
        /// building.
        ///
        /// Anybody who asks once the limit is used up is told to head straight
        /// at what they want for now, which is what everybody did before there
        /// were fields, and asks again next tick. It is a fixed count rather
        /// than a time limit on purpose: a time limit would make the same
        /// scenario play out differently on a faster machine.
        /// </summary>
        private const int FieldsBuiltPerTick = 8;

        /// <summary>
        /// How far ahead a person looks before trusting a field instead. Far
        /// enough to cross most rooms, short enough that checking it square by
        /// square costs little.
        /// </summary>
        private const int LookAheadMillimetres = 8000;

        /// <summary>How far down the field to look for a point to make straight for: four metres.</summary>
        private const int SmoothingSquares = 16;

        /// <summary>One in this many squares along the way is tested, to keep the cost down.</summary>
        private const int SmoothingCheckEvery = 4;

        /// <summary>
        /// How coarsely "where somebody is standing" is rounded when asking how
        /// far away everything else is. Two metres: near enough to rank what is
        /// nearest, coarse enough that a whole corner of a room shares one
        /// answer instead of everybody paying for their own.
        /// </summary>
        private const int ReachQuantumMillimetres = 2000;

        /// <summary>
        /// How far round somebody standing on a square no field reaches looks
        /// for one it does, in squares. Two is half a metre: a body pressed
        /// into a table's edge by a crowd, or with a table shoved into it.
        /// Further off than that, no field can help them.
        /// </summary>
        private const int RecoverySquares = 2;

        private readonly SimulationContext context;
        private readonly NavigationGrid grid;
        private readonly FlowField[] fields = new FlowField[FieldsKept];

        /// <summary>How many fields may still be worked out this tick.</summary>
        private int buildsLeft;

        /// <summary>The tick the budget was last refreshed for.</summary>
        private int budgetTick = -1;

        public Navigation(SimulationContext context, NavigationGrid grid)
        {
            this.context = context;
            this.grid = grid;
        }

        /// <summary>
        /// Throws away every route worked out so far, because the floor has
        /// changed shape: a wall blown through, a table shoved somewhere new.
        /// They are cheap to work out again, and a stale one sends people at a
        /// wall that is no longer there.
        /// </summary>
        public void Forget()
        {
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = null;
            }
        }

        /// <summary>How many fields have had to be worked out so far, for measuring.</summary>
        public int FieldsBuilt { get; private set; }

        /// <summary>
        /// The way to face to get from <paramref name="from"/> towards
        /// <paramref name="target"/>, going round whatever is in the way.
        /// Falls back to pointing straight at it when there is no way through
        /// at all, which is what everybody did before there were fields.
        /// </summary>
        public int HeadingToward(LogicalPosition from, LogicalPosition target, int radius, int currentHeading)
        {
            int straight = IntegerMath.HeadingBetween(from, target, currentHeading);
            if (IsTheWayAheadClear(from, target, radius))
            {
                return straight;
            }

            int here = grid.CellAt(from);
            int goal = grid.CellAt(target);
            if (here < 0 || goal < 0)
            {
                return straight;
            }

            FlowField field = FieldTo(goal, radius);
            if (field == null)
            {
                return straight;
            }

            int start = SquareToFollowFrom(field, here);
            if (start < 0)
            {
                return straight;
            }

            if (start != here)
            {
                // Off the field: the first step is onto it, and from there
                // the field takes over.
                return IntegerMath.HeadingBetween(from, grid.CentreOfCell(start), currentHeading);
            }

            LogicalPosition makeFor = FurthestPointStraightAhead(field, here, from, radius);
            if (makeFor.X != from.X || makeFor.Z != from.Z)
            {
                return IntegerMath.HeadingBetween(from, makeFor, currentHeading);
            }

            int downhill = field.DownhillHeading(here, currentHeading, out bool found);
            return found ? downhill : straight;
        }

        /// <summary>
        /// How far it is to walk from one place to another, in millimetres,
        /// going round what is in the way. Long.MaxValue when there is no way.
        /// This is a real walking distance, unlike measuring door centre to
        /// door centre in straight lines, which does not know a table is there.
        /// </summary>
        public long WalkingDistance(LogicalPosition from, LogicalPosition target, int radius)
        {
            int here = grid.CellAt(from);
            int goal = grid.CellAt(target);
            if (here < 0 || goal < 0)
            {
                return long.MaxValue;
            }

            FlowField field = FieldTo(goal, radius);
            if (field == null)
            {
                return long.MaxValue;
            }

            int start = SquareToFollowFrom(field, here);
            if (start < 0)
            {
                return long.MaxValue;
            }

            // Costs are in fifths of a square; a square is the grid's own size.
            return (long)field.CostAt(start) * NavigationGrid.CellSizeMillimetres / FlowField.StraightCost;
        }

        /// <summary>
        /// How far it is to walk to everywhere else from one place, worked out
        /// in one go. Null when this tick's share of the work is already spent.
        ///
        /// Walking distance is the same in both directions, so a field worked
        /// out from where somebody stands answers "how far to each of these?"
        /// for every candidate at once. That is what makes "the nearest chair",
        /// "the nearest alarm" and "the nearest fire worth fighting" mean the
        /// nearest one they could actually walk to, rather than the nearest one
        /// in the room they happen to be standing in.
        /// </summary>
        public FlowField ReachFrom(LogicalPosition from, int radius)
        {
            // Measured from roughly where they stand rather than exactly.
            //
            // A field is worth working out because it is shared, and a field
            // keyed on one person's exact position is shared with nobody --
            // not even with themselves a moment later, because they have
            // moved. Rounding to a couple of metres means everybody in the
            // same corner of the room asks the same question, and the answer
            // is still good enough to rank which chair is nearest.
            int rounded = grid.CellAt(new LogicalPosition(
                RoundTo(from.X, ReachQuantumMillimetres),
                RoundTo(from.Z, ReachQuantumMillimetres)));
            int here = rounded >= 0 && grid.Fits(rounded, radius) ? rounded : grid.CellAt(from);
            return here < 0 ? null : FieldTo(here, radius);
        }

        private static int RoundTo(int value, int step)
        {
            int down = value / step;
            if (value % step != 0 && value < 0)
            {
                down--;
            }

            return down * step + step / 2;
        }

        /// <summary>
        /// How far it is to walk to a place, read off a field from
        /// <see cref="ReachFrom"/>. Long.MaxValue when there is no way there.
        /// </summary>
        public long DistanceIn(FlowField reach, LogicalPosition to)
        {
            int cell = grid.CellAt(to);
            if (reach == null || cell < 0 || !reach.Reaches(cell))
            {
                return long.MaxValue;
            }

            return (long)reach.CostAt(cell) * NavigationGrid.CellSizeMillimetres / FlowField.StraightCost;
        }

        /// <summary>
        /// Whether there is any way at all from one place to another.
        ///
        /// False only when a way was genuinely looked for and none was found.
        /// When no field could be worked out this tick the honest answer is "no
        /// opinion", and the answer given is yes: somebody setting off and
        /// finding out on the way is what everybody did before there were
        /// fields, whereas answering no would have them abandon a perfectly
        /// good plan because the tick was busy.
        /// </summary>
        public bool CanGetFromHereToThere(LogicalPosition from, LogicalPosition target, int radius)
        {
            int here = grid.CellAt(from);
            int goal = grid.CellAt(target);
            if (here < 0 || goal < 0)
            {
                return true;
            }

            FlowField field = FieldTo(goal, radius);
            return field == null || SquareToFollowFrom(field, here) >= 0;
        }

        /// <summary>
        /// The square to follow a field from: the person's own, or, when the
        /// field does not reach it, the nearest one it does.
        ///
        /// Somebody pressed into a table's edge by a crowd, or with a table
        /// shoved into them, stands on floor too tight for a body, which no
        /// field reaches. That used to read as "no way from here", the fallback
        /// was to point straight at the goal, and on seed 41 a frightened
        /// person spent half a minute shoving the meeting table towards the
        /// door instead of stepping round it. The rings are walked in a fixed
        /// order and the cheapest square of the first ring with any wins, so
        /// a replay picks the same one. Only squares of the same room count,
        /// so nobody is sent through a wall to the floor beyond it; a square
        /// under the table itself belongs to no room, and from there any
        /// neighbour will do. -1 when nothing within reach is on the field.
        /// </summary>
        private int SquareToFollowFrom(FlowField field, int here)
        {
            if (field.Reaches(here))
            {
                return here;
            }

            int column = here % grid.Columns;
            int row = here / grid.Columns;
            short room = grid.RoomOfCell(here);
            for (int ring = 1; ring <= RecoverySquares; ring++)
            {
                int best = -1;
                int bestCost = int.MaxValue;
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring)
                        {
                            continue;
                        }

                        int nextColumn = column + dx;
                        int nextRow = row + dz;
                        if (nextColumn < 0 || nextRow < 0 || nextColumn >= grid.Columns || nextRow >= grid.Rows)
                        {
                            continue;
                        }

                        int cell = nextRow * grid.Columns + nextColumn;
                        if ((room != NavigationGrid.Outside && grid.RoomOfCell(cell) != room) || !field.Reaches(cell))
                        {
                            continue;
                        }

                        int cost = field.CostAt(cell);
                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            best = cell;
                        }
                    }
                }

                if (best >= 0)
                {
                    return best;
                }
            }

            return -1;
        }

        /// <summary>
        /// Follows the field a few squares down and returns the furthest one
        /// the person can walk straight to.
        ///
        /// Without this they would take the direction of the next square every
        /// tick, and a route across a grid looks like one: a faint constant
        /// weave, and -- because a body slows down to make a sharp turn -- a
        /// run that is measurably slower than a straight one. Aiming at the
        /// furthest square in plain sight turns the same route into a few long
        /// straight runs with a corner between them, which is how somebody
        /// crossing a room actually moves.
        /// </summary>
        private LogicalPosition FurthestPointStraightAhead(FlowField field, int here, LogicalPosition from, int radius)
        {
            int cell = here;
            int found = -1;
            for (int step = 0; step < SmoothingSquares; step++)
            {
                cell = field.NextDownhill(cell);
                if (cell < 0)
                {
                    break;
                }

                // Only every few squares, because each check walks the line.
                if (step % SmoothingCheckEvery == SmoothingCheckEvery - 1 &&
                    IsTheWayAheadClear(from, grid.CentreOfCell(cell), radius))
                {
                    found = cell;
                }
            }

            return found < 0 ? from : grid.CentreOfCell(found);
        }

        /// <summary>
        /// Nothing solid in the next stretch of the straight walk towards the
        /// goal. Checked every half square, which is finer than anything a body
        /// could slip through.
        ///
        /// This asks about the way ahead rather than about the whole journey on
        /// purpose. Somebody crossing an empty hall towards a door thirty
        /// metres away should walk straight at it, not pick their way down a
        /// grid; whatever is in the way further on is dealt with once they are
        /// near enough to see it, which is what a person does.
        /// </summary>
        private bool IsTheWayAheadClear(LogicalPosition from, LogicalPosition target, int radius)
        {
            long distance = IntegerMath.Distance(from, target);
            if (distance == 0L)
            {
                return true;
            }

            long looked = Math.Min(distance, LookAheadMillimetres);
            int steps = (int)(looked / (NavigationGrid.CellSizeMillimetres / 2)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                long along = looked * i / steps;
                var at = new LogicalPosition(
                    from.X + (int)((long)(target.X - from.X) * along / distance),
                    from.Z + (int)((long)(target.Z - from.Z) * along / distance));
                if (!grid.Fits(grid.CellAt(at), radius))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The field leading to this square, worked out now if it is not
        /// already kept. Fields are shared, so the cost of working one out is
        /// paid once however many people want it.
        /// </summary>
        private FlowField FieldTo(int goal, int radius)
        {
            int oldest = 0;
            int oldestWanted = int.MaxValue;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == null)
                {
                    oldest = i;
                    oldestWanted = int.MinValue;
                    continue;
                }

                if (fields[i].Goal == goal && fields[i].Radius == radius)
                {
                    fields[i].LastWantedTick = context.Tick;
                    return fields[i];
                }

                if (fields[i].LastWantedTick < oldestWanted)
                {
                    oldestWanted = fields[i].LastWantedTick;
                    oldest = i;
                }
            }

            if (budgetTick != context.Tick)
            {
                budgetTick = context.Tick;
                buildsLeft = FieldsBuiltPerTick;
            }

            if (buildsLeft <= 0)
            {
                return null;
            }

            buildsLeft--;
            fields[oldest] ??= new FlowField(grid);
            fields[oldest].Build(goal, radius);
            fields[oldest].LastWantedTick = context.Tick;
            FieldsBuilt++;
            return fields[oldest];
        }
    }
}

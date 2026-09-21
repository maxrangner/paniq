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
        /// A clear line this long or shorter is walked straight. Past that it is
        /// not worth checking square by square, and the field is the better
        /// answer anyway.
        /// </summary>
        private const int LineOfSightMillimetres = 8000;

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
            if (CanSeeStraightTo(from, target, radius))
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
            if (field == null || !field.Reaches(here))
            {
                return straight;
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
            if (field == null || !field.Reaches(here))
            {
                return long.MaxValue;
            }

            // Costs are in fifths of a square; a square is the grid's own size.
            return (long)field.CostAt(here) * NavigationGrid.CellSizeMillimetres / FlowField.StraightCost;
        }

        /// <summary>Whether there is any way at all from one place to another.</summary>
        public bool CanGetFromHereToThere(LogicalPosition from, LogicalPosition target, int radius)
        {
            return WalkingDistance(from, target, radius) != long.MaxValue;
        }

        /// <summary>
        /// A clear straight walk, with nothing solid in the way. Checked every
        /// half square, which is finer than anything a body could slip through.
        /// </summary>
        private bool CanSeeStraightTo(LogicalPosition from, LogicalPosition target, int radius)
        {
            long distance = IntegerMath.Distance(from, target);
            if (distance > LineOfSightMillimetres)
            {
                return false;
            }

            int steps = (int)(distance / (NavigationGrid.CellSizeMillimetres / 2)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                var at = new LogicalPosition(
                    from.X + (int)((long)(target.X - from.X) * i / steps),
                    from.Z + (int)((long)(target.Z - from.Z) * i / steps));
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

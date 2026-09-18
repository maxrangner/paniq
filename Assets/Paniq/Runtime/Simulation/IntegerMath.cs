using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Integer-only geometry for replayable movement. Headings are whole
    /// degrees measured clockwise from north (+Z), so 90 is east (+X); this
    /// matches Unity's yaw. Trigonometry comes from a fixed table rather than
    /// floating-point functions.
    /// </summary>
    public static class IntegerMath
    {
        /// <summary>Sin and Cos return values scaled by this factor.</summary>
        public const int TrigScale = 10000;

        // round(sin(d degrees) * 10000) for d = 0..90.
        private static readonly int[] QuarterSine =
        {
            0, 175, 349, 523, 698, 872, 1045, 1219, 1392, 1564, 1736, 1908, 2079, 2250, 2419, 2588, 2756, 2924,
            3090, 3256, 3420, 3584, 3746, 3907, 4067, 4226, 4384, 4540, 4695, 4848, 5000, 5150, 5299, 5446, 5592,
            5736, 5878, 6018, 6157, 6293, 6428, 6561, 6691, 6820, 6947, 7071, 7193, 7314, 7431, 7547, 7660, 7771,
            7880, 7986, 8090, 8192, 8290, 8387, 8480, 8572, 8660, 8746, 8829, 8910, 8988, 9063, 9135, 9205, 9272,
            9336, 9397, 9455, 9511, 9563, 9613, 9659, 9703, 9744, 9781, 9816, 9848, 9877, 9903, 9925, 9945, 9962,
            9976, 9986, 9994, 9998, 10000
        };

        public static int NormalizeDegrees(int degrees)
        {
            int normalized = degrees % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }

        public static int Sin(int degrees)
        {
            int d = NormalizeDegrees(degrees);
            if (d <= 90)
            {
                return QuarterSine[d];
            }

            if (d <= 180)
            {
                return QuarterSine[180 - d];
            }

            if (d <= 270)
            {
                return -QuarterSine[d - 180];
            }

            return -QuarterSine[360 - d];
        }

        public static int Cos(int degrees) => Sin(degrees + 90);

        public static int CardinalToDegrees(CardinalDirection direction) => (int)direction * 90;

        /// <summary>Shortest signed turn from one heading to another, in (-180, 180].</summary>
        public static int SignedAngleDifference(int fromDegrees, int toDegrees)
        {
            int difference = NormalizeDegrees(toDegrees - fromDegrees);
            return difference > 180 ? difference - 360 : difference;
        }

        /// <summary>Turns <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maximumStep"/> degrees.</summary>
        public static int TurnToward(int current, int target, int maximumStep)
        {
            int difference = SignedAngleDifference(current, target);
            if (Math.Abs(difference) <= maximumStep)
            {
                return NormalizeDegrees(target);
            }

            return NormalizeDegrees(current + Math.Sign(difference) * maximumStep);
        }

        /// <summary>Unit direction for a heading, scaled by <see cref="TrigScale"/>.</summary>
        public static LogicalPosition Direction(int headingDegrees)
        {
            return new LogicalPosition(Sin(headingDegrees), Cos(headingDegrees));
        }

        /// <summary>Displacement of <paramref name="distance"/> millimetres along a heading.</summary>
        public static LogicalPosition Displacement(int headingDegrees, int distance)
        {
            long x = (long)distance * Sin(headingDegrees) / TrigScale;
            long z = (long)distance * Cos(headingDegrees) / TrigScale;
            return new LogicalPosition(checked((int)x), checked((int)z));
        }

        /// <summary>
        /// Whole-degree heading of a vector, to within one degree. Returns
        /// <paramref name="fallback"/> for the zero vector.
        /// </summary>
        public static int HeadingOf(long dx, long dz, int fallback)
        {
            if (dx == 0L && dz == 0L)
            {
                return NormalizeDegrees(fallback);
            }

            long ax = Math.Abs(dx);
            long az = Math.Abs(dz);

            // Smallest angle a in [0, 90] with tan(a) >= ax / az, i.e.
            // sin(a) * az >= cos(a) * ax. The left side rises and the right
            // side falls with a, so a binary search finds it.
            int low = 0;
            int high = 90;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (checked(QuarterSine[middle] * az) >= checked(QuarterSine[90 - middle] * ax))
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            int angle = low;
            if (dx >= 0L)
            {
                return NormalizeDegrees(dz >= 0L ? angle : 180 - angle);
            }

            return NormalizeDegrees(dz >= 0L ? 360 - angle : 180 + angle);
        }

        public static long Sqrt(long value)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value < 2L)
            {
                return value;
            }

            long x = (long)Math.Sqrt(value);
            while (x * x > value)
            {
                x--;
            }

            while ((x + 1L) * (x + 1L) <= value)
            {
                x++;
            }

            return x;
        }

        /// <summary>
        /// True when the segment from <paramref name="start"/> to
        /// <paramref name="end"/> passes strictly closer than
        /// sqrt(<paramref name="distanceSquared"/>) to <paramref name="point"/>.
        /// Correct for moves in any direction; exact touching is allowed.
        /// </summary>
        public static bool SegmentPassesWithin(
            LogicalPosition start,
            LogicalPosition end,
            LogicalPosition point,
            long distanceSquared)
        {
            long abx = (long)end.X - start.X;
            long abz = (long)end.Z - start.Z;
            long apx = (long)point.X - start.X;
            long apz = (long)point.Z - start.Z;
            long lengthSquared = checked(abx * abx + abz * abz);
            long dot = checked(apx * abx + apz * abz);
            if (lengthSquared == 0L || dot <= 0L)
            {
                return checked(apx * apx + apz * apz) < distanceSquared;
            }

            if (dot >= lengthSquared)
            {
                return LogicalPosition.DistanceSquared(end, point) < distanceSquared;
            }

            // Perpendicular distance squared is cross^2 / length^2; compare
            // without dividing so the test stays exact.
            long cross = checked(abx * apz - abz * apx);
            return checked(cross * cross) < checked(distanceSquared * lengthSquared);
        }

        /// <summary>True when a circle swept along a segment overlaps the interior of a rectangle.</summary>
        public static bool SweptCircleOverlapsBounds(
            LogicalPosition start,
            LogicalPosition end,
            int radius,
            LogicalBounds bounds)
        {
            long radiusSquared = (long)radius * radius;
            if (bounds.DistanceSquaredTo(start) < radiusSquared || bounds.DistanceSquaredTo(end) < radiusSquared)
            {
                return true;
            }

            LogicalPosition a = new LogicalPosition(bounds.MinX, bounds.MinZ);
            LogicalPosition b = new LogicalPosition(bounds.MaxX, bounds.MinZ);
            LogicalPosition c = new LogicalPosition(bounds.MaxX, bounds.MaxZ);
            LogicalPosition d = new LogicalPosition(bounds.MinX, bounds.MaxZ);
            if (SegmentPassesWithin(start, end, a, radiusSquared) ||
                SegmentPassesWithin(start, end, b, radiusSquared) ||
                SegmentPassesWithin(start, end, c, radiusSquared) ||
                SegmentPassesWithin(start, end, d, radiusSquared))
            {
                return true;
            }

            // A long segment could cross straight through the rectangle
            // between corners without either end being near it.
            return SegmentsCross(start, end, a, b) || SegmentsCross(start, end, b, c) ||
                   SegmentsCross(start, end, c, d) || SegmentsCross(start, end, d, a);
        }

        private static long Cross(LogicalPosition origin, LogicalPosition first, LogicalPosition second)
        {
            return checked((long)(first.X - origin.X) * (second.Z - origin.Z) -
                           (long)(first.Z - origin.Z) * (second.X - origin.X));
        }

        private static bool SegmentsCross(
            LogicalPosition firstStart,
            LogicalPosition firstEnd,
            LogicalPosition secondStart,
            LogicalPosition secondEnd)
        {
            long d1 = Cross(firstStart, firstEnd, secondStart);
            long d2 = Cross(firstStart, firstEnd, secondEnd);
            long d3 = Cross(secondStart, secondEnd, firstStart);
            long d4 = Cross(secondStart, secondEnd, firstEnd);
            return ((d1 > 0L && d2 < 0L) || (d1 < 0L && d2 > 0L)) &&
                   ((d3 > 0L && d4 < 0L) || (d3 < 0L && d4 > 0L));
        }
    }
}

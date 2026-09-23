using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Reading the little green signs on the way out. A person running who
    /// catches sight of one takes its word for which way the door is: it pulls
    /// them the way it points, and it weighs on where they decide to head next.
    /// <para>
    /// The signs used to be scenery -- drawn for the player, ignored by
    /// everybody in the building. The floor's corridor runs its whole length
    /// and Ts at one end, so somebody coming out of a room had no way of
    /// telling which arm the door was up and would as often as not commit to
    /// the dead end. A sign is what a real floor gives them, and now it is what
    /// this one gives them too.
    /// </para>
    /// <para>
    /// Reading one causes nothing in the world and draws no random numbers, so
    /// it logs no events and cannot shift a replay by itself. What a sign
    /// teaches somebody who does not know the building -- the doors on the way
    /// out -- is the <see cref="WayfindingSystem"/>'s business.
    /// </para>
    /// </summary>
    internal sealed class ExitSignBehaviour
    {
        private readonly FireReactionExitSignDefinition[] signs;
        private readonly WorldGeometry geometry;
        private readonly PanicSettings settings;

        public ExitSignBehaviour(SimulationContext context, WorldGeometry geometry)
        {
            signs = context.Scenario.ExitSigns ?? new FireReactionExitSignDefinition[0];
            this.geometry = geometry;
            settings = context.Scenario.Panic;
        }

        /// <summary>
        /// Which way the nearest sign this person can see points, as a compass
        /// bearing. False when there is none in sight.
        /// </summary>
        public bool TryRead(Agent agent, out int pointingDegrees)
        {
            int sign = NearestReadable(agent);
            pointingDegrees = sign >= 0 ? signs[sign].PointingDegrees : 0;
            return sign >= 0;
        }

        /// <summary>How many signs the building has.</summary>
        public int Count => signs.Length;

        /// <summary>Where a sign hangs.</summary>
        public LogicalPosition At(int sign) => signs[sign].At;

        /// <summary>Which way a sign points, as a compass bearing.</summary>
        public int PointingOf(int sign) => signs[sign].PointingDegrees;

        /// <summary>
        /// The nearest sign this person can read, or -1.
        /// <para>
        /// Readable means: near enough to read, inside the cone they are
        /// looking down, and in their room or a room theirs stands open to --
        /// nobody reads a sign through a wall. Ties go to the sign written
        /// first, so two signs the same distance off always read the same way.
        /// </para>
        /// </summary>
        public int NearestReadable(Agent agent)
        {
            long range = settings.SignReadRangeMillimetres;
            if (signs.Length == 0 || range <= 0L)
            {
                return -1;
            }

            LogicalPosition eye = agent.Body.Position;
            LogicalPosition looking = IntegerMath.Direction(agent.Body.Heading);
            long rangeSquared = range * range;
            int room = geometry.RoomAtPoint(eye);
            long nearest = long.MaxValue;
            int found = -1;
            for (int i = 0; i < signs.Length; i++)
            {
                LogicalPosition at = signs[i].At;
                long distanceSquared = LogicalPosition.DistanceSquared(eye, at);
                if (distanceSquared >= nearest || !InVisionCone(eye, looking, at, rangeSquared))
                {
                    continue;
                }

                int signRoom = geometry.RoomAtPoint(at);
                if (room >= 0 && signRoom >= 0 && signRoom != room && !geometry.RoomsOpenToEachOther(room, signRoom))
                {
                    continue;
                }

                nearest = distanceSquared;
                found = i;
            }

            return found;
        }

        /// <summary>
        /// What a spot is worth to somebody who has read a sign, in
        /// millimetres, to add to how that spot already scores: the full bonus
        /// for lying exactly the way the sign points, nothing for lying square
        /// to it, and the same again off for lying the other way entirely.
        /// </summary>
        public long ScoreToward(LogicalPosition from, LogicalPosition candidate, int pointingDegrees)
        {
            long bonus = settings.SignEscapeBonusMillimetres;
            if (bonus <= 0L)
            {
                return 0L;
            }

            return bonus * Agreement(from, candidate, pointingDegrees) / IntegerMath.TrigScale;
        }

        /// <summary>
        /// How far the way from one point to another agrees with the way a
        /// sign points: a whole <see cref="IntegerMath.TrigScale"/> for exactly
        /// the same way, nothing for square to it, and minus that for the
        /// opposite way. Nothing, too, for two points on top of each other.
        /// </summary>
        public static long Agreement(LogicalPosition from, LogicalPosition to, int pointingDegrees)
        {
            long offsetX = (long)to.X - from.X;
            long offsetZ = (long)to.Z - from.Z;
            long distance = IntegerMath.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
            if (distance <= 0L)
            {
                return 0L;
            }

            LogicalPosition way = IntegerMath.Direction(pointingDegrees);
            return (offsetX * way.X + offsetZ * way.Z) / distance;
        }

        /// <summary>Near enough to read and inside the 45-degree cone they are looking down.</summary>
        private static bool InVisionCone(LogicalPosition eye, LogicalPosition looking, LogicalPosition point,
            long rangeSquared)
        {
            long offsetX = (long)point.X - eye.X;
            long offsetZ = (long)point.Z - eye.Z;
            if (checked(offsetX * offsetX + offsetZ * offsetZ) > rangeSquared)
            {
                return false;
            }

            long forward = checked(offsetX * looking.X + offsetZ * looking.Z);
            long lateral = checked(offsetX * looking.Z - offsetZ * looking.X);
            return forward >= 0L && Math.Abs(lateral) <= forward;
        }
    }
}

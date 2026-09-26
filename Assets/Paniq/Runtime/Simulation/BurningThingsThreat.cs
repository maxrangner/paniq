namespace Paniq.Simulation
{
    /// <summary>
    /// Things on fire -- a waste bin, a chair, a box -- as a danger in their
    /// own right (prototype 3, 2026-09-26). Before this only burning floor
    /// squares frightened anybody, so a bin blazing in a room full of people
    /// went unnoticed until the carpet caught. The owner's rule: people sense
    /// danger, never "floor on fire", so that fire, a monster and whatever
    /// comes after are all the same question to them.
    /// <para>
    /// It answers the crowd's questions about the things
    /// <see cref="FlammablesSystem"/> has burning: where the nearest one is,
    /// whether one is in this room, whether one is in sight. What touching one
    /// does -- setting you alight -- stays with the flammables, so
    /// <see cref="Touching"/> is always 0 and nobody is harmed twice.
    /// </para>
    /// It draws no random numbers.
    /// </summary>
    internal sealed class BurningThingsThreat : IThreat
    {
        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly FlammablesSystem flammables;

        public BurningThingsThreat(SimulationContext context, WorldGeometry geometry, FlammablesSystem flammables)
        {
            this.context = context;
            this.geometry = geometry;
            this.flammables = flammables;
        }

        public bool Active => flammables.BurningCount > 0;

        /// <summary>Things catch fire; nobody starts them as a disaster of their own.</summary>
        public bool StartRequested => false;

        public void RequestStart()
        {
        }

        /// <summary>The first thing still alight: what a fright at the sight of burning things names as its cause.</summary>
        public ulong RootEventId => flammables.BurningCount > 0 ? flammables.AlightEventId(0) : 0UL;

        public int Count => flammables.BurningCount;

        public long Signature => flammables.BurningCount;

        /// <summary>A thing burning crackles like a square of burning floor does.</summary>
        public int HeardWithinMillimetres => flammables.BurningCount > 0 ? context.Scenario.Hearing.FireHearingRadiusMillimetres : 0;

        public void Advance()
        {
        }

        public long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId) =>
            flammables.NearestBurning(from, out point, out causeEventId);

        public bool AnyCloserThan(LogicalPosition position, int distance)
        {
            long reach = (long)distance * distance;
            for (int k = 0; k < flammables.BurningCount; k++)
            {
                if (LogicalPosition.DistanceSquared(position, flammables.AlightNearestPoint(k, position)) < reach)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AnyCloserThanInRooms(LogicalPosition position, int distance, int roomA, int roomB)
        {
            long reach = (long)distance * distance;
            for (int k = 0; k < flammables.BurningCount; k++)
            {
                int room = geometry.RoomAtPoint(flammables.AlightPosition(k));
                if ((room == roomA || room == roomB) &&
                    LogicalPosition.DistanceSquared(position, flammables.AlightNearestPoint(k, position)) < reach)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsInRoom(int room) => room >= 0 && flammables.AnythingBurningInRoom(room);

        /// <summary>In the same 45-degree cone the fire is seen in, within range, along a line of sight through open doorways only.</summary>
        public bool IsVisibleFrom(LogicalPosition eye, int heading, int range)
        {
            long rangeSquared = (long)range * range;
            LogicalPosition direction = IntegerMath.Direction(heading);
            int eyeRoom = geometry.RoomAtPoint(eye);
            for (int k = 0; k < flammables.BurningCount; k++)
            {
                LogicalPosition at = flammables.AlightPosition(k);
                if (LogicalPosition.DistanceSquared(eye, at) > rangeSquared || !InVisionCone(eye, direction, at) ||
                    !geometry.CanSeeBetween(eyeRoom, eye, geometry.RoomAtPoint(at), at))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance)
        {
            for (int quarter = 1; quarter <= 3; quarter++)
            {
                var point = new LogicalPosition(
                    (int)(from.X + ((long)to.X - from.X) * quarter / 4),
                    (int)(from.Z + ((long)to.Z - from.Z) * quarter / 4));
                if (AnyCloserThan(point, clearance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Touching a burning thing sets you alight, but that is the flammables' rule, not this one's.</summary>
        public ulong Touching(LogicalPosition position) => 0UL;

        public ulong TouchingAlong(LogicalPosition from, LogicalPosition to) => 0UL;

        public void Harm(Agent agent, ulong causeEventId, BodySystem body)
        {
        }

        /// <summary>A 45-degree half-angle either side of the way they face.</summary>
        internal static bool InVisionCone(LogicalPosition eye, LogicalPosition direction, LogicalPosition point)
        {
            long offsetX = (long)point.X - eye.X;
            long offsetZ = (long)point.Z - eye.Z;
            long forward = checked(offsetX * direction.X + offsetZ * direction.Z);
            long lateral = checked(offsetX * direction.Z - offsetZ * direction.X);
            return forward >= 0L && System.Math.Abs(lateral) <= forward;
        }
    }
}

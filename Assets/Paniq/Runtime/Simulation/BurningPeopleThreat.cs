using System.Collections.Generic;

namespace Paniq.Simulation
{
    /// <summary>
    /// Somebody on fire, as a danger the people around them can see
    /// (prototype 3, 2026-09-26, the owner's rule that people sense danger
    /// rather than "floor on fire"). Before this, a person alight frightened
    /// nobody who merely saw them; only their screams did.
    /// <para>
    /// A burning person moves, so they are a danger to look at and to keep
    /// away from, not a place: they never make a room count as "on fire"
    /// (<see cref="IsInRoom"/>), never make a route through the heat, never
    /// eat a door, and never count as "danger right beside me"
    /// (<see cref="AnyCloserThan"/>) -- which is what lets the kind still run
    /// up to them with an extinguisher. Setting others alight on contact stays
    /// with <see cref="BurningBehaviour"/>.
    /// </para>
    /// <para>
    /// Who is alight is read once a tick, in phase 2, in ascending person
    /// order, so a question asked five hundred times a tick walks the few
    /// people burning rather than the whole crowd. A person never counts as a
    /// danger to themselves: a question asked from exactly where somebody
    /// burning stands is theirs, and they are skipped.
    /// </para>
    /// It draws no random numbers.
    /// </summary>
    internal sealed class BurningPeopleThreat : IThreat
    {
        private readonly SimulationContext context;
        private readonly WorldGeometry geometry;
        private readonly Crowd crowd;
        private readonly List<int> burning = new List<int>();

        public BurningPeopleThreat(SimulationContext context, WorldGeometry geometry, Crowd crowd)
        {
            this.context = context;
            this.geometry = geometry;
            this.crowd = crowd;
        }

        public bool Active => burning.Count > 0;

        public bool StartRequested => false;

        public void RequestStart()
        {
        }

        public ulong RootEventId => burning.Count > 0 ? crowd.All[burning[0]].Burning.EventId : 0UL;

        public int Count => burning.Count;

        public long Signature => burning.Count;

        /// <summary>Their screams are their own noise (see <see cref="BurningBehaviour"/>); this adds none.</summary>
        public int HeardWithinMillimetres => 0;

        /// <summary>Phase 2: who is alight this tick.</summary>
        public void Advance()
        {
            burning.Clear();
            Agent[] agents = crowd.All;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].IsParticipating && agents[i].Burning.IsBurning)
                {
                    burning.Add(i);
                }
            }
        }

        public long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId)
        {
            long nearest = long.MaxValue;
            point = from;
            causeEventId = 0UL;
            for (int k = 0; k < burning.Count; k++)
            {
                Agent other = crowd.All[burning[k]];
                if (!IsSomebodyElse(other, from))
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(from, other.Body.Position);
                if (distance < nearest)
                {
                    nearest = distance;
                    point = other.Body.Position;
                    causeEventId = other.Burning.EventId;
                }
            }

            return nearest;
        }

        /// <summary>Never: a burning person is to be kept away from, not a patch of floor too hot to stand beside.</summary>
        public bool AnyCloserThan(LogicalPosition position, int distance) => false;

        public bool AnyCloserThanInRooms(LogicalPosition position, int distance, int roomA, int roomB) => false;

        public bool IsInRoom(int room) => false;

        public bool IsVisibleFrom(LogicalPosition eye, int heading, int range)
        {
            long rangeSquared = (long)range * range;
            LogicalPosition direction = IntegerMath.Direction(heading);
            int eyeRoom = geometry.RoomAtPoint(eye);
            for (int k = 0; k < burning.Count; k++)
            {
                Agent other = crowd.All[burning[k]];
                LogicalPosition at = other.Body.Position;
                if (!IsSomebodyElse(other, eye) || LogicalPosition.DistanceSquared(eye, at) > rangeSquared ||
                    !BurningThingsThreat.InVisionCone(eye, direction, at) ||
                    !geometry.CanSeeBetween(eyeRoom, eye, geometry.RoomAtPoint(at), at))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance) => false;

        public ulong Touching(LogicalPosition position) => 0UL;

        public ulong TouchingAlong(LogicalPosition from, LogicalPosition to) => 0UL;

        public void Harm(Agent agent, ulong causeEventId, BodySystem body)
        {
        }

        /// <summary>Two bodies never stand on exactly the same millimetre, so a question asked from there is the burning person's own.</summary>
        private static bool IsSomebodyElse(Agent burner, LogicalPosition askedFrom) =>
            burner.Body.Position.X != askedFrom.X || burner.Body.Position.Z != askedFrom.Z;
    }
}

namespace Paniq.Simulation
{
    /// <summary>Everyone in the run, in ascending ID order, and questions about who is where.</summary>
    internal sealed class Crowd
    {
        private readonly long touchingSquared;

        public Crowd(Agent[] agents, int occupancyRadius)
        {
            All = agents;
            long touching = (long)occupancyRadius * 2L;
            touchingSquared = touching * touching;
        }

        /// <summary>Ascending ID order; the order every per-person loop uses.</summary>
        public Agent[] All { get; }

        /// <summary>The person with this ID, or -1 if the run has no such person.</summary>
        public int IndexOf(SimulationId id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The lowest-ID participating person, other than <paramref name="mover"/>, that this move would pass through; or null.</summary>
        public Agent FindBlocking(Agent mover, LogicalPosition start, LogicalPosition destination)
        {
            for (int i = 0; i < All.Length; i++)
            {
                Agent other = All[i];
                if (other == mover || !other.IsParticipating)
                {
                    continue;
                }

                if (IntegerMath.SegmentPassesWithin(start, destination, other.Body.Position, touchingSquared))
                {
                    return other;
                }
            }

            return null;
        }
    }
}

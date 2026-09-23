using System;

namespace Paniq.Simulation
{
    /// <summary>
    /// Everyone in the run, in ascending ID order, and questions about who is
    /// where. The crowd also owns the index of who is standing in which patch
    /// of floor, so that those questions do not have to read everybody: it is
    /// kept true by moving people through <see cref="MoveTo"/> rather than by
    /// writing their position directly.
    /// </summary>
    internal sealed class Crowd
    {
        /// <summary>
        /// How wide a patch of floor one cell covers. A little wider than the
        /// questions usually asked of it -- personal space is 800 mm and a step
        /// is at most 120 mm -- so a typical question reads four cells and each
        /// cell holds only the two or three people who fit in a square metre.
        /// </summary>
        private const int CellSizeMillimetres = 1000;

        /// <summary>Room for people who have walked out of the building before they stop taking part.</summary>
        private const int OutsideMarginMillimetres = 8000;

        private readonly long touchingSquared;
        private readonly long touching;
        private readonly UniformGridIndex index;

        /// <summary>
        /// Reused by <see cref="Gather"/>. One buffer per level of nesting,
        /// because a few rules ask a second question while walking the answer
        /// to the first.
        /// </summary>
        private readonly int[][] gathered;
        private int gatherDepth;

        public Crowd(Agent[] agents, int occupancyRadius, LogicalBounds worldArea)
        {
            All = agents;
            touching = (long)occupancyRadius * 2L;
            touchingSquared = touching * touching;

            var area = new LogicalBounds(
                worldArea.MinX - OutsideMarginMillimetres, worldArea.MaxX + OutsideMarginMillimetres,
                worldArea.MinZ - OutsideMarginMillimetres, worldArea.MaxZ + OutsideMarginMillimetres);
            index = new UniformGridIndex(area, CellSizeMillimetres, agents.Length);

            gathered = new int[4][];
            for (int i = 0; i < gathered.Length; i++)
            {
                gathered[i] = new int[agents.Length];
            }

            for (int i = 0; i < agents.Length; i++)
            {
                index.Place(i, agents[i].Body.Position);
                indexById[agents[i].Id] = i;
            }
        }

        /// <summary>Ascending ID order; the order every per-person loop uses.</summary>
        public Agent[] All { get; }

        /// <summary>
        /// Moves somebody, and tells the index at the same moment. This is the
        /// only way a body moves: the index has to be true partway through a
        /// tick, because a movement resolved for one person is what the next
        /// person's movement is checked against.
        /// </summary>
        public void MoveTo(Agent agent, LogicalPosition position)
        {
            agent.Body.MoveWithoutTellingTheCrowd(position);
            index.Place(agent.Index, position);
        }

        /// <summary>The person with this ID, or -1 if the run has no such person.</summary>
        public int IndexOf(SimulationId id) => indexById.TryGetValue(id, out int index) ? index : -1;

        /// <summary>Everybody by ID, built once, so a command naming a person is not a walk down the crowd.</summary>
        private readonly System.Collections.Generic.Dictionary<SimulationId, int> indexById =
            new System.Collections.Generic.Dictionary<SimulationId, int>();

        /// <summary>
        /// The people who might be inside <paramref name="area"/>, in ascending
        /// order, written into a buffer belonging to the crowd. The list is a
        /// superset: every caller still applies its own exact test. Dispose the
        /// returned handle -- a <c>using</c> statement does -- so the buffer can
        /// be used again.
        /// </summary>
        public Nearby Gather(LogicalBounds area)
        {
            if (gatherDepth == gathered.Length)
            {
                throw new InvalidOperationException(
                    "Too many crowd questions are open at once; add another buffer if a rule really needs to nest this deep.");
            }

            int[] buffer = gathered[gatherDepth++];
            return new Nearby(this, buffer, index.Gather(area, buffer));
        }

        /// <summary>Everyone who might be within <paramref name="radius"/> of a point.</summary>
        public Nearby Within(LogicalPosition position, long radius)
        {
            return Gather(UniformGridIndex.Around(position, radius));
        }

        /// <summary>
        /// The lowest-ID participating person, other than
        /// <paramref name="mover"/>, that this move would pass through; or null.
        /// </summary>
        public Agent FindBlocking(Agent mover, LogicalPosition start, LogicalPosition destination)
        {
            using (Nearby candidates = Gather(UniformGridIndex.Sweeping(start, destination, touching)))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    Agent other = All[candidates[i]];
                    if (other == mover || !other.IsParticipating)
                    {
                        continue;
                    }

                    if (IntegerMath.SegmentPassesWithin(start, destination, other.Body.Position, touchingSquared))
                    {
                        // Ascending order, so the first match is the lowest ID.
                        return other;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the index still describes where everybody actually is.
        /// Nothing in a run calls this: it is how a test proves that no new way
        /// of moving somebody has been written that forgets to say so.
        /// </summary>
        internal bool IndexMatchesPositions()
        {
            for (int i = 0; i < All.Length; i++)
            {
                using Nearby here = Within(All[i].Body.Position, 0L);
                bool found = false;
                for (int n = 0; n < here.Count && !found; n++)
                {
                    found = here[n] == i;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleaseGatherBuffer()
        {
            gatherDepth--;
        }

        /// <summary>
        /// A borrowed list of people who might be in some area, in ascending
        /// order. A struct, so asking costs no allocation.
        /// </summary>
        internal readonly struct Nearby : IDisposable
        {
            private readonly Crowd crowd;
            private readonly int[] items;

            public Nearby(Crowd crowd, int[] items, int count)
            {
                this.crowd = crowd;
                this.items = items;
                Count = count;
            }

            /// <summary>How many people are in the list.</summary>
            public int Count { get; }

            /// <summary>The index into <see cref="All"/> of the nth person in the list.</summary>
            public int this[int i] => items[i];

            public void Dispose()
            {
                crowd.ReleaseGatherBuffer();
            }
        }
    }
}

using System;
using System.Diagnostics;
using Paniq.Simulation;

namespace Paniq.Gameplay
{
    /// <summary>
    /// What would have happened if the player had never clicked (2026-09-27,
    /// the owner's choice): the same building on the same seed, played with
    /// none of the player's help, until the round is over, so the end card
    /// can say "left alone, N of 20 would have lived" beside the player's own
    /// score.
    /// <para>
    /// A run replays exactly from its seed, so this is not a guess: it is the
    /// round the player was handed, without them. It is built with the scene
    /// and keeps pace with the real round until the disaster starts, so that
    /// when the player presses Trigger event it can be told the same tick
    /// (<see cref="MirrorTrigger"/>): when the disaster starts is part of the
    /// round both are compared on, not help. Every other click is left out.
    /// Once the disaster is under way it runs ahead, a few dozen ticks a
    /// frame and never more than a few milliseconds' work, in a physics scene
    /// of its own, and is thrown away as soon as it has its answer. It lives
    /// in the gameplay layer because it decides nothing in the player's round.
    /// </para>
    /// <para>
    /// One caveat, recorded in the decision log: the physics engine spread
    /// over several threads does not always replay a busy seed to the same
    /// answer, so on the odd seed this may differ from a truly hands-off play
    /// by a person or so. For a comparison line, that is close enough.
    /// </para>
    /// </summary>
    public sealed class LeftAloneRunner : IDisposable
    {
        /// <summary>A round that has not ended by itself in four minutes of game time is called here.</summary>
        public const int DefaultCapTicks = 12000;

        private Run run;
        private readonly int capTicks;
        private int ticksPlayed;

        public LeftAloneRunner(ScenarioData data, ulong seed, int capTicks = DefaultCapTicks)
        {
            if (capTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capTicks));
            }

            this.capTicks = capTicks;
            run = new Run(data, seed);
        }

        /// <summary>How many of the crowd lived, once known; null while still working it out.</summary>
        public int? SavedCount { get; private set; }

        /// <summary>How many were in the crowd, once known.</summary>
        public int CrowdSize { get; private set; }

        public bool IsDone => SavedCount.HasValue;

        /// <summary>The tick the hands-off round has reached; the cap once it is done.</summary>
        public int Tick => run?.Tick ?? capTicks;

        /// <summary>
        /// The player pressed Trigger event for this tick: the hands-off round
        /// starts its disaster on the same one. Ignored if this round has
        /// already passed that tick, or is done.
        /// </summary>
        public void MirrorTrigger(int tick)
        {
            if (run != null && tick > run.Tick)
            {
                run.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), tick);
            }
        }

        /// <summary>Plays up to <paramref name="ticks"/> more ticks. Returns true once the answer is in.</summary>
        public bool Advance(int ticks) => Advance(ticks, int.MaxValue, 0L);

        /// <summary>
        /// Plays up to <paramref name="ticks"/> more ticks, never beyond
        /// <paramref name="noFurtherThanTick"/>, and stops early once
        /// <paramref name="budgetStopwatchTicks"/> of work have been spent (0:
        /// no budget, at least one tick is always played when allowed).
        /// Returns true once the answer is in, at which point the run has been
        /// let go of.
        /// </summary>
        public bool Advance(int ticks, int noFurtherThanTick, long budgetStopwatchTicks)
        {
            if (IsDone)
            {
                return true;
            }

            long started = Stopwatch.GetTimestamp();
            for (int i = 0; i < ticks && ticksPlayed < capTicks && run.Phase != RoundPhase.Over && run.Tick < noFurtherThanTick; i++)
            {
                run.Step();
                ticksPlayed++;
                if (budgetStopwatchTicks > 0L && Stopwatch.GetTimestamp() - started >= budgetStopwatchTicks)
                {
                    break;
                }
            }

            if (run.Phase != RoundPhase.Over && ticksPlayed < capTicks)
            {
                return false;
            }

            RunSnapshot snapshot = run.NewSnapshotBuffer();
            run.FillSnapshot(snapshot);

            // Stopped at the cap rather than over: everybody still alive
            // counts as having lived, as they would at a round's own end.
            SavedCount = run.Phase == RoundPhase.Over ? snapshot.SavedCount : snapshot.CrowdSize - snapshot.LostCount;
            CrowdSize = snapshot.CrowdSize;
            Dispose();
            return true;
        }

        public void Dispose()
        {
            run?.Dispose();
            run = null;
        }
    }
}

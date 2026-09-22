using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// The one thing that outlives a run: which seed to play next, and the
    /// best score ever managed on each level.
    /// <para>
    /// Playing again throws the whole run away and builds a fresh one, which
    /// means reloading the scene, which means everything in the scene is
    /// destroyed. This is deliberately not in the scene, so it is still here
    /// afterwards to say what the new run should be.
    /// </para>
    /// <para>
    /// Nothing here is simulation state. It chooses the seed a run is built
    /// with, before tick zero; once a run exists it never reads this again.
    /// </para>
    /// </summary>
    public static class LevelSession
    {
        private const string BestScoreKeyPrefix = "paniq.best.";

        /// <summary>The seed the next run is built with, or none to use the level's own.</summary>
        public static ulong? RequestedSeed { get; private set; }

        /// <summary>
        /// Whether the next run should start playing straight away rather than
        /// waiting behind the start card. Set when the player presses "play
        /// again", so they are not asked to press Play twice.
        /// </summary>
        public static bool StartImmediately { get; private set; }

        /// <summary>The seed the run now on screen was built with, for the display and for playing it again.</summary>
        public static ulong CurrentSeed { get; private set; }

        /// <summary>Chooses the seed for the next run. Zero is refused: a scenario seed is never zero.</summary>
        public static void RequestSeed(ulong seed, bool startImmediately = false)
        {
            if (seed == 0UL)
            {
                return;
            }

            RequestedSeed = seed;
            StartImmediately = startImmediately;
        }

        /// <summary>Back to the level's own seed for the next run.</summary>
        public static void ClearRequestedSeed()
        {
            RequestedSeed = null;
            StartImmediately = false;
        }

        /// <summary>
        /// The seed a run should use: the one asked for, or the level's own.
        /// Records it as the current seed so the display can show it and the
        /// player can ask for the same one again.
        /// </summary>
        public static ulong TakeSeedFor(LevelDefinition level)
        {
            ulong seed = RequestedSeed ?? (level != null ? level.DefaultSeed : 0UL);
            if (seed == 0UL)
            {
                seed = 42UL;
            }

            CurrentSeed = seed;
            return seed;
        }

        /// <summary>A seed nobody chose, for the "random" button. Presentation only; no run is built from it yet.</summary>
        public static ulong RandomSeed()
        {
            // Never zero, and spread across the whole range rather than the
            // small numbers a single Range call would give.
            uint high = (uint)Random.Range(1, int.MaxValue);
            uint low = (uint)Random.Range(1, int.MaxValue);
            return ((ulong)high << 32) | low;
        }

        /// <summary>The best share of a crowd ever saved on this level, or zero if it has never been played.</summary>
        public static int BestPercentFor(string levelId)
        {
            return string.IsNullOrEmpty(levelId) ? 0 : PlayerPrefs.GetInt(BestScoreKeyPrefix + levelId, 0);
        }

        /// <summary>
        /// Records a finished round. Returns true if it beat the previous
        /// best, so the end card can say so.
        /// </summary>
        public static bool RecordResult(string levelId, int savedPercent)
        {
            if (string.IsNullOrEmpty(levelId) || savedPercent <= BestPercentFor(levelId))
            {
                return false;
            }

            PlayerPrefs.SetInt(BestScoreKeyPrefix + levelId, savedPercent);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Forgets a level's best score. For tests, and for a player who wants a clean slate.</summary>
        public static void ForgetBest(string levelId)
        {
            if (!string.IsNullOrEmpty(levelId))
            {
                PlayerPrefs.DeleteKey(BestScoreKeyPrefix + levelId);
            }
        }
    }
}

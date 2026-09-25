using Paniq.App;
using UnityEngine.SceneManagement;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Starting a level again. The run owns a physics world of its own and a
    /// scene full of built geometry, so rather than trying to put everything
    /// back how it was, the whole scene is thrown away and loaded fresh. What
    /// the next run should be is left in <see cref="LevelSession"/>, which is
    /// not in the scene and so survives the reload.
    /// </summary>
    public static class LevelLoader
    {
        /// <summary>The same seed again: the same fire, in the same place, with the same people.</summary>
        public static void PlayAgain()
        {
            LevelSession.RequestSeed(LevelSession.CurrentSeed, true);
            Reload();
        }

        /// <summary>A different day in the same building.</summary>
        public static void PlayWithSeed(ulong seed)
        {
            LevelSession.RequestSeed(seed, true);
            Reload();
        }

        /// <summary>Back to the start card, so the seed can be chosen before anything moves.</summary>
        public static void BackToTheStart(ulong seed)
        {
            LevelSession.RequestSeed(seed);
            Reload();
        }

        private static void Reload()
        {
            SceneManager.LoadScene(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
        }
    }
}

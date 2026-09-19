using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.App
{
    /// <summary>
    /// Keeps startup separate from playable scenes. Future app-level setup belongs
    /// here; the prototype currently advances straight to the fire-reaction scene.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        public const string FireReactionPrototypeSceneName = "FireReactionPrototype";
        public const string DevelopmentSceneName = "Development";

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == FireReactionPrototypeSceneName)
            {
                return;
            }

            SceneManager.LoadSceneAsync(FireReactionPrototypeSceneName, LoadSceneMode.Single);
        }
    }
}

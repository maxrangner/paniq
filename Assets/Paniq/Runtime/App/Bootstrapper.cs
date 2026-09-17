using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.App
{
    /// <summary>
    /// Keeps startup separate from playable scenes. Future app-level setup belongs
    /// here; the foundation currently advances straight to the development scene.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        public const string DevelopmentSceneName = "Development";

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == DevelopmentSceneName)
            {
                return;
            }

            SceneManager.LoadSceneAsync(DevelopmentSceneName, LoadSceneMode.Single);
        }
    }
}

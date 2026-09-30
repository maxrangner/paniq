using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.App
{
    /// <summary>
    /// Keeps startup separate from playable scenes. Future app-level setup belongs
    /// here; the prototype currently advances straight to the one playable
    /// scene, named for what it is (2026-09-30, the owner): a fire, one floor,
    /// small.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        public const string PrototypeSceneName = "prototype_fire_1_fl_small";
        public const string DevelopmentSceneName = "Development";

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == PrototypeSceneName)
            {
                return;
            }

            SceneManager.LoadSceneAsync(PrototypeSceneName, LoadSceneMode.Single);
        }
    }
}

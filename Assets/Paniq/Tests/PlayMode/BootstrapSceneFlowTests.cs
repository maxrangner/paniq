using System.Collections;
using NUnit.Framework;
using Paniq.App;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paniq.Tests.PlayMode
{
    public sealed class BootstrapSceneFlowTests
    {
        private const float SceneLoadTimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator BootstrapScene_LoadsDevelopmentScene()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

            var deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != Bootstrapper.DevelopmentSceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(Bootstrapper.DevelopmentSceneName),
                $"Bootstrap did not load {Bootstrapper.DevelopmentSceneName} within {SceneLoadTimeoutSeconds} seconds.");
            Assert.That(Object.FindFirstObjectByType<DevelopmentSceneBootstrap>(), Is.Not.Null);
        }
    }
}

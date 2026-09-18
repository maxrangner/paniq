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
        public IEnumerator BootstrapScene_LoadsFireReactionPrototypeScene()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

            var deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while (SceneManager.GetActiveScene().name != Bootstrapper.FireReactionPrototypeSceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(Bootstrapper.FireReactionPrototypeSceneName),
                $"Bootstrap did not load {Bootstrapper.FireReactionPrototypeSceneName} within {SceneLoadTimeoutSeconds} seconds.");
            Assert.That(Object.FindFirstObjectByType<Paniq.Gameplay.FireReactionRunner>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Paniq.Presentation.FireReactionPrototypePresentation>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FireReactionPrototype_ShowsTheFireAfterItsAuthoredDelay()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.FireReactionRunner runner = Object.FindFirstObjectByType<Paniq.Gameplay.FireReactionRunner>();
            Assert.That(runner, Is.Not.Null);
            for (int tick = 0; tick < runner.Scenario.FireActivationTick; tick++)
            {
                runner.StepForTests();
            }

            yield return null;

            Assert.That(runner.Snapshot.FireActive, Is.True);
            GameObject fire = GameObject.Find("Fire cell 1 (read-only presentation)");
            Assert.That(fire, Is.Not.Null);
            Assert.That(fire.activeSelf, Is.True);
            Assert.That(fire.transform.childCount, Is.GreaterThanOrEqualTo(3), "Expected a scorch tile plus flame cubes.");
        }
    }
}

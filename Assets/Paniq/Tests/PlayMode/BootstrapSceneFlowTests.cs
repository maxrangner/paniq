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
            for (int tick = 0; tick < runner.Simulation.Scenario.Fire.ActivationTick; tick++)
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

        [UnityTest]
        public IEnumerator FireReactionPrototype_DoorClicksUnlockThenOpen()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.FireReactionRunner runner = Object.FindFirstObjectByType<Paniq.Gameplay.FireReactionRunner>();
            Assert.That(runner, Is.Not.Null);
            GameObject leaf = GameObject.Find("Door 2001 (click target)");
            Assert.That(leaf, Is.Not.Null, "Expected a clickable door leaf in the north wall.");
            Assert.That(leaf.GetComponent<Collider>(), Is.Not.Null, "The door leaf needs a collider to be clicked.");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None),
                Has.Some.Property("name").EqualTo("Box 3001 (presentation)"));

            var door = new Paniq.Simulation.SimulationId(2001UL);
            runner.QueueDoorClick(door);
            runner.StepForTests();
            Assert.That(DoorState(runner, door), Is.EqualTo(Paniq.Simulation.DoorState.Unlocked));
            runner.QueueDoorClick(door);
            runner.StepForTests();
            Assert.That(DoorState(runner, door), Is.EqualTo(Paniq.Simulation.DoorState.Open));

            // Let the leaf swing open on screen.
            float before = leaf.transform.parent.eulerAngles.y;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Mathf.Abs(Mathf.DeltaAngle(before, leaf.transform.parent.eulerAngles.y)) < 80f &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before, leaf.transform.parent.eulerAngles.y)), Is.GreaterThan(80f),
                "The opened door did not swing open.");
        }

        private static Paniq.Simulation.DoorState DoorState(
            Paniq.Gameplay.FireReactionRunner runner,
            Paniq.Simulation.SimulationId door)
        {
            foreach (Paniq.Simulation.FireReactionDoorSnapshot snapshot in runner.Snapshot.Doors)
            {
                if (snapshot.DoorId == door)
                {
                    return snapshot.State;
                }
            }

            throw new System.Collections.Generic.KeyNotFoundException(door.ToString());
        }
    }
}

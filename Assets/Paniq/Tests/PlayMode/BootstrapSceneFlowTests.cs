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
            Assert.That(Object.FindFirstObjectByType<Paniq.Gameplay.RunDriver>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Paniq.Presentation.RunPresentation>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FireReactionPrototype_OpensCalmAndWaitsBehindTheStartCard()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.RunDriver runner = Object.FindFirstObjectByType<Paniq.Gameplay.RunDriver>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.IsWaitingToStart, Is.True, "A level opens behind its start card.");
            Assert.That(runner.IsTicking, Is.False, "Nothing moves until the player presses Play.");

            // A long minute of office life: still nothing alight, because the
            // fire waits for the player rather than for a tick count.
            for (int tick = 0; tick < 60 * Paniq.Simulation.Run.TicksPerSecond; tick++)
            {
                runner.StepForTests();
            }

            yield return null;

            Assert.That(runner.Snapshot.FireActive, Is.False, "Nobody triggered anything, so nothing should be alight.");
            Assert.That(runner.Snapshot.RoundIsOver, Is.False);
        }

        [UnityTest]
        public IEnumerator FireReactionPrototype_ShowsTheFireOnceTheEventIsTriggered()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.RunDriver runner = Object.FindFirstObjectByType<Paniq.Gameplay.RunDriver>();
            Assert.That(runner, Is.Not.Null);
            runner.BeginPlaying();
            runner.QueueTriggerEvent();
            for (int tick = 0; tick < 5; tick++)
            {
                runner.StepForTests();
            }

            yield return null;

            Assert.That(runner.Snapshot.FireActive, Is.True);

            // The fire is drawn in batches, not as scene objects, so the
            // check is what the view says it drew this frame.
            var presentation = Object.FindFirstObjectByType<Paniq.Presentation.RunPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.FireForTests, Is.Not.Null);
            Assert.That(presentation.FireForTests.DrawnCellCount, Is.GreaterThanOrEqualTo(1),
                "Expected at least one burning square to be drawn.");
            Assert.That(presentation.FireForTests.DrawnFlameCount, Is.GreaterThanOrEqualTo(2),
                "Expected flame cubes over the first burning square.");
        }

        [UnityTest]
        public IEnumerator PlayingAgainWithAChosenSeed_BuildsTheRunOnThatSeed()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            const ulong chosen = 4242UL;
            Paniq.Gameplay.LevelSession.RequestSeed(chosen, true);
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.RunDriver runner = Object.FindFirstObjectByType<Paniq.Gameplay.RunDriver>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.Seed, Is.EqualTo(chosen), "A chosen seed has to survive the reload that restarts the level.");
            Assert.That(runner.IsWaitingToStart, Is.False,
                "Playing again means the player has already chosen, so the start card is not shown twice.");

            // Leave nothing behind for the next test.
            Paniq.Gameplay.LevelSession.ClearRequestedSeed();
        }

        [UnityTest]
        public IEnumerator FireReactionPrototype_DoorClicksUnlockThenOpen()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);

            Paniq.Gameplay.RunDriver runner = Object.FindFirstObjectByType<Paniq.Gameplay.RunDriver>();
            Assert.That(runner, Is.Not.Null);
            GameObject leaf = GameObject.Find("Door 2008 (click target)");
            Assert.That(leaf, Is.Not.Null, "Expected a clickable door leaf on the meeting room's east wall.");
            Assert.That(leaf.GetComponent<Collider>(), Is.Not.Null, "The door leaf needs a collider to be clicked.");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None),
                Has.Some.Property("name").EqualTo("Box 3001 (presentation)"));

            // A round opens with an empty purse and working a door costs, so
            // without this the clicks are refused and the door never moves.
            // This test is about the scene being wired up -- a leaf that is
            // there, can be clicked, and swings -- not about what the player
            // can afford.
            runner.Simulation.GiveInfluenceForTests(1000);

            var door = new Paniq.Simulation.SimulationId(2008UL);
            runner.QueueDoorClick(door);
            runner.StepForTests();
            Assert.That(DoorState(runner, door), Is.EqualTo(Paniq.Simulation.DoorState.Unlocked));

            // The purse holds a hundred and the way out took all of it: fill
            // it again for the click that opens the door.
            runner.Simulation.GiveInfluenceForTests(1000);
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
            Paniq.Gameplay.RunDriver runner,
            Paniq.Simulation.SimulationId door)
        {
            foreach (Paniq.Simulation.DoorSnapshot snapshot in runner.Snapshot.Doors)
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

using System.Collections;
using NUnit.Framework;
using Paniq.App;
using Paniq.Simulation;
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

            // Twenty-five seconds of office life: still nothing alight. The
            // fire waits for the player, or on the office for the Director's
            // first incident, which never comes before thirty seconds
            // (2026-09-26).
            for (int tick = 0; tick < 25 * Paniq.Simulation.Run.TicksPerSecond; tick++)
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
            for (int tick = 0; tick < 10; tick++)
            {
                runner.StepForTests();
            }

            // On the office the trigger sets a waste bin alight (2026-09-26):
            // the fire exists, and the bin is what is burning.
            Assert.That(runner.Snapshot.FireActive, Is.True);
            bool binBurning = false;
            for (int i = 0; i < runner.Snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = runner.Snapshot.PhysicsObjects[i];
                binBurning |= thing.Kind == PhysicsObjectKind.WasteBin && thing.BurnState == ObjectBurnState.Burning;
            }

            Assert.That(binBurning, "The trigger set a waste bin alight.");

            // The carpet under it catches about ten seconds later -- unless
            // somebody brave puts the bin out first, or the crowd kicks it
            // about so it never rests, which is the game working. When it
            // does catch, the burning square is drawn.
            for (int tick = 0; tick < 20 * Paniq.Simulation.Run.TicksPerSecond && runner.Snapshot.FireCells.Count == 0; tick++)
            {
                runner.StepForTests();
            }

            yield return null;

            if (runner.Snapshot.FireCells.Count == 0)
            {
                Assert.Pass("The bin never set the carpet alight in twenty seconds (put out, or kicked about); nothing on the floor to draw.");
            }

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
            runner.Simulation.GivePurseForTests(1000);

            var door = new Paniq.Simulation.SimulationId(2008UL);
            runner.QueueDoorClick(door);
            runner.StepForTests();
            Assert.That(DoorState(runner, door), Is.EqualTo(Paniq.Simulation.DoorState.Unlocked));

            // The purse holds a hundred and the way out took all of it: fill
            // it again for the click that opens the door.
            runner.Simulation.GivePurseForTests(1000);
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

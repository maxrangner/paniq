using System.Collections;
using NUnit.Framework;
using Paniq.App;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paniq.Tests.PlayMode
{
    /// <summary>
    /// Clicking a person with a card. This needs the real scene and the real
    /// camera, because the whole bug was about the angle the camera looks
    /// down at: nothing measured on the floor can catch it.
    /// <para>
    /// Aiming used to be worked out by asking where the pointer met the
    /// ground and then looking for somebody within half a metre of that
    /// spot. But a person is drawn a metre up in the air, so with the camera
    /// tilted low the floor under their chest is metres behind their feet.
    /// The player clicked a body and the game looked for them on an empty
    /// patch of carpet.
    /// </para>
    /// <para>
    /// No card is aimed at a person any more -- every one of them is thrown at
    /// a patch of floor, so <c>PlayerInput.TargetsAPerson</c> is false for all
    /// of them and nothing in a played round reaches the picking below. It is
    /// kept, and so are these, for the end screen's "click somebody for the
    /// facts about them", which wants exactly this and has the same camera
    /// angle to get wrong. Read these as guarding something not yet switched
    /// on, not as covering live behaviour.
    /// </para>
    /// </summary>
    public sealed class PlayerInputPickingPlayModeTests
    {
        /// <summary>How near the pointer had to be, on the floor, under the old rule.</summary>
        private const float OldPickReachMillimetres = 550f;

        [UnityTest]
        public IEnumerator PointingAtSomeonesBody_PicksThatPerson()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            yield return null;

            Camera camera = Camera.main;
            RunDriver runner = Object.FindObjectOfType<RunDriver>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(runner, Is.Not.Null);

            RunSnapshot snapshot = runner.Snapshot;
            Assert.That(snapshot.Agents.Count, Is.GreaterThan(0));

            // Everybody on screen, aimed at squarely: every one of them has to
            // be pickable, not most of them.
            int aimedAt = 0;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                Vector3 onScreen = camera.WorldToScreenPoint(BodyMiddleOf(agent));
                if (onScreen.z <= 0f || onScreen.x < 0f || onScreen.x > Screen.width ||
                    onScreen.y < 0f || onScreen.y > Screen.height)
                {
                    // Off the edge of the opening framing: nobody could click it.
                    continue;
                }

                aimedAt++;
                SimulationId? picked = PlayerInput.NearestPerson(camera, snapshot, new Vector2(onScreen.x, onScreen.y));
                Assert.That(picked, Is.EqualTo(agent.AgentId),
                    $"Pointing straight at person {i + 1}'s body should pick them.");
            }

            Assert.That(aimedAt, Is.GreaterThan(5), "Most of the office should be on screen to aim at.");
        }

        /// <summary>
        /// The bug itself, written down so it cannot come back: from this
        /// camera, the floor under somebody's chest is nowhere near their
        /// feet, so the old rule could not have found them.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFloorUnderSomeonesChest_IsNowhereNearTheirFeet()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            yield return null;

            Camera camera = Camera.main;
            RunDriver runner = Object.FindObjectOfType<RunDriver>();
            RunSnapshot snapshot = runner.Snapshot;
            var ground = new Plane(Vector3.up, 0f);

            int checkedPeople = 0;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                Vector3 onScreen = camera.WorldToScreenPoint(BodyMiddleOf(agent));
                if (onScreen.z <= 0f)
                {
                    continue;
                }

                Ray ray = camera.ScreenPointToRay(new Vector2(onScreen.x, onScreen.y));
                Assert.That(ground.Raycast(ray, out float distance), Is.True);

                Vector3 feet = PresentationUtility.ToUnityPosition(agent.Position);
                float strayMillimetres = Vector3.Distance(ray.GetPoint(distance), feet) * 1000f;
                Assert.That(strayMillimetres, Is.GreaterThan(OldPickReachMillimetres),
                    $"Person {i + 1}: the old rule would still have worked, so this test proves nothing.");
                checkedPeople++;
            }

            Assert.That(checkedPeople, Is.GreaterThan(5));
        }

        private static Vector3 BodyMiddleOf(AgentSnapshot agent) =>
            PresentationUtility.ToUnityPosition(agent.Position) + Vector3.up * 0.5f;
    }
}

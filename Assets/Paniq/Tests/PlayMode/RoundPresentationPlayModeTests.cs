using System.Collections;
using NUnit.Framework;
using Paniq.App;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paniq.Tests.PlayMode
{
    /// <summary>
    /// Smoke checks for what the round put on screen: the camera the player
    /// drives actually frames the building, and the building's outside is
    /// built below the floor rather than in front of it.
    /// <para>
    /// These do not judge whether it looks good -- nothing can -- but they do
    /// catch a camera pointing at nothing or a shell drawn over the rooms,
    /// which are the two ways this could be badly wrong without anybody
    /// noticing in a test.
    /// </para>
    /// </summary>
    public sealed class RoundPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator TheCamera_FramesTheBuildingFromAboveAndCornerOn()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True, "The view is orthographic, per look-and-controls.");
            Assert.That(camera.transform.position.y, Is.GreaterThan(5f), "The camera looks down on the building.");
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.1f), "It has to be tilted downward, not level.");

            // Corner-on: the view direction runs diagonally across both floor
            // axes, so two walls recede each way rather than one facing us.
            Vector3 flat = new Vector3(camera.transform.forward.x, 0f, camera.transform.forward.z).normalized;
            Assert.That(Mathf.Abs(flat.x), Is.GreaterThan(0.3f).And.LessThan(0.95f),
                $"The view should be corner-on, but its flat direction is {flat}.");
            Assert.That(Mathf.Abs(flat.z), Is.GreaterThan(0.3f).And.LessThan(0.95f),
                $"The view should be corner-on, but its flat direction is {flat}.");

            // Every room's middle has to be on screen at the opening framing.
            foreach (GameObject floor in FloorsInTheScene())
            {
                Vector3 viewport = camera.WorldToViewportPoint(floor.transform.position);
                Assert.That(viewport.z, Is.GreaterThan(0f), $"{floor.name} is behind the camera.");
                Assert.That(viewport.x, Is.InRange(0f, 1f), $"{floor.name} is off the side of the screen.");
                Assert.That(viewport.y, Is.InRange(0f, 1f), $"{floor.name} is off the top or bottom of the screen.");
            }
        }

        [UnityTest]
        public IEnumerator TheBuildingsOutside_IsBuiltBelowTheFloor()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            yield return null;

            GameObject shell = GameObject.Find("Building shell");
            Assert.That(shell, Is.Not.Null, "The building's outside should have been built.");
            Assert.That(shell.transform.childCount, Is.GreaterThan(8),
                "Expected a slab, a window band with uprights, a spandrel and the storey below.");

            foreach (Transform part in shell.transform)
            {
                Assert.That(part.position.y, Is.LessThan(0f),
                    $"'{part.name}' is at or above floor level, where it would cover the rooms.");
            }

            // No part of it may poke up through the floor into a room.
            foreach (Renderer part in shell.GetComponentsInChildren<Renderer>())
            {
                Assert.That(part.bounds.max.y, Is.LessThanOrEqualTo(0.01f),
                    $"'{part.name}' pokes up through the floor, where it would cover the room above it.");
            }

            // The slab has to overhang the rooms, or the floor appears to float.
            Bounds rooms = RoomBounds();
            Renderer slab = null;
            foreach (Transform part in shell.transform)
            {
                if (part.name == "Floor slab")
                {
                    slab = part.GetComponent<Renderer>();
                }
            }

            Assert.That(slab, Is.Not.Null, "Expected a slab under the floor.");
            Assert.That(slab.bounds.min.x, Is.LessThan(rooms.min.x));
            Assert.That(slab.bounds.max.x, Is.GreaterThan(rooms.max.x));
            Assert.That(slab.bounds.min.z, Is.LessThan(rooms.min.z));
            Assert.That(slab.bounds.max.z, Is.GreaterThan(rooms.max.z));
        }

        [UnityTest]
        public IEnumerator TheOpeningView_ActuallyDrawsSomething()
        {
            yield return SceneManager.LoadSceneAsync(Bootstrapper.FireReactionPrototypeSceneName, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForEndOfFrame();

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);

            var target = new RenderTexture(320, 180, 24);
            var shot = new Texture2D(320, 180, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;

                // The background is one flat dark colour. If the building is
                // being drawn, a decent share of the frame is not that colour.
                Color background = camera.backgroundColor;
                int different = 0;
                Color[] pixels = shot.GetPixels();
                foreach (Color pixel in pixels)
                {
                    if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) +
                        Mathf.Abs(pixel.b - background.b) > 0.05f)
                    {
                        different++;
                    }
                }

                float share = different / (float)pixels.Length;
                Assert.That(share, Is.GreaterThan(0.1f),
                    $"Only {share:P0} of the frame is anything but the background: the camera is looking at nothing.");
                Assert.That(share, Is.LessThan(0.98f),
                    $"{share:P0} of the frame is filled: something is drawn right across the view.");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(shot);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static System.Collections.Generic.List<GameObject> FloorsInTheScene()
        {
            var floors = new System.Collections.Generic.List<GameObject>();
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform.name.EndsWith(" floor"))
                {
                    floors.Add(transform.gameObject);
                }
            }

            Assert.That(floors, Is.Not.Empty, "Expected the rooms' floors to have been built.");
            return floors;
        }

        private static Bounds RoomBounds()
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (GameObject floor in FloorsInTheScene())
            {
                Renderer renderer = floor.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }
    }
}

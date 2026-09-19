using UnityEngine;

namespace Paniq.App
{
    /// <summary>
    /// Temporary, code-created geometry proving that the development scene can
    /// render without committing authored level content before the slice exists.
    /// </summary>
    public sealed class DevelopmentSceneBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            CreateCamera();
            CreateLight();
            CreateGround();
            CreateMarker(new Vector3(-2f, 0.5f, 0f), new Color(0.35f, 0.18f, 0.2f));
            CreateMarker(new Vector3(0f, 0.5f, 0f), new Color(0.75f, 0.62f, 0.22f));
            CreateMarker(new Vector3(2f, 0.5f, 0f), new Color(0.2f, 0.38f, 0.5f));
        }

        private static void CreateCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Development Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 5f, -8f),
                Quaternion.LookRotation(new Vector3(0f, -0.45f, 1f)));
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Development Light", typeof(Light));
            var sceneLight = lightObject.GetComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Placeholder Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        private static void CreateMarker(Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "Placeholder Crowd Member";
            marker.transform.position = position;
            marker.GetComponent<Renderer>().material.color = color;
        }
    }
}

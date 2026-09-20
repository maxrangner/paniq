using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>Small helpers shared by the prototype's display classes. Presentation only.</summary>
    internal static class PresentationUtility
    {
        /// <summary>
        /// The layer for anything that should still be visible, faintly,
        /// when a wall is between it and the camera. The renderer draws this
        /// layer twice: once normally, once as a see-through silhouette
        /// wherever something is in front of it (see ConfigureUrpProject).
        /// </summary>
        public const string SeeThroughLayer = "SeeThrough";

        public static float Metres(int millimetres) => millimetres / (float)FireReactionSimulation.MillimetresPerMetre;

        /// <summary>Puts an object, and everything under it, on the see-through layer.</summary>
        public static void ShowThroughWalls(GameObject gameObject)
        {
            int layer = LayerMask.NameToLayer(SeeThroughLayer);
            if (layer < 0)
            {
                // The project has not been set up for it yet; drawn normally.
                return;
            }

            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                ShowThroughWalls(child.gameObject);
            }
        }

        public static Vector3 ToUnityPosition(LogicalPosition position)
        {
            return new Vector3(Metres(position.X), 0f, Metres(position.Z));
        }

        /// <summary>A primitive with no collider, so it can never take part in physics or clicks.</summary>
        public static GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(gameObject);
            return gameObject;
        }

        public static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }

        /// <summary>The yaw that turns local +X to face <paramref name="direction"/>.</summary>
        public static float YawOf(Vector3 direction)
        {
            return Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
        }

        /// <summary>Stable 0..1 variation from grid coordinates; presentation-only, never the simulation generator.</summary>
        public static float Hash01(int x, int z, int salt)
        {
            unchecked
            {
                uint hash = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995U;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFFU) / 16777216f;
            }
        }

        public static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        public static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}

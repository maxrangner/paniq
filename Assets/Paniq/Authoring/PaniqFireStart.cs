using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// Where the fire begins. The exact square is drawn from the scenario's
    /// seed somewhere inside this patch, so the same seed always starts the
    /// same fire and a different seed starts a different one.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Fire Start")]
    public sealed class PaniqFireStart : MonoBehaviour
    {
        [Tooltip("Seconds before it starts.")]
        public float StartsAfterSeconds = 5f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Vector2Int size = PaniqAuthoring.SizeMillimetres(transform);
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, 0.1f, transform.position.z),
                new Vector3(
                    size.x / (float)PaniqAuthoring.MillimetresPerMetre,
                    0.1f,
                    size.y / (float)PaniqAuthoring.MillimetresPerMetre));
        }
    }
}

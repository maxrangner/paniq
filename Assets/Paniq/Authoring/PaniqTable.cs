using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// A solid piece of furniture people walk around and things bounce off: a
    /// desk, a counter, a meeting table. Scale it to its footprint.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Table")]
    public sealed class PaniqTable : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long TableId = 4001L;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 0.3f, 0.9f);
            Vector2Int size = PaniqAuthoring.SizeMillimetres(transform);
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, 0.05f, transform.position.z),
                new Vector3(
                    size.x / (float)PaniqAuthoring.MillimetresPerMetre,
                    0.05f,
                    size.y / (float)PaniqAuthoring.MillimetresPerMetre));
        }
    }
}

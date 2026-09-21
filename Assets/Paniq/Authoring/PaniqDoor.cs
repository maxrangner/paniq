using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// A doorway. Drop it on the wall of a room and the baker works out which
    /// room's wall it is in, which side of that room, and where along it -- so
    /// sliding it along a wall, or onto a different wall, is all there is to do.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Door")]
    public sealed class PaniqDoor : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long DoorId = 2001L;

        [Tooltip("How wide the gap is, in millimetres. A person is 500 across, so 600 is a squeeze.")]
        public int WidthMillimetres = 1000;

        [Tooltip("Locked to start with: the player has to click it before anybody can open it.")]
        public bool StartsLocked = true;

        private void OnDrawGizmos()
        {
            Gizmos.color = StartsLocked ? new Color(1f, 0.35f, 0.3f) : new Color(0.4f, 1f, 0.5f);
            float half = WidthMillimetres / 2f / PaniqAuthoring.MillimetresPerMetre;
            Vector3 at = transform.position;
            Gizmos.DrawLine(at + new Vector3(-half, 0.05f, 0f), at + new Vector3(half, 0.05f, 0f));
            Gizmos.DrawLine(at + new Vector3(0f, 0.05f, -half), at + new Vector3(0f, 0.05f, half));
        }
    }
}

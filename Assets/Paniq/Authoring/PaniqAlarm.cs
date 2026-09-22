using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// A fire alarm on a wall. Somebody who has seen the fire hits it, and then
    /// every bell in the building rings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Alarm")]
    public sealed class PaniqAlarm : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long AlarmId = 6001L;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, 1.2f, transform.position.z),
                new Vector3(0.2f, 0.25f, 0.2f));
        }
    }
}

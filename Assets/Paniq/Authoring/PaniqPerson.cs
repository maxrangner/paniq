using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// Somebody in the building. Leave the personality alone and it is drawn
    /// from the scenario's seed; tick the box and this person is always the
    /// same, whatever the seed.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Person")]
    public sealed class PaniqPerson : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long AgentId = 1001L;

        [Tooltip("Give this person a personality of your own rather than one drawn from the seed.")]
        public bool AuthorTheirPersonality;

        [Range(0, 10)] public int Strength = 5;
        [Range(0, 10)] public int Speed = 5;
        [Range(0, 10)] public int Bravery = 5;
        [Range(0, 10)] public int Compassion = 5;
        [Range(0, 10)] public int Evil = 2;
        [Range(0, 10)] public int Nervousness = 5;
        [Range(0, 10)] public int Leadership = 4;

        [Tooltip("A visitor: knows only the room they start in, and has to find the way out by looking, " +
                 "reading the signs and following people who know.")]
        public bool Visitor;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.9f, 0.95f);
            var head = new Vector3(transform.position.x, 0.9f, transform.position.z);
            Gizmos.DrawWireSphere(head, 0.25f);
            Gizmos.DrawLine(head, head + transform.forward * 0.6f);
        }
    }
}

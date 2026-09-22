using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// A loose thing on the floor: a box, a chair, a bin, a laptop, an
    /// extinguisher. What it is made of and what it is for come from its kind,
    /// so all that is needed here is which kind, how big and how heavy.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Prop")]
    public sealed class PaniqProp : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long ObjectId = 3001L;

        [Tooltip("Which kind of thing this is. Its kind decides how it slides, burns and behaves.")]
        public Paniq.Simulation.PhysicsObjectKind Kind = Paniq.Simulation.PhysicsObjectKind.Box;

        [Tooltip("How wide it is, in millimetres.")]
        public int SizeMillimetres = 400;

        [Tooltip("How heavy it is, in grams. A cardboard box is about 12000.")]
        public int MassGrams = 12000;

        [Tooltip("Standing on a table or another thing rather than on the floor.")]
        public bool StartsResting;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.9f, 0.5f, 0.9f);
            float size = SizeMillimetres / (float)PaniqAuthoring.MillimetresPerMetre;
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, size / 2f, transform.position.z),
                new Vector3(size, size, size));
        }
    }
}

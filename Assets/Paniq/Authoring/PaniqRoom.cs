using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// A room, laid out by dragging and scaling this object in the scene. The
    /// floor it covers is its position and its width and depth; height is
    /// ignored, because the simulation is flat.
    ///
    /// Put one of these on a cube, scale it to the shape of the room, and give
    /// it a number nothing else in the building uses.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Room")]
    public sealed class PaniqRoom : MonoBehaviour
    {
        [Tooltip("A number nothing else in the building uses. It is how everything refers to this room.")]
                // A whole number rather than an unsigned one, because Unity's
        // inspector is reliable with these and IDs never come near the
        // limit. The simulation takes them unsigned.
        public long RoomId = 5001L;

        [Tooltip("What the room is for, where that changes what people do in it: a toilet stall is somewhere " +
                 "people go in, shut the door, and come out of a while later.")]
        public RoomUse Use = RoomUse.Ordinary;

        /// <summary>The floor this room covers, in whole millimetres.</summary>
        public RectInt Floor
        {
            get
            {
                Vector2Int centre = PaniqAuthoring.Millimetres(transform.position);
                Vector2Int size = PaniqAuthoring.SizeMillimetres(transform);
                int minX = PaniqAuthoring.Snapped(centre.x - size.x / 2);
                int minZ = PaniqAuthoring.Snapped(centre.y - size.y / 2);
                int maxX = PaniqAuthoring.Snapped(centre.x + size.x / 2);
                int maxZ = PaniqAuthoring.Snapped(centre.y + size.y / 2);
                return new RectInt(
                    minX,
                    minZ,
                    Mathf.Max(PaniqAuthoring.RoomSnapMillimetres, maxX - minX),
                    Mathf.Max(PaniqAuthoring.RoomSnapMillimetres, maxZ - minZ));
            }
        }

        private void OnDrawGizmos()
        {
            RectInt floor = Floor;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            var centre = new Vector3(
                (floor.xMin + floor.xMax) / 2f / PaniqAuthoring.MillimetresPerMetre,
                0.02f,
                (floor.yMin + floor.yMax) / 2f / PaniqAuthoring.MillimetresPerMetre);
            var size = new Vector3(
                floor.width / (float)PaniqAuthoring.MillimetresPerMetre,
                0.02f,
                floor.height / (float)PaniqAuthoring.MillimetresPerMetre);
            Gizmos.DrawWireCube(centre, size);
        }
    }
}

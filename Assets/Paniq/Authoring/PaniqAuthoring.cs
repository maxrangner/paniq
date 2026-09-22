using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// Shared ground rules for laying a building out by hand.
    ///
    /// Everything in the simulation is whole millimetres, because that is what
    /// lets a run repeat exactly. Unity works in metres and fractions of them,
    /// so anything dragged around in the scene is rounded on the way in. Rooms
    /// are rounded to the size of a navigation square as well, so that no
    /// square ends up half in one room and half in the next.
    /// </summary>
    public static class PaniqAuthoring
    {
        /// <summary>Millimetres in a metre, matching the simulation's units.</summary>
        public const int MillimetresPerMetre = 1000;

        /// <summary>Rooms and walls are rounded to this, which is the navigation square size.</summary>
        public const int RoomSnapMillimetres = 250;

        /// <summary>A position in the scene, in whole millimetres, flattened to the floor.</summary>
        public static Vector2Int Millimetres(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x * MillimetresPerMetre),
                Mathf.RoundToInt(worldPosition.z * MillimetresPerMetre));
        }

        /// <summary>The same, rounded to the nearest navigation square.</summary>
        public static int Snapped(int millimetres)
        {
            return Mathf.RoundToInt(millimetres / (float)RoomSnapMillimetres) * RoomSnapMillimetres;
        }

        /// <summary>How wide and deep a thing is, in whole millimetres, from its scale.</summary>
        public static Vector2Int SizeMillimetres(Transform transform)
        {
            Vector3 scale = transform.lossyScale;
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) * MillimetresPerMetre)),
                Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.z) * MillimetresPerMetre)));
        }
    }
}

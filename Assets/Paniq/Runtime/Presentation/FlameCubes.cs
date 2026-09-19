using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// A handful of small glowing cubes that rise up through a box-shaped
    /// area, shrinking and turning from yellow to red, then start again at
    /// the bottom: flames on a burning person or thing. They are children of
    /// whatever burns, so they follow it. Presentation only.
    /// </summary>
    internal sealed class FlameCubes
    {
        private static readonly Color FlameYellow = new Color(1f, 0.82f, 0.2f);

        private readonly PresentationMaterials materials;
        private readonly Transform[] flames;
        private readonly Renderer[] renderers;
        private readonly float seed;
        private bool shown;

        public FlameCubes(Transform parent, int count, PresentationMaterials materials, float seed)
        {
            this.materials = materials;
            this.seed = seed;
            flames = new Transform[count];
            renderers = new Renderer[count];
            for (int f = 0; f < count; f++)
            {
                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = $"Flame {f + 1}";
                RemoveCollider(flame);
                flame.transform.SetParent(parent, false);
                renderers[f] = flame.GetComponent<Renderer>();
                renderers[f].sharedMaterial = materials.Fire;
                flame.SetActive(false);
                flames[f] = flame.transform;
            }
        }

        /// <param name="bottom">Local point where flames start.</param>
        /// <param name="spread">Local half-size of the area they spread over (X and Z) and how high they rise (Y).</param>
        /// <param name="size">Local size of a fresh flame cube.</param>
        public void Update(bool burning, float time, Vector3 bottom, Vector3 spread, float size)
        {
            if (shown != burning)
            {
                shown = burning;
                for (int f = 0; f < flames.Length; f++)
                {
                    flames[f].gameObject.SetActive(burning);
                }
            }

            if (!burning)
            {
                return;
            }

            for (int f = 0; f < flames.Length; f++)
            {
                float phase = seed * 0.37f + f * 1.618f;
                float rise = Mathf.Repeat(time * 1.6f + phase, 1f);
                float angle = phase * 2.4f + time * 2f;
                flames[f].localPosition = bottom + new Vector3(Mathf.Cos(angle) * spread.x, rise * spread.y, Mathf.Sin(angle) * spread.z);
                flames[f].localScale = Vector3.one * Mathf.Lerp(size, size * 0.2f, rise);
                flames[f].localRotation = Quaternion.Euler(time * 200f + f * 40f, time * 150f + f * 70f, 0f);
                Color color = Color.Lerp(FlameYellow, PresentationMaterials.FlameRed, rise);
                materials.SetColors(renderers[f], color, color * 2.2f);
            }
        }
    }
}

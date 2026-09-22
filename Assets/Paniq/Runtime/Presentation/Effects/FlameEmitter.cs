using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Flames on a burning person, thing or table: particles licking straight
    /// up from it, with the odd wisp of smoke and ember, for as long as it
    /// burns. It follows whatever it is attached to, and the flames rise
    /// straight up even when a chair lies on its side. Behind a wall it shows
    /// as an orange glow, so a fire in the next room is never missed.
    /// Presentation only.
    /// </summary>
    internal sealed class FlameEmitter
    {
        private readonly Transform parent;
        private readonly ParticleEffects effects;
        private readonly float intensity;
        private readonly Transform glow;
        private float carry;
        private bool shown;

        /// <param name="strength">How fierce it burns: 4 for an object, 6 for a person, 10 for a table.</param>
        public FlameEmitter(Transform parent, int strength, ParticleEffects effects, PresentationMaterials materials)
        {
            this.parent = parent;
            this.effects = effects;
            intensity = strength / 4f;

            // A box drawn only with the fire silhouette: invisible in the open,
            // an orange glow wherever a wall is in front of it.
            if (materials.FireSeeThrough != null)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Flame glow through walls";
                RemoveCollider(box);
                box.transform.SetParent(parent, false);
                box.GetComponent<Renderer>().sharedMaterial = materials.FireSeeThrough;
                box.SetActive(false);
                glow = box.transform;
            }
        }

        /// <param name="bottom">Local point where flames start.</param>
        /// <param name="spread">Local half-size of the area they spread over (X and Z) and how high they rise (Y).</param>
        /// <param name="size">Local size of a fresh flame.</param>
        public void Update(bool burning, Vector3 bottom, Vector3 spread, float size)
        {
            if (shown != burning)
            {
                shown = burning;
                carry = 0f;
                if (glow != null)
                {
                    glow.gameObject.SetActive(burning);
                    glow.localPosition = bottom + Vector3.up * (spread.y * 0.3f);
                    glow.localScale = new Vector3(spread.x * 1.4f, spread.y * 0.6f, spread.z * 1.4f);
                }
            }

            if (!burning)
            {
                return;
            }

            // Worked out in the world, so a thing lying on its side still
            // burns upward rather than sideways.
            Vector3 start = parent.TransformPoint(bottom);
            float halfWidth = parent.TransformVector(new Vector3(spread.x, 0f, 0f)).magnitude;
            float height = parent.TransformVector(new Vector3(0f, spread.y, 0f)).magnitude;
            float worldSize = size * parent.lossyScale.x;
            effects.Flames(start, halfWidth, height, worldSize, Time.deltaTime * intensity, ref carry);
        }
    }
}

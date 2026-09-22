using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Something going off: a laptop battery, a wall socket, a microwave, or
    /// the TNT card. A white flash that lights the room for a moment and a
    /// short shake of the camera, with the sparks, debris and smoke handed to
    /// <see cref="ParticleEffects.Bang"/>. Everything is sized from how big the
    /// blast is, so a laptop cracks, a microwave bangs and TNT booms. Reused
    /// once finished. Presentation only: the scatter of the sparks comes from
    /// the event ID, never from the dice of the simulation.
    /// </summary>
    internal sealed class PopBursts
    {
        private const float FlashSeconds = 0.18f;
        private const float LightSeconds = 0.35f;
        private const float ShakeSeconds = 0.3f;

        /// <summary>The size of blast everything else is measured against: a wall socket at 1.4 m.</summary>
        private const float ReferenceRadius = 1.4f;

        private static readonly Color FlashColor = new Color(1f, 0.95f, 0.8f);

        private sealed class Burst
        {
            public Transform Root;
            public Renderer Flash;
            public Light Glow;
            public Vector3 Origin;
            public float Size;
            public float StartTime;
            public bool Active;
        }

        private readonly PresentationMaterials materials;
        private readonly ParticleEffects effects;
        private readonly Transform parent;
        private readonly List<Burst> bursts = new List<Burst>();

        public PopBursts(PresentationMaterials materials, ParticleEffects effects, Transform parent)
        {
            this.materials = materials;
            this.effects = effects;
            this.parent = parent;
        }

        /// <summary>
        /// How far to nudge the camera this frame. The display adds this to
        /// where the camera normally rests, so the shake never drifts.
        /// </summary>
        public Vector3 Shake { get; private set; }

        /// <summary>Sets one off where something exploded, <paramref name="height"/> metres above the floor.</summary>
        public void Start(LogicalPosition position, float height, int radiusMillimetres, ulong seed, float time)
        {
            if (radiusMillimetres <= 0)
            {
                return;
            }

            Burst burst = FindFree();
            burst.Origin = ToUnityPosition(position) + Vector3.up * height;
            burst.Size = Metres(radiusMillimetres) / ReferenceRadius;
            burst.StartTime = time;
            burst.Active = true;
            burst.Root.gameObject.SetActive(true);

            // The same event ID always throws the same sparks.
            effects.Bang(burst.Origin, burst.Size, seed);
        }

        public void Update(float time)
        {
            Vector3 shake = Vector3.zero;
            foreach (Burst burst in bursts)
            {
                if (!burst.Active)
                {
                    continue;
                }

                float age = time - burst.StartTime;
                if (age > Mathf.Max(LightSeconds, ShakeSeconds))
                {
                    burst.Active = false;
                    burst.Root.gameObject.SetActive(false);
                    continue;
                }

                UpdateFlash(burst, age);

                if (age < ShakeSeconds)
                {
                    // A jolt that dies away, bigger for a bigger bang.
                    float strength = 0.09f * burst.Size * (1f - age / ShakeSeconds);
                    shake += new Vector3(
                        Mathf.Sin(time * 91f + burst.StartTime) * strength,
                        Mathf.Sin(time * 73f + burst.StartTime * 2f) * strength * 0.6f,
                        Mathf.Sin(time * 67f + burst.StartTime * 3f) * strength);
                }
            }

            Shake = shake;
        }

        private void UpdateFlash(Burst burst, float age)
        {
            // A bright ball that swells in a blink and is gone in a fifth of a second.
            float rise = FlashSeconds * 0.3f;
            float flash = age >= FlashSeconds ? 0f
                : age < rise ? age / rise
                : 1f - (age - rise) / (FlashSeconds - rise);
            burst.Flash.enabled = flash > 0f;
            burst.Flash.transform.position = burst.Origin;
            burst.Flash.transform.localScale = Vector3.one * (0.9f * burst.Size * Mathf.Max(0.05f, flash));
            materials.SetColors(burst.Flash, FlashColor, FlashColor * (4f * flash));

            // The light it throws on the walls and floor dies away a little slower.
            float glow = age < LightSeconds ? Mathf.Pow(1f - age / LightSeconds, 2f) : 0f;
            burst.Glow.enabled = glow > 0f;
            burst.Glow.transform.position = burst.Origin + Vector3.up * 0.3f;
            burst.Glow.intensity = 9f * burst.Size * glow;
            burst.Glow.range = 3.2f * burst.Size;
        }

        private Burst FindFree()
        {
            foreach (Burst burst in bursts)
            {
                if (!burst.Active)
                {
                    return burst;
                }
            }

            Burst created = Create(bursts.Count);
            bursts.Add(created);
            return created;
        }

        private Burst Create(int number)
        {
            var root = new GameObject($"Pop burst {number} (presentation)").transform;
            root.SetParent(parent, false);

            GameObject flash = CreatePrimitive("Flash", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, materials.Fire);

            var glowObject = new GameObject("Glow", typeof(Light));
            glowObject.transform.SetParent(root, false);
            Light glow = glowObject.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.78f, 0.5f);
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            var burst = new Burst
            {
                Root = root,
                Flash = flash.GetComponent<Renderer>(),
                Glow = glow
            };

            root.gameObject.SetActive(false);
            return burst;
        }
    }
}

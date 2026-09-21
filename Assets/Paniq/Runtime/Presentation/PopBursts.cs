using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// Something going off: a laptop battery, a wall socket, a microwave, or
    /// the TNT card. A white flash that lights the room for a moment, sparks
    /// thrown out and falling back to the floor, a puff of grey smoke that
    /// swells and drifts up, and a short shake of the camera. Everything is
    /// sized from how big the blast is, so a laptop cracks, a microwave bangs
    /// and TNT booms. Built from primitives like every other effect here, and
    /// reused once finished. Presentation only: the scatter of the sparks
    /// comes from the event ID, never from the dice of the simulation.
    /// </summary>
    internal sealed class PopBursts
    {
        private const int SparkCount = 10;
        private const int SmokeCount = 3;
        private const float FlashSeconds = 0.18f;
        private const float LightSeconds = 0.35f;
        private const float SparkSeconds = 0.65f;
        private const float SmokeDelay = 0.05f;
        private const float SmokeSeconds = 1.1f;
        private const float ShakeSeconds = 0.3f;
        private const float Gravity = 9.8f;

        /// <summary>The size of blast everything else is measured against: a wall socket at 1.4 m.</summary>
        private const float ReferenceRadius = 1.4f;

        private static readonly Color FlashColor = new Color(1f, 0.95f, 0.8f);
        private static readonly Color SparkHot = new Color(1f, 0.9f, 0.55f);
        private static readonly Color SparkCool = new Color(0.9f, 0.25f, 0.05f);
        private static readonly Color SmokeColor = new Color(0.32f, 0.31f, 0.3f);

        private sealed class Burst
        {
            public Transform Root;
            public Renderer Flash;
            public Light Glow;
            public Transform[] Sparks;
            public Renderer[] SparkRenderers;
            public Vector3[] SparkVelocities;
            public Transform[] Smoke;
            public Renderer[] SmokeRenderers;
            public Vector3[] SmokeOffsets;
            public Vector3 Origin;
            public float Size;
            public float StartTime;
            public bool Active;
        }

        private readonly PresentationMaterials materials;
        private readonly Transform parent;
        private readonly List<Burst> bursts = new List<Burst>();

        public PopBursts(PresentationMaterials materials, Transform parent)
        {
            this.materials = materials;
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

            // Sparks fan out all round, some higher than others. The fan comes
            // from the event ID, so the same bang always looks the same.
            int salt = (int)(seed % 100003UL);
            float outward = 2.2f * Mathf.Sqrt(burst.Size);
            for (int i = 0; i < SparkCount; i++)
            {
                float angle = (i + Hash01(salt, i, 3)) / SparkCount * Mathf.PI * 2f;
                float speed = outward * Mathf.Lerp(0.6f, 1.3f, Hash01(salt, i, 5));
                float rise = Mathf.Lerp(1.5f, 3.5f, Hash01(salt, i, 7)) * Mathf.Sqrt(burst.Size);
                burst.SparkVelocities[i] = new Vector3(Mathf.Cos(angle) * speed, rise, Mathf.Sin(angle) * speed);
            }

            for (int i = 0; i < SmokeCount; i++)
            {
                burst.SmokeOffsets[i] = new Vector3(
                    (Hash01(salt, i, 11) - 0.5f) * 0.6f,
                    Hash01(salt, i, 13) * 0.2f,
                    (Hash01(salt, i, 17) - 0.5f) * 0.6f) * burst.Size;
            }
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
                if (age > SmokeDelay + SmokeSeconds)
                {
                    burst.Active = false;
                    burst.Root.gameObject.SetActive(false);
                    continue;
                }

                UpdateFlash(burst, age);
                UpdateSparks(burst, age);
                UpdateSmoke(burst, age);

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

        private void UpdateSparks(Burst burst, float age)
        {
            bool alive = age < SparkSeconds;
            float life = Mathf.Clamp01(age / SparkSeconds);
            Color colour = Color.Lerp(SparkHot, SparkCool, life);
            for (int i = 0; i < SparkCount; i++)
            {
                Transform spark = burst.Sparks[i];
                spark.gameObject.SetActive(alive);
                if (!alive)
                {
                    continue;
                }

                // Thrown out, pulled down, and stopped by the floor.
                Vector3 velocity = burst.SparkVelocities[i];
                Vector3 at = burst.Origin + velocity * age + Vector3.down * (0.5f * Gravity * age * age);
                at.y = Mathf.Max(0.02f, at.y);
                spark.position = at;
                spark.rotation = Quaternion.Euler(age * 720f + i * 37f, age * 540f + i * 53f, 0f);
                spark.localScale = Vector3.one * (0.07f * (1f - life * 0.8f));
                materials.SetColors(burst.SparkRenderers[i], colour, colour * (3f * (1f - life)));
            }
        }

        private void UpdateSmoke(Burst burst, float age)
        {
            float smokeAge = age - SmokeDelay;
            bool alive = smokeAge >= 0f && smokeAge < SmokeSeconds;
            float life = Mathf.Clamp01(smokeAge / SmokeSeconds);
            for (int i = 0; i < SmokeCount; i++)
            {
                Transform puff = burst.Smoke[i];
                puff.gameObject.SetActive(alive);
                if (!alive)
                {
                    continue;
                }

                // It swells quickly, then drifts up and thins out.
                float swell = 1f - Mathf.Pow(1f - life, 3f);
                puff.position = burst.Origin + burst.SmokeOffsets[i] + Vector3.up * (0.7f * burst.Size * life);
                puff.localScale = Vector3.one * (Mathf.Lerp(0.3f, 1.1f, swell) * burst.Size * (0.8f + 0.1f * i));
                Color smoke = SmokeColor;
                smoke.a = 0.55f * (1f - life);
                materials.SetColor(burst.SmokeRenderers[i], smoke);
            }
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
                Glow = glow,
                Sparks = new Transform[SparkCount],
                SparkRenderers = new Renderer[SparkCount],
                SparkVelocities = new Vector3[SparkCount],
                Smoke = new Transform[SmokeCount],
                SmokeRenderers = new Renderer[SmokeCount],
                SmokeOffsets = new Vector3[SmokeCount]
            };

            for (int i = 0; i < SparkCount; i++)
            {
                GameObject spark = CreatePrimitive($"Spark {i + 1}", PrimitiveType.Cube, root, Vector3.zero,
                    Vector3.one * 0.07f, materials.Fire);
                burst.Sparks[i] = spark.transform;
                burst.SparkRenderers[i] = spark.GetComponent<Renderer>();
            }

            // Smoke uses the unlit, see-through material the icons use, so it
            // can fade out rather than pop out of existence.
            for (int i = 0; i < SmokeCount; i++)
            {
                GameObject puff = CreatePrimitive($"Smoke {i + 1}", PrimitiveType.Sphere, root, Vector3.zero,
                    Vector3.one, materials.Icon);
                burst.Smoke[i] = puff.transform;
                burst.SmokeRenderers[i] = puff.GetComponent<Renderer>();
            }

            root.gameObject.SetActive(false);
            return burst;
        }
    }
}

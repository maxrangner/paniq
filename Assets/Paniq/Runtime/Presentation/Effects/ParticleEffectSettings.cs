using System;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// How one kind of particle looks: how many there are, how big, how long
    /// they last and how fast they fly, each as a percentage of the built-in
    /// look, and the colour they start and end their life with.
    /// </summary>
    [Serializable]
    public sealed class ParticleLook
    {
        [Tooltip("How many particles, as a percentage of the built-in amount. 0 turns this effect off.")]
        [Range(0, 400)] public int AmountPercent = 100;

        [Tooltip("How big each particle is, as a percentage of the built-in size.")]
        [Range(10, 400)] public int SizePercent = 100;

        [Tooltip("How long each particle lasts, as a percentage of the built-in lifetime.")]
        [Range(10, 400)] public int LifetimePercent = 100;

        [Tooltip("How fast particles are thrown, as a percentage of the built-in speed.")]
        [Range(0, 400)] public int SpeedPercent = 100;

        [Tooltip("The colour a particle is born with; its see-through-ness (alpha) sets how solid it starts.")]
        public Color Start = Color.white;

        [Tooltip("The colour it fades to by the end of its life; the end is always see-through.")]
        public Color End = Color.white;

        public ParticleLook()
        {
        }

        public ParticleLook(Color start, Color end)
        {
            Start = start;
            End = end;
        }

        public float Amount => AmountPercent / 100f;
        public float Size => SizePercent / 100f;
        public float Lifetime => LifetimePercent / 100f;
        public float Speed => SpeedPercent / 100f;
    }

    /// <summary>
    /// Every dial for the particle effects: bangs, fire, extinguisher foam,
    /// dust from knocks and splinters from breakages. Purely the look: nothing
    /// here can change what happens in a run. Edit the asset in
    /// Assets/Paniq/Content in the Inspector while the game runs and the change
    /// shows on the next effect.
    /// </summary>
    [CreateAssetMenu(fileName = "ParticleEffects", menuName = "Paniq/Particle Effect Settings")]
    public sealed class ParticleEffectSettings : ScriptableObject
    {
        [Tooltip("The most particles alive at once across every effect. Past half of this, new effects make proportionally fewer, so the worst blast cannot slow the game down.")]
        [Range(500, 50000)] public int LiveParticleBudget = 8000;

        [Header("Bangs")]
        public ParticleLook Sparks = new ParticleLook(new Color(1f, 0.92f, 0.6f), new Color(0.9f, 0.2f, 0.04f));
        public ParticleLook BangSmoke = new ParticleLook(new Color(0.36f, 0.35f, 0.34f, 0.7f), new Color(0.22f, 0.22f, 0.22f));
        public ParticleLook Debris = new ParticleLook(new Color(0.24f, 0.22f, 0.2f), new Color(0.18f, 0.17f, 0.16f));

        [Header("Knocks and breakages")]
        public ParticleLook Dust = new ParticleLook(new Color(0.72f, 0.68f, 0.6f, 0.6f), new Color(0.6f, 0.58f, 0.54f));
        public ParticleLook Splinters = new ParticleLook(new Color(0.55f, 0.37f, 0.21f), new Color(0.4f, 0.27f, 0.15f));
        public ParticleLook Shards = new ParticleLook(new Color(0.75f, 0.8f, 0.86f), new Color(0.45f, 0.5f, 0.56f));

        [Header("Fire")]
        public ParticleLook Flames = new ParticleLook(new Color(1f, 0.85f, 0.25f), new Color(0.95f, 0.12f, 0.02f));
        public ParticleLook FireSmoke = new ParticleLook(new Color(0.2f, 0.19f, 0.19f, 0.55f), new Color(0.12f, 0.12f, 0.12f));
        public ParticleLook Embers = new ParticleLook(new Color(1f, 0.55f, 0.1f), new Color(0.6f, 0.08f, 0.02f));

        [Header("Extinguisher")]
        public ParticleLook Foam = new ParticleLook(new Color(0.92f, 0.97f, 1f), new Color(0.8f, 0.88f, 0.95f));
        public ParticleLook Mist = new ParticleLook(new Color(0.85f, 0.92f, 1f, 0.35f), new Color(0.85f, 0.92f, 1f));

        /// <summary>The built-in look, for when no settings asset is assigned.</summary>
        public static ParticleEffectSettings CreateDefaults()
        {
            var settings = CreateInstance<ParticleEffectSettings>();
            settings.name = "Particle effects (built-in defaults)";
            settings.hideFlags = HideFlags.DontSave;
            return settings;
        }
    }
}

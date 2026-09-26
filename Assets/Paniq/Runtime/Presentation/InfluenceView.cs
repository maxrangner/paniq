using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The player's influence, as the owner asked for it (2026-09-26): every
    /// influenced door, thing or patch of floor glows with a sparkling aura,
    /// faint at one click and intense at twenty; and everybody feeling a pull
    /// shows a glowing, sparkling line from them to it -- faint first, more
    /// intense the more they feel it. So the player can see influence working,
    /// on whom, and how much.
    /// <para>
    /// Everything is read from the snapshot and nothing decides anything. The
    /// line renderers are pooled and reused frame to frame, so a crowd of
    /// people pulled costs no new objects.
    /// </para>
    /// </summary>
    internal sealed class InfluenceView
    {
        private const int AuraSegments = 40;

        /// <summary>A pale gold-white: not the orange of fire, not the blue of a held door.</summary>
        private static readonly Color Glow = new Color(1f, 0.93f, 0.62f, 1f);

        private readonly Material material;
        private readonly Transform parent;
        private readonly ParticleEffects effects;
        private readonly List<LineRenderer> auras = new List<LineRenderer>();
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<float> sparkleCarry = new List<float>();

        public InfluenceView(Material material, Transform parent, ParticleEffects effects)
        {
            this.material = material;
            this.parent = parent;
            this.effects = effects;
        }

        public void Update(RunSnapshot snapshot, float time, float deltaTime)
        {
            int places = snapshot == null ? 0 : snapshot.InfluencePlaces.Count;
            for (int i = 0; i < places; i++)
            {
                InfluencePlaceSnapshot place = snapshot.InfluencePlaces[i];
                float strength = place.MaximumLevel > 0 ? place.Level / (float)place.MaximumLevel : 0f;
                DrawAura(i, place, strength, time, deltaTime);
            }

            for (int i = places; i < auras.Count; i++)
            {
                auras[i].enabled = false;
            }

            int pulls = snapshot == null ? 0 : snapshot.InfluencePulls.Count;
            int drawn = 0;
            for (int i = 0; i < pulls; i++)
            {
                InfluencePullSnapshot pull = snapshot.InfluencePulls[i];
                if (pull.Place < 0 || pull.Place >= places || pull.AgentIndex < 0 || pull.AgentIndex >= snapshot.Agents.Count)
                {
                    continue;
                }

                DrawLine(drawn++, snapshot.Agents[pull.AgentIndex].Position, snapshot.InfluencePlaces[pull.Place].At,
                    Mathf.Clamp01(pull.FeltPerMille / 1000f), time);
            }

            for (int i = drawn; i < lines.Count; i++)
            {
                lines[i].enabled = false;
            }
        }

        /// <summary>A ring on the floor that breathes and flickers, wider and brighter the more clicks it has, throwing off sparks.</summary>
        private void DrawAura(int index, InfluencePlaceSnapshot place, float strength, float time, float deltaTime)
        {
            while (auras.Count <= index)
            {
                auras.Add(NewLine("Influence aura (presentation)", AuraSegments, true));
                sparkleCarry.Add(0f);
            }

            LineRenderer ring = auras[index];
            Vector3 middle = ToUnityPosition(place.At) + Vector3.up * (place.IsDoor ? 1f : 0.06f);
            float radius = Mathf.Lerp(0.3f, 0.8f, strength) * (1f + 0.06f * Mathf.Sin(time * 5f + index));
            for (int s = 0; s < AuraSegments; s++)
            {
                float angle = s / (float)AuraSegments * Mathf.PI * 2f;
                ring.SetPosition(s, middle + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            float flicker = 0.85f + 0.15f * Mathf.Sin(time * 23f + index * 1.7f);
            Color colour = Glow;
            colour.a = Mathf.Lerp(0.2f, 1f, strength) * flicker;
            ring.startColor = colour;
            ring.endColor = colour;
            ring.widthMultiplier = Mathf.Lerp(0.02f, 0.09f, strength);
            ring.enabled = true;

            float carry = sparkleCarry[index];
            effects?.Sparkle(middle, radius * 0.8f, strength, deltaTime, ref carry);
            sparkleCarry[index] = carry;
        }

        /// <summary>A thin line from a person's chest to the place, shimmering along its length, brighter the harder they are pulled.</summary>
        private void DrawLine(int index, LogicalPosition person, LogicalPosition place, float felt, float time)
        {
            const int Points = 12;
            while (lines.Count <= index)
            {
                lines.Add(NewLine("Influence pull (presentation)", Points, false));
            }

            LineRenderer line = lines[index];
            Vector3 from = ToUnityPosition(person) + Vector3.up * 0.9f;
            Vector3 to = ToUnityPosition(place) + Vector3.up * 0.3f;
            for (int p = 0; p < Points; p++)
            {
                float along = p / (float)(Points - 1);

                // A shimmer travelling along it toward the place, so it reads
                // as a pull and not as a string.
                float shimmer = Mathf.Sin(along * 18f - time * 9f) * 0.03f * (1f - Mathf.Abs(along * 2f - 1f));
                line.SetPosition(p, Vector3.Lerp(from, to, along) + Vector3.up * (Mathf.Sin(along * Mathf.PI) * 0.25f + shimmer));
            }

            Color start = Glow;
            start.a = Mathf.Lerp(0.08f, 0.8f, felt);
            Color end = Glow;
            end.a = start.a * 0.35f;
            line.startColor = start;
            line.endColor = end;
            line.widthMultiplier = Mathf.Lerp(0.01f, 0.05f, felt) * (0.9f + 0.1f * Mathf.Sin(time * 17f + index));
            line.enabled = true;
        }

        private LineRenderer NewLine(string name, int points, bool loop)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            LineRenderer line = holder.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

    }
}

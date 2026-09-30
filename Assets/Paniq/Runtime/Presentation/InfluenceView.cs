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

        /// <summary>The right button's push (2026-09-30): a cool blue-grey, the other way from the gold.</summary>
        private static readonly Color PushGlow = new Color(0.62f, 0.8f, 1f, 1f);

        private readonly Material material;
        private readonly Transform parent;
        private readonly ParticleEffects effects;
        private readonly List<LineRenderer> auras = new List<LineRenderer>();

        /// <summary>The countdown rings of people winding up to something dangerous (2026-09-30).</summary>
        private readonly List<LineRenderer> tellRings = new List<LineRenderer>();

        /// <summary>A hot red-orange: the colour of the danger they are winding up toward.</summary>
        private static readonly Color TellGlow = new Color(1f, 0.32f, 0.12f, 1f);
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

            // The hand on a person (2026-09-29): the same gold ring, at their
            // feet, so the player can see who they have hold of.
            int drawnAuras = places;
            if (snapshot != null && snapshot.TuggedAgentIndex >= 0 && snapshot.TuggedAgentIndex < snapshot.Agents.Count)
            {
                AgentSnapshot held = snapshot.Agents[snapshot.TuggedAgentIndex];
                DrawAura(drawnAuras++, new InfluencePlaceSnapshot(held.AgentId, false, held.Position, 1, 1), 1f, time, deltaTime);
            }

            for (int i = drawnAuras; i < auras.Count; i++)
            {
                auras[i].enabled = false;
            }

            // Tells (2026-09-30, the owner: "the visible agent tells"): a ring
            // at the feet of anybody winding up to something dangerous,
            // shrinking to nothing as their time runs out -- the creak, for
            // people. Always drawn, whatever the Tab panel hides: it is play.
            int tells = 0;
            int people = snapshot == null ? 0 : snapshot.Agents.Count;
            for (int i = 0; i < people; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                if (agent.Tell != AgentTell.None && agent.FearState == AgentFearState.Scared && !agent.IsBurning &&
                    agent.Participation == AgentParticipation.Participating)
                {
                    DrawTellRing(tells++, agent, time);
                }
            }

            for (int i = tells; i < tellRings.Count; i++)
            {
                tellRings[i].enabled = false;
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

                InfluencePlaceSnapshot from = snapshot.InfluencePlaces[pull.Place];
                DrawLine(drawn++, snapshot.Agents[pull.AgentIndex].Position, from.At,
                    Mathf.Clamp01(pull.FeltPerMille / 1000f), time, from.Repels, pull.ActingForTheHand);
            }

            for (int i = drawn; i < lines.Count; i++)
            {
                lines[i].enabled = false;
            }
        }

        /// <summary>
        /// A tell's countdown: a red-orange ring at their feet, closing from
        /// most of a metre to a hand's width as the wind-up runs out, and
        /// pulsing faster the closer it gets.
        /// </summary>
        private void DrawTellRing(int index, AgentSnapshot agent, float time)
        {
            while (tellRings.Count <= index)
            {
                tellRings.Add(NewLine("Tell countdown (presentation)", AuraSegments, true));
            }

            LineRenderer ring = tellRings[index];
            float wound = Mathf.Clamp01(agent.TellProgress / 1000f);
            Vector3 middle = ToUnityPosition(agent.Position) + Vector3.up * 0.05f;
            float radius = Mathf.Lerp(0.85f, 0.12f, wound);
            for (int s = 0; s < AuraSegments; s++)
            {
                float angle = s / (float)AuraSegments * Mathf.PI * 2f;
                ring.SetPosition(s, middle + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            float pulse = 0.65f + 0.35f * Mathf.Sin(time * Mathf.Lerp(8f, 26f, wound) + index);
            Color colour = TellGlow;
            colour.a = Mathf.Lerp(0.6f, 1f, wound) * pulse;
            ring.startColor = colour;
            ring.endColor = colour;
            ring.widthMultiplier = Mathf.Lerp(0.05f, 0.11f, wound);
            ring.enabled = true;
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

            // A click's beacon (2026-09-30) throbs, so it reads as something
            // that will not stay.
            float flicker = place.IsBeacon
                ? 0.7f + 0.3f * Mathf.Sin(time * 12f)
                : 0.85f + 0.15f * Mathf.Sin(time * 23f + index * 1.7f);
            Color colour = place.Repels ? PushGlow : Glow;
            colour.a = Mathf.Lerp(0.2f, 1f, strength) * flicker;
            ring.startColor = colour;
            ring.endColor = colour;
            ring.widthMultiplier = Mathf.Lerp(0.02f, 0.09f, strength);
            ring.enabled = true;

            float carry = sparkleCarry[index];
            effects?.Sparkle(middle, radius * 0.8f, strength, deltaTime, ref carry);
            sparkleCarry[index] = carry;
        }

        /// <summary>
        /// A thin line from a person's chest to the place, shimmering along its
        /// length, brighter the harder they are pulled -- and brighter and
        /// thicker still for somebody doing what the hand asked (2026-09-30).
        /// A push is drawn the other way: a short blue line from the person
        /// on away from the place, the shimmer running outward.
        /// </summary>
        private void DrawLine(int index, LogicalPosition person, LogicalPosition place, float felt, float time,
            bool pushes = false, bool acting = false)
        {
            const int Points = 12;
            while (lines.Count <= index)
            {
                lines.Add(NewLine("Influence pull (presentation)", Points, false));
            }

            LineRenderer line = lines[index];
            Vector3 from = ToUnityPosition(person) + Vector3.up * 0.9f;
            Vector3 to = ToUnityPosition(place) + Vector3.up * 0.3f;
            if (pushes)
            {
                // From the person, a metre and a half on away from the push.
                Vector3 away = from - (ToUnityPosition(place) + Vector3.up * 0.9f);
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
                to = from + away * 1.5f + Vector3.down * 0.6f;
            }

            for (int p = 0; p < Points; p++)
            {
                float along = p / (float)(Points - 1);

                // A shimmer travelling along it toward the place, so it reads
                // as a pull and not as a string.
                float shimmer = Mathf.Sin(along * 18f - time * 9f) * 0.03f * (1f - Mathf.Abs(along * 2f - 1f));
                line.SetPosition(p, Vector3.Lerp(from, to, along) + Vector3.up * (Mathf.Sin(along * Mathf.PI) * 0.25f + shimmer));
            }

            Color start = pushes ? PushGlow : Glow;
            start.a = acting ? 1f : Mathf.Lerp(0.08f, 0.8f, felt);
            Color end = start;
            end.a = start.a * (acting ? 0.7f : 0.35f);
            line.startColor = start;
            line.endColor = end;
            float width = acting ? 0.08f : Mathf.Lerp(0.01f, 0.05f, felt);
            line.widthMultiplier = width * (0.9f + 0.1f * Mathf.Sin(time * 17f + index));
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

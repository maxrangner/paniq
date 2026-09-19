using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// People as capsules. They blend between the last two ticks so movement
    /// is smooth at any frame rate, bob with each stride, lean with speed,
    /// tip over when they fall, wobble when staggering, tremble when frozen,
    /// lunge at doors they shove, and shrink away when they escape. Each has
    /// a vision-cone outline and floating icons.
    /// </summary>
    internal sealed class AgentViews
    {
        private const float FallSeconds = 0.22f;
        private const float EscapeFadeSeconds = 0.45f;

        private static readonly Color CalmColor = new Color(0.78f, 0.84f, 0.9f);
        private static readonly Color ScaredColor = new Color(1f, 0.58f, 0.12f);
        private static readonly Color FrozenColor = new Color(0.72f, 0.8f, 0.95f);
        private static readonly Color LostColor = new Color(0.32f, 0.06f, 0.04f);

        private sealed class AgentView
        {
            public Transform Transform;
            public Renderer Renderer;
            public AgentIconViews Icons;
            public LineRenderer Vision;
            public Vector3 LastPlanarPosition;
            public float StridePhase;
            public float ShakePhase;
            public AgentBodyState LastBodyState;
            public float BodyStateSince;
            public float TiltAtStateChange;
            public float RiseSeconds;
            public float Tilt;
            public bool Initialized;
            public float LungeStart = -10f;
            public float EscapedSince = -1f;
            public Vector3 EscapePosition;
        }

        private readonly FireReactionScenarioData scenario;
        private readonly PresentationMaterials materials;
        private readonly Dictionary<SimulationId, AgentView> agents = new Dictionary<SimulationId, AgentView>();

        public AgentViews(FireReactionScenarioData scenario, PresentationMaterials materials, Transform parent)
        {
            this.scenario = scenario;
            this.materials = materials;
            int number = 0;
            foreach (FireReactionAgentDefinition definition in scenario.Agents)
            {
                number++;
                GameObject agentObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agentObject.name = $"Agent {definition.AgentId.Value} (presentation)";
                agentObject.transform.SetParent(parent, false);
                RemoveCollider(agentObject);
                agentObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                Renderer agentRenderer = agentObject.GetComponent<Renderer>();
                agentRenderer.sharedMaterial = materials.Agent;

                var visionObject = new GameObject($"Agent {definition.AgentId.Value} vision cone (presentation)");
                visionObject.transform.SetParent(parent, false);
                LineRenderer vision = visionObject.AddComponent<LineRenderer>();
                vision.useWorldSpace = true;
                vision.loop = false;
                vision.positionCount = 7;
                vision.startWidth = 0.025f;
                vision.endWidth = 0.025f;
                vision.sharedMaterial = materials.Vision;

                agents.Add(definition.AgentId, new AgentView
                {
                    Transform = agentObject.transform,
                    Renderer = agentRenderer,
                    Icons = new AgentIconViews($"Agent {definition.AgentId.Value}", number.ToString(), materials.Icon,
                        definition.AgentId.Value % 60UL, parent),
                    Vision = vision,
                    ShakePhase = definition.AgentId.Value % 97UL
                });
            }
        }

        /// <summary>A red "!" for noticing something.</summary>
        public void Notice(SimulationId agentId, float time)
        {
            if (agents.TryGetValue(agentId, out AgentView view))
            {
                view.Icons.Notice(time);
            }
        }

        /// <summary>Sound-wave arcs for a yell.</summary>
        public void Yell(SimulationId agentId, float time)
        {
            if (agents.TryGetValue(agentId, out AgentView view))
            {
                view.Icons.Yell(time);
            }
        }

        /// <summary>A shoulder thrown at a stuck door.</summary>
        public void Lunge(SimulationId agentId, float time)
        {
            if (agents.TryGetValue(agentId, out AgentView view))
            {
                view.LungeStart = time;
            }
        }

        public void Update(FireReactionSnapshot snapshot, FireReactionSnapshot previousSnapshot, float blend, float time,
            Transform cameraTransform)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
                if (!agents.TryGetValue(agent.AgentId, out AgentView view))
                {
                    continue;
                }

                FireReactionAgentSnapshot previous = previousSnapshot != null && i < previousSnapshot.Agents.Count
                    ? previousSnapshot.Agents[i]
                    : agent;
                Vector3 planar = Vector3.Lerp(ToUnityPosition(previous.Position), ToUnityPosition(agent.Position), blend);
                float yaw = Mathf.LerpAngle(previous.HeadingDegrees, agent.HeadingDegrees, blend);
                if (!view.Initialized)
                {
                    view.LastPlanarPosition = planar;
                    view.Initialized = true;
                }

                if (agent.Outcome == AgentTerminalOutcome.Escaped)
                {
                    UpdateEscaped(agent, view, planar, yaw, time);
                    continue;
                }

                bool lost = agent.Outcome == AgentTerminalOutcome.Lost;
                bool frozen = agent.ActivityState == AgentActivityState.Frozen;
                bool running = agent.FearState == AgentFearState.Scared && !frozen;
                float speed = agent.SpeedMillimetresPerTick * FireReactionSimulation.TicksPerSecond /
                              (float)FireReactionSimulation.MillimetresPerMetre;

                // A step bounce driven by distance actually travelled, so feet
                // never appear to slide.
                float travelled = (planar - view.LastPlanarPosition).magnitude;
                view.LastPlanarPosition = planar;
                view.StridePhase += travelled / (running ? 1.1f : 0.7f) * Mathf.PI;
                float bounce = Mathf.Abs(Mathf.Sin(view.StridePhase)) * (running ? 0.12f : 0.05f) * Mathf.Clamp01(speed);
                float alertJump = agent.FearState == AgentFearState.Alert && !agent.IsDown
                    ? 0.18f + Mathf.Abs(Mathf.Sin(time * 18f + agent.AgentId.Value % 997UL)) * 0.18f
                    : 0f;

                float tilt = UpdateTilt(agent, view, time);
                bool down = lost || tilt > 45f;
                if (lost)
                {
                    // Knocked flat where the fire caught them.
                    view.Transform.SetPositionAndRotation(planar + Vector3.up * 0.25f, Quaternion.Euler(90f, yaw, 0f));
                }
                else if (tilt > 0f)
                {
                    // Falling forward from the feet, or pushing back up.
                    float lying = tilt / 90f;
                    Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    view.Transform.SetPositionAndRotation(
                        planar + forward * (0.25f * lying) + Vector3.up * Mathf.Lerp(0.5f, 0.25f, lying),
                        Quaternion.Euler(tilt, yaw, 0f));
                }
                else
                {
                    float lean = Mathf.Min(running ? 14f : 4f, speed * 3f);
                    float roll = 0f;
                    Vector3 shake = Vector3.zero;
                    if (agent.BodyState == AgentBodyState.Staggering)
                    {
                        // Reeling from a bump.
                        roll = Mathf.Sin(time * 26f + view.ShakePhase) * 14f;
                        lean = -8f;
                    }
                    else if (frozen)
                    {
                        // Trembling on the spot.
                        shake = new Vector3(
                            Mathf.Sin(time * 47f + view.ShakePhase) * 0.025f,
                            0f,
                            Mathf.Sin(time * 53f + view.ShakePhase * 1.7f) * 0.025f);
                        roll = Mathf.Sin(time * 41f + view.ShakePhase) * 2.5f;
                        bounce = 0f;
                    }

                    // A shoulder thrown at a stuck door.
                    Vector3 lunge = Vector3.zero;
                    float lungeAge = (time - view.LungeStart) / 0.3f;
                    if (lungeAge < 1f)
                    {
                        float push = Mathf.Sin(lungeAge * Mathf.PI);
                        lunge = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (push * 0.18f);
                        lean += push * 20f;
                    }

                    view.Transform.SetPositionAndRotation(
                        planar + shake + lunge + Vector3.up * (0.5f + bounce + alertJump),
                        Quaternion.Euler(lean, yaw, roll));
                }

                UpdateAppearance(agent, view, planar, yaw, down, alertJump, time, cameraTransform);
            }
        }

        /// <summary>Someone who got out keeps walking a few steps and shrinks away.</summary>
        private static void UpdateEscaped(FireReactionAgentSnapshot agent, AgentView view, Vector3 planar, float yaw, float time)
        {
            if (view.EscapedSince < 0f)
            {
                view.EscapedSince = time;
                view.EscapePosition = planar;
                view.Icons.HideAll();
                view.Vision.enabled = false;
            }

            float age = (time - view.EscapedSince) / EscapeFadeSeconds;
            if (age >= 1f)
            {
                view.Transform.gameObject.SetActive(false);
                return;
            }

            float speed = Mathf.Max(1.5f, agent.SpeedMillimetresPerTick * FireReactionSimulation.TicksPerSecond /
                                          (float)FireReactionSimulation.MillimetresPerMetre);
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            view.Transform.SetPositionAndRotation(
                view.EscapePosition + forward * (speed * age * EscapeFadeSeconds) + Vector3.up * 0.5f,
                Quaternion.Euler(10f, yaw, 0f));
            view.Transform.localScale = Vector3.one * (0.5f * (1f - age));
        }

        /// <summary>Forward tilt in degrees: 0 upright, 90 lying on the floor. Timed locally per state.</summary>
        private float UpdateTilt(FireReactionAgentSnapshot agent, AgentView view, float time)
        {
            if (agent.BodyState != view.LastBodyState)
            {
                // Coming round from being knocked out, people get up more slowly.
                int riseTicks = view.LastBodyState == AgentBodyState.Unconscious
                    ? scenario.Falls.ComeToGetUpTicks
                    : scenario.Falls.GetUpTicks;
                view.RiseSeconds = (float)riseTicks / FireReactionSimulation.TicksPerSecond;
                view.LastBodyState = agent.BodyState;
                view.BodyStateSince = time;
                view.TiltAtStateChange = view.Tilt;
            }

            float age = time - view.BodyStateSince;
            switch (agent.BodyState)
            {
                case AgentBodyState.Fallen:
                case AgentBodyState.Unconscious:
                    view.Tilt = Mathf.Lerp(view.TiltAtStateChange, 90f, EaseInQuad(age / FallSeconds));
                    break;
                case AgentBodyState.GettingUp:
                    view.Tilt = Mathf.Lerp(view.TiltAtStateChange, 0f, Mathf.SmoothStep(0f, 1f, age / view.RiseSeconds));
                    break;
                default:
                    view.Tilt = 0f;
                    break;
            }

            return view.Tilt;
        }

        private void UpdateAppearance(
            FireReactionAgentSnapshot agent,
            AgentView view,
            Vector3 planar,
            float yaw,
            bool down,
            float alertJump,
            float time,
            Transform cameraTransform)
        {
            bool participating = agent.Participation == AgentParticipation.Participating;
            bool frozen = agent.ActivityState == AgentActivityState.Frozen;
            materials.SetColor(view.Renderer, agent.Outcome == AgentTerminalOutcome.Lost
                ? LostColor
                : agent.FearState == AgentFearState.Calm ? CalmColor
                : frozen ? FrozenColor : ScaredColor);

            if (!participating)
            {
                view.Icons.HideAll();
            }
            else
            {
                Vector3 anchor = planar + Vector3.up * (down ? 0.75f : 1.4f + alertJump);
                Vector3 facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float facingSide = Vector3.Dot(facing, cameraTransform.right) >= 0f ? 1f : -1f;
                bool calm = agent.FearState == AgentFearState.Calm;
                view.Icons.Update(
                    anchor,
                    cameraTransform.rotation,
                    facingSide,
                    frozen,
                    agent.BodyState == AgentBodyState.Unconscious,
                    calm && agent.ActivityState == AgentActivityState.Investigating,
                    calm && (agent.ActivityState == AgentActivityState.Standing ||
                             agent.ActivityState == AgentActivityState.LookingAround),
                    time);
            }

            UpdateVisionCone(agent, view.Vision, planar, yaw);
        }

        private void UpdateVisionCone(FireReactionAgentSnapshot agent, LineRenderer vision, Vector3 planar, float yaw)
        {
            vision.enabled = agent.Participation == AgentParticipation.Participating;
            if (!vision.enabled)
            {
                return;
            }

            float range = Metres(scenario.Perception.VisionRangeMillimetres);
            var origin = new Vector3(planar.x, 0.03f, planar.z);
            vision.SetPosition(0, origin);
            for (int i = 0; i < 5; i++)
            {
                float angle = yaw + Mathf.Lerp(-45f, 45f, i / 4f);
                vision.SetPosition(i + 1, origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * range);
            }

            vision.SetPosition(6, origin);
            Color coneColor = agent.FearState == AgentFearState.Calm
                ? new Color(0.25f, 0.7f, 1f, 0.65f)
                : new Color(1f, 0.58f, 0.12f, 0.6f);
            vision.startColor = coneColor;
            vision.endColor = coneColor;
        }
    }
}

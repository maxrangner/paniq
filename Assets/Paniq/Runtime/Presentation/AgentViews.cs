using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// People as capsules. They blend between the last two ticks so movement
    /// is smooth at any frame rate, waddle from foot to foot as they walk,
    /// bob with each stride, lean with speed,
    /// fall and lie where the physics engine laid them (and fly with it when a
    /// blast throws them), slump where there is no room to fall, wobble when staggering, tremble when frozen,
    /// flail with little flames licking up them when on fire, lunge at doors
    /// they shove, shake whoever they are shaking awake, lean back when
    /// dragging someone, and shrink away when they escape. Each has
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
        private static readonly Color BurningColor = new Color(1f, 0.35f, 0.05f);
        private const int FlamesPerPerson = 6;

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
            public float RiseSeconds;

            /// <summary>Where the body was drawn when it last began to fall or get up: the animation starts there.</summary>
            public Vector3 FromPosition;

            public Quaternion FromRotation = Quaternion.identity;
            public bool Initialized;
            public float LungeStart = -10f;
            public float EscapedSince = -1f;
            public Vector3 EscapePosition;
            public FlameEmitter Flames;
        }

        private readonly FireReactionScenarioData scenario;
        private readonly PresentationMaterials materials;
        private readonly Dictionary<SimulationId, AgentView> agents = new Dictionary<SimulationId, AgentView>();

        public AgentViews(FireReactionScenarioData scenario, PresentationMaterials materials, ParticleEffects effects,
            Transform parent)
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
                agentObject.transform.localScale = BodyScale;
                Renderer agentRenderer = agentObject.GetComponent<Renderer>();
                agentRenderer.sharedMaterial = materials.Agent;
                ShowThroughWalls(agentObject, materials);

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
                    // Children of the capsule so they follow it when it runs or falls.
                    Flames = new FlameEmitter(agentObject.transform, FlamesPerPerson, effects, materials),
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

                // The waddle. The bounce above is a hop on every footfall, so
                // it uses the size of the sine; these use its sign as well, so
                // they come out opposite on the left foot and the right one
                // and the body rocks from one to the other. Both fade out with
                // speed, so somebody shuffling barely moves and somebody
                // sprinting throws themselves about.
                float onThisFoot = Mathf.Sin(view.StridePhase) * Mathf.Clamp01(speed) *
                                   (running ? RunningWaddle : 1f);
                float waddleRoll = onThisFoot * WaddleRollDegrees;
                float waddleTwist = onThisFoot * WaddleTwistDegrees;
                float alertJump = agent.FearState == AgentFearState.Alert && !agent.IsDown
                    ? 0.18f + Mathf.Abs(Mathf.Sin(time * 18f + agent.AgentId.Value % 997UL)) * 0.18f
                    : 0f;

                NoteBodyStateChange(agent, view, time);
                float age = time - view.BodyStateSince;
                bool fallen = agent.BodyState == AgentBodyState.Fallen || agent.BodyState == AgentBodyState.Unconscious;
                bool rising = agent.BodyState == AgentBodyState.GettingUp;

                // Anybody not sitting stands at their full height. Set here as
                // well as in the seated branch, because somebody knocked out of a
                // chair goes from sitting to lying in one tick and would
                // otherwise stay folded up on the floor.
                bool seatedNow = agent.ActivityState == AgentActivityState.Sitting ||
                                 agent.ActivityState == AgentActivityState.StandingUp;
                if (!seatedNow)
                {
                    view.Transform.localScale = BodyScale;
                }

                if (lost)
                {
                    // Knocked flat where the fire caught them.
                    view.Transform.SetPositionAndRotation(planar + Vector3.up * BodyRadius, Quaternion.Euler(90f, yaw, 0f));
                }
                else if (fallen || rising)
                {
                    // Going down: from where they stood to where the physics
                    // laid them, quickly, then following the body wherever it
                    // is thrown or dragged. Getting up: from the floor back to
                    // standing, taking as long as the simulation gives them.
                    Vector3 position;
                    Quaternion rotation;
                    float progress;
                    if (rising)
                    {
                        position = planar + Vector3.up * BodyHalfHeight;
                        rotation = Quaternion.Euler(0f, yaw, 0f);
                        progress = Mathf.SmoothStep(0f, 1f, age / view.RiseSeconds);
                    }
                    else
                    {
                        DownPose(agent, previous, blend, planar, yaw, out position, out rotation);
                        progress = EaseInQuad(age / FallSeconds);
                    }

                    view.Transform.SetPositionAndRotation(
                        Vector3.Lerp(view.FromPosition, position, progress),
                        Quaternion.Slerp(view.FromRotation, rotation, progress));
                }
                else
                {
                    float lean = Mathf.Min(running ? 14f : 4f, speed * 3f);
                    float roll = waddleRoll;
                    float twist = waddleTwist;
                    Vector3 shake = Vector3.zero;
                    if (agent.BodyState == AgentBodyState.Staggering)
                    {
                        // Reeling from a bump.
                        roll = Mathf.Sin(time * 26f + view.ShakePhase) * 14f;
                        twist = 0f;
                        lean = -8f;
                    }
                    else if (agent.ActivityState == AgentActivityState.ShakingAwake && agent.SpeedMillimetresPerTick == 0)
                    {
                        // Shaking someone by the shoulders: a quick back-and-forth.
                        Vector3 facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        shake = facing * (Mathf.Sin(time * 30f + view.ShakePhase) * 0.06f);
                        roll = Mathf.Sin(time * 30f + view.ShakePhase) * 6f;
                        twist = 0f;
                    }
                    else if (agent.ActivityState == AgentActivityState.Dragging)
                    {
                        // Hauling someone along behind them.
                        lean = -18f;
                    }
                    else if (agent.IsBurning)
                    {
                        // Flailing: thrashing side to side as they run. Far
                        // bigger than the waddle, and it replaces it: somebody
                        // alight is not taking tidy steps any more.
                        roll = Mathf.Sin(time * 22f + view.ShakePhase) * 18f;
                        twist = 0f;
                        lean += Mathf.Sin(time * 15f + view.ShakePhase * 0.7f) * 8f;
                    }
                    else if (frozen)
                    {
                        // Trembling on the spot.
                        shake = new Vector3(
                            Mathf.Sin(time * 47f + view.ShakePhase) * 0.025f,
                            0f,
                            Mathf.Sin(time * 53f + view.ShakePhase * 1.7f) * 0.025f);
                        roll = Mathf.Sin(time * 41f + view.ShakePhase) * 2.5f;
                        twist = 0f;
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

                    // Sitting: lowered onto the seat, and rising back out of
                    // it as they stand, so the change reads as a movement.
                    float seated = 0f;
                    if (agent.ActivityState == AgentActivityState.Sitting)
                    {
                        seated = 1f;
                        bounce = 0f;
                        roll = 0f;
                        twist = 0f;
                    }
                    else if (agent.ActivityState == AgentActivityState.StandingUp)
                    {
                        seated = 0.5f;
                        bounce = 0f;
                        roll = 0f;
                        twist = 0f;
                    }

                    // Seated, the body settles lower and leans back into the
                    // chair. It is not squashed: it used to be flattened to
                    // two-thirds height at full width, which turned a person
                    // into a hunched blob, and then stood on the seat as well,
                    // so a sitting head ended up higher than a standing one.
                    // The same body simply sinks, and what goes below the floor
                    // line is inside the chair nobody can see into anyway.
                    float middle = Mathf.Lerp(BodyHalfHeight, SeatedCentreHeight, seated);
                    view.Transform.localScale = BodyScale;
                    view.Transform.SetPositionAndRotation(
                        planar + shake + lunge + Vector3.up * (middle + bounce + alertJump),
                        Quaternion.Euler(lean + SeatedLeanDegrees * seated, yaw + twist, roll));
                }

                bool down = lost || view.Transform.up.y < 0.7f;
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
                view.EscapePosition + forward * (speed * age * EscapeFadeSeconds) + Vector3.up * BodyHalfHeight,
                Quaternion.Euler(10f, yaw, 0f));
            view.Transform.localScale = BodyScale * (1f - age);
        }

        /// <summary>
        /// Notices a fall, a get-up or a recovery, remembering when it began
        /// and where the body was drawn at that moment.
        /// </summary>
        private void NoteBodyStateChange(FireReactionAgentSnapshot agent, AgentView view, float time)
        {
            if (agent.BodyState == view.LastBodyState)
            {
                return;
            }

            // Coming round from being knocked out, people get up more slowly.
            int riseTicks = view.LastBodyState == AgentBodyState.Unconscious
                ? scenario.Falls.ComeToGetUpTicks
                : scenario.Falls.GetUpTicks;
            view.RiseSeconds = Mathf.Max(0.05f, (float)riseTicks / FireReactionSimulation.TicksPerSecond);
            view.LastBodyState = agent.BodyState;
            view.BodyStateSince = time;
            view.FromPosition = view.Transform.position;
            view.FromRotation = view.Transform.rotation;
        }

        /// <summary>
        /// How somebody on the floor is drawn: turned exactly as the physics
        /// has their body, with the middle of the drawn figure on the middle
        /// of the physical one. Where there was no room to fall they stay on
        /// their feet in the physics, and are drawn slumped to their knees.
        /// </summary>
        private static void DownPose(FireReactionAgentSnapshot agent, FireReactionAgentSnapshot previous, float blend,
            Vector3 planar, float yaw, out Vector3 position, out Quaternion rotation)
        {
            if (!agent.Pose.IsKnown)
            {
                position = planar + Vector3.up * BodyRadius;
                rotation = Quaternion.Euler(90f, yaw, 0f);
                return;
            }

            BodyPose from = previous.Pose.IsKnown ? previous.Pose : agent.Pose;
            rotation = Quaternion.Slerp(BoxViews.PoseRotation(from), BoxViews.PoseRotation(agent.Pose), blend);
            if ((rotation * Vector3.up).y > 0.7f)
            {
                position = planar + Vector3.up * (BodyHalfHeight * Mathf.Cos(SlumpDegrees * Mathf.Deg2Rad));
                rotation = Quaternion.Euler(SlumpDegrees, yaw, 0f);
                return;
            }

            Vector3 origin = Vector3.Lerp(BoxViews.PoseOrigin(from), BoxViews.PoseOrigin(agent.Pose), blend);
            position = origin + rotation * Vector3.up * PhysicalHalfHeight;
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
            bool burning = agent.IsBurning && participating;
            Color bodyColor = agent.Outcome == AgentTerminalOutcome.Lost
                ? LostColor
                : burning ? Color.Lerp(BurningColor, FlameRed, 0.5f + 0.5f * Mathf.Sin(time * 17f + view.ShakePhase))
                : agent.FearState == AgentFearState.Calm ? CalmColor
                : frozen ? FrozenColor : ScaredColor;
            materials.SetColor(view.Renderer, bodyColor);
            // Capsule space: the body runs from -1 to 1 along Y, radius 0.5.
            view.Flames.Update(burning, new Vector3(0f, -0.7f, 0f), new Vector3(0.45f, 2f, 0.45f), 0.42f);

            if (!participating)
            {
                view.Icons.HideAll();
            }
            else
            {
                Vector3 anchor = planar + Vector3.up * (down ? 0.75f : BodyHalfHeight * 2f + 0.4f + alertJump);
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
                    agent.IsLeading,
                    time);
            }

            UpdateVisionCone(agent, view.Vision, planar, yaw);
        }

        private static readonly Color FlameRed = PresentationMaterials.FlameRed;

        /// <summary>
        /// A person is drawn as a capsule half a metre across and half a metre
        /// from middle to end, which is how they have always looked. The width
        /// matches the 250 mm radius the simulation uses for a body, so what
        /// you see is exactly what bumps.
        /// </summary>
        private const float BodyRadius = 0.25f;
        private const float BodyHalfHeight = 0.5f;

        /// <summary>Half the height of the physical body, feet to middle, in metres.</summary>
        private const float PhysicalHalfHeight = PeopleBodies.HeightMillimetres / 2000f;

        /// <summary>How far forward somebody slumps where there is no room to fall flat.</summary>
        private const float SlumpDegrees = 40f;
        private static readonly Vector3 BodyScale = new Vector3(BodyRadius * 2f, BodyHalfHeight, BodyRadius * 2f);

        /// <summary>
        /// Where the middle of a seated body sits, in metres. Standing, it is
        /// at <see cref="BodyHalfHeight"/> and the head is a metre up; sitting,
        /// it drops to here, which puts the head at 0.8 m -- plainly lower than
        /// standing, and just above the 0.74 m table they are sitting at.
        /// </summary>
        private const float SeatedCentreHeight = 0.3f;

        /// <summary>How far back somebody sitting leans into the chair.</summary>
        private const float SeatedLeanDegrees = 12f;

        /// <summary>
        /// How far a walking body rocks onto each foot in turn, in degrees.
        /// This is the waddle: a person is a capsule with no legs, so the
        /// tipping from side to side is what reads as steps being taken. It is
        /// deliberately more than a real walk -- the look wanted is somebody
        /// play-walking a doll across a table, not a gait.
        /// </summary>
        private const float WaddleRollDegrees = 9f;

        /// <summary>
        /// How far the body twists about its own axis on each step, in
        /// degrees. A rock with no twist reads as a metronome; the two
        /// together read as weight being thrown from one foot to the other.
        /// </summary>
        private const float WaddleTwistDegrees = 5f;

        /// <summary>
        /// How much harder somebody running waddles than somebody walking.
        /// A panicked run is all shoulders.
        /// </summary>
        private const float RunningWaddle = 1.45f;

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

using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// Read-only display for the fire-reaction run. It creates simple geometry
    /// so the prototype can be viewed without authored prefabs or physics.
    /// All animation variation here is presentation-only and never feeds back
    /// into the simulation.
    /// </summary>
    public sealed class FireReactionPrototypePresentation : MonoBehaviour
    {
        private sealed class AgentView
        {
            public Transform Transform;
            public Renderer Renderer;
            public TextMesh AlertText;
            public TextMesh YellText;
            public TextMesh IdleText;
            public LineRenderer Vision;
            public Vector3 LastPlanarPosition;
            public float StridePhase;
            public float YellUntilTime;
            public bool Initialized;
        }

        private sealed class FireCellView
        {
            public Transform Root;
            public Renderer Tile;
            public Transform[] Cubes;
            public Renderer[] Renderers;
            public Vector3[] Offsets;
            public float[] Sizes;
            public float[] Seeds;
            public float SpawnTime;
        }

        [SerializeField] private FireReactionRunner runner;

        private readonly Dictionary<StableAgentId, AgentView> agentViews = new Dictionary<StableAgentId, AgentView>();
        private readonly List<FireCellView> fireViews = new List<FireCellView>();
        private MaterialPropertyBlock propertyBlock;
        private Material fireMaterial;
        private Material roomMaterial;
        private Material wallMaterial;
        private Material visionMaterial;
        private Camera prototypeCamera;
        private FireReactionSnapshot frameSnapshot;
        private int eventsSeen;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly Color CalmColor = new Color(0.78f, 0.84f, 0.9f);
        private static readonly Color ScaredColor = new Color(1f, 0.58f, 0.12f);
        private static readonly Color LostColor = new Color(0.32f, 0.06f, 0.04f);
        private static readonly Color FlameRed = new Color(1f, 0.16f, 0.02f);
        private static readonly Color FlameYellow = new Color(1f, 0.82f, 0.2f);
        private static readonly Color EmberRed = new Color(0.42f, 0.04f, 0.02f);

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<FireReactionRunner>();
            }

            propertyBlock = new MaterialPropertyBlock();
            CreateMaterials();
            CreateRoom();
            CreateCameraAndLight();
            CreateAgents();
        }

        private void Update()
        {
            frameSnapshot = runner == null ? null : runner.Snapshot;
            if (frameSnapshot == null)
            {
                return;
            }

            UpdateYellCues(frameSnapshot);
            UpdateAgents(frameSnapshot, runner.PreviousSnapshot);
            UpdateFire(frameSnapshot);
        }

        private void OnGUI()
        {
            FireReactionSnapshot snapshot = frameSnapshot;
            if (snapshot == null)
            {
                return;
            }

            GUI.color = Color.white;
            string fireText = snapshot.FireActive
                ? $"FIRE  {snapshot.FireCells.Count} squares burning"
                : $"FIRE IN {Mathf.Max(0f, (runner.Scenario.FireActivationTick - snapshot.Tick) / (float)FireReactionSimulation.TicksPerSecond):0.00} s";
            GUI.Label(new Rect(20f, 20f, 360f, 24f), $"Fire-reaction prototype  |  tick {snapshot.Tick}");
            GUI.Label(new Rect(20f, 44f, 360f, 24f), fireText);
            GUI.Label(new Rect(20f, 68f, 360f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount}   Lost {snapshot.LostCount}");
        }

        private void CreateMaterials()
        {
            roomMaterial = CreateMaterial(new Color(0.12f, 0.14f, 0.18f));
            wallMaterial = CreateMaterial(new Color(0.22f, 0.24f, 0.3f));
            visionMaterial = CreateMaterial(new Color(0.25f, 0.7f, 1f));
            fireMaterial = CreateMaterial(FlameRed);
            fireMaterial.EnableKeyword("_EMISSION");
            fireMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            fireMaterial.SetColor(EmissionColorId, FlameRed);
        }

        private void CreateRoom()
        {
            const float roomSize = 12f;
            CreatePrimitive("Room Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f),
                new Vector3(roomSize, 0.1f, roomSize), roomMaterial);
            CreatePrimitive("Room Wall North", PrimitiveType.Cube, new Vector3(0f, 0.75f, roomSize * 0.5f),
                new Vector3(roomSize, 1.5f, 0.4f), wallMaterial);
            CreatePrimitive("Room Wall South", PrimitiveType.Cube, new Vector3(0f, 0.75f, -roomSize * 0.5f),
                new Vector3(roomSize, 1.5f, 0.4f), wallMaterial);
            CreatePrimitive("Room Wall East", PrimitiveType.Cube, new Vector3(roomSize * 0.5f, 0.75f, 0f),
                new Vector3(0.4f, 1.5f, roomSize), wallMaterial);
            CreatePrimitive("Room Wall West", PrimitiveType.Cube, new Vector3(-roomSize * 0.5f, 0.75f, 0f),
                new Vector3(0.4f, 1.5f, roomSize), wallMaterial);
        }

        private void CreateCameraAndLight()
        {
            prototypeCamera = Camera.main;
            if (prototypeCamera == null)
            {
                var cameraObject = new GameObject("Fire Reaction Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                prototypeCamera = cameraObject.GetComponent<Camera>();
            }

            // A classic isometric view: equal horizontal depth on X and Z,
            // with the camera 35.264 degrees above the ground and 45 degrees
            // around the room.
            Vector3 cameraPosition = new Vector3(10f, 10f, -10f);
            prototypeCamera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation((Vector3.zero - cameraPosition).normalized));
            prototypeCamera.orthographic = true;
            prototypeCamera.orthographicSize = 8.5f;

            var lightObject = new GameObject("Fire Reaction Light", typeof(Light));
            Light sceneLight = lightObject.GetComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private void CreateAgents()
        {
            IReadOnlyList<FireReactionAgentDefinition> definitions = runner.Scenario.Agents;
            for (int i = 0; i < definitions.Count; i++)
            {
                FireReactionAgentDefinition definition = definitions[i];
                GameObject agentObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agentObject.name = $"Agent {definition.AgentId.Value} (presentation)";
                RemoveCollider(agentObject);
                agentObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                Renderer agentRenderer = agentObject.GetComponent<Renderer>();
                agentRenderer.material.color = CalmColor;

                var visionObject = new GameObject($"Agent {definition.AgentId.Value} vision cone (presentation)");
                LineRenderer vision = visionObject.AddComponent<LineRenderer>();
                vision.useWorldSpace = true;
                vision.loop = false;
                vision.positionCount = 7;
                vision.startWidth = 0.025f;
                vision.endWidth = 0.025f;
                vision.material = visionMaterial;

                agentViews.Add(definition.AgentId, new AgentView
                {
                    Transform = agentObject.transform,
                    Renderer = agentRenderer,
                    AlertText = CreateLabel(agentObject.transform, "Hazard alert (presentation)", "!", 1.35f, 0.32f, 64,
                        new Color(1f, 0.86f, 0.18f)),
                    YellText = CreateLabel(agentObject.transform, "Yell cue (presentation)", "))", 1.65f, 0.22f, 52,
                        new Color(0.36f, 0.9f, 1f)),
                    IdleText = CreateLabel(agentObject.transform, "Idle activity (presentation)", "...", 1.2f, 0.18f, 48,
                        new Color(0.72f, 0.82f, 0.95f)),
                    Vision = vision
                });
            }
        }

        /// <summary>Shows a short "))" cue for every new yell since the last frame.</summary>
        private void UpdateYellCues(FireReactionSnapshot snapshot)
        {
            for (int i = eventsSeen; i < snapshot.Events.Count; i++)
            {
                CausalEvent record = snapshot.Events[i];
                if (record.EventType == FireReactionEventType.AgentYelled &&
                    agentViews.TryGetValue(record.SourceId, out AgentView view))
                {
                    view.YellUntilTime = Time.time + 0.65f;
                }
            }

            eventsSeen = snapshot.Events.Count;
        }

        private void UpdateAgents(FireReactionSnapshot snapshot, FireReactionSnapshot previousSnapshot)
        {
            // Blend between the last two simulation ticks so movement is
            // smooth at any frame rate.
            float blend = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
                if (!agentViews.TryGetValue(agent.AgentId, out AgentView view))
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

                bool lost = agent.Outcome == AgentTerminalOutcome.Lost;
                bool running = agent.FearState == AgentFearState.Scared;
                float speed = agent.SpeedMillimetresPerTick * FireReactionSimulation.TicksPerSecond /
                              (float)FireReactionSimulation.MillimetresPerMetre;

                // A step bounce driven by distance actually travelled, so feet
                // never appear to slide.
                float travelled = (planar - view.LastPlanarPosition).magnitude;
                view.LastPlanarPosition = planar;
                view.StridePhase += travelled / (running ? 1.1f : 0.7f) * Mathf.PI;
                float bounce = Mathf.Abs(Mathf.Sin(view.StridePhase)) * (running ? 0.12f : 0.05f) *
                               Mathf.Clamp01(speed);
                float alertJump = agent.FearState == AgentFearState.Alert
                    ? 0.18f + Mathf.Abs(Mathf.Sin(Time.time * 18f + agent.AgentId.Value % 997UL)) * 0.18f
                    : 0f;

                if (lost)
                {
                    // Knocked flat where the fire caught them.
                    view.Transform.SetPositionAndRotation(planar + Vector3.up * 0.25f, Quaternion.Euler(90f, yaw, 0f));
                }
                else
                {
                    float lean = Mathf.Min(running ? 14f : 4f, speed * 3f);
                    view.Transform.SetPositionAndRotation(
                        planar + Vector3.up * (0.5f + bounce + alertJump),
                        Quaternion.Euler(lean, yaw, 0f));
                }

                UpdateAgentAppearance(agent, view, planar, yaw);
            }
        }

        private void UpdateAgentAppearance(FireReactionAgentSnapshot agent, AgentView view, Vector3 planar, float yaw)
        {
            bool participating = agent.Participation == AgentParticipation.Participating;
            view.Renderer.material.color = agent.Outcome == AgentTerminalOutcome.Lost
                ? LostColor
                : agent.FearState == AgentFearState.Calm ? CalmColor : ScaredColor;
            bool alert = agent.FearState == AgentFearState.Alert;
            view.AlertText.gameObject.SetActive(participating && alert && agent.AlertSource == AgentAlertSource.Visual);
            view.YellText.gameObject.SetActive(participating &&
                                               ((alert && agent.AlertSource == AgentAlertSource.Yell) ||
                                                Time.time < view.YellUntilTime));
            view.IdleText.gameObject.SetActive(participating && agent.FearState == AgentFearState.Calm &&
                                               (agent.ActivityState == AgentActivityState.Standing ||
                                                agent.ActivityState == AgentActivityState.LookingAround));
            UpdateVisionCone(agent, view.Vision, planar, yaw);
        }

        private void UpdateVisionCone(FireReactionAgentSnapshot agent, LineRenderer vision, Vector3 planar, float yaw)
        {
            vision.enabled = agent.Participation == AgentParticipation.Participating;
            if (!vision.enabled)
            {
                return;
            }

            float range = runner.Scenario.VisionRangeMillimetres / (float)FireReactionSimulation.MillimetresPerMetre;
            Vector3 origin = new Vector3(planar.x, 0.03f, planar.z);
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

        private void UpdateFire(FireReactionSnapshot snapshot)
        {
            while (fireViews.Count < snapshot.FireCells.Count)
            {
                fireViews.Add(CreateFireCell(snapshot.FireCells[fireViews.Count], fireViews.Count + 1));
            }

            float time = Time.time;
            for (int i = 0; i < fireViews.Count; i++)
            {
                AnimateFireCell(fireViews[i], time);
            }
        }

        private FireCellView CreateFireCell(FireCellSnapshot cell, int number)
        {
            var root = new GameObject($"Fire cell {number} (read-only presentation)").transform;
            root.position = ToUnityPosition(cell.Centre);
            float cellSize = (cell.Bounds.MaxX - cell.Bounds.MinX) / (float)FireReactionSimulation.MillimetresPerMetre;

            // A dim glowing floor tile marks the exact square that burns.
            GameObject tile = CreatePrimitive("Scorch", PrimitiveType.Cube, Vector3.zero,
                new Vector3(cellSize * 0.96f, 0.02f, cellSize * 0.96f), fireMaterial);
            tile.transform.SetParent(root, false);
            tile.transform.localPosition = new Vector3(0f, 0.01f, 0f);

            int cubeCount = Hash01(cell.CellX, cell.CellZ, 0) < 0.5f ? 2 : 3;
            var view = new FireCellView
            {
                Root = root,
                Tile = tile.GetComponent<Renderer>(),
                Cubes = new Transform[cubeCount],
                Renderers = new Renderer[cubeCount],
                Offsets = new Vector3[cubeCount],
                Sizes = new float[cubeCount],
                Seeds = new float[cubeCount],
                SpawnTime = Time.time
            };

            float spread = cellSize * 0.28f;
            for (int k = 0; k < cubeCount; k++)
            {
                float seed = Hash01(cell.CellX, cell.CellZ, k + 1);
                GameObject cube = CreatePrimitive($"Flame {k + 1}", PrimitiveType.Cube, Vector3.zero, Vector3.one, fireMaterial);
                cube.transform.SetParent(root, false);
                view.Cubes[k] = cube.transform;
                view.Renderers[k] = cube.GetComponent<Renderer>();
                view.Seeds[k] = seed;
                view.Sizes[k] = Mathf.Lerp(0.15f, 0.35f, Hash01(cell.CellX, cell.CellZ, k + 11));
                view.Offsets[k] = new Vector3(
                    (Hash01(cell.CellX, cell.CellZ, k + 21) * 2f - 1f) * spread,
                    0f,
                    (Hash01(cell.CellX, cell.CellZ, k + 31) * 2f - 1f) * spread);
            }

            return view;
        }

        private void AnimateFireCell(FireCellView view, float time)
        {
            float age = time - view.SpawnTime;
            float pop = age < 0.4f ? EaseOutBack(age / 0.4f) : 1f;
            float ember = Mathf.Clamp01((age - 10f) / 20f);

            Color tileColor = Color.Lerp(new Color(0.55f, 0.1f, 0.02f), EmberRed * 0.6f, ember);
            SetColors(view.Tile, tileColor, tileColor * (0.7f + 0.15f * Mathf.Sin(time * 4f + view.Seeds[0] * 20f)));

            for (int k = 0; k < view.Cubes.Length; k++)
            {
                float seed = view.Seeds[k];
                float flicker = 1f + 0.18f * Mathf.Sin(time * (7f + seed * 6f) + seed * 20f) +
                                0.08f * Mathf.Sin(time * (13f + seed * 9f));
                float size = view.Sizes[k] * pop * flicker * Mathf.Lerp(1f, 0.65f, ember);
                float hover = (0.06f + 0.06f * seed) * (0.5f + 0.5f * Mathf.Sin(time * (3f + seed * 3f) + seed * 10f)) *
                              (1f - 0.7f * ember);

                Transform cube = view.Cubes[k];
                cube.localPosition = view.Offsets[k] + Vector3.up * (size * 0.5f + 0.03f + hover);
                cube.localRotation = Quaternion.Euler(
                    12f * Mathf.Sin(time * 2f + seed * 7f),
                    time * (40f + seed * 80f) + seed * 360f,
                    12f * Mathf.Cos(time * 2.3f + seed * 5f));
                cube.localScale = Vector3.one * Mathf.Max(0.001f, size);

                float heat = 0.5f + 0.5f * Mathf.Sin(time * (5f + seed * 5f) + seed * 30f);
                Color color = Color.Lerp(Color.Lerp(FlameRed, FlameYellow, heat), EmberRed, ember);
                SetColors(view.Renderers[k], color, color * Mathf.Lerp(2.2f, 0.6f, ember));
            }
        }

        private void SetColors(Renderer target, Color baseColor, Color emission)
        {
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(ColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            target.SetPropertyBlock(propertyBlock);
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        /// <summary>Stable 0..1 variation per fire cell; presentation-only, never the simulation generator.</summary>
        private static float Hash01(int x, int z, int salt)
        {
            unchecked
            {
                uint hash = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995U;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFFU) / 16777216f;
            }
        }

        private static TextMesh CreateLabel(
            Transform parent,
            string objectName,
            string text,
            float height,
            float characterSize,
            int fontSize,
            Color color)
        {
            var labelObject = new GameObject(objectName);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, height, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = fontSize;
            label.color = color;
            labelObject.SetActive(false);
            return label;
        }

        private static Vector3 ToUnityPosition(LogicalPosition position)
        {
            return new Vector3(
                position.X / (float)FireReactionSimulation.MillimetresPerMetre,
                0f,
                position.Z / (float)FireReactionSimulation.MillimetresPerMetre);
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = objectName;
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(gameObject);
            return gameObject;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }
    }
}

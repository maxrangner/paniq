using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// Read-only display for the fire-reaction run. It creates simple geometry
    /// so the prototype can be viewed without authored prefabs or physics.
    /// All animation variation here is presentation-only and never feeds back
    /// into the simulation. The one thing it sends the other way is a player
    /// click on a door, which it hands to the runner as a door ID; the door
    /// leaves keep a collider only so the click can find which door it hit.
    /// </summary>
    public sealed class FireReactionPrototypePresentation : MonoBehaviour
    {
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
            public float Tilt;
            public bool Initialized;
            public float LungeStart = -10f;
            public float EscapedSince = -1f;
            public Vector3 EscapePosition;
        }

        private sealed class DoorView
        {
            public StableAgentId DoorId;
            public Transform Hinge;
            public Renderer Leaf;
            public Collider Collider;
            public Vector3 Centre;
            public float ClosedYaw;
            public float OpenYaw;
            public float Swing;
            public float ShakeStart = -10f;
            public DoorState State;
        }

        private sealed class BoxView
        {
            public Transform Transform;
            public float Height;
            public float HopStart = -10f;
            public float HopStrength;
        }

        /// <summary>A ring on the floor showing how far a noise carries.</summary>
        private sealed class SoundRipple
        {
            public LineRenderer Line;
            public Vector3 Centre;
            public float Radius;
            public float StartTime;
            public Color Color;
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
        private readonly List<SoundRipple> ripples = new List<SoundRipple>();
        private readonly List<DoorView> doorViews = new List<DoorView>();
        private readonly Dictionary<Collider, DoorView> doorByCollider = new Dictionary<Collider, DoorView>();
        private readonly Dictionary<StableAgentId, BoxView> boxViews = new Dictionary<StableAgentId, BoxView>();
        private DoorView hoveredDoor;
        private Material doorMaterial;
        private Material boxMaterial;
        private Material outsideMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Material fireMaterial;
        private Material roomMaterial;
        private Material wallMaterial;
        private Material visionMaterial;
        private Material iconMaterial;
        private Camera prototypeCamera;
        private FireReactionSnapshot frameSnapshot;
        private int eventsSeen;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private const float RippleDuration = 0.55f;
        private const float FallSeconds = 0.22f;
        private const float WallHeight = 1.5f;
        private const float WallThickness = 0.4f;
        private const float DoorHeight = 1.3f;
        private const float DoorSwingSeconds = 0.3f;
        private const float EscapeFadeSeconds = 0.45f;

        private static readonly Color LockedDoorColor = new Color(0.86f, 0.14f, 0.1f);
        private static readonly Color UnlockedDoorColor = new Color(0.18f, 0.8f, 0.3f);
        private static readonly Color BoxColor = new Color(0.62f, 0.45f, 0.26f);

        private static readonly Color CalmColor = new Color(0.78f, 0.84f, 0.9f);
        private static readonly Color ScaredColor = new Color(1f, 0.58f, 0.12f);
        private static readonly Color FrozenColor = new Color(0.72f, 0.8f, 0.95f);
        private static readonly Color YellRippleColor = new Color(0.4f, 0.92f, 1f, 0.55f);
        private static readonly Color ThudRippleColor = new Color(1f, 0.85f, 0.6f, 0.6f);
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
            CreateBoxes();
        }

        private void Update()
        {
            frameSnapshot = runner == null ? null : runner.Snapshot;
            if (frameSnapshot == null)
            {
                return;
            }

            HandleDoorClicks();
            UpdateEventCues(frameSnapshot);
            UpdateAgents(frameSnapshot, runner.PreviousSnapshot);
            UpdateDoors(frameSnapshot);
            UpdateBoxes(frameSnapshot, runner.PreviousSnapshot);
            UpdateRipples();
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
            GUI.Label(new Rect(20f, 68f, 520f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount})   " +
                $"Down {snapshot.DownCount}   Lost {snapshot.LostCount}   Escaped {snapshot.EscapedCount}");
            GUI.Label(new Rect(20f, 92f, 520f, 24f),
                "Click a door: red = locked. Click once to unlock (green), again to open.");
            if (hoveredDoor != null)
            {
                string action = hoveredDoor.State == DoorState.Locked ? "Click to unlock"
                    : hoveredDoor.State == DoorState.Unlocked ? "Click to open"
                    : "Open";
                GUI.Label(new Rect(20f, 116f, 360f, 24f), $"Door {hoveredDoor.DoorId.Value}: {action}");
            }
        }

        private void CreateMaterials()
        {
            roomMaterial = CreateMaterial(new Color(0.12f, 0.14f, 0.18f));
            wallMaterial = CreateMaterial(new Color(0.22f, 0.24f, 0.3f));
            visionMaterial = CreateMaterial(new Color(0.25f, 0.7f, 1f));

            // Unlit and coloured per vertex, so icons stay bright and can fade.
            Shader iconShader = Shader.Find("Sprites/Default") ??
                                Shader.Find("Universal Render Pipeline/Unlit");
            iconMaterial = new Material(iconShader);
            doorMaterial = CreateMaterial(LockedDoorColor);
            doorMaterial.EnableKeyword("_EMISSION");
            boxMaterial = CreateMaterial(BoxColor);
            outsideMaterial = CreateMaterial(new Color(0.2f, 0.22f, 0.2f));
            fireMaterial = CreateMaterial(FlameRed);
            fireMaterial.EnableKeyword("_EMISSION");
            fireMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            fireMaterial.SetColor(EmissionColorId, FlameRed);
        }

        /// <summary>
        /// Floor and walls from the scenario's room, each wall split around
        /// its doors, a door leaf in every gap and a strip of ground outside.
        /// </summary>
        private void CreateRoom()
        {
            LogicalBounds room = runner.Scenario.RoomBounds;
            float minX = Metres(room.MinX);
            float maxX = Metres(room.MaxX);
            float minZ = Metres(room.MinZ);
            float maxZ = Metres(room.MaxZ);
            CreatePrimitive("Room Floor", PrimitiveType.Cube, new Vector3((minX + maxX) * 0.5f, -0.05f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.1f, maxZ - minZ), roomMaterial);

            IReadOnlyList<FireReactionDoorDefinition> doors = runner.Scenario.Doors;
            foreach (WallSide side in new[] { WallSide.North, WallSide.East, WallSide.South, WallSide.West })
            {
                bool alongX = side == WallSide.North || side == WallSide.South;
                float wallLine = side == WallSide.North ? maxZ : side == WallSide.South ? minZ : side == WallSide.East ? maxX : minX;
                float start = (alongX ? minX : minZ) - WallThickness * 0.5f;
                float end = (alongX ? maxX : maxZ) + WallThickness * 0.5f;

                var gaps = new List<FireReactionDoorDefinition>();
                for (int i = 0; i < doors.Count; i++)
                {
                    if (doors[i].Side == side)
                    {
                        gaps.Add(doors[i]);
                    }
                }

                gaps.Sort((left, right) => left.CentreAlongWallMillimetres.CompareTo(right.CentreAlongWallMillimetres));
                float cursor = start;
                int piece = 1;
                foreach (FireReactionDoorDefinition door in gaps)
                {
                    float gapStart = Metres(door.CentreAlongWallMillimetres - door.WidthMillimetres / 2);
                    CreateWallPiece(side, piece++, alongX, wallLine, cursor, gapStart);
                    cursor = Metres(door.CentreAlongWallMillimetres + door.WidthMillimetres / 2);
                    CreateDoor(door, alongX, wallLine);
                }

                CreateWallPiece(side, piece, alongX, wallLine, cursor, end);
            }
        }

        private void CreateWallPiece(WallSide side, int piece, bool alongX, float wallLine, float from, float to)
        {
            if (to - from <= 0.001f)
            {
                return;
            }

            float middle = (from + to) * 0.5f;
            Vector3 position = alongX ? new Vector3(middle, WallHeight * 0.5f, wallLine) : new Vector3(wallLine, WallHeight * 0.5f, middle);
            Vector3 scale = alongX ? new Vector3(to - from, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, to - from);
            CreatePrimitive($"Room Wall {side} {piece}", PrimitiveType.Cube, position, scale, wallMaterial);
        }

        /// <summary>A door leaf hinged at one side of the gap, which swings outward when the door opens.</summary>
        private void CreateDoor(FireReactionDoorDefinition door, bool alongX, float wallLine)
        {
            float width = Metres(door.WidthMillimetres);
            float centre = Metres(door.CentreAlongWallMillimetres);
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 outward = door.Side == WallSide.North ? Vector3.forward
                : door.Side == WallSide.South ? Vector3.back
                : door.Side == WallSide.East ? Vector3.right
                : Vector3.left;
            Vector3 gapCentre = alongX ? new Vector3(centre, 0f, wallLine) : new Vector3(wallLine, 0f, centre);

            var hinge = new GameObject($"Door {door.DoorId.Value} hinge (presentation)").transform;
            hinge.position = gapCentre - along * (width * 0.5f);
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = $"Door {door.DoorId.Value} (click target)";
            leaf.transform.SetParent(hinge, false);
            leaf.transform.localPosition = new Vector3(width * 0.5f, DoorHeight * 0.5f, 0f);
            leaf.transform.localScale = new Vector3(width - 0.04f, DoorHeight, 0.08f);
            Renderer leafRenderer = leaf.GetComponent<Renderer>();
            leafRenderer.sharedMaterial = doorMaterial;

            // A strip of ground outside, as far as the doorway reaches.
            float depth = Metres(runner.Scenario.DoorwayDepthMillimetres);
            CreatePrimitive($"Door {door.DoorId.Value} outside ground", PrimitiveType.Cube,
                gapCentre + outward * (depth * 0.5f + WallThickness * 0.25f) + Vector3.down * 0.05f,
                alongX ? new Vector3(width + 0.4f, 0.1f, depth) : new Vector3(depth, 0.1f, width + 0.4f),
                outsideMaterial);

            var view = new DoorView
            {
                DoorId = door.DoorId,
                Hinge = hinge,
                Leaf = leafRenderer,
                Collider = leaf.GetComponent<Collider>(),
                Centre = gapCentre,
                ClosedYaw = YawOf(along),
                OpenYaw = YawOf(outward),
                State = DoorState.Locked
            };
            hinge.rotation = Quaternion.Euler(0f, view.ClosedYaw, 0f);
            doorViews.Add(view);
            doorByCollider.Add(view.Collider, view);
        }

        /// <summary>The yaw that turns local +X to face <paramref name="direction"/>.</summary>
        private static float YawOf(Vector3 direction)
        {
            return Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
        }

        private void CreateBoxes()
        {
            IReadOnlyList<FireReactionPhysicsObjectDefinition> objects = runner.Scenario.PhysicsObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                FireReactionPhysicsObjectDefinition definition = objects[i];
                float size = Metres(definition.SizeMillimetres);
                float height = size * 0.75f;
                GameObject box = CreatePrimitive($"Box {definition.ObjectId.Value} (presentation)", PrimitiveType.Cube,
                    ToUnityPosition(definition.InitialPosition) + Vector3.up * (height * 0.5f),
                    new Vector3(size, height, size), boxMaterial);

                // Slightly different cardboard for each box; presentation-only variation.
                Color shade = BoxColor * (0.85f + 0.3f * Hash01((int)definition.ObjectId.Value, 7, 3));
                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, shade);
                propertyBlock.SetColor(ColorId, shade);
                box.GetComponent<Renderer>().SetPropertyBlock(propertyBlock);
                boxViews.Add(definition.ObjectId, new BoxView { Transform = box.transform, Height = height });
            }
        }

        /// <summary>
        /// A left click on a door leaf becomes a door click for the runner.
        /// The ray only finds which door was clicked; the simulation decides
        /// what the click does.
        /// </summary>
        private void HandleDoorClicks()
        {
            hoveredDoor = null;
            Mouse mouse = Mouse.current;
            if (mouse == null || prototypeCamera == null)
            {
                return;
            }

            Physics.SyncTransforms();
            Ray ray = prototypeCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f) ||
                !doorByCollider.TryGetValue(hit.collider, out DoorView door))
            {
                return;
            }

            hoveredDoor = door;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                runner.QueueDoorClick(door.DoorId);
            }
        }

        private void UpdateDoors(FireReactionSnapshot snapshot)
        {
            float time = Time.time;
            for (int i = 0; i < doorViews.Count; i++)
            {
                DoorView view = doorViews[i];
                for (int d = 0; d < snapshot.Doors.Count; d++)
                {
                    if (snapshot.Doors[d].DoorId == view.DoorId)
                    {
                        view.State = snapshot.Doors[d].State;
                        break;
                    }
                }

                float target = view.State == DoorState.Open ? 1f : 0f;
                view.Swing = Mathf.MoveTowards(view.Swing, target, Time.deltaTime / DoorSwingSeconds);
                float yaw = Mathf.LerpAngle(view.ClosedYaw, view.OpenYaw, Mathf.SmoothStep(0f, 1f, view.Swing));

                // A shove makes a shut door judder in its frame.
                float shakeAge = time - view.ShakeStart;
                if (shakeAge < 0.3f && view.State != DoorState.Open)
                {
                    yaw += Mathf.Sin(shakeAge * 70f) * 4f * (1f - shakeAge / 0.3f);
                }

                view.Hinge.rotation = Quaternion.Euler(0f, yaw, 0f);
                Color color = view.State == DoorState.Locked ? LockedDoorColor : UnlockedDoorColor;
                if (view == hoveredDoor)
                {
                    color = Color.Lerp(color, Color.white, 0.3f);
                }

                SetColors(view.Leaf, color, color * 0.35f);
            }
        }

        private void UpdateBoxes(FireReactionSnapshot snapshot, FireReactionSnapshot previousSnapshot)
        {
            float blend = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            float time = Time.time;
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                FireReactionPhysicsObjectSnapshot box = snapshot.PhysicsObjects[i];
                if (!boxViews.TryGetValue(box.ObjectId, out BoxView view))
                {
                    continue;
                }

                FireReactionPhysicsObjectSnapshot previous = previousSnapshot != null && i < previousSnapshot.PhysicsObjects.Count
                    ? previousSnapshot.PhysicsObjects[i]
                    : box;
                Vector3 planar = Vector3.Lerp(ToUnityPosition(previous.Position), ToUnityPosition(box.Position), blend);
                float yaw = Mathf.LerpAngle(previous.HeadingDegrees, box.HeadingDegrees, blend);

                // A little hop and tip when something hits it.
                float hopAge = (time - view.HopStart) / 0.3f;
                float hop = hopAge < 1f ? Mathf.Sin(hopAge * Mathf.PI) * view.HopStrength : 0f;
                view.Transform.SetPositionAndRotation(
                    planar + Vector3.up * (view.Height * 0.5f + hop * 0.12f),
                    Quaternion.Euler(hop * 18f, yaw, 0f));
            }
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
            prototypeCamera.orthographicSize = 9.5f;

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
                    Icons = new AgentIconViews($"Agent {definition.AgentId.Value}", iconMaterial,
                        definition.AgentId.Value % 60UL),
                    Vision = vision,
                    ShakePhase = definition.AgentId.Value % 97UL
                });
            }
        }

        /// <summary>Starts icons and floor ripples for every new event since the last frame.</summary>
        private void UpdateEventCues(FireReactionSnapshot snapshot)
        {
            float time = Time.time;
            for (int i = eventsSeen; i < snapshot.Events.Count; i++)
            {
                CausalEvent record = snapshot.Events[i];
                agentViews.TryGetValue(record.SourceId, out AgentView view);
                switch (record.EventType)
                {
                    case FireReactionEventType.AgentAlerted:
                    case FireReactionEventType.AgentNoticedSound:
                        view?.Icons.Notice(time);
                        break;
                    case FireReactionEventType.AgentYelled:
                        view?.Icons.Yell(time);
                        StartRipple(record.Position, record.StrengthMillimetres, YellRippleColor);
                        break;
                    case FireReactionEventType.AgentsCollided:
                        StartRipple(record.Position, runner.Scenario.BumpSoundRadiusMillimetres, ThudRippleColor);
                        break;
                    case FireReactionEventType.AgentTripped:
                        StartRipple(record.Position, record.StrengthMillimetres, ThudRippleColor);
                        break;
                    case FireReactionEventType.AgentForcedDoor:
                        if (view != null)
                        {
                            view.LungeStart = time;
                        }

                        NearestDoor(record.Position).ShakeStart = time;
                        StartRipple(record.Position, record.StrengthMillimetres, ThudRippleColor);
                        break;
                    case FireReactionEventType.BoxBumped:
                        HopNearestBox(record.Position, Mathf.Clamp01(record.StrengthMillimetres / 80f), time);
                        if (record.StrengthMillimetres >= runner.Scenario.BumpMinimumSpeed)
                        {
                            StartRipple(record.Position, runner.Scenario.BumpSoundRadiusMillimetres, ThudRippleColor);
                        }

                        break;
                    case FireReactionEventType.BoxHitAgent:
                        if (boxViews.TryGetValue(record.SourceId, out BoxView hitBox))
                        {
                            hitBox.HopStart = time;
                            hitBox.HopStrength = 0.6f;
                        }

                        StartRipple(record.Position, runner.Scenario.BumpSoundRadiusMillimetres, ThudRippleColor);
                        break;
                }
            }

            eventsSeen = snapshot.Events.Count;
        }

        private DoorView NearestDoor(LogicalPosition position)
        {
            Vector3 point = ToUnityPosition(position);
            DoorView nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < doorViews.Count; i++)
            {
                float distance = (doorViews[i].Centre - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = doorViews[i];
                }
            }

            return nearest ?? new DoorView();
        }

        private void HopNearestBox(LogicalPosition position, float strength, float time)
        {
            Vector3 point = ToUnityPosition(position);
            BoxView nearest = null;
            float best = float.MaxValue;
            foreach (BoxView box in boxViews.Values)
            {
                Vector3 offset = box.Transform.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude < best)
                {
                    best = offset.sqrMagnitude;
                    nearest = box;
                }
            }

            if (nearest != null)
            {
                nearest.HopStart = time;
                nearest.HopStrength = Mathf.Max(0.25f, strength);
            }
        }

        private void StartRipple(LogicalPosition position, int radiusMillimetres, Color color)
        {
            if (radiusMillimetres <= 0)
            {
                return;
            }

            SoundRipple ripple = null;
            for (int i = 0; i < ripples.Count; i++)
            {
                if (!ripples[i].Line.enabled)
                {
                    ripple = ripples[i];
                    break;
                }
            }

            if (ripple == null)
            {
                var rippleObject = new GameObject($"Sound ripple {ripples.Count + 1} (presentation)");
                rippleObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                LineRenderer line = rippleObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.alignment = LineAlignment.TransformZ;
                line.positionCount = 48;
                line.sharedMaterial = iconMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                ripple = new SoundRipple { Line = line };
                ripples.Add(ripple);
            }

            ripple.Centre = ToUnityPosition(position) + Vector3.up * 0.04f;
            ripple.Radius = radiusMillimetres / (float)FireReactionSimulation.MillimetresPerMetre;
            ripple.StartTime = Time.time;
            ripple.Color = color;
            ripple.Line.enabled = true;
        }

        private void UpdateRipples()
        {
            float time = Time.time;
            for (int r = 0; r < ripples.Count; r++)
            {
                SoundRipple ripple = ripples[r];
                if (!ripple.Line.enabled)
                {
                    continue;
                }

                float t = (time - ripple.StartTime) / RippleDuration;
                if (t >= 1f)
                {
                    ripple.Line.enabled = false;
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Max(0.05f, ripple.Radius * eased);
                LineRenderer line = ripple.Line;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i / (float)line.positionCount * Mathf.PI * 2f;
                    line.SetPosition(i, ripple.Centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }

                Color color = ripple.Color;
                color.a *= 1f - t;
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Lerp(0.06f, 0.02f, t);
            }
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

                if (agent.Outcome == AgentTerminalOutcome.Escaped)
                {
                    UpdateEscapedAgent(agent, view, planar, yaw);
                    continue;
                }

                bool lost = agent.Outcome == AgentTerminalOutcome.Lost;
                bool frozen = agent.ActivityState == AgentActivityState.Frozen;
                bool running = agent.FearState == AgentFearState.Scared && !frozen;
                float speed = agent.SpeedMillimetresPerTick * FireReactionSimulation.TicksPerSecond /
                              (float)FireReactionSimulation.MillimetresPerMetre;
                float time = Time.time;

                // A step bounce driven by distance actually travelled, so feet
                // never appear to slide.
                float travelled = (planar - view.LastPlanarPosition).magnitude;
                view.LastPlanarPosition = planar;
                view.StridePhase += travelled / (running ? 1.1f : 0.7f) * Mathf.PI;
                float bounce = Mathf.Abs(Mathf.Sin(view.StridePhase)) * (running ? 0.12f : 0.05f) *
                               Mathf.Clamp01(speed);
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

                UpdateAgentAppearance(agent, view, planar, yaw, down, alertJump, time);
            }
        }

        /// <summary>Someone who got out keeps walking a few steps and shrinks away.</summary>
        private void UpdateEscapedAgent(FireReactionAgentSnapshot agent, AgentView view, Vector3 planar, float yaw)
        {
            if (view.EscapedSince < 0f)
            {
                view.EscapedSince = Time.time;
                view.EscapePosition = planar;
                view.Icons.HideAll();
                view.Vision.enabled = false;
            }

            float age = (Time.time - view.EscapedSince) / EscapeFadeSeconds;
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
                view.LastBodyState = agent.BodyState;
                view.BodyStateSince = time;
                view.TiltAtStateChange = view.Tilt;
            }

            float age = time - view.BodyStateSince;
            switch (agent.BodyState)
            {
                case AgentBodyState.Fallen:
                    view.Tilt = Mathf.Lerp(view.TiltAtStateChange, 90f, EaseInQuad(age / FallSeconds));
                    break;
                case AgentBodyState.GettingUp:
                    float rise = (float)runner.Scenario.GetUpTicks / FireReactionSimulation.TicksPerSecond;
                    view.Tilt = Mathf.Lerp(view.TiltAtStateChange, 0f, Mathf.SmoothStep(0f, 1f, age / rise));
                    break;
                default:
                    view.Tilt = 0f;
                    break;
            }

            return view.Tilt;
        }

        private void UpdateAgentAppearance(
            FireReactionAgentSnapshot agent,
            AgentView view,
            Vector3 planar,
            float yaw,
            bool down,
            float alertJump,
            float time)
        {
            bool participating = agent.Participation == AgentParticipation.Participating;
            bool frozen = agent.ActivityState == AgentActivityState.Frozen;
            view.Renderer.material.color = agent.Outcome == AgentTerminalOutcome.Lost
                ? LostColor
                : agent.FearState == AgentFearState.Calm ? CalmColor
                : frozen ? FrozenColor : ScaredColor;

            if (!participating)
            {
                view.Icons.HideAll();
            }
            else
            {
                Vector3 anchor = planar + Vector3.up * (down ? 0.75f : 1.4f + alertJump);
                Vector3 facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float facingSide = Vector3.Dot(facing, prototypeCamera.transform.right) >= 0f ? 1f : -1f;
                bool calm = agent.FearState == AgentFearState.Calm;
                view.Icons.Update(
                    anchor,
                    prototypeCamera.transform.rotation,
                    facingSide,
                    frozen,
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

        private static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        private static float Metres(int millimetres) => millimetres / (float)FireReactionSimulation.MillimetresPerMetre;

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

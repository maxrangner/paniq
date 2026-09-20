using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// Read-only display for the fire-reaction run. It builds simple geometry
    /// so the prototype can be viewed without authored prefabs or physics,
    /// then each frame hands the latest snapshots to the views and turns new
    /// events into icons, ripples, hops and door judders by the IDs the
    /// events name. All animation variation is presentation-only and never
    /// feeds back into the simulation. The one thing sent the other way is a
    /// player click on a door, handed to the runner as a door ID.
    /// </summary>
    public sealed class FireReactionPrototypePresentation : MonoBehaviour
    {
        [SerializeField] private FireReactionRunner runner;

        private PresentationMaterials materials;
        private Transform root;
        private Camera prototypeCamera;
        private RoomView room;
        private AgentViews agents;
        private BoxViews boxes;
        private FireView fire;
        private SoundRipples ripples;
        private DoorClickInput clicks;
        private FireReactionSnapshot frameSnapshot;
        private SimulationId? hoveredDoor;
        private bool showStats;
        private int eventsSeen;

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<FireReactionRunner>();
            }

            // The runner starts first, so its simulation and the data it runs on exist.
            FireReactionScenarioData scenario = runner.Simulation.Scenario;
            materials = new PresentationMaterials();
            root = new GameObject("Fire reaction presentation").transform;
            prototypeCamera = CreateCameraAndLight(root, scenario);
            room = new RoomView(scenario, materials, root);
            agents = new AgentViews(scenario, materials, root);
            boxes = new BoxViews(scenario, materials, root);
            fire = new FireView(materials, root);
            ripples = new SoundRipples(materials.Icon, root);
            clicks = new DoorClickInput(runner, room);
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            materials?.Destroy();
        }

        private void Update()
        {
            frameSnapshot = runner == null ? null : runner.Snapshot;
            if (frameSnapshot == null)
            {
                return;
            }

            float time = Time.time;

            // Blend between the last two simulation ticks so movement is
            // smooth at any frame rate.
            float blend = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            FireReactionSnapshot previous = runner.PreviousSnapshot;

            hoveredDoor = clicks.Update(prototypeCamera);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                showStats = !showStats;
            }

            PlayNewEvents(frameSnapshot, time);
            agents.Update(frameSnapshot, previous, blend, time, prototypeCamera.transform);
            room.Update(frameSnapshot, hoveredDoor, time, Time.deltaTime);
            boxes.Update(frameSnapshot, previous, blend, time);
            ripples.Update(time);
            fire.Update(frameSnapshot, time);
        }

        private void OnGUI()
        {
            if (frameSnapshot != null)
            {
                PrototypeHud.Draw(frameSnapshot, runner.Simulation.Scenario, hoveredDoor,
                    hoveredDoor.HasValue ? room.StateOf(hoveredDoor.Value) : DoorState.Locked);
                if (showStats)
                {
                    PrototypeHud.DrawStats(frameSnapshot);
                }
            }
        }

        /// <summary>Starts icons, ripples, hops and judders for every event since the last frame.</summary>
        private void PlayNewEvents(FireReactionSnapshot snapshot, float time)
        {
            FireReactionScenarioData scenario = runner.Simulation.Scenario;
            int thudReach = scenario.Hearing.BumpSoundRadiusMillimetres;
            for (int i = eventsSeen; i < snapshot.Events.Count; i++)
            {
                CausalEvent record = snapshot.Events[i];
                switch (record.EventType)
                {
                    case FireReactionEventType.AgentAlerted:
                    case FireReactionEventType.AgentNoticedSound:
                        agents.Notice(record.SourceId, time);
                        break;
                    case FireReactionEventType.AgentShookAwake:
                        // The person shaken awake gets the "!".
                        agents.Notice(record.TargetId, time);
                        break;
                    case FireReactionEventType.AgentYelled:
                        agents.Yell(record.SourceId, time);
                        ripples.Start(record.Position, record.Strength, SoundRipples.YellColor, time);
                        break;
                    case FireReactionEventType.AgentsCollided:
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.AgentTripped:
                        ripples.Start(record.Position, record.Strength, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.DoorBrokenDown:
                    case FireReactionEventType.DoorClosed:
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.AgentForcedDoor:
                        agents.Lunge(record.SourceId, time);
                        room.Shake(record.TargetId, time);
                        ripples.Start(record.Position, record.Strength, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.BoxBumped:
                        boxes.Hop(record.TargetId, Mathf.Max(0.25f, Mathf.Clamp01(record.Strength / 80f)), time);
                        if (record.Strength >= scenario.Falls.BumpMinimumSpeed)
                        {
                            ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        }

                        break;
                    case FireReactionEventType.BoxHitAgent:
                        boxes.Hop(record.SourceId, 0.6f, time);
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        break;
                }
            }

            eventsSeen = snapshot.Events.Count;
        }

        /// <summary>
        /// A classic isometric view: equal horizontal depth on X and Z, with
        /// the camera 35.264 degrees above the ground and 45 degrees around
        /// the room.
        /// </summary>
        private static Camera CreateCameraAndLight(Transform parent, FireReactionScenarioData scenario)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Fire Reaction Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(parent, false);
                camera = cameraObject.GetComponent<Camera>();
            }

            // Framed around every room, so a bigger floor plan still fits on screen.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (FireReactionRoomDefinition room in scenario.Rooms)
            {
                minX = Mathf.Min(minX, Metres(room.Bounds.MinX));
                maxX = Mathf.Max(maxX, Metres(room.Bounds.MaxX));
                minZ = Mathf.Min(minZ, Metres(room.Bounds.MinZ));
                maxZ = Mathf.Max(maxZ, Metres(room.Bounds.MaxZ));
            }

            var centre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            Vector3 offset = new Vector3(10f, 10f, -10f).normalized * 40f;
            camera.transform.SetPositionAndRotation(centre + offset, Quaternion.LookRotation(-offset.normalized));
            camera.orthographic = true;

            // Seen from 35.264 degrees up, the floor's diagonal spans this much
            // across the screen, and this much up it; 1.5 m of wall and a
            // tenth of a margin are added on top.
            float across = (maxX - minX + (maxZ - minZ)) * 0.70711f;
            float up = across * 0.57735f + 1.5f;
            float aspect = camera.aspect > 0.1f ? camera.aspect : 16f / 9f;
            camera.orthographicSize = Mathf.Max(up * 0.5f, across * 0.5f / aspect) * 1.1f;

            var lightObject = new GameObject("Fire Reaction Light", typeof(Light));
            lightObject.transform.SetParent(parent, false);
            Light sceneLight = lightObject.GetComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            return camera;
        }
    }
}

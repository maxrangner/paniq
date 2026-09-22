using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using static Paniq.Presentation.PresentationUtility;

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

        [Tooltip("How the particle effects look. Leave empty for the built-in look.")]
        [SerializeField] private ParticleEffectSettings particleEffects;

        private PresentationMaterials materials;
        private Transform root;
        private Camera prototypeCamera;
        private RoomView room;
        private AgentViews agents;
        private BoxViews boxes;
        private FireView fire;
        private SoundRipples ripples;
        private SprayView spray;
        private NavigationGridView navigationGrid;
        private PopBursts pops;
        private ParticleEffects effects;
        private PlayerInput input;
        private CameraRig cameraRig;
        private BuildingShellView shell;
        private RoundScreens screens;

        /// <summary>
        /// The round read back as a list, opened from the end card. The end
        /// card only asks for it; somebody has to own it and draw it, and that
        /// is here, because this is the one place that draws anything.
        /// </summary>
        private readonly EventLogScreen log = new EventLogScreen();

        private FireReactionSnapshot frameSnapshot;

        /// <summary>Why the display could not be built, or null when all is well.</summary>
        private string startupError;

        private SimulationId? hoveredDoor;
        private bool showStats;
        private int eventsSeen;

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<FireReactionRunner>();
            }

            root = new GameObject("Fire reaction presentation").transform;
            try
            {
                // The runner starts first, so its simulation and the data it runs on exist.
                FireReactionScenarioData scenario = runner.Simulation.Scenario;
                materials = new PresentationMaterials();
                prototypeCamera = CreateCameraAndLight(root);
                Bounds floorPlan = FloorPlanBounds(scenario);
                cameraRig = new CameraRig(prototypeCamera, floorPlan);
                shell = new BuildingShellView(floorPlan, root);
                screens = new RoundScreens(runner);
                effects = new ParticleEffects(particleEffects, root);
                room = new RoomView(scenario, materials, effects, root);
                agents = new AgentViews(scenario, materials, effects, root);
                boxes = new BoxViews(scenario, materials, effects, root);
                fire = new FireView(materials, effects, root);
                ripples = new SoundRipples(materials.Icon, root);
                spray = new SprayView(effects);
                pops = new PopBursts(materials, effects, root);
                input = new PlayerInput(runner, room);

                // Off until G is pressed: the floor painted square by square
                // wherever somebody could stand.
                navigationGrid = new NavigationGridView(
                    runner.Simulation, root, scenario.World.OccupancyRadiusMillimetres);
            }
            catch (System.Exception failure)
            {
                // A scenario the run will not accept, or anything else that
                // goes wrong while building the display. Without this the scene
                // ends up with no camera at all and Unity shows an empty window
                // saying nothing useful, with the real reason buried in the
                // console. Put a camera and the reason on the screen instead.
                startupError = failure.Message;
                Debug.LogException(failure, this);
                MakeSureThereIsACamera();
            }
        }

        /// <summary>
        /// A bare camera looking at the floor, for when the display could not be
        /// built. It exists only so the window shows the message below rather
        /// than Unity's "no cameras rendering".
        /// </summary>
        private void MakeSureThereIsACamera()
        {
            if (prototypeCamera != null)
            {
                return;
            }

            prototypeCamera = Camera.main;
            if (prototypeCamera == null)
            {
                var cameraObject = new GameObject("Fire Reaction Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root, false);
                prototypeCamera = cameraObject.GetComponent<Camera>();
            }

            prototypeCamera.clearFlags = CameraClearFlags.SolidColor;
            prototypeCamera.backgroundColor = new Color(0.10f, 0.11f, 0.14f);
        }

        private void OnDestroy()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            effects?.Dispose();
            shell?.Destroy();
            materials?.Destroy();
        }

        private void Update()
        {
            if (startupError != null)
            {
                return;
            }

            frameSnapshot = runner == null ? null : runner.Snapshot;
            if (frameSnapshot == null)
            {
                return;
            }

            float time = Time.time;

            // Blend between the last two simulation ticks so movement is
            // smooth at any frame rate. Standing still -- paused, waiting to
            // start, or finished -- means no blending at all, or people would
            // creep forward in a scene that is meant to be frozen.
            float blend = runner.IsTicking
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime)
                : 1f;
            FireReactionSnapshot previous = runner.PreviousSnapshot;

            // Pause to look, not to act: while a card is up or the world is
            // stopped, the pointer still hovers but no click reaches the run.
            input.Update(prototypeCamera, frameSnapshot,
                runner.IsPaused || screens.CardIsUp || log.IsOpen);
            hoveredDoor = input.HoveredDoor;
            Keyboard keyboard = Keyboard.current;

            // Escape closes the log. Taken before anything else reads the key,
            // so backing out of the story never also cancels something else.
            if (log.IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                log.Close();
            }

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                showStats = !showStats;
            }

            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                navigationGrid?.Toggle();
            }

            // Space pauses, but only once the round is actually going: there
            // is nothing to pause behind the start card or after the end one.
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !screens.CardIsUp)
            {
                runner.TogglePause();
            }

            effects.BeginFrame();
            PlayNewEvents(frameSnapshot, time);
            agents.Update(frameSnapshot, previous, blend, time, prototypeCamera.transform);
            room.Update(frameSnapshot, hoveredDoor, time, Time.deltaTime);
            room.UpdateAlarms(frameSnapshot, time);
            room.UpdateHoles(frameSnapshot);
            boxes.Update(frameSnapshot, previous, blend, time);
            ripples.Update(time);
            fire.Update(frameSnapshot, time);
            spray.Update(frameSnapshot);
            pops.Update(time);

            // The player's own camera, with a bang's shake added on top of
            // wherever they have put it.
            cameraRig.Update(pops.Shake);
        }

        /// <summary>Every room together, in metres: what the camera frames and the shell wraps.</summary>
        private static Bounds FloorPlanBounds(FireReactionScenarioData scenario)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (FireReactionRoomDefinition room in scenario.Rooms)
            {
                minX = Mathf.Min(minX, Metres(room.Bounds.MinX));
                maxX = Mathf.Max(maxX, Metres(room.Bounds.MaxX));
                minZ = Mathf.Min(minZ, Metres(room.Bounds.MinZ));
                maxZ = Mathf.Max(maxZ, Metres(room.Bounds.MaxZ));
            }

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, maxZ));
            return bounds;
        }

        private void OnGUI()
        {
            if (startupError != null)
            {
                var message = new System.Text.StringBuilder();
                message.AppendLine("The fire-reaction run could not start, so there is nothing to show.");
                message.AppendLine();
                message.AppendLine(startupError);
                message.AppendLine();
                message.AppendLine("The saved scenario asset is probably out of step with the code.");
                message.AppendLine("Fix it with the menu command:");
                message.AppendLine("    Paniq > Rewrite Scenario Asset From Code Defaults");
                message.AppendLine();
                message.Append("The full details are in the Console.");
                GUI.Label(new Rect(20f, 20f, Screen.width - 40f, 140f), message.ToString());
                return;
            }

            if (frameSnapshot != null)
            {
                PrototypeHud.Draw(frameSnapshot, runner.Simulation.Scenario, hoveredDoor,
                    hoveredDoor.HasValue ? room.StateOf(hoveredDoor.Value) : DoorState.Locked,
                    hoveredDoor.HasValue && IsJammed(frameSnapshot, hoveredDoor.Value));
                screens.DrawStrip(frameSnapshot);
                PrototypeHud.DrawCards(frameSnapshot, input.SelectedCard, input);
                if (runner.IsPaused)
                {
                    PrototypeHud.DrawPauseHelp(frameSnapshot);
                }

                if (showStats)
                {
                    string feel = runner.PhysicsFeelName ?? "the scenario's own";
                    string footer = $"Physics feel: {feel}.  Particles: {effects.LiveParticles} of {effects.Settings.LiveParticleBudget}.";
                    if (runner.IsLiveTuned)
                    {
                        footer += "  Tuned live: this run cannot be replayed.";
                    }

                    PrototypeHud.DrawStats(frameSnapshot, footer);
                }

                // Last, so a card sits over everything else.
                if (runner.IsWaitingToStart)
                {
                    screens.DrawStartCard(frameSnapshot);
                }
                else if (frameSnapshot.RoundIsOver)
                {
                    screens.DrawEndCard(frameSnapshot);
                }

                // The end card only asks; taking the request here is what
                // clears it, so the button opens the log once rather than
                // holding it open.
                if (screens.WantsTheLog)
                {
                    screens.WantsTheLog = false;
                    log.Open();
                }

                // Very last, so the story covers the end card behind it.
                log.Draw(frameSnapshot);
            }
        }

        /// <summary>Whether this door is wedged so hard that no click will move it.</summary>
        private static bool IsJammed(FireReactionSnapshot snapshot, SimulationId doorId)
        {
            foreach (FireReactionDoorSnapshot door in snapshot.Doors)
            {
                if (door.DoorId == doorId)
                {
                    return door.IsJammed;
                }
            }

            return false;
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
                    case FireReactionEventType.AgentShoved:
                        // The shover lunges; the person shoved gets the thud.
                        agents.Lunge(record.SourceId, time);
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.AlarmPulled:
                        agents.Lunge(record.SourceId, time);
                        break;
                    case FireReactionEventType.AlarmRang:
                        // One big ring from every bell, so the noise is visible.
                        ripples.Start(record.Position, record.Strength, SoundRipples.YellColor, time);
                        break;
                    case FireReactionEventType.PowerBeefcake:
                        agents.Notice(record.TargetId, time);
                        break;
                    case FireReactionEventType.PowerBlastedWall:
                        // A very big ring: the bang carries across the building.
                        // Under it, the biggest burst there is.
                        ripples.Start(record.Position, scenario.Blast.BangHearingRadiusMillimetres,
                            SoundRipples.ThudColor, time);
                        pops.Start(record.Position, 0.8f, scenario.Blast.ThrowRadiusMillimetres, record.EventId, time);
                        break;
                    case FireReactionEventType.ObjectExploded:
                        // A flash, sparks and smoke sized to the blast, and a big
                        // ring for the bang; the blast itself throws the thing.
                        pops.Start(record.Position, boxes.BurstHeightOf(record.SourceId), record.Strength,
                            record.EventId, time);
                        ripples.Start(record.Position, record.Strength * 6, SoundRipples.ThudColor, time);
                        break;
                    case FireReactionEventType.ObjectBroke:
                        // Splinters or shards where it smashed; a table that is
                        // not one of the loose things is a big wooden one.
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        if (boxes.TryDescribe(record.SourceId, out Vector3 middle, out float size, out bool shatters))
                        {
                            effects.Break(middle, size, shatters, record.EventId);
                        }
                        else
                        {
                            effects.Break(ToUnityPosition(record.Position) + Vector3.up * 0.6f, 1.2f, false,
                                record.EventId);
                        }

                        break;
                    case FireReactionEventType.AgentTripped:
                        ripples.Start(record.Position, record.Strength, SoundRipples.ThudColor, time);
                        effects.Knock(ToUnityPosition(record.Position), 0.6f, record.EventId);
                        break;
                    case FireReactionEventType.AgentKnockedDown:
                    case FireReactionEventType.AgentCrushed:
                        // Hitting the floor kicks up a little dust.
                        effects.Knock(ToUnityPosition(record.Position), 0.6f, record.EventId);
                        break;
                    case FireReactionEventType.BoxesCollided:
                        effects.Knock(ToUnityPosition(record.Position) + Vector3.up * 0.3f,
                            Mathf.Clamp01(record.Strength / 80f), record.EventId);
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
                        if (record.Strength >= scenario.Falls.BumpMinimumSpeed)
                        {
                            ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                            effects.Knock(ToUnityPosition(record.Position), 0.3f, record.EventId);
                        }

                        break;
                    case FireReactionEventType.BoxHitAgent:
                        ripples.Start(record.Position, thudReach, SoundRipples.ThudColor, time);
                        effects.Knock(ToUnityPosition(record.Position) + Vector3.up * 0.8f,
                            Mathf.Clamp01(record.Strength / 80f), record.EventId);
                        break;
                }
            }

            eventsSeen = snapshot.Events.Count;
        }

        /// <summary>
        /// The camera and the light. Where the camera goes and how it is
        /// framed belongs to <see cref="CameraRig"/>, which the player drives;
        /// this only makes sure the objects exist.
        /// </summary>
        private static Camera CreateCameraAndLight(Transform parent)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Fire Reaction Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(parent, false);
                camera = cameraObject.GetComponent<Camera>();
            }

            camera.orthographic = true;

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

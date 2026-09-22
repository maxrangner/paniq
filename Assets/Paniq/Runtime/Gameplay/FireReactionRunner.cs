using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Owns the one simulation FixedUpdate entry point for the prototype. The
    /// run is created the first time anything asks for it, so other
    /// components can read it from their own Awake whatever order Unity
    /// starts them in.
    /// <para>
    /// It also owns the clock the player controls: the run is held still
    /// behind the start card, runs while the round is going, freezes when
    /// paused, and stops for good when the round is over.
    /// </para>
    /// </summary>
    public sealed class FireReactionRunner : MonoBehaviour
    {
        [Tooltip("The level being played: which building, how a round in it begins, and what clears it.")]
        [SerializeField] private LevelDefinition level;

        [Tooltip("Ignored when a level is assigned above. The building on its own, for a scene that has no level yet.")]
        [SerializeField] private FireReactionScenario scenario;

        [Tooltip("Play with this physics feel instead of the scenario's own. Leave empty for the scenario's values.")]
        [SerializeField] private PhysicsFeelPreset physicsFeel;

        [Tooltip("Editor only: copy the preset's dials into the run every tick, so changes show while playing. " +
                 "Floor grip, top speed and wall and table height only change on the next Play. A run tuned live cannot be replayed.")]
        [SerializeField] private bool livePhysicsTuning;

        private bool warnedAboutFeel;

        private FireReactionSimulation simulation;
        private FireReactionSnapshot current;
        private FireReactionSnapshot previous;

        /// <summary>The run. Read its <see cref="FireReactionSimulation.Scenario"/> for the values it actually uses.</summary>
        public FireReactionSimulation Simulation
        {
            get
            {
                if (simulation == null)
                {
                    FireReactionScenarioData data;
                    if (level != null)
                    {
                        data = level.ToRuntimeData();
                    }
                    else
                    {
                        // No level assigned: the building on its own, played by
                        // the same rules a level would impose, so the scene is
                        // playable without any setup step.
                        if (scenario == null)
                        {
                            scenario = FireReactionScenario.CreateDefault();
                        }

                        data = scenario.ToRuntimeData();
                        data.Round.HazardWaitsForTrigger = true;
                    }

                    if (EffectiveFeel() != null && FeelIsUsable())
                    {
                        data.PhysicsFeel = EffectiveFeel().Feel.Clone();
                    }

                    // The seed is settled here, before tick zero, and recorded
                    // so the player can ask for the same one again.
                    simulation = new FireReactionSimulation(data, LevelSession.TakeSeedFor(level));
                    Seed = LevelSession.CurrentSeed;

                    // "Play again" means the player has already chosen; only a
                    // fresh arrival waits behind the start card.
                    IsWaitingToStart = !LevelSession.StartImmediately;
                    LevelSession.ClearRequestedSeed();
                }

                return simulation;
            }
        }

        /// <summary>State after the latest tick, built at most once per tick.</summary>
        public FireReactionSnapshot Snapshot => current ??= Simulation.GetSnapshot();

        /// <summary>State one tick earlier, so presentation can blend smoothly between ticks.</summary>
        public FireReactionSnapshot PreviousSnapshot => previous ?? Snapshot;

        /// <summary>
        /// True once live tuning has been on for this run: its physics no
        /// longer follow from its scenario and seed alone, so it cannot be replayed.
        /// </summary>
        public bool IsLiveTuned { get; private set; }

        /// <summary>The name of the physics feel preset in use, or null for the scenario's own values.</summary>
        public string PhysicsFeelName => EffectiveFeel() != null ? EffectiveFeel().name : null;

        /// <summary>The level being played. Never null once the run exists.</summary>
        public LevelDefinition Level => level;

        /// <summary>The seed this run was built from.</summary>
        public ulong Seed { get; private set; }

        /// <summary>
        /// Held still behind the start card: the run exists and can be looked
        /// at, but no tick has happened yet.
        /// </summary>
        public bool IsWaitingToStart { get; private set; }

        /// <summary>
        /// Frozen so the scene can be read. Time stops for everything --
        /// people, fire, smoke and sparks -- while the camera keeps answering
        /// the player, and nothing they do reaches the run.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>Whether the run is actually advancing right now.</summary>
        public bool IsTicking => !IsWaitingToStart && !IsPaused && Simulation.Phase != RoundPhase.Over;

        /// <summary>The player pressing Play on the start card.</summary>
        public void BeginPlaying()
        {
            IsWaitingToStart = false;
        }

        /// <summary>
        /// Pause to look, not to act. A round that is already over cannot be
        /// paused; it is frozen already.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (Simulation.Phase == RoundPhase.Over)
            {
                paused = false;
            }

            IsPaused = paused;
            ApplyTimeScale();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>
        /// Freezing everything, not just the simulation. The run steps its own
        /// physics world by hand, so stopping Unity's clock does not change
        /// the size of a tick -- it just stops ticks happening at all, and
        /// stops the particle effects with them.
        /// </summary>
        private void ApplyTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        /// <summary>The level's physics feel, or the one set directly on this component.</summary>
        private PhysicsFeelPreset EffectiveFeel()
        {
            if (level != null && level.PhysicsFeel != null)
            {
                return level.PhysicsFeel;
            }

            return physicsFeel;
        }

        private void Awake()
        {
            _ = Simulation;

            // A previous run may have left the clock stopped, and a reloaded
            // scene inherits it.
            IsPaused = false;
            ApplyTimeScale();
        }

        private void FixedUpdate()
        {
            // Behind the start card, paused, or finished: in all three the
            // scene stands still and can be looked at, and no tick happens.
            if (!IsTicking)
            {
                return;
            }

            Advance();
        }

        private void OnDestroy()
        {
            // The clock is Unity's, not this object's, so a scene reload that
            // happened while paused must not leave the next one frozen.
            Time.timeScale = 1f;

            // The run keeps a physics scene of its own; let it go with the runner.
            simulation?.Dispose();
            simulation = null;
        }

        /// <summary>
        /// A player click on a door, queued for the next tick that has not
        /// started. Locked becomes unlocked, unlocked becomes open.
        /// </summary>
        /// <summary>Copies the preset's live dials into the run, when live tuning is on in the editor.</summary>
        private void TuneLive()
        {
            PhysicsFeelPreset feel = EffectiveFeel();
            if (!Application.isEditor || !livePhysicsTuning || feel == null || !FeelIsUsable())
            {
                return;
            }

            Simulation.Scenario.PhysicsFeel.TakeLiveValuesFrom(feel.Feel);
            IsLiveTuned = true;
        }

        /// <summary>Whether the preset's values would be accepted; a bad one is ignored, with one warning.</summary>
        private bool FeelIsUsable()
        {
            PhysicsFeelPreset feel = EffectiveFeel();
            string error = "It has no values.";
            if (feel.Feel != null && feel.Feel.IsValid(out error))
            {
                warnedAboutFeel = false;
                return true;
            }

            if (!warnedAboutFeel)
            {
                warnedAboutFeel = true;
                Debug.LogWarning($"Paniq: the physics feel preset '{feel.name}' is not usable, so it is ignored. {error}",
                    feel);
            }

            return false;
        }

        public void QueueDoorClick(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ClickDoor, doorId, Simulation.Tick + 1);
        }

        /// <summary>
        /// The player setting the disaster going, queued for the next tick that
        /// has not started. Only the first one does anything.
        /// </summary>
        public void QueueTriggerEvent()
        {
            Simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), Simulation.Tick + 1);
        }

        /// <summary>
        /// A card played on somebody, queued for the next tick that has not
        /// started.
        /// </summary>
        public void QueueCard(PlayerCommandType card, SimulationId personId)
        {
            Simulation.QueueCommand(card, personId, Simulation.Tick + 1);
        }

        /// <summary>A card played on a place, in whole millimetres.</summary>
        public void QueueCard(PlayerCommandType card, LogicalPosition spot)
        {
            Simulation.QueueCommand(card, spot, Simulation.Tick + 1);
        }

        public void StepForTests()
        {
            Advance();
        }

        private void Advance()
        {
            TuneLive();
            previous = Snapshot;
            Simulation.Step();
            current = null;
        }
    }
}

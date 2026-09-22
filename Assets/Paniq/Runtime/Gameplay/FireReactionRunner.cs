using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Owns the one simulation FixedUpdate entry point for the prototype. The
    /// run is created the first time anything asks for it, so other
    /// components can read it from their own Awake whatever order Unity
    /// starts them in.
    /// </summary>
    public sealed class FireReactionRunner : MonoBehaviour
    {
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
                    if (scenario == null)
                    {
                        scenario = FireReactionScenario.CreateDefault();
                    }

                    FireReactionScenarioData data = scenario.ToRuntimeData();
                    if (physicsFeel != null && FeelIsUsable())
                    {
                        data.PhysicsFeel = physicsFeel.Feel.Clone();
                    }

                    simulation = new FireReactionSimulation(data);
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
        public string PhysicsFeelName => physicsFeel != null ? physicsFeel.name : null;

        private void Awake()
        {
            _ = Simulation;
        }

        private void FixedUpdate()
        {
            Advance();
        }

        private void OnDestroy()
        {
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
            if (!Application.isEditor || !livePhysicsTuning || physicsFeel == null || !FeelIsUsable())
            {
                return;
            }

            Simulation.Scenario.PhysicsFeel.TakeLiveValuesFrom(physicsFeel.Feel);
            IsLiveTuned = true;
        }

        /// <summary>Whether the preset's values would be accepted; a bad one is ignored, with one warning.</summary>
        private bool FeelIsUsable()
        {
            string error = "It has no values.";
            if (physicsFeel.Feel != null && physicsFeel.Feel.IsValid(out error))
            {
                warnedAboutFeel = false;
                return true;
            }

            if (!warnedAboutFeel)
            {
                warnedAboutFeel = true;
                Debug.LogWarning($"Paniq: the physics feel preset '{physicsFeel.name}' is not usable, so it is ignored. {error}",
                    physicsFeel);
            }

            return false;
        }

        public void QueueDoorClick(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ClickDoor, doorId, Simulation.Tick + 1);
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

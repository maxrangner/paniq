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

                    simulation = new FireReactionSimulation(scenario.ToRuntimeData());
                }

                return simulation;
            }
        }

        /// <summary>State after the latest tick, built at most once per tick.</summary>
        public FireReactionSnapshot Snapshot => current ??= Simulation.GetSnapshot();

        /// <summary>State one tick earlier, so presentation can blend smoothly between ticks.</summary>
        public FireReactionSnapshot PreviousSnapshot => previous ?? Snapshot;

        private void Awake()
        {
            _ = Simulation;
        }

        private void FixedUpdate()
        {
            Advance();
        }

        /// <summary>
        /// A player click on a door, queued for the next tick that has not
        /// started. Locked becomes unlocked, unlocked becomes open.
        /// </summary>
        public void QueueDoorClick(SimulationId doorId)
        {
            Simulation.QueueCommand(PlayerCommandType.ClickDoor, doorId, Simulation.Tick + 1);
        }

        public void StepForTests()
        {
            Advance();
        }

        private void Advance()
        {
            previous = Snapshot;
            Simulation.Step();
            current = null;
        }
    }
}

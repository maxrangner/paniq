using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>Owns the one simulation FixedUpdate entry point for the prototype.</summary>
    public sealed class FireReactionRunner : MonoBehaviour
    {
        [SerializeField] private FireReactionScenario scenario;

        private FireReactionSimulation simulation;
        private FireReactionSnapshot current;
        private FireReactionSnapshot previous;

        public FireReactionScenario Scenario => scenario;
        public FireReactionSimulation Simulation => simulation;

        /// <summary>State after the latest tick, built at most once per tick.</summary>
        public FireReactionSnapshot Snapshot
        {
            get
            {
                if (current == null && simulation != null)
                {
                    current = simulation.GetSnapshot();
                }

                return current;
            }
        }

        /// <summary>State one tick earlier, so presentation can blend smoothly between ticks.</summary>
        public FireReactionSnapshot PreviousSnapshot => previous ?? Snapshot;

        private void Awake()
        {
            if (scenario == null)
            {
                scenario = FireReactionScenario.CreateDefault();
            }

            simulation = new FireReactionSimulation(scenario);
        }

        private void FixedUpdate()
        {
            Advance();
        }

        /// <summary>
        /// A player click on a door, queued for the next tick that has not
        /// started. Locked becomes unlocked, unlocked becomes open.
        /// </summary>
        public void QueueDoorClick(StableAgentId doorId)
        {
            if (simulation == null)
            {
                return;
            }

            simulation.QueueCommand(PlayerCommandType.ClickDoor, doorId, simulation.Tick + 1);
        }

        public void StepForTests()
        {
            if (simulation == null)
            {
                Awake();
            }

            Advance();
        }

        private void Advance()
        {
            if (simulation == null)
            {
                return;
            }

            previous = Snapshot;
            simulation.Step();
            current = null;
        }
    }
}

using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;
using static Paniq.Presentation.PresentationUtility;

namespace Paniq.Presentation
{
    /// <summary>
    /// The jet from an extinguisher: foam thrown out in front of whoever is
    /// spraying, spreading as it flies, falling to the floor and settling
    /// there in a thin mist. Read-only presentation, driven entirely by who
    /// is in the spraying state this frame.
    /// </summary>
    internal sealed class SprayView
    {
        private readonly ParticleEffects effects;

        /// <summary>Each sprayer's leftover fraction of a particle, so the jet is even at any frame rate.</summary>
        private readonly Dictionary<SimulationId, float> carries = new Dictionary<SimulationId, float>();

        public SprayView(ParticleEffects effects)
        {
            this.effects = effects;
        }

        public void Update(FireReactionSnapshot snapshot)
        {
            foreach (FireReactionAgentSnapshot agent in snapshot.Agents)
            {
                if (agent.ActivityState != AgentActivityState.Spraying ||
                    agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                Vector3 nozzle = ToUnityPosition(agent.Position) + Vector3.up * 0.55f;
                Vector3 forward = Quaternion.Euler(0f, agent.HeadingDegrees, 0f) * Vector3.forward;
                carries.TryGetValue(agent.AgentId, out float carry);
                effects.Spray(nozzle + forward * 0.3f, forward, Time.deltaTime, ref carry);
                carries[agent.AgentId] = carry;
            }
        }
    }
}

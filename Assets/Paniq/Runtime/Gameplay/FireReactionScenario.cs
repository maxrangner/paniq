using System;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// Read-only authored input for the fire-reaction prototype: one block of
    /// <see cref="FireReactionScenarioData"/> edited in the Inspector. A run
    /// gets a copy through <see cref="ToRuntimeData"/> and never writes this
    /// asset. Every value, its unit and its default live in
    /// FireReactionScenarioData and ScenarioSettings.cs.
    /// </summary>
    [CreateAssetMenu(fileName = "FireReactionScenario", menuName = "Paniq/Fire Reaction Scenario")]
    public sealed class FireReactionScenario : ScriptableObject
    {
        [SerializeField] private FireReactionScenarioData scenario = new FireReactionScenarioData();

        /// <summary>A fresh copy for one run; changing it never changes this asset.</summary>
        public FireReactionScenarioData ToRuntimeData()
        {
            FireReactionScenarioData copy = scenario.Clone();
            if (copy.Agents != null)
            {
                Array.Sort(copy.Agents, (left, right) => left.AgentId.CompareTo(right.AgentId));
            }

            return copy;
        }

        public bool IsValid(out string error)
        {
            try
            {
                ToRuntimeData().Validate();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>An in-memory asset holding the code defaults, for tests and for a runner with no asset assigned.</summary>
        public static FireReactionScenario CreateDefault()
        {
            return CreateInstance<FireReactionScenario>();
        }
    }
}

using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// One playable level: which building to run, how a round in it begins,
    /// and what it takes to clear it. The scenario says what the building is
    /// and how everything in it behaves; this says how it is played.
    /// <para>
    /// There is one of these today and no way to choose between them, but a
    /// second level is a duplicate of this asset with a different scenario
    /// rather than new code, which is the point of keeping it separate.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Paniq/Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Tooltip("Never shown to the player. Used to keep this level's best score apart from another level's, so changing it forgets the old best.")]
        [SerializeField] private string levelId = "the-office";

        [Tooltip("The name on the start card.")]
        [SerializeField] private string displayName = "The Office";

        [Tooltip("The building, the people and every rule they follow.")]
        [SerializeField] private ScenarioAsset scenario;

        [Tooltip("How the physics feels. Leave empty for the scenario's own values.")]
        [SerializeField] private PhysicsFeelPreset physicsFeel;

        [Tooltip("On: the level opens calm and nothing happens until the player triggers the event. Off: the hazard starts itself on the scenario's own tick count.")]
        [SerializeField] private bool hazardWaitsForTrigger = true;

        [Range(0, 100)]
        [Tooltip("The share of the crowd that has to be saved to clear the level. Twenty people at 75 means fifteen.")]
        [SerializeField] private int targetSavedPercent = 75;

        [Tooltip("On: the player has a purse of influence that doors, alarms and cards cost. Off (the office since prototype 3): everything is free and no purse is shown.")]
        [SerializeField] private bool influenceEnabled = true;

        public string LevelId => string.IsNullOrEmpty(levelId) ? name : levelId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public PhysicsFeelPreset PhysicsFeel => physicsFeel;
        public int TargetSavedPercent => targetSavedPercent;

        /// <summary>The seed this level runs on when the player has not chosen one.</summary>
        public ulong DefaultSeed => ToRuntimeData().DefaultSeed;

        /// <summary>
        /// A fresh copy of the scenario for one run, with this level's own
        /// rules written over it. Nothing here writes to the assets.
        /// </summary>
        public ScenarioData ToRuntimeData()
        {
            // With no scenario assigned, the code defaults: the same building
            // the scenario asset holds a saved copy of.
            ScenarioData data = scenario != null
                ? scenario.ToRuntimeData()
                : new ScenarioData();
            data.Round.HazardWaitsForTrigger = hazardWaitsForTrigger;
            data.Round.TargetSavedPercent = targetSavedPercent;
            data.Influence.Enabled = influenceEnabled;
            return data;
        }

        /// <summary>An in-memory level holding the code defaults, for tests and for a runner with no asset assigned.</summary>
        public static LevelDefinition CreateDefault() => CreateInstance<LevelDefinition>();
    }
}

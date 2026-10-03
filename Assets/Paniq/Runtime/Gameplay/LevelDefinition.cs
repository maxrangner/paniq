using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// One playable level: which building to run, how a round in it begins,
    /// and what it takes to clear it. The scenario says what the building is
    /// and how everything in it behaves; this says how it is played.
    /// <para>
    /// The office is one of these, and since 2026-10-01 so are the three
    /// test levels (<c>Assets/Paniq/Content/Levels</c>): the start card
    /// offers every level the runner's catalogue lists. A new level is a
    /// new asset -- a different scenario, or a building drawn by code --
    /// rather than new code, which is the point of keeping it separate.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Paniq/Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Tooltip("Never shown to the player. Used to keep this level's best score apart from another level's, so changing it forgets the old best.")]
        [SerializeField] private string levelId = "prototype_fire_1_fl_small";

        [Tooltip("The name on the start card.")]
        [SerializeField] private string displayName = "Prototype fire 1 (one floor, small)";

        [Tooltip("The building, the people and every rule they follow.")]
        [SerializeField] private ScenarioAsset scenario;

        [Tooltip("How the physics feels. Leave empty for the scenario's own values.")]
        [SerializeField] private PhysicsFeelPreset physicsFeel;

        [Tooltip("On: the level opens calm and nothing happens until the player triggers the event. Off: the hazard starts itself on the scenario's own tick count.")]
        [SerializeField] private bool hazardWaitsForTrigger = true;

        [Range(0, 100)]
        [Tooltip("The share of the crowd that has to be saved to clear the level. Twenty people at 75 means fifteen.")]
        [SerializeField] private int targetSavedPercent = 75;

        [Tooltip("On (the office since prototype 3's second batch): the Director climbs a ladder of small incidents -- a bin catches fire after a while of ordinary day; doused before the carpet under it caught, another bin catches; put out for good, that is the end of it (since 2026-10-02 a socket or the fuse box goes only as the cap's push). Off: the fire starts where and when the scenario says, all at once.")]
        [SerializeField] private bool directorClimbsTheLadder;

        [Tooltip("On (the office since 2026-09-28): the Director also caps the round. It draws how many the building lets out this time (a tenth to two fifths of the crowd) and, when more than that are on course for the way out, pops the socket where they are, then the fuse box, or lights another bin; when the round is already a massacre it adds nothing more. Needs the ladder. It reads only how the round is going, never what the player does.")]
        [SerializeField] private bool directorCapsTheRound;

        [Tooltip("On: the player can pull a fire alarm by clicking it. Off (the office since prototype 3's second batch): only the people in the building pull alarms.")]
        [SerializeField] private bool playerPullsAlarms = true;

        [Tooltip("A building drawn by code instead of the scenario's own (2026-10-01): the square room, the maze or the interaction room. " +
                 "The scenario's tuning numbers still apply; only the building, the people and the things in it are replaced. None plays the scenario's own building.")]
        [SerializeField] private BuiltInBuilding builtInBuilding = BuiltInBuilding.None;

        [Tooltip("On (the test levels): a Crowd button on screen sets the whole crowd panicking or calms it down again. Off (the office): the crowd is only ever frightened by what happens to it.")]
        [SerializeField] private bool offersCrowdSwitch;

        [Tooltip("On (the office): the red Trigger event button starts the level's hazard. Off (the square room and the maze, which have none): the button is not shown, and the round is not judged against the same seed left alone.")]
        [SerializeField] private bool triggerStartsAHazard = true;

        [Tooltip("A short name for the level row on the start card, which has room for about twelve letters a button. Empty: the display name.")]
        [SerializeField] private string shortName = "";

        [Tooltip("Two or three lines on the start card under the level's name: what happens here and what the player's hand can do about it. Empty: none.")]
        [TextArea(2, 4)]
        [SerializeField] private string brief = "";

        [Tooltip("The line under PLAY saying how the round starts. Empty: the start card's own wording.")]
        [SerializeField] private string howItStarts = "";

        public string LevelId => string.IsNullOrEmpty(levelId) ? name : levelId;

        /// <summary>The level row's label (2026-10-03): the short name, or the display name.</summary>
        public string ShortName => string.IsNullOrEmpty(shortName) ? DisplayName : shortName;

        /// <summary>What the start card says about the level under its name; empty for nothing.</summary>
        public string Brief => brief ?? "";

        /// <summary>The start card's line on how the round starts; empty for its own wording.</summary>
        public string HowItStarts => howItStarts ?? "";
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public PhysicsFeelPreset PhysicsFeel => physicsFeel;
        public int TargetSavedPercent => targetSavedPercent;

        /// <summary>Which building the level plays: the scenario's own, or one drawn by code (2026-10-01).</summary>
        public BuiltInBuilding BuiltInBuilding => builtInBuilding;

        /// <summary>
        /// Whether the Crowd switch is on screen on this level (2026-10-01).
        /// A level with the switch plays no hands-off copy of the round for
        /// the end card either: the switch is never copied into it, so a
        /// crowd the player panicked would be judged against a calm one.
        /// </summary>
        public bool OffersCrowdSwitch => offersCrowdSwitch;

        /// <summary>
        /// Whether the red Trigger event button does anything here. Off, the
        /// button is not drawn and no hands-off copy of the round is played
        /// for the end card: with nothing to set off, "left alone" is the
        /// same round (2026-10-01).
        /// </summary>
        public bool TriggerStartsAHazard => triggerStartsAHazard;

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

            // A building drawn by code replaces the scenario's building
            // and keeps its tuning (2026-10-01); None leaves it as it is.
            data = TestBuildings.Apply(builtInBuilding, data);
            data.Round.HazardWaitsForTrigger = hazardWaitsForTrigger;
            data.Round.TargetSavedPercent = targetSavedPercent;
            data.Director.ClimbsTheLadder = directorClimbsTheLadder;
            data.Director.CapsTheRound = directorCapsTheRound;
            data.Alarm.PlayerMayPull = playerPullsAlarms;
            return data;
        }

        /// <summary>An in-memory level holding the code defaults, for tests and for a runner with no asset assigned.</summary>
        public static LevelDefinition CreateDefault() => CreateInstance<LevelDefinition>();
    }
}

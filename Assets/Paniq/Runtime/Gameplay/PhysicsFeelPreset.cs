using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Gameplay
{
    /// <summary>
    /// A named set of physics feel dials ("Cartoon", "Heavy") that can stand in
    /// for the scenario's own. Drop one into the Fire Reaction runner's
    /// Physics Feel slot to play the level with that feel. The values become
    /// part of the run's scenario data when it starts, so a run played with a
    /// preset replays exactly like any other.
    /// </summary>
    [CreateAssetMenu(fileName = "PhysicsFeel", menuName = "Paniq/Physics Feel Preset")]
    public sealed class PhysicsFeelPreset : ScriptableObject
    {
        [Tooltip("How things fall, fly, grip and crush. 100 percent is the plain physical answer.")]
        public PhysicsFeelSettings Feel = new PhysicsFeelSettings();
    }
}

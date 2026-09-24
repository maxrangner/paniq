using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Authoring
{
    /// <summary>
    /// One entry on the building's timetable, placed in the scene: a cue the
    /// Director calls at a moment of the day. Put it inside the room it
    /// happens in (the meeting that ends), or anywhere at all for one that
    /// happens to the whole building (home time). This is the first, smallest
    /// event editor: a scene with none of these keeps the timetable the level
    /// already has.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Paniq/Cue")]
    public sealed class PaniqCue : MonoBehaviour
    {
        [Tooltip("What happens.")]
        public CueKind Kind = CueKind.MeetingEnds;

        [Tooltip("How many seconds into the run it happens.")]
        public float AtSeconds = 60f;

        [Tooltip("How many seconds apart, at most, the people it reaches take it up: a meeting breaks up " +
                 "one person at a time over this long, never all at once.")]
        public float SpreadSeconds = 8f;

        [Tooltip("The room it happens in, for a cue that happens in one (the meeting that ends). " +
                 "Leave it empty for one that happens to the whole building (home time).")]
        public PaniqRoom Room;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f);
            var at = new Vector3(transform.position.x, 1.8f, transform.position.z);
            Gizmos.DrawWireSphere(at, 0.2f);
            Gizmos.DrawLine(at, at + Vector3.up * 0.4f);
        }
    }
}

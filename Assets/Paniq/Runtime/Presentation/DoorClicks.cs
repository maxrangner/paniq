using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// One click works a door; a double click turns its key (the owner's
    /// rule, 2026-09-25). A single click is held back for the double-click
    /// window before it is sent, because acting on the first click at once
    /// would have opened the door, and charged for it, before anybody knew a
    /// second click was coming. The window is about fifteen ticks, less than
    /// the delay people already take to react to anything, so a held click
    /// is never seen to lag.
    /// <para>
    /// Plain arithmetic on ids and seconds, with no Unity in it, so it can be
    /// checked without a scene.
    /// </para>
    /// </summary>
    internal sealed class DoorClicks
    {
        /// <summary>How long after a click a second one on the same door counts as a double click.</summary>
        public const float WindowSeconds = 0.3f;

        private SimulationId? pending;
        private float pendingSince;

        /// <summary>The door with a single click waiting to be sent, if any.</summary>
        public SimulationId? Pending => pending;

        /// <summary>
        /// A click on a door. True when this was the second click on the same
        /// door inside the window: the key is to be turned and no single click
        /// is sent. Otherwise the click waits, and a click that was waiting on
        /// some other door is handed back in <paramref name="settled"/> to be
        /// sent as the single click it was.
        /// </summary>
        public bool Press(SimulationId door, float now, out SimulationId? settled)
        {
            settled = null;
            if (pending.HasValue && pending.Value == door && now - pendingSince <= WindowSeconds)
            {
                pending = null;
                return true;
            }

            settled = pending;
            pending = door;
            pendingSince = now;
            return false;
        }

        /// <summary>The single click whose window has closed with no second click, if any: it is to be sent now.</summary>
        public SimulationId? Settle(float now)
        {
            if (!pending.HasValue || now - pendingSince <= WindowSeconds)
            {
                return null;
            }

            SimulationId due = pending.Value;
            pending = null;
            return due;
        }

        /// <summary>Pausing, a card going up, or the round ending: whatever was waiting is forgotten.</summary>
        public void Clear()
        {
            pending = null;
        }
    }
}

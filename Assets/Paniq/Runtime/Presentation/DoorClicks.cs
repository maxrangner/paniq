using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// One click works a door; a double click turns its key (the owner's
    /// rule, 2026-09-25); and a button held down on a door for longer than
    /// the double-click window is a hand on it, holding it shut until the
    /// button comes back up (prototype 3, 2026-09-25). A single click is
    /// held back for the double-click window before it is sent, because
    /// acting on the first click at once would have opened the door, and
    /// charged for it, before anybody knew a second click was coming. The
    /// window is about fifteen ticks, less than the delay people already
    /// take to react to anything, so a held click is never seen to lag.
    /// <para>
    /// A hold starts the moment the window closes with the button still
    /// down, and the click that began it is not a click any more: taking
    /// hold of a door and opening it are different things. The one window
    /// serves both, so there is only one number for the player to learn.
    /// </para>
    /// <para>
    /// Plain arithmetic on ids and seconds, with no Unity in it, so it can be
    /// checked without a scene.
    /// </para>
    /// </summary>
    internal sealed class DoorClicks
    {
        /// <summary>How long after a click a second one on the same door counts as a double click, and how long a press must last to be a hold.</summary>
        public const float WindowSeconds = 0.3f;

        private SimulationId? pending;
        private float pendingSince;
        private SimulationId? pressed;
        private float pressedAt;
        private SimulationId? held;

        /// <summary>The door with a single click waiting to be sent, if any.</summary>
        public SimulationId? Pending => pending;

        /// <summary>The door the player is holding shut, if any.</summary>
        public SimulationId? Held => held;

        /// <summary>
        /// A click on a door. True when this was the second click on the same
        /// door inside the window: the key is to be turned and no single click
        /// is sent. Otherwise the click waits, and a click that was waiting on
        /// some other door is handed back in <paramref name="settled"/> to be
        /// sent as the single click it was, and the press is remembered,
        /// because if the button stays down it becomes a hold. The second half
        /// of a double click is not remembered: turning the key and then
        /// keeping the button down a moment too long is still only turning
        /// the key, never a hand on the door as well.
        /// </summary>
        public bool Press(SimulationId door, float now, out SimulationId? settled)
        {
            settled = null;
            if (pending.HasValue && pending.Value == door && now - pendingSince <= WindowSeconds)
            {
                pending = null;
                pressed = null;
                return true;
            }

            pressed = door;
            pressedAt = now;
            settled = pending;
            pending = door;
            pendingSince = now;
            return false;
        }

        /// <summary>
        /// The door to take hold of, if the button has stayed down on one for
        /// the whole window: the press was a hold, not a click, so the single
        /// click it would have been is forgotten. Ask this before
        /// <see cref="Settle"/>, because the two turn on the same instant.
        /// </summary>
        public SimulationId? Hold(float now, bool buttonDown)
        {
            if (!buttonDown)
            {
                pressed = null;
                return null;
            }

            if (!pressed.HasValue || held.HasValue || now - pressedAt <= WindowSeconds)
            {
                return null;
            }

            held = pressed;
            pressed = null;
            if (pending.HasValue && pending.Value == held.Value)
            {
                pending = null;
            }

            return held;
        }

        /// <summary>The door to let go of, if one is held and the button has come back up.</summary>
        public SimulationId? Release(bool buttonDown)
        {
            if (buttonDown || !held.HasValue)
            {
                return null;
            }

            SimulationId letGo = held.Value;
            held = null;
            return letGo;
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

        /// <summary>
        /// Pausing, a card going up, or the round ending: whatever click was
        /// waiting is forgotten, and a held door is handed back to be let go
        /// of, because nothing pressed while the world is stopped reaches it.
        /// </summary>
        public SimulationId? Clear()
        {
            pending = null;
            pressed = null;
            SimulationId? letGo = held;
            held = null;
            return letGo;
        }
    }
}

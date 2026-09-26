using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// What the left button does on a door (2026-09-26): a click -- pressed
    /// and let go of quickly -- is one step of influence on it, drawing people
    /// to use it, so clicking frantically builds a strong pull; and the button
    /// held down on it for longer than the window is a hand on it, holding it
    /// shut until the button comes back up (prototype 3, 2026-09-25). The key
    /// has moved to the right button, so there is no double click any more to
    /// wait for, and a click is sent the moment the button comes back up.
    /// <para>
    /// A hold starts the moment the window closes with the button still down,
    /// and the press that began it is not a click any more: taking hold of a
    /// door and drawing people to it are different things. The window is
    /// about fifteen ticks, less than the delay people already take to react
    /// to anything, so a click is never seen to lag.
    /// </para>
    /// <para>
    /// Plain arithmetic on ids and seconds, with no Unity in it, so it can be
    /// checked without a scene.
    /// </para>
    /// </summary>
    internal sealed class DoorClicks
    {
        /// <summary>How long a press must last to be a hold rather than a click.</summary>
        public const float WindowSeconds = 0.3f;

        private SimulationId? pressed;
        private float pressedAt;
        private SimulationId? held;

        /// <summary>The door the player is holding shut, if any.</summary>
        public SimulationId? Held => held;

        /// <summary>The button has gone down on a door: a click or a hold, depending on how long it stays down.</summary>
        public void Press(SimulationId door, float now)
        {
            pressed = door;
            pressedAt = now;
        }

        /// <summary>
        /// The door to take hold of, if the button has stayed down on one for
        /// the whole window: the press was a hold, not a click. Ask this before
        /// <see cref="Clicked"/>, because the two turn on the same instant.
        /// </summary>
        public SimulationId? Hold(float now, bool buttonDown)
        {
            if (!buttonDown || !pressed.HasValue || held.HasValue || now - pressedAt <= WindowSeconds)
            {
                return null;
            }

            held = pressed;
            pressed = null;
            return held;
        }

        /// <summary>
        /// The door that was clicked, if the button has come back up inside
        /// the window: one step of influence, to be sent now. Once, however
        /// often it is asked.
        /// </summary>
        public SimulationId? Clicked(bool buttonDown)
        {
            if (buttonDown || !pressed.HasValue)
            {
                return null;
            }

            SimulationId door = pressed.Value;
            pressed = null;
            return door;
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

        /// <summary>
        /// Pausing, a card going up, or the round ending: whatever press was
        /// under way is forgotten, and a held door is handed back to be let go
        /// of, because nothing pressed while the world is stopped reaches it.
        /// </summary>
        public SimulationId? Clear()
        {
            pressed = null;
            SimulationId? letGo = held;
            held = null;
            return letGo;
        }
    }
}

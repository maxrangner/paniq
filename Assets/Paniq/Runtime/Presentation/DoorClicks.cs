using Paniq.Simulation;

namespace Paniq.Presentation
{
    /// <summary>
    /// A click told from a hold, on something with an ID. Used twice since
    /// 2026-09-29: for the right button on a door (a click turns the key, a
    /// hold is a hand holding it shut, prototype 3's rule moved from the left
    /// button) and for the left button on a person (a click is a poke, a
    /// hold is a tug on their shirt). A click is sent the moment the button
    /// comes back up inside the window; a hold starts the moment the window
    /// closes with the button still down.
    /// <para>
    /// The press that began a hold is not a click any more: taking hold of
    /// somebody and poking them are different things. The window is about
    /// fifteen ticks, less than the delay people already take to react to
    /// anything, so a click is never seen to lag.
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

        /// <summary>What the player has hold of, if anything.</summary>
        public SimulationId? Held => held;

        /// <summary>The button has gone down on something: a click or a hold, depending on how long it stays down.</summary>
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
        /// What was clicked, if the button has come back up inside the
        /// window: the key, or a poke, to be sent now. Once, however often it
        /// is asked.
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
        /// under way is forgotten, and whatever is held is handed back to be
        /// let go of, because nothing pressed while the world is stopped
        /// reaches it.
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

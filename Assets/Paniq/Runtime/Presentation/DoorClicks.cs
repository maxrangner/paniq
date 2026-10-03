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

    /// <summary>
    /// The hand on a place (2026-09-30): which button put it there, and
    /// whether, when that button comes up, it was a hold -- the hand comes
    /// off -- or a click, which leaves a beacon behind for a few seconds (the
    /// owner: "a single click should place an influence beacon for 3
    /// seconds"). The left button pulls, the right pushes; one hand, so a
    /// press of either replaces what the other had. Plain arithmetic, so it
    /// can be checked without a scene.
    /// </summary>
    internal sealed class PlaceHold
    {
        /// <summary>What the button coming up means.</summary>
        public enum Ending
        {
            /// <summary>Nothing held, or its button is still down.</summary>
            None,

            /// <summary>It was a hold: the hand comes off now.</summary>
            Release,

            /// <summary>It was a click: the place stays a moment, then comes off by itself.</summary>
            Leave
        }

        /// <summary>0 when the hand is on no place, 1 for the left button, 2 for the right.</summary>
        private int button;
        private float pressedAt;

        public bool IsOn => button != 0;

        /// <summary>The right button's hand: it pushes people away.</summary>
        public bool Repels => button == 2;

        /// <summary>The hand went on a place, with this button (1 left, 2 right).</summary>
        public void Press(int whichButton, float now)
        {
            button = whichButton;
            pressedAt = now;
        }

        /// <summary>What to send, if the button that has the place has come up.</summary>
        public Ending ComingUp(bool leftDown, bool rightDown, float now)
        {
            if (button == 0 || (button == 1 && leftDown) || (button == 2 && rightDown))
            {
                return Ending.None;
            }

            button = 0;
            return now - pressedAt <= DoorClicks.WindowSeconds ? Ending.Leave : Ending.Release;
        }

        /// <summary>Everything off at once (pause, a card over the screen): true when a place was held, to be let go of.</summary>
        public bool Clear()
        {
            bool was = button != 0;
            button = 0;
            return was;
        }
    }

    /// <summary>
    /// The hand dragged (2026-09-30, the owner: "when left click is held, if
    /// then dragged the influence point should move with the pointer. So
    /// agents can be guided with this. Same with right click hold"). While a
    /// button holds a place, the floor under the pointer is where the hand
    /// goes -- but a hand pressed on a door or a thing stays on it until the
    /// pointer has clearly left it (<see cref="DetachMillimetres"/>), so a
    /// shaky hold on a door is still a hold on the door; and a move is sent
    /// only once the pointer has gone a little way (<see cref="StepMillimetres"/>)
    /// and not more than ten times a second, so the run is not flooded with
    /// every twitch. Plain arithmetic, so it can be checked without a scene.
    /// </summary>
    internal sealed class HandDrag
    {
        /// <summary>How far the pointer must go from a door or a thing before the hand leaves it for the floor.</summary>
        public const long DetachMillimetres = 800;

        /// <summary>How far the pointer must go from the last spot sent before the next is.</summary>
        public const long StepMillimetres = 250;

        /// <summary>The least time between two moves sent.</summary>
        public const float EverySeconds = 0.1f;

        private bool onAPlace;
        private bool detached;
        private LogicalPosition pressedAt;
        private LogicalPosition lastSent;
        private float lastSentAt;

        /// <summary>
        /// The hand went on a place at this spot on the floor: on the floor
        /// itself (<paramref name="onTheFloor"/>), when it follows the pointer
        /// at once, or on a door or a thing, when it waits to be pulled off.
        /// </summary>
        public void Press(LogicalPosition floor, bool onTheFloor, float now)
        {
            onAPlace = true;
            detached = onTheFloor;
            pressedAt = floor;
            lastSent = floor;
            lastSentAt = now;
        }

        /// <summary>The hand is off: nothing to drag.</summary>
        public void Clear() => onAPlace = false;

        /// <summary>
        /// The pointer is over this spot of floor with the button still down:
        /// true, with the spot to send, when the hand should move there.
        /// </summary>
        public bool Moved(LogicalPosition floor, float now, out LogicalPosition send)
        {
            send = default;
            if (!onAPlace)
            {
                return false;
            }

            if (!detached)
            {
                if (IntegerMath.Distance(floor, pressedAt) < DetachMillimetres)
                {
                    return false;
                }

                detached = true;
            }
            else if (IntegerMath.Distance(floor, lastSent) < StepMillimetres || now - lastSentAt < EverySeconds)
            {
                return false;
            }

            lastSent = floor;
            lastSentAt = now;
            send = floor;
            return true;
        }
    }
}

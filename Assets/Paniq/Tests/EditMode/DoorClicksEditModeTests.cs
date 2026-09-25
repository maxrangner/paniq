using NUnit.Framework;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// One click works a door, a double click turns its key (the owner's
    /// rule, 2026-09-25), and a click on a card or a button never reaches the
    /// world behind it. The decisions are plain arithmetic, checked here
    /// without a scene; the clicks themselves are read in <c>PlayerInput</c>.
    /// </summary>
    public sealed class DoorClicksEditModeTests
    {
        private static readonly SimulationId A = new SimulationId(2002UL);
        private static readonly SimulationId B = new SimulationId(2005UL);

        [Test]
        public void ASingleClick_IsSentOnceTheWindowHasClosed()
        {
            var clicks = new DoorClicks();
            Assert.That(clicks.Press(A, 0f, out SimulationId? settled), Is.False, "The first click waits.");
            Assert.That(settled, Is.Null);
            Assert.That(clicks.Settle(DoorClicks.WindowSeconds * 0.5f), Is.Null, "Still inside the window.");
            Assert.That(clicks.Settle(DoorClicks.WindowSeconds + 0.01f), Is.EqualTo(A), "Sent once the window has closed.");
            Assert.That(clicks.Settle(10f), Is.Null, "And only once.");
        }

        [Test]
        public void ASecondClickInsideTheWindow_TurnsTheKey_AndSendsNoSingleClick()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            Assert.That(clicks.Press(A, DoorClicks.WindowSeconds * 0.5f, out SimulationId? settled), Is.True, "A double click.");
            Assert.That(settled, Is.Null);
            Assert.That(clicks.Settle(10f), Is.Null, "The single click was never sent.");
            Assert.That(clicks.Pending, Is.Null);
        }

        [Test]
        public void AClickOnAnotherDoor_SendsTheFirstAsTheSingleClickItWas()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            Assert.That(clicks.Press(B, 0.1f, out SimulationId? settled), Is.False);
            Assert.That(settled, Is.EqualTo(A), "The first door's click goes as a single click.");
            Assert.That(clicks.Settle(0.1f + DoorClicks.WindowSeconds + 0.01f), Is.EqualTo(B));
        }

        [Test]
        public void AClickAfterTheWindow_IsAFreshSingleClick_NotADoubleClick()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            Assert.That(clicks.Settle(1f), Is.EqualTo(A));
            Assert.That(clicks.Press(A, 1.1f, out _), Is.False, "Too late to be the second half of a double click.");
        }

        // ------------------------------------------------------------ holding (prototype 3)

        /// <summary>
        /// The button kept down on a door past the window is a hand on it:
        /// the door is taken hold of, and the click that began it is not sent.
        /// </summary>
        [Test]
        public void AButtonHeldDownPastTheWindow_TakesHoldOfTheDoor_AndIsNotAClick()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            Assert.That(clicks.Hold(DoorClicks.WindowSeconds * 0.5f, true), Is.Null, "Still inside the window: it may yet be a click.");
            Assert.That(clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true), Is.EqualTo(A), "The window closed with the button still down.");
            Assert.That(clicks.Held, Is.EqualTo(A));
            Assert.That(clicks.Settle(DoorClicks.WindowSeconds + 0.01f), Is.Null, "A hold is not a click as well.");
            Assert.That(clicks.Hold(5f, true), Is.Null, "Taken hold of once, not every frame.");
        }

        [Test]
        public void LettingGoOfTheButton_LetsGoOfTheDoor()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true);
            Assert.That(clicks.Release(true), Is.Null, "Still holding.");
            Assert.That(clicks.Release(false), Is.EqualTo(A), "The button came up: the door is let go of.");
            Assert.That(clicks.Held, Is.Null);
            Assert.That(clicks.Release(false), Is.Null, "And only once.");
        }

        /// <summary>A quick click, the button up again inside the window, is a click and never a hold.</summary>
        [Test]
        public void AQuickClick_IsStillAClick_NotAHold()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            Assert.That(clicks.Hold(0.1f, false), Is.Null, "The button came up.");
            Assert.That(clicks.Hold(DoorClicks.WindowSeconds + 0.01f, false), Is.Null);
            Assert.That(clicks.Settle(DoorClicks.WindowSeconds + 0.01f), Is.EqualTo(A), "Sent as the single click it was.");
            Assert.That(clicks.Held, Is.Null);
        }

        /// <summary>Pausing hands a held door back so it can be let go of, because nothing pressed while the world is stopped reaches it.</summary>
        [Test]
        public void Clearing_HandsBackTheHeldDoor()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true);
            Assert.That(clicks.Clear(), Is.EqualTo(A));
            Assert.That(clicks.Held, Is.Null);
            Assert.That(clicks.Clear(), Is.Null);
        }

        [Test]
        public void Clearing_ForgetsWhateverWasWaiting()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f, out _);
            clicks.Clear();
            Assert.That(clicks.Settle(10f), Is.Null);
        }

        [Test]
        public void TheHud_CoversWhatItClaimed_UntilTheNextFrameIsDrawn()
        {
            HudHitTest.Clear();
            HudHitTest.BeginFrame();
            HudHitTest.Claim(new Rect(20f, 20f, 100f, 30f));
            Assert.That(HudHitTest.Covers(new Vector2(50f, 30f)), Is.False, "Nothing counts until the frame's drawing is done.");
            HudHitTest.EndFrame();
            Assert.That(HudHitTest.Covers(new Vector2(50f, 30f)), Is.True);
            Assert.That(HudHitTest.Covers(new Vector2(500f, 500f)), Is.False);

            HudHitTest.BeginFrame();
            Assert.That(HudHitTest.Covers(new Vector2(50f, 30f)), Is.True, "Last frame's claims hold while this frame draws.");
            HudHitTest.EndFrame();
            Assert.That(HudHitTest.Covers(new Vector2(50f, 30f)), Is.False, "A frame that drew nothing covers nothing.");
            HudHitTest.Clear();
        }
    }
}

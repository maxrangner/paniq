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

using NUnit.Framework;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// What the left button does on a door (2026-09-26): a quick click is one
    /// step of influence on it, sent the moment the button comes back up, so
    /// clicking frantically is so many steps; the button held down past the
    /// window is a hand holding it shut. The key is the right button's now, so
    /// there is no double click to wait for. And a click on a card or a button
    /// never reaches the world behind it. The decisions are plain arithmetic,
    /// checked here without a scene; the clicks themselves are read in
    /// <c>PlayerInput</c>.
    /// </summary>
    public sealed class DoorClicksEditModeTests
    {
        private static readonly SimulationId A = new SimulationId(2002UL);
        private static readonly SimulationId B = new SimulationId(2005UL);

        [Test]
        public void AQuickClick_IsSentTheMomentTheButtonComesBackUp_AndOnlyOnce()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            Assert.That(clicks.Clicked(true), Is.Null, "Still down: it may yet be a hold.");
            Assert.That(clicks.Hold(0.1f, true), Is.Null);
            Assert.That(clicks.Clicked(false), Is.EqualTo(A), "Up inside the window: one click.");
            Assert.That(clicks.Clicked(false), Is.Null, "And only once.");
            Assert.That(clicks.Held, Is.Null, "A click is never a hold.");
        }

        [Test]
        public void ThreeQuickClicks_AreThreeClicks()
        {
            var clicks = new DoorClicks();
            int sent = 0;
            for (int i = 0; i < 3; i++)
            {
                clicks.Press(A, i * 0.1f);
                sent += clicks.Clicked(false).HasValue ? 1 : 0;
            }

            Assert.That(sent, Is.EqualTo(3), "Clicking frantically is so many steps of influence, none of them a double click.");
        }

        [Test]
        public void AClickOnAnotherDoor_IsThatDoorsClick()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            Assert.That(clicks.Clicked(false), Is.EqualTo(A));
            clicks.Press(B, 0.1f);
            Assert.That(clicks.Clicked(false), Is.EqualTo(B));
        }

        // ------------------------------------------------------------ holding (prototype 3)

        /// <summary>
        /// The button kept down on a door past the window is a hand on it:
        /// the door is taken hold of, and the press that began it is not a
        /// click when the button comes back up.
        /// </summary>
        [Test]
        public void AButtonHeldDownPastTheWindow_TakesHoldOfTheDoor_AndIsNotAClick()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            Assert.That(clicks.Hold(DoorClicks.WindowSeconds * 0.5f, true), Is.Null, "Still inside the window: it may yet be a click.");
            Assert.That(clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true), Is.EqualTo(A), "The window closed with the button still down.");
            Assert.That(clicks.Held, Is.EqualTo(A));
            Assert.That(clicks.Hold(5f, true), Is.Null, "Taken hold of once, not every frame.");
            Assert.That(clicks.Clicked(false), Is.Null, "A hold is not a click as well.");
        }

        [Test]
        public void LettingGoOfTheButton_LetsGoOfTheDoor()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true);
            Assert.That(clicks.Release(true), Is.Null, "Still holding.");
            Assert.That(clicks.Release(false), Is.EqualTo(A), "The button came up: the door is let go of.");
            Assert.That(clicks.Held, Is.Null);
            Assert.That(clicks.Release(false), Is.Null, "And only once.");
        }

        /// <summary>Pausing hands a held door back so it can be let go of, because nothing pressed while the world is stopped reaches it.</summary>
        [Test]
        public void Clearing_HandsBackTheHeldDoor()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            clicks.Hold(DoorClicks.WindowSeconds + 0.01f, true);
            Assert.That(clicks.Clear(), Is.EqualTo(A));
            Assert.That(clicks.Held, Is.Null);
            Assert.That(clicks.Clear(), Is.Null);
        }

        [Test]
        public void Clearing_ForgetsAPressUnderWay()
        {
            var clicks = new DoorClicks();
            clicks.Press(A, 0f);
            clicks.Clear();
            Assert.That(clicks.Clicked(false), Is.Null);
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

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

        /// <summary>
        /// The hand on a place (2026-09-30): held past the window, the button
        /// coming up takes the hand off; a quick click leaves it there as a
        /// three-second beacon (the owner: "a single click should place an
        /// influence beacon for 3 seconds"). Only the button that put it
        /// there ends it: the right button's push is not ended by the left
        /// button coming up.
        /// </summary>
        [Test]
        public void TheHandOnAPlace_AClickLeavesABeacon_AHoldComesOff_AndOnlyItsOwnButtonEndsIt()
        {
            var hand = new PlaceHold();
            hand.Press(1, 0f);
            Assert.That(hand.IsOn && !hand.Repels, Is.True, "The left button pulls.");
            Assert.That(hand.ComingUp(true, false, 0.1f), Is.EqualTo(PlaceHold.Ending.None), "Still down.");
            Assert.That(hand.ComingUp(false, false, 0.2f), Is.EqualTo(PlaceHold.Ending.Leave), "Up inside the window: a beacon.");
            Assert.That(hand.IsOn, Is.False);
            Assert.That(hand.ComingUp(false, false, 0.3f), Is.EqualTo(PlaceHold.Ending.None), "And only once.");

            hand.Press(2, 1f);
            Assert.That(hand.Repels, Is.True, "The right button pushes.");
            Assert.That(hand.ComingUp(false, true, 2f), Is.EqualTo(PlaceHold.Ending.None), "The left button being up ends nothing of the right's.");
            Assert.That(hand.ComingUp(false, false, 2f), Is.EqualTo(PlaceHold.Ending.Release), "Held a second: the hand comes off.");

            hand.Press(1, 3f);
            Assert.That(hand.Clear(), Is.True, "Paused: let go of.");
            Assert.That(hand.Clear(), Is.False);
        }

        /// <summary>
        /// The owner (2026-09-30): "I couldn't get them to pick up keycard. Hit
        /// box too small." The card lies on a desk, and the pointer is now
        /// measured to where it is drawn -- up on the desk -- rather than to a
        /// spot a few centimetres off the floor beneath it.
        /// </summary>
        [Test]
        public void TheKeycardOnADesk_IsAimedAtWhereItIsDrawn_UpOnTheDesk()
        {
            var scenario = Paniq.Gameplay.ScenarioAsset.CreateDefault();
            try
            {
                ScenarioData data = scenario.ToRuntimeData();
                data.Keycard.Enabled = true;
                using (var simulation = new Run(data, 42UL))
                {
                    simulation.PutKeycardOnATableForTests(0);
                    simulation.Step();
                    RunSnapshot snapshot = simulation.GetSnapshot();
                    bool found = false;
                    for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
                    {
                        PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                        if (thing.Kind != PhysicsObjectKind.Keycard)
                        {
                            continue;
                        }

                        found = true;
                        Assert.That(PlayerInput.DrawnMiddle(thing).y, Is.GreaterThan(RoomView.TableHeight - 0.05f),
                            "Aimed at up on the desk, where it is drawn.");
                    }

                    Assert.That(found, Is.True, "The card is in the building.");
                }
            }
            finally
            {
                Object.DestroyImmediate(scenario);
            }
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

        // The hand dragged (2026-09-30, the owner: "when left click is held,
        // if then dragged the influence point should move with the pointer"):
        // from the floor it follows at once, off a door or a thing only once
        // the pointer has clearly left it, and never more than a step at a time.

        [Test]
        public void TheHandDragged_OnTheFloor_FollowsThePointer_AStepAtATime()
        {
            var drag = new HandDrag();
            drag.Press(new LogicalPosition(0, 0), true, 0f);
            Assert.That(drag.Moved(new LogicalPosition(100, 0), 1f, out _), Is.False, "A twitch is not a move.");
            Assert.That(drag.Moved(new LogicalPosition(400, 0), 1f, out LogicalPosition to), Is.True, "A step is.");
            Assert.That(to, Is.EqualTo(new LogicalPosition(400, 0)));
            Assert.That(drag.Moved(new LogicalPosition(900, 0), 1.05f, out _), Is.False, "Not more than ten times a second.");
            Assert.That(drag.Moved(new LogicalPosition(900, 0), 1.2f, out _), Is.True);
        }

        [Test]
        public void TheHandDragged_OnADoor_StaysOnIt_UntilThePointerHasClearlyLeftIt()
        {
            var drag = new HandDrag();
            drag.Press(new LogicalPosition(0, 0), false, 0f);
            Assert.That(drag.Moved(new LogicalPosition(600, 0), 1f, out _), Is.False, "A shaky hold on a door is a hold on the door.");
            Assert.That(drag.Moved(new LogicalPosition(900, 0), 1f, out _), Is.True, "Pulled well off it, the hand leaves it.");
            drag.Clear();
            Assert.That(drag.Moved(new LogicalPosition(5000, 0), 5f, out _), Is.False, "Let go of, nothing follows.");
        }
    }
}

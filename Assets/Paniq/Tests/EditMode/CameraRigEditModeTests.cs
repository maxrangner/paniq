using NUnit.Framework;
using Paniq.Presentation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The camera the player drives. Two things here are easy to break without
    /// anybody noticing until they are playing: the tilt has to stay flat for
    /// the first half of the wheel and only then swoop, and Q and E have to
    /// step round the eight tidy views (2026-09-29: an eighth of a turn each,
    /// corner, side, corner; it was a quarter, and the right-button drag that
    /// swung the view anywhere is gone).
    /// </summary>
    public sealed class CameraRigEditModeTests
    {
        private GameObject cameraObject;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Test camera", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        private CameraRig NewRig()
        {
            var floorPlan = new Bounds();
            floorPlan.SetMinMax(new Vector3(-6f, 0f, -6f), new Vector3(19f, 0f, 6f));
            return new CameraRig(cameraObject.GetComponent<Camera>(), floorPlan);
        }

        /// <summary>
        /// Zoomed out, and all the way through the first half of the wheel,
        /// the camera has not started tipping at all -- the view just comes
        /// straight in. Tilting from the first notch made a small zoom lurch.
        /// </summary>
        [Test]
        public void TheFirstHalfOfTheZoom_DoesNotTiltTheCameraAtAll()
        {
            Assert.That(CameraRig.SwoopAt(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(CameraRig.SwoopAt(0.25f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(CameraRig.SwoopAt(0.5f), Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>Past halfway it swoops the whole way down, and gets there.</summary>
        [Test]
        public void TheSecondHalfOfTheZoom_SwoopsAllTheWayDown()
        {
            Assert.That(CameraRig.SwoopAt(0.75f), Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(CameraRig.SwoopAt(1f), Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// It only ever goes one way, so the view never tips back up part way
        /// through a zoom that is going in.
        /// </summary>
        [Test]
        public void TheSwoop_NeverGoesBackwards()
        {
            float last = -1f;
            for (int step = 0; step <= 20; step++)
            {
                float swoop = CameraRig.SwoopAt(step / 20f);
                Assert.That(swoop, Is.GreaterThanOrEqualTo(last), $"The tilt went backwards at zoom {step / 20f}.");
                last = swoop;
            }
        }

        /// <summary>From a corner view, E goes to the next tidy view round: an eighth of a turn, onto a side view.</summary>
        [Test]
        public void PressingE_FromACornerView_TurnsExactlyAnEighth()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(45f).Within(0.001f));
            Assert.That(IsATidyView(rig.TargetYaw));
        }

        /// <summary>And Q goes the other way.</summary>
        [Test]
        public void PressingQ_FromACornerView_TurnsAnEighthTheOtherWay()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(-1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(-45f).Within(0.001f));
            Assert.That(IsATidyView(rig.TargetYaw));
        }

        /// <summary>Two taps are a quarter turn: the next corner, where one tap used to land.</summary>
        [Test]
        public void TappingETwice_TurnsAQuarter_OntoTheNextCorner()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(1);
            rig.StepToTheNextCorner(1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(90f).Within(0.001f));
            Assert.That(IsACornerView(rig.TargetYaw));
        }

        /// <summary>Eight taps bring the view all the way round to where it started.</summary>
        [Test]
        public void EightTaps_AreAWholeTurn()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;
            for (int i = 0; i < 8; i++)
            {
                rig.StepToTheNextCorner(1);
            }

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(0f).Within(0.001f));
        }

        /// <summary>The corner views sit at 45 degrees and every 90 from there.</summary>
        private static bool IsACornerView(float yaw)
        {
            float fromACorner = Mathf.Repeat(yaw - 45f, 90f);
            return fromACorner < 0.001f || fromACorner > 90f - 0.001f;
        }

        /// <summary>The tidy views sit every 45 degrees from the first corner: corners and sides alike.</summary>
        private static bool IsATidyView(float yaw)
        {
            float fromAView = Mathf.Repeat(yaw - 45f, 45f);
            return fromAView < 0.001f || fromAView > 45f - 0.001f;
        }
    }
}

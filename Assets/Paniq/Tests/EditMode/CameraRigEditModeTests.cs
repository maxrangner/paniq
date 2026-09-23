using NUnit.Framework;
using Paniq.Presentation;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The camera the player drives. Two things here are easy to break without
    /// anybody noticing until they are playing: the tilt has to stay flat for
    /// the first half of the wheel and only then swoop, and Q and E have to
    /// land on a tidy corner view however far a drag has wandered off one.
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

        /// <summary>From a corner view, E goes to the next one round: a quarter turn.</summary>
        [Test]
        public void PressingE_FromACornerView_TurnsExactlyAQuarter()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(90f).Within(0.001f));
        }

        /// <summary>And Q goes the other way.</summary>
        [Test]
        public void PressingQ_FromACornerView_TurnsAQuarterTheOtherWay()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(-1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(-90f).Within(0.001f));
        }

        /// <summary>
        /// The point of the change: after dragging the view to some angle of
        /// its own, Q and E still land on a tidy corner rather than turning a
        /// quarter from wherever the drag stopped.
        /// </summary>
        [TestCase(10f)]
        [TestCase(40f)]
        [TestCase(-25f)]
        [TestCase(200f)]
        public void AfterADrag_TheNextCornerIsStillATidyOne(float dragged)
        {
            CameraRig rig = NewRig();
            rig.Turn(dragged);

            rig.StepToTheNextCorner(1);

            Assert.That(IsACornerView(rig.TargetYaw),
                $"After dragging {dragged} degrees, E landed on {rig.TargetYaw}, which is not a corner view.");
        }

        /// <summary>
        /// A drag never lands on a corner by itself, and never springs back to
        /// one: where the hand leaves the view is where it stays.
        /// </summary>
        [Test]
        public void ADrag_LeavesTheViewWhereTheHandPutIt_AndDoesNotSnapBack()
        {
            CameraRig rig = NewRig();
            float before = rig.Yaw;

            rig.Turn(37f);

            Assert.That(Mathf.DeltaAngle(before, rig.Yaw), Is.EqualTo(37f).Within(0.001f),
                "The shown angle should follow the hand exactly.");
            Assert.That(Mathf.DeltaAngle(rig.Yaw, rig.TargetYaw), Is.EqualTo(0f).Within(0.001f),
                "Nothing should be left pulling the view somewhere else after the drag.");
        }

        /// <summary>Two quick taps turn two corners, not one and a bit.</summary>
        [Test]
        public void TappingETwice_TurnsTwoCorners()
        {
            CameraRig rig = NewRig();
            float before = rig.TargetYaw;

            rig.StepToTheNextCorner(1);
            rig.StepToTheNextCorner(1);

            Assert.That(Mathf.DeltaAngle(before, rig.TargetYaw), Is.EqualTo(180f).Within(0.001f));
        }

        /// <summary>The corner views sit at 45 degrees and every 90 from there.</summary>
        private static bool IsACornerView(float yaw)
        {
            float fromACorner = Mathf.Repeat(yaw - 45f, 90f);
            return fromACorner < 0.001f || fromACorner > 90f - 0.001f;
        }
    }
}

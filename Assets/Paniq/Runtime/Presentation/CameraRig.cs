using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The camera the player drives, built to
    /// <c>docs/look-and-controls.md</c>: the building is always seen from a
    /// corner, W A S D slide the view across it, Q and E swing a quarter turn
    /// to the next corner, and the wheel zooms while tilting the camera as it
    /// goes -- further out looks down on the building, closer looks along the
    /// floor.
    /// <para>
    /// The rig owns where the camera rests. A bang adds a shake on top of
    /// that rest position rather than writing the transform itself, so the two
    /// never fight over it.
    /// </para>
    /// <para>
    /// It runs on the clock that ignores pausing, so the player can still look
    /// around a frozen scene.
    /// </para>
    /// </summary>
    internal sealed class CameraRig
    {
        /// <summary>The classic isometric elevation: a step north and a step east cover the same distance on screen.</summary>
        private const float IsometricPitch = 35.264f;

        /// <summary>Zoomed all the way in, the camera looks much more along the floor.</summary>
        private const float ClosePitch = 18f;

        /// <summary>How far in the closest view is, as a share of the view that frames the whole building.</summary>
        private const float CloseSizeFraction = 0.16f;

        private const float PanMetresPerSecond = 14f;
        private const float ZoomPerWheelNotch = 0.12f;

        /// <summary>How far past the building's edge the player may push the view.</summary>
        private const float PanMarginMetres = 8f;

        /// <summary>How quickly a quarter turn and a zoom settle. Higher is snappier.</summary>
        private const float Settle = 9f;

        private readonly Camera camera;

        /// <summary>How tall the rooms' walls are drawn, which the framing has to leave room for.</summary>
        private const float WallHeightMetres = 1.5f;

        /// <summary>The middle of the building, and how far the view may wander from it.</summary>
        private readonly Vector3 buildingCentre;
        private readonly float panRangeX;
        private readonly float panRangeZ;

        /// <summary>The orthographic size that frames the whole building.</summary>
        private readonly float framedSize;

        /// <summary>Which corner the view is from, and which it is heading for.</summary>
        private int corner;
        private float yaw;

        /// <summary>0 is the whole building in view, 1 is as close as it goes.</summary>
        private float zoom;
        private float shownZoom;

        /// <summary>The point on the floor the camera looks at.</summary>
        private Vector3 focus;

        public CameraRig(Camera camera, Bounds floorPlan)
        {
            this.camera = camera;

            // The building is not just its floor: the rooms stand 1.5 m up and
            // the storey below hangs a good five metres down. The view is aimed
            // at the middle of all that, or the tower's foot falls off the
            // bottom of the screen.
            float top = WallHeightMetres;
            float bottom = -BuildingShellView.DepthMetres;
            buildingCentre = new Vector3(floorPlan.center.x, (top + bottom) * 0.5f, floorPlan.center.z);
            focus = buildingCentre;
            panRangeX = floorPlan.extents.x + PanMarginMetres;
            panRangeZ = floorPlan.extents.z + PanMarginMetres;

            // Seen from the isometric angle, the floor's diagonal spans this
            // much across the screen, and this much up it once the building's
            // full height is added, with a tenth of a margin on top.
            float across = (floorPlan.size.x + floorPlan.size.z) * 0.70711f;
            float up = across * 0.57735f + (top - bottom);
            float aspect = camera.aspect > 0.1f ? camera.aspect : 16f / 9f;
            framedSize = Mathf.Max(up * 0.5f, across * 0.5f / aspect) * 1.1f;

            camera.orthographic = true;

            // A flat dark background rather than Unity's default sky: the
            // building is a model on a table, and the dark is what the tower
            // fades into at its foot.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.14f);
            yaw = YawFor(corner);
            shownZoom = zoom;
            Place();
        }

        /// <summary>The camera's resting place, for a shake to be added on top of.</summary>
        public Vector3 Rest { get; private set; }

        /// <summary>
        /// One frame of the player's camera keys. <paramref name="shake"/> is
        /// whatever a bang is doing to the view this frame.
        /// </summary>
        public void Update(Vector3 shake)
        {
            // Unscaled: a paused round still lets the player look around.
            float delta = Time.unscaledDeltaTime;
            ReadKeys(delta);
            ReadWheel();

            float blend = 1f - Mathf.Exp(-Settle * delta);
            yaw = Mathf.LerpAngle(yaw, YawFor(corner), blend);
            shownZoom = Mathf.Lerp(shownZoom, zoom, blend);
            Place();
            camera.transform.position = Rest + shake;
        }

        private void ReadKeys(float delta)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var move = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

            if (move.sqrMagnitude > 0.0001f)
            {
                // W always moves the view up the screen, whichever corner it is
                // from, so the keys never change meaning when the player turns.
                Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
                Vector3 forward = flat * Vector3.forward;
                Vector3 right = flat * Vector3.right;

                // Panning is slower when zoomed in, so the same key press
                // covers about the same amount of screen either way.
                float speed = PanMetresPerSecond * Mathf.Lerp(1f, CloseSizeFraction + 0.15f, shownZoom);
                focus += (right * move.x + forward * move.y).normalized * (speed * delta);
                focus.x = Mathf.Clamp(focus.x, buildingCentre.x - panRangeX, buildingCentre.x + panRangeX);
                focus.z = Mathf.Clamp(focus.z, buildingCentre.z - panRangeZ, buildingCentre.z + panRangeZ);
                focus.y = buildingCentre.y;
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                corner--;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                corner++;
            }
        }

        private void ReadWheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float notches = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(notches) < 0.01f)
            {
                return;
            }

            zoom = Mathf.Clamp01(zoom + Mathf.Sign(notches) * ZoomPerWheelNotch);
        }

        /// <summary>Puts the camera where the corner, the zoom and the focus say it goes.</summary>
        private void Place()
        {
            float pitch = Mathf.Lerp(IsometricPitch, ClosePitch, shownZoom);
            camera.orthographicSize = Mathf.Lerp(framedSize, framedSize * CloseSizeFraction, shownZoom);

            var rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Far enough back that nothing in the building is ever behind the
            // camera's near plane, whatever the tilt.
            float back = framedSize * 4f + 20f;
            Rest = focus - rotation * Vector3.forward * back;
            camera.transform.SetPositionAndRotation(Rest, rotation);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = back * 2f + 200f;
        }

        /// <summary>The four corners: 45 degrees around from each other, so two walls recede either way.</summary>
        private static float YawFor(int corner) => 45f + 90f * corner;
    }
}

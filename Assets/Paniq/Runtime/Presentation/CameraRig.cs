using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The camera the player drives, built to
    /// <c>docs/look-and-controls.md</c>: W A S D slide the view across the
    /// building, holding the right mouse button and dragging swings it to any
    /// angle at all, Q and E snap a quarter turn to the next corner from
    /// wherever it happens to be pointing, and the wheel zooms.
    /// <para>
    /// A drag does not spring back when it is let go. The four corners are
    /// still there as somewhere tidy to land, but they are no longer the only
    /// places the view can be.
    /// </para>
    /// <para>
    /// The zoom tilts the camera as well, but not straight away: the first
    /// half of the wheel's travel comes straight in from the isometric angle,
    /// and only past halfway does the view swoop down to look along the floor.
    /// Tilting from the very first notch made a small zoom feel like a lurch.
    /// </para>
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

        /// <summary>
        /// How far the view swings for each pixel the pointer is dragged. A
        /// whole turn takes about a screen and a half of travel, which is
        /// enough to aim finely without becoming a chore.
        /// </summary>
        private const float DegreesPerDragPixel = 0.25f;

        /// <summary>
        /// How far the pointer has to travel with the right button down before
        /// it counts as turning the view rather than clicking. Below this a
        /// right click still means "put the card down", which is what it has
        /// always meant.
        /// </summary>
        private const float DragPixels = 5f;

        /// <summary>
        /// How much of the wheel's travel is spent coming straight in before
        /// the camera starts tipping toward the floor.
        /// </summary>
        private const float ZoomBeforeTheSwoop = 0.5f;

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

        /// <summary>Where the view is pointing, and where it is heading for.</summary>
        private float yaw;
        private float targetYaw = YawFor(0);

        /// <summary>The right-button drag in progress, and how far it has travelled.</summary>
        private bool dragging;
        private float dragTravel;

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
            yaw = targetYaw;
            shownZoom = zoom;
            Place();
        }

        /// <summary>The camera's resting place, for a shake to be added on top of.</summary>
        public Vector3 Rest { get; private set; }

        /// <summary>
        /// Whether the right button is being used to swing the view rather
        /// than to click. A card in hand is put down by a right <em>click</em>,
        /// so whoever reads the pointer has to be able to tell the two apart --
        /// otherwise every drag would also throw the card away.
        /// </summary>
        public bool IsTurningTheView => dragging && dragTravel >= DragPixels;

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
            ReadDrag();

            float blend = 1f - Mathf.Exp(-Settle * delta);
            yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
            shownZoom = Mathf.Lerp(shownZoom, zoom, blend);
            KeepTheAnglesSmall();
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
                StepToTheNextCorner(-1);
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                StepToTheNextCorner(1);
            }
        }

        /// <summary>
        /// Swings to the next corner view round from wherever the view is
        /// heading. Measured from where it is <em>going</em> rather than where
        /// it has got to, so tapping the key twice quickly turns two corners
        /// instead of losing the second tap to the first one's travel.
        /// </summary>
        internal void StepToTheNextCorner(int direction)
        {
            // The corner views are the whole numbers on this scale.
            float where = (targetYaw - 45f) / 90f;
            float next = direction > 0 ? Mathf.Floor(where + 1f) : Mathf.Ceil(where - 1f);
            targetYaw = YawFor(Mathf.RoundToInt(next));
        }

        /// <summary>
        /// One frame of a right-button drag. While the button is down the view
        /// follows the hand exactly, with no easing at all: a view that lagged
        /// behind the pointer felt like dragging something heavy through mud.
        /// </summary>
        private void ReadDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                dragging = false;
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                dragging = true;
                dragTravel = 0f;
            }

            if (dragging && mouse.rightButton.isPressed)
            {
                Vector2 moved = mouse.delta.ReadValue();
                dragTravel += moved.magnitude;
                Turn(moved.x * DegreesPerDragPixel);
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                dragging = false;
            }
        }

        /// <summary>
        /// Swings the view by this much at once. Both the shown angle and the
        /// one being eased toward move together, so letting go of a drag
        /// leaves the view exactly where the hand put it rather than easing
        /// on somewhere else afterwards.
        /// </summary>
        internal void Turn(float degrees)
        {
            targetYaw += degrees;
            yaw += degrees;
        }

        /// <summary>
        /// Drags add up, so after enough of them the angles would be thousands
        /// of degrees and start to lose their precision. Both are shifted by
        /// the same whole number of turns, which leaves the view untouched.
        /// </summary>
        private void KeepTheAnglesSmall()
        {
            if (targetYaw >= -360f && targetYaw <= 360f)
            {
                return;
            }

            float whole = Mathf.Floor(targetYaw / 360f) * 360f;
            targetYaw -= whole;
            yaw -= whole;
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
            float pitch = Mathf.Lerp(IsometricPitch, ClosePitch, SwoopAt(shownZoom));
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

        /// <summary>The four corners: 90 degrees around from each other, so two walls recede either way.</summary>
        private static float YawFor(int corner) => 45f + 90f * corner;

        /// <summary>
        /// How far through the tilt the camera is at this much zoom. Flat at
        /// nothing for the first half of the wheel's travel -- the view simply
        /// comes straight in -- and then eased the whole way down over the
        /// second half, so the swoop starts and ends without a kink in it.
        /// </summary>
        internal static float SwoopAt(float zoom)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZoomBeforeTheSwoop, 1f, zoom));
        }

        /// <summary>The view's angle right now, for a test to check a turn landed.</summary>
        internal float Yaw => yaw;

        /// <summary>Where the view is swinging to, for a test to check a turn was asked for.</summary>
        internal float TargetYaw => targetYaw;
    }
}

using System;
using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The player's pointer and keys. One hand, two ways (2026-09-30, the
    /// owner's rules): the left button draws people, the right button pushes
    /// them away.
    /// <list type="bullet">
    /// <item>the left button held down on a door, a thing, a pull station or
    /// a patch of floor is the player's hand on it: a full pull the moment it
    /// goes down, gone the moment it comes up, one place at a time. A quick
    /// click leaves it there for three seconds (the owner: "a single click
    /// should place an influence beacon for 3 seconds");</item>
    /// <item>the right button does the same the other way round: people are
    /// pushed away from the place (the owner: "an anti-influence. Works same
    /// as the left mouse button, but in reverse"). On a person it pushes the
    /// people round them. The key and the hand holding a door shut are gone
    /// from the mouse (the owner's choice, "pure push-away");</item>
    /// <item>the left button held down on a person is a tug on their shirt:
    /// they are held where they are until it comes up, unless they are strong
    /// enough to tear free; a quick left click on a person is still a poke,
    /// sent when the button comes back up inside the window.</item>
    /// </list>
    /// The cards are gone (2026-09-30, the owner: "remove cards"): nothing is
    /// picked up or thrown any more. The right button no longer swings the
    /// camera (the owner, 2026-09-29): Q and E do that, in eighths.
    /// <para>
    /// Everything here is presentation: rays, colliders and screen positions
    /// never leave this class. What reaches the run is a door's stable ID, a
    /// person's stable ID, or a place in whole millimetres, which is what the
    /// simulation contract allows a command to carry.
    /// </para>
    /// </summary>
    internal sealed class PlayerInput
    {
        /// <summary>
        /// How near the pointer must be to somebody, on the screen, to count as
        /// pointing at them.
        /// <para>
        /// This used to be a distance across the floor, measured from where the
        /// pointer met the ground. That is the wrong place to measure from: you
        /// aim at a body drawn a metre in the air, and with the camera tilted
        /// low the floor under somebody's chest is metres behind their feet, so
        /// the click landed on empty carpet and the card was never played.
        /// </para>
        /// </summary>
        private const float PickPersonPixels = 45f;

        /// <summary>
        /// How high up a body the pointer is taken to be aiming, in metres.
        /// The middle of the capsule <see cref="AgentViews"/> draws.
        /// </summary>
        private const float TorsoHeight = 0.5f;

        /// <summary>
        /// How often, in seconds, the person under an idle pointer is looked
        /// for. Finding them means putting everybody in the building on the
        /// screen, which is fine for a click and wasteful sixty times a
        /// second for a hover line in a large crowd; a click always looks
        /// afresh.
        /// </summary>
        private const float PersonHoverSeconds = 0.1f;

        /// <summary>How near the pointer must be to a thing, on the screen, to count as pointing at it.</summary>
        private const float PickThingPixels = 30f;

        /// <summary>
        /// The same for a small thing (under <see cref="SmallThingMillimetres"/>):
        /// the keycard is the size of a hand and was too hard to hit (the
        /// owner, 2026-09-30: "I couldn't get them to pick up keycard. Hit box
        /// too small").
        /// </summary>
        private const float PickSmallThingPixels = 42f;

        private const int SmallThingMillimetres = 200;

        /// <summary>How far in front of where the pointer meets somebody's body a nudge is taken to come from, toward the camera, in metres.</summary>
        private const float NudgeFromMetres = 0.3f;

        private SimulationId? personUnderPointer;
        private float nextPersonLook = float.NegativeInfinity;

        private readonly RunDriver runner;
        private readonly RoomView room;

        /// <summary>The left button on a person: a click is a poke, a hold is a tug.</summary>
        private readonly DoorClicks leftHand = new DoorClicks();

        /// <summary>Where the press on a person landed, for the poke it may turn out to be.</summary>
        private LogicalPosition pokeFrom;

        /// <summary>The hand on a place (a door, a thing, the floor), and which button put it there.</summary>
        private readonly PlaceHold place = new PlaceHold();

        /// <summary>The held hand following the pointer (2026-09-30).</summary>
        private readonly HandDrag drag = new HandDrag();

        /// <summary>Where on the floor this frame's press landed, and whether the pointer was over floor at all.</summary>
        private LogicalPosition pressFloor;
        private bool pressFloorKnown;

        /// <summary>The ground, for turning a screen position into a place on the floor.</summary>
        private static readonly Plane Ground = new Plane(Vector3.up, 0f);

        public PlayerInput(RunDriver runner, RoomView room)
        {
            this.runner = runner;
            this.room = room;
        }

        /// <summary>The door under the pointer, for the hover highlight.</summary>
        public SimulationId? HoveredDoor { get; private set; }

        /// <summary>The person the player has by the shirt, for the hover line.</summary>
        public SimulationId? TuggedPerson => leftHand.Held;

        /// <summary>Whether the player's hand is on a place right now.</summary>
        public bool HandOnAPlace => place.IsOn;

        /// <summary>Whether the hand on a place is the right button's, which pushes people away.</summary>
        public bool HandRepels => place.Repels;

        /// <summary>The fire alarm under the pointer, for the hover line.</summary>
        public SimulationId? HoveredAlarm { get; private set; }

        /// <summary>The person under the pointer: with a person-card picked, or with nothing picked (a poke or a tug).</summary>
        public SimulationId? HoveredPerson { get; private set; }

        /// <summary>The thing under the pointer, with nothing picked: a hold on it draws people to it.</summary>
        public SimulationId? HoveredThing { get; private set; }

        /// <summary>The patch of floor under the pointer, with nothing picked: a hold on it draws people to it.</summary>
        public LogicalPosition? HoveredFloor { get; private set; }

        /// <param name="lookOnly">
        /// The world is stopped, or a screen is covering it. The pointer
        /// still tells the player what is under it, but nothing they press
        /// reaches the run: pause is for looking, not for acting. Every hand
        /// comes off, because a release would never reach a stopped run.
        /// </param>
        /// <param name="pointerOverHud">
        /// The pointer is over a button or the bar. A press there is the HUD's
        /// and never the world's, and nothing in the world is hovered. A
        /// button coming back up over the HUD still lets go of whatever the
        /// press had hold of.
        /// </param>
        /// <param name="now">The clock a click is told from a hold on, in seconds.</param>
        public void Update(Camera camera, RunSnapshot snapshot, bool lookOnly = false,
            bool pointerOverHud = false, float now = 0f)
        {
            HoveredDoor = null;
            HoveredAlarm = null;
            HoveredPerson = null;
            HoveredThing = null;
            HoveredFloor = null;

            Mouse mouse = Mouse.current;
            bool leftDown = mouse != null && mouse.leftButton.isPressed;
            bool rightDown = mouse != null && mouse.rightButton.isPressed;
            if (lookOnly)
            {
                // Every hand comes off, because the release would never reach
                // the run while it is stopped.
                LetGoOfEverything();
            }
            else
            {
                ReadTheButtonsComingUp(leftDown, rightDown, now);
            }

            if (mouse == null || camera == null || snapshot == null || pointerOverHud)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();

            // A hand held down follows the pointer (2026-09-30): dragged, the
            // hand moves, and whoever answers it follows.
            if (!lookOnly && place.IsOn && TryGroundPoint(camera, pointer, out LogicalPosition under) &&
                drag.Moved(under, now, out LogicalPosition moveTo))
            {
                runner.QueueMoveInfluence(moveTo);
            }

            // The right button pushes (2026-09-30); the left wins a press of
            // both on one frame.
            bool pressed = mouse.leftButton.wasPressedThisFrame && !lookOnly;
            bool pushed = !pressed && mouse.rightButton.wasPressedThisFrame && !lookOnly;
            UpdateWorldPress(camera, snapshot, pointer, pressed, pushed, now);
        }

        /// <summary>Every hand off: the place and the person, whatever the buttons are doing.</summary>
        private void LetGoOfEverything()
        {
            drag.Clear();
            if (place.Clear())
            {
                runner.QueueReleaseInfluence();
            }

            SimulationId? person = leftHand.Clear();
            if (person.HasValue)
            {
                runner.QueueReleaseTug(person.Value);
            }
        }

        /// <summary>
        /// The buttons coming back up, wherever the pointer is now: the hand
        /// comes off the place -- or, after a click, stays there a moment as a
        /// beacon (2026-09-30) -- or off the person; a press on a person let
        /// go of inside the window was a poke. The tug is asked for before
        /// the poke, because the two turn on the same instant.
        /// </summary>
        private void ReadTheButtonsComingUp(bool leftDown, bool rightDown, float now)
        {
            switch (place.ComingUp(leftDown, rightDown, now))
            {
                case PlaceHold.Ending.Release:
                    drag.Clear();
                    runner.QueueReleaseInfluence();
                    break;
                case PlaceHold.Ending.Leave:
                    drag.Clear();
                    runner.QueueLeaveInfluence();
                    break;
            }

            SimulationId? letGo = leftHand.Release(leftDown);
            if (letGo.HasValue)
            {
                runner.QueueReleaseTug(letGo.Value);
            }

            SimulationId? taken = leftHand.Hold(now, leftDown);
            if (taken.HasValue)
            {
                runner.QueueTug(taken.Value);
            }

            SimulationId? poked = leftHand.Clicked(leftDown);
            if (poked.HasValue)
            {
                runner.QueueNudge(poked.Value, pokeFrom);
            }
        }

        /// <summary>
        /// With nothing in hand: a fire alarm or a door under the pointer
        /// first (they are solid things the ray can hit), then whichever is
        /// drawn nearer the pointer of the nearest person and the nearest
        /// thing, then the floor. A button going down on a place is the hand
        /// going on it, sent at once -- the left to draw people, the right to
        /// push them away (2026-09-30). The left going down on a person
        /// starts the window that tells a poke from a tug; the right going
        /// down on a person pushes away the people round them.
        /// </summary>
        private void UpdateWorldPress(Camera camera, RunSnapshot snapshot, Vector2 pointer, bool pressed, bool pushed,
            float now)
        {
            int button = pressed ? 1 : pushed ? 2 : 0;
            bool repels = button == 2;
            pressFloorKnown = button != 0 && TryGroundPoint(camera, pointer, out pressFloor);

            // Door leaves swing, so their colliders must be where they are drawn.
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // A fire alarm: pulled, where the level lets the player (the
                // run decides the price, see PlayerCommandSystem); on the
                // office only people pull them, and the hand on it draws
                // people to it -- or, with the right button, away.
                if (room.TryGetAlarm(hit.collider, out SimulationId alarmId))
                {
                    HoveredAlarm = alarmId;
                    if (pressed && snapshot.PlayerMayPullAlarms)
                    {
                        runner.QueueAlarmPull(alarmId);
                    }
                    else if (button != 0 && TryAlarmPosition(alarmId, out LogicalPosition at))
                    {
                        HandOn(() => runner.QueueInfluenceSpot(at, repels), button, now, onTheFloor: false);
                    }

                    return;
                }

                if (room.TryGetDoor(hit.collider, out SimulationId doorId))
                {
                    HoveredDoor = doorId;
                    if (button != 0)
                    {
                        HandOn(() => runner.QueueInfluenceDoor(doorId, repels), button, now, onTheFloor: false);
                    }

                    return;
                }
            }

            bool onTheFloor = TryGroundPoint(camera, pointer, out LogicalPosition floor);

            // Somebody, perhaps. A press always looks for the person afresh;
            // the hover line looks ten times a second.
            if (button != 0 || now >= nextPersonLook)
            {
                personUnderPointer = NearestPerson(camera, snapshot, pointer, out personPixels);
                nextPersonLook = now + PersonHoverSeconds;
            }

            // A thing drawn nearer the pointer than the nearest person wins
            // it (2026-09-30): the keycard on a desk with somebody sitting at
            // it used to be unclickable, because a person anywhere near always
            // came first.
            SimulationId? thing = NearestThing(camera, snapshot, pointer, out float thingPixels);
            bool personFirst = personUnderPointer.HasValue && (!thing.HasValue || personPixels <= thingPixels);

            if (personFirst)
            {
                HoveredPerson = personUnderPointer;
                if (pressed)
                {
                    // A poke or a tug: which, the button coming back up decides.
                    pokeFrom = WhereANudgeComesFrom(camera, pointer, snapshot, HoveredPerson.Value);
                    leftHand.Press(HoveredPerson.Value, now);
                }
                else if (pushed)
                {
                    // Push the people round them away (2026-09-30).
                    LogicalPosition at = PositionOf(snapshot, HoveredPerson.Value);
                    HandOn(() => runner.QueueInfluenceSpot(at, true), button, now, onTheFloor: true);
                }

                return;
            }

            // A thing: the hand on it draws people to where it stands, or
            // pushes them from it.
            HoveredThing = thing;
            if (HoveredThing.HasValue)
            {
                if (button != 0)
                {
                    SimulationId pointed = HoveredThing.Value;
                    HandOn(() => runner.QueueInfluenceThing(pointed, repels), button, now, onTheFloor: false);
                }

                return;
            }

            // Otherwise the floor itself.
            if (onTheFloor)
            {
                HoveredFloor = floor;
                if (button != 0)
                {
                    HandOn(() => runner.QueueInfluenceSpot(floor, repels), button, now, onTheFloor: true);
                }
            }
        }

        /// <summary>How far the nearest person was from the pointer on the screen, the last time anybody was looked for.</summary>
        private float personPixels = float.MaxValue;

        /// <summary>
        /// The hand goes on a place: the press is sent at once, and the button
        /// coming up will send the release, or leave a beacon. Held and
        /// dragged, it follows the pointer: from the floor at once, off a
        /// door or a thing once the pointer has clearly left it (2026-09-30).
        /// </summary>
        private void HandOn(Action press, int button, float now, bool onTheFloor)
        {
            press();
            place.Press(button, now);
            if (pressFloorKnown)
            {
                drag.Press(pressFloor, onTheFloor, now);
            }
            else
            {
                drag.Clear();
            }
        }

        /// <summary>
        /// Where a nudge comes from: where the pointer meets the body at chest
        /// height, pulled back a little toward the camera. A click on their
        /// left side pushes them right; one dead centre pushes them away from
        /// the camera.
        /// </summary>
        private static LogicalPosition WhereANudgeComesFrom(Camera camera, Vector2 pointer, RunSnapshot snapshot,
            SimulationId person)
        {
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Ray ray = camera.ScreenPointToRay(pointer);
            var chest = new Plane(Vector3.up, -TorsoHeight);
            Vector3 hit = chest.Raycast(ray, out float distance)
                ? ray.GetPoint(distance)
                : PresentationUtility.ToUnityPosition(PositionOf(snapshot, person));
            Vector3 from = hit - forward * NudgeFromMetres;
            return new LogicalPosition(
                Mathf.RoundToInt(from.x * Run.MillimetresPerMetre),
                Mathf.RoundToInt(from.z * Run.MillimetresPerMetre));
        }

        private static LogicalPosition PositionOf(RunSnapshot snapshot, SimulationId person)
        {
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                if (snapshot.Agents[i].AgentId == person)
                {
                    return snapshot.Agents[i].Position;
                }
            }

            return default;
        }

        /// <summary>
        /// The loose thing drawn nearest the pointer, if any is near enough:
        /// one in the world, not held, not wreckage. Measured to the middle of
        /// the thing where it is drawn -- up on the desk for a card or a laptop
        /// (2026-09-30: it used to be measured to a spot a few centimetres off
        /// the floor under it, most of a desk's height below the card) -- and
        /// a small thing may be a little further off.
        /// </summary>
        private static SimulationId? NearestThing(Camera camera, RunSnapshot snapshot, Vector2 pointer, out float pixels)
        {
            float best = float.MaxValue;
            SimulationId? found = null;
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                if (thing.Dormant || thing.IsHeld || thing.Wrecked)
                {
                    continue;
                }

                Vector3 onScreen = camera.WorldToScreenPoint(DrawnMiddle(thing));
                if (onScreen.z <= 0f)
                {
                    continue;
                }

                float reach = thing.SizeMillimetres < SmallThingMillimetres ? PickSmallThingPixels : PickThingPixels;
                float distance = (new Vector2(onScreen.x, onScreen.y) - pointer).magnitude;
                if (distance <= reach && distance < best)
                {
                    best = distance;
                    found = thing.ObjectId;
                }
            }

            pixels = best;
            return found;
        }

        /// <summary>
        /// The middle of a thing as it is drawn: where the engine has it, when
        /// it keeps a pose for it; else on the floor, or on the desk for a
        /// thing resting on one.
        /// </summary>
        internal static Vector3 DrawnMiddle(PhysicsObjectSnapshot thing)
        {
            float half = Mathf.Min(0.5f, thing.SizeMillimetres / 2000f);
            if (thing.Pose.IsKnown)
            {
                return BoxViews.PoseOrigin(thing.Pose) + Vector3.up * Mathf.Min(0.1f, half);
            }

            float floor = thing.Resting ? RoomView.TableHeight : 0f;
            return PresentationUtility.ToUnityPosition(thing.Position) + Vector3.up * (floor + half);
        }

        /// <summary>Where a fire alarm is on the wall, from the level's own list of them.</summary>
        private bool TryAlarmPosition(SimulationId alarmId, out LogicalPosition at)
        {
            AlarmDefinition[] alarms = runner.Simulation.Scenario.Alarms;
            for (int i = 0; i < alarms.Length; i++)
            {
                if (alarms[i].AlarmId == alarmId)
                {
                    at = alarms[i].Position;
                    return true;
                }
            }

            at = default;
            return false;
        }

        /// <summary>
        /// Where the pointer meets the floor, in whole millimetres. Worked out
        /// against the mathematical ground plane, so no collider is needed.
        /// </summary>
        private static bool TryGroundPoint(Camera camera, Vector2 pointer, out LogicalPosition spot)
        {
            spot = default;
            Ray ray = camera.ScreenPointToRay(pointer);
            if (!Ground.Raycast(ray, out float distance))
            {
                return false;
            }

            Vector3 point = ray.GetPoint(distance);
            spot = new LogicalPosition(
                Mathf.RoundToInt(point.x * Run.MillimetresPerMetre),
                Mathf.RoundToInt(point.z * Run.MillimetresPerMetre));
            return true;
        }

        /// <summary>
        /// Whoever is drawn nearest the pointer, if anybody is near enough.
        /// Measured on the screen, where the player is actually aiming, rather
        /// than across the floor: a body stands a metre up in the air, so the
        /// two are nowhere near each other once the camera tilts.
        /// </summary>
        internal static SimulationId? NearestPerson(Camera camera, RunSnapshot snapshot, Vector2 pointer) =>
            NearestPerson(camera, snapshot, pointer, out _);

        /// <summary>The same, and how far from the pointer they are drawn, in pixels.</summary>
        internal static SimulationId? NearestPerson(Camera camera, RunSnapshot snapshot, Vector2 pointer, out float pixels)
        {
            float best = PickPersonPixels * PickPersonPixels;
            SimulationId? found = null;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                AgentSnapshot agent = snapshot.Agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                Vector3 torso = PresentationUtility.ToUnityPosition(agent.Position) + Vector3.up * TorsoHeight;
                Vector3 onScreen = camera.WorldToScreenPoint(torso);
                if (onScreen.z <= 0f)
                {
                    // Behind the camera: it comes back mirrored onto the screen,
                    // so somebody stood behind you could be picked instead.
                    continue;
                }

                float distance = (new Vector2(onScreen.x, onScreen.y) - pointer).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    found = agent.AgentId;
                }
            }

            pixels = found.HasValue ? Mathf.Sqrt(best) : float.MaxValue;
            return found;
        }
    }
}

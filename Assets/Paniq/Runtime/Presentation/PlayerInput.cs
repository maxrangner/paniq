using System;
using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The player's pointer and keys. Two hands (2026-09-29, the owner's
    /// rules): the left button is the crowd, the right button is the
    /// building.
    /// <list type="bullet">
    /// <item>the left button held down on a door, a thing, a pull station or
    /// a patch of floor is the player's hand on it: a full pull the moment it
    /// goes down, gone the moment it comes up, one place at a time;</item>
    /// <item>the left button held down on a person is a tug on their shirt:
    /// they are held where they are until it comes up, unless they are strong
    /// enough to tear free; a quick left click on a person is still a poke,
    /// sent when the button comes back up inside the window;</item>
    /// <item>a right click on a door turns its key; the right button held
    /// down on a door is a hand holding it shut (prototype 3, 2026-09-25,
    /// moved from the left button); a right click elsewhere puts down the
    /// card in hand.</item>
    /// </list>
    /// With a card picked, a left click plays it on the spot on the floor
    /// under the pointer. A card is picked up by clicking it on the screen
    /// (2026-09-25; the number keys are gone) and put down with Escape or a
    /// right click. The right button no longer swings the camera (the owner,
    /// 2026-09-29): Q and E do that, in eighths.
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

        /// <summary>How far in front of where the pointer meets somebody's body a nudge is taken to come from, toward the camera, in metres.</summary>
        private const float NudgeFromMetres = 0.3f;

        private SimulationId? personUnderPointer;
        private float nextPersonLook = float.NegativeInfinity;

        private readonly RunDriver runner;
        private readonly RoomView room;

        /// <summary>The right button on a door: a click is the key, a hold is a hand on it.</summary>
        private readonly DoorClicks rightHand = new DoorClicks();

        /// <summary>The left button on a person: a click is a poke, a hold is a tug.</summary>
        private readonly DoorClicks leftHand = new DoorClicks();

        /// <summary>Where the press on a person landed, for the poke it may turn out to be.</summary>
        private LogicalPosition pokeFrom;

        /// <summary>The left button is down on a place (a door, a thing, the floor): the hand is on it until the button comes up.</summary>
        private bool handOnAPlace;

        /// <summary>The ground, for turning a screen position into a place on the floor.</summary>
        private static readonly Plane Ground = new Plane(Vector3.up, 0f);

        public PlayerInput(RunDriver runner, RoomView room)
        {
            this.runner = runner;
            this.room = room;
        }

        /// <summary>The card the player has picked up, or none.</summary>
        public PlayerCommandType? SelectedCard { get; private set; }

        /// <summary>The door under the pointer, for the hover highlight.</summary>
        public SimulationId? HoveredDoor { get; private set; }

        /// <summary>The door the player is holding shut with the right button, for the hover line.</summary>
        public SimulationId? HeldDoor => rightHand.Held;

        /// <summary>The person the player has by the shirt, for the hover line.</summary>
        public SimulationId? TuggedPerson => leftHand.Held;

        /// <summary>Whether the player's hand is on a place right now.</summary>
        public bool HandOnAPlace => handOnAPlace;

        /// <summary>The fire alarm under the pointer, for the hover line.</summary>
        public SimulationId? HoveredAlarm { get; private set; }

        /// <summary>The person under the pointer: with a person-card picked, or with nothing picked (a poke or a tug).</summary>
        public SimulationId? HoveredPerson { get; private set; }

        /// <summary>Where on the floor the pointer is, while a place-card is picked.</summary>
        public LogicalPosition? HoveredSpot { get; private set; }

        /// <summary>The thing under the pointer, with nothing picked: a hold on it draws people to it.</summary>
        public SimulationId? HoveredThing { get; private set; }

        /// <summary>The patch of floor under the pointer, with nothing picked: a hold on it draws people to it.</summary>
        public LogicalPosition? HoveredFloor { get; private set; }

        /// <summary>
        /// The cards the player is holding, in the order they were dealt.
        /// <para>
        /// This used to be a fixed list of every card in the game, because
        /// every card was always available and only the purse decided whether
        /// one could be played. Cards are now dealt by the dead, so the bar is
        /// a hand that grows and shrinks during the round, and it comes from
        /// the run rather than from here. Two of a kind are drawn as one
        /// card with a count on it; picking the kind up is picking one of them.
        /// </para>
        /// </summary>
        public IReadOnlyList<PlayerCommandType> Hand { get; private set; } = Array.Empty<PlayerCommandType>();

        /// <summary>
        /// Nothing is aimed at a chosen person any more: every card is thrown
        /// at a patch of floor and catches whoever is standing in it. Kept as a
        /// method rather than deleted because the end screen's "click somebody
        /// for their facts" still wants the person-picking below.
        /// </summary>
        public static bool TargetsAPerson(PlayerCommandType card) => false;

        public static string NameOf(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return "Beefcake";
                case PlayerCommandType.PlayCourage: return "Courage";
                case PlayerCommandType.PlayTerror: return "Terror";
                case PlayerCommandType.PlayBastard: return "Bastard";
                case PlayerCommandType.PlayColdHeart: return "Cold heart";
                case PlayerCommandType.SpawnFire: return "Start a fire";
                case PlayerCommandType.SpawnExtinguisher: return "Fire extinguisher";
                case PlayerCommandType.BlastWall: return "TNT";
                case PlayerCommandType.PopFuseBox: return "Pop the fuse box";
                case PlayerCommandType.PullAlarm: return "Pull a fire alarm";
                case PlayerCommandType.StickTogether: return "Stick together";
                default: return card.ToString();
            }
        }

        /// <summary>
        /// The card on the screen was clicked: pick it up, or put it back down
        /// if it was the one in hand. Called from the HUD as it draws.
        /// </summary>
        public void Toggle(PlayerCommandType card)
        {
            if (!Holding(card))
            {
                return;
            }

            SelectedCard = SelectedCard == card ? (PlayerCommandType?)null : card;
        }

        /// <param name="lookOnly">
        /// The world is stopped, or a card is covering the screen. The pointer
        /// still tells the player what is under it, but nothing they press
        /// reaches the run: pause is for looking, not for acting. Every hand
        /// comes off, because a release would never reach a stopped run.
        /// </param>
        /// <param name="pointerOverHud">
        /// The pointer is over a card or a button. A press there is the HUD's
        /// and never the world's, and nothing in the world is hovered. A
        /// button coming back up over the HUD still lets go of whatever the
        /// press had hold of.
        /// </param>
        /// <param name="now">The clock a click is told from a hold on, in seconds.</param>
        public void Update(Camera camera, RunSnapshot snapshot, bool lookOnly = false,
            bool pointerOverHud = false, float now = 0f)
        {
            SimulationId? doorLastFrame = HoveredDoor;
            HoveredDoor = null;
            HoveredAlarm = null;
            HoveredPerson = null;
            HoveredSpot = null;
            HoveredThing = null;
            HoveredFloor = null;
            Hand = snapshot != null ? snapshot.Hand : Array.Empty<PlayerCommandType>();

            // A card that has just been played, or that was never theirs, is
            // not still in their hand to aim.
            if (SelectedCard.HasValue && !Holding(SelectedCard.Value))
            {
                SelectedCard = null;
            }

            Mouse mouse = Mouse.current;
            bool leftDown = mouse != null && mouse.leftButton.isPressed;
            bool rightDown = mouse != null && mouse.rightButton.isPressed;
            if (lookOnly)
            {
                // A card picked up before the freeze is put back down, so
                // unpausing never plays something the player has forgotten
                // about; and every hand comes off, because the release would
                // never reach the run while it is stopped.
                SelectedCard = null;
                LetGoOfEverything();
            }
            else
            {
                ReadKeys(rightDown, now, doorLastFrame);
                ReadTheLeftButtonComingUp(leftDown, now);
            }

            if (mouse == null || camera == null || snapshot == null || pointerOverHud)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.wasPressedThisFrame && !lookOnly;
            if (SelectedCard == null)
            {
                UpdateWorldPress(camera, snapshot, pointer, pressed, now);
                return;
            }

            PlayerCommandType card = SelectedCard.Value;
            if (TargetsAPerson(card))
            {
                // Aimed at a body on the screen, not at a place on the floor.
                HoveredPerson = NearestPerson(camera, snapshot, pointer);
                if (pressed && HoveredPerson.HasValue)
                {
                    runner.QueueCard(card, HoveredPerson.Value);
                    SelectedCard = null;
                }

                return;
            }

            // Fire, an extinguisher and TNT are put somewhere rather than given
            // to somebody, so for those the floor under the pointer is exactly
            // the right place to read.
            if (!TryGroundPoint(camera, pointer, out LogicalPosition spot))
            {
                return;
            }

            HoveredSpot = spot;
            if (pressed)
            {
                runner.QueueCard(card, spot);
                SelectedCard = null;
            }
        }

        /// <summary>Every hand off: the place, the person and the door, whatever the buttons are doing.</summary>
        private void LetGoOfEverything()
        {
            if (handOnAPlace)
            {
                handOnAPlace = false;
                runner.QueueReleaseInfluence();
            }

            SimulationId? person = leftHand.Clear();
            if (person.HasValue)
            {
                runner.QueueReleaseTug(person.Value);
            }

            SimulationId? door = rightHand.Clear();
            if (door.HasValue)
            {
                runner.QueueReleaseDoor(door.Value);
            }
        }

        /// <summary>
        /// The left button coming back up, wherever the pointer is now: the
        /// hand comes off the place, or off the person; a press on a person
        /// let go of inside the window was a poke. The tug is asked for
        /// before the poke, because the two turn on the same instant.
        /// </summary>
        private void ReadTheLeftButtonComingUp(bool leftDown, float now)
        {
            if (handOnAPlace && !leftDown)
            {
                handOnAPlace = false;
                runner.QueueReleaseInfluence();
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
        /// first (they are solid things the ray can hit), then the nearest
        /// person on the screen, then the nearest thing, then the floor. The
        /// left button going down on a place is the hand going on it, sent at
        /// once; going down on a person starts the window that tells a poke
        /// from a tug.
        /// </summary>
        private void UpdateWorldPress(Camera camera, RunSnapshot snapshot, Vector2 pointer, bool pressed, float now)
        {
            // Door leaves swing, so their colliders must be where they are drawn.
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // A fire alarm: pulled, where the level lets the player (the
                // run decides the price, see PlayerCommandSystem); on the
                // office only people pull them, and the hand on it draws
                // people to it.
                if (room.TryGetAlarm(hit.collider, out SimulationId alarmId))
                {
                    HoveredAlarm = alarmId;
                    if (pressed)
                    {
                        if (snapshot.PlayerMayPullAlarms)
                        {
                            runner.QueueAlarmPull(alarmId);
                        }
                        else if (TryAlarmPosition(alarmId, out LogicalPosition at))
                        {
                            HandOn(() => runner.QueueInfluenceSpot(at));
                        }
                    }

                    return;
                }

                if (room.TryGetDoor(hit.collider, out SimulationId doorId))
                {
                    HoveredDoor = doorId;
                    if (pressed)
                    {
                        HandOn(() => runner.QueueInfluenceDoor(doorId));
                    }

                    return;
                }
            }

            bool onTheFloor = TryGroundPoint(camera, pointer, out LogicalPosition floor);

            // Somebody, perhaps. A press always looks for the person afresh;
            // the hover line looks ten times a second.
            if (pressed || now >= nextPersonLook)
            {
                personUnderPointer = NearestPerson(camera, snapshot, pointer);
                nextPersonLook = now + PersonHoverSeconds;
            }

            HoveredPerson = personUnderPointer;
            if (HoveredPerson.HasValue)
            {
                if (pressed)
                {
                    // A poke or a tug: which, the button coming back up decides.
                    pokeFrom = WhereANudgeComesFrom(camera, pointer, snapshot, HoveredPerson.Value);
                    leftHand.Press(HoveredPerson.Value, now);
                }

                return;
            }

            // A thing: the hand on it draws people to where it stands.
            HoveredThing = NearestThing(camera, snapshot, pointer);
            if (HoveredThing.HasValue)
            {
                if (pressed)
                {
                    SimulationId thing = HoveredThing.Value;
                    HandOn(() => runner.QueueInfluenceThing(thing));
                }

                return;
            }

            // Otherwise the floor itself.
            if (onTheFloor)
            {
                HoveredFloor = floor;
                if (pressed)
                {
                    HandOn(() => runner.QueueInfluenceSpot(floor));
                }
            }
        }

        /// <summary>The hand goes on a place: the press is sent at once, and the button coming up will send the release.</summary>
        private void HandOn(Action press)
        {
            press();
            handOnAPlace = true;
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

        /// <summary>The loose thing drawn nearest the pointer, if any is near enough: one in the world, not held, not wreckage.</summary>
        private static SimulationId? NearestThing(Camera camera, RunSnapshot snapshot, Vector2 pointer)
        {
            float best = PickThingPixels * PickThingPixels;
            SimulationId? found = null;
            for (int i = 0; i < snapshot.PhysicsObjects.Count; i++)
            {
                PhysicsObjectSnapshot thing = snapshot.PhysicsObjects[i];
                if (thing.Dormant || thing.IsHeld || thing.Wrecked)
                {
                    continue;
                }

                Vector3 middle = PresentationUtility.ToUnityPosition(thing.Position) +
                                 Vector3.up * Mathf.Min(0.5f, thing.SizeMillimetres / 2000f);
                Vector3 onScreen = camera.WorldToScreenPoint(middle);
                if (onScreen.z <= 0f)
                {
                    continue;
                }

                float distance = (new Vector2(onScreen.x, onScreen.y) - pointer).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    found = thing.ObjectId;
                }
            }

            return found;
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
        /// Whether a hand can go on this door, as the run last drew it: a
        /// door in a frame that is shut or open, not locked, broken, a pair of
        /// swing doors or a hole. The run decides again when the command
        /// lands; this only keeps the screen from believing in a hold the run
        /// was never going to take.
        /// </summary>
        private static bool CanBeHeld(RunSnapshot snapshot, SimulationId door)
        {
            if (snapshot == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.Doors.Count; i++)
            {
                DoorSnapshot d = snapshot.Doors[i];
                if (d.DoorId == door)
                {
                    return !d.Swings && !d.IsHole &&
                           (d.State == DoorState.Unlocked || d.State == DoorState.Open);
                }
            }

            return false;
        }

        /// <summary>
        /// The right button and the keys. Escape or a right click puts the
        /// card in hand back down; with no card in hand, the right button on
        /// a door is the key (a click) or a hand holding it shut (a hold), told
        /// apart by the same window the left button uses on a person. There is
        /// no drag any more to wait for, so the card is put down on the press.
        /// </summary>
        private void ReadKeys(bool rightDown, float now, SimulationId? doorUnderThePointer)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool rightPressed = mouse != null && mouse.rightButton.wasPressedThisFrame;

            SimulationId? released = rightHand.Release(rightDown);
            if (released.HasValue)
            {
                runner.QueueReleaseDoor(released.Value);
            }

            SimulationId? taken = rightHand.Hold(now, rightDown);
            if (taken.HasValue)
            {
                if (CanBeHeld(runner.Snapshot, taken.Value))
                {
                    runner.QueueHoldDoor(taken.Value);
                }
                else
                {
                    // Swing doors, holes, broken and locked doors take no
                    // hand: nothing is sent, and nothing is thought held.
                    rightHand.Release(false);
                }
            }

            SimulationId? keyed = rightHand.Clicked(rightDown);
            if (keyed.HasValue)
            {
                runner.QueueLockToggle(keyed.Value);
            }

            if (rightPressed && SelectedCard == null && doorUnderThePointer.HasValue)
            {
                rightHand.Press(doorUnderThePointer.Value, now);
                return;
            }

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || rightPressed)
            {
                SelectedCard = null;
            }
        }

        /// <summary>Whether that card is on the bar. A short list, walked rather than searched.</summary>
        private bool Holding(PlayerCommandType card)
        {
            for (int i = 0; i < Hand.Count; i++)
            {
                if (Hand[i] == card)
                {
                    return true;
                }
            }

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
        internal static SimulationId? NearestPerson(Camera camera, RunSnapshot snapshot, Vector2 pointer)
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

            return found;
        }
    }
}

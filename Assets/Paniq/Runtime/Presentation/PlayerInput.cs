using System;
using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The player's pointer and keys. With no card picked, a left click on a
    /// door leaf works that door and a double click turns its key; with a card
    /// picked, a left click plays it on the spot on the floor under the
    /// pointer. A card is picked up by clicking it on the screen (2026-09-25;
    /// the number keys are gone) and put down with Escape or a right click.
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

        private readonly RunDriver runner;
        private readonly RoomView room;
        private readonly DoorClicks clicks = new DoorClicks();

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

        /// <summary>The fire alarm under the pointer, for the hover line.</summary>
        public SimulationId? HoveredAlarm { get; private set; }

        /// <summary>The person under the pointer while a person-card is picked.</summary>
        public SimulationId? HoveredPerson { get; private set; }

        /// <summary>Where on the floor the pointer is, while a place-card is picked.</summary>
        public LogicalPosition? HoveredSpot { get; private set; }

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
        /// reaches the run: pause is for looking, not for acting.
        /// </param>
        /// <param name="turningTheView">
        /// The right button is being dragged to swing the camera. The right
        /// button also puts a card back down, so without knowing this every
        /// swing of the view would throw away whatever was in hand.
        /// </param>
        /// <param name="pointerOverHud">
        /// The pointer is over a card or a button. A click there is the HUD's
        /// and never the world's, and nothing in the world is hovered.
        /// </param>
        /// <param name="now">The clock the double-click window is measured on, in seconds.</param>
        public void Update(Camera camera, RunSnapshot snapshot, bool lookOnly = false,
            bool turningTheView = false, bool pointerOverHud = false, float now = 0f)
        {
            HoveredDoor = null;
            HoveredAlarm = null;
            HoveredPerson = null;
            HoveredSpot = null;
            Hand = snapshot != null ? snapshot.Hand : Array.Empty<PlayerCommandType>();

            // A card that has just been played, or that was never theirs, is
            // not still in their hand to aim.
            if (SelectedCard.HasValue && !Holding(SelectedCard.Value))
            {
                SelectedCard = null;
            }

            if (lookOnly)
            {
                // A card picked up before the freeze is put back down, so
                // unpausing never plays something the player has forgotten about.
                SelectedCard = null;
                clicks.Clear();
            }
            else
            {
                ReadKeys(turningTheView);

                // A single click whose double-click window has closed is sent now.
                SendSingleClick(clicks.Settle(now));
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || camera == null || snapshot == null || pointerOverHud)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool clicked = mouse.leftButton.wasPressedThisFrame && !lookOnly;
            if (SelectedCard == null)
            {
                UpdateDoors(camera, pointer, clicked, now);
                return;
            }

            PlayerCommandType card = SelectedCard.Value;
            if (TargetsAPerson(card))
            {
                // Aimed at a body on the screen, not at a place on the floor.
                HoveredPerson = NearestPerson(camera, snapshot, pointer);
                if (clicked && HoveredPerson.HasValue)
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
            if (clicked)
            {
                runner.QueueCard(card, spot);
                SelectedCard = null;
            }
        }

        private void UpdateDoors(Camera camera, Vector2 pointer, bool clicked, float now)
        {
            // Door leaves swing, so their colliders must be where they are drawn.
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                return;
            }

            // A fire alarm is clicked like a door: pulled for a price, which
            // the run decides (see PlayerCommandSystem).
            if (room.TryGetAlarm(hit.collider, out SimulationId alarmId))
            {
                HoveredAlarm = alarmId;
                if (clicked)
                {
                    runner.QueueAlarmPull(alarmId);
                }

                return;
            }

            if (!room.TryGetDoor(hit.collider, out SimulationId doorId))
            {
                return;
            }

            HoveredDoor = doorId;
            if (!clicked)
            {
                return;
            }

            // One click works the door, held back for the double-click
            // window; a second click inside it turns the key instead.
            if (clicks.Press(doorId, now, out SimulationId? settled))
            {
                runner.QueueLockToggle(doorId);
            }

            SendSingleClick(settled);
        }

        /// <summary>
        /// The single click on a door, once it is certain to be one. A locked
        /// door gets nothing from a single click: its key is a double click,
        /// and the hover line says so.
        /// </summary>
        private void SendSingleClick(SimulationId? door)
        {
            if (!door.HasValue || room.StateOf(door.Value) == DoorState.Locked)
            {
                return;
            }

            runner.QueueDoorClick(door.Value);
        }

        /// <summary>
        /// Escape or a right click puts the card in hand back down.
        /// <para>
        /// The card is dropped on the right button being <em>released</em>
        /// rather than pressed, because at the moment of pressing nobody yet
        /// knows whether this is a click or the start of a drag that swings
        /// the camera. By the time it comes back up, they do.
        /// </para>
        /// </summary>
        private void ReadKeys(bool turningTheView)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool rightClicked = Mouse.current != null &&
                                Mouse.current.rightButton.wasReleasedThisFrame &&
                                !turningTheView;
            if (keyboard.escapeKey.wasPressedThisFrame || rightClicked)
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

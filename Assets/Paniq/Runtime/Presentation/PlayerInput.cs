using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// The player's pointer and keys. With no card picked, a left click on a
    /// door leaf clicks that door, as it always has. With a card picked, a left
    /// click plays it: on the person under the pointer, or on the spot on the
    /// floor under it.
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

        private readonly FireReactionRunner runner;
        private readonly RoomView room;

        /// <summary>The ground, for turning a screen position into a place on the floor.</summary>
        private static readonly Plane Ground = new Plane(Vector3.up, 0f);

        public PlayerInput(FireReactionRunner runner, RoomView room)
        {
            this.runner = runner;
            this.room = room;
        }

        /// <summary>The card the player has picked up, or none.</summary>
        public PlayerCommandType? SelectedCard { get; private set; }

        /// <summary>The door under the pointer, for the hover highlight.</summary>
        public SimulationId? HoveredDoor { get; private set; }

        /// <summary>The person under the pointer while a person-card is picked.</summary>
        public SimulationId? HoveredPerson { get; private set; }

        /// <summary>Where on the floor the pointer is, while a place-card is picked.</summary>
        public LogicalPosition? HoveredSpot { get; private set; }

        /// <summary>The cards, in the order their number keys run.</summary>
        public static readonly PlayerCommandType[] Cards =
        {
            PlayerCommandType.PlayBeefcake,
            PlayerCommandType.SpawnFire,
            PlayerCommandType.SpawnExtinguisher,
            PlayerCommandType.BlastWall,
            PlayerCommandType.PopFuseBox
        };

        public static bool TargetsAPerson(PlayerCommandType card) => card == PlayerCommandType.PlayBeefcake;

        /// <summary>The number keys, in the order the cards run along the bar.</summary>
        private static readonly UnityEngine.InputSystem.Key[] NumberKeys =
        {
            UnityEngine.InputSystem.Key.Digit1,
            UnityEngine.InputSystem.Key.Digit2,
            UnityEngine.InputSystem.Key.Digit3,
            UnityEngine.InputSystem.Key.Digit4,
            UnityEngine.InputSystem.Key.Digit5,
            UnityEngine.InputSystem.Key.Digit6
        };

        public static string NameOf(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return "Beefcake";
                case PlayerCommandType.SpawnFire: return "Start a fire";
                case PlayerCommandType.SpawnExtinguisher: return "Put down an extinguisher";
                case PlayerCommandType.BlastWall: return "TNT: blow open a wall";
                case PlayerCommandType.PopFuseBox: return "Pop the fuse box";
                default: return card.ToString();
            }
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
        public void Update(Camera camera, FireReactionSnapshot snapshot, bool lookOnly = false,
            bool turningTheView = false)
        {
            HoveredDoor = null;
            HoveredPerson = null;
            HoveredSpot = null;
            if (lookOnly)
            {
                // A card picked up before the freeze is put back down, so
                // unpausing never plays something the player has forgotten about.
                SelectedCard = null;
            }
            else
            {
                ReadKeys(turningTheView);
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || camera == null || snapshot == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool clicked = mouse.leftButton.wasPressedThisFrame && !lookOnly;
            if (SelectedCard == null)
            {
                UpdateDoors(camera, pointer, clicked);
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

        private void UpdateDoors(Camera camera, Vector2 pointer, bool clicked)
        {
            // Door leaves swing, so their colliders must be where they are drawn.
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f) || !room.TryGetDoor(hit.collider, out SimulationId doorId))
            {
                return;
            }

            HoveredDoor = doorId;
            if (clicked)
            {
                runner.QueueDoorClick(doorId);
            }
        }

        /// <summary>
        /// Number keys pick a card up; Escape or a right click puts it down
        /// again.
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

            // One branch per card rather than a ladder of them: the fifth
            // card was the moment copying the fourth stopped being sensible.
            for (int i = 0; i < Cards.Length && i < NumberKeys.Length; i++)
            {
                if (keyboard[NumberKeys[i]].wasPressedThisFrame)
                {
                    Pick(i);
                    break;
                }
            }

            bool rightClicked = Mouse.current != null &&
                                Mouse.current.rightButton.wasReleasedThisFrame &&
                                !turningTheView;
            if (keyboard.escapeKey.wasPressedThisFrame || rightClicked)
            {
                SelectedCard = null;
            }
        }

        private void Pick(int index)
        {
            // Pressing the same number again puts the card back down.
            PlayerCommandType card = Cards[index];
            SelectedCard = SelectedCard == card ? (PlayerCommandType?)null : card;
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
                Mathf.RoundToInt(point.x * FireReactionSimulation.MillimetresPerMetre),
                Mathf.RoundToInt(point.z * FireReactionSimulation.MillimetresPerMetre));
            return true;
        }

        /// <summary>
        /// Whoever is drawn nearest the pointer, if anybody is near enough.
        /// Measured on the screen, where the player is actually aiming, rather
        /// than across the floor: a body stands a metre up in the air, so the
        /// two are nowhere near each other once the camera tilts.
        /// </summary>
        internal static SimulationId? NearestPerson(Camera camera, FireReactionSnapshot snapshot, Vector2 pointer)
        {
            float best = PickPersonPixels * PickPersonPixels;
            SimulationId? found = null;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
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

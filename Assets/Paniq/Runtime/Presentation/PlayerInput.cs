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
        /// <summary>How near the pointer must be to somebody to count as pointing at them.</summary>
        private const float PickPersonMetres = 0.55f;

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
            PlayerCommandType.BlastWall
        };

        public static bool TargetsAPerson(PlayerCommandType card) => card == PlayerCommandType.PlayBeefcake;

        public static string NameOf(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake: return "Beefcake";
                case PlayerCommandType.SpawnFire: return "Start a fire";
                case PlayerCommandType.SpawnExtinguisher: return "Put down an extinguisher";
                case PlayerCommandType.BlastWall: return "TNT: blow open a wall";
                default: return card.ToString();
            }
        }

        public void Update(Camera camera, FireReactionSnapshot snapshot)
        {
            HoveredDoor = null;
            HoveredPerson = null;
            HoveredSpot = null;
            ReadKeys();

            Mouse mouse = Mouse.current;
            if (mouse == null || camera == null || snapshot == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool clicked = mouse.leftButton.wasPressedThisFrame;
            if (SelectedCard == null)
            {
                UpdateDoors(camera, pointer, clicked);
                return;
            }

            PlayerCommandType card = SelectedCard.Value;
            if (!TryGroundPoint(camera, pointer, out LogicalPosition spot))
            {
                return;
            }

            if (TargetsAPerson(card))
            {
                HoveredPerson = NearestPerson(snapshot, spot);
                if (clicked && HoveredPerson.HasValue)
                {
                    runner.QueueCard(card, HoveredPerson.Value);
                    SelectedCard = null;
                }

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

        /// <summary>Number keys pick a card up; Escape or right click puts it down again.</summary>
        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Pick(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Pick(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Pick(2);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                Pick(3);
            }

            bool cancelled = keyboard.escapeKey.wasPressedThisFrame ||
                             (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (cancelled)
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

        /// <summary>Whoever is standing nearest the pointer, if anybody is near enough.</summary>
        private static SimulationId? NearestPerson(FireReactionSnapshot snapshot, LogicalPosition spot)
        {
            long reach = (long)(PickPersonMetres * FireReactionSimulation.MillimetresPerMetre);
            long best = reach * reach;
            SimulationId? found = null;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
                if (agent.Participation != AgentParticipation.Participating)
                {
                    continue;
                }

                long distance = LogicalPosition.DistanceSquared(agent.Position, spot);
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

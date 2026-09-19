using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paniq.Presentation
{
    /// <summary>
    /// Turns a left click on a door leaf into a door click for the runner.
    /// The ray only finds which door was clicked; the simulation decides what
    /// the click does.
    /// </summary>
    internal sealed class DoorClickInput
    {
        private readonly FireReactionRunner runner;
        private readonly RoomView room;

        public DoorClickInput(FireReactionRunner runner, RoomView room)
        {
            this.runner = runner;
            this.room = room;
        }

        /// <summary>Queues a click if the button went down over a door. Returns the door under the pointer, if any.</summary>
        public SimulationId? Update(Camera camera)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || camera == null)
            {
                return null;
            }

            // Door leaves swing, so their colliders must be where they are drawn.
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f) || !room.TryGetDoor(hit.collider, out SimulationId doorId))
            {
                return null;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                runner.QueueDoorClick(doorId);
            }

            return doorId;
        }
    }
}

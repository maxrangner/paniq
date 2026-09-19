using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>The text at the top left: tick, fire, the head count, and what a door click will do.</summary>
    internal static class PrototypeHud
    {
        public static void Draw(FireReactionSnapshot snapshot, FireReactionScenarioData scenario, SimulationId? hoveredDoor,
            DoorState hoveredState)
        {
            GUI.color = Color.white;
            string fireText = snapshot.FireActive
                ? $"FIRE  {snapshot.FireCells.Count} squares burning"
                : $"FIRE IN {Mathf.Max(0f, (scenario.Fire.ActivationTick - snapshot.Tick) / (float)FireReactionSimulation.TicksPerSecond):0.00} s";
            GUI.Label(new Rect(20f, 20f, 360f, 24f), $"Fire-reaction prototype  |  tick {snapshot.Tick}");
            GUI.Label(new Rect(20f, 44f, 360f, 24f), fireText);
            GUI.Label(new Rect(20f, 68f, 520f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount})   " +
                $"Down {snapshot.DownCount}   Lost {snapshot.LostCount}   Escaped {snapshot.EscapedCount}");
            GUI.Label(new Rect(20f, 92f, 520f, 24f),
                "Click a door: red = locked. Click once to unlock (green), again to open.");
            if (hoveredDoor.HasValue)
            {
                string action = hoveredState == DoorState.Locked ? "Click to unlock"
                    : hoveredState == DoorState.Unlocked ? "Click to open"
                    : "Open";
                GUI.Label(new Rect(20f, 116f, 360f, 24f), $"Door {hoveredDoor.Value.Value}: {action}");
            }
        }
    }
}

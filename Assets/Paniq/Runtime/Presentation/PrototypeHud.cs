using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The text at the top left: tick, fire, the head count, and what a door
    /// click will do. Tab toggles a plain table of everyone's traits and state.
    /// </summary>
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
            GUI.Label(new Rect(20f, 68f, 640f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount}, on fire {snapshot.BurningCount})   " +
                $"Down {snapshot.DownCount} (out cold {snapshot.UnconsciousCount})   Lost {snapshot.LostCount}   Escaped {snapshot.EscapedCount}");
            GUI.Label(new Rect(20f, 92f, 520f, 24f),
                "Click a door: red = locked. Click once to unlock (green), again to open.   Tab: everyone's stats");
            if (hoveredDoor.HasValue)
            {
                string action = hoveredState == DoorState.Locked ? "Click to unlock"
                    : hoveredState == DoorState.Unlocked ? "Click to open"
                    : hoveredState == DoorState.Broken ? "Broken down"
                    : "Open";
                GUI.Label(new Rect(20f, 116f, 360f, 24f), $"Door {hoveredDoor.Value.Value}: {action}");
            }
        }

        /// <summary>One row per person, numbered like the labels over their heads.</summary>
        public static void DrawStats(FireReactionSnapshot snapshot)
        {
            const float rowHeight = 20f;
            float width = 560f;
            float height = rowHeight * (snapshot.Agents.Count + 2) + 12f;
            var area = new Rect(Screen.width - width - 20f, 20f, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = area.x + 10f;
            float y = area.y + 6f;
            string[] headings = { "#", "Str", "Spd", "Brv", "Cmp", "Evl", "Nrv", "Panics by", "Now" };
            DrawRow(x, y, rowHeight, headings);
            y += rowHeight;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
                AgentTraitValues t = agent.Traits;
                DrawRow(x, y, rowHeight, new[]
                {
                    (i + 1).ToString(), t.Strength.ToString(), t.Speed.ToString(), t.Bravery.ToString(),
                    t.Compassion.ToString(), t.Evil.ToString(), t.Nervousness.ToString(),
                    TemperamentText(agent.Temperament), StateText(agent)
                });
                y += rowHeight;
            }

            GUI.Label(new Rect(x, y, width, rowHeight), "Traits run 0-10; 5 is an ordinary person.");
        }

        private static readonly float[] ColumnX = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 290f, 400f };

        private static void DrawRow(float x, float y, float height, string[] cells)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                float right = c + 1 < ColumnX.Length ? ColumnX[c + 1] : ColumnX[c] + 140f;
                GUI.Label(new Rect(x + ColumnX[c], y, right - ColumnX[c], height), cells[c]);
            }
        }

        private static string TemperamentText(AgentPanicTemperament temperament)
        {
            switch (temperament)
            {
                case AgentPanicTemperament.FreezeForever: return "freezing";
                case AgentPanicTemperament.FreezeThenRun: return "freeze, run";
                default: return "running";
            }
        }

        private static string StateText(FireReactionAgentSnapshot agent)
        {
            if (agent.Outcome == AgentTerminalOutcome.Lost)
            {
                return "lost";
            }

            if (agent.Outcome == AgentTerminalOutcome.Escaped)
            {
                return "escaped";
            }

            if (agent.IsBurning)
            {
                return agent.BodyState == AgentBodyState.Upright ? "on fire!" : "burning on the floor";
            }

            if (agent.BodyState == AgentBodyState.Unconscious)
            {
                return "out cold";
            }

            if (agent.BodyState != AgentBodyState.Upright)
            {
                return agent.BodyState == AgentBodyState.Staggering ? "staggering" : "down";
            }

            if (agent.FearState == AgentFearState.Calm)
            {
                return "calm";
            }

            if (agent.FearState == AgentFearState.Alert)
            {
                return "startled";
            }

            switch (agent.ActivityState)
            {
                case AgentActivityState.FetchingItem: return "going for an item";
                case AgentActivityState.PickingUp: return "picking it up";
                case AgentActivityState.CarryingItem: return "carrying";
                case AgentActivityState.SettingDown: return "putting it down";
                case AgentActivityState.TryingDoor: return "trying a door";
                case AgentActivityState.ForcingDoor: return "shoving a door";
                case AgentActivityState.OpeningDoor: return "opening a door";
                default: return agent.ActivityState.ToString().ToLowerInvariant();
            }
        }
    }
}

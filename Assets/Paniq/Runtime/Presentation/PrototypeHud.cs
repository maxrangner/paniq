using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The text at the top left: tick, fire, the head count, and what a door
    /// click will do. Along the bottom, the influence the player has left and
    /// the cards they can spend it on. Tab toggles a plain table of everyone's
    /// traits and state.
    /// </summary>
    internal static class PrototypeHud
    {
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFill = new Color(0.3f, 0.75f, 1f, 0.9f);
        private static readonly Color CardPicked = new Color(0.25f, 0.55f, 0.85f, 0.95f);
        private static readonly Color CardAffordable = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color CardTooDear = new Color(0.25f, 0.1f, 0.1f, 0.7f);

        public static void Draw(
            FireReactionSnapshot snapshot,
            FireReactionScenarioData scenario,
            SimulationId? hoveredDoor,
            DoorState hoveredState)
        {
            GUI.color = Color.white;
            string fireText = snapshot.FireActive
                ? $"FIRE  {snapshot.FireCells.Count} squares burning"
                : $"FIRE IN {Mathf.Max(0f, (scenario.Fire.ActivationTick - snapshot.Tick) / (float)FireReactionSimulation.TicksPerSecond):0.00} s";
            GUI.Label(new Rect(20f, 20f, 360f, 24f), $"Fire-reaction prototype  |  tick {snapshot.Tick}");
            GUI.Label(new Rect(20f, 44f, 480f, 24f),
                snapshot.AlarmsRinging ? $"{fireText}   |   ALARM RINGING" : fireText);
            GUI.Label(new Rect(20f, 68f, 900f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount}, on fire {snapshot.BurningCount})   " +
                $"Down {snapshot.DownCount} (out cold {snapshot.UnconsciousCount})   Lost {snapshot.LostCount}   " +
                $"Escaped {snapshot.EscapedCount}   In a room with no fire {snapshot.ClearOfFireCount}");
            GUI.Label(new Rect(20f, 92f, 900f, 24f),
                "Click a door: red = locked. Click to unlock (green), again to open, again to close.   " +
                "Tab: everyone's stats   G: the floor people can walk on");
            if (hoveredDoor.HasValue)
            {
                string action = hoveredState == DoorState.Locked ? "Click to unlock"
                    : hoveredState == DoorState.Unlocked ? "Click to open"
                    : hoveredState == DoorState.Broken ? "Broken down"
                    : "Click to close (if nobody is in the doorway)";
                GUI.Label(new Rect(20f, 116f, 600f, 24f), $"Door {hoveredDoor.Value.Value}: {action}");
            }
        }

        /// <summary>
        /// The player's purse and their cards, along the bottom. A card they
        /// cannot afford is dimmed red and cannot be picked up; the one in their
        /// hand is highlighted, and the line above says what a click will do.
        /// </summary>
        public static void DrawCards(FireReactionSnapshot snapshot, PlayerCommandType? selected, PlayerInput input)
        {
            const float cardWidth = 210f;
            const float cardHeight = 34f;
            const float gap = 8f;
            float bottom = Screen.height - 20f;

            // The purse.
            var barArea = new Rect(20f, bottom - cardHeight - gap - 22f, cardWidth * 2f, 16f);
            GUI.color = BarBack;
            GUI.DrawTexture(barArea, Texture2D.whiteTexture);
            GUI.color = BarFill;
            float fraction = snapshot.InfluenceMaximum <= 0
                ? 0f
                : Mathf.Clamp01(snapshot.Influence / (float)snapshot.InfluenceMaximum);
            GUI.DrawTexture(new Rect(barArea.x, barArea.y, barArea.width * fraction, barArea.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(barArea.x + barArea.width + 10f, barArea.y - 3f, 400f, 22f),
                $"Influence {snapshot.Influence}   (spent {snapshot.InfluenceSpent}, earned back {snapshot.InfluenceEarned})");

            // The cards.
            for (int i = 0; i < PlayerInput.Cards.Length; i++)
            {
                PlayerCommandType card = PlayerInput.Cards[i];
                int cost = snapshot.CostOf(card);
                bool affordable = snapshot.Influence >= cost;
                var area = new Rect(20f + i * (cardWidth + gap), bottom - cardHeight, cardWidth, cardHeight);
                GUI.color = selected == card ? CardPicked : affordable ? CardAffordable : CardTooDear;
                GUI.DrawTexture(area, Texture2D.whiteTexture);
                GUI.color = affordable ? Color.white : new Color(1f, 0.7f, 0.7f, 0.8f);
                GUI.Label(new Rect(area.x + 8f, area.y + 7f, area.width - 16f, 22f),
                    $"{i + 1}. {PlayerInput.NameOf(card)}  ({cost})");
            }

            GUI.color = Color.white;
            string hint = selected == null
                ? "Press 1-4 to pick a card, then click. Escape or right click puts it down."
                : PlayerInput.TargetsAPerson(selected.Value)
                    ? $"{PlayerInput.NameOf(selected.Value)}: click a person" +
                      (input.HoveredPerson.HasValue ? $"  ->  person {input.HoveredPerson.Value.Value}" : string.Empty)
                    : selected.Value == PlayerCommandType.BlastWall
                        ? $"TNT: click a wall  ({snapshot.BlastChargesRemaining} left)"
                        : $"{PlayerInput.NameOf(selected.Value)}: click a spot on the floor";
            GUI.Label(new Rect(20f, bottom - cardHeight - gap - 44f, 900f, 22f), hint);
        }

        /// <summary>
        /// One row per person, numbered like the labels over their heads, then
        /// <paramref name="footer"/>: which physics feel is in use, and so on.
        /// </summary>
        public static void DrawStats(FireReactionSnapshot snapshot, string footer)
        {
            const float rowHeight = 20f;
            float width = 640f;
            float height = rowHeight * (snapshot.Agents.Count + 3) + 12f;
            var area = new Rect(Screen.width - width - 20f, 20f, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float x = area.x + 10f;
            float y = area.y + 6f;
            string[] headings = { "#", "Str", "Spd", "Brv", "Cmp", "Evl", "Nrv", "Ldr", "Panics by", "Now" };
            DrawRow(x, y, rowHeight, headings);
            y += rowHeight;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                FireReactionAgentSnapshot agent = snapshot.Agents[i];
                AgentTraitValues t = agent.Traits;
                DrawRow(x, y, rowHeight, new[]
                {
                    (i + 1).ToString(), t.Strength.ToString(), t.Speed.ToString(), t.Bravery.ToString(),
                    t.Compassion.ToString(), t.Evil.ToString(), t.Nervousness.ToString(), t.Leadership.ToString(),
                    TemperamentText(agent.Temperament), StateText(agent)
                });
                y += rowHeight;
            }

            GUI.Label(new Rect(x, y, width, rowHeight), "Traits run 0-10; 5 is an ordinary person.");
            GUI.Label(new Rect(x, y + rowHeight, width - 20f, rowHeight), footer);
        }

        private static readonly float[] ColumnX = { 0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 330f, 440f };

        private static void DrawRow(float x, float y, float height, string[] cells)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                float right = c + 1 < ColumnX.Length ? ColumnX[c + 1] : ColumnX[c] + 180f;
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
                case AgentActivityState.ShakingAwake: return "shaking someone awake";
                case AgentActivityState.Grabbing: return "grabbing someone";
                case AgentActivityState.Dragging: return "dragging someone";
                case AgentActivityState.Following: return "following someone";
                case AgentActivityState.FetchingExtinguisher: return "going for an extinguisher";
                case AgentActivityState.Spraying: return "spraying the fire";
                case AgentActivityState.GoingToSit: return "going to sit down";
                case AgentActivityState.Sitting: return "sitting";
                case AgentActivityState.StandingUp: return "getting up";
                case AgentActivityState.GoingToAlarm: return "going for the alarm";
                case AgentActivityState.PullingAlarm: return "hitting the alarm";
                case AgentActivityState.Fleeing: return agent.IsComposed ? "walking out" : "running";
                default: return agent.ActivityState.ToString().ToLowerInvariant();
            }
        }
    }
}

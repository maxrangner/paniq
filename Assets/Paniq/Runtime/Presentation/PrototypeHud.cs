using System;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// The text at the top left: tick, fire, the head count, and what a door
    /// click will do and cost. Along the bottom, the influence the player has
    /// left and the cards they can spend it on. Tab toggles a plain table of
    /// everyone's traits and state.
    /// <para>
    /// What is on screen while the round runs is what the player is reading:
    /// the numbers, the buttons and the purse. Everything that only explains
    /// how to play -- which keys do what, what the marks over people's heads
    /// mean -- lives in <see cref="DrawPauseHelp"/> and appears only when the
    /// world is stopped, which is when somebody actually wants to read it.
    /// </para>
    /// </summary>
    internal static class PrototypeHud
    {
        private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarFill = new Color(0.3f, 0.75f, 1f, 0.9f);
        private static readonly Color CardPicked = new Color(0.25f, 0.55f, 0.85f, 0.95f);
        private static readonly Color CardAffordable = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color CardTooDear = new Color(0.25f, 0.1f, 0.1f, 0.7f);

        public static void Draw(
            RunSnapshot snapshot,
            ScenarioData scenario,
            SimulationId? hoveredDoor,
            DoorState hoveredState,
            bool hoveredIsJammed = false)
        {
            GUI.color = Color.white;
            string fireText;
            if (snapshot.FireActive)
            {
                fireText = $"FIRE  {snapshot.FireCells.Count} squares burning";
            }
            else if (scenario.Round.HazardWaitsForTrigger)
            {
                // Nothing is counting down: it waits for the player.
                fireText = snapshot.EventTriggered ? "FIRE STARTING" : "NO FIRE YET";
            }
            else
            {
                fireText = $"FIRE IN {Mathf.Max(0f, (scenario.Fire.ActivationTick - snapshot.Tick) / (float)Run.TicksPerSecond):0.00} s";
            }

            GUI.Label(new Rect(20f, 20f, 360f, 24f), $"Fire-reaction prototype  |  tick {snapshot.Tick}");
            GUI.Label(new Rect(20f, 44f, 480f, 24f),
                snapshot.AlarmsRinging ? $"{fireText}   |   ALARM RINGING" : fireText);
            GUI.Label(new Rect(20f, 68f, 900f, 24f),
                $"Calm {snapshot.CalmCount}   Scared {snapshot.ScaredCount} (frozen {snapshot.FrozenCount}, on fire {snapshot.BurningCount})   " +
                $"Down {snapshot.DownCount} (out cold {snapshot.UnconsciousCount})   Lost {snapshot.LostCount}   " +
                $"Escaped {snapshot.EscapedCount}   In a room with no fire {snapshot.ClearOfFireCount}");
            if (hoveredDoor.HasValue)
            {
                // A door with something wedged in it will not move however many
                // times you click, so say so rather than letting the click look
                // as though it did nothing. Same for a door they cannot pay for:
                // without this the click simply vanishes.
                int price = snapshot.CostOfDoorClick(hoveredState);
                bool affordable = snapshot.Influence >= price;
                string action = hoveredIsJammed ? "SOMETHING IS WEDGED IN IT - it will not open until that is shifted"
                    : hoveredState == DoorState.Broken ? "Broken down"
                    : !affordable ? $"NOT ENOUGH INFLUENCE - it costs {price}, and you have {snapshot.Influence}"
                    : hoveredState == DoorState.Locked ? $"Click to unlock ({price})"
                    : hoveredState == DoorState.Unlocked ? $"Click to open ({price})"
                    : $"Click to close ({price}, if nobody is in the doorway)";
                GUI.color = hoveredIsJammed || !affordable ? new Color(1f, 0.7f, 0.6f) : Color.white;
                GUI.Label(new Rect(20f, 92f, 700f, 24f), $"Door {hoveredDoor.Value.Value}: {action}");
                GUI.color = Color.white;
            }
        }

        /// <summary>Which cards are thrown at a patch of crowd rather than at a place in the building.</summary>
        private static bool IsAThrownCard(PlayerCommandType card)
        {
            switch (card)
            {
                case PlayerCommandType.PlayBeefcake:
                case PlayerCommandType.PlayCourage:
                case PlayerCommandType.PlayTerror:
                case PlayerCommandType.PlayBastard:
                case PlayerCommandType.PlayColdHeart:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The player's purse and their cards, along the bottom. A card they
        /// cannot afford is dimmed red and cannot be picked up; the one in their
        /// hand is highlighted, and the line above says what a click will do.
        /// </summary>
        public static void DrawCards(
            RunSnapshot snapshot, PlayerCommandType? selected, PlayerInput input, int peopleInTheCircle)
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
                $"Influence {snapshot.Influence}   (spent {snapshot.InfluenceSpent}, taken in {snapshot.InfluenceEarned})");

            // The hand. Empty at the start of every round: cards are not
            // bought, they are dealt by whoever the building kills, so an empty
            // bar is the game saying "nobody has died yet" rather than a
            // display that has not loaded.
            if (snapshot.Hand.Count == 0)
            {
                GUI.color = new Color(0.75f, 0.75f, 0.75f);
                GUI.Label(new Rect(20f, bottom - cardHeight + 7f, 700f, 22f),
                    "No cards. The dead deal them.");
                GUI.color = Color.white;
                return;
            }

            for (int i = 0; i < snapshot.Hand.Count; i++)
            {
                PlayerCommandType card = snapshot.Hand[i];
                int cost = snapshot.CostOf(card);
                bool affordable = snapshot.Influence >= cost;
                var area = new Rect(20f + i * (cardWidth + gap), bottom - cardHeight, cardWidth, cardHeight);
                GUI.color = selected == card ? CardPicked : affordable ? CardAffordable : CardTooDear;
                GUI.DrawTexture(area, Texture2D.whiteTexture);
                GUI.color = affordable ? Color.white : new Color(1f, 0.7f, 0.7f, 0.8f);
                GUI.Label(new Rect(area.x + 8f, area.y + 7f, area.width - 16f, 22f),
                    $"{i + 1}. {PlayerInput.NameOf(card)}  ({cost})");
            }

            // Only while a card is actually in hand: what it is waiting to be
            // aimed at, and what it is pointing at right now. With nothing
            // picked up there is nothing to say, and the line that used to sit
            // here explaining the number keys has moved to the pause screen.
            GUI.color = Color.white;
            if (selected == null)
            {
                return;
            }

            // A trait card is thrown at a patch and catches whoever is inside
            // it, so what the player needs to know is how many that is right
            // now. The circle on the floor says the same thing; this says it in
            // words, and says nought out loud, because a throw that catches
            // nobody is the one mistake that is free.
            string hint;
            if (IsAThrownCard(selected.Value))
            {
                int caught = peopleInTheCircle;
                hint = $"{PlayerInput.NameOf(selected.Value)}: " + (caught == 0
                    ? "nobody in the circle -- a throw that catches nobody is free"
                    : caught == 1 ? "1 person in the circle" : $"{caught} people in the circle");
            }
            else if (selected.Value == PlayerCommandType.BlastWall)
            {
                hint = $"TNT: click a wall  ({snapshot.BlastChargesRemaining} left)";
            }
            else
            {
                hint = $"{PlayerInput.NameOf(selected.Value)}: click a spot on the floor";
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(20f, bottom - cardHeight - gap - 44f, 900f, 22f), hint);
        }

        /// <summary>
        /// Everything that explains how to play, shown only while the world is
        /// stopped: what the marks over people's heads mean, and what every key
        /// and click does.
        /// <para>
        /// All of this used to be on screen the whole time -- two rows of key
        /// reminders across the top, a line under the cards, and the marks
        /// panel down the left -- which left very little of the office to look
        /// at. Pause is the moment somebody is reading rather than playing, so
        /// this is where it belongs.
        /// </para>
        /// </summary>
        public static void DrawPauseHelp(RunSnapshot snapshot)
        {
            const float rowHeight = 18f;
            const float width = 620f;
            var marks = new[]
            {
                (Colour: new Color(1f, 0.25f, 0.2f), Mark: "!", Means: "just noticed something"),
                (Colour: new Color(0.45f, 0.9f, 1f), Mark: ")))", Means: "shouting"),
                (Colour: new Color(1f, 0.85f, 0.3f), Mark: "?", Means: "what was that noise?"),
                (Colour: new Color(0.8f, 0.8f, 0.8f), Mark: "...", Means: "idling"),
                (Colour: new Color(0.7f, 0.85f, 1f), Mark: "*", Means: "frozen with fear"),
                (Colour: new Color(1f, 0.9f, 0.35f), Mark: "o o o", Means: "out cold"),
                (Colour: new Color(0.4f, 0.95f, 0.5f), Mark: "star", Means: "somebody is following them"),
                (Colour: new Color(1f, 0.55f, 0.15f), Mark: "[]", Means: "on fire"),
                (Colour: new Color(0.55f, 0.15f, 0.15f), Mark: "[]", Means: "lost")
            };

            var keys = new[]
            {
                ("1 - 6", "pick a card up, then click to play it"),
                ("Cards", "dealt by the dead, one each. Nobody dies, nobody deals"),
                ("Influence", "paid by the uproar, and by everyone who gets out"),
                ("Escape", "put the card back down (or right click)"),
                ("Click a door", $"red is locked. Unlock {snapshot.CostOfDoorClick(DoorState.Locked)}, " +
                                 $"open {snapshot.CostOfDoorClick(DoorState.Unlocked)}, " +
                                 $"close {snapshot.CostOfDoorClick(DoorState.Open)}"),
                ("W A S D", "move the camera"),
                ("Q E", "turn a quarter"),
                ("Wheel", "zoom"),
                ("Tab", "everyone's stats"),
                ("G", "the floor people can walk on"),
                ("Space", "start and stop the world")
            };

            int rows = Math.Max(marks.Length, keys.Length);
            float height = rowHeight * (rows + 2) + 16f;
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float left = area.x + 12f;
            float right = area.x + 270f;
            float top = area.y + 8f;
            GUI.Label(new Rect(left, top, 240f, rowHeight), "WHAT THE MARKS MEAN");
            GUI.Label(new Rect(right, top, 340f, rowHeight), "WHAT THE KEYS DO");

            float y = top + rowHeight * 1.5f;
            for (int i = 0; i < rows; i++)
            {
                if (i < marks.Length)
                {
                    GUI.color = marks[i].Colour;
                    GUI.Label(new Rect(left, y, 46f, rowHeight), marks[i].Mark);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(left + 50f, y, 200f, rowHeight), marks[i].Means);
                }

                if (i < keys.Length)
                {
                    GUI.Label(new Rect(right, y, 90f, rowHeight), keys[i].Item1);
                    GUI.Label(new Rect(right + 96f, y, width - 108f - 270f + 260f, rowHeight), keys[i].Item2);
                }

                y += rowHeight;
            }
        }

        /// <summary>
        /// One row per person, numbered like the labels over their heads, then
        /// <paramref name="footer"/>: which physics feel is in use, and so on.
        /// </summary>
        public static void DrawStats(RunSnapshot snapshot, string footer)
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
                AgentSnapshot agent = snapshot.Agents[i];
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

        private static string StateText(AgentSnapshot agent)
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

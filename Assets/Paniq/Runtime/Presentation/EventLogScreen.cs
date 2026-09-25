using System.Collections.Generic;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// What happened, read back after the round. The run already keeps a full
    /// causal log of everything it did; until now nothing ever showed it to
    /// anybody, so a round that went wrong left the player guessing.
    /// <para>
    /// It opens on the story: the fire taking hold, every shout, every door
    /// forced or shut, everybody who caught fire or got out. The background
    /// chatter -- the fire creeping one square at a time, an extinguisher
    /// hissing -- is folded into one line apiece, because printed in full it
    /// runs to thousands of lines and buries the rest. One button unfolds the
    /// lot for anybody who wants it.
    /// </para>
    /// </summary>
    internal sealed class EventLogScreen
    {
        private static readonly Color Back = new Color(0.04f, 0.04f, 0.06f, 0.95f);
        private static readonly Color Chatter = new Color(0.62f, 0.66f, 0.74f);
        private static readonly Color Player = new Color(0.55f, 0.8f, 1f);
        private static readonly Color Bad = new Color(1f, 0.55f, 0.45f);
        private static readonly Color Good = new Color(0.5f, 0.95f, 0.6f);

        private const float RowHeight = 19f;
        private const float Margin = 40f;

        private Vector2 scroll;
        private bool showEverything;

        /// <summary>The lines as they stand, and what they were built from.</summary>
        private readonly List<Line> lines = new List<Line>();
        private int builtFromCount = -1;
        private bool builtShowingEverything;

        private readonly struct Line
        {
            public Line(string text, Color colour)
            {
                Text = text;
                Colour = colour;
            }

            public string Text { get; }
            public Color Colour { get; }
        }

        /// <summary>Whether the log is covering the screen.</summary>
        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            scroll = Vector2.zero;
            builtFromCount = -1;
        }

        public void Close() => IsOpen = false;

        /// <summary>Drawn last of all, over the end card, so nothing shows through it.</summary>
        public void Draw(RunSnapshot snapshot)
        {
            if (!IsOpen || snapshot == null)
            {
                return;
            }

            Rebuild(snapshot);

            var area = new Rect(Margin, Margin, Screen.width - Margin * 2f, Screen.height - Margin * 2f);
            GUI.color = Back;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(area.x + 16f, area.y + 12f, 500f, 24f),
                $"What happened  |  {lines.Count} lines of {snapshot.Events.Count} events");

            if (GUI.Button(new Rect(area.xMax - 330f, area.y + 10f, 180f, 26f),
                    showEverything ? "Just the story" : "Show everything"))
            {
                showEverything = !showEverything;
                scroll = Vector2.zero;
            }

            if (GUI.Button(new Rect(area.xMax - 130f, area.y + 10f, 110f, 26f), "Back (Esc)"))
            {
                Close();
                return;
            }

            var view = new Rect(area.x + 16f, area.y + 48f, area.width - 32f, area.height - 64f);
            var content = new Rect(0f, 0f, view.width - 24f, lines.Count * RowHeight + 8f);
            scroll = GUI.BeginScrollView(view, scroll, content);
            for (int i = 0; i < lines.Count; i++)
            {
                GUI.color = lines[i].Colour;
                GUI.Label(new Rect(4f, i * RowHeight, content.width - 8f, RowHeight), lines[i].Text);
            }

            GUI.EndScrollView();
            GUI.color = Color.white;
        }

        /// <summary>
        /// Builds the lines once, and again only when the log or the fold has
        /// changed. A finished round never grows, so in practice this runs once
        /// per opening rather than once per frame.
        /// </summary>
        private void Rebuild(RunSnapshot snapshot)
        {
            if (builtFromCount == snapshot.Events.Count && builtShowingEverything == showEverything)
            {
                return;
            }

            builtFromCount = snapshot.Events.Count;
            builtShowingEverything = showEverything;
            lines.Clear();

            var story = new EventStory(snapshot);
            int runLength = 0;
            CausalEventType runType = default;
            int runStartTick = 0;
            string runText = null;

            for (int i = 0; i < snapshot.Events.Count; i++)
            {
                CausalEvent record = snapshot.Events[i];
                bool chatter = !showEverything && EventStory.IsBackground(record.EventType);
                if (chatter && runLength > 0 && record.EventType == runType)
                {
                    runLength++;
                    continue;
                }

                FlushRun(ref runLength, runType, runStartTick, runText);
                if (chatter)
                {
                    runLength = 1;
                    runType = record.EventType;
                    runStartTick = record.Tick;
                    runText = story.Describe(record);
                    continue;
                }

                lines.Add(new Line(
                    $"{EventStory.TimeOf(record.Tick),6}   {story.Describe(record)}",
                    ColourOf(record.EventType)));
            }

            FlushRun(ref runLength, runType, runStartTick, runText);
        }

        /// <summary>
        /// Closes off a run of the same background event: one of them is a line
        /// of its own, a stretch of them is one line saying how many.
        /// </summary>
        private void FlushRun(ref int runLength, CausalEventType type, int startTick, string text)
        {
            if (runLength <= 0)
            {
                return;
            }

            string line = runLength == 1 ? text : $"{text}, {runLength} times";
            lines.Add(new Line($"{EventStory.TimeOf(startTick),6}   {line}", Chatter));
            runLength = 0;
        }

        /// <summary>
        /// Three colours and no more: what the player did, what went wrong, and
        /// what went right. Everything else is plain text.
        /// </summary>
        private static Color ColourOf(CausalEventType type)
        {
            switch (type)
            {
                case CausalEventType.PowerBeefcake:
                case CausalEventType.PowerCourage:
                case CausalEventType.PowerTerror:
                case CausalEventType.PowerBastard:
                case CausalEventType.PowerColdHeart:
                case CausalEventType.PowerSpawnedFire:
                case CausalEventType.PowerSpawnedExtinguisher:
                case CausalEventType.PowerBlastedWall:
                case CausalEventType.PowerPoppedFuseBox:
                case CausalEventType.PowerPulledAlarm:
                case CausalEventType.PowerStickTogether:
                case CausalEventType.RoundEventTriggered:
                case CausalEventType.DoorUnlocked:
                case CausalEventType.CardDealt:
                    return Player;

                case CausalEventType.AgentLost:
                case CausalEventType.AgentCaughtFire:
                case CausalEventType.AgentPassedOut:
                case CausalEventType.AgentCrushed:
                case CausalEventType.DoorBurntThrough:
                case CausalEventType.DoorLocked:
                case CausalEventType.DoorBlocked:
                    return Bad;

                case CausalEventType.AgentEscaped:
                case CausalEventType.AgentSurvived:
                case CausalEventType.AgentRescued:
                case CausalEventType.AgentDoused:
                case CausalEventType.AgentFoundTheWayOut:
                case CausalEventType.RoundEnded:
                    return Good;

                default:
                    return Color.white;
            }
        }
    }
}

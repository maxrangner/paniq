using System.Collections.Generic;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// Everything the round itself puts on screen: the card before it starts,
    /// the running score and the buttons along the top, and the card at the
    /// end.
    /// <para>
    /// All of it observes. It reads the snapshot and the runner and it presses
    /// the runner's own buttons; it never decides anything about the run.
    /// </para>
    /// </summary>
    internal sealed class RoundScreens
    {
        private const float CardWidth = 460f;
        private static readonly Color CardBack = new Color(0f, 0f, 0f, 0.86f);
        private static readonly Color Cleared = new Color(0.45f, 0.95f, 0.55f);
        private static readonly Color NotCleared = new Color(1f, 0.6f, 0.45f);
        private static readonly Color TriggerReady = new Color(0.75f, 0.2f, 0.15f, 0.95f);
        private static readonly Color Spent = new Color(0.18f, 0.18f, 0.2f, 0.8f);
        private static readonly Color CrowdPanicked = new Color(0.85f, 0.35f, 0.2f, 0.95f);
        private static readonly Color CrowdCalm = new Color(0.25f, 0.55f, 0.8f, 0.95f);
        private static readonly Color CurrentLevel = new Color(0.55f, 0.75f, 1f);

        private readonly RunDriver runner;

        /// <summary>What the player has typed in the seed box, kept between frames.</summary>
        private string seedText;

        /// <summary>Whether this round's result has already gone into the high score.</summary>
        private bool resultRecorded;

        /// <summary>Whether this round beat the best ever, worked out once when it ended.</summary>
        private bool beatTheBest;

        public RoundScreens(RunDriver runner)
        {
            this.runner = runner;
            seedText = runner.Seed.ToString();
        }

        /// <summary>True while a card is covering the screen, so clicks in the world are ignored.</summary>
        public bool CardIsUp => runner.IsWaitingToStart || runner.Simulation.Phase == RoundPhase.Over;

        private string LevelId => runner.Level != null ? runner.Level.LevelId : "prototype_fire_1_fl_small";
        private string LevelName => runner.Level != null ? runner.Level.DisplayName : "The Office";
        private bool OffersCrowdSwitch => runner.Level != null && runner.Level.OffersCrowdSwitch;
        private bool TriggerStartsAHazard => runner.Level == null || runner.Level.TriggerStartsAHazard;

        /// <summary>The line under PLAY: what sets this level off.</summary>
        private string HowItStarts
        {
            get
            {
                if (OffersCrowdSwitch && TriggerStartsAHazard)
                {
                    return "Calm until you press Crowd: panicked, or Trigger event for a fire.";
                }

                if (OffersCrowdSwitch)
                {
                    return "Calm until you press Crowd: panicked. Nothing burns here.";
                }

                if (runner.Level != null && runner.Level.HowItStarts.Length > 0)
                {
                    return runner.Level.HowItStarts;
                }

                return "The office is calm until you press Trigger event yourself.";
            }
        }

        /// <summary>Reset and Pause in the top-right corner, and Trigger event bottom centre. (The running score is drawn by the HUD, packed under the alarm band.)</summary>
        public void DrawStrip(RunSnapshot snapshot)
        {
            // Back to the start card, quick, from anywhere in the round or
            // from the end card (the owner asked, 2026-09-24). The seed is
            // kept, so the same day can be played again from the card, or a
            // new one typed in. It says Reset (2026-09-25), because that is
            // what it does.
            if (!runner.IsWaitingToStart)
            {
                var reset = new Rect(Screen.width - 130f, 36f, 110f, 30f);
                HudHitTest.Claim(reset);
                if (GUI.Button(reset, "Reset"))
                {
                    LevelLoader.BackToTheStart(runner.Seed);
                    return;
                }
            }

            // The trigger sits bottom centre and goes the moment it is pressed
            // (the owner asked, 2026-09-25): nothing left to press means the
            // fire has been set going. Kept clear of the hand on a narrow
            // screen.
            // Not on a level with nothing for it to start (2026-10-01), and
            // since the crowd switch may have begun the round without it, it
            // stays until the fire is actually asked for.
            float bottomLeft = Mathf.Max(Screen.width * 0.5f - 110f, 460f);
            bool triggerShown = TriggerStartsAHazard && !snapshot.HazardRequested && !snapshot.RoundIsOver;
            if (triggerShown)
            {
                var trigger = new Rect(bottomLeft, Screen.height - 64f, 220f, 36f);
                HudHitTest.Claim(trigger);
                GUI.backgroundColor = TriggerReady;
                if (GUI.Button(trigger, "Trigger event"))
                {
                    runner.QueueTriggerEvent();
                }

                GUI.backgroundColor = Color.white;
            }

            // The crowd switch (2026-10-01, the test levels): one button that
            // reads what the crowd is being held at and flicks it the other
            // way. Beside the trigger, or where the trigger would be.
            if (OffersCrowdSwitch && !runner.IsWaitingToStart && !snapshot.RoundIsOver)
            {
                float x = triggerShown ? bottomLeft - 230f : bottomLeft;
                var crowd = new Rect(x, Screen.height - 64f, 220f, 36f);
                HudHitTest.Claim(crowd);
                GUI.backgroundColor = snapshot.CrowdHeldPanicked ? CrowdPanicked : CrowdCalm;
                if (GUI.Button(crowd, snapshot.CrowdHeldPanicked ? "Crowd: panicked" : "Crowd: calm"))
                {
                    if (snapshot.CrowdHeldPanicked)
                    {
                        runner.QueueCrowdCalm();
                    }
                    else
                    {
                        runner.QueueCrowdPanicked();
                    }
                }

                GUI.backgroundColor = Color.white;
            }

            if (snapshot.RoundIsOver)
            {
                return;
            }

            // Pause, under Reset. The pause screen itself says PAUSED.
            var pause = new Rect(Screen.width - 130f, 72f, 110f, 30f);
            HudHitTest.Claim(pause);
            if (GUI.Button(pause, runner.IsPaused ? "Resume (Space)" : "Pause (Space)"))
            {
                runner.TogglePause();
            }
        }
        /// <summary>The card before the round: the level, the target, the best so far, and the seed.</summary>
        public void DrawStartCard(RunSnapshot snapshot)
        {
            IReadOnlyList<LevelDefinition> levels = runner.Levels;
            bool levelRow = levels.Count > 1;
            const int perRow = 3;
            int levelRows = levelRow ? (levels.Count + perRow - 1) / perRow : 0;
            string brief = runner.Level != null ? runner.Level.Brief : "";
            var briefStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            float briefHeight = brief.Length > 0 ? briefStyle.CalcHeight(new GUIContent(brief), CardWidth - 48f) + 8f : 0f;
            float height = 250f + levelRows * 34f + (levelRow ? 6f : 0f) + briefHeight;
            Rect card = CentredCard(height);
            float x = card.x + 24f;
            float y = card.y + 20f;
            float width = card.width - 48f;

            GUI.Label(new Rect(x, y, width, 26f), LevelName.ToUpperInvariant());
            y += 30f;

            // What happens here and what the hand can do about it (2026-10-03).
            if (brief.Length > 0)
            {
                GUI.color = new Color(0.86f, 0.88f, 0.92f);
                GUI.Label(new Rect(x, y, width, briefHeight), brief, briefStyle);
                GUI.color = Color.white;
                y += briefHeight;
            }

            // The level row (2026-10-01): every level the runner offers, the
            // one on screen lit. Another one reloads the scene into it,
            // behind its own start card. Three to a row, by their short
            // names (2026-10-03: five in one row cut every name off).
            if (levelRow)
            {
                float gap = 6f;
                float each = (width - gap * (perRow - 1)) / perRow;
                for (int i = 0; i < levels.Count; i++)
                {
                    bool current = ReferenceEquals(levels[i], runner.Level);
                    GUI.backgroundColor = current ? CurrentLevel : Color.white;
                    var button = new Rect(x + i % perRow * (each + gap), y + i / perRow * 34f, each, 28f);
                    if (GUI.Button(button, levels[i].ShortName) && !current)
                    {
                        LevelLoader.SwitchLevel(levels[i].LevelId);
                        GUI.backgroundColor = Color.white;
                        return;
                    }
                }

                GUI.backgroundColor = Color.white;
                y += levelRows * 34f + 6f;
            }
            GUI.Label(new Rect(x, y, width, 22f),
                $"Save {snapshot.TargetSavedCount} of {snapshot.CrowdSize} people to clear it.");
            y += 24f;
            DrawBestSoFar(x, y, width);
            y += 32f;

            y = DrawSeedRow(x, y, width);
            y += 12f;

            if (GUI.Button(new Rect(x, y, width, 38f), "PLAY"))
            {
                Play();
            }

            y += 44f;
            GUI.color = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, width, 22f), HowItStarts);
            GUI.color = Color.white;
        }

        /// <summary>
        /// The card at the end: what the round came to, the margin over the
        /// same seed left alone (the line the round is judged by, the owner's
        /// choice, 2026-09-29), three lines of why, a way to read the whole
        /// round back, and two ways to play it again.
        /// </summary>
        public void DrawEndCard(RunSnapshot snapshot, int? leftAloneSavedCount = null, bool leftAloneStillWorking = false,
            IReadOnlyList<string> retelling = null, IReadOnlyList<string> byRoom = null)
        {
            RecordResultOnce(snapshot);
            RecordMarginOnce(snapshot, leftAloneSavedCount);

            int lines = retelling?.Count ?? 0;
            int roomLines = byRoom?.Count ?? 0;
            float height = 372f + 22f * lines + 22f * roomLines + (roomLines > 0 ? 6f : 0f);
            Rect card = CentredCard(height);
            float x = card.x + 24f;
            float y = card.y + 20f;
            float width = card.width - 48f;

            GUI.Label(new Rect(x, y, width, 26f),
                $"You saved {snapshot.SavedCount} of {snapshot.CrowdSize} — {snapshot.SavedPercent}%");
            y += 30f;

            // What the same seed came to with nobody at the controls
            // (2026-09-27), and the difference the player made (2026-09-29):
            // the round is you against the building.
            if (leftAloneSavedCount.HasValue)
            {
                int margin = snapshot.SavedCount - leftAloneSavedCount.Value;
                GUI.color = margin > 0 ? Cleared : margin < 0 ? NotCleared : new Color(0.75f, 0.78f, 0.82f);
                GUI.Label(new Rect(x, y, width, 22f), margin > 0
                    ? $"Left alone, {leftAloneSavedCount.Value} would have lived. You made the difference for {margin}."
                    : margin < 0
                        ? $"Left alone, {leftAloneSavedCount.Value} would have lived: {-margin} fewer lived with you playing."
                        : $"Left alone, {leftAloneSavedCount.Value} would have lived: the same as with you playing.");
            }
            else
            {
                GUI.color = new Color(0.75f, 0.78f, 0.82f);
                GUI.Label(new Rect(x, y, width, 22f), leftAloneStillWorking ? "Left alone: still working it out…" : "");
            }

            GUI.color = Color.white;
            y += 24f;

            GUI.color = snapshot.Cleared ? Cleared : new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, width, 22f), snapshot.Cleared
                ? $"Cleared — {snapshot.TargetSavedPercent}% was needed"
                : $"Not cleared — {snapshot.TargetSavedPercent}% was needed");
            GUI.color = Color.white;
            y += 24f;

            GUI.Label(new Rect(x, y, width, 22f),
                $"{snapshot.EscapedCount} got out, {snapshot.SurvivedCount} sat it out somewhere safe, " +
                $"{snapshot.LostCount} did not make it.");
            y += 26f;

            // By the room they began in, against the same seed left alone
            // (2026-10-03): where the round was won and lost.
            if (roomLines > 0)
            {
                GUI.color = new Color(0.95f, 0.88f, 0.65f);
                for (int i = 0; i < roomLines; i++)
                {
                    GUI.Label(new Rect(x, y, width, 22f), byRoom[i]);
                    y += 22f;
                }

                GUI.color = Color.white;
                y += 6f;
            }

            // Three lines of why (2026-09-29): the smallest form of the
            // retelling the vision asks for.
            GUI.color = new Color(0.82f, 0.84f, 0.88f);
            for (int i = 0; i < lines; i++)
            {
                GUI.Label(new Rect(x, y, width, 22f), retelling[i]);
                y += 22f;
            }

            GUI.color = Color.white;
            y += 4f;

            if (beatTheBestMargin)
            {
                GUI.color = Cleared;
                GUI.Label(new Rect(x, y, width, 22f), $"Your best margin over the building yet: {bestMarginThisRound}.");
                GUI.color = Color.white;
            }
            else if (beatTheBest)
            {
                GUI.color = Cleared;
                GUI.Label(new Rect(x, y, width, 22f), $"A new best: {snapshot.SavedPercent}%.");
                GUI.color = Color.white;
            }
            else
            {
                DrawBestSoFar(x, y, width);
            }

            y += 32f;
            y = DrawSeedRow(x, y, width);
            y += 12f;

            if (GUI.Button(new Rect(x, y, width, 32f), "What happened"))
            {
                WantsTheLog = true;
            }

            y += 40f;
            float half = (width - 10f) * 0.5f;
            if (GUI.Button(new Rect(x, y, half, 36f), "Play again (same seed)"))
            {
                LevelLoader.PlayAgain();
            }

            if (GUI.Button(new Rect(x + half + 10f, y, half, 36f), "Play the seed above"))
            {
                Play();
            }
        }

        /// <summary>
        /// Set when the player asks to read the round back. Whoever is drawing
        /// takes it and clears it, so the button is a request rather than the
        /// screens owning a second screen.
        /// </summary>
        public bool WantsTheLog { get; set; }

        private void DrawBestSoFar(float x, float y, float width)
        {
            int best = LevelSession.BestPercentFor(LevelId);
            int? margin = LevelSession.BestMarginFor(LevelId);
            string overTheBuilding = margin.HasValue ? $"; best margin over the building: {margin.Value}" : "";
            GUI.color = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, width, 22f),
                best > 0 ? $"Best so far: {best}%{overTheBuilding}" : "Never played this one before.");
            GUI.color = Color.white;
        }

        /// <summary>The seed box and its Random button. Returns the y below it.</summary>
        private float DrawSeedRow(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y + 4f, 44f, 22f), "Seed");
            seedText = GUI.TextField(new Rect(x + 48f, y, width - 48f - 96f, 26f), seedText, 20);
            if (GUI.Button(new Rect(x + width - 90f, y, 90f, 26f), "Random"))
            {
                seedText = LevelSession.RandomSeed().ToString();
            }

            y += 30f;
            GUI.color = new Color(0.66f, 0.7f, 0.74f);
            GUI.Label(new Rect(x, y, width, 20f),
                "The same seed is the same day: the same people, the fire in the same place.");
            GUI.color = Color.white;
            return y + 22f;
        }

        /// <summary>Starts the level on whatever is in the seed box, or on the level's own seed if it makes no sense.</summary>
        private void Play()
        {
            if (ulong.TryParse(seedText, out ulong seed) && seed != 0UL && seed != runner.Seed)
            {
                LevelLoader.PlayWithSeed(seed);
                return;
            }

            if (runner.IsWaitingToStart)
            {
                runner.BeginPlaying();
                return;
            }

            LevelLoader.PlayAgain();
        }

        /// <summary>The high score is written once, the first frame the end card is drawn.</summary>
        private void RecordResultOnce(RunSnapshot snapshot)
        {
            if (resultRecorded)
            {
                return;
            }

            resultRecorded = true;
            beatTheBest = LevelSession.RecordResult(LevelId, snapshot.SavedPercent);
        }

        /// <summary>Whether this round's margin over "left alone" has gone into the record; it can only be judged once the hands-off round is done.</summary>
        private bool marginRecorded;
        private bool beatTheBestMargin;
        private int bestMarginThisRound;

        private void RecordMarginOnce(RunSnapshot snapshot, int? leftAloneSavedCount)
        {
            if (marginRecorded || !leftAloneSavedCount.HasValue)
            {
                return;
            }

            marginRecorded = true;
            bestMarginThisRound = snapshot.SavedCount - leftAloneSavedCount.Value;
            beatTheBestMargin = LevelSession.RecordMargin(LevelId, bestMarginThisRound);
        }

        private static Rect CentredCard(float height)
        {
            var card = new Rect((Screen.width - CardWidth) * 0.5f, (Screen.height - height) * 0.5f, CardWidth, height);
            GUI.color = CardBack;
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            return card;
        }
    }
}

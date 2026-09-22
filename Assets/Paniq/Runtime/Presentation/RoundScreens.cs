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
        private static readonly Color StripBack = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Cleared = new Color(0.45f, 0.95f, 0.55f);
        private static readonly Color NotCleared = new Color(1f, 0.6f, 0.45f);
        private static readonly Color TriggerReady = new Color(0.75f, 0.2f, 0.15f, 0.95f);
        private static readonly Color Spent = new Color(0.18f, 0.18f, 0.2f, 0.8f);

        private readonly FireReactionRunner runner;

        /// <summary>What the player has typed in the seed box, kept between frames.</summary>
        private string seedText;

        /// <summary>Whether this round's result has already gone into the high score.</summary>
        private bool resultRecorded;

        /// <summary>Whether this round beat the best ever, worked out once when it ended.</summary>
        private bool beatTheBest;

        public RoundScreens(FireReactionRunner runner)
        {
            this.runner = runner;
            seedText = runner.Seed.ToString();
        }

        /// <summary>True while a card is covering the screen, so clicks in the world are ignored.</summary>
        public bool CardIsUp => runner.IsWaitingToStart || runner.Simulation.Phase == RoundPhase.Over;

        private string LevelId => runner.Level != null ? runner.Level.LevelId : "the-office";
        private string LevelName => runner.Level != null ? runner.Level.DisplayName : "The Office";

        /// <summary>The running score, the trigger and the pause button, along the top.</summary>
        public void DrawStrip(FireReactionSnapshot snapshot)
        {
            var strip = new Rect(20f, 140f, 720f, 30f);
            GUI.color = StripBack;
            GUI.DrawTexture(strip, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(strip.x + 10f, strip.y + 5f, strip.width - 20f, 22f),
                $"Saved {snapshot.SavedCount}   Lost {snapshot.LostCount}   Still inside {snapshot.RemainingCount}" +
                $"      Need {snapshot.TargetSavedCount} of {snapshot.CrowdSize} to clear      Seed {runner.Seed}");

            float y = strip.yMax + 8f;
            if (!snapshot.EventTriggered)
            {
                GUI.backgroundColor = TriggerReady;
                if (GUI.Button(new Rect(20f, y, 200f, 30f), "Trigger event"))
                {
                    runner.QueueTriggerEvent();
                }

                GUI.backgroundColor = Color.white;
                GUI.Label(new Rect(228f, y + 5f, 460f, 22f), "Nothing is wrong yet. This is what sets it off.");
            }
            else
            {
                GUI.backgroundColor = Spent;
                GUI.Button(new Rect(20f, y, 200f, 30f), "Event triggered");
                GUI.backgroundColor = Color.white;
            }

            if (snapshot.RoundIsOver)
            {
                return;
            }

            if (GUI.Button(new Rect(228f + (snapshot.EventTriggered ? 0f : 470f), y, 130f, 30f),
                    runner.IsPaused ? "Resume (Space)" : "Pause (Space)"))
            {
                runner.TogglePause();
            }

            if (runner.IsPaused)
            {
                GUI.color = new Color(1f, 0.95f, 0.5f);
                GUI.Label(new Rect(20f, y + 36f, 720f, 22f),
                    "PAUSED — look around all you like. Nothing can be played while the world is stopped.");
                GUI.color = Color.white;
            }
        }

        /// <summary>The card before the round: the level, the target, the best so far, and the seed.</summary>
        public void DrawStartCard(FireReactionSnapshot snapshot)
        {
            const float height = 250f;
            Rect card = CentredCard(height);
            float x = card.x + 24f;
            float y = card.y + 20f;
            float width = card.width - 48f;

            GUI.Label(new Rect(x, y, width, 26f), LevelName.ToUpperInvariant());
            y += 30f;
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
            GUI.Label(new Rect(x, y, width, 22f),
                "The office is calm until you press Trigger event yourself.");
            GUI.color = Color.white;
        }

        /// <summary>The card at the end: what the round came to, and two ways to play it again.</summary>
        public void DrawEndCard(FireReactionSnapshot snapshot)
        {
            RecordResultOnce(snapshot);

            const float height = 268f;
            Rect card = CentredCard(height);
            float x = card.x + 24f;
            float y = card.y + 20f;
            float width = card.width - 48f;

            GUI.Label(new Rect(x, y, width, 26f),
                $"You saved {snapshot.SavedCount} of {snapshot.CrowdSize} — {snapshot.SavedPercent}%");
            y += 30f;

            GUI.color = snapshot.Cleared ? Cleared : NotCleared;
            GUI.Label(new Rect(x, y, width, 22f), snapshot.Cleared
                ? $"Cleared — {snapshot.TargetSavedPercent}% was needed"
                : $"Not cleared — {snapshot.TargetSavedPercent}% was needed");
            GUI.color = Color.white;
            y += 24f;

            GUI.Label(new Rect(x, y, width, 22f),
                $"{snapshot.EscapedCount} got out, {snapshot.SurvivedCount} sat it out somewhere safe, " +
                $"{snapshot.LostCount} did not make it.");
            y += 26f;

            if (beatTheBest)
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

        private void DrawBestSoFar(float x, float y, float width)
        {
            int best = LevelSession.BestPercentFor(LevelId);
            GUI.color = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, width, 22f),
                best > 0 ? $"Best so far: {best}%" : "Never played this one before.");
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
                "The same seed is the same day: the fire starts in the same place.");
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
        private void RecordResultOnce(FireReactionSnapshot snapshot)
        {
            if (resultRecorded)
            {
                return;
            }

            resultRecorded = true;
            beatTheBest = LevelSession.RecordResult(LevelId, snapshot.SavedPercent);
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

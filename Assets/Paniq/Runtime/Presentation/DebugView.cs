using System.Globalization;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// What the screen shows besides the building and the people, and the
    /// panel Tab opens to switch it (2026-09-30, the owner: "Tab-key opens
    /// debug settings. Toggles for Remove vision cones, agent numbers,
    /// icons"). Everything starts as it always looked -- cones, numbers and
    /// marks on, the traits table and the walkable floor off -- and the
    /// switches last while the game runs, Reset included, but are never saved:
    /// every press of Play starts from the same picture.
    /// <para>
    /// Presentation only, except the hand's two dials (2026-09-30, the owner:
    /// "can we put general attraction as a slider in debug with a print out
    /// number so I can find the sweetspot and later hardcode it?", and later
    /// the same day, asked which feelings they tune most: "influence strength
    /// and influence area"): the presentation sends each to the run as a
    /// command, so a replay replays it.
    /// </para>
    /// </summary>
    internal sealed class DebugView
    {
        private const float Width = 250f;
        private const float RowHeight = 22f;

        /// <summary>The fan on the floor in front of each person.</summary>
        public bool VisionCones = true;

        /// <summary>The small number beside each head, matching the traits table.</summary>
        public bool Numbers = true;

        /// <summary>The marks over heads: "!", "?", the snowflake, the stars, the leader's star, the hand, and the rest.</summary>
        public bool Marks = true;

        /// <summary>The table of everyone's traits and what they are doing, which Tab used to open on its own.</summary>
        public bool Stats;

        /// <summary>The floor painted wherever a person could stand. G flips it too.</summary>
        public bool WalkableFloor;

        /// <summary>Whether the panel is on screen.</summary>
        public bool PanelOpen;

        /// <summary>
        /// How strongly everybody feels the hand, in percent of the level's
        /// own: the slider, from nothing to three times as strong, in tens.
        /// Kept through Reset, back to the level's own at every Play.
        /// </summary>
        public int HandStrengthPercent = 100;

        /// <summary>The slider's range and step.</summary>
        public const int HandStrengthMaximum = 300;
        public const int HandStrengthStep = 10;

        /// <summary>
        /// How far the hand is felt, in millimetres of walk: the slider, from
        /// two metres (a huddle round the hand) to twenty-four (the length of
        /// the building, walls allowing), in half metres. Kept through Reset,
        /// back to the level's own at every Play.
        /// </summary>
        public int HandReachMillimetres = 12000;

        /// <summary>The slider's range and step.</summary>
        public const int HandReachMinimum = 2000;
        public const int HandReachMaximum = 24000;
        public const int HandReachStep = 500;

        /// <summary>
        /// The level's own values, learnt from the first round seen, so the
        /// "Level's own" button has something to put the dials back to and
        /// the labels can say when a dial is off them.
        /// </summary>
        public int LevelStrengthPercent = 100;
        public int LevelReachMillimetres = 12000;

        /// <summary>
        /// Draws the panel when it is open, top right under Reset and Pause,
        /// and returns the height below which anything else in that corner
        /// should start.
        /// </summary>
        public float Draw(float top, RunSnapshot snapshot = null)
        {
            if (!PanelOpen)
            {
                return top;
            }

            // Five switches, a heading, two rows for each of the hand's two
            // dials, and the button that puts them back.
            const int rows = 5 + 1 + 2 + 2 + 1;
            float height = RowHeight * rows + 14f;
            var area = new Rect(Screen.width - Width - 20f, top, Width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            HudHitTest.Claim(area);

            float x = area.x + 10f;
            float y = area.y + 6f;
            float wide = Width - 20f;
            GUI.Label(new Rect(x, y, wide, RowHeight), "SHOW  (Tab closes)");
            y += RowHeight;
            VisionCones = GUI.Toggle(new Rect(x, y, wide, RowHeight), VisionCones, " Vision cones");
            y += RowHeight;
            Numbers = GUI.Toggle(new Rect(x, y, wide, RowHeight), Numbers, " Numbers over heads");
            y += RowHeight;
            Marks = GUI.Toggle(new Rect(x, y, wide, RowHeight), Marks, " Marks over heads");
            y += RowHeight;
            Stats = GUI.Toggle(new Rect(x, y, wide, RowHeight), Stats, " Everyone's stats");
            y += RowHeight;
            WalkableFloor = GUI.Toggle(new Rect(x, y, wide, RowHeight), WalkableFloor, " Walkable floor (G)");
            y += RowHeight;

            // The strength: how many are doing what the hand asks says whether
            // it is strong enough.
            GUI.Label(new Rect(x, y, wide, RowHeight),
                $"Hand strength {HandStrengthPercent}%   answering now: {Answering(snapshot)}");
            y += RowHeight;
            float slid = GUI.HorizontalSlider(new Rect(x, y + 5f, wide, RowHeight), HandStrengthPercent, 0f,
                HandStrengthMaximum);
            HandStrengthPercent = Mathf.RoundToInt(slid / HandStrengthStep) * HandStrengthStep;
            y += RowHeight;

            // The reach: how many feel the hand at all says whether it goes
            // far enough. A walk, not a circle, so no ring is drawn for it: a
            // ring would reach through walls the hand does not.
            GUI.Label(new Rect(x, y, wide, RowHeight),
                $"Hand reach {Metres(HandReachMillimetres)} m   feeling it: {Feeling(snapshot)}");
            y += RowHeight;
            float reached = GUI.HorizontalSlider(new Rect(x, y + 5f, wide, RowHeight), HandReachMillimetres,
                HandReachMinimum, HandReachMaximum);
            HandReachMillimetres = Mathf.RoundToInt(reached / HandReachStep) * HandReachStep;
            y += RowHeight;

            // Back to what the level says, in one click, so a dial left
            // somewhere odd is never a mystery.
            bool offTheLevel = HandStrengthPercent != LevelStrengthPercent || HandReachMillimetres != LevelReachMillimetres;
            GUI.enabled = offTheLevel;
            if (GUI.Button(new Rect(x, y + 2f, wide, RowHeight - 4f),
                    $"Level's own ({LevelStrengthPercent}%, {Metres(LevelReachMillimetres)} m)"))
            {
                HandStrengthPercent = LevelStrengthPercent;
                HandReachMillimetres = LevelReachMillimetres;
            }

            GUI.enabled = true;
            return area.yMax + 8f;
        }

        /// <summary>Millimetres as metres for a label: "12", "7.5".</summary>
        public static string Metres(int millimetres) =>
            (millimetres / 1000f).ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>How many people are doing what the hand asks right now: the number that says whether it is strong enough.</summary>
        private static int Answering(RunSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                if (snapshot.Agents[i].ActingForTheHand)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// How many people feel the hand at all right now, answering it or
        /// not: the number that says whether the reach is enough. The run
        /// lists a pull for everybody who feels the place or keeps its ask.
        /// </summary>
        private static int Feeling(RunSnapshot snapshot) => snapshot == null ? 0 : snapshot.InfluencePulls.Count;
    }
}

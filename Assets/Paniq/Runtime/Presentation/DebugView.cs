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
    /// Presentation only, except the hand strength (2026-09-30, the owner:
    /// "can we put general attraction as a slider in debug with a print out
    /// number so I can find the sweetspot and later hardcode it?"): the
    /// presentation sends it to the run as a command, so a replay replays it.
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
        /// Kept through Reset, back to 100 at every Play.
        /// </summary>
        public int HandStrengthPercent = 100;

        /// <summary>The slider's range and step.</summary>
        public const int HandStrengthMaximum = 300;
        public const int HandStrengthStep = 10;

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

            // Five switches, a heading, and the hand's two rows.
            const int rows = 5 + 1 + 2;
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
            GUI.Label(new Rect(x, y, wide, RowHeight),
                $"Hand strength {HandStrengthPercent}%   answering now: {Answering(snapshot)}");
            y += RowHeight;
            float slid = GUI.HorizontalSlider(new Rect(x, y + 5f, wide, RowHeight), HandStrengthPercent, 0f,
                HandStrengthMaximum);
            HandStrengthPercent = Mathf.RoundToInt(slid / HandStrengthStep) * HandStrengthStep;
            return area.yMax + 8f;
        }

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
    }
}

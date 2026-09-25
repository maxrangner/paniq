using System.Collections.Generic;
using UnityEngine;

namespace Paniq.Presentation
{
    /// <summary>
    /// Which parts of the screen the HUD is covering, so a click on a card or
    /// a button is never also a click on the door or the floor behind it.
    /// <para>
    /// The world is read in <c>Update</c> and the HUD is drawn in <c>OnGUI</c>,
    /// later in the same frame, and nothing tells one about the other: until
    /// 2026-09-25 a click on "Trigger event" with a door under it clicked the
    /// door too. Every card and button claims its rectangle as it is drawn,
    /// and the next frame's <c>Update</c> asks whether the pointer is over any
    /// of them. The rectangles are a frame old, which does not matter for a
    /// layout that stays put. IMGUI's y runs from the top of the screen and
    /// the input system's from the bottom, so the caller flips it.
    /// </para>
    /// </summary>
    internal static class HudHitTest
    {
        private static readonly List<Rect> claimed = new List<Rect>();
        private static readonly List<Rect> drawing = new List<Rect>();

        /// <summary>The start of a frame's drawing: forget last frame's claims once this frame's are complete.</summary>
        public static void BeginFrame()
        {
            drawing.Clear();
        }

        /// <summary>Something clickable was drawn here.</summary>
        public static void Claim(Rect area)
        {
            drawing.Add(area);
        }

        /// <summary>The frame's drawing is done: these are the claims the next Update reads.</summary>
        public static void EndFrame()
        {
            claimed.Clear();
            claimed.AddRange(drawing);
        }

        /// <summary>Whether this point (IMGUI coordinates, y from the top) is over something the HUD drew.</summary>
        public static bool Covers(Vector2 guiPoint)
        {
            for (int i = 0; i < claimed.Count; i++)
            {
                if (claimed[i].Contains(guiPoint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>For tests and scene changes: nothing is claimed.</summary>
        public static void Clear()
        {
            claimed.Clear();
            drawing.Clear();
        }
    }
}

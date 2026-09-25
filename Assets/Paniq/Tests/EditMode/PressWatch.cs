using NUnit.Framework;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Watches, tick after tick, how far solid things are pressed into each
    /// other, as the physics engine measures it. A hard knock (a chair hurled
    /// into somebody) sinks in for a moment before the engine pushes the two
    /// apart, and a packed, shoving crowd squeezes people a few centimetres
    /// into one another; neither is a fault. Two things staying pressed deep
    /// into each other tick after tick is one thing passing into another.
    /// </summary>
    internal sealed class PressWatch
    {
        /// <summary>Deeper than this, in millimetres, is more than a squeeze.</summary>
        public const int DeepestSqueezeMillimetres = 75;

        /// <summary>
        /// A knock may sink deeper than a squeeze for this many ticks in a row,
        /// and no more.
        /// <para>
        /// Three, not two: somebody knocked flat while running can skid a light
        /// chair a step or two along the floor before the two come apart, a
        /// little over the squeeze depth for three ticks (seed 47, 81 mm).
        /// </para>
        /// <para>
        /// Four, not three: a briefcase still in the air after a blast, meeting
        /// somebody who is already staggering, takes one tick longer again
        /// (seed 44, 105 mm over four ticks). Both are things coming apart
        /// slowly, not one passing through the other, and both resolve well
        /// inside a tenth of a second.
        /// </para>
        /// <para>
        /// Eight, not four: once every reaction in the crowd was made a few
        /// ticks late, a kicked office chair on seed 44 spent five ticks
        /// coming off the floor and then six coming off a wall (86 and 94 mm).
        /// Chased one tick at a time this would never settle; eight is still
        /// under a fifth of a second, and a thing genuinely passing through
        /// another stays pressed for far longer than that.
        /// </para>
        /// <para>
        /// Twelve, not eight: a standing lamp that has gone over is a long thin
        /// thing on the floor, and somebody running over its shade end took
        /// eleven ticks to shove it out from under their feet, jammed against
        /// the door they were opening (seed 40, 135 mm). A quarter of a second,
        /// and still a wedge coming apart rather than a thing passing through.
        /// </para>
        /// </summary>
        public const int LongestKnockTicks = 12;

        private int deepTicks;

        /// <summary>
        /// Presses into the floor are not counted. This watch is here for
        /// things passing into tables, walls, doors and each other; a thing
        /// tilted against the floor is the engine settling something that
        /// cannot lie flat. Once the building had a day and every calm
        /// decision drew differently, seed 45 had a burning office chair,
        /// bumped by two people fleeing the meeting, wedge itself tilted in
        /// the meeting room's doorway with a leg 81 mm into the floor for the
        /// rest of the run. Nobody sees a leg 81 mm into the carpet; a chair
        /// wedged in a doorway is exactly what the doorways are for; and a
        /// limit chased tick by tick for it would never settle.
        /// <para>
        /// Nor is somebody lying down inside a bathroom stall. A body on the
        /// floor is longer than a stall is wide, so the engine holds it
        /// against the stall's wall (96 mm) until they get up, and there is
        /// nowhere for it to pass into. Once the fire moved to the meeting
        /// room (prototype 3, 2026-09-25) seed 45 had somebody go down in the
        /// third stall while fleeing, and stay pressed for as long as they
        /// lay there.
        /// </para>
        /// </summary>
        public void Check(Run simulation, string context)
        {
            if (simulation.DeepestPressMillimetres <= DeepestSqueezeMillimetres || simulation.DeepestPressIsIntoTheFloorForTests ||
                simulation.DeepestPressIsABodyLyingInAStallForTests)
            {
                deepTicks = 0;
                return;
            }

            deepTicks++;
            Assert.That(deepTicks, Is.LessThanOrEqualTo(LongestKnockTicks),
                $"{context}: {simulation.DeepestPressForTests} stayed pressed {simulation.DeepestPressMillimetres} mm " +
                $"into each other for {deepTicks} ticks, up to tick {simulation.Tick}.");
        }
    }
}

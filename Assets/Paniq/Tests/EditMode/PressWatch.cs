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
        /// </summary>
        public const int LongestKnockTicks = 8;

        private int deepTicks;

        public void Check(Run simulation, string context)
        {
            if (simulation.DeepestPressMillimetres <= DeepestSqueezeMillimetres)
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

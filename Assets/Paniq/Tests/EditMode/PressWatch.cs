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

        /// <summary>A knock may sink deeper than a squeeze for this many ticks in a row, and no more.</summary>
        public const int LongestKnockTicks = 2;

        private int deepTicks;

        public void Check(FireReactionSimulation simulation, string context)
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

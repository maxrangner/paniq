using System;
using NUnit.Framework;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The meter is paid by events, so every kind of event has to say what it
    /// pays, even if that is nothing. An event type left out used to fall into
    /// a silent "nothing"; now it is an error, and this is what catches it the
    /// day a new event type is added.
    /// </summary>
    public sealed class UproarTableEditModeTests
    {
        [Test]
        public void EveryEventType_HasAnUproarTier()
        {
            foreach (FireReactionEventType type in Enum.GetValues(typeof(FireReactionEventType)))
            {
                Assert.That(() => InfluenceSystem.UproarTierOf(type), Throws.Nothing,
                    $"{type} does not say what it pays into the meter.");
            }
        }

        [TestCase(FireReactionEventType.AgentCaughtFire, UproarTier.Big)]
        [TestCase(FireReactionEventType.AgentKnockedDown, UproarTier.Middling)]
        [TestCase(FireReactionEventType.AgentYelled, UproarTier.Small)]
        [TestCase(FireReactionEventType.FireSpread, UproarTier.Nothing)]
        [TestCase(FireReactionEventType.AgentLost, UproarTier.Nothing)]
        [TestCase(FireReactionEventType.PowerBeefcake, UproarTier.Nothing)]
        public void TheTiers_AreAsTheDecisionLogSays(FireReactionEventType type, UproarTier expected)
        {
            Assert.That(InfluenceSystem.UproarTierOf(type), Is.EqualTo(expected));
        }
    }
}

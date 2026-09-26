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
            foreach (CausalEventType type in Enum.GetValues(typeof(CausalEventType)))
            {
                Assert.That(() => PurseSystem.UproarTierOf(type), Throws.Nothing,
                    $"{type} does not say what it pays into the meter.");
            }
        }

        [TestCase(CausalEventType.AgentCaughtFire, UproarTier.Big)]
        [TestCase(CausalEventType.AgentKnockedDown, UproarTier.Middling)]
        [TestCase(CausalEventType.AgentYelled, UproarTier.Small)]
        [TestCase(CausalEventType.FireSpread, UproarTier.Nothing)]
        [TestCase(CausalEventType.AgentLost, UproarTier.Nothing)]
        [TestCase(CausalEventType.PowerBeefcake, UproarTier.Nothing)]
        public void TheTiers_AreAsTheDecisionLogSays(CausalEventType type, UproarTier expected)
        {
            Assert.That(PurseSystem.UproarTierOf(type), Is.EqualTo(expected));
        }
    }
}

using System;
using NUnit.Framework;
using Paniq.Presentation;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The little signs that pop up to say why something happened. What can be
    /// checked without a screen is the choice of what earns one: the rule is
    /// that a sign is for something which changes what a person can do and
    /// which the player cannot work out by looking, and the log's own idea of
    /// background chatter is the first filter.
    /// </summary>
    public sealed class EventSignsEditModeTests
    {
        /// <summary>
        /// The chatter never gets a sign. Fire creeping a square and people
        /// bumping shoulders happen hundreds of times a round; a sign apiece
        /// would bury the screen and say nothing.
        /// </summary>
        [Test]
        public void BackgroundChatter_NeverEarnsASign()
        {
            foreach (FireReactionEventType type in Enum.GetValues(typeof(FireReactionEventType)))
            {
                if (EventStory.IsBackground(type))
                {
                    Assert.That(EventSigns.EarnsASign(type), Is.False,
                        $"{type} is background chatter in the log, so it must not pop a sign either.");
                }
            }
        }

        /// <summary>The moments worth interrupting somebody for.</summary>
        [TestCase(FireReactionEventType.AgentCaughtFire)]
        [TestCase(FireReactionEventType.AgentPassedOut)]
        [TestCase(FireReactionEventType.AgentCrushed)]
        [TestCase(FireReactionEventType.AgentGaveUpOnDoor)]
        [TestCase(FireReactionEventType.AgentRescued)]
        [TestCase(FireReactionEventType.DoorBlocked)]
        [TestCase(FireReactionEventType.ObjectExploded)]
        [TestCase(FireReactionEventType.AgentLookedForAWayOut)]
        [TestCase(FireReactionEventType.AgentFoundADeadEnd)]
        [TestCase(FireReactionEventType.AgentFoundTheWayOut)]
        public void SomethingWorthSaying_EarnsASign(FireReactionEventType type)
        {
            Assert.That(EventSigns.EarnsASign(type), Is.True);
        }

        /// <summary>
        /// Things the player can already see for themselves get no sign. A door
        /// swinging open is on the screen; there is nothing to explain.
        /// </summary>
        [TestCase(FireReactionEventType.DoorOpened)]
        [TestCase(FireReactionEventType.DoorClosed)]
        [TestCase(FireReactionEventType.FireActivated)]
        [TestCase(FireReactionEventType.AgentAlerted)]
        public void SomethingAlreadyOnTheScreen_EarnsNoSign(FireReactionEventType type)
        {
            Assert.That(EventSigns.EarnsASign(type), Is.False);
        }

        /// <summary>
        /// Good news is green and bad news is red, so the colour carries when
        /// there is no time to read the words. Being dragged clear of a fire is
        /// the good kind; catching fire is not.
        /// </summary>
        [Test]
        public void RescuesReadAsGoodNews_AndDisastersDoNot()
        {
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentRescued), Is.True);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentDoused), Is.True);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentShookAwake), Is.True);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentFoundTheWayOut), Is.True);

            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentCaughtFire), Is.False);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentCrushed), Is.False);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentLost), Is.False);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentLookedForAWayOut), Is.False);
            Assert.That(EventSigns.IsGoodNews(FireReactionEventType.AgentFoundADeadEnd), Is.False);
        }

        /// <summary>
        /// Nothing may claim to be good news without also earning a sign --
        /// otherwise a colour is being chosen for something never shown.
        /// </summary>
        [Test]
        public void EverythingCalledGoodNews_AlsoEarnsASign()
        {
            foreach (FireReactionEventType type in Enum.GetValues(typeof(FireReactionEventType)))
            {
                if (EventSigns.IsGoodNews(type))
                {
                    Assert.That(EventSigns.EarnsASign(type), Is.True,
                        $"{type} is coloured as good news but never shown.");
                }
            }
        }
    }
}

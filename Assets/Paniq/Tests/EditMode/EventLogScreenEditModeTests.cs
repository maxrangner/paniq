using NUnit.Framework;
using Paniq.Presentation;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The round read back as a list. The end card asks for it by setting a
    /// flag; something else has to take that flag, open the log and draw it.
    /// For a while nothing did, and pressing "What happened" did nothing at
    /// all, so these pin down the half of that handshake which can be checked
    /// without a screen.
    /// </summary>
    public sealed class EventLogScreenEditModeTests
    {
        [Test]
        public void TheLog_StartsClosed()
        {
            var log = new EventLogScreen();
            Assert.That(log.IsOpen, Is.False, "Nothing should cover the screen until somebody asks for it.");
        }

        [Test]
        public void OpeningAndClosingTheLog_ShowsAndHidesIt()
        {
            var log = new EventLogScreen();
            log.Open();
            Assert.That(log.IsOpen, Is.True);

            log.Close();
            Assert.That(log.IsOpen, Is.False, "Escape and the Back button both close it.");
        }

        [Test]
        public void OpeningTheLogTwice_IsTheSameAsOpeningItOnce()
        {
            var log = new EventLogScreen();
            log.Open();
            log.Open();
            log.Close();
            Assert.That(log.IsOpen, Is.False, "One close should be enough however many times it was opened.");
        }

        [Test]
        public void DrawingAClosedLog_WithNothingToShow_DoesNotThrow()
        {
            // Drawn every frame whether or not it is open, and on a frame
            // before the first snapshot exists there is nothing to read.
            var log = new EventLogScreen();
            Assert.DoesNotThrow(() => log.Draw(null));

            log.Open();
            Assert.DoesNotThrow(() => log.Draw((FireReactionSnapshot)null));
        }
    }
}

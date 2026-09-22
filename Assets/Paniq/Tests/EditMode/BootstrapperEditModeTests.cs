using NUnit.Framework;
using Paniq.App;

namespace Paniq.Tests.EditMode
{
    public sealed class BootstrapperEditModeTests
    {
        [Test]
        public void Bootstrapper_UsesTheFireReactionPrototypeAsItsFirstDestination()
        {
            Assert.That(Bootstrapper.FireReactionPrototypeSceneName, Is.EqualTo("FireReactionPrototype"));
        }
    }
}

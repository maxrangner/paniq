using NUnit.Framework;
using Paniq.App;

namespace Paniq.Tests.EditMode
{
    public sealed class BootstrapperEditModeTests
    {
        [Test]
        public void Bootstrapper_UsesTheDevelopmentSceneAsItsFirstDestination()
        {
            Assert.That(Bootstrapper.DevelopmentSceneName, Is.EqualTo("Development"));
        }
    }
}

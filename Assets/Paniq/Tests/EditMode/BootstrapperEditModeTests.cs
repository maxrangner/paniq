using NUnit.Framework;
using Paniq.App;

namespace Paniq.Tests.EditMode
{
    public sealed class BootstrapperEditModeTests
    {
        [Test]
        public void Bootstrapper_UsesThePrototypeSceneAsItsFirstDestination()
        {
            Assert.That(Bootstrapper.PrototypeSceneName, Is.EqualTo("prototype_fire_1_fl_small"));
        }
    }
}

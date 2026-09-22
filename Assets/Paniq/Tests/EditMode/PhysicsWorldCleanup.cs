using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Paniq.Simulation;

[assembly: Paniq.Tests.DisposePhysicsWorldsAfterEachTest]

namespace Paniq.Tests
{
    /// <summary>
    /// Every run keeps a physics scene of its own in the editor. Most tests
    /// build a run and simply drop it, so after each test every scene still
    /// open is closed here, rather than asking every test to remember.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class DisposePhysicsWorldsAfterEachTestAttribute : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
        }

        public void AfterTest(ITest test)
        {
            PhysicsWorld.DisposeEveryWorld();
        }
    }
}

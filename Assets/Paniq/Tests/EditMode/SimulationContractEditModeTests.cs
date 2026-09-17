using NUnit.Framework;
using UnityEngine;

namespace Paniq.Tests.EditMode
{
    public sealed class SimulationContractEditModeTests
    {
        private const float SimulationFixedStepSeconds = 0.02f;
        private const float FixedStepTolerance = 0.000001f;

        [Test]
        public void FixedTimestep_MatchesTheReplayCompatibleSimulationContract()
        {
            Assert.That(
                Time.fixedDeltaTime,
                Is.EqualTo(SimulationFixedStepSeconds).Within(FixedStepTolerance),
                "The fixed timestep is replay-compatible simulation configuration and must remain 0.02 seconds.");
        }
    }
}

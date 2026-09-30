using NUnit.Framework;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The maze (2026-09-30): a test level for watching following. One
    /// person works here and knows every turn; ten are visitors who know
    /// only the cell they stand in. Set off, the staff member should get
    /// out, and somebody should fall in behind them on the way.
    /// </summary>
    public sealed class MazeEditModeTests
    {
        private ScenarioAsset scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = ScenarioAsset.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        /// <summary>
        /// A whole minute and a half, because the claim is that people get
        /// out of a maze: sixteen cells of four metres from the far corner
        /// to the way out, with ten strangers searching, is not a claim a
        /// shorter run can make.
        /// </summary>
        [Test]
        public void SetOff_TheStaffMemberGetsOut_SomebodyFollowsThem_AndAVisitorGetsOutToo()
        {
            ScenarioData data = TestBuildings.Maze(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 11UL))
            {
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), 5);
                bool followed = false;
                int leaderOutOnTick = -1;
                int visitorOutOnTick = -1;
                for (int t = 0; t < 90 * Run.TicksPerSecond && visitorOutOnTick < 0; t++)
                {
                    simulation.Step();
                    for (int i = 1; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot visitor = simulation.GetAgent(i);
                        followed |= visitor.ActivityState == AgentActivityState.Following;
                        if (visitorOutOnTick < 0 && visitor.Outcome == AgentTerminalOutcome.Escaped)
                        {
                            visitorOutOnTick = simulation.Tick;
                        }
                    }

                    if (leaderOutOnTick < 0 && simulation.GetAgent(0).Outcome == AgentTerminalOutcome.Escaped)
                    {
                        leaderOutOnTick = simulation.Tick;
                    }
                }

                Assert.That(leaderOutOnTick, Is.GreaterThan(0), "The one person who knows the way never got out.");
                Assert.That(leaderOutOnTick, Is.LessThan(60 * Run.TicksPerSecond), "Sixty-four metres of maze should not take a minute.");
                Assert.That(followed, Is.True, "Nobody ever fell in behind the staff member.");
                Assert.That(visitorOutOnTick, Is.GreaterThan(0), "Not one visitor found the way out in a minute and a half.");
            }
        }
    }
}

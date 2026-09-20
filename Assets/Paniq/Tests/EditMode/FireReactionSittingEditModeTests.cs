using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Sitting on chairs: a calm person walks over and sits down, the chair
    /// stays put while they are on it, and a fright gets them out of it
    /// before they can run.
    /// </summary>
    public sealed class FireReactionSittingEditModeTests
    {
        private FireReactionScenario scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = FireReactionScenario.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        /// <summary>Whether this person is sitting on the one chair.</summary>
        private static bool OnTheChair(FireReactionSimulation simulation)
        {
            return simulation.GetPhysicsObject(0).OccupiedBy == simulation.GetAgent(0).AgentId;
        }

        /// <summary>One person and one chair alone in the office, with no fire yet.</summary>
        private FireReactionScenarioData OnePersonOneChair()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, 0), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Chair,
                    new LogicalPosition(-1500, 0), 450, 5000)
            };
            data.Fire.ActivationTick = int.MaxValue;

            // They always choose to sit down when they choose afresh.
            data.Items.TidyChancePercent = 0;
            data.Items.SitChancePercent = 100;
            data.Calm.DecisionMinimumTicks = 10;
            data.Calm.DecisionMaximumTicks = 20;
            return data;
        }

        [Test]
        public void ACalmPerson_WalksToAChairAndSitsOnIt()
        {
            var simulation = new FireReactionSimulation(OnePersonOneChair());
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond && !OnTheChair(simulation); t++)
            {
                simulation.Step();
            }

            Assert.That(OnTheChair(simulation), Is.True, "Nobody ever sat down.");
            Assert.That(simulation.GetPhysicsObject(0).IsSatOn, Is.True);
            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting));
            Assert.That(LogicalPosition.DistanceSquared(simulation.GetAgent(0).Position, new LogicalPosition(-1500, 0)),
                Is.LessThan(400L * 400L), "They are on the chair, not beside it.");
        }

        [Test]
        public void AChairWithSomeoneOnIt_StaysPutWhenKicked()
        {
            var simulation = new FireReactionSimulation(OnePersonOneChair());
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            LogicalPosition before = simulation.GetPhysicsObject(0).Position;
            simulation.LaunchObjectForTests(0, 90, 0);
            for (int t = 0; t < FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(0).Position, Is.EqualTo(before),
                "A chair with someone sitting on it should not slide.");
        }

        [Test]
        public void SomeoneSittingWhenTheFireStarts_GetsUpBeforeTheyRun()
        {
            FireReactionScenarioData data = OnePersonOneChair();

            // The fire breaks out right in front of them, well after they sit.
            data.Fire.ActivationTick = 400;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 8 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).ActivityState != AgentActivityState.Sitting; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Nobody sat down.");
            while (simulation.Tick < data.Fire.ActivationTick)
            {
                simulation.Step();
            }

            int satStill = 0;
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond && OnTheChair(simulation); t++)
            {
                simulation.Step();
                satStill += simulation.GetAgent(0).SpeedMillimetresPerTick == 0 ? 1 : 0;
            }

            Assert.That(OnTheChair(simulation), Is.False, "They never got out of the chair.");
            Assert.That(satStill, Is.GreaterThan(5), "Getting out of a chair should cost them a moment.");

            for (int t = 0; t < 3 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            FireReactionAgentSnapshot person = simulation.GetAgent(0);
            Assert.That(person.FearState, Is.EqualTo(AgentFearState.Scared));
            Assert.That(person.Position.X, Is.LessThan(-1500), "Once up, they run away from the flames.");
            Assert.That(simulation.GetPhysicsObject(0).IsSatOn, Is.False, "The chair is free again.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>People helping each other: shaking the frozen awake, dragging the knocked-out to safety.</summary>
    public sealed class FireReactionHelpingEditModeTests
    {
        private static readonly SimulationId Helper = new SimulationId(1UL);
        private static readonly SimulationId InNeed = new SimulationId(2UL);

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

        private static List<CausalEvent> EventsOfType(FireReactionSimulation simulation, FireReactionEventType type)
        {
            var found = new List<CausalEvent>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType == type)
                {
                    found.Add(record);
                }
            }

            return found;
        }

        private FireReactionScenarioData EmptyRoom(params FireReactionAgentDefinition[] people)
        {
            FireReactionScenarioData data =
                FireReactionDoorsEditModeTests.WithAWayOutOfTheOffice(scenario.ToRuntimeData());
            data.Agents = people;
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            data.Perception.MaximumReactionDelayTicks = 0;
            return data;
        }

        /// <summary>
        /// Two people facing a small fire 2 m ahead: a helper with the given
        /// traits, and someone so fearful they freeze (for a long while).
        /// </summary>
        private FireReactionSimulation HelperAndSomeoneFrozen(AgentTraitValues helper)
        {
            FireReactionScenarioData data = EmptyRoom(
                new FireReactionAgentDefinition(Helper, new LogicalPosition(-2000, 0), CardinalDirection.North, helper),
                new FireReactionAgentDefinition(InNeed, new LogicalPosition(0, 0), CardinalDirection.North,
                    new AgentTraitValues(5, 5, 0, 5, 0, 10)));
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(-1250, -1250, 2250, 2250);
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 50;
            data.Temperament.FreezeMinimumTicks = 3000;
            data.Temperament.FreezeMaximumTicks = 3000;
            return new FireReactionSimulation(data);
        }

        [Test]
        public void KindRunner_ShakesSomeoneFrozenAwake()
        {
            FireReactionSimulation simulation = HelperAndSomeoneFrozen(new AgentTraitValues(4, 4, 7, 9, 0, 4));
            Assert.That(simulation.GetAgent(1).Temperament, Is.EqualTo(AgentPanicTemperament.FreezeThenRun));
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond && EventsOfType(simulation, FireReactionEventType.AgentShookAwake).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> shook = EventsOfType(simulation, FireReactionEventType.AgentShookAwake);
            Assert.That(shook, Has.Count.EqualTo(1), "Nobody shook the frozen person awake.");
            Assert.That(shook[0].SourceId, Is.EqualTo(Helper));
            Assert.That(shook[0].TargetId, Is.EqualTo(InNeed));
            List<CausalEvent> unfroze = EventsOfType(simulation, FireReactionEventType.AgentUnfroze);
            Assert.That(unfroze, Has.Count.EqualTo(1));
            Assert.That(unfroze[0].CausalParentEventId, Is.EqualTo(shook[0].EventId), "They snapped out of it because of the shake.");
            Assert.That(unfroze[0].Tick, Is.LessThan(3000), "Long before their own freeze would have ended.");

            simulation.Step();
            Assert.That(simulation.GetAgent(1).ActivityState, Is.Not.EqualTo(AgentActivityState.Frozen));
        }

        [Test]
        public void EvilRunner_NeverHelps()
        {
            FireReactionSimulation simulation = HelperAndSomeoneFrozen(new AgentTraitValues(4, 4, 7, 9, 9, 4));
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentShookAwake), Is.Empty);
            Assert.That(simulation.GetAgent(1).ActivityState, Is.EqualTo(AgentActivityState.Frozen));
        }

        [Test]
        public void StrongKindRunner_DragsSomeoneKnockedOutThroughAnOpenDoor()
        {
            // The person in need is knocked out cold by a heavy box before the fire starts;
            // the helper, between them and a fire to the south, sees the fire and panics.
            FireReactionScenarioData data = EmptyRoom(
                new FireReactionAgentDefinition(Helper, new LogicalPosition(-2500, 1500), CardinalDirection.South,
                    new AgentTraitValues(8, 6, 8, 8, 1, 3)),
                new FireReactionAgentDefinition(InNeed, new LogicalPosition(-2500, 3000), CardinalDirection.South,
                    AgentTraitValues.AllOrdinary));
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Box,
                    new LogicalPosition(-4500, 3000), 600, 20000)
            };
            data.Falls.PassOutChancePercent = 100;
            data.Falls.PassOutMaximumPercent = 100;
            data.Falls.UnconsciousMinimumTicks = 3000;
            data.Falls.UnconsciousMaximumTicks = 3000;
            data.Fire.ActivationTick = 60;
            data.Fire.SpawnBounds = new LogicalBounds(-2750, -2750, -750, -750);
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new FireReactionSimulation(data);
            simulation.LaunchObjectForTests(0, 110, 0);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(2001UL), 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, new SimulationId(2001UL), 2);
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(1).Outcome == AgentTerminalOutcome.Unresolved; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentPassedOut), Has.Count.EqualTo(1),
                "The box should have knocked them out.");
            List<CausalEvent> grabbed = EventsOfType(simulation, FireReactionEventType.AgentGrabbed);
            Assert.That(grabbed, Has.Count.EqualTo(1), "The helper never grabbed them.");
            Assert.That(grabbed[0].TargetId, Is.EqualTo(InNeed));
            List<CausalEvent> rescued = EventsOfType(simulation, FireReactionEventType.AgentRescued);
            Assert.That(rescued, Has.Count.EqualTo(1), "They were never dragged out.");
            Assert.That(rescued[0].SourceId, Is.EqualTo(Helper));
            Assert.That(rescued[0].TargetId, Is.EqualTo(InNeed));
            Assert.That(simulation.EventLog.Get(rescued[0].CausalParentEventId).EventType,
                Is.EqualTo(FireReactionEventType.AgentEscaped), "Rescued because the helper got out.");
            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            Assert.That(simulation.GetAgent(1).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            Assert.That(simulation.GetAgent(1).BodyState, Is.EqualTo(AgentBodyState.Unconscious), "Still out cold: carried out.");
        }
    }
}

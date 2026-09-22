using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>People on fire: they run around wildly, set others alight, then collapse.</summary>
    public sealed class FireReactionBurningEditModeTests
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

        private FireReactionScenarioData DefaultData() => scenario.ToRuntimeData();

        private static FireReactionAgentDefinition Person(ulong id, int x, int z, CardinalDirection facing)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing, AgentTraitValues.AllOrdinary);
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

        /// <summary>Someone standing where the fire starts, in a room with nothing else in it.</summary>
        private FireReactionScenarioData StandingInTheFire(params FireReactionAgentDefinition[] others)
        {
            FireReactionScenarioData data = DefaultData();
            var people = new List<FireReactionAgentDefinition> { Person(1UL, 250, 250, CardinalDirection.North) };
            people.AddRange(others);
            data.Agents = people.ToArray();
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(250, 250, 250, 250);

            // Slow fire, so only the first square matters here.
            data.Fire.SpreadMinimumTicks = 5000;
            data.Fire.SpreadMaximumTicks = 5000;
            return data;
        }

        [Test]
        public void TouchingFire_SetsSomeoneAlightInsteadOfKillingThemOutright()
        {
            FireReactionScenarioData data = StandingInTheFire();
            var simulation = new FireReactionSimulation(data);
            simulation.Step();

            FireReactionAgentSnapshot person = simulation.GetAgent(0);
            Assert.That(person.IsBurning, Is.True);
            Assert.That(person.Participation, Is.EqualTo(AgentParticipation.Participating), "Not dead yet.");
            Assert.That(person.ActivityState, Is.EqualTo(AgentActivityState.Burning));
            List<CausalEvent> caught = EventsOfType(simulation, FireReactionEventType.AgentCaughtFire);
            Assert.That(caught, Has.Count.EqualTo(1));
            Assert.That(caught[0].CausalParentEventId, Is.EqualTo(simulation.FireActivationEventId));
            Assert.That(caught[0].DurationTicks, Is.InRange(data.Fire.BurnMinimumTicks, data.Fire.BurnMaximumTicks));

            // Running around wildly: they cover ground and change direction, and scream.
            LogicalPosition start = person.Position;
            var headings = new HashSet<int>();
            long farthest = 0L;
            while (simulation.GetAgent(0).Participation == AgentParticipation.Participating)
            {
                simulation.Step();
                FireReactionAgentSnapshot now = simulation.GetAgent(0);
                headings.Add(now.HeadingDegrees / 30);
                farthest = System.Math.Max(farthest, LogicalPosition.DistanceSquared(start, now.Position));
                Assert.That(simulation.Tick, Is.LessThanOrEqualTo(1 + caught[0].DurationTicks), "They burned too long.");
            }

            Assert.That(farthest, Is.GreaterThan(1500L * 1500L), "A burning person should run around, not stand still.");
            Assert.That(headings.Count, Is.GreaterThanOrEqualTo(4), "A burning person should lurch in many directions.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentYelled).Count, Is.GreaterThan(2), "They scream.");

            FireReactionAgentSnapshot end = simulation.GetAgent(0);
            Assert.That(end.Outcome, Is.EqualTo(AgentTerminalOutcome.Lost));
            List<CausalEvent> lost = EventsOfType(simulation, FireReactionEventType.AgentLost);
            Assert.That(lost[0].CausalParentEventId, Is.EqualTo(caught[0].EventId));
            Assert.That(lost[0].Tick, Is.EqualTo(caught[0].Tick + caught[0].DurationTicks));
        }

        [Test]
        public void BurningPerson_SetsSomeoneRightNextToThemAlight()
        {
            // A second person stands a hand's breadth away, frozen for good so they never move off.
            FireReactionScenarioData data = StandingInTheFire(Person(2UL, 250, 800, CardinalDirection.South));
            data.Fire.BurningSpreadChancePercent = 100;
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 3 && !simulation.GetAgent(1).IsBurning; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(1).IsBurning, Is.True, "The flames never jumped across.");
            List<CausalEvent> caught = EventsOfType(simulation, FireReactionEventType.AgentCaughtFire);
            Assert.That(caught, Has.Count.EqualTo(2));
            Assert.That(caught[1].SourceId, Is.EqualTo(new SimulationId(2UL)));
            Assert.That(caught[1].CausalParentEventId, Is.EqualTo(caught[0].EventId), "Set alight by the first person.");
        }

        [Test]
        public void FlamesDoNotJumpAcrossAGap()
        {
            FireReactionScenarioData data = StandingInTheFire(Person(2UL, 250, 1500, CardinalDirection.South));
            data.Fire.BurningSpreadChancePercent = 100;
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;

            var simulation = new FireReactionSimulation(data);
            simulation.Step();
            Assert.That(simulation.GetAgent(0).IsBurning, Is.True);
            Assert.That(LogicalPosition.DistanceSquared(simulation.GetAgent(0).Position, simulation.GetAgent(1).Position),
                Is.GreaterThan(800L * 800L));
            Assert.That(simulation.GetAgent(1).IsBurning, Is.False);
        }

        [Test]
        public void PanickedCrowds_SpreadFireToEachOtherSometimes()
        {
            int caught = 0;
            int caughtFromPeople = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                var simulation = new FireReactionSimulation(DefaultData(), seed);
                for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                        if (agent.IsBurning && agent.Participation == AgentParticipation.Participating)
                        {
                            Assert.That(agent.Outcome, Is.EqualTo(AgentTerminalOutcome.Unresolved), "Nobody escapes while on fire.");
                        }
                    }
                }

                foreach (CausalEvent record in EventsOfType(simulation, FireReactionEventType.AgentCaughtFire))
                {
                    caught++;
                    if (simulation.EventLog.Get(record.CausalParentEventId).EventType == FireReactionEventType.AgentCaughtFire)
                    {
                        caughtFromPeople++;
                    }
                }
            }

            Assert.That(caught, Is.GreaterThan(0));
            TestContext.WriteLine($"Seeds 40-46, 60 s: {caught} people caught fire, {caughtFromPeople} of them from another burning person.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Fire extinguishers: putting the fire out square by square, saving
    /// someone who is alight, knocking people over with the jet, running dry,
    /// and the recoil that shoves a weak person backwards.
    /// </summary>
    public sealed class FireReactionExtinguisherEditModeTests
    {
        private static readonly SimulationId TheExtinguisher = new SimulationId(3001UL);

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

        /// <summary>
        /// A small fire in the middle of the office, an extinguisher beside
        /// one brave person, and nobody else about.
        /// </summary>
        private FireReactionScenarioData BraveWithAnExtinguisher(AgentTraitValues traits)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, 0), CardinalDirection.East, traits)
            };
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheExtinguisher, PhysicsObjectKind.Extinguisher,
                    new LogicalPosition(-3600, 0), 250, 7000)
            };
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 0, 0);

            // The fire stays put, so the test measures the spray, not the spread.
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            return data;
        }

        /// <summary>The brave one: bravery 9, ordinary otherwise.</summary>
        private static AgentTraitValues Brave(int strength = 8) => new AgentTraitValues(strength, 5, 9, 5, 2, 3);

        [Test]
        public void ABravePerson_FetchesAnExtinguisherAndPutsTheFireOut()
        {
            var simulation = new FireReactionSimulation(BraveWithAnExtinguisher(Brave()));
            simulation.Step();
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond && simulation.FireCellCount > 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentTookExtinguisher), Is.Not.Empty,
                "Nobody picked the extinguisher up.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.ExtinguisherSprayed), Is.Not.Empty, "Nobody sprayed it.");
            Assert.That(simulation.FireCellCount, Is.EqualTo(0), "The fire should have been put out.");

            CausalEvent doused = EventsOfType(simulation, FireReactionEventType.FireDoused)[0];
            Assert.That(simulation.EventLog.Get(doused.CausalParentEventId).EventType,
                Is.EqualTo(FireReactionEventType.ExtinguisherSprayed), "Putting a square out traces back to the spray.");
        }

        [Test]
        public void ADousedSquare_StaysOutAndWillNotCatchAgainForAWhile()
        {
            FireReactionScenarioData data = BraveWithAnExtinguisher(Brave());

            // The fire spreads freely again, so it would retake the ground if it could.
            data.Fire.SpreadMinimumTicks = 40;
            data.Fire.SpreadMaximumTicks = 60;
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.FireDoused).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> doused = EventsOfType(simulation, FireReactionEventType.FireDoused);
            Assert.That(doused, Is.Not.Empty, "No square was ever put out.");
            LogicalPosition square = doused[0].Position;
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
                {
                    Assert.That(cell.IsOut || !cell.Centre.Equals(square), Is.True,
                        $"The square at {square} caught again at tick {simulation.Tick}.");
                }
            }
        }

        [Test]
        public void TheBottle_RunsDryAndIsDropped()
        {
            FireReactionScenarioData data = BraveWithAnExtinguisher(Brave());

            // A fire that takes far longer to beat than one bottle lasts.
            data.Extinguishers.FuelTicks = 40;
            data.Fire.DouseTicksPerCell = 10000;
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.ExtinguisherEmptied).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.ExtinguisherEmptied), Is.Not.Empty,
                "The bottle never ran dry.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.ExtinguisherSprayed),
                Has.Count.EqualTo(data.Extinguishers.FuelTicks), "A bottle holds exactly its fuel in spray.");
            Assert.That(simulation.GetPhysicsObject(0).IsHeld, Is.False, "They dropped the empty bottle.");
        }

        [Test]
        public void SomeoneAlight_IsPutOutByTheJet()
        {
            FireReactionScenarioData data = BraveWithAnExtinguisher(new AgentTraitValues(8, 5, 5, 9, 0, 3));

            // A second person who walks into the flames and catches fire.
            data.Agents = new[]
            {
                data.Agents[0],
                new FireReactionAgentDefinition(new SimulationId(2UL), new LogicalPosition(600, 0), CardinalDirection.West,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.BurnMinimumTicks = 100000;
            data.Fire.BurnMaximumTicks = 100000;

            // Nobody runs far here, and the flames do not jump between people,
            // so the test is about the jet and nothing else.
            data.Fire.BurningSpreadChancePercent = 0;
            data.Fire.BurningSpreadGapMillimetres = 0;
            data.Panic.SpeedMinimum = 20;
            data.Panic.SpeedMaximum = 30;
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 40 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.AgentDoused).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> saved = EventsOfType(simulation, FireReactionEventType.AgentDoused);
            Assert.That(saved, Is.Not.Empty, "Nobody who was alight was ever hosed down.");
            Assert.That(simulation.GetAgent(saved[0].TargetId).IsBurning, Is.False, "The flames on them are out.");

            // The same jet knocks them off their feet, away from the sprayer.
            List<CausalEvent> blasted = EventsOfType(simulation, FireReactionEventType.AgentBlasted);
            Assert.That(blasted, Is.Not.Empty, "The jet should have knocked them over as well.");
            Assert.That(blasted[0].TargetId, Is.EqualTo(saved[0].TargetId), "The blast names who it hit.");
            Assert.That(simulation.GetAgent(blasted[0].TargetId).IsDown, Is.True, "The jet puts them on the floor.");
        }

        [TestCase(10, false)]
        [TestCase(1, true)]
        public void TheRecoil_ShovesAWeakSprayerBackwards(int strength, bool expectPushedBack)
        {
            FireReactionScenarioData data = BraveWithAnExtinguisher(Brave(strength));

            // Long enough on the trigger for the recoil to tell.
            data.Fire.DouseTicksPerCell = 10000;
            var simulation = new FireReactionSimulation(data);
            bool sprayed = false;
            int firstSprayX = 0;
            int furthestBack = 0;
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                if (simulation.GetAgent(0).ActivityState != AgentActivityState.Spraying)
                {
                    continue;
                }

                if (!sprayed)
                {
                    sprayed = true;
                    firstSprayX = simulation.GetAgent(0).Position.X;
                }

                furthestBack = System.Math.Max(furthestBack, firstSprayX - simulation.GetAgent(0).Position.X);
            }

            Assert.That(sprayed, Is.True, "They never sprayed.");
            if (expectPushedBack)
            {
                Assert.That(furthestBack, Is.GreaterThan(200), "A weak person is walked backwards by the bottle.");
            }
            else
            {
                Assert.That(furthestBack, Is.LessThan(200), "A strong person holds it steady.");
            }
        }
    }
}

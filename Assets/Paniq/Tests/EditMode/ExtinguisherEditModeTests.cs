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
    public sealed class ExtinguisherEditModeTests
    {
        private static readonly SimulationId TheExtinguisher = new SimulationId(3001UL);

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

        private static List<CausalEvent> EventsOfType(Run simulation, CausalEventType type)
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
        private ScenarioData BraveWithAnExtinguisher(AgentTraitValues traits)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, 0), CardinalDirection.East, traits)
            };
            data.Tables = new TableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheExtinguisher, PhysicsObjectKind.Extinguisher,
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
            var simulation = new Run(BraveWithAnExtinguisher(Brave()));
            simulation.Step();
            for (int t = 0; t < 30 * Run.TicksPerSecond && simulation.FireCellCount > 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.AgentTookExtinguisher), Is.Not.Empty,
                "Nobody picked the extinguisher up.");
            Assert.That(EventsOfType(simulation, CausalEventType.ExtinguisherSprayed), Is.Not.Empty, "Nobody sprayed it.");
            Assert.That(simulation.FireCellCount, Is.EqualTo(0), "The fire should have been put out.");

            CausalEvent doused = EventsOfType(simulation, CausalEventType.FireDoused)[0];
            Assert.That(simulation.EventLog.Get(doused.CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.ExtinguisherSprayed), "Putting a square out traces back to the spray.");
        }

        [Test]
        public void ADousedSquare_StaysOutAndWillNotCatchAgainForAWhile()
        {
            ScenarioData data = BraveWithAnExtinguisher(Brave());

            // The fire spreads freely again, so it would retake the ground if it could.
            data.Fire.SpreadMinimumTicks = 40;
            data.Fire.SpreadMaximumTicks = 60;
            var simulation = new Run(data);
            for (int t = 0; t < 30 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.FireDoused).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> doused = EventsOfType(simulation, CausalEventType.FireDoused);
            Assert.That(doused, Is.Not.Empty, "No square was ever put out.");
            LogicalPosition square = doused[0].Position;
            for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
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
            ScenarioData data = BraveWithAnExtinguisher(Brave());

            // A fire that takes far longer to beat than one bottle lasts.
            data.Extinguishers.FuelTicks = 40;
            data.Fire.DouseTicksPerCell = 10000;
            var simulation = new Run(data);
            for (int t = 0; t < 60 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.ExtinguisherEmptied).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.ExtinguisherEmptied), Is.Not.Empty,
                "The bottle never ran dry.");
            Assert.That(EventsOfType(simulation, CausalEventType.ExtinguisherSprayed),
                Has.Count.EqualTo(data.Extinguishers.FuelTicks), "A bottle holds exactly its fuel in spray.");
            Assert.That(simulation.GetPhysicsObject(0).IsHeld, Is.False, "They dropped the empty bottle.");
        }

        [Test]
        public void SomeoneAlight_IsPutOutByTheJet()
        {
            ScenarioData data = BraveWithAnExtinguisher(new AgentTraitValues(8, 5, 5, 9, 0, 3));

            // A second person who walks into the flames and catches fire.
            data.Agents = new[]
            {
                data.Agents[0],
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(600, 0), CardinalDirection.West,
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
            var simulation = new Run(data);
            for (int t = 0; t < 40 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.AgentDoused).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> saved = EventsOfType(simulation, CausalEventType.AgentDoused);
            Assert.That(saved, Is.Not.Empty, "Nobody who was alight was ever hosed down.");
            if (simulation.GetAgent(saved[0].TargetId).IsBurning)
            {
                // Put out, then knocked flat by the same jet into the flames
                // they were standing in, and alight again: the log has to say
                // so, or the douse did nothing.
                bool caughtAgain = false;
                foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.AgentCaughtFire))
                {
                    caughtAgain |= record.SourceId == saved[0].TargetId && record.EventId > saved[0].EventId;
                }

                Assert.That(caughtAgain, Is.True, "The flames on them are out, unless they caught again afterwards.");
            }

            // The same jet knocks them off their feet, away from the sprayer.
            List<CausalEvent> blasted = EventsOfType(simulation, CausalEventType.AgentBlasted);
            Assert.That(blasted, Is.Not.Empty, "The jet should have knocked them over as well.");
            Assert.That(blasted[0].TargetId, Is.EqualTo(saved[0].TargetId), "The blast names who it hit.");
            Assert.That(simulation.GetAgent(blasted[0].TargetId).IsDown, Is.True, "The jet puts them on the floor.");
        }

        [TestCase(10, false)]
        [TestCase(1, true)]
        public void TheRecoil_ShovesAWeakSprayerBackwards(int strength, bool expectPushedBack)
        {
            ScenarioData data = BraveWithAnExtinguisher(Brave(strength));

            // Long enough on the trigger for the recoil to tell.
            data.Fire.DouseTicksPerCell = 10000;
            var simulation = new Run(data);
            bool sprayed = false;
            int firstSprayX = 0;
            int furthestBack = 0;
            for (int t = 0; t < 30 * Run.TicksPerSecond; t++)
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

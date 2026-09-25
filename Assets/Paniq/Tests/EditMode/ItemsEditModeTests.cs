using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Picking items up, carrying them, setting them down, dropping and throwing them.</summary>
    public sealed class ItemsEditModeTests
    {
        private static readonly SimulationId BoxId = new SimulationId(3001UL);

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
        /// A tidy-minded person at (-4, -4) m with one box right beside them,
        /// deciding afresh every tick or two, in a room with no fire (unless a
        /// test adds one) and no furniture.
        /// </summary>
        private ScenarioData TidierWithABox(AgentTraitValues traits, int massGrams, params AgentDefinition[] others)
        {
            ScenarioData data = scenario.ToRuntimeData();
            var people = new List<AgentDefinition>
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-4000, -4000), CardinalDirection.North, traits)
            };
            people.AddRange(others);
            data.Agents = people.ToArray();
            data.Tables = new TableDefinition[0];
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(BoxId, PhysicsObjectKind.Box, new LogicalPosition(-4000, -3400), 400, massGrams)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Items.TidyChancePercent = 100;
            data.Calm.DecisionMinimumTicks = 1;
            data.Calm.DecisionMaximumTicks = 2;
            return data;
        }

        [Test]
        public void CarryLimit_GrowsWithStrength()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var weak = new Agent(0, new SimulationId(1UL), 0) { Traits = new AgentTraitValues(0, 5, 5, 5, 5, 5) };
            var strong = new Agent(0, new SimulationId(1UL), 0) { Traits = new AgentTraitValues(10, 5, 5, 5, 5, 5) };
            Assert.That(TraitEffects.CarryLimitGrams(weak, data), Is.EqualTo(5000));
            Assert.That(TraitEffects.CarryLimitGrams(strong, data), Is.EqualTo(30000));
        }

        [Test]
        public void CalmPerson_PicksABoxUpCarriesItOffAndSetsItDown()
        {
            ScenarioData data = TidierWithABox(AgentTraitValues.AllOrdinary, 6000);
            var simulation = new Run(data);
            LogicalPosition start = simulation.GetPhysicsObject(0).Position;
            int pickedUpTick = -1;
            int setDownTick = -1;
            int fastestWhileCarrying = 0;
            for (int t = 0; t < 1500 && setDownTick < 0; t++)
            {
                simulation.Step();
                PhysicsObjectSnapshot box = simulation.GetPhysicsObject(0);
                if (box.IsHeld)
                {
                    Assert.That(box.HeldBy, Is.EqualTo(new SimulationId(1UL)));
                    pickedUpTick = pickedUpTick < 0 ? simulation.Tick : pickedUpTick;
                    fastestWhileCarrying = System.Math.Max(fastestWhileCarrying, simulation.GetAgent(0).SpeedMillimetresPerTick);
                }
                else if (pickedUpTick >= 0)
                {
                    setDownTick = simulation.Tick;
                }
            }

            Assert.That(pickedUpTick, Is.GreaterThan(0), "They never picked the box up.");
            Assert.That(setDownTick, Is.GreaterThan(pickedUpTick), "They never set it down.");
            PhysicsObjectSnapshot placed = simulation.GetPhysicsObject(0);
            Assert.That(LogicalPosition.DistanceSquared(start, placed.Position), Is.GreaterThan(1000L * 1000L),
                "They should carry it somewhere else.");
            Assert.That(placed.SpeedMillimetresPerTick, Is.EqualTo(0), "Set down, not thrown.");

            // A 6 kg box is a third of an ordinary person's 17.5 kg limit: about 14% slower.
            int calmPace = simulation.GetAgent(0).CalmSpeedMillimetresPerTick;
            Assert.That(fastestWhileCarrying, Is.LessThan(calmPace));
            Assert.That(EventsOfType(simulation, CausalEventType.ItemThrown), Is.Empty);
        }

        [Test]
        public void TooHeavyABox_IsLeftWhereItIs()
        {
            ScenarioData data = TidierWithABox(new AgentTraitValues(0, 5, 5, 5, 5, 5), 20000);
            var simulation = new Run(data);
            for (int t = 0; t < 500; t++)
            {
                simulation.Step();
                Assert.That(simulation.GetPhysicsObject(0).IsHeld, Is.False);
            }
        }

        /// <summary>
        /// Someone else sees a fire and yells; everyone hears it. The carrier,
        /// startled, lets go of the box: <paramref name="nervousness"/> decides
        /// whether they drop it or throw it.
        /// </summary>
        [TestCase(10, CausalEventType.ItemDropped)]
        [TestCase(0, CausalEventType.ItemThrown)]
        public void StartledCarrier_DropsOrThrowsWhatTheyCarry(int nervousness, CausalEventType expected)
        {
            ScenarioData data = TidierWithABox(new AgentTraitValues(5, 5, 5, 5, 0, nervousness), 6000,
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(3000, 3000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary));
            data.Fire.ActivationTick = 150;
            data.Fire.SpawnBounds = new LogicalBounds(3000, 3000, 4000, 4000);
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            data.Hearing.YellAlarmRadiusMillimetres = 15000;
            data.Hearing.YellHearingRadiusMillimetres = 15000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;

            // The one who yells is penned in by four tables, so they cannot wander away from the fire.
            data.Tables = new[]
            {
                new TableDefinition(new SimulationId(4001UL), new LogicalPosition(3000, 3500), 1400, 200),
                new TableDefinition(new SimulationId(4002UL), new LogicalPosition(3000, 2500), 1400, 200),
                new TableDefinition(new SimulationId(4003UL), new LogicalPosition(2500, 3000), 200, 800),
                new TableDefinition(new SimulationId(4004UL), new LogicalPosition(3500, 3000), 200, 800)
            };

            // Slow to put it down, so they are still holding it when the yell comes.
            data.Items.SetDownTicks = 5000;
            var simulation = new Run(data);
            while (simulation.Tick < 149)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(0).IsHeld, Is.True, "The carrier should have the box before the fire starts.");
            for (int t = 0; t < 300 && simulation.GetPhysicsObject(0).IsHeld; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(0).IsHeld, Is.False, "Startled, they should let go.");
            List<CausalEvent> letGo = EventsOfType(simulation, expected);
            Assert.That(letGo, Has.Count.EqualTo(1));
            Assert.That(letGo[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
            Assert.That(letGo[0].TargetId, Is.EqualTo(BoxId));
            CausalEventType cause = simulation.EventLog.Get(letGo[0].CausalParentEventId).EventType;
            Assert.That(cause == CausalEventType.AgentAlerted || cause == CausalEventType.AgentScared, Is.True,
                $"Let go because of {cause}.");
            if (expected == CausalEventType.ItemThrown)
            {
                Assert.That(letGo[0].Strength, Is.GreaterThan(0), "A throw has a speed.");
            }
        }

        [Test]
        public void PanickedCrowds_ThrowThingsAround()
        {
            int hurled = 0;
            int thrownAtPeopleHit = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                var simulation = new Run(scenario.ToRuntimeData(), seed);
                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (record.EventType == CausalEventType.ItemThrown)
                    {
                        hurled++;
                        Assert.That(record.HasCausalParent, Is.True);
                    }
                    else if (record.EventType == CausalEventType.BoxHitAgent && record.HasCausalParent &&
                             simulation.EventLog.Get(record.CausalParentEventId).EventType == CausalEventType.ItemThrown)
                    {
                        thrownAtPeopleHit++;
                    }
                }
            }

            Assert.That(hurled, Is.GreaterThan(0), "Nobody ever threw anything.");
            TestContext.WriteLine($"Seeds 40-46, 60 s: {hurled} items thrown; {thrownAtPeopleHit} thrown items hit someone hard enough to count.");
        }
    }
}

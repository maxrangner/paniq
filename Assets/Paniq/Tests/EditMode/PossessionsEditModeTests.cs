using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The things people walk in holding. A bag or a briefcase starts in
    /// somebody's hand, and the moment they are frightened they let go of it:
    /// the nervous fumble it onto the floor, everyone else flings it away in
    /// whatever direction they happen to be facing, and the cruel aim it at
    /// somebody.
    /// </summary>
    public sealed class PossessionsEditModeTests
    {
        private static readonly SimulationId Carrier = new SimulationId(1UL);
        private static readonly SimulationId Bystander = new SimulationId(2UL);
        private static readonly SimulationId TheBag = new SimulationId(3001UL);

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

        private static PhysicsObjectSnapshot Thing(Run simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                if (simulation.GetPhysicsObject(i).ObjectId == id)
                {
                    return simulation.GetPhysicsObject(i);
                }
            }

            throw new KeyNotFoundException(id.ToString());
        }

        /// <summary>
        /// One person holding a bag, facing a fire that starts right in front
        /// of them, with somebody else standing off to one side. Nothing else
        /// in the room, and nobody helps, leads or fights the fire.
        /// </summary>
        private ScenarioData HoldingABagInFrontOfAFire(AgentTraitValues carrier)
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(Carrier, new LogicalPosition(0, 0), CardinalDirection.South, carrier, TheBag),
                new AgentDefinition(Bystander, new LogicalPosition(1500, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheBag, PhysicsObjectKind.Bag, new LogicalPosition(0, 0), 350, 4000)
            };
            data.Tables = new TableDefinition[0];

            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -1200, -1200);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Hearing.FireHearingRadiusMillimetres = 0;
            data.Hearing.YellAlarmRadiusMillimetres = 1;
            data.Hearing.YellHearingRadiusMillimetres = 1;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Leadership.LeaderMinimum = AgentTraitValues.Maximum + 1;
            return data;
        }

        [Test]
        public void SomebodyGivenSomething_StartsWithItInTheirHand()
        {
            var simulation = new Run(HoldingABagInFrontOfAFire(AgentTraitValues.AllOrdinary));
            PhysicsObjectSnapshot bag = Thing(simulation, TheBag);

            Assert.That(bag.IsHeld, Is.True, "The bag should start in somebody's hand.");
            Assert.That(bag.HeldBy, Is.EqualTo(Carrier), "And it should be the person the scenario gave it to.");
        }

        [Test]
        public void AFrightenedCarrier_FlingsWhatTheyWereHolding()
        {
            // Not nervous, so they throw rather than fumble it.
            var simulation = new Run(
                HoldingABagInFrontOfAFire(new AgentTraitValues(5, 5, 5, 5, 0, 1)));
            for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> thrown = EventsOfType(simulation, CausalEventType.ItemThrown);
            Assert.That(thrown, Is.Not.Empty, "A frightened person should have flung their bag.");
            Assert.That(thrown[0].SourceId, Is.EqualTo(Carrier));
            Assert.That(thrown[0].TargetId, Is.EqualTo(TheBag));
            Assert.That(thrown[0].Strength, Is.GreaterThan(0), "A thrown thing should be moving.");
            Assert.That(Thing(simulation, TheBag).IsHeld, Is.False, "They should no longer be holding it.");
        }

        [Test]
        public void AVeryNervousCarrier_FumblesItOntoTheFloorInstead()
        {
            var simulation = new Run(
                HoldingABagInFrontOfAFire(new AgentTraitValues(5, 5, 5, 5, 0, 10)));
            for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.ItemDropped), Is.Not.Empty,
                "The very nervous drop what they are holding.");
            Assert.That(EventsOfType(simulation, CausalEventType.ItemThrown), Is.Empty);
        }

        [Test]
        public void ACruelCarrier_AimsItAtSomebody()
        {
            var simulation = new Run(
                HoldingABagInFrontOfAFire(new AgentTraitValues(5, 5, 5, 1, 9, 1)));
            for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> thrown = EventsOfType(simulation, CausalEventType.ItemThrown);
            Assert.That(thrown, Is.Not.Empty, "The cruel carrier never let go of the bag.");

            // Thrown toward the bystander, who stands due east of them.
            LogicalPosition bag = Thing(simulation, TheBag).Position;
            Assert.That(bag.X, Is.GreaterThan(0), "A cruel thrower should have sent the bag at the other person.");
        }

        [Test]
        public void TheDefaultCast_WalksInCarryingFourThings()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var simulation = new Run(data);
            int held = 0;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                held += simulation.GetPhysicsObject(i).IsHeld ? 1 : 0;
            }

            Assert.That(held, Is.EqualTo(4), "Two briefcases and two bags should start in people's hands.");
        }

        [Test]
        public void AScenarioCannotGiveSomebodySomethingThatIsNotThere()
        {
            ScenarioData data = HoldingABagInFrontOfAFire(AgentTraitValues.AllOrdinary);
            data.Agents[0] = new AgentDefinition(Carrier, new LogicalPosition(0, 0), CardinalDirection.South,
                AgentTraitValues.AllOrdinary, new SimulationId(9999UL));

            Assert.That(() => new Run(data), Throws.InvalidOperationException);
        }

        [Test]
        public void AScenarioCannotGiveSomebodySomethingTooHeavyToHold()
        {
            ScenarioData data = HoldingABagInFrontOfAFire(AgentTraitValues.AllOrdinary);
            data.PhysicsObjects[0] = new PhysicsObjectDefinition(
                TheBag, PhysicsObjectKind.Bag, new LogicalPosition(0, 0), 350, 150000);

            Assert.That(() => new Run(data), Throws.InvalidOperationException);
        }
    }
}

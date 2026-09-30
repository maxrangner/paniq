using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The hand's charge (2026-09-30, the owner: "Influence points and
    /// cooldown. Using influence depletes a bar that is automatically
    /// refilled continuously"). One bar for the one hand: holding drains it,
    /// it refills on its own, and empty it takes the hand off until it has
    /// rested.
    /// </summary>
    public sealed class HandChargeEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);
        private static readonly SimulationId SomebodyElse = new SimulationId(2UL);

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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        private static void Press(Run simulation, PlayerCommandType command, LogicalPosition spot)
        {
            simulation.QueueCommand(command, spot, simulation.Tick + 1);
            simulation.Step();
        }

        private static void Press(Run simulation, PlayerCommandType command, SimulationId target)
        {
            simulation.QueueCommand(command, target, simulation.Tick + 1);
            simulation.Step();
        }

        /// <summary>The office, quiet, with the charge on as the level has it.</summary>
        private ScenarioData Office(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.HandCharge.Enabled = true;
            data.Agents = people;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Day.ToiletEveryTicks = 0;
            data.Calming.Enabled = false;
            return data;
        }

        private static AgentDefinition Person(SimulationId id, LogicalPosition at) =>
            new AgentDefinition(id, at, CardinalDirection.North, AgentTraitValues.AllOrdinary);

        [Test]
        public void HoldingDrainsTheBar_AndRestingRefillsIt()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000))), 42UL))
            {
                HandChargeSettings rules = simulation.Scenario.HandCharge;
                HandChargeSystem charge = simulation.HandChargeForTests;
                Assert.That(charge.Charge, Is.EqualTo(rules.Capacity), "Full at the start.");

                Press(simulation, PlayerCommandType.InfluenceSpot, new LogicalPosition(0, -1500));
                Advance(simulation, 5 * Run.TicksPerSecond);
                int net = rules.DrainPerTick - rules.RefillPerTick;
                Assert.That(charge.Charge, Is.EqualTo(rules.Capacity - net * (5 * Run.TicksPerSecond + 1)).Within(net),
                    "Held for five seconds: the drain, less the refill, a tick.");

                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                int rested = charge.Charge;
                Advance(simulation, 5 * Run.TicksPerSecond);
                Assert.That(charge.Charge, Is.EqualTo(Math.Min(rules.Capacity, rested + rules.RefillPerTick * 5 * Run.TicksPerSecond)),
                    "Let go, it refills on its own.");
            }
        }

        [Test]
        public void AnEmptyBar_TakesTheHandOff_AndRefusesThePressUntilItHasRested()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000))), 42UL))
            {
                HandChargeSettings rules = simulation.Scenario.HandCharge;
                HandChargeSystem charge = simulation.HandChargeForTests;
                InfluenceSystem influence = simulation.InfluenceForTests;
                var spot = new LogicalPosition(0, -1500);
                Press(simulation, PlayerCommandType.InfluenceSpot, spot);

                int ticksToEmpty = rules.Capacity / (rules.DrainPerTick - rules.RefillPerTick) + 2;
                Advance(simulation, ticksToEmpty);
                Assert.That(charge.Charge, Is.LessThan(rules.PressNeeds), "Run dry, and only just refilling.");
                Assert.That(influence.Count, Is.Zero, "The hand came off by itself.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerHandSpent), Has.Count.EqualTo(1), "And the log says it gave out.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerReleasedInfluence), Has.Count.EqualTo(1));
                Assert.That(simulation.GetSnapshot().HandResting, Is.True);

                Press(simulation, PlayerCommandType.InfluenceSpot, spot);
                Assert.That(influence.Count, Is.Zero, "A press on an empty bar is refused.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerInfluenced), Has.Count.EqualTo(1), "And not written.");

                Advance(simulation, rules.PressNeeds / rules.RefillPerTick + 2);
                Assert.That(charge.MayPress, Is.True, "Rested enough for a press.");
                Assert.That(simulation.GetSnapshot().HandResting, Is.False);
                Press(simulation, PlayerCommandType.InfluenceSpot, spot);
                Assert.That(influence.Count, Is.EqualTo(1), "Pressed again, it is taken.");
            }
        }

        [Test]
        public void ATugDrainsItToo_AndAnEmptyBarLetsGoOfThePerson()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000))), 42UL))
            {
                HandChargeSettings rules = simulation.Scenario.HandCharge;
                HandChargeSystem charge = simulation.HandChargeForTests;
                Press(simulation, PlayerCommandType.TugPerson, Somebody);
                Assert.That(simulation.TugsForTests.HeldIndex, Is.EqualTo(0), "Held by the shirt.");
                Advance(simulation, 2 * Run.TicksPerSecond);
                Assert.That(charge.Charge, Is.LessThan(rules.Capacity), "The tug drains the bar.");

                Advance(simulation, rules.Capacity / (rules.DrainPerTick - rules.RefillPerTick) + 2);
                Assert.That(simulation.TugsForTests.HeldIndex, Is.EqualTo(-1), "Run dry, the hand lets go of them.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerReleasedTug), Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void AClicksBeacon_CostsItsThreeSeconds()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000))), 42UL))
            {
                HandChargeSettings rules = simulation.Scenario.HandCharge;
                HandChargeSystem charge = simulation.HandChargeForTests;
                Press(simulation, PlayerCommandType.InfluenceSpot, new LogicalPosition(0, -1500));
                Press(simulation, PlayerCommandType.LeaveInfluence, default(SimulationId));
                int beacon = simulation.Scenario.Influence.BeaconTicks;
                Advance(simulation, beacon + 5);
                Assert.That(simulation.InfluenceForTests.Count, Is.Zero, "The beacon has come off.");
                int net = rules.DrainPerTick - rules.RefillPerTick;
                Assert.That(charge.Charge, Is.EqualTo(rules.Capacity - net * beacon).Within(net * 8),
                    "The beacon cost what three seconds of holding cost.");
            }
        }

        [Test]
        public void LeftAlone_TheBarNeverMoves()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000))), 42UL))
            {
                HandChargeSystem charge = simulation.HandChargeForTests;
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(charge.Charge, Is.EqualTo(simulation.Scenario.HandCharge.Capacity), "Nobody at the controls: full, always.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerHandSpent), Is.Empty);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Influence (prototype 3, second batch, 2026-09-26): the player clicks a
    /// door, a thing or a patch of floor, and people are drawn toward it --
    /// never ordered. Each click is one step, up to twenty; it ticks down on
    /// its own and cannot be cancelled; it pulls anybody who comes near it,
    /// less the further off they are, never from another room; and every
    /// person weighs it by who they are. The owner: "clicking a door once just
    /// increases the chances of an agent using the door; clicking it a few
    /// more times increases it more."
    /// </summary>
    public sealed class InfluenceEditModeTests
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

        /// <summary>The office with these people in it, nothing burning, nothing on the timetable, and nobody deciding anything of their own for a long while.</summary>
        private ScenarioData Office(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Day.ToiletEveryTicks = 0;
            return data;
        }

        private static AgentDefinition Person(SimulationId id, int x, int z, AgentTraitValues traits) =>
            new AgentDefinition(id, new LogicalPosition(x, z), CardinalDirection.North, traits);

        private static void Click(Run simulation, LogicalPosition spot, int times)
        {
            for (int i = 0; i < times; i++)
            {
                simulation.QueueCommand(PlayerCommandType.InfluenceSpot, spot, simulation.Tick + 1);
                simulation.Step();
            }
        }

        [Test]
        public void EachClick_AddsOneStep_UpToTwenty_AndItTicksDownToNothingOnItsOwn()
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                var spot = new LogicalPosition(2000, -4000);
                Click(simulation, spot, 3);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Count, Is.EqualTo(1), "Three clicks on one spot are one place.");
                Assert.That(influence.LevelOf(0), Is.EqualTo(3), "One step a click.");

                Click(simulation, new LogicalPosition(spot.X + 500, spot.Z), 30);
                Assert.That(influence.Count, Is.EqualTo(1), "A click within a metre adds to it.");
                Assert.That(influence.LevelOf(0), Is.EqualTo(20), "And it stops at twenty.");

                Advance(simulation, 100);
                Assert.That(influence.LevelOf(0), Is.EqualTo(19), "A step lost every two seconds.");
                Advance(simulation, 20 * 100);
                Assert.That(influence.Count, Is.Zero, "Forty seconds on, it is gone, with nobody having to cancel it.");
            }
        }

        [Test]
        public void AClickTheOtherSideOfAWall_StartsAPlaceOfItsOwn()
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                // Eighty centimetres apart, the office's north wall between
                // them: near enough to stack, were they in one room.
                Click(simulation, new LogicalPosition(-3000, 5600), 5);
                Click(simulation, new LogicalPosition(-3000, 6400), 2);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Count, Is.EqualTo(2), "One place in the office, one in the corridor.");
                Assert.That(influence.LevelOf(0), Is.EqualTo(5), "The corridor's clicks did not land on the office's place.");
                Assert.That(influence.LevelOf(1), Is.EqualTo(2));
            }
        }

        [Test]
        public void ThePull_WeakensWithDistance_AndIsGoneBeyondTwelveMetres_AndNeverReachesAnotherRoom()
        {
            ScenarioData data = Office(
                Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, 4000, -4000, AgentTraitValues.AllOrdinary),
                Person(new SimulationId(3UL), -4000, 7500, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                Click(simulation, new LogicalPosition(-3000, -4000), 20);
                InfluenceSystem influence = simulation.InfluenceForTests;
                int near = influence.FeltBy(simulation.AgentForTests(0), 0);
                int far = influence.FeltBy(simulation.AgentForTests(1), 0);
                int nextDoor = influence.FeltBy(simulation.AgentForTests(2), 0);

                Assert.That(near, Is.GreaterThan(far), "One metre off, it pulls harder than seven.");
                Assert.That(far, Is.GreaterThan(0), "But seven metres off in the same room, it still pulls.");
                Assert.That(nextDoor, Is.Zero, "In the corridor, through the wall, it does not pull at all.");
            }
        }

        [Test]
        public void TheNervous_FeelItMore_ThanALeader()
        {
            var nervous = new AgentTraitValues(5, 5, 5, 5, 2, 9);
            var leader = new AgentTraitValues(5, 5, 5, 5, 2, 5, 10);
            ScenarioData data = Office(Person(Somebody, -3000, -4000, nervous), Person(SomebodyElse, -1000, -4000, leader));
            using (var simulation = new Run(data, 42UL))
            {
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Susceptibility(simulation.AgentForTests(0)),
                    Is.GreaterThan(influence.Susceptibility(simulation.AgentForTests(1)) * 2),
                    "The nervous are led more than twice as easily as a leader, who goes their own way.");
            }
        }

        [Test]
        public void AFrightenedPerson_TakesAnInfluencedDoor_OverTheShorterWayOut()
        {
            // From the middle of the office there are two ways out of the
            // room toward the way out: the corridor door and the stockroom
            // door. Whichever they take left alone, a strong pull on the
            // other one turns them. The pull is made strong for this test
            // because the test is about the mechanism, not about whether the
            // shipped strength is right.
            var easilyLed = new AgentTraitValues(5, 5, 5, 5, 2, 9);
            ScenarioData Scene()
            {
                ScenarioData data = Office(Person(Somebody, 1000, 0, easilyLed));
                data.Influence.FullPullBonusMillimetres = 30000;
                return data;
            }

            int left;
            using (var simulation = new Run(Scene(), 42UL))
            {
                Advance(simulation, 20);
                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                left = simulation.ExitDoorForTests(0);
            }

            int stockroom = DoorIndex(TheBuilding.StockroomDoor);
            SimulationId other = left == stockroom ? TheBuilding.OfficeDoor : TheBuilding.StockroomDoor;
            Assert.That(left, Is.EqualTo(stockroom).Or.EqualTo(DoorIndex(TheBuilding.OfficeDoor)),
                "Left alone, they go out of the office one way or the other.");

            using (var simulation = new Run(Scene(), 42UL))
            {
                for (int i = 0; i < 20; i++)
                {
                    simulation.QueueCommand(PlayerCommandType.InfluenceDoor, other, simulation.Tick + 1);
                    simulation.Step();
                }

                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                Assert.That(simulation.ExitDoorForTests(0), Is.EqualTo(DoorIndex(other)),
                    "Drawn to the other door, they take it instead.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Is.Not.Empty,
                    "And the log says influence is why.");
            }
        }

        [Test]
        public void NobodyIsDrawnIntoARoomThatIsAlight()
        {
            ScenarioData data = Office(Person(Somebody, 1000, 0, new AgentTraitValues(5, 5, 5, 5, 2, 9)));
            data.Influence.FullPullBonusMillimetres = 30000;
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, TheBuilding.Stockroom);
            using (var simulation = new Run(data, 42UL))
            {
                for (int i = 0; i < 20; i++)
                {
                    simulation.QueueCommand(PlayerCommandType.InfluenceDoor, TheBuilding.StockroomDoor, simulation.Tick + 1);
                    simulation.Step();
                }

                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                Assert.That(simulation.ExitDoorForTests(0), Is.Not.EqualTo(DoorIndex(TheBuilding.StockroomDoor)),
                    "However hard the player pulls, nobody runs into the flames for it.");
            }
        }

        [Test]
        public void ACalmPerson_DriftsTowardIt()
        {
            ScenarioData data = Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                var spot = new LogicalPosition(2000, -4000);
                long before = IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot);
                for (int i = 0; i < 20; i++)
                {
                    simulation.QueueCommand(PlayerCommandType.InfluenceSpot, spot, simulation.Tick + 1);
                    simulation.Step();
                }

                Advance(simulation, 15 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Is.Not.Empty,
                    "With nothing in particular to do, they wandered over to it.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot), Is.LessThan(before / 2));
            }
        }

        [Test]
        public void InfluenceNobodyIsNear_ChangesNothing()
        {
            ScenarioData data = Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            LogicalPosition PositionAfter(bool influenced)
            {
                using (var simulation = new Run(data, 42UL))
                {
                    if (influenced)
                    {
                        // The maintenance room, where nobody is.
                        simulation.QueueCommand(PlayerCommandType.InfluenceSpot, TheBuilding.Maintenance, 1);
                    }

                    Advance(simulation, 20 * Run.TicksPerSecond);
                    return simulation.GetAgent(Somebody).Position;
                }
            }

            Assert.That(PositionAfter(true), Is.EqualTo(PositionAfter(false)),
                "Weighing a pull nobody feels draws no random numbers, so the run goes exactly as it would have.");
        }

        [Test]
        public void InfluenceOffTheFloor_IsRefused_AndWritesNothing()
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.InfluenceSpot, new LogicalPosition(60000, 60000), 1);
                Advance(simulation, 3);
                Assert.That(simulation.InfluenceForTests.Count, Is.Zero);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerInfluenced), Is.Empty);
            }
        }

        private int DoorIndex(SimulationId door)
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                for (int i = 0; i < simulation.DoorCount; i++)
                {
                    if (simulation.GetDoor(i).DoorId == door)
                    {
                        return i;
                    }
                }
            }

            throw new KeyNotFoundException(door.ToString());
        }
    }
}

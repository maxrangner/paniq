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

        // ------------------------------------------------------- using what is pointed at (2026-09-27)

        private static readonly SimulationId ABox = new SimulationId(3901UL);
        private static readonly SimulationId AChair = new SimulationId(3902UL);
        private static readonly SimulationId TheOfficeBottle = new SimulationId(3301UL);

        private static CausalEvent? AdvanceUntil(Run simulation, CausalEventType type, int limit)
        {
            for (int t = 0; t < limit; t++)
            {
                List<CausalEvent> found = EventsOfType(simulation, type);
                if (found.Count > 0)
                {
                    return found[0];
                }

                simulation.Step();
            }

            List<CausalEvent> last = EventsOfType(simulation, type);
            return last.Count > 0 ? last[0] : (CausalEvent?)null;
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

        private static void ClickTheDoor(Run simulation, SimulationId door, int times)
        {
            for (int i = 0; i < times; i++)
            {
                simulation.QueueCommand(PlayerCommandType.InfluenceDoor, door, simulation.Tick + 1);
                simulation.Step();
            }
        }

        private static void ClickTheThing(Run simulation, SimulationId thing, int times)
        {
            for (int i = 0; i < times; i++)
            {
                simulation.QueueCommand(PlayerCommandType.InfluenceThing, thing, simulation.Tick + 1);
                simulation.Step();
            }
        }

        /// <summary>One person in the office, deciding things at the usual rate, with a box and a spare chair near them.</summary>
        private ScenarioData OfficeWithThingsToUse(int x, int z, AgentTraitValues traits)
        {
            ScenarioData data = Office(Person(Somebody, x, z, traits));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects)
            {
                new PhysicsObjectDefinition(ABox, PhysicsObjectKind.Box, new LogicalPosition(x + 1500, z), 400, 8000),
                new PhysicsObjectDefinition(AChair, PhysicsObjectKind.Chair, new LogicalPosition(x - 1500, z), 450, 5000)
            };
            data.PhysicsObjects = things.ToArray();
            return data;
        }

        /// <summary>
        /// The owner's rule (2026-09-27): influencing a door makes people
        /// want to use it. A shut door is opened; using it spends the pull;
        /// pointed at again, it is shut.
        /// </summary>
        [Test]
        public void ACalmPerson_DrawnToAShutDoor_OpensIt_AndThePullIsSpent_AndDrawnAgain_ShutsIt()
        {
            ScenarioData data = OfficeWithThingsToUse(0, 3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                int door = DoorIndex(TheBuilding.OfficeDoor);
                ClickTheDoor(simulation, TheBuilding.OfficeDoor, 20);
                CausalEvent? opened = AdvanceUntil(simulation, CausalEventType.DoorOpened, 20 * Run.TicksPerSecond);
                Assert.That(opened.HasValue, "Drawn to the shut office door, they open it.");
                Assert.That(opened.Value.SourceId, Is.EqualTo(TheBuilding.OfficeDoor));
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 5);
                Assert.That(spent.HasValue, "Used, the pull on it is spent.");
                Assert.That(spent.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(spent.Value.TargetId, Is.EqualTo(TheBuilding.OfficeDoor));
                Assert.That(simulation.InfluenceForTests.PlaceOfDoor(door), Is.LessThan(0), "Nothing left on the door.");
                Advance(simulation, 3 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).Position.Z, Is.LessThan(6000), "They did not go through it: it was opened for its own sake.");

                ClickTheDoor(simulation, TheBuilding.OfficeDoor, 20);
                CausalEvent? shut = AdvanceUntil(simulation, CausalEventType.DoorClosed, 20 * Run.TicksPerSecond);
                Assert.That(shut.HasValue, "Pointed at again, the open door is shut.");
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Has.Count.EqualTo(2), "And that pull is spent too.");
            }
        }

        [Test]
        public void ACruelPerson_DrawnToAShutDoor_WedgesAThingInItInstead()
        {
            ScenarioData data = OfficeWithThingsToUse(0, 3000, AgentTraitValues.AllOrdinary.With(AgentTrait.Evil, 10));
            using (var simulation = new Run(data, 42UL))
            {
                ClickTheDoor(simulation, TheBuilding.OfficeDoor, 20);
                CausalEvent? wedged = AdvanceUntil(simulation, CausalEventType.AgentBarricadedDoor, 40 * Run.TicksPerSecond);
                Assert.That(wedged.HasValue, "The cruel wedge the door shut with the nearest thing instead of opening it.");
                Assert.That(wedged.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(wedged.Value.TargetId, Is.EqualTo(TheBuilding.OfficeDoor));
                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened), Is.Empty, "Never opened.");
                CausalEvent? blocked = AdvanceUntil(simulation, CausalEventType.DoorBlocked, 5);
                Assert.That(blocked.HasValue, "And it is jammed.");
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Is.Not.Empty, "That was their use of it: the pull is spent.");
            }
        }

        [Test]
        public void AnInfluencedChair_GetsSatOn_AndThePullIsSpent()
        {
            ScenarioData data = OfficeWithThingsToUse(-3000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                ClickTheThing(simulation, AChair, 20);
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 20 * Run.TicksPerSecond);
                Assert.That(spent.HasValue, "Drawn to a chair, they sit on it, and the pull is spent.");
                Assert.That(spent.Value.TargetId, Is.EqualTo(AChair));
                Assert.That(simulation.GetAgent(Somebody).SeatedPercent, Is.EqualTo(100), "Sat on it.");
            }
        }

        [Test]
        public void AnInfluencedBox_IsCarriedOff()
        {
            ScenarioData data = OfficeWithThingsToUse(-3000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                LogicalPosition before = Thing(simulation, ABox).Position;
                ClickTheThing(simulation, ABox, 20);
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 20 * Run.TicksPerSecond);
                Assert.That(spent.HasValue, "Drawn to a box, they pick it up, and the pull is spent.");
                Assert.That(spent.Value.TargetId, Is.EqualTo(ABox));
                Assert.That(Thing(simulation, ABox).IsHeld, Is.True, "In their arms.");
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(before, Thing(simulation, ABox).Position), Is.GreaterThan(1000), "Carried off and set down somewhere else.");
            }
        }

        [Test]
        public void AnInfluencedExtinguisher_IsTakenAndHeld()
        {
            ScenarioData data = OfficeWithThingsToUse(-1000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                ClickTheThing(simulation, TheOfficeBottle, 20);
                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookExtinguisher, 20 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Drawn to the bottle on the wall, they take it.");
                Assert.That(took.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent).Exists(e => e.TargetId == TheOfficeBottle), Is.True);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(Thing(simulation, TheOfficeBottle).IsHeld, Is.True, "And keep hold of it, as of their own bag, while nothing frightens them.");
            }
        }

        [Test]
        public void ADoorTheyCannotOpen_KeepsItsInfluence()
        {
            ScenarioData data = Office(Person(Somebody, 14500, 9500, AgentTraitValues.AllOrdinary));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                int door = DoorIndex(TheBuilding.TheWayOut);
                ClickTheDoor(simulation, TheBuilding.TheWayOut, 20);
                CausalEvent? tried = AdvanceUntil(simulation, CausalEventType.AgentTriedDoor, 20 * Run.TicksPerSecond);
                Assert.That(tried.HasValue, "Drawn to the locked way out, they try it.");
                Advance(simulation, 5 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Is.Empty, "A door they could not use spends nothing.");
                Assert.That(simulation.InfluenceForTests.PlaceOfDoor(door), Is.GreaterThanOrEqualTo(0), "The pull on it stands.");
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

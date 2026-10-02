using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Influence (prototype 3, second batch, 2026-09-26; a hold since
    /// 2026-09-29): the player's hand on a door, a thing or a patch of floor,
    /// and people are drawn toward it -- never ordered. The owner's rule for
    /// the hold: "when you interact the influence is clear and instant, but
    /// as soon as you let go the agents are on their own." So a press is a
    /// full pull at once, one place at a time, and a release takes it away
    /// at once; it reaches about a room's length, through an open doorway
    /// but never a wall; and every person weighs it by who they are.
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

        /// <summary>The hand goes on a spot on the floor, and the tick that takes it runs.</summary>
        private static void HoldTheFloor(Run simulation, LogicalPosition spot)
        {
            simulation.QueueCommand(PlayerCommandType.InfluenceSpot, spot, simulation.Tick + 1);
            simulation.Step();
        }

        private static void HoldTheDoor(Run simulation, SimulationId door)
        {
            simulation.QueueCommand(PlayerCommandType.InfluenceDoor, door, simulation.Tick + 1);
            simulation.Step();
        }

        private static void HoldTheThing(Run simulation, SimulationId thing)
        {
            simulation.QueueCommand(PlayerCommandType.InfluenceThing, thing, simulation.Tick + 1);
            simulation.Step();
        }

        private static void LetGo(Run simulation)
        {
            simulation.QueueCommand(PlayerCommandType.ReleaseInfluence, default(SimulationId), simulation.Tick + 1);
            simulation.Step();
        }

        // ------------------------------------------------------------------ the hand (2026-09-29)

        [Test]
        public void APress_IsAFullPullAtOnce_AndARelease_TakesItAwayAtOnce()
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                var spot = new LogicalPosition(2000, -4000);
                HoldTheFloor(simulation, spot);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Count, Is.EqualTo(1), "The hand is on one place.");
                Assert.That(influence.LevelOf(0), Is.EqualTo(simulation.Scenario.Influence.MaximumLevel), "Full, the moment it is pressed.");

                Advance(simulation, 60 * Run.TicksPerSecond);
                Assert.That(influence.Count, Is.EqualTo(1), "A minute on, still held and still full: nothing fades.");
                Assert.That(influence.LevelOf(0), Is.EqualTo(simulation.Scenario.Influence.MaximumLevel));

                LetGo(simulation);
                Assert.That(influence.Count, Is.Zero, "Let go, and it is gone at once.");
                List<CausalEvent> released = EventsOfType(simulation, CausalEventType.PowerReleasedInfluence);
                Assert.That(released, Has.Count.EqualTo(1), "And written down, with how long it was held.");
                Assert.That(released[0].Strength, Is.GreaterThan(60 * Run.TicksPerSecond - 5));
            }
        }

        [Test]
        public void OneHand_APressElsewhere_ReplacesThePlaceHeldBefore()
        {
            using (var simulation = new Run(Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary)), 42UL))
            {
                HoldTheFloor(simulation, new LogicalPosition(2000, -4000));
                HoldTheFloor(simulation, new LogicalPosition(-3000, 2000));
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Count, Is.EqualTo(1), "You cannot be everywhere at once: one place, the last one pressed.");
                Assert.That(influence[0].At, Is.EqualTo(new LogicalPosition(-3000, 2000)));

                HoldTheDoor(simulation, TheBuilding.OfficeDoor);
                Assert.That(influence.Count, Is.EqualTo(1));
                Assert.That(influence[0].Target, Is.EqualTo(TheBuilding.OfficeDoor), "A door pressed replaces the spot.");
            }
        }

        [Test]
        public void ThePull_WeakensWithDistance_AndIsGoneBeyondAboutARoomsLength()
        {
            ScenarioData data = Office(
                Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, 4000, -4000, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                HoldTheFloor(simulation, new LogicalPosition(-3000, -4000));
                InfluenceSystem influence = simulation.InfluenceForTests;
                int near = influence.FeltBy(simulation.AgentForTests(0), 0);
                int far = influence.FeltBy(simulation.AgentForTests(1), 0);

                Assert.That(near, Is.GreaterThan(far), "One metre off, it pulls harder than seven.");
                Assert.That(far, Is.GreaterThan(0), "But seven metres off in the same room, it still pulls.");
            }
        }

        /// <summary>
        /// The owner's rules: "through open doors, but limit range to be
        /// around a room's length" (2026-09-29), and "influence should travel
        /// mostly through line of sight ... if a wall is in between it should
        /// cut off. Maybe keep a slight gradient" (2026-10-02). Through a
        /// wall, nothing; through a shut door, nothing; through an open one,
        /// only where the hand can be seen through the gap: in full straight
        /// through it, fading as the line misses the frame, nothing a stride
        /// past it.
        /// </summary>
        [Test]
        public void ThePull_ReachesThroughAnOpenDoor_OnlyWhereItCanBeSeen_AndNeverThroughAWallOrAShutOne()
        {
            // In the corridor, a metre and a half north of the office's north
            // wall, straight out from the office door at (0, 6000).
            ScenarioData data = Office(Person(Somebody, 0, 7500, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                var straightInsideTheDoor = new LogicalPosition(0, 5000);
                HoldTheFloor(simulation, straightInsideTheDoor);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Agent somebody = simulation.AgentForTests(0);
                Assert.That(influence.FeltBy(somebody, 0), Is.Zero,
                    "The office door is shut: two and a half metres away through it, they feel nothing.");

                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.OfficeDoor, simulation.Tick + 1);
                simulation.Step();
                Assert.That(simulation.GetDoor(DoorIndex(TheBuilding.OfficeDoor)).State, Is.EqualTo(DoorState.Open), "The player walked the office door open.");
                HoldTheFloor(simulation, straightInsideTheDoor);
                int inLine = influence.FeltBy(somebody, 0);
                Assert.That(inLine, Is.GreaterThan(0), "The door open and the hand in line with the gap: the pull reaches them through the doorway.");

                // The line from them to a spot 1.5 m east of the door's line
                // crosses the wall 0.9 m from the door's centre: 0.4 m past
                // the half-metre frame, inside the soft edge.
                HoldTheFloor(simulation, new LogicalPosition(1500, 5000));
                int atTheEdge = influence.FeltBy(somebody, 0);
                Assert.That(atTheEdge, Is.GreaterThan(0), "A hand just past the frame still shows faintly.");
                Assert.That(atTheEdge, Is.LessThan(inLine), "But fainter than one straight through the gap.");

                // 2.5 m east: the line crosses the wall 1.5 m from the door's
                // centre, a metre past the frame -- beyond the soft edge.
                HoldTheFloor(simulation, new LogicalPosition(2500, 5000));
                Assert.That(influence.FeltBy(somebody, 0), Is.Zero,
                    "A hand a stride past the frame is hidden by the wall beside the open door, as by any wall.");

                HoldTheFloor(simulation, new LogicalPosition(0, 7000));
                Assert.That(influence.FeltBy(somebody, 0), Is.GreaterThanOrEqualTo(inLine),
                    "A spot in their own room, half a metre off, pulls at least as hard as one a short walk away through the door (both inside the full-pull half).");

                HoldTheFloor(simulation, TheBuilding.OfficeFarCorner);
                Assert.That(influence.FeltBy(somebody, 0), Is.Zero, "The far corner of the office: out of sight and out of reach.");
            }
        }

        [Test]
        public void WithNoSoftEdge_ADoorwayCutsLikeAWall()
        {
            ScenarioData data = Office(Person(Somebody, 0, 7500, AgentTraitValues.AllOrdinary));
            data.Influence.DoorwaySightSoftEdgeMillimetres = 0;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.OfficeDoor, simulation.Tick + 1);
                simulation.Step();
                InfluenceSystem influence = simulation.InfluenceForTests;
                HoldTheFloor(simulation, new LogicalPosition(0, 5000));
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.GreaterThan(0), "Straight through the gap: felt.");
                HoldTheFloor(simulation, new LogicalPosition(1500, 5000));
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.Zero, "A hand's breadth past the frame with no soft edge: nothing.");
            }
        }

        [Test]
        public void TheDoorwayMiss_IsZeroThroughTheGap_TheOvershootBeside_AndNothingWhenTheWallIsNotCrossed()
        {
            ScenarioData data = Office(Person(Somebody, 0, 7500, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                int door = DoorIndex(TheBuilding.OfficeDoor);
                var inTheCorridor = new LogicalPosition(0, 7500);
                Assert.That(geometry.DoorwayMissMillimetres(door, inTheCorridor, new LogicalPosition(0, 5000)), Is.Zero,
                    "Straight through the metre-wide gap.");
                Assert.That(geometry.DoorwayMissMillimetres(door, inTheCorridor, new LogicalPosition(1500, 5000)), Is.EqualTo(400),
                    "Crossing the wall 900 mm from the door's centre is 400 mm past its half-metre frame.");
                Assert.That(geometry.DoorwayMissMillimetres(door, inTheCorridor, new LogicalPosition(3000, 7000)), Is.EqualTo(long.MaxValue),
                    "Two spots in the corridor: the line never crosses the office's wall at all.");
            }
        }

        /// <summary>
        /// The lines drawn to people are for who feels the hand right now
        /// (2026-10-02, the owner's note that the lines showed far more than
        /// the reach). Somebody keeping at an earlier ask from out of reach
        /// still has their gold hand, but no line.
        /// </summary>
        [Test]
        public void TheSnapshot_ListsOnlyWhoFeelsTheHandNow()
        {
            ScenarioData data = Office(
                Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, 2000, 7500, AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                InfluenceSystem influence = simulation.InfluenceForTests;
                HoldTheFloor(simulation, new LogicalPosition(-3000, -4000));
                Advance(simulation, 3 * Run.TicksPerSecond);
                Agent somebody = simulation.AgentForTests(0);
                Assert.That(somebody.Hand.Conviction, Is.GreaterThanOrEqualTo(data.Influence.CommitFromPerMille),
                    "Three seconds beside the hand: they have taken it in past the commit line.");

                // The hand moves out into the corridor, behind the shut office
                // door and out of their reach; the other person is beside it
                // now, and the first keeps at what they were asked.
                HoldTheFloor(simulation, new LogicalPosition(3500, 7500));
                Advance(simulation, 2);
                Assert.That(influence.IsCommitted(somebody), Is.True, "Out of the new hand's reach, they keep the old ask.");
                RunSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.InfluencePulls.All(p => p.FeltPerMille > 0), "Every line is to somebody feeling the hand.");
                Assert.That(snapshot.InfluencePulls.Any(p => p.AgentId == SomebodyElse), "The person beside the hand has a line.");
                Assert.That(influence.FeltBy(somebody, 0), Is.Zero, "The first person is out of its reach now.");
                Assert.That(snapshot.InfluencePulls.Any(p => p.AgentId == Somebody), Is.False,
                    "No line to somebody out of reach, whatever they are still keeping at.");
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
        public void AFrightenedPerson_TakesAHeldDoor_OverTheShorterWayOut()
        {
            // From the middle of the office there are two ways out of the
            // room toward the way out: the corridor door and the stockroom
            // door. Whichever they take left alone, the hand on the other one
            // turns them. The pull is made strong for this test because the
            // test is about the mechanism, not about whether the shipped
            // strength is right.
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
                HoldTheDoor(simulation, other);
                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                Assert.That(simulation.ExitDoorForTests(0), Is.EqualTo(DoorIndex(other)),
                    "Drawn to the other door, they take it instead.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Is.Not.Empty,
                    "And the log says the hand is why.");
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
                HoldTheDoor(simulation, TheBuilding.StockroomDoor);
                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                Assert.That(simulation.ExitDoorForTests(0), Is.Not.EqualTo(DoorIndex(TheBuilding.StockroomDoor)),
                    "However hard the player pulls, nobody runs into the flames for it.");
            }
        }

        /// <summary>
        /// Since the fourth pass (2026-09-30) a hand held long enough is kept
        /// as a goal when it comes off: they stay at it a while, sure of it,
        /// and drift off only as that fades. Nothing draws them afresh.
        /// </summary>
        [Test]
        public void ACalmPerson_DriftsTowardIt_KeepsAtItOnceItIsLetGoOf_AndDriftsOffAsItFades()
        {
            ScenarioData data = Office(Person(Somebody, -4000, -4000, AgentTraitValues.AllOrdinary));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                var spot = new LogicalPosition(2000, -4000);
                long before = IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot);
                HoldTheFloor(simulation, spot);
                Advance(simulation, 15 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Is.Not.Empty,
                    "With nothing in particular to do, they wandered over to it.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot), Is.LessThan(before / 2));

                LetGo(simulation);
                int drawnBefore = EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence).Count;
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True, "Fifteen seconds of a full pull: they are sure of it.");
                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot), Is.LessThan(2500),
                    "Ten seconds on, they are still where the hand was.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Has.Count.EqualTo(drawnBefore),
                    "Let go of, nothing draws them afresh.");
                Advance(simulation, 60 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.False, "A minute on, the goal has faded.");
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
        /// The owner's rule (2026-09-27): the hand on a door makes people want
        /// to use it. A shut door is opened; using it spends the use, and the
        /// hand goes on gathering people there; pressed afresh, it is shut.
        /// </summary>
        [Test]
        public void ACalmPerson_DrawnToAShutDoor_OpensIt_AndTheUseIsSpent_AndPressedAfresh_ShutsIt()
        {
            ScenarioData data = OfficeWithThingsToUse(0, 3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                int door = DoorIndex(TheBuilding.OfficeDoor);
                HoldTheDoor(simulation, TheBuilding.OfficeDoor);
                CausalEvent? opened = AdvanceUntil(simulation, CausalEventType.DoorOpened, 20 * Run.TicksPerSecond);
                Assert.That(opened.HasValue, "Drawn to the shut office door, they open it.");
                Assert.That(opened.Value.SourceId, Is.EqualTo(TheBuilding.OfficeDoor));
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 5);
                Assert.That(spent.HasValue, "Used, the use is spent.");
                Assert.That(spent.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(spent.Value.TargetId, Is.EqualTo(TheBuilding.OfficeDoor));
                Assert.That(simulation.InfluenceForTests.PlaceOfDoor(door), Is.LessThan(0), "Nothing left to use on the door.");
                Assert.That(simulation.InfluenceForTests.Count, Is.EqualTo(1), "But the hand is still on it, gathering.");
                Advance(simulation, 3 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).Position.Z, Is.LessThan(6000), "They did not go through it: it was opened for its own sake.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorClosed), Is.Empty, "And nobody shuts it again while the hand stays.");

                HoldTheDoor(simulation, TheBuilding.OfficeDoor);
                CausalEvent? shut = AdvanceUntil(simulation, CausalEventType.DoorClosed, 20 * Run.TicksPerSecond);
                Assert.That(shut.HasValue, "Pressed afresh, the open door is shut.");
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Has.Count.EqualTo(2), "And that use is spent too.");
            }
        }

        [Test]
        public void ACruelPerson_DrawnToAShutDoor_WedgesAThingInItInstead()
        {
            // Cruel, but not so strong-willed as to refuse the hand (nine and
            // up do, since 2026-09-30).
            ScenarioData data = OfficeWithThingsToUse(0, 3000, AgentTraitValues.AllOrdinary.With(AgentTrait.Evil, 8));
            using (var simulation = new Run(data, 42UL))
            {
                HoldTheDoor(simulation, TheBuilding.OfficeDoor);
                CausalEvent? wedged = AdvanceUntil(simulation, CausalEventType.AgentBarricadedDoor, 40 * Run.TicksPerSecond);
                Assert.That(wedged.HasValue, "The cruel wedge the door shut with the nearest thing instead of opening it.");
                Assert.That(wedged.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(wedged.Value.TargetId, Is.EqualTo(TheBuilding.OfficeDoor));
                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened), Is.Empty, "Never opened.");
                CausalEvent? blocked = AdvanceUntil(simulation, CausalEventType.DoorBlocked, 5);
                Assert.That(blocked.HasValue, "And it is jammed.");
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Is.Not.Empty, "That was their use of it: the use is spent.");
            }
        }

        [Test]
        public void AHeldChair_GetsSatOn_AndTheUseIsSpent()
        {
            ScenarioData data = OfficeWithThingsToUse(-3000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                HoldTheThing(simulation, AChair);
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 20 * Run.TicksPerSecond);
                Assert.That(spent.HasValue, "Drawn to a chair, they sit on it, and the use is spent.");
                Assert.That(spent.Value.TargetId, Is.EqualTo(AChair));
                Assert.That(simulation.GetAgent(Somebody).SeatedPercent, Is.EqualTo(100), "Sat on it.");
            }
        }

        [Test]
        public void AHeldBox_IsCarriedOff()
        {
            ScenarioData data = OfficeWithThingsToUse(-3000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                LogicalPosition before = Thing(simulation, ABox).Position;
                HoldTheThing(simulation, ABox);
                CausalEvent? spent = AdvanceUntil(simulation, CausalEventType.InfluenceSpent, 20 * Run.TicksPerSecond);
                Assert.That(spent.HasValue, "Drawn to a box, they pick it up, and the use is spent.");
                Assert.That(spent.Value.TargetId, Is.EqualTo(ABox));
                Assert.That(Thing(simulation, ABox).IsHeld, Is.True, "In their arms.");
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(before, Thing(simulation, ABox).Position), Is.GreaterThan(1000), "Carried off and set down somewhere else.");
            }
        }

        [Test]
        public void AHeldExtinguisher_IsTakenAndHeld_ByTheTimid()
        {
            ScenarioData data = OfficeWithThingsToUse(-1000, -3000, AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                HoldTheThing(simulation, TheOfficeBottle);
                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookExtinguisher, 20 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Drawn to the bottle on the wall, they take it.");
                Assert.That(took.Value.SourceId, Is.EqualTo(Somebody));
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent).Exists(e => e.TargetId == TheOfficeBottle), Is.True);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(Thing(simulation, TheOfficeBottle).IsHeld, Is.True, "And keep hold of it, as of their own bag, while nothing frightens them.");
            }
        }

        /// <summary>
        /// The hand on the bottle finishes what it starts (2026-09-29):
        /// somebody brave who took it for the player keeps it when the fright
        /// comes and goes at the fire with it. They used to fling it away and
        /// then go back for it, or leave it to somebody else.
        /// </summary>
        [Test]
        public void AHeldExtinguisher_TakenByTheBrave_IsUsedOnTheFire_WhenTheFrightComes()
        {
            var brave = AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, 9);
            ScenarioData data = OfficeWithThingsToUse(-1000, -3000, brave);
            data.Fire.ActivationTick = 20 * Run.TicksPerSecond;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, new LogicalPosition(2000, 2000));
            using (var simulation = new Run(data, 42UL))
            {
                HoldTheThing(simulation, TheOfficeBottle);
                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookExtinguisher, 15 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Drawn to the bottle, they take it, calm.");
                LetGo(simulation);

                CausalEvent? sprayed = AdvanceUntil(simulation, CausalEventType.ExtinguisherSprayed, 40 * Run.TicksPerSecond);
                Assert.That(sprayed.HasValue, "The fire starts in their room: frightened and brave, they keep the bottle and spray.");
                Assert.That(EventsOfType(simulation, CausalEventType.ItemThrown).Exists(e => e.TargetId == TheOfficeBottle), Is.False,
                    "Never flung away.");
                Assert.That(EventsOfType(simulation, CausalEventType.ItemDropped).Exists(e => e.TargetId == TheOfficeBottle), Is.False,
                    "Never dropped.");
            }
        }

        /// <summary>
        /// The hand on the pull station makes the brave pull it sooner
        /// (2026-09-29): somebody frightened who feels the pull needs a
        /// little less nerve to think of it. An ordinary person (bravery
        /// five) never raises the alarm of their own accord; drawn to the
        /// station, they do.
        /// </summary>
        [Test]
        public void TheHandOnThePullStation_SendsSomebodyForIt_WhoWouldNotHaveThoughtOfIt()
        {
            ScenarioData data = Office(Person(Somebody, -3000, 7500, AgentTraitValues.AllOrdinary));
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, TheBuilding.MeetingRoom);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();

            bool PulledWithin(bool hand, int seconds)
            {
                using (var simulation = new Run(data, 42UL))
                {
                    if (hand)
                    {
                        HoldTheFloor(simulation, TheBuilding.CorridorWestEnd + new LogicalPosition(-700, 1200));
                    }

                    simulation.FrightenForTests(0);
                    return AdvanceUntil(simulation, CausalEventType.AlarmPulled, seconds * Run.TicksPerSecond).HasValue;
                }
            }

            Assert.That(PulledWithin(false, 15), Is.False, "Bravery five, three metres from the station: they never think of it.");
            Assert.That(PulledWithin(true, 15), Is.True, "With the hand on it, they go and pull it.");
        }

        [Test]
        public void ADoorTheyCannotOpen_KeepsItsUse()
        {
            ScenarioData data = Office(Person(Somebody, 14500, 9500, AgentTraitValues.AllOrdinary));
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                int door = DoorIndex(TheBuilding.TheWayOut);
                HoldTheDoor(simulation, TheBuilding.TheWayOut);
                CausalEvent? tried = AdvanceUntil(simulation, CausalEventType.AgentTriedDoor, 20 * Run.TicksPerSecond);
                Assert.That(tried.HasValue, "Drawn to the locked way out, they try it.");
                Advance(simulation, 5 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.InfluenceSpent), Is.Empty, "A door they could not use spends nothing.");
                Assert.That(simulation.InfluenceForTests.PlaceOfDoor(door), Is.GreaterThanOrEqualTo(0), "The hand on it stands.");
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

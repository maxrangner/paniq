using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The hand, second pass (2026-09-30). The owner played the hand and
    /// found it hard to feel: "Influence barely feels there. Holding next to a
    /// group barely made them come closer." And: "Agents acted upon should be
    /// stuff they normally wouldn't, like a cowardly agent should pick up the
    /// fire extinguisher, an agent with low strength will bash on door." The
    /// owner's answers: most people answer the hand and strong wills refuse;
    /// against their nature should be often; a click leaves the hand for three
    /// seconds; the right button pushes people away; the card door is pounded
    /// under the hand but never gives, and the hand there sends for the card.
    /// </summary>
    public sealed class InfluenceTheHandEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);
        private static readonly SimulationId SomebodyElse = new SimulationId(2UL);
        private static readonly SimulationId TheOfficeBottle = new SimulationId(3301UL);
        private static readonly SimulationId ACrate = new SimulationId(3990UL);

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

        private static CausalEvent? AdvanceUntil(Run simulation, Predicate<CausalEvent> wanted, int limit)
        {
            for (int t = 0; t <= limit; t++)
            {
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (wanted(record))
                    {
                        return record;
                    }
                }

                if (t < limit)
                {
                    simulation.Step();
                }
            }

            return null;
        }

        private static AgentDefinition Person(SimulationId id, LogicalPosition at, AgentTraitValues traits) =>
            new AgentDefinition(id, at, CardinalDirection.North, traits);

        /// <summary>
        /// The office with these people standing in it, nothing burning, nothing
        /// on the timetable, and nobody deciding anything of their own for a
        /// long while: whatever they do, the hand made them do.
        /// </summary>
        private ScenarioData Office(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Day.ToiletEveryTicks = 0;
            data.Calming.Enabled = false;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            return data;
        }

        private static void Press(Run simulation, PlayerCommandType command, SimulationId target)
        {
            simulation.QueueCommand(command, target, simulation.Tick + 1);
            simulation.Step();
        }

        private static void Press(Run simulation, PlayerCommandType command, LogicalPosition spot)
        {
            simulation.QueueCommand(command, spot, simulation.Tick + 1);
            simulation.Step();
        }

        private int DoorIndex(Run simulation, SimulationId door)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == door)
                {
                    return i;
                }
            }

            throw new KeyNotFoundException(door.ToString());
        }

        /// <summary>
        /// The owner: "holding next to a group barely made them come closer",
        /// and "most, strong wills refuse". Somebody steady (nervousness two,
        /// who used to sit through any pull) standing about with nothing of
        /// their own to do answers a hand three metres away within a few
        /// seconds; the host's match (leadership nine) never does.
        /// </summary>
        [Test]
        public void TheSteady_AnswerTheHandWithinSeconds_ButTheStrongestWillsRefuseIt()
        {
            var steady = AgentTraitValues.AllOrdinary.With(AgentTrait.Nervousness, 2);
            var strongWilled = AgentTraitValues.AllOrdinary.With(AgentTrait.Leadership, 9);
            ScenarioData data = Office(Person(Somebody, new LogicalPosition(-3000, -3000), steady),
                Person(SomebodyElse, new LogicalPosition(-3000, 0), strongWilled));
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Susceptibility(simulation.AgentForTests(1)), Is.Zero, "Leadership nine: the hand is nothing to them.");
                Assert.That(influence.Susceptibility(simulation.AgentForTests(0)),
                    Is.GreaterThanOrEqualTo(simulation.Scenario.Influence.MinimumPercent));

                var spot = new LogicalPosition(0, -1500);
                Press(simulation, PlayerCommandType.InfluenceSpot, spot);
                CausalEvent? drawn = AdvanceUntil(simulation,
                    e => e.EventType == CausalEventType.AgentDrawnByInfluence && e.SourceId == Somebody, 4 * Run.TicksPerSecond);
                Assert.That(drawn.HasValue, "The steady come within a few seconds.");
                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence).Exists(e => e.SourceId == SomebodyElse),
                    Is.False, "The strong-willed never answer it.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot), Is.LessThan(1500),
                    "The steady one is standing at the hand.");
            }
        }

        /// <summary>The owner: "a single click should place an influence beacon for 3 seconds."</summary>
        [Test]
        public void AClick_LeavesTheHandThereForThreeSeconds_ThenItComesOffByItself()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000), AgentTraitValues.AllOrdinary)), 42UL))
            {
                Press(simulation, PlayerCommandType.InfluenceSpot, new LogicalPosition(0, -1500));
                int pressed = simulation.Tick;
                Press(simulation, PlayerCommandType.LeaveInfluence, default(SimulationId));
                InfluenceSystem influence = simulation.InfluenceForTests;
                int beacon = simulation.Scenario.Influence.BeaconTicks;
                while (simulation.Tick < pressed + beacon - 1)
                {
                    Assert.That(influence.Count, Is.EqualTo(1), $"Still there at tick {simulation.Tick}.");
                    simulation.Step();
                }

                Advance(simulation, 3);
                Assert.That(influence.Count, Is.Zero, "Three seconds on, it has come off by itself.");
                List<CausalEvent> released = EventsOfType(simulation, CausalEventType.PowerReleasedInfluence);
                Assert.That(released, Has.Count.EqualTo(1), "As a release would.");
            }
        }

        /// <summary>The owner: "Replace the right button with an anti-influence. Works same as the left mouse button, but in reverse."</summary>
        [Test]
        public void ThePush_SendsTheCalmAwayFromIt()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000), AgentTraitValues.AllOrdinary)), 42UL))
            {
                var spot = new LogicalPosition(-4000, -4000);
                long before = IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot);
                Press(simulation, PlayerCommandType.RepelSpot, spot);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerRepelled), Has.Count.EqualTo(1));
                CausalEvent? pushed = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentPushedAwayByInfluence, 4 * Run.TicksPerSecond);
                Assert.That(pushed.HasValue, "They move off, and the log says why.");
                Advance(simulation, 6 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, spot), Is.GreaterThan(before + 2000),
                    "Well away from where the hand pushes.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence), Is.Empty, "Nobody is drawn to a push.");
            }
        }

        /// <summary>A push on a door: the frightened choose another way out of the room.</summary>
        [Test]
        public void ThePushOnADoor_TurnsTheFrightenedToTheOtherWay()
        {
            ScenarioData Scene() => Office(Person(Somebody, new LogicalPosition(1000, 0), AgentTraitValues.AllOrdinary));

            int left;
            using (var simulation = new Run(Scene(), 42UL))
            {
                Advance(simulation, 20);
                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                left = simulation.ExitDoorForTests(0);
                Assert.That(left, Is.EqualTo(DoorIndex(simulation, TheBuilding.StockroomDoor))
                    .Or.EqualTo(DoorIndex(simulation, TheBuilding.OfficeDoor)), "Left alone, they go out of the office one way or the other.");
            }

            using (var simulation = new Run(Scene(), 42UL))
            {
                SimulationId chosen = left == DoorIndex(simulation, TheBuilding.StockroomDoor) ? TheBuilding.StockroomDoor : TheBuilding.OfficeDoor;
                Advance(simulation, 18);
                Press(simulation, PlayerCommandType.RepelDoor, chosen);
                simulation.FrightenForTests(0);
                Advance(simulation, 30);
                Assert.That(simulation.ExitDoorForTests(0), Is.Not.EqualTo(DoorIndex(simulation, chosen)),
                    "Pushed off the door they would have taken, they take the other.");
            }
        }

        /// <summary>
        /// The owner: "a cowardly agent should pick up the fire extinguisher".
        /// Bravery one, frightened by a fire across the office: the hand on the
        /// bottle sends them for it, and they fight the fire with it, which
        /// they never would of their own accord.
        /// </summary>
        [Test]
        public void ACoward_DrawnToTheBottle_FightsTheFireWithIt()
        {
            var coward = AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, 1);
            ScenarioData Scene()
            {
                ScenarioData data = Office(Person(Somebody, new LogicalPosition(-1000, -3000), coward));
                data.Fire.ActivationTick = 1;
                data.Fire.SpreadMinimumTicks = 100000;
                data.Fire.SpreadMaximumTicks = 100000;
                TheBuilding.FireAt(data, new LogicalPosition(2000, 2000));
                return data;
            }

            using (var simulation = new Run(Scene(), 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                Assert.That(AdvanceUntil(simulation, e => e.EventType == CausalEventType.ExtinguisherSprayed, 20 * Run.TicksPerSecond).HasValue,
                    Is.False, "Left alone, a coward never fights it.");
            }

            using (var simulation = new Run(Scene(), 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                Advance(simulation, 2);
                Press(simulation, PlayerCommandType.InfluenceThing, TheOfficeBottle);
                int pressed = simulation.Tick;
                CausalEvent? acted = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentActedForTheHand, 5 * Run.TicksPerSecond);
                Assert.That(acted.HasValue, "Against their nature, for the hand.");
                Assert.That(acted.Value.Tick, Is.GreaterThan(pressed), "A beat after the hand lands, never on its tick (the owner's rule).");
                Assert.That((AgainstTheirNature)acted.Value.Strength, Is.EqualTo(AgainstTheirNature.FoughtTheFire));
                Assert.That(AdvanceUntil(simulation, e => e.EventType == CausalEventType.ExtinguisherSprayed && e.SourceId == Somebody,
                    30 * Run.TicksPerSecond).HasValue, Is.True, "They take the bottle and spray the fire.");
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True, "Drawn with the hand over them while they do it.");
            }
        }

        /// <summary>
        /// The owner: "an agent with low strength will bash on door". Three
        /// weak people (strength three: no damage to a door of their own
        /// accord) frightened in the office with its door locked: the hand on
        /// the door has them throw themselves at it, and together they break it.
        /// </summary>
        [Test]
        public void TheWeak_DrawnToALockedDoor_ThrowThemselvesAtIt_AndBreakItTogether()
        {
            var weak = AgentTraitValues.AllOrdinary.With(AgentTrait.Strength, 3);
            ScenarioData data = Office(
                Person(Somebody, new LogicalPosition(-1000, 3500), weak),
                Person(SomebodyElse, new LogicalPosition(0, 3500), weak),
                Person(new SimulationId(3UL), new LogicalPosition(1000, 3500), weak));
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, TheBuilding.Stockroom);
            using (var simulation = new Run(data, 42UL))
            {
                Press(simulation, PlayerCommandType.ToggleLock, TheBuilding.OfficeDoor);
                int door = DoorIndex(simulation, TheBuilding.OfficeDoor);
                Assert.That(simulation.GetDoor(door).State, Is.EqualTo(DoorState.Locked));
                Press(simulation, PlayerCommandType.InfluenceDoor, TheBuilding.OfficeDoor);
                for (int i = 0; i < 3; i++)
                {
                    simulation.FrightenForTests(i);
                }

                CausalEvent? acted = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentActedForTheHand &&
                                                                   (AgainstTheirNature)e.Strength == AgainstTheirNature.BatteredTheDoor,
                    15 * Run.TicksPerSecond);
                Assert.That(acted.HasValue, "The weak throw themselves at it for the hand.");
                CausalEvent? broken = AdvanceUntil(simulation, e => e.EventType == CausalEventType.DoorBrokenDown, 60 * Run.TicksPerSecond);
                Assert.That(broken.HasValue, "And between them they break it.");
                Assert.That(simulation.GetDoor(door).State, Is.EqualTo(DoorState.Broken));
            }
        }

        /// <summary>
        /// The owner, on the card door under the hand: "they batter it, it
        /// holds", and the hand there "also sends someone who knows where the
        /// card is to fetch it".
        /// </summary>
        [Test]
        public void TheCardDoor_UnderTheHand_IsPounded_ButNeverGives_AndSomebodyGoesForTheCard()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data = TheBuilding.WithThePlayerAbleToAct(data);
            data.Keycard.Enabled = true;
            data.Agents = new[]
            {
                Person(Somebody, new LogicalPosition(13800, 14800), AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, new LogicalPosition(15200, 14800), AgentTraitValues.AllOrdinary),
                Person(new SimulationId(3UL), new LogicalPosition(14500, 14200), AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, TheBuilding.MeetingRoom);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calming.Enabled = false;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                Press(simulation, PlayerCommandType.InfluenceDoor, TheBuilding.TheWayOut);
                for (int i = 0; i < 3; i++)
                {
                    simulation.FrightenForTests(i);
                }

                CausalEvent? pounded = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentForcedDoor &&
                                                                    e.TargetId == TheBuilding.TheWayOut, 15 * Run.TicksPerSecond);
                Assert.That(pounded.HasValue, "Under the hand they throw themselves at the card door.");
                CausalEvent? fetching = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentDrawnByInfluence &&
                                                                     e.TargetId == TheBuilding.TheWayOut &&
                                                                     simulation.GetAgent(e.SourceId).ActivityState == AgentActivityState.FetchingKeycard,
                    5 * Run.TicksPerSecond);
                bool someoneFetches = false;
                for (int i = 0; i < 3; i++)
                {
                    someoneFetches |= simulation.GetAgent(i).ActivityState == AgentActivityState.FetchingKeycard ||
                                      simulation.KeycardBeliefForTests(i).Held >= 0;
                }

                Assert.That(someoneFetches || fetching.HasValue, Is.True, "And somebody who knows where the card lies goes for it.");
                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown).Exists(e => e.TargetId == TheBuilding.TheWayOut ||
                                                                                               e.SourceId == TheBuilding.TheWayOut),
                    Is.False, "It never gives to a shoulder.");
            }
        }

        /// <summary>
        /// The owner: "I held the blocking boxes, and none were moved." A crate
        /// too heavy for anybody to carry, lying still: somebody weak (strength
        /// two) drawn to it by the hand strains at it and heaves it aside,
        /// which they never would of their own accord.
        /// </summary>
        [Test]
        public void TheWeak_DrawnToAHeavyCrate_StrainAtIt_AndHeaveItAside()
        {
            var weak = AgentTraitValues.AllOrdinary.With(AgentTrait.Strength, 2);
            ScenarioData data = Office(Person(Somebody, new LogicalPosition(-2000, -3000), weak));
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects)
            {
                new PhysicsObjectDefinition(ACrate, PhysicsObjectKind.Box, new LogicalPosition(0, -3500), 600, 40000)
            };
            data.PhysicsObjects = things.ToArray();
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                int crate = simulation.ObjectsForTests.IndexOf(ACrate);
                Assert.That(simulation.ObjectsForTests.CanHeaveAside(simulation.AgentForTests(0), crate), Is.False,
                    "Too weak to heave it of their own accord.");
                LogicalPosition before = simulation.ObjectsForTests.PositionOf(crate);

                Press(simulation, PlayerCommandType.InfluenceThing, ACrate);
                CausalEvent? acted = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentActedForTheHand, 15 * Run.TicksPerSecond);
                Assert.That(acted.HasValue, "For the hand, they heave it.");
                Assert.That((AgainstTheirNature)acted.Value.Strength, Is.EqualTo(AgainstTheirNature.HeavedTheBox));
                Assert.That(acted.Value.Tick - EventsOfType(simulation, CausalEventType.PowerInfluenced)[0].Tick,
                    Is.GreaterThan(2 * Run.TicksPerSecond), "After straining at it a while: they are weak.");
                Advance(simulation, 2 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.ObjectsForTests.PositionOf(crate), before), Is.GreaterThan(300),
                    "The crate has moved.");
            }
        }
    }
}

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

        // ------------------------------------------------ the third pass (2026-09-30)

        /// <summary>
        /// The office as <see cref="Office"/> has it, with no keycard: whoever
        /// holds the card goes for the door and nothing else, and with one or
        /// two people in the building that may be the person under test.
        /// </summary>
        private ScenarioData OfficeWithoutTheCard(params AgentDefinition[] people)
        {
            ScenarioData data = Office(people);
            data.Keycard.Enabled = false;
            return data;
        }

        /// <summary>
        /// The owner: "when panicked, the agents still run around too much."
        /// Two frightened people in the office with its ways out open and no
        /// fire to run from: an ordinary person comes to a hand on the floor
        /// and stays at it; the host's match (leadership nine) runs on.
        /// </summary>
        [Test]
        public void TheFrightened_ComeToTheHand_AndStayAtIt_ButAStrongWillRunsOn()
        {
            var strongWilled = AgentTraitValues.AllOrdinary.With(AgentTrait.Leadership, 9);
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, new LogicalPosition(2500, 0), strongWilled));
            var hand = new LogicalPosition(-2500, -2500);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                Advance(simulation, 5 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, hand), Is.LessThan(2500),
                    "Five seconds on, the ordinary person is at the hand.");
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True, "Answering it, and shown so.");
                Advance(simulation, 6 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, hand), Is.LessThan(2500),
                    "And still there six seconds later: they do not sprint past it.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(SomebodyElse).Position, hand), Is.GreaterThan(4000),
                    "The strong-willed have none of it and run on.");
            }
        }

        /// <summary>
        /// The owner: "when left click is held, if then dragged the influence
        /// point should move with the pointer. So agents can be guided with
        /// this." A frightened person gathered at the hand follows it across
        /// the office when it is moved, answering the same press throughout.
        /// </summary>
        [Test]
        public void AHandDraggedAlong_TakesTheFrightenedAnsweringItWithIt()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, new LogicalPosition(2500, -2500));
                simulation.FrightenForTests(0);
                Advance(simulation, 4 * Run.TicksPerSecond);
                AgentSnapshot seen = simulation.GetAgent(Somebody);
                Assert.That(seen.ActingForTheHand, Is.True,
                    $"Answering the hand; they are {seen.ActivityState} at {seen.Position}, feeling it at " +
                    $"{simulation.InfluenceForTests.FeltBy(simulation.AgentForTests(0), 0)}.");

                // Dragged west across the office, a step at a time.
                for (int x = 2000; x >= -3000; x -= 500)
                {
                    Press(simulation, PlayerCommandType.MoveInfluence, new LogicalPosition(x, -2500));
                    Advance(simulation, 10);
                }

                Advance(simulation, 4 * Run.TicksPerSecond);
                InfluenceSystem influence = simulation.InfluenceForTests;
                Assert.That(influence.Count, Is.EqualTo(1));
                Assert.That(influence[0].At, Is.EqualTo(new LogicalPosition(-3000, -2500)), "The hand is where it was dragged to.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerInfluenced), Has.Count.EqualTo(1),
                    "One press all the way: moving it is not pressing again.");
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True, "Still answering it.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, new LogicalPosition(-3000, -2500)),
                    Is.LessThan(2500), "And they came with it.");
            }
        }

        /// <summary>
        /// The owner: "when influenced they should often switch to that
        /// specific task, like clearing boxes for a path." Three crates too
        /// heavy to carry, lying together: one ordinary person drawn by a hand
        /// on the floor among them heaves every one of them out of its reach
        /// with that one press. It used to be one crate a press.
        /// </summary>
        [Test]
        public void AHandAmongFallenCrates_ClearsThemAll_WithOnePress()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(-1000, -1500), AgentTraitValues.AllOrdinary));
            var crates = new[] { new SimulationId(3990UL), new SimulationId(3991UL), new SimulationId(3992UL) };
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects)
            {
                new PhysicsObjectDefinition(crates[0], PhysicsObjectKind.Box, new LogicalPosition(-900, -4400), 400, 40000),
                new PhysicsObjectDefinition(crates[1], PhysicsObjectKind.Box, new LogicalPosition(0, -4400), 400, 40000),
                new PhysicsObjectDefinition(crates[2], PhysicsObjectKind.Box, new LogicalPosition(900, -4400), 400, 40000)
            };
            data.PhysicsObjects = things.ToArray();
            var hand = new LogicalPosition(0, -4400);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                long reach = simulation.Scenario.Influence.ClearReachMillimetres;
                bool cleared = false;
                for (int t = 0; t < 45 * Run.TicksPerSecond && !cleared; t++)
                {
                    simulation.Step();
                    cleared = true;
                    foreach (SimulationId id in crates)
                    {
                        int crate = simulation.ObjectsForTests.IndexOf(id);
                        cleared &= IntegerMath.Distance(simulation.ObjectsForTests.PositionOf(crate), hand) > reach;
                    }
                }

                Assert.That(cleared, Is.True, "Every crate is heaved out of the hand's reach.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerInfluenced), Has.Count.EqualTo(1), "With one press.");

                // The hand says so (2026-10-02), a beat after the last crate
                // has left its reach, and is not used up by it: dragged on
                // to another heap, it would clear that too.
                CausalEvent? said = AdvanceUntil(simulation, e => e.EventType == CausalEventType.InfluenceSpent, 3 * Run.TicksPerSecond);
                Assert.That(said.HasValue, "Cleared, and said so.");
                Assert.That(said.Value.Strength, Is.EqualTo((int)HandAsk.ClearTheBoxes));
                Assert.That(simulation.InfluenceForTests[0].Spent, Is.False, "The place is not used up by a heap cleared.");
                Assert.That(simulation.InfluenceForTests.AskAt(simulation.InfluenceForTests[0]), Is.EqualTo(HandAsk.ComeHere),
                    "With nothing left to clear, it only gathers.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence).FindAll(e => e.SourceId == Somebody),
                    Has.Count.EqualTo(1), "Answered once: every crate after the first is the same answer.");
            }
        }

        /// <summary>
        /// A click's beacon lasts three seconds, and walking to a crate and
        /// straining at it takes longer for somebody weak. A crate somebody
        /// has set off for is finished all the same (2026-09-30: the heave
        /// used to stop the moment the beacon came off).
        /// </summary>
        [Test]
        public void ACrateSetOffFor_IsHeaved_EvenAfterAClicksBeaconHasComeOff()
        {
            var weak = AgentTraitValues.AllOrdinary.With(AgentTrait.Strength, 2);
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(-3500, -3500), weak));
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects)
            {
                new PhysicsObjectDefinition(ACrate, PhysicsObjectKind.Box, new LogicalPosition(0, -3500), 600, 40000)
            };
            data.PhysicsObjects = things.ToArray();
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 2 * Run.TicksPerSecond);
                Press(simulation, PlayerCommandType.InfluenceThing, ACrate);
                Press(simulation, PlayerCommandType.LeaveInfluence, default(SimulationId));
                Advance(simulation, simulation.Scenario.Influence.BeaconTicks + 5);
                Assert.That(simulation.InfluenceForTests.Count, Is.Zero, "The beacon has come off.");
                Assert.That(AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentActedForTheHand, 15 * Run.TicksPerSecond)
                    .HasValue, Is.True, "They heave it anyway: begun for the hand, finished for the hand.");
            }
        }

        /// <summary>
        /// People answering a hand on the floor used to walk to its very spot,
        /// all of them, and shove. Now each has a spot of their own round it.
        /// </summary>
        [Test]
        public void TheCalm_AnsweringAHand_StandOnSpotsOfTheirOwn()
        {
            var people = new AgentDefinition[5];
            for (int i = 0; i < people.Length; i++)
            {
                people[i] = Person(new SimulationId((ulong)(i + 1)), new LogicalPosition(-3500 + i * 700, -4300),
                    AgentTraitValues.AllOrdinary.With(AgentTrait.Nervousness, 8));
            }

            var hand = new LogicalPosition(0, -1500);
            using (var simulation = new Run(OfficeWithoutTheCard(people), 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                Advance(simulation, 12 * Run.TicksPerSecond);
                for (int i = 0; i < people.Length; i++)
                {
                    LogicalPosition at = simulation.GetAgent(i).Position;
                    Assert.That(IntegerMath.Distance(at, hand), Is.LessThan(3000), $"Person {i + 1} came to the hand.");
                    for (int j = i + 1; j < people.Length; j++)
                    {
                        // Two bodies side by side, give or take the jostle
                        // (a spot of their own is 900 mm from the next).
                        Assert.That(IntegerMath.Distance(at, simulation.GetAgent(j).Position), Is.GreaterThan(450),
                            $"Persons {i + 1} and {j + 1} stand apart.");
                    }
                }
            }
        }

        /// <summary>
        /// The owner: "can we put general attraction as a slider in debug with
        /// a print out number so I can find the sweetspot?" The hand strength
        /// scales how strongly everybody feels the hand: at nothing, nobody
        /// comes; at double, a person feels it twice as strongly.
        /// </summary>
        [Test]
        public void TheHandStrength_ScalesHowStronglyEverybodyFeelsTheHand()
        {
            var hand = new LogicalPosition(0, -1500);
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000), AgentTraitValues.AllOrdinary)), 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                InfluenceSystem influence = simulation.InfluenceForTests;
                int ordinary = influence.FeltBy(simulation.AgentForTests(0), 0);
                Assert.That(ordinary, Is.GreaterThan(0));

                Press(simulation, PlayerCommandType.SetHandStrength, new LogicalPosition(200, 0));
                Assert.That(simulation.Scenario.Influence.StrengthPercent, Is.EqualTo(200), "The run's own setting has it.");
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.EqualTo(ordinary * 2).Within(2),
                    "Twice as strong, twice as strongly felt.");

                Press(simulation, PlayerCommandType.SetHandStrength, new LogicalPosition(0, 0));
                Advance(simulation, 8 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence).Exists(e => e.SourceId == Somebody),
                    Is.False, "At nothing, nobody comes.");
            }
        }

        /// <summary>
        /// The second dial (2026-09-30, the owner, asked which feelings they
        /// tune most by hand: "influence strength and influence area"). The
        /// hand reach sets how far the hand is felt: somebody three and a bit
        /// metres from it feels it in full at the level's twelve, nothing at
        /// two, and a little at four and a half, where they stand in the
        /// fading outer half.
        /// </summary>
        [Test]
        public void TheHandReach_SetsHowFarTheHandIsFelt()
        {
            var hand = new LogicalPosition(0, -1500);
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000), AgentTraitValues.AllOrdinary)), 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                InfluenceSystem influence = simulation.InfluenceForTests;
                int full = influence.FeltBy(simulation.AgentForTests(0), 0);
                Assert.That(full, Is.GreaterThan(0));

                Press(simulation, PlayerCommandType.SetHandReach, new LogicalPosition(2000, 0));
                Assert.That(simulation.Scenario.Influence.ReachMillimetres, Is.EqualTo(2000), "The run's own setting has it.");
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.Zero, "Out of reach: nothing at all.");

                Press(simulation, PlayerCommandType.SetHandReach, new LogicalPosition(4500, 0));
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.GreaterThan(0).And.LessThan(full),
                    "In the fading outer half of a short reach: a little.");

                Press(simulation, PlayerCommandType.SetHandReach, new LogicalPosition(24000, 0));
                Assert.That(influence.FeltBy(simulation.AgentForTests(0), 0), Is.EqualTo(full),
                    "Twice the level's reach: full, as it was.");

                // The dial is clamped, not trusted: a reach of nothing would
                // be a hand nobody could ever feel.
                Press(simulation, PlayerCommandType.SetHandReach, new LogicalPosition(0, 0));
                Assert.That(simulation.Scenario.Influence.ReachMillimetres, Is.EqualTo(InfluenceSettings.MinimumReachMillimetres));
            }
        }

        /// <summary>
        /// Somebody with an errand still to come -- a meeting later in the day
        /// -- used to get up for the hand and then never go to it, because
        /// having any errand at all kept them from it. They come now; the
        /// meeting waits.
        /// </summary>
        [Test]
        public void SomebodyWithAnErrandStillToCome_AnswersTheHand()
        {
            using (var simulation = new Run(Office(Person(Somebody, new LogicalPosition(-3000, -3000), AgentTraitValues.AllOrdinary)), 42UL))
            {
                Advance(simulation, 10);
                AgentErrand errand = simulation.AgentForTests(0).Errand;
                errand.Has = true;
                errand.Cue = CueKind.GoHome;
                errand.StartTick = int.MaxValue / 2;
                Assert.That(errand.Pending, Is.True, "An errand handed to them and not yet taken up.");

                var hand = new LogicalPosition(0, -1500);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                Advance(simulation, 8 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, hand), Is.LessThan(2500),
                    "They came to the hand.");
                Assert.That(errand.Pending, Is.True, "And the errand still waits for them.");
            }
        }
        // ------------------------------------------------ the fourth pass (2026-09-30)

        /// <summary>
        /// The owner: "after some influence points spent they should stick to
        /// that choice." A hand held on a shut door for two seconds and then
        /// let go: the person sent to open it opens it all the same.
        /// </summary>
        [Test]
        public void AHandHeldTwoSeconds_ThenLetGo_TheyFinishWhatItAsked()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(-1000, 3500), AgentTraitValues.AllOrdinary));
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                int door = DoorIndex(simulation, TheBuilding.OfficeDoor);
                Assert.That(simulation.GetDoor(door).State, Is.Not.EqualTo(DoorState.Open));
                Press(simulation, PlayerCommandType.InfluenceDoor, TheBuilding.OfficeDoor);
                Advance(simulation, 3 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True, "Sent to the door.");
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True, "Three seconds beside the hand: they keep the task.");
                CausalEvent? opened = AdvanceUntil(simulation, e => e.EventType == CausalEventType.DoorOpened && e.SourceId == TheBuilding.OfficeDoor,
                    15 * Run.TicksPerSecond);
                Assert.That(opened.HasValue, "The door is opened after the hand came off.");
            }
        }

        /// <summary>A hand on and off again inside half a second convinces nobody: let go, they are on their own at once.</summary>
        [Test]
        public void AFlick_CommitsNobody()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary));
            var hand = new LogicalPosition(-2500, -2500);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                simulation.FrightenForTests(0);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                Advance(simulation, 20);
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Advance(simulation, 2);
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.False, "A flick: nothing kept.");
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.False);
            }
        }

        /// <summary>
        /// Character in one number: a kept goal fades faster the less easily
        /// led they are. A leader (at six tenths) drifts off a spot the hand
        /// left long before a nervous visitor does.
        /// </summary>
        [Test]
        public void ALeader_LosesACommitmentSooner_ThanANervousVisitor()
        {
            var leader = AgentTraitValues.AllOrdinary.With(AgentTrait.Leadership, 8);
            var nervous = AgentTraitValues.AllOrdinary.With(AgentTrait.Nervousness, 8);
            ScenarioData data = OfficeWithoutTheCard(
                Person(Somebody, new LogicalPosition(2000, 1000), leader),
                Person(SomebodyElse, new LogicalPosition(2500, 0), nervous).WithFamiliarity(AgentFamiliarity.Visitor));
            var hand = new LogicalPosition(-2500, -2500);
            // The commitment kept after the hand comes off is what this is
            // about: the frightened ran on when let go everywhere from
            // 2026-10-03, so the old rule is asked for here.
            data.Influence.FrightenedGoOnWhenLetGo = false;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                Advance(simulation, 8 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True, "Even the leader comes, driven long enough.");
                Assert.That(simulation.GetAgent(SomebodyElse).ActingForTheHand, Is.True);
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True);
                Assert.That(simulation.GetAgent(SomebodyElse).CommittedToTheHand, Is.True);

                int leaderLetGo = -1;
                int nervousLetGo = -1;
                for (int t = 0; t < 150 * Run.TicksPerSecond && (leaderLetGo < 0 || nervousLetGo < 0); t++)
                {
                    simulation.Step();
                    if (leaderLetGo < 0 && !simulation.GetAgent(Somebody).CommittedToTheHand)
                    {
                        leaderLetGo = t;
                    }

                    if (nervousLetGo < 0 && !simulation.GetAgent(SomebodyElse).CommittedToTheHand)
                    {
                        nervousLetGo = t;
                    }
                }

                Assert.That(leaderLetGo, Is.GreaterThan(0), "The leader lets it go in time.");
                Assert.That(nervousLetGo, Is.GreaterThan(leaderLetGo), "The nervous visitor keeps it longer.");
            }
        }

        /// <summary>
        /// The flames inside their danger distance put the hand out of their
        /// head, kept or not: the one rule for everybody.
        /// </summary>
        [Test]
        public void AFrightenedPersonCommittedToTheHand_DropsItWhenTheFlamesComeNear()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary));
            var hand = new LogicalPosition(-2500, -2500);

            // A fire that starts eight seconds in, on the very spot the hand
            // is on, and does not spread: by then they stand in a ring round
            // it, inside their danger distance.
            data.Fire.ActivationTick = 8 * Run.TicksPerSecond;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, hand);
            // The commitment kept after the hand comes off is what this is
            // about: the frightened ran on when let go everywhere from
            // 2026-10-03, so the old rule is asked for here.
            data.Influence.FrightenedGoOnWhenLetGo = false;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                simulation.FrightenForTests(0);
                Advance(simulation, 6 * Run.TicksPerSecond);
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True, "Kept after the hand came off.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, hand), Is.LessThan(2500), "Standing at it.");

                Advance(simulation, 3 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.False, "Flames at their feet: the hand is out of their head.");
            }
        }

        /// <summary>The player's tug on somebody takes them off whatever they were doing for the hand.</summary>
        [Test]
        public void ATug_EndsWhatTheyWereDoingForTheHand()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary));
            var hand = new LogicalPosition(-2500, -2500);
            // The commitment kept after the hand comes off is what this is
            // about: the frightened ran on when let go everywhere from
            // 2026-10-03, so the old rule is asked for here.
            data.Influence.FrightenedGoOnWhenLetGo = false;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, hand);
                simulation.FrightenForTests(0);
                Advance(simulation, 4 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.True);
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True);
                Press(simulation, PlayerCommandType.TugPerson, Somebody);
                Advance(simulation, 2);
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.False, "Taken by the shirt: the goal is dropped.");
                Assert.That(simulation.GetAgent(Somebody).ActingForTheHand, Is.False);
            }
        }

        /// <summary>
        /// A fresh press they feel replaces the goal and keeps their
        /// attention: somebody standing at one spot sets off for the next
        /// without a beat's dithering, and keeps that one too once let go of.
        /// </summary>
        [Test]
        public void AHandMovedOntoSomethingElse_TakesThemWithIt_ConvictionKept()
        {
            ScenarioData data = OfficeWithoutTheCard(Person(Somebody, new LogicalPosition(2000, 1000), AgentTraitValues.AllOrdinary));
            var first = new LogicalPosition(-2500, -2500);
            var second = new LogicalPosition(2500, -2500);
            // The commitment kept after the hand comes off is what this is
            // about: the frightened ran on when let go everywhere from
            // 2026-10-03, so the old rule is asked for here.
            data.Influence.FrightenedGoOnWhenLetGo = false;
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                Press(simulation, PlayerCommandType.InfluenceSpot, first);
                simulation.FrightenForTests(0);
                Advance(simulation, 6 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, first), Is.LessThan(2500), "At the first spot.");

                Press(simulation, PlayerCommandType.InfluenceSpot, second);
                Advance(simulation, 1 * Run.TicksPerSecond);
                Press(simulation, PlayerCommandType.ReleaseInfluence, default(SimulationId));
                Assert.That(simulation.GetAgent(Somebody).CommittedToTheHand, Is.True,
                    "One second of the second press, with the conviction the first built: kept.");
                Advance(simulation, 6 * Run.TicksPerSecond);
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, second), Is.LessThan(2500),
                    "And they went to the second spot after the hand came off.");
            }
        }

        /// <summary>
        /// The owner's decision (2026-09-30): under the hand the card door
        /// gives to a long pounding. Three people pound it for the hand while
        /// the card sits in a calm person's pocket across the building (so
        /// nobody can fetch it and nobody swipes): not broken at twenty
        /// seconds, broken by a minute. Without the hand it never gives
        /// (DoorsEditModeTests). The fetch the hand sends is
        /// TheCardDoor_UnderTheHand_SendsSomebodyForTheCard.
        /// </summary>
        [Test]
        public void TheCardDoor_UnderTheHand_IsPounded_AndGivesAfterAboutFortySeconds()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data = TheBuilding.WithThePlayerAbleToAct(data);
            data.Keycard.Enabled = true;
            data.Agents = new[]
            {
                Person(Somebody, new LogicalPosition(13800, 14800), AgentTraitValues.AllOrdinary),
                Person(SomebodyElse, new LogicalPosition(15200, 14800), AgentTraitValues.AllOrdinary),
                Person(new SimulationId(3UL), new LogicalPosition(14500, 14200), AgentTraitValues.AllOrdinary),
                Person(new SimulationId(4UL), new LogicalPosition(-4000, -4000), AgentTraitValues.AllOrdinary)
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
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.GiveKeycardForTests(3);
                Press(simulation, PlayerCommandType.InfluenceDoor, TheBuilding.TheWayOut);
                for (int i = 0; i < 3; i++)
                {
                    simulation.FrightenForTests(i);
                }

                CausalEvent? pounded = AdvanceUntil(simulation, e => e.EventType == CausalEventType.AgentForcedDoor &&
                                                                    e.TargetId == TheBuilding.TheWayOut, 15 * Run.TicksPerSecond);
                Assert.That(pounded.HasValue, "Under the hand they throw themselves at the card door.");
                Advance(simulation, 20 * Run.TicksPerSecond - (simulation.Tick - pounded.Value.Tick));
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown).Exists(e => e.TargetId == TheBuilding.TheWayOut),
                    Is.False, "Twenty seconds of pounding: it holds.");
                Assert.That(simulation.GetDoor(DoorIndex(simulation, TheBuilding.TheWayOut)).DamagePercent, Is.GreaterThan(0),
                    "But it is visibly weakening.");
                CausalEvent? gave = AdvanceUntil(simulation, e => e.EventType == CausalEventType.DoorBrokenDown &&
                                                                 e.TargetId == TheBuilding.TheWayOut, 40 * Run.TicksPerSecond);
                Assert.That(gave.HasValue, "By a minute of pounding it has given.");
            }
        }

    }
}

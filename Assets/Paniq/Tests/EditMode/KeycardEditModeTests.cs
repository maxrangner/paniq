using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The keycard (2026-09-27): where it starts, who knows, who fetches it,
    /// who drops it, and the card door it opens, which nobody batters and
    /// the player has no key to.
    /// </summary>
    public sealed class KeycardEditModeTests
    {
        private ScenarioAsset scenario;

        /// <summary>The office's ordinary person (1001), the cafeteria's seated ordinary person (1015), and the office door.</summary>
        private static readonly SimulationId OfficeOrdinary = new SimulationId(1001UL);
        private static readonly SimulationId CafeteriaSitter = new SimulationId(1015UL);
        private const int OfficeOrdinaryIndex = 0;
        private const int CafeteriaSitterIndex = 14;

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

        // ---------------------------------------------------------------- the start

        [Test]
        public void SeedsFortyToFortyNine_StartTheCardOnAMemberOfStaffOrADesk_AndVisitorsKnowNothing()
        {
            int onAPerson = 0;
            int onADesk = 0;
            for (ulong seed = 40UL; seed < 50UL; seed++)
            {
                ScenarioData data = scenario.ToRuntimeData();
                using (var simulation = new Run(data, seed))
                {
                    List<CausalEvent> started = EventsOfType(simulation, CausalEventType.KeycardStarted);
                    Assert.That(started, Has.Count.EqualTo(1), $"Seed {seed}: the card starts somewhere, once.");
                    CausalEvent start = started[0];
                    Assert.That(start.TargetId, Is.EqualTo(TheBuilding.TheKeycard));
                    PhysicsObjectSnapshot card = Thing(simulation, TheBuilding.TheKeycard);
                    if (start.SourceId == TheBuilding.TheKeycard)
                    {
                        onADesk++;
                        Assert.That(card.IsHeld, Is.False, $"Seed {seed}: on a desk, in nobody's pocket.");
                        Assert.That(card.Resting, Is.True, $"Seed {seed}: up on the desk, not on the floor.");
                    }
                    else
                    {
                        onAPerson++;
                        Assert.That(card.HeldBy, Is.EqualTo(start.SourceId), $"Seed {seed}: in the pocket the log says.");
                        AgentDefinition holder = Array.Find(data.Agents, a => a.AgentId == start.SourceId);
                        Assert.That(holder.Familiarity, Is.Not.EqualTo(AgentFamiliarity.Visitor),
                            $"Seed {seed}: a visitor never starts with the card.");
                    }

                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        bool visitor = data.Agents[i].Familiarity == AgentFamiliarity.Visitor;
                        Assert.That(simulation.KeycardBeliefForTests(i).Knows, Is.EqualTo(!visitor),
                            $"Seed {seed}: staff know where the card started; visitors do not.");
                    }
                }
            }

            Assert.That(onAPerson, Is.GreaterThan(0), "Over ten seeds somebody starts with it.");
            Assert.That(onADesk, Is.GreaterThan(0), "Over ten seeds it starts on a desk at least once.");
        }

        [Test]
        public void ALevelWithNoKeycard_HasAPlainLockedWayOut()
        {
            ScenarioData data = TheBuilding.WithAnOrdinaryWayOut(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 42UL))
            {
                Assert.That(EventsOfType(simulation, CausalEventType.KeycardStarted), Is.Empty);
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).Dormant, Is.True, "Put away.");
                Assert.That(Door(simulation, TheBuilding.TheWayOut).NeedsKeycard, Is.False);
            }
        }

        [Test]
        public void OnlyADoorToTheStreet_CanNeedTheCard()
        {
            ScenarioData data = scenario.ToRuntimeData();
            int office = Array.FindIndex(data.Doors, d => d.DoorId == TheBuilding.OfficeDoor);
            DoorDefinition inside = data.Doors[office];
            data.Doors[office] = new DoorDefinition(inside.DoorId, inside.RoomId, inside.Side, inside.CentreAlongWallMillimetres,
                inside.WidthMillimetres, startsLocked: true, needsKeycard: true);
            Assert.Throws<InvalidOperationException>(() => data.Validate(), "An inside door cannot be a card door.");
        }

        // ---------------------------------------------------------------- the door

        [Test]
        public void TheStrong_CannotBatterTheCardDoor_AndGiveItUp()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(OfficeOrdinary, new LogicalPosition(14500, 15000), CardinalDirection.North,
                    new AgentTraitValues(10, 5, 5, 5, 5, 5))
            };
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, new LogicalPosition(14500, 12500));
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Exits.DoorForceChancePercent = 100;
            data.Exits.DoorStrength = 4;
            using (var simulation = new Run(data, 42UL))
            {
                // The card is somewhere in the office, far out of reach.
                simulation.PutKeycardOnATableForTests(0);
                CausalEvent? gaveUp = AdvanceUntil(simulation, CausalEventType.AgentGaveUpOnDoor, 20 * Run.TicksPerSecond);
                Assert.That(gaveUp.HasValue, "At the card door the strong give up like anybody else.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentForcedDoor), Is.Empty, "Not a single shoulder.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Empty);
                DoorSnapshot door = Door(simulation, TheBuilding.TheWayOut);
                Assert.That(door.State, Is.EqualTo(DoorState.Locked));
                Assert.That(door.DamagePercent, Is.EqualTo(0));
                Assert.That(door.NeedsKeycard, Is.True);
            }
        }

        [Test]
        public void ThePlayersKey_DoesNotFitTheCardDoor_ButStillFitsTheOthers()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Keycard.Enabled = true;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheBuilding.TheWayOut, 1);
                simulation.QueueCommand(PlayerCommandType.ToggleLock, TheBuilding.TheWayOut, 2);
                simulation.QueueCommand(PlayerCommandType.ToggleLock, TheBuilding.OfficeDoor, 3);
                Advance(simulation, 5);
                Assert.That(Door(simulation, TheBuilding.TheWayOut).State, Is.EqualTo(DoorState.Locked), "No key fits a card door.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorUnlocked), Is.Empty);
                Assert.That(Door(simulation, TheBuilding.OfficeDoor).State, Is.EqualTo(DoorState.Locked), "An inside door still takes the key.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorLocked), Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void AHolderAtTheCardDoor_SwipesIt_AndItStaysUnlockedForGood()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new AgentDefinition(OfficeOrdinary, new LogicalPosition(14500, 15000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, new LogicalPosition(14500, 12500));
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.GiveKeycardForTests(OfficeOrdinaryIndex);
                CausalEvent? swiped = AdvanceUntil(simulation, CausalEventType.DoorUnlockedWithKeycard, 20 * Run.TicksPerSecond);
                Assert.That(swiped.HasValue, "With the card in their pocket they swipe the door.");
                Assert.That(swiped.Value.SourceId, Is.EqualTo(OfficeOrdinary));
                Assert.That(swiped.Value.TargetId, Is.EqualTo(TheBuilding.TheWayOut));
                CausalEventType cause = simulation.EventLog.Get(swiped.Value.CausalParentEventId).EventType;
                Assert.That(cause == CausalEventType.AgentScared || cause == CausalEventType.AgentTriedDoor,
                    "The swipe follows their fright (from the reader's reach) or their try at the handle.");

                CausalEvent? opened = AdvanceUntil(simulation, CausalEventType.DoorOpened, 5 * Run.TicksPerSecond);
                Assert.That(opened.HasValue, "And open it.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentForcedDoor), Is.Empty);
                DoorSnapshot door = Door(simulation, TheBuilding.TheWayOut);
                Assert.That(door.NeedsKeycard, Is.False, "An ordinary door from now on.");
                Assert.That(door.State, Is.EqualTo(DoorState.Open));
                AdvanceUntil(simulation, CausalEventType.AgentEscaped, 20 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(OfficeOrdinaryIndex).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            }
        }

        // ---------------------------------------------------------------- fetching it

        [Test]
        public void FrightenedStaff_GoBackForTheCardOnTheDesk_AndSwipeTheWayOut_OneFetcherAtATime()
        {
            // Two brave members of staff at the way out, frightened, with the
            // card on a desk in the office: they find the door shut, one of
            // them -- only one -- goes back for the card, and swipes the door.
            // The whole cast used to play this out, and a card knocked off its
            // desk in the crush (which people who never saw it happen still
            // believe is on the desk) made the test about one seed's luck.
            ScenarioData data = TwoBraveStaffAtTheWayOut();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                int onTheirWayAtOnce = 0;
                CausalEvent? took = null;
                for (int t = 0; t < 120 * Run.TicksPerSecond && !took.HasValue; t++)
                {
                    simulation.Step();

                    // Somebody knocked down mid-fetch keeps the activity but
                    // not the claim, so only those on their feet count.
                    int onTheirWay = 0;
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot a = simulation.GetAgent(i);
                        onTheirWay += a.ActivityState == AgentActivityState.FetchingKeycard &&
                                      (a.BodyState == AgentBodyState.Upright || a.BodyState == AgentBodyState.Staggering) ? 1 : 0;
                    }

                    onTheirWayAtOnce = Math.Max(onTheirWayAtOnce, onTheirWay);
                    List<CausalEvent> taken = EventsOfType(simulation, CausalEventType.AgentTookKeycard);
                    if (taken.Count > 0)
                    {
                        took = taken[0];
                    }
                }

                Assert.That(took.HasValue, "Somebody who found the way out shut went back for the card. " + WhoGaveUp(simulation, data));
                Assert.That(onTheirWayAtOnce, Is.EqualTo(1), "Never two people on their way to one card.");
                Assert.That(simulation.EventLog.Get(took.Value.CausalParentEventId).EventType, Is.EqualTo(CausalEventType.AgentScared),
                    "Fetched out of fright, not tidiness.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentGaveUpOnDoor).Exists(e =>
                        e.SourceId == took.Value.SourceId && e.TargetId == TheBuilding.TheWayOut && e.Tick < took.Value.Tick),
                    "They had found the card door shut first.");

                CausalEvent? swiped = AdvanceUntil(simulation, CausalEventType.DoorUnlockedWithKeycard, 120 * Run.TicksPerSecond);
                Assert.That(swiped.HasValue, "And swiped the way out open. " + WhereTheHolderIs(simulation, data, took.Value));
                Assert.That(swiped.Value.SourceId, Is.EqualTo(took.Value.SourceId));
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Empty, "Nobody battered it meanwhile.");
            }
        }

        [Test]
        public void ACalmTidier_NeverCarriesTheCardOff()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Items.TidyChancePercent = 100;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                LogicalPosition before = Thing(simulation, TheBuilding.TheKeycard).Position;
                Advance(simulation, 60 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookKeycard), Is.Empty);
                Assert.That(EventsOfType(simulation, CausalEventType.ItemDropped).Exists(e => e.TargetId == TheBuilding.TheKeycard), Is.False);
                PhysicsObjectSnapshot card = Thing(simulation, TheBuilding.TheKeycard);
                Assert.That(card.IsHeld, Is.False);
                Assert.That(IntegerMath.Distance(card.Position, before), Is.LessThan(50), "Still on the desk where it lay.");
                Assert.That(card.Resting, Is.True);
            }
        }

        [Test]
        public void ThePlayersPullOnTheCard_MakesSomebodyCalmPocketIt()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Keycard.Enabled = true;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                simulation.QueueCommand(PlayerCommandType.InfluenceThing, TheBuilding.TheKeycard, simulation.Tick + 1);
                simulation.Step();

                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookKeycard, 60 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Drawn to the card, somebody pockets it.");
                Assert.That(simulation.EventLog.Get(took.Value.CausalParentEventId).EventType, Is.EqualTo(CausalEventType.InfluenceSpent),
                    "Because the player asked, and the pull is spent.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(took.Value.SourceId));
                int holder = Array.FindIndex(data.Agents, a => a.AgentId == took.Value.SourceId);
                Assert.That(simulation.KeycardBeliefForTests(holder).Held, Is.GreaterThanOrEqualTo(0), "In the pocket, not the arms.");
            }
        }

        // ---------------------------------------------------------------- the pull, once the panic has started (2026-09-28)

        private static readonly LogicalPosition CardOnTheFloor = new LogicalPosition(-3000, -3000);

        /// <summary>
        /// Ordinary people (bravery five: never fetchers of their own accord)
        /// in the office, the fire far off in the meeting room and never
        /// spreading, nobody calming down or freezing, and no traps.
        /// </summary>
        private ScenarioData OrdinaryPeopleInTheOffice(params LogicalPosition[] where)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Keycard.Enabled = true;
            var agents = new AgentDefinition[where.Length];
            for (int i = 0; i < where.Length; i++)
            {
                agents[i] = new AgentDefinition(new SimulationId((ulong)(i + 1)), where[i], CardinalDirection.North,
                    AgentTraitValues.AllOrdinary);
            }

            data.Agents = agents;
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, TheBuilding.MeetingRoom);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calming.Enabled = false;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            return data;
        }

        /// <summary>
        /// The player's hand on the card (a hold since 2026-09-29): pressed
        /// once and kept there. <paramref name="ticks"/> of holding before the
        /// test looks: the frightened only go for it once it has been held
        /// for <see cref="KeycardSettings.PulledAfterTicks"/>.
        /// </summary>
        private static void PullOnTheCard(Run simulation, int ticks)
        {
            simulation.QueueCommand(PlayerCommandType.InfluenceThing, TheBuilding.TheKeycard, simulation.Tick + 1);
            for (int i = 0; i < ticks; i++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void ThePlayersPullOnTheCard_SendsSomebodyFrightenedForIt_BraveOrNot()
        {
            ScenarioData data = OrdinaryPeopleInTheOffice(new LogicalPosition(-1000, -3000));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(CardOnTheFloor);
                simulation.FrightenForTests(0);
                PullOnTheCard(simulation, 120);
                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookKeycard, 20 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Frightened, bravery five, and pulled: they go and pocket it.");
                Assert.That(took.Value.SourceId, Is.EqualTo(new SimulationId(1UL)));
                Assert.That(simulation.EventLog.Get(took.Value.CausalParentEventId).EventType, Is.EqualTo(CausalEventType.InfluenceSpent),
                    "Because the player asked, and the pull is spent.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentDrawnByInfluence)
                        .Exists(e => e.SourceId == new SimulationId(1UL) && e.Tick <= took.Value.Tick),
                    "The log says the pull is what moved them.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(took.Value.SourceId));
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Scared),
                    "Still frightened: a fetch, not a calm errand.");
            }
        }

        [Test]
        public void WithoutThePull_SomebodyOrdinaryNeverGoesForTheCard()
        {
            ScenarioData data = OrdinaryPeopleInTheOffice(new LogicalPosition(-1000, -3000));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(CardOnTheFloor);
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookKeycard), Is.Empty,
                    "Going for the card of their own accord takes bravery eight.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).IsHeld, Is.False);
            }
        }

        [Test]
        public void ThePull_NeverSendsAnybodyIntoTheFlamesForTheCard()
        {
            ScenarioData data = OrdinaryPeopleInTheOffice(new LogicalPosition(-1000, -3000));
            TheBuilding.FireAt(data, CardOnTheFloor);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(CardOnTheFloor);
                Advance(simulation, 30);
                PullOnTheCard(simulation, 120);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookKeycard), Is.Empty,
                    "The card lies in the flames: it waits, whatever the player asks.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).IsHeld, Is.False);
            }
        }

        [Test]
        public void TwoPeoplePulledToTheCard_GoOneAtATime()
        {
            ScenarioData data = OrdinaryPeopleInTheOffice(new LogicalPosition(-1000, -3000), new LogicalPosition(-1000, -1500));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(CardOnTheFloor);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                PullOnTheCard(simulation, 120);
                int onTheirWayAtOnce = 0;
                CausalEvent? took = null;
                for (int t = 0; t < 20 * Run.TicksPerSecond && !took.HasValue; t++)
                {
                    simulation.Step();
                    int onTheirWay = 0;
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        AgentSnapshot a = simulation.GetAgent(i);
                        onTheirWay += a.ActivityState == AgentActivityState.FetchingKeycard &&
                                      (a.BodyState == AgentBodyState.Upright || a.BodyState == AgentBodyState.Staggering) ? 1 : 0;
                    }

                    onTheirWayAtOnce = Math.Max(onTheirWayAtOnce, onTheirWay);
                    List<CausalEvent> taken = EventsOfType(simulation, CausalEventType.AgentTookKeycard);
                    if (taken.Count > 0)
                    {
                        took = taken[0];
                    }
                }

                Assert.That(took.HasValue, "One of them pockets it.");
                Assert.That(onTheirWayAtOnce, Is.EqualTo(1), "Never two people on their way to one card, pulled or not.");
            }
        }

        // ---------------------------------------------------------------- keeping and losing it

        [Test]
        public void AFrightenedHolder_KeepsTheCard_AndAlightKeepsItUntilTheyGoDown()
        {
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.GiveKeycardForTests(OfficeOrdinaryIndex);
                simulation.FrightenForTests(OfficeOrdinaryIndex);
                Advance(simulation, 5 * Run.TicksPerSecond);
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(OfficeOrdinary), "Frightened, they keep it.");

                simulation.SetAlightForTests(OfficeOrdinaryIndex);
                bool outColdOrDead = false;
                for (int t = 0; t < 90 * Run.TicksPerSecond && !outColdOrDead; t++)
                {
                    simulation.Step();
                    AgentSnapshot holder = simulation.GetAgent(OfficeOrdinaryIndex);
                    if (holder.BodyState != AgentBodyState.Unconscious && holder.Outcome == AgentTerminalOutcome.Unresolved)
                    {
                        // Alight, rolling on the floor, up again: still theirs.
                        Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(OfficeOrdinary),
                            $"Alight and {holder.BodyState}, they still have it.");
                    }
                    else
                    {
                        outColdOrDead = true;
                    }
                }

                Assert.That(outColdOrDead, "Somebody alight is out cold or dead inside a minute and a half.");

                // The card slips out on their next turn, a tick later.
                Advance(simulation, 5);
                Assert.That(EventsOfType(simulation, CausalEventType.KeycardDropped), Has.Count.EqualTo(1), "And the card fell out where they lay.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).IsHeld, Is.False);
            }
        }

        [Test]
        public void AHolderKnockedOffTheirChair_KeepsTheCard()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Keycard.Enabled = true;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.GiveKeycardForTests(CafeteriaSitterIndex);
                for (int poke = 0; poke < 3; poke++)
                {
                    simulation.QueueCommand(PlayerCommandType.NudgePerson, CafeteriaSitter, simulation.Tick + 1);
                    Advance(simulation, 10);
                }

                CausalEvent? knocked = AdvanceUntil(simulation, CausalEventType.AgentKnockedOffChair, 10 * Run.TicksPerSecond);
                Assert.That(knocked.HasValue, "Three quick pokes knock a sitter off the chair.");
                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.KeycardDropped), Is.Empty,
                    "A fall they get up from keeps the card in their pocket: only out cold or dead loses it (the owner's rule).");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(CafeteriaSitter));
            }
        }

        [Test]
        public void ACardOnTheFloor_IsPickedUpByWhoeverThePlayerDrawsToIt()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Keycard.Enabled = true;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(new LogicalPosition(-3000, -3000));
                simulation.QueueCommand(PlayerCommandType.InfluenceThing, TheBuilding.TheKeycard, simulation.Tick + 1);
                simulation.Step();

                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookKeycard, 60 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "Somebody picks it up off the floor.");
                Assert.That(Thing(simulation, TheBuilding.TheKeycard).HeldBy, Is.EqualTo(took.Value.SourceId));
            }
        }

        [Test]
        public void ACardInTheFlames_NeverBurns()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var spot = new LogicalPosition(-3000, -3000);
            data.Fire.ActivationTick = 1;
            TheBuilding.FireAt(data, spot);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardDownForTests(spot);
                Advance(simulation, 40 * Run.TicksPerSecond);
                PhysicsObjectSnapshot card = Thing(simulation, TheBuilding.TheKeycard);
                Assert.That(card.BurnState, Is.EqualTo(ObjectBurnState.Intact), "Fireproof: the owner's rule.");
                Assert.That(EventsOfType(simulation, CausalEventType.ObjectCaughtFire).Exists(e => e.SourceId == TheBuilding.TheKeycard), Is.False);
            }
        }

        // ---------------------------------------------------------------- left alone

        [Test]
        public void LeftAlone_ComesToWhatAHandsOffRunComesTo()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            const int cap = 60 * Run.TicksPerSecond;
            int expectedSaved;
            int expectedCrowd;
            using (var byHand = new Run(data.Clone(), 42UL))
            {
                for (int t = 0; t < cap && byHand.Phase != RoundPhase.Over; t++)
                {
                    byHand.Step();
                }

                expectedSaved = LivedByTheEnd(byHand, out expectedCrowd);
            }

            using (var leftAlone = new LeftAloneRunner(data, 42UL, cap))
            {
                Assert.That(leftAlone.IsDone, Is.False);
                int steps = 0;
                while (!leftAlone.Advance(20))
                {
                    steps++;
                    Assert.That(steps, Is.LessThan(cap), "It ends at the cap at the latest.");
                }

                Assert.That(leftAlone.SavedCount, Is.EqualTo(expectedSaved), "The same seed, the same answer.");
                Assert.That(leftAlone.CrowdSize, Is.EqualTo(expectedCrowd));
            }
        }

        [Test]
        public void LeftAlone_StartsItsDisasterWhenThePlayerPressedTrigger_AndKeepsInStepUntilThen()
        {
            // A level whose fire waits for the button and that has no Director
            // to start it: left alone would never burn at all unless the
            // player's press is copied onto the same tick.
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            data.Round.HazardWaitsForTrigger = true;
            data.Director.ClimbsTheLadder = false;
            const int cap = 40 * Run.TicksPerSecond;
            const int pressedFor = 300;
            int expectedSaved;
            using (var byHand = new Run(data.Clone(), 42UL))
            {
                byHand.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), pressedFor);
                for (int t = 0; t < cap && byHand.Phase != RoundPhase.Over; t++)
                {
                    byHand.Step();
                }

                Assert.That(EventsOfType(byHand, CausalEventType.FireActivated), Is.Not.Empty, "The press lit the fire.");
                expectedSaved = LivedByTheEnd(byHand, out _);
            }

            using (var leftAlone = new LeftAloneRunner(data, 42UL, cap))
            {
                // Kept in step: never further than the real round, 250 ticks in.
                leftAlone.Advance(1000, 250, 0L);
                Assert.That(leftAlone.Tick, Is.EqualTo(250), "Held at the real round's tick until the disaster starts.");
                leftAlone.MirrorTrigger(pressedFor);
                while (!leftAlone.Advance(50))
                {
                }

                Assert.That(leftAlone.SavedCount, Is.EqualTo(expectedSaved), "The same disaster, started on the same tick.");
            }
        }

        [Test]
        public void LeftAlone_WithABudget_PlaysAtLeastOneTickAndStopsWhenItIsSpent()
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            using (var leftAlone = new LeftAloneRunner(data, 42UL, 600))
            {
                leftAlone.Advance(1000, int.MaxValue, 1L);
                Assert.That(leftAlone.Tick, Is.EqualTo(1), "A budget smaller than one tick still plays one.");
            }
        }

        // ---------------------------------------------------------------- the review's findings (2026-09-27)

        [Test]
        public void ACardDoor_WithNoKeycardInTheBuilding_IsRefused_AndSoAreTwoKeycards()
        {
            ScenarioData none = scenario.ToRuntimeData();
            none.PhysicsObjects = Array.FindAll(none.PhysicsObjects, o => o.Kind != PhysicsObjectKind.Keycard);
            Assert.Throws<InvalidOperationException>(() => none.Validate(), "A way out nobody could ever open.");
            TheBuilding.WithAnOrdinaryWayOut(none);
            Assert.DoesNotThrow(() => none.Validate(), "With the keycard switched off it is a plain locked door.");

            ScenarioData two = scenario.ToRuntimeData();
            var things = new List<PhysicsObjectDefinition>(two.PhysicsObjects)
            {
                new PhysicsObjectDefinition(new SimulationId(3951UL), PhysicsObjectKind.Keycard, new LogicalPosition(-3000, -3000), 150, 50)
            };
            two.PhysicsObjects = things.ToArray();
            Assert.Throws<InvalidOperationException>(() => two.Validate(), "The run only knows one card.");
        }

        [Test]
        public void AFetcherKnockedOut_LetsSomebodyElseGoForTheCard()
        {
            ScenarioData data = TwoBraveStaffAtTheWayOut();
            data.Falls.PassOutChancePercent = 100;
            data.Falls.UnconsciousMinimumTicks = 100000;
            data.Falls.UnconsciousMaximumTicks = 100000;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                int first = -1;
                for (int t = 0; t < 120 * Run.TicksPerSecond && first < 0; t++)
                {
                    simulation.Step();
                    first = FetchingIndex(simulation, -1);
                }

                Assert.That(first, Is.GreaterThanOrEqualTo(0), "Somebody sets off for the card.");
                simulation.KnockDownForTests(first);
                Assert.That(simulation.GetAgent(first).BodyState, Is.EqualTo(AgentBodyState.Unconscious), "Out cold.");

                int second = -1;
                bool took = false;
                for (int t = 0; t < 120 * Run.TicksPerSecond && second < 0 && !took; t++)
                {
                    simulation.Step();
                    second = FetchingIndex(simulation, first);
                    took = EventsOfType(simulation, CausalEventType.AgentTookKeycard).Count > 0;
                }

                Assert.That(second >= 0 || took, Is.True,
                    "With the first fetcher on the floor, the card is anybody's to go for again. " + WhoGaveUp(simulation, data));
                SimulationId down = simulation.GetAgent(first).AgentId;
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookKeycard).Exists(e => e.SourceId == down),
                    Is.False, "Not by the one lying on the floor.");
            }
        }

        [Test]
        public void AFetcherWhoTrips_KeepsTheirClaimOnTheCard()
        {
            ScenarioData data = TwoBraveStaffAtTheWayOut();
            data.Falls.PassOutChancePercent = 0;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.PutKeycardOnATableForTests(0);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                int first = -1;
                for (int t = 0; t < 120 * Run.TicksPerSecond && first < 0; t++)
                {
                    simulation.Step();
                    first = FetchingIndex(simulation, -1);
                }

                Assert.That(first, Is.GreaterThanOrEqualTo(0), "Somebody sets off for the card.");
                simulation.KnockDownForTests(first);
                Assert.That(simulation.GetAgent(first).BodyState, Is.EqualTo(AgentBodyState.Fallen));
                for (int t = 0; t < 3 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    Assert.That(FetchingIndex(simulation, first), Is.LessThan(0),
                        "Somebody on the floor for a moment is still the one going for it.");
                }
            }
        }

        [Test]
        public void SomebodyWhoseFetchForThePlayerFellThrough_StillTidiesAfterwards()
        {
            // A calm person left with the pocket flag from a fetch for the
            // card that came to nothing: the next tidy-up must still be a
            // tidy-up, not a card they are trying to pocket.
            ScenarioData data = scenario.ToRuntimeData();
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Items.TidyChancePercent = 100;
            data.Calm.DecisionMinimumTicks = 50;
            data.Calm.DecisionMaximumTicks = 100;
            data.Agents = new[]
            {
                new AgentDefinition(OfficeOrdinary, new LogicalPosition(-3000, -3000), CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            var things = new List<PhysicsObjectDefinition>(data.PhysicsObjects)
            {
                new PhysicsObjectDefinition(new SimulationId(3952UL), PhysicsObjectKind.Box, new LogicalPosition(-2000, -3000), 400, 3000)
            };
            data.PhysicsObjects = things.ToArray();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.AgentForTests(0).Carry.Pocket = true;
                bool carried = false;
                for (int t = 0; t < 60 * Run.TicksPerSecond && !carried; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.PhysicsObjectCount; i++)
                    {
                        PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                        carried |= thing.IsHeld && thing.Kind != PhysicsObjectKind.Keycard && thing.HeldBy == OfficeOrdinary;
                    }
                }

                Assert.That(carried, Is.True, "They pick something up to tidy it away.");
            }
        }

        [Test]
        public void AVisitorGivenTheCard_KnowsTheyHaveIt()
        {
            ScenarioData data = scenario.ToRuntimeData();
            using (var simulation = new Run(data, 42UL))
            {
                int visitor = Array.FindIndex(data.Agents, a => a.Familiarity == AgentFamiliarity.Visitor);
                simulation.GiveKeycardForTests(visitor);
                AgentKeycard belief = simulation.KeycardBeliefForTests(visitor);
                Assert.That(belief.Knows, Is.True);
                Assert.That(belief.WithSomebody, Is.True);
                Assert.That(belief.Holder, Is.EqualTo(visitor));
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Two brave members of staff just inside the way out, a single square
        /// of fire in the meeting room that never spreads, no traps, nothing on
        /// the timetable, and nobody calming down or freezing: a fetch for the
        /// card with nothing else going on.
        /// </summary>
        private ScenarioData TwoBraveStaffAtTheWayOut()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var brave = new AgentTraitValues(5, 5, 8, 5, 2, 3, 4);
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(14200, 14500), CardinalDirection.North, brave),
                new AgentDefinition(new SimulationId(2UL), new LogicalPosition(14900, 14000), CardinalDirection.North, brave)
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
            return data;
        }

        /// <summary>How many lived, counting everybody still alive as saved if the round was stopped short, as the left-alone runner does.</summary>
        private static int LivedByTheEnd(Run simulation, out int crowd)
        {
            RunSnapshot snapshot = simulation.NewSnapshotBuffer();
            simulation.FillSnapshot(snapshot);
            crowd = snapshot.CrowdSize;
            return simulation.Phase == RoundPhase.Over ? snapshot.SavedCount : snapshot.CrowdSize - snapshot.LostCount;
        }

        /// <summary>The first person on their way to the card, other than <paramref name="except"/>, or -1.</summary>
        private static int FetchingIndex(Run simulation, int except)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (i != except && simulation.GetAgent(i).ActivityState == AgentActivityState.FetchingKeycard)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>For a failing fetch: who gave the way out up, and what each of them believed about the card.</summary>
        private static string WhoGaveUp(Run simulation, ScenarioData data)
        {
            var lines = new List<string>();
            foreach (CausalEvent gaveUp in EventsOfType(simulation, CausalEventType.AgentGaveUpOnDoor))
            {
                if (gaveUp.TargetId != TheBuilding.TheWayOut)
                {
                    continue;
                }

                int index = Array.FindIndex(data.Agents, a => a.AgentId == gaveUp.SourceId);
                AgentKeycard belief = simulation.KeycardBeliefForTests(index);
                AgentSnapshot agent = simulation.GetAgent(index);
                lines.Add($"{gaveUp.SourceId.Value} at {gaveUp.Tick} (bravery {agent.Traits.Bravery}, knows {belief.Knows}, " +
                          $"with somebody {belief.WithSomebody}, may fetch from {belief.MayFetchFromTick}, now {agent.ActivityState} " +
                          $"{agent.FearState} {agent.Outcome} at {agent.Position})");
            }

            return lines.Count == 0 ? "Nobody gave the way out up." : string.Join("; ", lines);
        }

        /// <summary>For a failing swipe: what became of whoever took the card, and what they did at doors afterwards.</summary>
        private static string WhereTheHolderIs(Run simulation, ScenarioData data, CausalEvent took)
        {
            int index = Array.FindIndex(data.Agents, a => a.AgentId == took.SourceId);
            AgentSnapshot holder = simulation.GetAgent(index);
            PhysicsObjectSnapshot card = Thing(simulation, TheBuilding.TheKeycard);
            var doors = new List<string>();
            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.Tick > took.Tick && record.SourceId == took.SourceId &&
                    (record.EventType == CausalEventType.AgentTriedDoor || record.EventType == CausalEventType.AgentGaveUpOnDoor ||
                     record.EventType == CausalEventType.AgentHidFromTheHeat || record.EventType == CausalEventType.KeycardDropped ||
                     record.EventType == CausalEventType.AgentFroze || record.EventType == CausalEventType.AgentKnockedDown))
                {
                    doors.Add($"{record.EventType} {record.TargetId.Value} at {record.Tick}");
                }
            }

            return $"Holder {took.SourceId.Value} took it at {took.Tick}; now {holder.ActivityState} {holder.FearState} {holder.BodyState} " +
                   $"{holder.Outcome} at {holder.Position}; card held by {card.HeldBy.Value} at {card.Position}; " +
                   $"since then: {(doors.Count == 0 ? "nothing at any door" : string.Join(", ", doors))}; tick {simulation.Tick}.";
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

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

        private static DoorSnapshot Door(Run simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == id)
                {
                    return simulation.GetDoor(i);
                }
            }

            throw new KeyNotFoundException(id.ToString());
        }
    }
}

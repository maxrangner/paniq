using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Nudging people (prototype 3, 2026-09-25): a click on somebody makes
    /// them lurch, look round a beat later, and after a few nudges in a row
    /// get annoyed. A nudge is free, not a card, and frightens nobody.
    /// </summary>
    public sealed class NudgeEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);

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

        /// <summary>One ordinary person standing still in the middle of the office, facing north, with no fire due.</summary>
        private ScenarioData QuietRoom()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, TheBuilding.Office, CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Tables = Array.Empty<TableDefinition>();
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void ANudge_IsFree_AndShovesThemBackwards_AndTheyLookRoundABeatLater()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                LogicalPosition before = simulation.GetAgent(Somebody).Position;
                int purse = simulation.Purse;
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1);
                Advance(simulation, 20);

                List<CausalEvent> nudged = EventsOfType(simulation, CausalEventType.PowerNudged);
                Assert.That(nudged, Has.Count.EqualTo(1));
                Assert.That(nudged[0].TargetId, Is.EqualTo(Somebody));
                Assert.That(nudged[0].HasCausalParent, Is.False, "The player is the root cause.");
                Assert.That(simulation.Purse, Is.EqualTo(purse), "A nudge costs nothing.");

                LogicalPosition after = simulation.GetAgent(Somebody).Position;
                Assert.That(after.Z, Is.LessThan(before.Z - 50), "Facing north, the nudge sends them south.");

                List<CausalEvent> looked = EventsOfType(simulation, CausalEventType.AgentNudged);
                Assert.That(looked, Has.Count.EqualTo(1), "They look round for whoever did it.");
                Assert.That(looked[0].Tick, Is.GreaterThan(nudged[0].Tick), "Not on the tick of the nudge: every reaction comes a beat late.");
                Assert.That(looked[0].CausalParentEventId, Is.EqualTo(nudged[0].EventId));
                Assert.That(simulation.GetAgent(Somebody).FearState, Is.EqualTo(AgentFearState.Calm), "A nudge frightens nobody.");
            }
        }

        [Test]
        public void ThreeNudgesInARow_AnnoyThem()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1);
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 60);
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 120);
                Advance(simulation, 150);

                Assert.That(EventsOfType(simulation, CausalEventType.AgentNudged), Has.Count.EqualTo(2), "The first two only turn their head.");
                List<CausalEvent> annoyed = EventsOfType(simulation, CausalEventType.AgentAnnoyed);
                Assert.That(annoyed, Has.Count.EqualTo(1), "The third in a row is one too many.");
                Assert.That(annoyed[0].SourceId, Is.EqualTo(Somebody));
                Assert.That(annoyed[0].Tick, Is.GreaterThan(120));
            }
        }

        /// <summary>
        /// Two nudges a tick apart are one jolt: they turn round once, when the
        /// first nudge's reaction was due, naming the first nudge. The second
        /// used to push the reaction later and take it over, and the first
        /// nudge was left with no reaction at all.
        /// </summary>
        [Test]
        public void TwoNudgesInQuickSuccession_AreOneLookRound_AtTheFirstNudgesTime()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1);
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 2);
                Advance(simulation, 20);

                List<CausalEvent> nudged = EventsOfType(simulation, CausalEventType.PowerNudged);
                Assert.That(nudged, Has.Count.EqualTo(2));
                List<CausalEvent> looked = EventsOfType(simulation, CausalEventType.AgentNudged);
                Assert.That(looked, Has.Count.EqualTo(1), "One look round for the pair.");
                Assert.That(looked[0].CausalParentEventId, Is.EqualTo(nudged[0].EventId), "It answers the first nudge.");
                Assert.That(looked[0].Tick - nudged[0].Tick, Is.InRange(1, data.Perception.ReactionLagMaximumTicks),
                    "At the first nudge's own reaction tick, not pushed later by the second.");
            }
        }

        [Test]
        public void ThreeNudgesFarApart_AnnoyNobody()
        {
            ScenarioData data = QuietRoom();
            int apart = data.Nudge.AnnoyedWindowTicks + 10;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1);
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1 + apart);
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1 + 2 * apart);
                Advance(simulation, 3 * apart);

                Assert.That(EventsOfType(simulation, CausalEventType.AgentAnnoyed), Is.Empty);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentNudged), Has.Count.EqualTo(3));
            }
        }

        /// <summary>Somebody sitting down feels the nudge and looks round, but keeps their seat.</summary>
        [Test]
        public void SomebodySeated_KeepsTheirSeat()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            SimulationId seated = default;
            foreach (AgentDefinition person in data.Agents)
            {
                if (person.StartsSeated)
                {
                    seated = person.AgentId;
                    break;
                }
            }

            Assert.That(seated.Value, Is.Not.Zero, "The office has people who start seated.");
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                LogicalPosition before = simulation.GetAgent(seated).Position;
                simulation.QueueCommand(PlayerCommandType.NudgePerson, seated, simulation.Tick + 1);
                Advance(simulation, 20);

                Assert.That(simulation.GetAgent(seated).ActivityState, Is.EqualTo(AgentActivityState.Sitting), "Still in the chair.");
                Assert.That(IntegerMath.Distance(before, simulation.GetAgent(seated).Position), Is.LessThan(50), "Not shoved out of it.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentNudged), Has.Count.EqualTo(1), "But they felt it.");
            }
        }

        [Test]
        public void NudgingNobody_IsRefused()
        {
            using (var simulation = new Run(QuietRoom(), 42UL))
            {
                Assert.Throws<ArgumentException>(() =>
                    simulation.QueueCommand(PlayerCommandType.NudgePerson, new SimulationId(999UL), 1));
            }
        }

        [Test]
        public void TheStory_SaysWhoNudgedWhom()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, 1);
                Advance(simulation, 20);
                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.PowerNudged)[0]), Is.EqualTo("you nudged person 1"));
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.AgentNudged)[0]),
                    Is.EqualTo("person 1 looked round for whoever nudged them"));
            }
        }

        /// <summary>
        /// Since 2026-09-26 a nudge comes from where the click landed, and
        /// they step away from it: clicked from their west, they go east,
        /// whichever way they happen to face.
        /// </summary>
        [Test]
        public void ANudgeFromOneSide_SendsThemTheOtherWay_WhicheverWayTheyFace()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                LogicalPosition before = simulation.GetAgent(Somebody).Position;
                var fromTheWest = new LogicalPosition(before.X - 400, before.Z);
                simulation.QueueCommand(PlayerCommandType.NudgePersonFrom, Somebody, fromTheWest, 1);
                Advance(simulation, 20);

                LogicalPosition after = simulation.GetAgent(Somebody).Position;
                Assert.That(after.X, Is.GreaterThan(before.X + 50), "Nudged from the west, they went east.");
                Assert.That(Math.Abs(after.Z - before.Z), Is.LessThan(after.X - before.X), "Not backwards from the way they face (north).");
            }
        }

        /// <summary>
        /// Annoyed, they shake with it, and for a while another nudge does
        /// nothing to them at all: no lurch, no look round (the owner's rule,
        /// 2026-09-26).
        /// </summary>
        [Test]
        public void WhileAnnoyed_ANudgeDoesNothingButGetWrittenDown()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data, 42UL))
            {
                for (int i = 0; i < 3; i++)
                {
                    simulation.QueueCommand(PlayerCommandType.NudgePerson, Somebody, simulation.Tick + 1);
                    Advance(simulation, 40);
                }

                Assert.That(EventsOfType(simulation, CausalEventType.AgentAnnoyed), Has.Count.EqualTo(1), "Three in a row: annoyed.");
                Assert.That(simulation.GetAgent(Somebody).IsAnnoyed, "And shaking with it.");

                LogicalPosition before = simulation.GetAgent(Somebody).Position;
                int lookedBefore = EventsOfType(simulation, CausalEventType.AgentNudged).Count;
                simulation.QueueCommand(PlayerCommandType.NudgePersonFrom, Somebody,
                    new LogicalPosition(before.X - 400, before.Z), simulation.Tick + 1);
                Advance(simulation, 2);
                Assert.That(simulation.GetAgent(Somebody).BodyState, Is.Not.EqualTo(AgentBodyState.Staggering),
                    "Annoyed, a nudge does not jolt them.");
                Advance(simulation, 38);
                Assert.That(EventsOfType(simulation, CausalEventType.AgentNudged), Has.Count.EqualTo(lookedBefore),
                    "Nor do they look round for it.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerNudged), Has.Count.EqualTo(4), "But it is written down.");
            }
        }
    }
}

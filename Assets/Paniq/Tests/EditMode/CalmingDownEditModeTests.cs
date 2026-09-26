using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Calming down (prototype 3, second batch, 2026-09-26). A frightened
    /// person who sees and hears nothing frightening for a while settles, at
    /// a pace their own personality sets: the brave in a few seconds, the
    /// nervous slowly, the very nervous never. Anybody who saw the danger stays
    /// rattled for a while afterwards, and a thud frightens them outright. The
    /// owner's words: "the ones that saw the fire stay rattled for a while, the
    /// nervous might freak out more, the calm and brave don't really care --
    /// this should be the personality system in play, not scripted."
    /// </summary>
    public sealed class CalmingDownEditModeTests
    {
        private static readonly AgentTraitValues Hero = new AgentTraitValues(5, 5, 10, 5, 2, 1);
        private static readonly AgentTraitValues Worrier = new AgentTraitValues(5, 5, 3, 5, 2, 7);
        private static readonly AgentTraitValues NervousWreck = new AgentTraitValues(5, 5, 2, 5, 2, 10);

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

        /// <summary>
        /// The office with nothing burning and nothing on the timetable, and
        /// these people standing in it, far enough apart not to bump. Nobody
        /// freezes, so everybody frightened runs.
        /// </summary>
        private ScenarioData Office(params AgentTraitValues[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            var agents = new AgentDefinition[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                agents[i] = new AgentDefinition(new SimulationId((ulong)(i + 1)),
                    new LogicalPosition(-4000 + i * 1500, -4000), CardinalDirection.North, people[i]);
            }

            data.Agents = agents;
            data.Fire.ActivationTick = int.MaxValue;
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Falls.TripChancePercent = 0;
            return data;
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

        private static int CalmedAt(Run simulation, int index)
        {
            SimulationId who = simulation.GetAgent(index).AgentId;
            CausalEvent found = EventsOfType(simulation, CausalEventType.AgentCalmedDown).Find(e => e.SourceId == who);
            return found.EventId == 0UL ? -1 : found.Tick;
        }

        [Test]
        public void ABravePerson_WhoSeesAndHearsNothingMore_CalmsDownInAFewSeconds_AndHeadsBackToTheirDesk()
        {
            ScenarioData data = Office(Hero);
            data.Agents[0] = data.Agents[0].WithHome(new LogicalPosition(-4000, -1000));
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                for (int t = 0; t < 20 * Run.TicksPerSecond && CalmedAt(simulation, 0) < 0; t++)
                {
                    simulation.Step();
                }

                int calmed = CalmedAt(simulation, 0);
                Assert.That(calmed, Is.GreaterThan(0), "Nothing to be frightened of: they settled.");
                Assert.That(calmed - 5, Is.InRange(8 * Run.TicksPerSecond, 14 * Run.TicksPerSecond),
                    "A quiet spell of about five seconds, and a hero's fear gone in about five more.");
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm));
                Assert.That(simulation.ErrandForTests(0).Cue, Is.EqualTo(CueKind.GoHome), "Back to their own desk.");
            }
        }

        [Test]
        public void TheBrave_CalmSoonerThanTheNervous()
        {
            using (var simulation = new Run(Office(Hero, Worrier), 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                simulation.FrightenForTests(1);
                for (int t = 0; t < 40 * Run.TicksPerSecond && CalmedAt(simulation, 1) < 0; t++)
                {
                    simulation.Step();
                }

                int hero = CalmedAt(simulation, 0);
                int worrier = CalmedAt(simulation, 1);
                Assert.That(hero, Is.GreaterThan(0));
                Assert.That(worrier, Is.GreaterThan(0), "A worrier settles too, inside forty seconds.");
                Assert.That(worrier - hero, Is.GreaterThan(10 * Run.TicksPerSecond), "But a good while after the hero.");
            }
        }

        [Test]
        public void TheNervousWreck_NeverCalmsDown_AndKeepsLeaving()
        {
            using (var simulation = new Run(Office(NervousWreck), 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                Advance(simulation, 40 * Run.TicksPerSecond);

                Assert.That(CalmedAt(simulation, 0), Is.EqualTo(-1),
                    "Forty seconds of nothing -- longer than a worrier takes to settle -- and still frightened.");
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Scared));
            }
        }

        [Test]
        public void WhileABellRings_NobodyInEarshotCalmsDown()
        {
            using (var simulation = new Run(Office(Hero), 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                simulation.QueueCommand(PlayerCommandType.PullAlarm, TheBuilding.TheAlarm, simulation.Tick + 1);
                Advance(simulation, 40 * Run.TicksPerSecond);

                Assert.That(CalmedAt(simulation, 0), Is.EqualTo(-1),
                    "Forty seconds of bells, and even a hero stays frightened.");
            }
        }

        [Test]
        public void WhileTheFireIsInTheirRoom_NobodyCalmsDown()
        {
            ScenarioData data = Office(Hero);
            data.Fire.ActivationTick = 5;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 100;
            TheBuilding.FireAt(data, TheBuilding.OfficeSouthEastCorner);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 10);
                simulation.FrightenForTests(0);
                Advance(simulation, 40 * Run.TicksPerSecond);

                Assert.That(CalmedAt(simulation, 0), Is.EqualTo(-1),
                    "Frozen in a room that is burning: the fire is right there, so they never settle.");
            }
        }

        [Test]
        public void NoTwoPeople_CalmDownOnTheSameTick()
        {
            using (var simulation = new Run(Office(Hero, Hero, Hero, Hero, Hero, Hero), 42UL))
            {
                Advance(simulation, 5);
                for (int i = 0; i < 6; i++)
                {
                    simulation.FrightenForTests(i);
                }

                Advance(simulation, 30 * Run.TicksPerSecond);
                var ticks = new HashSet<int>();
                foreach (CausalEvent calmed in EventsOfType(simulation, CausalEventType.AgentCalmedDown))
                {
                    Assert.That(ticks.Add(calmed.Tick), $"Two people settled on tick {calmed.Tick}.");
                }

                Assert.That(ticks, Has.Count.EqualTo(6), "All six settled.");
            }
        }

        [Test]
        public void SomebodyWhoCalmedDown_StaysRattled_AndAThudNearbyFrightensThemAgain()
        {
            ScenarioData data = Office(Hero, Hero);

            // The second one well out of earshot of the first one's shouting.
            data.Agents[1] = new AgentDefinition(new SimulationId(2UL), TheBuilding.Cafeteria, CardinalDirection.North, Hero);
            using (var simulation = new Run(data, 42UL))
            {
                Advance(simulation, 5);
                simulation.FrightenForTests(0);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm), "They settled.");
                Assert.That(simulation.AgentForTests(0).Fear.IsRattledAt(simulation.Tick), "But they are rattled.");

                // The same thud beside each of them: the one never frightened
                // only turns to look; the one who was is frightened again.
                simulation.MakeANoiseForTests(simulation.GetAgent(0).Position);
                simulation.MakeANoiseForTests(simulation.GetAgent(1).Position);
                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                    "Rattled, a thud beside them is a fright.");
                Assert.That(simulation.GetAgent(1).FearState, Is.EqualTo(AgentFearState.Calm),
                    "Somebody who was never frightened only turns to look.");
            }
        }
    }
}

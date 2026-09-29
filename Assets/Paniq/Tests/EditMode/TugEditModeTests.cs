using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The tug (2026-09-29, the owner's rule): the player's hand on a person
    /// "holds them in place. Should not be 100% instant, more like tugging
    /// someone's shirt. So you can save someone running into fire." It lasts
    /// as long as the button is held; "the strongest can break free. Sliding
    /// scale. A slightly not too strong can eventually break free by visibly
    /// shaking you off."
    /// </summary>
    public sealed class TugEditModeTests
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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>
        /// One person in the middle of the office, a fire far off in the
        /// stockroom that never spreads, and nothing else going on: frightened,
        /// they run for a door, and nothing but the hand stops them.
        /// </summary>
        private ScenarioData RunnerInTheOffice(AgentTraitValues traits)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, TheBuilding.Office, CardinalDirection.North, traits)
            };
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            TheBuilding.FireAt(data, TheBuilding.Stockroom);
            data.TrapDefinitions = Array.Empty<TrapDefinition>();
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Calming.Enabled = false;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            return data;
        }

        private static void Tug(Run simulation)
        {
            simulation.QueueCommand(PlayerCommandType.TugPerson, Somebody, simulation.Tick + 1);
            simulation.Step();
        }

        private static void LetGo(Run simulation)
        {
            simulation.QueueCommand(PlayerCommandType.ReleaseTug, Somebody, simulation.Tick + 1);
            simulation.Step();
        }

        private static int SpeedOf(Run simulation) => simulation.GetAgent(Somebody).SpeedMillimetresPerTick;

        [Test]
        public void ATug_SlowsARunnerToAStopOverAboutASecond_AndHoldsThemThere_UntilLetGoOf()
        {
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.FrightenForTests(0);
                Advance(simulation, 40);
                int running = SpeedOf(simulation);
                Assert.That(running, Is.GreaterThan(30), "Frightened, they are running.");

                Tug(simulation);
                List<CausalEvent> tugged = EventsOfType(simulation, CausalEventType.PowerTugged);
                Assert.That(tugged, Has.Count.EqualTo(1));
                Assert.That(tugged[0].TargetId, Is.EqualTo(Somebody));
                Assert.That(tugged[0].HasCausalParent, Is.False, "The player is the root cause.");
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.True);
                Assert.That(SpeedOf(simulation), Is.GreaterThan(0), "Not stopped dead: a hand on a shirt, not a wall.");

                Advance(simulation, 10);
                Assert.That(SpeedOf(simulation), Is.LessThan(running), "Slowing.");
                LogicalPosition where = simulation.GetAgent(Somebody).Position;
                Advance(simulation, 2 * Run.TicksPerSecond);
                Assert.That(SpeedOf(simulation), Is.Zero, "Stopped inside a couple of seconds.");
                Assert.That(IntegerMath.Distance(simulation.GetAgent(Somebody).Position, where), Is.LessThan(1500),
                    "And held about where they were caught.");
                Assert.That(simulation.GetAgent(Somebody).FearState, Is.EqualTo(AgentFearState.Scared), "Still frightened: the hand changes nothing about that.");

                Advance(simulation, 10 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.True, "An ordinary person stays as long as the hand is on them.");
                Assert.That(SpeedOf(simulation), Is.Zero);

                LetGo(simulation);
                List<CausalEvent> released = EventsOfType(simulation, CausalEventType.PowerReleasedTug);
                Assert.That(released, Has.Count.EqualTo(1));
                Assert.That(released[0].CausalParentEventId, Is.EqualTo(tugged[0].EventId));
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.False);
                Advance(simulation, 2 * Run.TicksPerSecond);
                Assert.That(SpeedOf(simulation), Is.GreaterThan(30), "Let go of, they are on their own again and running.");
            }
        }

        [Test]
        public void TheTugged_LookRoundForWhoeverHasThem_ABeatLater_AndAreNotAnnoyed()
        {
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary);
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            using (var simulation = new Run(data, 42UL))
            {
                Tug(simulation);
                Advance(simulation, 20);
                List<CausalEvent> looked = EventsOfType(simulation, CausalEventType.AgentNudged);
                Assert.That(looked, Has.Count.EqualTo(1), "They look round for whoever has hold of them.");
                Assert.That(looked[0].Tick, Is.GreaterThan(EventsOfType(simulation, CausalEventType.PowerTugged)[0].Tick),
                    "Not on the tick of the tug: every reaction comes a beat late.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentAnnoyed), Is.Empty, "A tug is not a poke: nobody gets annoyed.");
                Assert.That(simulation.GetAgent(Somebody).FearState, Is.EqualTo(AgentFearState.Calm), "A tug frightens nobody.");
            }
        }

        /// <summary>
        /// The sliding scale: below the threshold, held for ever; at it, a
        /// good while; the strongest in a moment. Every one who tears free
        /// does so visibly and is written down.
        /// </summary>
        [TestCase(5, false)]
        [TestCase(6, true)]
        [TestCase(9, true)]
        public void TheStrong_TearFree_SoonerTheStronger(int strength, bool tearsFree)
        {
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary.With(AgentTrait.Strength, strength));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.FrightenForTests(0);
                Advance(simulation, 20);
                Tug(simulation);
                int tuggedAt = simulation.Tick;
                int freeAt = -1;
                for (int t = 0; t < 20 * Run.TicksPerSecond && freeAt < 0; t++)
                {
                    simulation.Step();
                    if (EventsOfType(simulation, CausalEventType.AgentShookFree).Count > 0)
                    {
                        freeAt = simulation.Tick;
                    }
                }

                Assert.That(freeAt >= 0, Is.EqualTo(tearsFree),
                    tearsFree ? "Strong enough to tear free." : "Not strong enough: held as long as the hand stays.");
                if (!tearsFree)
                {
                    Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.True);
                    return;
                }

                int held = freeAt - tuggedAt;
                int threshold = data.Tug.TearFreeTicksAtThreshold;
                int jitter = threshold * data.World.TimingJitterPercent / 100;
                if (strength == data.Tug.TearsFreeFromStrength)
                {
                    Assert.That(held, Is.InRange(threshold - jitter, threshold + jitter), "At the threshold, about eight seconds.");
                }
                else
                {
                    Assert.That(held, Is.LessThan(threshold / 4), "The brute shrugs it off in a moment.");
                }

                CausalEvent shook = EventsOfType(simulation, CausalEventType.AgentShookFree)[0];
                Assert.That(shook.SourceId, Is.EqualTo(Somebody));
                Assert.That(shook.CausalParentEventId, Is.EqualTo(EventsOfType(simulation, CausalEventType.PowerTugged)[0].EventId));
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.False, "The hand is off them.");
                Assert.That(simulation.GetAgent(Somebody).IsShakingFree, Is.True, "And the shake is shown.");
                Advance(simulation, data.Tug.ShookFreeShownTicks + 5);
                Assert.That(simulation.GetAgent(Somebody).IsShakingFree, Is.False, "For a couple of seconds.");
                Assert.That(simulation.TugsForTests.HeldIndex, Is.EqualTo(-1));
            }
        }

        [Test]
        public void SomebodyAlight_CannotBeHeld_AndSomebodyWhoCatchesFireWhileHeld_IsLetGoOf()
        {
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.FrightenForTests(0);
                Tug(simulation);
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.True);
                simulation.SetAlightForTests(0);
                Advance(simulation, 2);
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.False, "Alight, there is nothing to hold: the hand is off them.");

                Tug(simulation);
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.False, "And they cannot be taken hold of again.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerTugged), Has.Count.EqualTo(1), "Nothing written for a tug that took nobody.");
            }
        }

        /// <summary>One hand: taking hold of a second person lets go of the first.</summary>
        [Test]
        public void OneHand_ASecondTug_LetsGoOfTheFirst()
        {
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary);
            var other = new SimulationId(2UL);
            data.Agents = new[]
            {
                data.Agents[0],
                new AgentDefinition(other, TheBuilding.OfficeFarCorner, CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            using (var simulation = new Run(data, 42UL))
            {
                Tug(simulation);
                simulation.QueueCommand(PlayerCommandType.TugPerson, other, simulation.Tick + 1);
                simulation.Step();
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.False, "The first is let go of.");
                Assert.That(simulation.GetAgent(other).IsTugged, Is.True, "The hand is on the second.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerReleasedTug), Has.Count.EqualTo(1));
                Assert.That(simulation.TugsForTests.HeldIndex, Is.EqualTo(1));
            }
        }

        /// <summary>The point of it, in the owner's words: "so you can save someone running into fire".</summary>
        [Test]
        public void ATug_StopsSomebodyShortOfTheFlames()
        {
            // The fire a few metres east of somebody who will be sent running
            // at it: their way out is through it, and a brave person dashes.
            ScenarioData data = RunnerInTheOffice(AgentTraitValues.AllOrdinary.With(AgentTrait.Bravery, 8));
            TheBuilding.FireAt(data, new LogicalPosition(3000, 0));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.FrightenForTests(0);
                Advance(simulation, 15);
                Tug(simulation);
                Advance(simulation, 20 * Run.TicksPerSecond);
                Assert.That(simulation.GetAgent(Somebody).IsBurning, Is.False, "Held by the shirt, they never reach the flames.");
                Assert.That(simulation.GetAgent(Somebody).Outcome, Is.EqualTo(AgentTerminalOutcome.Unresolved));
                Assert.That(simulation.GetAgent(Somebody).IsTugged, Is.True);
            }
        }
    }
}

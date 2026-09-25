using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Hard knocks: being knocked out cold, and strong people breaking locked doors down.</summary>
    public sealed class HardKnocksEditModeTests
    {
        private static readonly SimulationId OfficeWayOut = new SimulationId(2001UL);

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
        /// The shipped floor, with a purse behind the player: a test here
        /// opens the way out so a panicking crowd has somewhere to run, and a
        /// round opens with nothing to spend it on.
        /// </summary>
        private ScenarioData DefaultData() =>
            TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());

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

        // ---------------------------------------------------------------- knocked out

        [Test]
        public void PassOutChance_IsHigherForHarderHitsAndLowerForTheStrong()
        {
            ScenarioData data = DefaultData();
            var ordinary = new Agent(0, new SimulationId(1UL), 0) { Traits = AgentTraitValues.AllOrdinary };
            var strong = new Agent(1, new SimulationId(2UL), 0) { Traits = new AgentTraitValues(10, 5, 5, 5, 5, 5) };
            var frail = new Agent(2, new SimulationId(3UL), 0) { Traits = new AgentTraitValues(0, 5, 5, 5, 5, 5) };

            Assert.That(TraitEffects.PassOutChancePercent(ordinary, data, data.Falls.PassOutChancePercent),
                Is.EqualTo(data.Falls.PassOutChancePercent));
            Assert.That(TraitEffects.PassOutChancePercent(strong, data, data.Falls.PassOutChancePercent), Is.EqualTo(0));
            Assert.That(TraitEffects.PassOutChancePercent(frail, data, data.Falls.PassOutChancePercent),
                Is.GreaterThan(data.Falls.PassOutChancePercent));
            Assert.That(TraitEffects.PassOutChancePercent(frail, data, 500), Is.EqualTo(data.Falls.PassOutMaximumPercent),
                "Never more likely than the cap.");
        }

        /// <summary>
        /// A heavy box slams into someone standing still. With every
        /// knock-down set to knock people out, they lie still with stars for
        /// a while, come to, get up slowly and carry on.
        /// </summary>
        [Test]
        public void HardHit_KnocksSomeoneOutColdThenTheyComeToAndGetUp()
        {
            ScenarioData data = DefaultData();
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Box,
                    new LogicalPosition(-2000, 0), 600, 20000)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Falls.PassOutChancePercent = 100;
            data.Falls.PassOutMaximumPercent = 100;
            var simulation = new Run(data);
            simulation.LaunchObjectForTests(0, 110, 0);

            int outSince = -1;
            int outTicks = 0;
            LogicalPosition lyingAt = default;
            for (int t = 0; t < 1000; t++)
            {
                simulation.Step();
                AgentSnapshot agent = simulation.GetAgent(0);
                if (agent.BodyState == AgentBodyState.Unconscious)
                {
                    if (outSince < 0)
                    {
                        outSince = simulation.Tick;
                        lyingAt = agent.Position;
                    }

                    // Out cold, they only slide on from the fall and settle;
                    // they never get up and go anywhere.
                    outTicks++;
                    Assert.That(LogicalPosition.DistanceSquared(agent.Position, lyingAt), Is.LessThanOrEqualTo(800L * 800L),
                        "Someone out cold never moves.");
                    Assert.That(agent.IsDown, Is.True);
                }
            }

            Assert.That(outSince, Is.GreaterThan(0), "The box never knocked them out.");
            Assert.That(outTicks, Is.InRange(data.Falls.UnconsciousMinimumTicks, data.Falls.UnconsciousMaximumTicks));
            Assert.That(simulation.GetAgent(0).BodyState, Is.EqualTo(AgentBodyState.Upright), "They never got back up.");

            List<CausalEvent> passedOut = EventsOfType(simulation, CausalEventType.AgentPassedOut);
            List<CausalEvent> cameTo = EventsOfType(simulation, CausalEventType.AgentCameTo);
            List<CausalEvent> gotUp = EventsOfType(simulation, CausalEventType.AgentGotUp);
            Assert.That(passedOut, Has.Count.EqualTo(1));
            Assert.That(cameTo, Has.Count.EqualTo(1));
            Assert.That(simulation.EventLog.Get(passedOut[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.AgentTripped), "Knocked out by the fall the box caused.");
            Assert.That(cameTo[0].CausalParentEventId, Is.EqualTo(passedOut[0].EventId));
            Assert.That(gotUp[gotUp.Count - 1].CausalParentEventId, Is.EqualTo(cameTo[0].EventId));
            Assert.That(cameTo[0].Tick - passedOut[0].Tick, Is.EqualTo(passedOut[0].DurationTicks));
            // Their own while, near the setting: every length of time a person
            // spends is stretched or squeezed a little from the seed, so that
            // nobody does the same thing on exactly the same tick as anybody
            // else. The event says how long it really took.
            Assert.That(gotUp[gotUp.Count - 1].Tick - cameTo[0].Tick, Is.EqualTo(cameTo[0].DurationTicks));
            int jitter = data.Falls.ComeToGetUpTicks * data.World.TimingJitterPercent / 100;
            Assert.That(cameTo[0].DurationTicks,
                Is.InRange(data.Falls.ComeToGetUpTicks - jitter, data.Falls.ComeToGetUpTicks + jitter));
        }

        [Test]
        public void PanickedCrowds_SometimesKnockSomeoneOut()
        {
            int knockouts = 0;
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                var simulation = new Run(DefaultData(), seed);
                for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.AgentPassedOut))
                {
                    knockouts++;
                    CausalEventType cause = simulation.EventLog.Get(record.CausalParentEventId).EventType;
                    Assert.That(cause == CausalEventType.AgentKnockedDown || cause == CausalEventType.AgentTripped ||
                                cause == CausalEventType.AgentCrushed,
                        Is.True, $"Seed {seed}: knocked out by {cause}.");
                }
            }

            Assert.That(knockouts, Is.GreaterThan(0), "Nobody was ever knocked out cold in seven minutes of panic.");
        }

        // ---------------------------------------------------------------- breaking doors

        [Test]
        public void StrongRunner_BreaksALockedDoorDownAndEscapesThroughIt()
        {
            ScenarioData data = DoorsEditModeTests.RunnerByTheWayOut(
                DefaultData(), 100, new AgentTraitValues(10, 5, 5, 5, 5, 5));
            data.Exits.DoorStrength = 12;
            var simulation = new Run(data);
            int shovesBeforeBreaking = 0;
            for (int t = 0; t < 10 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome == AgentTerminalOutcome.Unresolved; t++)
            {
                simulation.Step();
                DoorSnapshot door = Door(simulation, OfficeWayOut);
                if (door.State == DoorState.Locked)
                {
                    shovesBeforeBreaking = EventsOfType(simulation, CausalEventType.AgentForcedDoor).Count;
                    Assert.That(door.DamagePercent, Is.EqualTo(Math.Min(100, shovesBeforeBreaking * 4 * 100 / 12)),
                        "Each Str 10 shove does 4 damage.");
                }
            }

            Assert.That(shovesBeforeBreaking, Is.EqualTo(2), "The third 4-damage shove breaks a 12-strength door.");
            List<CausalEvent> broken = EventsOfType(simulation, CausalEventType.DoorBrokenDown);
            Assert.That(broken, Has.Count.EqualTo(1), "The strong runner never broke the door.");
            Assert.That(broken[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
            Assert.That(broken[0].TargetId, Is.EqualTo(OfficeWayOut));
            Assert.That(simulation.EventLog.Get(broken[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.AgentForcedDoor));
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Broken));

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            List<CausalEvent> escaped = EventsOfType(simulation, CausalEventType.AgentEscaped);
            Assert.That(escaped[0].CausalParentEventId, Is.EqualTo(broken[0].EventId), "The escape traces back to the break.");

            // A broken door cannot be clicked shut or locked again.
            simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, simulation.Tick + 1);
            simulation.Step();
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Broken));
        }

        [Test]
        public void OrdinaryRunner_NeverBreaksADoorDown()
        {
            ScenarioData data = DoorsEditModeTests.RunnerByTheWayOut(
                DefaultData(), 100, AgentTraitValues.AllOrdinary);
            data.Exits.DoorStrength = 1;
            var simulation = new Run(data);
            for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.AgentForcedDoor), Is.Not.Empty);
            Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Empty);
            Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Locked));
            Assert.That(Door(simulation, OfficeWayOut).DamagePercent, Is.EqualTo(0), "Ordinary shoulders do no damage.");
        }

        [Test]
        public void DoorDamage_StartsAtTheMinimumStrength()
        {
            ScenarioData data = DefaultData();
            int minimum = data.Traits.DoorBreakMinimumStrength;
            var justBelow = new Agent(0, new SimulationId(1UL), 0) { Traits = new AgentTraitValues(minimum - 1, 5, 5, 5, 5, 5) };
            var atMinimum = new Agent(0, new SimulationId(1UL), 0) { Traits = new AgentTraitValues(minimum, 5, 5, 5, 5, 5) };
            var strongest = new Agent(0, new SimulationId(1UL), 0) { Traits = new AgentTraitValues(10, 5, 5, 5, 5, 5) };
            Assert.That(TraitEffects.DoorShoveDamage(justBelow, data), Is.EqualTo(0));
            Assert.That(TraitEffects.DoorShoveDamage(atMinimum, data), Is.EqualTo(data.Traits.DoorDamagePerPoint));
            Assert.That(TraitEffects.DoorShoveDamage(strongest, data), Is.GreaterThan(TraitEffects.DoorShoveDamage(atMinimum, data)));
            Assert.That(TraitEffects.DoorForceChancePercent(strongest, data), Is.GreaterThan(data.Exits.DoorForceChancePercent));
        }
    }
}

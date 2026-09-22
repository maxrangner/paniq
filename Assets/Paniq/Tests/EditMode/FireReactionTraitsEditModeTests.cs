using System;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Personalities: authored and drawn traits, and what they change.</summary>
    public sealed class FireReactionTraitsEditModeTests
    {
        private FireReactionScenario scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = FireReactionScenario.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private FireReactionScenarioData DefaultData() => scenario.ToRuntimeData();

        private static FireReactionAgentDefinition Person(ulong id, int x, int z, AgentTraitValues traits)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), CardinalDirection.North, traits);
        }

        private static FireReactionAgentDefinition Undecided(ulong id, int x, int z)
        {
            return new FireReactionAgentDefinition(new SimulationId(id), new LogicalPosition(x, z), CardinalDirection.North);
        }

        private static Agent AgentWith(AgentTraitValues traits)
        {
            return new Agent(0, new SimulationId(1UL), 0) { Traits = traits };
        }

        [Test]
        public void DefaultCast_StartsWithItsAuthoredTraits()
        {
            FireReactionScenarioData data = DefaultData();
            var simulation = new FireReactionSimulation(data);
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                Assert.That(data.Agents[i].HasAuthoredTraits, Is.True);
                Assert.That(simulation.GetAgent(i).Traits, Is.EqualTo(data.Agents[i].Traits), $"agent {i + 1}");
            }
        }

        [Test]
        public void DrawnTraits_AreInRangeAndReplayable()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[] { Undecided(1UL, -3000, 0), Undecided(2UL, 0, 0), Undecided(3UL, 3000, 0) };
            var first = new FireReactionSimulation(data, 7UL);
            var again = new FireReactionSimulation(data, 7UL);
            bool differsAcrossSeeds = false;
            for (int seed = 1; seed <= 5 && !differsAcrossSeeds; seed++)
            {
                differsAcrossSeeds = !new FireReactionSimulation(data, (ulong)seed).GetAgent(0).Traits.Equals(first.GetAgent(0).Traits);
            }

            for (int i = 0; i < first.AgentCount; i++)
            {
                AgentTraitValues traits = first.GetAgent(i).Traits;
                Assert.That(traits.IsValid, Is.True, traits.ToString());
                Assert.That(again.GetAgent(i).Traits, Is.EqualTo(traits), "Same seed, same personality.");
            }

            Assert.That(differsAcrossSeeds, Is.True, "Other seeds should draw other personalities.");
        }

        [Test]
        public void AuthoredTraitOutsideZeroToTen_IsRejected()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[] { Person(1UL, 0, 0, new AgentTraitValues(11, 5, 5, 5, 5, 5)) };
            Assert.Throws<InvalidOperationException>(() => data.Validate());
        }

        [Test]
        public void Temperaments_GoToTheMostFearfulFirst()
        {
            var simulation = new FireReactionSimulation(DefaultData());
            int lowestFreezer = int.MaxValue;
            int highestRunner = int.MinValue;
            int freezeForever = 0;
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                int fear = agent.Traits.Nervousness - agent.Traits.Bravery;
                if (agent.Temperament == AgentPanicTemperament.Runner)
                {
                    highestRunner = Math.Max(highestRunner, fear);
                }
                else
                {
                    lowestFreezer = Math.Min(lowestFreezer, fear);
                }

                if (agent.Temperament == AgentPanicTemperament.FreezeForever)
                {
                    freezeForever++;
                    Assert.That(agent.AgentId.Value, Is.EqualTo(1006UL).Or.EqualTo(1009UL).Or.EqualTo(1015UL),
                        "The nervous wreck, the coward and the timid carer are the most fearful.");
                }
            }

            Assert.That(freezeForever, Is.EqualTo(3), "15% of twenty people.");
            Assert.That(lowestFreezer, Is.GreaterThanOrEqualTo(highestRunner));
        }

        [Test]
        public void SpeedTrait_SetsWalkingAndSprintingPace()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                Person(1UL, -3000, 0, new AgentTraitValues(5, 0, 5, 5, 5, 5)),
                Person(2UL, 0, 0, new AgentTraitValues(5, 5, 5, 5, 5, 5)),
                Person(3UL, 3000, 0, new AgentTraitValues(5, 10, 5, 5, 5, 5))
            };
            var simulation = new FireReactionSimulation(data);
            FireReactionAgentSnapshot slow = simulation.GetAgent(0);
            FireReactionAgentSnapshot ordinary = simulation.GetAgent(1);
            FireReactionAgentSnapshot fast = simulation.GetAgent(2);
            int calmJitter = data.Traits.CalmSpeedJitter;
            int panicJitter = data.Traits.PanicSpeedJitter;

            Assert.That(slow.CalmSpeedMillimetresPerTick, Is.InRange(data.Calm.SpeedMinimum - calmJitter, data.Calm.SpeedMinimum + calmJitter));
            Assert.That(fast.CalmSpeedMillimetresPerTick, Is.InRange(data.Calm.SpeedMaximum - calmJitter, data.Calm.SpeedMaximum + calmJitter));
            Assert.That(slow.PanicSpeedMillimetresPerTick, Is.InRange(data.Panic.SpeedMinimum - panicJitter, data.Panic.SpeedMinimum + panicJitter));
            Assert.That(fast.PanicSpeedMillimetresPerTick, Is.InRange(data.Panic.SpeedMaximum - panicJitter, data.Panic.SpeedMaximum + panicJitter));
            Assert.That(fast.PanicSpeedMillimetresPerTick, Is.LessThanOrEqualTo(data.World.MaximumStepDistanceMillimetres));
            Assert.That(ordinary.PanicSpeedMillimetresPerTick, Is.GreaterThan(slow.PanicSpeedMillimetresPerTick));
            Assert.That(fast.PanicSpeedMillimetresPerTick, Is.GreaterThan(ordinary.PanicSpeedMillimetresPerTick));
        }

        [Test]
        public void OrdinaryPerson_BehavesExactlyAsTheSettingsSay()
        {
            FireReactionScenarioData data = DefaultData();
            Agent ordinary = AgentWith(AgentTraitValues.AllOrdinary);
            Assert.That(TraitEffects.DangerDistance(ordinary, data), Is.EqualTo(data.Panic.DangerDistanceMillimetres));
            Assert.That(TraitEffects.MaximumReactionDelayTicks(ordinary, data), Is.EqualTo(data.Perception.MaximumReactionDelayTicks));
            Assert.That(TraitEffects.SwerveChancePercent(ordinary, data), Is.EqualTo(data.Panic.SwerveChancePercent));
            Assert.That(TraitEffects.HesitateChancePercent(ordinary, data), Is.EqualTo(data.Panic.HesitateChancePercent));
            Assert.That(TraitEffects.TripChancePercent(ordinary, data), Is.EqualTo(data.Falls.TripChancePercent));
            Assert.That(TraitEffects.PanicPeopleAvoidPercent(ordinary, data), Is.EqualTo(data.Panic.PeopleAvoidPercent));
            Assert.That(TraitEffects.BumpMinimumSpeed(ordinary, data), Is.EqualTo(data.Falls.BumpMinimumSpeed));
            Assert.That(TraitEffects.PushMassGrams(ordinary, data), Is.EqualTo(data.ObjectPhysics.AgentMassGrams));
        }

        [Test]
        public void Traits_PushBehaviourTheExpectedWay()
        {
            FireReactionScenarioData data = DefaultData();
            Agent ordinary = AgentWith(AgentTraitValues.AllOrdinary);
            Agent brave = AgentWith(new AgentTraitValues(5, 5, 10, 5, 5, 5));
            Agent nervous = AgentWith(new AgentTraitValues(5, 5, 5, 5, 5, 10));
            Agent kind = AgentWith(new AgentTraitValues(5, 5, 5, 10, 0, 5));
            Agent cruel = AgentWith(new AgentTraitValues(5, 5, 5, 0, 10, 5));
            Agent strong = AgentWith(new AgentTraitValues(9, 5, 5, 5, 5, 5));

            Assert.That(TraitEffects.DangerDistance(brave, data), Is.LessThan(TraitEffects.DangerDistance(ordinary, data)));
            Assert.That(TraitEffects.MaximumReactionDelayTicks(brave, data),
                Is.LessThan(TraitEffects.MaximumReactionDelayTicks(ordinary, data)));
            Assert.That(TraitEffects.SwerveChancePercent(nervous, data), Is.GreaterThan(TraitEffects.SwerveChancePercent(ordinary, data)));
            Assert.That(TraitEffects.TripChancePercent(nervous, data), Is.GreaterThan(TraitEffects.TripChancePercent(ordinary, data)));
            Assert.That(TraitEffects.PanicPeopleAvoidPercent(kind, data),
                Is.GreaterThan(TraitEffects.PanicPeopleAvoidPercent(ordinary, data)));
            Assert.That(TraitEffects.PanicPeopleAvoidPercent(cruel, data), Is.EqualTo(0), "The cruel barge straight through.");
            Assert.That(TraitEffects.BumpMinimumSpeed(cruel, data), Is.LessThan(TraitEffects.BumpMinimumSpeed(kind, data)));
            Assert.That(TraitEffects.PushMassGrams(strong, data), Is.GreaterThan(TraitEffects.PushMassGrams(ordinary, data)));
            Assert.That(TraitEffects.ShrugsOff(strong, ordinary, data), Is.True, "Four points stronger only staggers.");
            Assert.That(TraitEffects.ShrugsOff(ordinary, strong, data), Is.False);
        }

        [Test]
        public void NervousRunner_ShoutsMoreOftenThanACalmOne()
        {
            FireReactionScenarioData data = DefaultData();
            data.Agents = new[]
            {
                Person(1UL, -2000, -4000, new AgentTraitValues(5, 5, 5, 5, 5, 10)),
                Person(2UL, 2000, -4000, new AgentTraitValues(5, 5, 5, 5, 5, 0))
            };
            data.PhysicsObjects = Array.Empty<FireReactionPhysicsObjectDefinition>();
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Fire.ActivationTick = 10;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -3000, -3000);
            var simulation = new FireReactionSimulation(data);
            int nervousYells = 0;
            int steadyYells = 0;
            for (int tick = 0; tick < 1500; tick++)
            {
                simulation.Step();
            }

            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.EventType != FireReactionEventType.AgentYelled)
                {
                    continue;
                }

                if (record.SourceId.Value == 1UL)
                {
                    nervousYells++;
                }
                else
                {
                    steadyYells++;
                }
            }

            Assert.That(nervousYells, Is.GreaterThan(steadyYells));
        }
    }
}

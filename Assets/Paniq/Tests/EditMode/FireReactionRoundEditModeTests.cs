using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// A round that begins, ends and is scored: nothing happens until the
    /// player sets the disaster going, the round is finished when nobody is
    /// left to resolve, somebody safe in a room the fire cannot reach counts
    /// as saved, and the clear target is measured against the whole crowd.
    /// </summary>
    public sealed class FireReactionRoundEditModeTests
    {
        /// <summary>The building's one way out, in the meeting room.</summary>
        private static readonly SimulationId WayOut = new SimulationId(2008UL);

        /// <summary>The office's east door, into the storage closet.</summary>
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

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

        /// <summary>A level's worth of settings: the hazard waits to be triggered.</summary>
        private FireReactionScenarioData LevelData()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Round.HazardWaitsForTrigger = true;
            return data;
        }

        private static void Step(FireReactionSimulation simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void BeforeTheTrigger_NothingBurnsAndTheRoundNeverEnds()
        {
            using (var simulation = new FireReactionSimulation(LevelData()))
            {
                // A minute of office life, well past the tick the fire would
                // have started itself on.
                Step(simulation, 60 * FireReactionSimulation.TicksPerSecond);

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.FireActive, Is.False, "Nobody pressed anything, so nothing should be alight.");
                Assert.That(snapshot.EventTriggered, Is.False);
                Assert.That(snapshot.RoundPhase, Is.EqualTo(RoundPhase.BeforeEvent));
                Assert.That(snapshot.RoundIsOver, Is.False, "A round that has not started cannot be over.");
                Assert.That(snapshot.LostCount, Is.Zero, "Nobody should come to any harm before the event.");
                Assert.That(snapshot.RemainingCount, Is.EqualTo(snapshot.CrowdSize));
            }
        }

        [Test]
        public void TheTrigger_StartsTheFireWhereTheSeedSaysAndOnlyOnce()
        {
            using (var simulation = new FireReactionSimulation(LevelData()))
            {
                LogicalPosition seededOrigin = simulation.GetSnapshot().FireOrigin;
                Step(simulation, 100);
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 2);
                Step(simulation, 5);

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.FireActive, Is.True, "The trigger should have set the fire going.");
                Assert.That(snapshot.EventTriggered, Is.True);
                Assert.That(snapshot.FireOrigin, Is.EqualTo(seededOrigin),
                    "Triggering by hand must not move where the seed said the fire starts.");

                int starts = 0;
                foreach (CausalEvent record in snapshot.Events)
                {
                    starts += record.EventType == FireReactionEventType.FireActivated ? 1 : 0;
                }

                Assert.That(starts, Is.EqualTo(1), "Pressing twice must not light two fires.");
            }
        }

        [Test]
        public void ARoundEnds_AndEverybodyIsAccountedFor()
        {
            FireReactionScenarioData data = LevelData();
            data.Round.SettleTicks = 50;
            using (var simulation = new FireReactionSimulation(data))
            {
                // Open the one way out, then set the fire going.
                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 10);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 11);
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 20);

                int ticks = 0;
                const int limit = 600 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Phase != RoundPhase.Over && ticks < limit)
                {
                    simulation.Step();
                    ticks++;
                }

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.RoundIsOver, Is.True,
                    $"The round was still going after {ticks / FireReactionSimulation.TicksPerSecond} seconds.");
                Assert.That(snapshot.RemainingCount, Is.Zero, "Nobody may be left unresolved once the round is over.");
                Assert.That(snapshot.SavedCount + snapshot.LostCount, Is.EqualTo(snapshot.CrowdSize),
                    "Saved and lost together have to account for the whole crowd.");
            }
        }

        [Test]
        public void SomebodySafeBehindAShutDoor_CountsAsSaved()
        {
            // One person in the office, a fire they cannot put out, and the
            // closet next door as the only way away from it. The closet door
            // shuts behind them, so the fire can never follow.
            FireReactionScenarioData data = LevelData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-3000, -3000),
                    CardinalDirection.East)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Fire.SpawnBounds = new LogicalBounds(2750, 2750, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 5000;
            data.Fire.SpreadMaximumTicks = 5000;
            data.Perception.MaximumReactionDelayTicks = 0;

            using (var simulation = new FireReactionSimulation(data))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 1);

                int ticks = 0;
                const int limit = 120 * FireReactionSimulation.TicksPerSecond;
                while (simulation.Phase != RoundPhase.Over && ticks < limit)
                {
                    simulation.Step();
                    ticks++;
                }

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.RoundIsOver, Is.True, "Safe next door, there is nothing left to resolve.");
                Assert.That(snapshot.SurvivedCount, Is.EqualTo(1),
                    "Alive in a room the fire cannot reach is a way of living through it.");
                Assert.That(snapshot.SavedCount, Is.EqualTo(1));
                Assert.That(snapshot.EscapedCount, Is.Zero, "They never left the building.");
                Assert.That(snapshot.SavedPercent, Is.EqualTo(100));
            }
        }

        [Test]
        public void WhileADoorStillLetsTheFireThrough_TheRoundKeepsGoing()
        {
            // The same closet, but the door between it and the fire is held
            // open. The person is one who freezes for good, so they stay put
            // and the only thing being judged is whether the fire could get to
            // them, not what they decide to do about it.
            FireReactionScenarioData data = LevelData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(7000, 2500),
                    CardinalDirection.West)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Fire.SpawnBounds = new LogicalBounds(2750, 2750, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 5000;
            data.Fire.SpreadMaximumTicks = 5000;
            data.Round.SettleTicks = 25;

            using (var simulation = new FireReactionSimulation(data))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 1);

                // One click opens the closet door: it starts shut but unlocked.
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 2);
                Step(simulation, 20 * FireReactionSimulation.TicksPerSecond);

                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.RemainingCount, Is.EqualTo(1),
                    "The frozen person should still be alive and still inside.");
                Assert.That(snapshot.RoundIsOver, Is.False,
                    "With the door open the fire can still reach them, so nothing is settled.");
            }
        }

        [Test]
        public void TheClearTarget_IsCountedAgainstTheWholeCrowd()
        {
            FireReactionScenarioData data = LevelData();
            data.Round.TargetSavedPercent = 75;
            using (var simulation = new FireReactionSimulation(data))
            {
                FireReactionSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.CrowdSize, Is.EqualTo(20), "The office holds twenty people.");
                Assert.That(snapshot.TargetSavedCount, Is.EqualTo(15), "Three quarters of twenty is fifteen.");
                Assert.That(snapshot.TargetSavedPercent, Is.EqualTo(75));
                Assert.That(snapshot.Cleared, Is.False, "Nobody has been saved yet.");
            }
        }

        [TestCase(75, 20, 15)]
        [TestCase(75, 13, 10)]
        [TestCase(100, 20, 20)]
        [TestCase(50, 9, 5)]
        public void TheTargetInPeople_RoundsUpSoTheBarIsNeverSofterThanItReads(
            int percent, int crowd, int expected)
        {
            // Worked out the same way the snapshot does, so a level with an
            // awkward crowd size cannot quietly let a player clear it short.
            Assert.That((crowd * percent + 99) / 100, Is.EqualTo(expected));
        }
    }
}

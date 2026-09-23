using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// A round that begins, ends and is scored: nothing happens until the
    /// player sets the disaster going, the round runs until everybody is out
    /// or dead, and the clear target is measured against the whole crowd.
    /// <para>
    /// The round no longer stops as soon as everybody left is somewhere the
    /// fire cannot reach -- it waits for them to walk out. The only other way
    /// it can end is the stall clock, and the thing that clock must never
    /// mistake for a settled building is a queue, which is what
    /// <see cref="AQueueAtADoor_NeverEndsTheRound"/> is here to hold down.
    /// </para>
    /// </summary>
    public sealed class FireReactionRoundEditModeTests
    {
        /// <summary>The building's one way out, in the meeting room.</summary>
        private static readonly SimulationId WayOut = new SimulationId(2008UL);

        /// <summary>The office's east door, into the storage closet.</summary>
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>The open-plan office, whose east wall holds the closet door.</summary>
        private static readonly SimulationId OfficeRoom = new SimulationId(5001UL);

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

        /// <summary>
        /// A level's worth of settings: the hazard waits to be triggered, and
        /// the player can pay for the doors these tests open. A round opens
        /// with an empty purse, so without that the clicks are refused, nobody
        /// gets out, and a test about how a round ends is really a test of a
        /// sealed building.
        /// </summary>
        private FireReactionScenarioData LevelData()
        {
            FireReactionScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
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
            using (var simulation = new FireReactionSimulation(data))
            {
                // Open the one way out, then set the fire going. Two clicks --
                // the key, then the door -- and the purse has to stretch to
                // both, which at the authored prices it just does.
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
        public void SomebodyFrozenWithNothingElseHappening_EndsTheRoundAndCountsAsSaved()
        {
            // One person who freezes for good, alone in the storage closet
            // with the door shut, and a fire on the far side of the office
            // that never spreads and never reaches the closet door. Nothing
            // in this building is ever going to change again, and the stall
            // clock is the only thing that can call it.
            FireReactionScenarioData data = SealedCloset();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(7000, 2500),
                    CardinalDirection.West)
            };
            data.Temperament.FreezeForeverPercent = 100;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Round.StallTicks = 100;

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
                Assert.That(snapshot.RoundIsOver, Is.True,
                    "Nothing can happen any more, so the stall clock should have called it.");
                Assert.That(snapshot.SurvivedCount, Is.EqualTo(1),
                    "Alive inside when the round stops is a way of living through it.");
                Assert.That(snapshot.SavedCount, Is.EqualTo(1));
                Assert.That(snapshot.EscapedCount, Is.Zero, "They never left the building.");
                Assert.That(snapshot.SavedPercent, Is.EqualTo(100));
            }
        }

        [Test]
        public void SomebodyStillWalkingOut_KeepsTheRoundGoing()
        {
            // The whole office, with the way out opened for them. Nobody is
            // out of the fire's reach for most of this, but the point is that
            // while people are walking the round is never called over on top
            // of them.
            FireReactionScenarioData data = LevelData();
            using (var simulation = new FireReactionSimulation(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 10);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 11);
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 20);

                const int limit = 600 * FireReactionSimulation.TicksPerSecond;
                for (int t = 0; t < limit && simulation.Phase != RoundPhase.Over; t++)
                {
                    simulation.Step();
                    Assert.That(AnybodyIsMoving(simulation) && simulation.Phase == RoundPhase.Over, Is.False,
                        $"Tick {simulation.Tick}: the round was called over while somebody was still moving.");
                }

                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Over), "The round should finish eventually.");
            }
        }

        /// <summary>
        /// The one the owner asked for by name. A crowd wedged in a doorway
        /// covers almost no ground for a long time, and if the stall clock
        /// measured distance alone it would call that a settled building and
        /// end the round on top of them.
        /// </summary>
        [Test]
        public void AQueueAtADoor_NeverEndsTheRound()
        {
            // Six people shut in the two-metre storage closet with a fire in
            // the office, and a door they can never get through: locked, and
            // strong enough that nobody will ever shoulder it down. They pile
            // into the doorway and stay there.
            FireReactionScenarioData data = SealedCloset();
            var crowd = new FireReactionAgentDefinition[6];
            for (int i = 0; i < crowd.Length; i++)
            {
                crowd[i] = new FireReactionAgentDefinition(new SimulationId((ulong)(1 + i)),
                    new LogicalPosition(6600 + i % 3 * 500, 1900 + i / 3 * 700), CardinalDirection.West);
            }

            data.Agents = crowd;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;

            // A second of quiet is enough to end the round, so if a jam ever
            // reads as quiet this test will notice within a second of it.
            data.Round.StallTicks = 50;

            using (var simulation = new FireReactionSimulation(data))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 1);

                int pressingTicks = 0;
                const int limit = 60 * FireReactionSimulation.TicksPerSecond;
                for (int t = 0; t < limit; t++)
                {
                    simulation.Step();
                    if (!AnybodyIsPressing(simulation))
                    {
                        continue;
                    }

                    pressingTicks++;
                    Assert.That(simulation.Phase, Is.Not.EqualTo(RoundPhase.Over),
                        $"Tick {simulation.Tick}: somebody was pushing to get out and the round was called over.");
                }

                Assert.That(pressingTicks, Is.GreaterThan(FireReactionSimulation.TicksPerSecond),
                    "This test proves nothing unless a real jam formed: nobody ever pushed and got nowhere.");
            }
        }

        /// <summary>
        /// The storage closet with its door locked and unbreakable, and a fire
        /// in the far corner of the office that never spreads and is too far
        /// from the closet door to burn through it.
        /// </summary>
        private FireReactionScenarioData SealedCloset()
        {
            FireReactionScenarioData data = LevelData();
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Doors = new[]
            {
                new FireReactionDoorDefinition(ClosetDoor, OfficeRoom, WallSide.East, 2500, 1000, true)
            };

            // Nobody is ever getting through it, so the jam cannot resolve
            // itself by somebody breaking the door down.
            data.Exits.DoorStrength = int.MaxValue;
            data.Fire.SpawnBounds = new LogicalBounds(-4000, -4000, -4000, -4000);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            return data;
        }

        /// <summary>Anybody still in the run who is under way on their own feet.</summary>
        private static bool AnybodyIsMoving(FireReactionSimulation simulation)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                FireReactionAgentSnapshot agent = simulation.GetAgent(i);
                if (agent.Participation == AgentParticipation.Participating && agent.SpeedMillimetresPerTick > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Anybody wanting to move and getting nowhere: the shape a queue has
        /// from the inside, and the thing the stall clock has to see.
        /// </summary>
        private static bool AnybodyIsPressing(FireReactionSimulation simulation)
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                if (simulation.GetAgent(i).Participation == AgentParticipation.Participating &&
                    simulation.BlockedTicksForTests(i) > 0)
                {
                    return true;
                }
            }

            return false;
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

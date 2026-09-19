using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The building as rooms joined by doors: the storage closet behind the
    /// office's east door is an ordinary room, fire only crosses an open
    /// door, and people run to whatever room is furthest from the flames
    /// when no way out is left.
    /// </summary>
    public sealed class FireReactionRoomsEditModeTests
    {
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

        /// <summary>The closet door starts shut but unlocked, so one click opens it.</summary>
        private static void OpenClosetDoor(FireReactionSimulation simulation, int tick)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, tick);
        }

        private static bool InCloset(FireReactionScenarioData data, LogicalPosition point)
        {
            LogicalBounds b = data.Rooms[1].Bounds;
            return point.X > b.MinX && point.X < b.MaxX && point.Z > b.MinZ && point.Z < b.MaxZ;
        }

        /// <summary>Fire that starts right against the east door and spreads fast; one person far away.</summary>
        private FireReactionScenarioData FireAtTheEastDoor()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-5000, -5000), CardinalDirection.North)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(5750, 5750, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 5;
            data.Fire.SpreadMaximumTicks = 10;
            return data;
        }

        private static int BurningInCloset(FireReactionSimulation simulation, FireReactionScenarioData data)
        {
            int count = 0;
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (InCloset(data, cell.Centre))
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void DefaultScenario_HasAStorageClosetBehindTheEastDoor()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.Rooms, Has.Length.EqualTo(2));
            LogicalBounds b = data.Rooms[1].Bounds;
            Assert.That((b.MaxX - b.MinX) * (long)(b.MaxZ - b.MinZ), Is.EqualTo(4000000L), "2 × 2 m: room for two or three.");

            // Its door is an inside door: shut, but not locked.
            FireReactionDoorDefinition door = Array.Find(data.Doors, d => d.DoorId == ClosetDoor);
            Assert.That(door.RoomId, Is.EqualTo(data.Rooms[0].RoomId), "The door sits in the office's east wall.");
            Assert.That(door.StartsLocked, Is.False);
            Assert.That(Array.FindAll(data.Doors, d => d.StartsLocked), Has.Length.EqualTo(3), "The three ways out start locked.");
        }

        [Test]
        public void Fire_NeverGetsThroughAClosedDoor()
        {
            FireReactionScenarioData data = FireAtTheEastDoor();
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.EqualTo(24 * 24), "The main room should be full of fire.");
            Assert.That(BurningInCloset(simulation, data), Is.EqualTo(0), "Fire got through the shut door.");
        }

        [Test]
        public void Fire_GetsInThroughAnOpenDoorOnlyAcrossTheGap()
        {
            FireReactionScenarioData data = FireAtTheEastDoor();
            var simulation = new FireReactionSimulation(data);
            OpenClosetDoor(simulation, 2);
            for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(BurningInCloset(simulation, data), Is.EqualTo(16), "The whole closet should burn.");

            // The first side-room square was lit from the main-room square right next to it, through the door gap.
            FireCellSnapshot first = default;
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (InCloset(data, cell.Centre))
                {
                    first = cell;
                    break;
                }
            }

            CausalEvent lit = simulation.EventLog.Get(first.EventId);
            CausalEvent from = simulation.EventLog.Get(lit.CausalParentEventId);
            Assert.That(from.Position.X, Is.EqualTo(5750), "Lit from the main room's edge square.");
            Assert.That(first.Centre.X, Is.EqualTo(6250));
            Assert.That(first.Centre.Z, Is.InRange(2000, 3000), "Only across the 1 m door gap.");
        }

        [Test]
        public void Runner_WithNoWayOut_RunsToTheRoomFurthestFromTheFire()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(4500, 2500), CardinalDirection.West,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(2750, 2750, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 5000;
            data.Fire.SpreadMaximumTicks = 5000;
            data.Perception.MaximumReactionDelayTicks = 0;

            // Every way out of the building stays locked, so the closet next
            // door is the only place left to get away from the flames.
            var simulation = new FireReactionSimulation(data);
            int insideFor = 0;
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                insideFor += InCloset(data, simulation.GetAgent(0).Position) ? 1 : 0;
            }

            FireReactionAgentSnapshot person = simulation.GetAgent(0);
            Assert.That(insideFor, Is.GreaterThan(0), "The runner never got into the closet.");
            Assert.That(InCloset(data, person.Position), Is.True, $"They ended up at {person.Position}.");
            Assert.That(person.Outcome, Is.EqualTo(AgentTerminalOutcome.Unresolved), "A closet is not a way out.");
            Assert.That(simulation.GetDoor(1).State, Is.Not.EqualTo(DoorState.Locked), "They opened the closet door themselves.");
        }

        [Test]
        public void ClosedDoor_HidesTheFireAndMufflesYells()
        {
            // Someone inside the closet, facing the wall toward the office, 1 m from a fire just beyond it.
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(6500, 2000), CardinalDirection.West),
                new FireReactionAgentDefinition(new SimulationId(2UL), new LogicalPosition(4500, 2000), CardinalDirection.East)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(5250, 5250, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 5000;
            data.Fire.SpreadMaximumTicks = 5000;
            data.Hearing.FireHearingRadiusMillimetres = 0;

            // Person 2 sees the fire and yells from 2 m away: within 2.5 m, but not within the muffled 1.25 m.
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 30; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(1).FearState, Is.Not.EqualTo(AgentFearState.Calm), "Person 2 should have seen the fire.");
            FireReactionAgentSnapshot inside = simulation.GetAgent(0);
            Assert.That(inside.FearState, Is.EqualTo(AgentFearState.Calm),
                "Behind a closed door they neither see the fire nor understand the yell.");
        }

        /// <summary>
        /// The doorway-jam guard. Two runners reaching a 1 m door together
        /// used to wedge against either side of the frame and block everyone
        /// behind them until the fire came (seed 5, owner report). People now
        /// give way beside a door they are not lined up with, so nobody
        /// stands still in front of an open door for long.
        /// </summary>
        [TestCase(300)]
        [TestCase(600)]
        public void NobodyStandsStillInFrontOfAnOpenDoor(int unlockTick)
        {
            const int stuckLimitTicks = 2 * FireReactionSimulation.TicksPerSecond;
            for (ulong seed = 1UL; seed <= 10UL; seed++)
            {
                FireReactionScenarioData data = scenario.ToRuntimeData();
                var simulation = new FireReactionSimulation(data, seed);
                foreach (FireReactionDoorDefinition door in data.Doors)
                {
                    simulation.QueueCommand(PlayerCommandType.ClickDoor, door.DoorId, unlockTick);
                }

                var stillFor = new int[simulation.AgentCount];
                var wasAt = new LogicalPosition[simulation.AgentCount];
                for (int t = 0; t < 40 * FireReactionSimulation.TicksPerSecond; t++)
                {
                    simulation.Step();
                    for (int i = 0; i < simulation.AgentCount; i++)
                    {
                        FireReactionAgentSnapshot person = simulation.GetAgent(i);
                        bool stuck = person.Participation == AgentParticipation.Participating &&
                                     !person.IsBurning && !person.IsDown &&
                                     person.Position.Equals(wasAt[i]) && NearAnOpenDoor(simulation, person.Position);
                        wasAt[i] = person.Position;
                        stillFor[i] = stuck ? stillFor[i] + 1 : 0;
                        Assert.That(stillFor[i], Is.LessThan(stuckLimitTicks),
                            $"Seed {seed}: person {person.AgentId} stood in a doorway at {person.Position} " +
                            $"for {stillFor[i]} ticks up to tick {simulation.Tick}.");
                    }
                }
            }
        }

        /// <summary>Within a metre of the middle of a door that is standing open.</summary>
        private static bool NearAnOpenDoor(FireReactionSimulation simulation, LogicalPosition position)
        {
            for (int d = 0; d < simulation.DoorCount; d++)
            {
                FireReactionDoorSnapshot door = simulation.GetDoor(d);
                if ((door.State == DoorState.Open || door.State == DoorState.Broken) &&
                    LogicalPosition.DistanceSquared(position, door.Centre) < 1000L * 1000L)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

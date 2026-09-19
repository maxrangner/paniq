using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>The small room behind the east door: shelter, and fire that only gets in through an open door.</summary>
    public sealed class FireReactionSideRoomEditModeTests
    {
        private static readonly SimulationId EastDoor = new SimulationId(2002UL);

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

        private static void OpenEastDoor(FireReactionSimulation simulation, int tick)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, EastDoor, tick);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, EastDoor, tick + 1);
        }

        private static bool InSideRoom(FireReactionScenarioData data, LogicalPosition point)
        {
            LogicalBounds b = data.SideRooms[0].Bounds;
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

        private static int BurningInSideRoom(FireReactionSimulation simulation, FireReactionScenarioData data)
        {
            int count = 0;
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (InSideRoom(data, cell.Centre))
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void DefaultScenario_HasASmallRoomBehindTheEastDoor()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.SideRooms, Has.Length.EqualTo(1));
            Assert.That(data.SideRooms[0].DoorId, Is.EqualTo(EastDoor));
            LogicalBounds b = data.SideRooms[0].Bounds;
            Assert.That((b.MaxX - b.MinX) * (long)(b.MaxZ - b.MinZ), Is.EqualTo(4000000L), "2 × 2 m: room for two or three.");
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
            Assert.That(BurningInSideRoom(simulation, data), Is.EqualTo(0), "Fire got through the locked door.");
        }

        [Test]
        public void Fire_GetsInThroughAnOpenDoorOnlyAcrossTheGap()
        {
            FireReactionScenarioData data = FireAtTheEastDoor();
            var simulation = new FireReactionSimulation(data);
            OpenEastDoor(simulation, 2);
            for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(BurningInSideRoom(simulation, data), Is.EqualTo(16), "The whole side room should burn.");

            // The first side-room square was lit from the main-room square right next to it, through the door gap.
            FireCellSnapshot first = default;
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (InSideRoom(data, cell.Centre))
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
        public void Runner_SheltersInTheSideRoomAndIsNotCountedAsEscaped()
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

            // Only the east door is open, so it is the obvious way out.
            var simulation = new FireReactionSimulation(data);
            OpenEastDoor(simulation, 1);
            int shelteredFor = 0;
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                if (simulation.GetAgent(0).ActivityState == AgentActivityState.Sheltering)
                {
                    shelteredFor++;
                }
            }

            FireReactionAgentSnapshot person = simulation.GetAgent(0);
            Assert.That(shelteredFor, Is.GreaterThan(0), "The runner never sheltered.");
            Assert.That(person.ActivityState, Is.EqualTo(AgentActivityState.Sheltering));
            Assert.That(person.Outcome, Is.EqualTo(AgentTerminalOutcome.Unresolved), "Sheltering is not escaping.");
            Assert.That(data.SideRooms[0].Bounds.ContainsCircle(person.Position, data.World.OccupancyRadiusMillimetres), Is.True);
            Assert.That(simulation.GetSnapshot().ShelteringCount, Is.EqualTo(1));
            Assert.That(person.Position.X, Is.GreaterThan(7000), "They move to the back of the room.");
        }

        [Test]
        public void ClosedDoor_HidesTheFireAndMufflesYells()
        {
            // Someone inside the side room, facing the wall toward the main room, 1 m from a fire just beyond it.
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
    }
}

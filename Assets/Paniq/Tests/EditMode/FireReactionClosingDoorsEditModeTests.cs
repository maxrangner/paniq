using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>Closing and locking doors: by the player, and by people according to their personality.</summary>
    public sealed class FireReactionClosingDoorsEditModeTests
    {
        private static readonly SimulationId NorthDoor = new SimulationId(2001UL);
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

        private static List<CausalEvent> EventsOfType(FireReactionSimulation simulation, FireReactionEventType type)
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

        private static DoorState StateOf(FireReactionSimulation simulation, SimulationId door)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == door)
                {
                    return simulation.GetDoor(i).State;
                }
            }

            throw new KeyNotFoundException(door.ToString());
        }

        private static void Click(FireReactionSimulation simulation, SimulationId door, int tick)
        {
            simulation.QueueCommand(PlayerCommandType.ClickDoor, door, tick);
        }

        [Test]
        public void Player_CannotCloseADoorSomeoneIsStandingIn()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();

            // Standing right in the north doorway, and staying put (no fire, very slow calm decisions).
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, 5750), CardinalDirection.South)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 5000;
            data.Calm.DecisionMaximumTicks = 5000;
            var simulation = new FireReactionSimulation(data);
            Click(simulation, NorthDoor, 1);
            Click(simulation, NorthDoor, 2);
            Click(simulation, NorthDoor, 3);
            for (int t = 0; t < 5; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Position, Is.EqualTo(new LogicalPosition(-2500, 5750)));
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open), "Nobody can close a door on someone in it.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed), Is.Empty);
        }

        /// <summary>
        /// One person inside the side room, right by its open door, who sees a
        /// fire 2.5 m beyond it and shelters; another person standing calmly
        /// 1.5 m outside the door (out of earshot of the alarm), as if about
        /// to come in.
        /// </summary>
        private FireReactionSimulation ShelterWithSomeoneOutside(AgentTraitValues shelterer, int outsiderX = 4500)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(6400, 2500), CardinalDirection.West, shelterer),
                new FireReactionAgentDefinition(new SimulationId(2UL), new LogicalPosition(outsiderX, 2500), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(3250, 3250, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            data.Hearing.FireHearingRadiusMillimetres = 0;

            // The shelterer's yell must not send the person outside running in too.
            data.Hearing.YellAlarmRadiusMillimetres = 1000;
            data.Hearing.YellHearingRadiusMillimetres = 1000;
            data.Calm.DecisionMinimumTicks = 5000;
            data.Calm.DecisionMaximumTicks = 5000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new FireReactionSimulation(data);
            Click(simulation, EastDoor, 1);
            Click(simulation, EastDoor, 2);
            return simulation;
        }

        [Test]
        public void EvilShelterer_SlamsAndLocksTheDoorInTheFaceOfSomeoneComing()
        {
            FireReactionSimulation simulation = ShelterWithSomeoneOutside(new AgentTraitValues(5, 5, 5, 1, 9, 5));
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond && EventsOfType(simulation, FireReactionEventType.DoorLocked).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(StateOf(simulation, EastDoor), Is.EqualTo(DoorState.Locked));
            List<CausalEvent> closed = EventsOfType(simulation, FireReactionEventType.DoorClosed);
            List<CausalEvent> locked = EventsOfType(simulation, FireReactionEventType.DoorLocked);
            Assert.That(closed[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
            Assert.That(closed[0].TargetId, Is.EqualTo(EastDoor));
            Assert.That(locked[0].CausalParentEventId, Is.EqualTo(closed[0].EventId));
            Assert.That(LogicalPosition.DistanceSquared(simulation.GetAgent(1).Position, new LogicalPosition(6000, 2500)),
                Is.LessThan(3000L * 3000L), "Someone was right outside when they shut it.");
        }

        [Test]
        public void CompassionateShelterer_KeepsTheDoorOpenWhileSomeoneIsNear()
        {
            FireReactionSimulation simulation = ShelterWithSomeoneOutside(new AgentTraitValues(5, 5, 5, 9, 0, 9));
            bool sheltered = false;
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                sheltered |= simulation.GetAgent(0).ActivityState == AgentActivityState.Sheltering;
            }

            Assert.That(sheltered, Is.True, "The first person should have sheltered.");
            Assert.That(StateOf(simulation, EastDoor), Is.EqualTo(DoorState.Open), "Kind people hold the door for others.");
        }

        [Test]
        public void NervousShelterer_ShutsTheDoorWhenNobodyIsNear()
        {
            // The other person stands far off across the room this time.
            FireReactionSimulation simulation = ShelterWithSomeoneOutside(new AgentTraitValues(5, 5, 5, 3, 0, 9), -4000);
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond && EventsOfType(simulation, FireReactionEventType.DoorClosed).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(StateOf(simulation, EastDoor), Is.EqualTo(DoorState.Unlocked), "Shut, but not locked.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed)[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
        }

        [Test]
        public void OrdinaryShelterer_LeavesTheDoorOpenWhileTheFireIsFarAndNobodyIsNear()
        {
            FireReactionSimulation simulation = ShelterWithSomeoneOutside(AgentTraitValues.AllOrdinary, -4000);
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(StateOf(simulation, EastDoor), Is.EqualTo(DoorState.Open));
        }

        [TestCase(9, true)]
        [TestCase(0, false)]
        public void Escaper_LocksTheDoorBehindThemOnlyIfEvil(int evil, bool expectLocked)
        {
            FireReactionScenarioData data = FireReactionDoorsEditModeTests.RunnerByTheNorthDoor(
                scenario.ToRuntimeData(), 0, new AgentTraitValues(5, 5, 5, 5, evil, 5));
            var simulation = new FireReactionSimulation(data);
            Click(simulation, NorthDoor, 1);
            Click(simulation, NorthDoor, 2);
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(expectLocked ? DoorState.Locked : DoorState.Open));
            if (expectLocked)
            {
                CausalEvent closed = EventsOfType(simulation, FireReactionEventType.DoorClosed)[0];
                Assert.That(simulation.EventLog.Get(closed.CausalParentEventId).EventType, Is.EqualTo(FireReactionEventType.AgentEscaped));
            }
        }
    }
}

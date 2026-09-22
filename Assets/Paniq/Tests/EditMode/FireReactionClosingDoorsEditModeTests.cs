using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Closing and locking doors: by the player, by the cruel as they leave a
    /// room or the building, and by anyone shutting flames out of the room they
    /// are standing in.
    /// </summary>
    public sealed class FireReactionClosingDoorsEditModeTests
    {
        private static readonly SimulationId NorthDoor = new SimulationId(2001UL);
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>Someone near the north door, close enough to be shut out, but off to one side of the gap.</summary>
        private static readonly LogicalPosition Nearby = new LogicalPosition(-400, 4600);

        /// <summary>Someone right across the room, far too far away to be shut out.</summary>
        private static readonly LogicalPosition FarOff = new LogicalPosition(-2500, -4000);

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
            FireReactionScenarioData data =
                FireReactionDoorsEditModeTests.WithAWayOutOfTheOffice(scenario.ToRuntimeData());

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

            Assert.That(LogicalPosition.DistanceSquared(simulation.GetAgent(0).Position, new LogicalPosition(-2500, 5750)),
                Is.LessThanOrEqualTo(50L * 50L), "They are still standing in the doorway.");
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open), "Nobody can close a door on someone in it.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed), Is.Empty);
        }

        /// <summary>
        /// One person inside the side room, right by its open door, who sees a
        /// fire 2.5 m beyond it and shelters; another person standing calmly
        /// 1.5 m outside the door (out of earshot of the alarm), as if about
        /// to come in.
        /// </summary>
        /// <summary>
        /// One person in the storage closet with its door already open and
        /// fire just outside it, and another person out in the office walking
        /// toward the closet.
        /// </summary>
        private FireReactionSimulation InTheClosetWithSomeoneOutside(AgentTraitValues insider, int outsiderX = 4500)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(7000, 2500), CardinalDirection.West, insider),
                new FireReactionAgentDefinition(new SimulationId(2UL), new LogicalPosition(outsiderX, 2500), CardinalDirection.East,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(5250, 5250, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 10000;
            data.Fire.SpreadMaximumTicks = 10000;
            data.Hearing.FireHearingRadiusMillimetres = 0;

            // The person inside must not send the one outside running in too.
            data.Hearing.YellAlarmRadiusMillimetres = 1000;
            data.Hearing.YellHearingRadiusMillimetres = 1000;
            data.Calm.DecisionMinimumTicks = 5000;
            data.Calm.DecisionMaximumTicks = 5000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            var simulation = new FireReactionSimulation(data);

            // The closet door starts shut but unlocked, so one click opens it.
            Click(simulation, ClosetDoor, 1);
            return simulation;
        }

        [Test]
        public void AnyoneInTheCloset_ShutsTheDoorWhenTheFireIsRightOutsideIt()
        {
            // Kind and unafraid: they would hold it open if the flames were not at the door.
            FireReactionSimulation simulation = InTheClosetWithSomeoneOutside(new AgentTraitValues(5, 5, 9, 9, 0, 1));
            for (int t = 0; t < 5 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.DoorClosed).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Unlocked), "Shut against the flames, but not locked.");
            List<CausalEvent> closed = EventsOfType(simulation, FireReactionEventType.DoorClosed);
            Assert.That(closed, Is.Not.Empty, "Nobody shut the door on the fire.");
            Assert.That(closed[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
        }

        /// <summary>
        /// The moments people decide about a door: on their way out of the
        /// building (tested below), and standing in a room with fire coming
        /// through a door (tested above). Personality decides which way.
        /// </summary>
        private FireReactionSimulation EscapingWithSomeoneBehind(AgentTraitValues escaper, LogicalPosition follower)
        {
            FireReactionScenarioData data = FireReactionDoorsEditModeTests.RunnerByTheNorthDoor(
                scenario.ToRuntimeData(), 0, escaper);
            FireReactionAgentDefinition runner = data.Agents[0];
            data.Agents = new[]
            {
                runner,
                new FireReactionAgentDefinition(new SimulationId(2UL), follower, CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            // The other person neither sees the fire nor hears the runner,
            // so they are still standing there when the runner reaches the door.
            data.Hearing.FireHearingRadiusMillimetres = 0;
            data.Hearing.YellAlarmRadiusMillimetres = 1;
            data.Hearing.YellHearingRadiusMillimetres = 1;
            data.Calm.DecisionMinimumTicks = 5000;
            data.Calm.DecisionMaximumTicks = 5000;
            var simulation = new FireReactionSimulation(data);
            Click(simulation, NorthDoor, 1);
            Click(simulation, NorthDoor, 2);
            return simulation;
        }

        private static void RunUntilEscaped(FireReactionSimulation simulation)
        {
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped), "The runner never got out.");
        }

        [Test]
        public void CompassionateEscaper_HoldsTheDoorOpenForSomeoneComingBehind()
        {
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(new AgentTraitValues(5, 5, 5, 9, 0, 9), Nearby);
            RunUntilEscaped(simulation);
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed), Is.Empty,
                "Kind people never shut a door with someone coming.");
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open));
        }

        [Test]
        public void NervousEscaper_LeavesTheDoorOpenBehindThem()
        {
            // Nobody else near, so nothing but cruelty could make them shut it.
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(new AgentTraitValues(5, 5, 5, 3, 0, 9), FarOff);
            RunUntilEscaped(simulation);
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open),
                "Being frightened is not a reason to shut people in; only the cruel do that.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed), Is.Empty);
        }

        [Test]
        public void BraveAndKindEscaper_LeavesTheDoorOpenBehindThem()
        {
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(new AgentTraitValues(5, 5, 9, 9, 0, 3), FarOff);
            RunUntilEscaped(simulation);
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open));
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorClosed), Is.Empty);
        }

        /// <summary>
        /// The cruel shut the door behind them; only the very worst also turn
        /// the key, so somebody merely nasty leaves it shut but openable.
        /// </summary>
        [TestCase(6, false, false)]
        [TestCase(7, true, false)]
        [TestCase(9, true, true)]
        public void OnlyTheCruelShutTheDoorBehindThem_AndOnlyTheWorstLockIt(int evil, bool expectShut, bool expectLocked)
        {
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(new AgentTraitValues(5, 5, 5, 1, evil, 5), Nearby);
            RunUntilEscaped(simulation);
            DoorState expected = expectLocked ? DoorState.Locked : expectShut ? DoorState.Unlocked : DoorState.Open;
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(expected),
                $"Evil {evil} should leave the north door {expected}.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorLocked).Count,
                Is.EqualTo(expectLocked ? 1 : 0), "Only the cruellest turn the key.");
        }

        [Test]
        public void OrdinaryEscaper_LeavesTheDoorOpenWhileTheFireIsFarAndNobodyIsNear()
        {
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(AgentTraitValues.AllOrdinary, FarOff);
            RunUntilEscaped(simulation);
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open));
        }

        /// <summary>
        /// The owner watched somebody shut a door behind them, turn round and
        /// hammer on it. Shutting a door now goes into that person's own memory
        /// of doors, so it stops being a way out to them.
        /// </summary>
        [Test]
        public void NobodyShouldersADoorTheyShutThemselves()
        {
            for (ulong seed = 40UL; seed <= 46UL; seed++)
            {
                var simulation = new FireReactionSimulation(scenario.ToRuntimeData(), seed);
                for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                var shutItThemselves = new HashSet<(ulong Person, ulong Door)>();
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    (ulong, ulong) who = (record.SourceId.Value, record.TargetId.Value);
                    if (record.EventType == FireReactionEventType.DoorClosed ||
                        record.EventType == FireReactionEventType.DoorLocked)
                    {
                        shutItThemselves.Add(who);
                    }
                    else if (record.EventType == FireReactionEventType.DoorOpened ||
                             record.EventType == FireReactionEventType.DoorBrokenDown)
                    {
                        // Somebody opened it again, so nobody's own shutting of
                        // it stands any more. If a villain then locks it, the
                        // person who shut it earlier is as locked in as anybody
                        // and may hammer on it like anybody.
                        // A door names itself as the source when it opens and as
                        // the target when somebody breaks it down.
                        shutItThemselves.RemoveWhere(pair => pair.Door == record.TargetId.Value ||
                                                             pair.Door == record.SourceId.Value);
                    }
                    else if (record.EventType == FireReactionEventType.AgentForcedDoor)
                    {
                        Assert.That(shutItThemselves.Contains(who), Is.False,
                            $"Seed {seed}: person {record.SourceId} shouldered door {record.TargetId} at tick " +
                            $"{record.Tick}, having shut it themselves.");
                    }
                }
            }
        }

        [Test]
        public void EvilEscaper_SlamsAndLocksTheDoorInTheFaceOfSomeoneComing()
        {
            // Evil 9 is past both the shutting bar (7) and the locking bar (9).
            FireReactionSimulation simulation = EscapingWithSomeoneBehind(new AgentTraitValues(5, 5, 5, 1, 9, 5), Nearby);
            RunUntilEscaped(simulation);
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Locked));
            List<CausalEvent> closed = EventsOfType(simulation, FireReactionEventType.DoorClosed);
            List<CausalEvent> locked = EventsOfType(simulation, FireReactionEventType.DoorLocked);
            Assert.That(closed[0].SourceId, Is.EqualTo(new SimulationId(1UL)));
            Assert.That(closed[0].TargetId, Is.EqualTo(NorthDoor));
            Assert.That(locked[0].CausalParentEventId, Is.EqualTo(closed[0].EventId));
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

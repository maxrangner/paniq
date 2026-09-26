using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Doors the player holds shut (prototype 3, 2026-09-25): the button
    /// kept down on a door is a hand on it. Nobody opens it while it is
    /// held, locked or not; somebody strong enough bursts it in one push
    /// (the owner's rule); everybody else rattles it, gives up and goes
    /// round. An open door is pulled shut first, once the doorway is clear.
    /// Free, and not a card.
    /// </summary>
    public sealed class HeldDoorsEditModeTests
    {
        /// <summary>The way out of the office the door tests add to its south wall.</summary>
        private static readonly SimulationId OfficeWayOut = new SimulationId(2001UL);

        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);
        private static readonly SimulationId Runner = new SimulationId(1UL);

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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>
        /// One runner who panics beside a way out of the office that is shut
        /// but not locked: the door they would simply push open, if nobody
        /// were holding it.
        /// </summary>
        private ScenarioData RunnerAtAnUnlockedWayOut(AgentTraitValues traits)
        {
            ScenarioData data = DoorsEditModeTests.RunnerByTheWayOut(
                TheBuilding.WithTheFireInTheOffice(TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData())), 100, traits);
            data.Doors = Array.ConvertAll(data.Doors, d => d.DoorId == OfficeWayOut
                ? new DoorDefinition(d.DoorId, d.RoomId, d.Side, d.CentreAlongWallMillimetres, d.WidthMillimetres, startsLocked: false)
                : d);
            return data;
        }

        /// <summary>Somebody too weak to batter any door.</summary>
        private static AgentTraitValues Weak => new AgentTraitValues(1, 5, 5, 5, 2, 5, 4);

        /// <summary>Somebody strong enough to batter a locked door down, given time.</summary>
        private static AgentTraitValues Strong => new AgentTraitValues(9, 5, 9, 5, 2, 3, 4);

        [Test]
        public void AHeldDoor_IsNotOpenedBySomebodyWhoCouldSimplyPushIt_UntilItIsLetGoOf()
        {
            ScenarioData data = RunnerAtAnUnlockedWayOut(Weak);

            // They keep rattling it rather than giving up, so that letting go
            // is the one thing that changes.
            data.Exits.DoorTryTicks = 100000;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.HoldDoor, OfficeWayOut, 1);
                simulation.QueueCommand(PlayerCommandType.ReleaseDoor, OfficeWayOut, 150);
                Advance(simulation, 149);

                Assert.That(Door(simulation, OfficeWayOut).IsHeld, Is.True);
                Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Unlocked), "Shut, and not locked: only held.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTriedDoor), Is.Not.Empty, "They should have reached it and tried it.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened), Is.Empty, "Held: it does not open.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Empty, "Too weak to burst it.");
                Assert.That(simulation.GetAgent(Runner).Outcome, Is.Not.EqualTo(AgentTerminalOutcome.Escaped));

                Advance(simulation, 60);
                Assert.That(Door(simulation, OfficeWayOut).IsHeld, Is.False);
                List<CausalEvent> opened = EventsOfType(simulation, CausalEventType.DoorOpened);
                Assert.That(opened, Has.Count.EqualTo(1), "Let go of, it opens under the hand already on it.");
                Assert.That(opened[0].Tick, Is.GreaterThanOrEqualTo(150));
                Assert.That(opened[0].SourceId, Is.EqualTo(OfficeWayOut));
            }
        }

        /// <summary>The owner's rule: somebody strong enough gets through a held door in a single push, and it is off its hinges for good.</summary>
        [Test]
        public void SomebodyStrong_BurstsAHeldDoorInOnePush()
        {
            ScenarioData data = RunnerAtAnUnlockedWayOut(Strong);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.HoldDoor, OfficeWayOut, 1);
                Advance(simulation, 5 * Run.TicksPerSecond);

                List<CausalEvent> pushed = EventsOfType(simulation, CausalEventType.AgentForcedDoor);
                Assert.That(pushed, Has.Count.EqualTo(1), "One push.");
                Assert.That(pushed[0].SourceId, Is.EqualTo(Runner));
                List<CausalEvent> broken = EventsOfType(simulation, CausalEventType.DoorBrokenDown);
                Assert.That(broken, Has.Count.EqualTo(1), "And the door is off its hinges.");
                Assert.That(broken[0].CausalParentEventId, Is.EqualTo(pushed[0].EventId));
                Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Broken));
                Assert.That(Door(simulation, OfficeWayOut).IsHeld, Is.False,
                    "Burst off its hinges, there is nothing left for the player to hold.");
            }
        }

        /// <summary>
        /// Somebody who once shut the door themselves still bursts it when the
        /// player is holding it: the hand on it is the player's doing, not
        /// theirs. The rule that people never batter a door they shut
        /// themselves used to stop them here, however strong they were.
        /// </summary>
        [Test]
        public void SomebodyStrongWhoOnceShutTheDoorThemselves_StillBurstsItWhenHeld()
        {
            ScenarioData data = RunnerAtAnUnlockedWayOut(Strong);
            using (var simulation = new Run(data, 42UL))
            {
                int door = DoorIndex(simulation, OfficeWayOut);
                simulation.AgentForTests(0).Doors.ShutByThem[door] = true;
                simulation.QueueCommand(PlayerCommandType.HoldDoor, OfficeWayOut, 1);
                Advance(simulation, 5 * Run.TicksPerSecond);

                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Has.Count.EqualTo(1),
                    "Strong enough, they get through the held door in one push, whoever shut it last.");
            }
        }

        /// <summary>
        /// A locked door takes no hand: its lock already holds it, and a hand
        /// there used to let the strong through a locked door in one push.
        /// </summary>
        [Test]
        public void ALockedDoor_CannotBeHeld()
        {
            using (var simulation = new Run(QuietRoom(TheBuilding.Office), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ToggleLock, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.HoldDoor, ClosetDoor, 2);
                Advance(simulation, 3);

                Assert.That(Door(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Locked));
                Assert.That(Door(simulation, ClosetDoor).IsHeld, Is.False);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerHeldDoor), Is.Empty);
            }
        }

        private static int DoorIndex(Run simulation, SimulationId id)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == id)
                {
                    return i;
                }
            }

            throw new KeyNotFoundException(id.ToString());
        }

        [Test]
        public void SomebodyWeak_RattlesAHeldDoor_GivesUp_AndLooksElsewhere()
        {
            ScenarioData data = RunnerAtAnUnlockedWayOut(Weak);
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.HoldDoor, OfficeWayOut, 1);
                Advance(simulation, 6 * Run.TicksPerSecond);

                Assert.That(EventsOfType(simulation, CausalEventType.AgentTriedDoor), Is.Not.Empty);
                List<CausalEvent> gaveUp = EventsOfType(simulation, CausalEventType.AgentGaveUpOnDoor);
                Assert.That(gaveUp, Is.Not.Empty, "They give it up as they would a wedged door.");
                Assert.That(gaveUp[0].TargetId, Is.EqualTo(OfficeWayOut));
                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened).Exists(record => record.SourceId == OfficeWayOut), Is.False,
                    "The held door never opened; whatever door they went for next is theirs to open.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Empty);
                Assert.That(Door(simulation, OfficeWayOut).State, Is.EqualTo(DoorState.Unlocked));
            }
        }

        /// <summary>A quiet office with one person standing still in the middle of it and no fire due.</summary>
        private ScenarioData QuietRoom(LogicalPosition where)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Runner, where, CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Tables = Array.Empty<TableDefinition>();
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            return data;
        }

        [Test]
        public void HoldingAnOpenDoor_ShutsIt_AndTheKeyIsNotTurnedWhileItIsHeld()
        {
            using (var simulation = new Run(QuietRoom(TheBuilding.Office), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.HoldDoor, ClosetDoor, 2);
                simulation.QueueCommand(PlayerCommandType.ToggleLock, ClosetDoor, 3);
                Advance(simulation, 3);

                Assert.That(Door(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Unlocked), "Pulled shut by the hand on it, and not locked.");
                Assert.That(Door(simulation, ClosetDoor).IsHeld, Is.True);
                List<CausalEvent> held = EventsOfType(simulation, CausalEventType.PowerHeldDoor);
                Assert.That(held, Has.Count.EqualTo(1));
                Assert.That(held[0].HasCausalParent, Is.False, "The player is the root cause.");
                List<CausalEvent> closed = EventsOfType(simulation, CausalEventType.DoorClosed);
                Assert.That(closed, Has.Count.EqualTo(1));
                Assert.That(closed[0].CausalParentEventId, Is.EqualTo(held[0].EventId), "Shut because it was taken hold of.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorLocked), Is.Empty, "A held door is not also locked.");
                Assert.That(simulation.Purse, Is.EqualTo(simulation.GetSnapshot().PurseMaximum - simulation.GetSnapshot().CostOfDoorClick(DoorState.Unlocked, false)),
                    "The click cost what a click costs; the hold cost nothing.");

                simulation.QueueCommand(PlayerCommandType.ReleaseDoor, ClosetDoor, 4);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 5);
                Advance(simulation, 2);
                Assert.That(Door(simulation, ClosetDoor).IsHeld, Is.False);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerReleasedDoor), Has.Count.EqualTo(1));
                Assert.That(Door(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Open), "Let go of, it is a door again.");
            }
        }

        [Test]
        public void AHandOnAnOpenDoor_WaitsForWhoeverIsStandingInIt()
        {
            // Somebody standing in the closet's doorway, on the wall line.
            using (var simulation = new Run(QuietRoom(new LogicalPosition(5700, 2500)), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.HoldDoor, ClosetDoor, 2);
                Advance(simulation, Run.TicksPerSecond);

                Assert.That(Door(simulation, ClosetDoor).IsHeld, Is.True, "The hand is on it.");
                Assert.That(Door(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Open), "But it cannot shut on somebody.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorClosed), Is.Empty);
            }
        }

        [Test]
        public void TheStory_SaysWhatThePlayerDid()
        {
            using (var simulation = new Run(QuietRoom(TheBuilding.Office), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.HoldDoor, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.ReleaseDoor, ClosetDoor, 2);
                Advance(simulation, 2);
                var story = new Paniq.Presentation.EventStory(simulation.GetSnapshot());
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.PowerHeldDoor)[0]), Is.EqualTo("you held door 2002 shut"));
                Assert.That(story.Describe(EventsOfType(simulation, CausalEventType.PowerReleasedDoor)[0]), Is.EqualTo("you let go of door 2002"));
            }
        }
    }
}

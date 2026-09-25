using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The player's influence and what they spend it on. Influence starts at a
    /// set amount, every card and every door click that actually does something
    /// takes its price, anything nobody can pay for does nothing at all, and the
    /// only thing that pays any back is somebody getting out alive.
    /// </summary>
    public sealed class PowersEditModeTests
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

        /// <summary>A quiet room with one ordinary person in it and no fire due.</summary>
        private ScenarioData QuietRoom()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;

            // No furniture or clutter, so anywhere in the room is clear floor.
            // The extinguishers stay, because two of the cards are about them.
            var bottles = new List<PhysicsObjectDefinition>();
            foreach (PhysicsObjectDefinition thing in data.PhysicsObjects)
            {
                if (thing.Kind == PhysicsObjectKind.Extinguisher)
                {
                    bottles.Add(thing);
                }
            }

            data.PhysicsObjects = bottles.ToArray();
            data.Tables = new TableDefinition[0];
            return data;
        }

        // ------------------------------------------------------------ doors
        //
        // Reaching into the building and working a door is the player's
        // commonest move and it used to be free, so there was never a reason
        // not to fling every door in the place open. Each click pays for what
        // that click does.

        /// <summary>The storage closet's door, which starts shut but unlocked.</summary>
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>The way out of the building, which starts locked.</summary>
        private static readonly SimulationId WayOut = new SimulationId(2008UL);

        private static DoorState StateOf(Run simulation, SimulationId door)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == door)
                {
                    return simulation.GetDoor(i).State;
                }
            }

            throw new KeyNotFoundException($"No door {door}.");
        }

        [Test]
        public void OpeningAShutDoor_CostsWhatTheScenarioSays()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.Step();

                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Open));
                Assert.That(simulation.Influence,
                    Is.EqualTo(data.Influence.Starting - data.Influence.OpenDoorCost));
                Assert.That(simulation.InfluenceSpent, Is.EqualTo(data.Influence.OpenDoorCost));
            }
        }

        [Test]
        public void ALockedDoor_CostsTheKeyAndThenTheDoor()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 1);
                simulation.Step();
                Assert.That(StateOf(simulation, WayOut), Is.EqualTo(DoorState.Unlocked),
                    "One click turns the key and leaves it shut, so the people inside can open it themselves.");
                Assert.That(simulation.Influence,
                    Is.EqualTo(data.Influence.Starting - data.Influence.UnlockExitCost),
                    "The way out costs the exit's price to unlock, not an inside door's.");

                simulation.QueueCommand(PlayerCommandType.ClickDoor, WayOut, 2);
                simulation.Step();
                Assert.That(StateOf(simulation, WayOut), Is.EqualTo(DoorState.Open));
                Assert.That(simulation.Influence, Is.EqualTo(
                    data.Influence.Starting - data.Influence.UnlockExitCost - data.Influence.OpenDoorCost));
            }
        }

        /// <summary>The key is the player's to turn (2026-09-25): ten on an inside door either way, and the whole purse at the way out.</summary>
        [Test]
        public void TheKey_CostsTenInside_AndTheWholePurseAtTheWayOut()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ToggleLock, ClosetDoor, 1);
                simulation.Step();
                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Locked), "Locked by the player.");
                Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.LockDoorCost));
                List<CausalEvent> locked = EventsOfType(simulation, CausalEventType.DoorLocked);
                Assert.That(locked, Has.Count.EqualTo(1));
                Assert.That(locked[0].SourceId, Is.EqualTo(ClosetDoor), "The player's own doing: the door names itself.");
                Assert.That(locked[0].HasCausalParent, Is.False);

                simulation.QueueCommand(PlayerCommandType.ToggleLock, ClosetDoor, 2);
                simulation.Step();
                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Unlocked), "And unlocked again.");
                Assert.That(simulation.Influence, Is.EqualTo(
                    data.Influence.Starting - data.Influence.LockDoorCost - data.Influence.UnlockDoorCost));

                simulation.QueueCommand(PlayerCommandType.ToggleLock, WayOut, 3);
                simulation.Step();
                Assert.That(StateOf(simulation, WayOut), Is.EqualTo(DoorState.Unlocked));
                Assert.That(simulation.Influence, Is.EqualTo(
                    data.Influence.Starting - data.Influence.LockDoorCost - data.Influence.UnlockDoorCost - data.Influence.UnlockExitCost));
            }
        }

        [Test]
        public void AnOpenDoor_LockedByThePlayer_IsShutFirst_AndCostsBoth()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.ToggleLock, ClosetDoor, 2);
                simulation.Step();
                simulation.Step();

                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Locked), "Shut and locked.");
                Assert.That(EventsOfType(simulation, CausalEventType.DoorClosed), Has.Count.EqualTo(1));
                Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting -
                    data.Influence.OpenDoorCost - data.Influence.CloseDoorCost - data.Influence.LockDoorCost));
            }
        }

        /// <summary>The shipped purse: a hundred at most, and the way out costs exactly that (the owner's rules, 2026-09-25).</summary>
        [Test]
        public void TheShippedPurse_HoldsAHundred_AndTheWayOutCostsAllOfIt()
        {
            ScenarioData data = scenario.ToRuntimeData();
            Assert.That(data.Influence.Maximum, Is.EqualTo(100));
            Assert.That(data.Influence.UnlockExitCost, Is.EqualTo(data.Influence.Maximum));
            Assert.That(data.Influence.OpenDoorCost, Is.EqualTo(10));
            Assert.That(data.Influence.CloseDoorCost, Is.EqualTo(10));
            Assert.That(data.Influence.LockDoorCost, Is.EqualTo(10));
            Assert.That(data.Influence.UnlockDoorCost, Is.EqualTo(10));
            using (var simulation = new Run(data))
            {
                simulation.GiveInfluenceForTests(1000);
                Assert.That(simulation.Influence, Is.EqualTo(100), "The purse never holds more than a hundred.");
            }
        }

        [Test]
        public void ClosingADoor_CostsSomethingToo()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 2);
                simulation.Step();
                simulation.Step();

                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Unlocked), "Shut again.");
                Assert.That(simulation.Influence, Is.EqualTo(
                    data.Influence.Starting - data.Influence.OpenDoorCost - data.Influence.CloseDoorCost));
            }
        }

        [Test]
        public void ADoorTheyCannotPayFor_StaysExactlyAsItWas()
        {
            ScenarioData data = QuietRoom();
            data.Influence.Starting = data.Influence.OpenDoorCost - 1;
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                simulation.Step();

                Assert.That(StateOf(simulation, ClosetDoor), Is.EqualTo(DoorState.Unlocked),
                    "Shut, as it started: they could not afford to open it.");
                Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting),
                    "And it cost them nothing to find that out.");
            }
        }

        [Test]
        public void ADoorAlreadyBrokenDown_CostsNothingToClickAt()
        {
            ScenarioData data = QuietRoom();
            using (var simulation = new Run(data))
            {
                // Blow the wall open: a hole is a way through with no door in
                // it, and there is nothing left to charge for working.
                simulation.QueueCommand(PlayerCommandType.BlastWall, new LogicalPosition(-5900, 0), 1);
                simulation.Step();
                int after = simulation.Influence;

                for (int i = 0; i < simulation.DoorCount; i++)
                {
                    DoorSnapshot door = simulation.GetDoor(i);
                    if (door.State != DoorState.Broken)
                    {
                        continue;
                    }

                    simulation.QueueCommand(PlayerCommandType.ClickDoor, door.DoorId, simulation.Tick + 1);
                    simulation.Step();
                    Assert.That(simulation.Influence, Is.EqualTo(after),
                        "There is nothing left of it to open, shut or unlock.");
                    return;
                }

                Assert.Fail("The wall should have been blown open.");
            }
        }

        [Test]
        public void Influence_StartsAtWhatTheScenarioSays()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting));
            Assert.That(simulation.InfluenceSpent, Is.Zero);
            Assert.That(simulation.InfluenceEarned, Is.Zero);
        }

        // ---------------------------------------------------------------- Beefcake

        [Test]
        public void Beefcake_MakesSomebodyAsStrongAsAnyoneCanBe()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new LogicalPosition(0, 0), 1);
            simulation.Step();

            Assert.That(simulation.GetAgent(0).Traits.Strength, Is.EqualTo(AgentTraitValues.Maximum));
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.CardCost));

            List<CausalEvent> played = EventsOfType(simulation, CausalEventType.PowerBeefcake);
            Assert.That(played, Is.Not.Empty, "Playing Beefcake should be in the log.");
            Assert.That(played[0].TargetId, Is.EqualTo(new SimulationId(1UL)), "It names who it was played on.");
            Assert.That(played[0].CausalParentEventId, Is.Zero, "The player is the cause, so it is a root event.");
            Assert.That(played[0].Strength, Is.EqualTo(data.Influence.CardCost), "It records what it cost.");
        }

        [Test]
        public void Beefcake_LeavesEveryOtherTraitAlone()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            AgentTraitValues before = simulation.GetAgent(0).Traits;
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new LogicalPosition(0, 0), 1);
            simulation.Step();
            AgentTraitValues after = simulation.GetAgent(0).Traits;

            Assert.That(after, Is.EqualTo(before.WithStrength(AgentTraitValues.Maximum)));
        }

        [Test]
        public void Beefcake_LetsSomebodyBatterDownADoorTheyCouldNotBudge()
        {
            // An ordinary person (strength 5) does no damage to a locked door at
            // all; the same person after Beefcake breaks it off its hinges.
            ScenarioData data = DoorsEditModeTests.RunnerByTheWayOut(
                TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()), 0, AgentTraitValues.AllOrdinary);
            data.Exits.DoorForceChancePercent = 100;
            data.Exits.DoorForceMinimumTicks = 100000;
            data.Exits.DoorForceMaximumTicks = 100000;
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, data.Agents[0].InitialPosition, 1);
            for (int t = 0; t < 40 * Run.TicksPerSecond &&
                            EventsOfType(simulation, CausalEventType.DoorBrokenDown).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, CausalEventType.DoorBrokenDown), Is.Not.Empty,
                "A Beefcake should shoulder a locked door off its hinges.");
        }

        [Test]
        public void Beefcake_ThrownWhereNobodyIsStanding_IsFree()
        {
            // Beefcake used to name a person, and naming somebody who was not
            // there threw. It is thrown at a patch of floor now, so the same
            // mistake is a miss instead: it catches nobody, and a card that
            // catches nobody costs neither influence nor the card.
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new LogicalPosition(4000, 4000), 1);
            simulation.Step();
            simulation.Step();

            Assert.That(simulation.GetAgent(0).Traits.Strength, Is.EqualTo(AgentTraitValues.Ordinary),
                "The person across the room is untouched.");
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting), "A miss is free.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerBeefcake), Is.Empty);
        }

        [Test]
        public void Beefcake_OnSomebodyAlreadyAtFullStrength_CostsNothing()
        {
            ScenarioData data = QuietRoom();
            data.Agents[0] = new AgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
                CardinalDirection.North, AgentTraitValues.AllOrdinary.WithStrength(AgentTraitValues.Maximum));
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new LogicalPosition(0, 0), 1);
            simulation.Step();

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting), "A card that does nothing is free.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerBeefcake), Is.Empty);
        }

        // ---------------------------------------------------------------- spawn fire

        [Test]
        public void SpawnFire_StartsAFireWhereThePlayerPoints()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            Assert.That(simulation.FireActive, Is.False, "No fire is due in this scenario.");

            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 1);
            simulation.Step();

            Assert.That(simulation.FireActive, Is.True, "The player's card should have started the fire.");
            Assert.That(simulation.FireCellCount, Is.EqualTo(1));
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.CardCost));

            List<CausalEvent> card = EventsOfType(simulation, CausalEventType.PowerSpawnedFire);
            Assert.That(card, Is.Not.Empty);
            Assert.That(card[0].CausalParentEventId, Is.Zero, "The player is the cause.");

            // The fire itself traces back to the card.
            List<CausalEvent> lit = EventsOfType(simulation, CausalEventType.FireActivated);
            Assert.That(lit, Is.Not.Empty);
            Assert.That(lit[0].CausalParentEventId, Is.EqualTo(card[0].EventId),
                "The fire should name the player's card as its cause.");
        }

        [Test]
        public void SpawnFire_SpreadsLikeAnyOtherFire()
        {
            var simulation = new Run(QuietRoom());
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 1);
            for (int t = 0; t < 20 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.GreaterThan(1), "A fire the player started should still spread.");
        }

        [Test]
        public void SpawnFire_OnSomewhereThatIsNotFloor_CostsNothing()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);

            // Far outside the building.
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(500000, 500000), 1);
            simulation.Step();

            Assert.That(simulation.FireActive, Is.False);
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting), "A card that does nothing is free.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerSpawnedFire), Is.Empty,
                "A card that could not be played leaves nothing in the log.");
        }

        [Test]
        public void SpawnFire_OnASquareAlreadyAlight_CostsNothing()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            var spot = new LogicalPosition(3000, 3000);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, spot, 1);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, spot, 2);
            simulation.Step();
            simulation.Step();

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.CardCost),
                "The second card should have been refused.");
        }

        // ---------------------------------------------------------------- spawn extinguisher

        [Test]
        public void SpawnExtinguisher_StandsAFullBottleWhereThePlayerPoints()
        {
            ScenarioData data = QuietRoom();
            var simulation = new Run(data);
            int bottlesBefore = CountBottlesInTheWorld(simulation);

            simulation.QueueCommand(PlayerCommandType.SpawnExtinguisher, new LogicalPosition(2000, 2000), 1);
            simulation.Step();

            Assert.That(CountBottlesInTheWorld(simulation), Is.EqualTo(bottlesBefore + 1));
            Assert.That(simulation.Influence,
                Is.EqualTo(data.Influence.Starting - data.Influence.CardCost));

            List<CausalEvent> card = EventsOfType(simulation, CausalEventType.PowerSpawnedExtinguisher);
            Assert.That(card, Is.Not.Empty);
            Assert.That(card[0].CausalParentEventId, Is.Zero, "The player is the cause.");
        }

        [Test]
        public void SpawnExtinguisher_RunsOutOfSpares()
        {
            ScenarioData data = QuietRoom();
            data.Influence.Starting = 10000;
            data.Influence.Maximum = 10000;
            var simulation = new Run(data);
            int spares = CountSpares(simulation);
            Assert.That(spares, Is.GreaterThan(0), "The scenario should keep some spares aside.");

            for (int i = 0; i < spares + 2; i++)
            {
                simulation.QueueCommand(PlayerCommandType.SpawnExtinguisher,
                    new LogicalPosition(-4000 + i * 1000, 3000), simulation.Tick + 1);
                simulation.Step();
            }

            Assert.That(CountSpares(simulation), Is.Zero, "Every spare should have been put down.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerSpawnedExtinguisher).Count,
                Is.EqualTo(spares), "Once the spares run out the card does nothing.");
        }

        private static int CountBottlesInTheWorld(Run simulation)
        {
            int count = 0;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                PhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                count += thing.Kind == PhysicsObjectKind.Extinguisher && !thing.Dormant ? 1 : 0;
            }

            return count;
        }

        private static int CountSpares(Run simulation)
        {
            int count = 0;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                count += simulation.GetPhysicsObject(i).Dormant ? 1 : 0;
            }

            return count;
        }

        // ---------------------------------------------------------------- paying for itself

        [Test]
        public void GettingSomebodyOut_PaysTheirRescueBack()
        {
            ScenarioData data = DoorsEditModeTests.RunnerByTheWayOut(
                TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()), 0, AgentTraitValues.AllOrdinary);
            int starting = data.Influence.Starting;
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, OfficeWayOut, 2);
            for (int t = 0; t < 20 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped), "Nobody got out.");
            // Two clicks to get the door open -- the key, then the door -- and
            // then somebody walks out through it and pays some of it back.
            int doorCost = data.Influence.UnlockExitCost + data.Influence.OpenDoorCost;
            Assert.That(simulation.Influence, Is.EqualTo(starting - doorCost + data.Influence.PerPersonSaved),
                "Getting somebody out should pay influence back, on top of what the door cost.");
            Assert.That(simulation.InfluenceEarned, Is.EqualTo(data.Influence.PerPersonSaved));
        }

        [Test]
        public void ACardNobodyCanPayFor_DoesNothing()
        {
            ScenarioData data = QuietRoom();
            data.Influence.Starting = 0;
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new LogicalPosition(0, 0), 1);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 2);
            simulation.Step();
            simulation.Step();

            Assert.That(simulation.GetAgent(0).Traits.Strength, Is.EqualTo(AgentTraitValues.Ordinary));
            Assert.That(simulation.FireActive, Is.False);
            Assert.That(simulation.Influence, Is.Zero);
        }

        // ReplayingTheSameCards_GivesTheSameRun compared nothing but the
        // random generator's final state after playing three cards, which
        // TheBusiestRun_PlaysOutTheSameWayThreeTimes covers with the whole run
        // hashed rather than one number, and the replay fingerprints cover
        // against recorded values as well.

    }
}

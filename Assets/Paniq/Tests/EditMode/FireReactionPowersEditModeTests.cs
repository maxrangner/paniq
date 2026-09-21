using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The player's influence and the cards they spend it on. Influence starts
    /// at a set amount, every card that actually does something takes its price,
    /// a card nobody can pay for does nothing at all, and the only thing that
    /// pays any back is somebody getting out of the building alive.
    /// </summary>
    public sealed class FireReactionPowersEditModeTests
    {
        private static readonly SimulationId NorthDoor = new SimulationId(2001UL);

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

        /// <summary>A quiet room with one ordinary person in it and no fire due.</summary>
        private FireReactionScenarioData QuietRoom()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;

            // No furniture or clutter, so anywhere in the room is clear floor.
            // The extinguishers stay, because two of the cards are about them.
            var bottles = new List<FireReactionPhysicsObjectDefinition>();
            foreach (FireReactionPhysicsObjectDefinition thing in data.PhysicsObjects)
            {
                if (thing.Kind == PhysicsObjectKind.Extinguisher)
                {
                    bottles.Add(thing);
                }
            }

            data.PhysicsObjects = bottles.ToArray();
            data.Tables = new FireReactionTableDefinition[0];
            return data;
        }

        [Test]
        public void Influence_StartsAtWhatTheScenarioSays()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting));
            Assert.That(simulation.InfluenceSpent, Is.Zero);
            Assert.That(simulation.InfluenceEarned, Is.Zero);
        }

        // ---------------------------------------------------------------- Beefcake

        [Test]
        public void Beefcake_MakesSomebodyAsStrongAsAnyoneCanBe()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1UL), 1);
            simulation.Step();

            Assert.That(simulation.GetAgent(0).Traits.Strength, Is.EqualTo(AgentTraitValues.Maximum));
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.BeefcakeCost));

            List<CausalEvent> played = EventsOfType(simulation, FireReactionEventType.PowerBeefcake);
            Assert.That(played, Is.Not.Empty, "Playing Beefcake should be in the log.");
            Assert.That(played[0].TargetId, Is.EqualTo(new SimulationId(1UL)), "It names who it was played on.");
            Assert.That(played[0].CausalParentEventId, Is.Zero, "The player is the cause, so it is a root event.");
            Assert.That(played[0].Strength, Is.EqualTo(data.Influence.BeefcakeCost), "It records what it cost.");
        }

        [Test]
        public void Beefcake_LeavesEveryOtherTraitAlone()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);
            AgentTraitValues before = simulation.GetAgent(0).Traits;
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1UL), 1);
            simulation.Step();
            AgentTraitValues after = simulation.GetAgent(0).Traits;

            Assert.That(after, Is.EqualTo(before.WithStrength(AgentTraitValues.Maximum)));
        }

        [Test]
        public void Beefcake_LetsSomebodyBatterDownADoorTheyCouldNotBudge()
        {
            // An ordinary person (strength 5) does no damage to a locked door at
            // all; the same person after Beefcake breaks it off its hinges.
            FireReactionScenarioData data = FireReactionDoorsEditModeTests.RunnerByTheNorthDoor(
                scenario.ToRuntimeData(), 0, AgentTraitValues.AllOrdinary);
            data.Exits.DoorForceChancePercent = 100;
            data.Exits.DoorForceMinimumTicks = 100000;
            data.Exits.DoorForceMaximumTicks = 100000;
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1UL), 1);
            for (int t = 0; t < 40 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.DoorBrokenDown).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorBrokenDown), Is.Not.Empty,
                "A Beefcake should shoulder a locked door off its hinges.");
        }

        [Test]
        public void Beefcake_CannotNameSomebodyWhoIsNotThere()
        {
            var simulation = new FireReactionSimulation(QuietRoom());
            Assert.That(() => simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(9999UL), 1),
                Throws.ArgumentException);
        }

        [Test]
        public void Beefcake_OnSomebodyAlreadyAtFullStrength_CostsNothing()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Agents[0] = new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(0, 0),
                CardinalDirection.North, AgentTraitValues.AllOrdinary.WithStrength(AgentTraitValues.Maximum));
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1UL), 1);
            simulation.Step();

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting), "A card that does nothing is free.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.PowerBeefcake), Is.Empty);
        }

        // ---------------------------------------------------------------- spawn fire

        [Test]
        public void SpawnFire_StartsAFireWhereThePlayerPoints()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);
            Assert.That(simulation.FireActive, Is.False, "No fire is due in this scenario.");

            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 1);
            simulation.Step();

            Assert.That(simulation.FireActive, Is.True, "The player's card should have started the fire.");
            Assert.That(simulation.FireCellCount, Is.EqualTo(1));
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.SpawnFireCost));

            List<CausalEvent> card = EventsOfType(simulation, FireReactionEventType.PowerSpawnedFire);
            Assert.That(card, Is.Not.Empty);
            Assert.That(card[0].CausalParentEventId, Is.Zero, "The player is the cause.");

            // The fire itself traces back to the card.
            List<CausalEvent> lit = EventsOfType(simulation, FireReactionEventType.FireActivated);
            Assert.That(lit, Is.Not.Empty);
            Assert.That(lit[0].CausalParentEventId, Is.EqualTo(card[0].EventId),
                "The fire should name the player's card as its cause.");
        }

        [Test]
        public void SpawnFire_SpreadsLikeAnyOtherFire()
        {
            var simulation = new FireReactionSimulation(QuietRoom());
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 1);
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.GreaterThan(1), "A fire the player started should still spread.");
        }

        [Test]
        public void SpawnFire_OnSomewhereThatIsNotFloor_CostsNothing()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);

            // Far outside the building.
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(500000, 500000), 1);
            simulation.Step();

            Assert.That(simulation.FireActive, Is.False);
            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting), "A card that does nothing is free.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.PowerSpawnedFire), Is.Empty,
                "A card that could not be played leaves nothing in the log.");
        }

        [Test]
        public void SpawnFire_OnASquareAlreadyAlight_CostsNothing()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);
            var spot = new LogicalPosition(3000, 3000);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, spot, 1);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, spot, 2);
            simulation.Step();
            simulation.Step();

            Assert.That(simulation.Influence, Is.EqualTo(data.Influence.Starting - data.Influence.SpawnFireCost),
                "The second card should have been refused.");
        }

        // ---------------------------------------------------------------- spawn extinguisher

        [Test]
        public void SpawnExtinguisher_StandsAFullBottleWhereThePlayerPoints()
        {
            FireReactionScenarioData data = QuietRoom();
            var simulation = new FireReactionSimulation(data);
            int bottlesBefore = CountBottlesInTheWorld(simulation);

            simulation.QueueCommand(PlayerCommandType.SpawnExtinguisher, new LogicalPosition(2000, 2000), 1);
            simulation.Step();

            Assert.That(CountBottlesInTheWorld(simulation), Is.EqualTo(bottlesBefore + 1));
            Assert.That(simulation.Influence,
                Is.EqualTo(data.Influence.Starting - data.Influence.SpawnExtinguisherCost));

            List<CausalEvent> card = EventsOfType(simulation, FireReactionEventType.PowerSpawnedExtinguisher);
            Assert.That(card, Is.Not.Empty);
            Assert.That(card[0].CausalParentEventId, Is.Zero, "The player is the cause.");
        }

        [Test]
        public void SpawnExtinguisher_RunsOutOfSpares()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Influence.Starting = 10000;
            data.Influence.Maximum = 10000;
            var simulation = new FireReactionSimulation(data);
            int spares = CountSpares(simulation);
            Assert.That(spares, Is.GreaterThan(0), "The scenario should keep some spares aside.");

            for (int i = 0; i < spares + 2; i++)
            {
                simulation.QueueCommand(PlayerCommandType.SpawnExtinguisher,
                    new LogicalPosition(-4000 + i * 1000, 3000), simulation.Tick + 1);
                simulation.Step();
            }

            Assert.That(CountSpares(simulation), Is.Zero, "Every spare should have been put down.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.PowerSpawnedExtinguisher).Count,
                Is.EqualTo(spares), "Once the spares run out the card does nothing.");
        }

        private static int CountBottlesInTheWorld(FireReactionSimulation simulation)
        {
            int count = 0;
            for (int i = 0; i < simulation.PhysicsObjectCount; i++)
            {
                FireReactionPhysicsObjectSnapshot thing = simulation.GetPhysicsObject(i);
                count += thing.Kind == PhysicsObjectKind.Extinguisher && !thing.Dormant ? 1 : 0;
            }

            return count;
        }

        private static int CountSpares(FireReactionSimulation simulation)
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
            FireReactionScenarioData data = FireReactionDoorsEditModeTests.RunnerByTheNorthDoor(
                scenario.ToRuntimeData(), 0, AgentTraitValues.AllOrdinary);
            int starting = data.Influence.Starting;
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, 2);
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped), "Nobody got out.");
            Assert.That(simulation.Influence, Is.EqualTo(starting + data.Influence.PerPersonSaved),
                "Getting somebody out should pay influence back. Clicking a door is free.");
            Assert.That(simulation.InfluenceEarned, Is.EqualTo(data.Influence.PerPersonSaved));
        }

        [Test]
        public void ACardNobodyCanPayFor_DoesNothing()
        {
            FireReactionScenarioData data = QuietRoom();
            data.Influence.Starting = 0;
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1UL), 1);
            simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(3000, 3000), 2);
            simulation.Step();
            simulation.Step();

            Assert.That(simulation.GetAgent(0).Traits.Strength, Is.EqualTo(AgentTraitValues.Ordinary));
            Assert.That(simulation.FireActive, Is.False);
            Assert.That(simulation.Influence, Is.Zero);
        }

        [Test]
        public void ReplayingTheSameCards_GivesTheSameRun()
        {
            ulong First()
            {
                FireReactionScenarioData data = scenario.ToRuntimeData();
                var simulation = new FireReactionSimulation(data, 42UL);
                simulation.QueueCommand(PlayerCommandType.PlayBeefcake, new SimulationId(1006UL), 60);
                simulation.QueueCommand(PlayerCommandType.SpawnFire, new LogicalPosition(4000, 4000), 120);
                simulation.QueueCommand(PlayerCommandType.SpawnExtinguisher, new LogicalPosition(-4000, 4000), 180);
                for (int t = 0; t < 600; t++)
                {
                    simulation.Step();
                }

                return simulation.Random.State;
            }

            Assert.That(First(), Is.EqualTo(First()), "The same cards on the same seed must give the same run.");
        }
    }
}

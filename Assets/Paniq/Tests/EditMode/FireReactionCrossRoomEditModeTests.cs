using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Things people can do now that they could not before, because nothing
    /// could work out how to cross a room.
    ///
    /// Each of these used to be impossible by rule, not by circumstance: the
    /// behaviours were written to refuse anything outside the room the person
    /// was standing in, because setting off for it only walked them into the
    /// wall between. They are the checks that would have failed before people
    /// could find their way.
    /// </summary>
    public sealed class FireReactionCrossRoomEditModeTests
    {
        private static readonly SimulationId WhereTheFireIs = new SimulationId(8001UL);
        private static readonly SimulationId NextDoor = new SimulationId(8002UL);
        private static readonly SimulationId BetweenThem = new SimulationId(8101UL);
        private static readonly SimulationId TheWayOut = new SimulationId(8102UL);
        private static readonly SimulationId TheBottle = new SimulationId(8201UL);
        private static readonly SimulationId TheBraveOne = new SimulationId(8301UL);
        private static readonly SimulationId TheOneWhoShouts = new SimulationId(8302UL);

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
        /// Two rooms joined by an open door. The fire is in the first; the
        /// bottle and the one person are in the second.
        /// </summary>
        private FireReactionScenarioData TwoRooms()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.BlastHoles = new SimulationId[0];
            data.Rooms = new[]
            {
                new FireReactionRoomDefinition(WhereTheFireIs, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new FireReactionRoomDefinition(NextDoor, new LogicalBounds(6000, 18000, -6000, 6000))
            };
            data.Doors = new[]
            {
                new FireReactionDoorDefinition(BetweenThem, WhereTheFireIs, WallSide.East, 0, 1000, false),

                // A way out of the burning room of its own, so whoever is in
                // there runs for that rather than back through the one doorway
                // the person with the bottle is coming the other way along.
                new FireReactionDoorDefinition(TheWayOut, WhereTheFireIs, WallSide.West, 0, 1000, false)
            };

            // A fire in the first room that stays where it is, so the test is
            // about somebody coming to it rather than it coming to them.
            data.Fire.ActivationTick = 10;
            data.Fire.SpawnBounds = new LogicalBounds(-3000, -3000, 0, 0);
            // A shout everybody in the building hears, so the test is about
            // what the brave one does once they know, not about whether the
            // news reaches them.
            data.Hearing.YellAlarmRadiusMillimetres = 40000;
            data.Hearing.YellHearingRadiusMillimetres = 40000;
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;
            return data;
        }

        [Test]
        public void ABravePerson_CarriesABottleIntoTheNextRoomToFightTheFire()
        {
            FireReactionScenarioData data = TwoRooms();
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(
                    TheBottle, PhysicsObjectKind.Extinguisher, new LogicalPosition(12000, 0), 300, 9000)
            };
            data.Agents = new[]
            {
                // Somebody standing over the fire who will see it and shout.
                // The brave one is two rooms of shouting away and would
                // otherwise never learn there was a fire at all.
                new FireReactionAgentDefinition(
                    TheOneWhoShouts, new LogicalPosition(-3000, 1500), CardinalDirection.South,
                    AgentTraitValues.AllOrdinary),
                new FireReactionAgentDefinition(
                    TheBraveOne, new LogicalPosition(12000, 1500), CardinalDirection.West,
                    new AgentTraitValues(
                        AgentTraitValues.Ordinary, AgentTraitValues.Ordinary, AgentTraitValues.Maximum,
                        AgentTraitValues.Ordinary, AgentTraitValues.Minimum, AgentTraitValues.Minimum))
            };

            var simulation = new FireReactionSimulation(data, 42UL);
            // Unlocked to start with, so one click swings each open.
            simulation.QueueCommand(PlayerCommandType.ClickDoor, BetweenThem, 5);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 5);

            bool sprayed = false;
            for (int tick = 0; tick < 60 * FireReactionSimulation.TicksPerSecond && !sprayed; tick++)
            {
                simulation.Step();
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    sprayed |= record.EventType == FireReactionEventType.ExtinguisherSprayed;
                }
            }

            Assert.That(sprayed, Is.True,
                "Nobody carried the bottle through the doorway to the fire. This is the behaviour that used to be " +
                "switched off, because walking to a fire in another room only walked them into the wall.");
        }

        [Test]
        public void ACalmPerson_SometimesWandersIntoTheNextRoom()
        {
            // Calm people could not leave the room they started in at all: only
            // somebody running for a way out was allowed into a doorway, and a
            // calm person is not running for one. The building read as sealed
            // boxes rather than one place.
            FireReactionScenarioData data = TwoRooms();
            data.Fire.ActivationTick = int.MaxValue;
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Calm.StrollNextDoorPercent = 100;

            var people = new FireReactionAgentDefinition[6];
            for (int i = 0; i < people.Length; i++)
            {
                people[i] = new FireReactionAgentDefinition(
                    new SimulationId((ulong)(8400 + i)),
                    new LogicalPosition(-4000 + i * 1200, 0),
                    CardinalDirection.East,
                    AgentTraitValues.AllOrdinary);
            }

            data.Agents = people;

            var simulation = new FireReactionSimulation(data, 42UL);
            // Unlocked to start with, so one click swings it open.
            simulation.QueueCommand(PlayerCommandType.ClickDoor, BetweenThem, 5);

            bool wentNextDoor = false;
            for (int tick = 0; tick < 120 * FireReactionSimulation.TicksPerSecond && !wentNextDoor; tick++)
            {
                simulation.Step();
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    wentNextDoor |= simulation.GetAgent(i).Position.X > 7000;
                }
            }

            Assert.That(wentNextDoor, Is.True, "Nobody calm ever went through the doorway into the next room.");
        }
    }
}

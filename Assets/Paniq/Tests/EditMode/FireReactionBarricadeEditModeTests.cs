using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Things wedged in doorways. Anything resting in a doorway jams the door
    /// both ways, so it can neither be opened nor shut. Somebody strong heaves
    /// the obstruction clear; anybody else treats the door as shut and looks
    /// elsewhere. And people wedge doors on purpose: the frightened to keep the
    /// fire out, the cruel to keep other people out.
    /// </summary>
    public sealed class FireReactionBarricadeEditModeTests
    {
        private static readonly SimulationId NorthDoor = new SimulationId(2001UL);
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);
        private static readonly SimulationId TheBox = new SimulationId(3001UL);
        private static readonly SimulationId Somebody = new SimulationId(1UL);

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

        /// <summary>
        /// The north door of the office, unlocked, with one box in its doorway.
        /// By default the box sits across the wall line itself, so it is in the
        /// frame and stops the leaf swinging whichever way it would go.
        /// </summary>
        /// <param name="millimetresInsideTheRoom">
        /// How far in from the wall line the box sits. 0 puts it in the frame;
        /// 200 puts it clear of the frame on the inside, where it stops the leaf
        /// swinging inwards and leaves the outward swing free.
        /// </param>
        private FireReactionScenarioData BoxInTheNorthDoorway(int millimetresInsideTheRoom = 200)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];

            // The north door's gap is centred on x = -2500 in the wall at z = 6000.
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box,
                    new LogicalPosition(-2500, 6000 - millimetresInsideTheRoom), 400, 12000)
            };
            data.Doors = new[]
            {
                new FireReactionDoorDefinition(NorthDoor, FireReactionScenarioData.Office, WallSide.North, -2500, 1000, false)
            };
            data.Rooms = new[]
            {
                new FireReactionRoomDefinition(FireReactionScenarioData.Office,
                    new LogicalBounds(-6000, 6000, -6000, 6000))
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Somebody, new LogicalPosition(-2500, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            return data;
        }

        /// <summary>
        /// A chair left in a doorway stops that door dead, whichever side it is
        /// on and whichever way the leaf would swing: the thing is sitting in the
        /// gap the leaf has to sweep. The player's clicks do nothing until
        /// somebody shifts it, which is the point of it.
        /// </summary>
        [Test]
        public void SomethingRestingInADoorway_JamsTheDoorShut()
        {
            var simulation = new FireReactionSimulation(BoxInTheNorthDoorway());

            // One tick for the blockage to be noticed, then the player tries to
            // open the door: nothing happens.
            simulation.Step();
            simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, simulation.Tick + 1);
            simulation.Step();
            simulation.Step();

            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Unlocked),
                "A door with something wedged against it does not open.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorOpened), Is.Empty);
        }

        [Test]
        public void SomethingRestingInADoorway_AlsoStopsTheDoorBeingShut()
        {
            FireReactionScenarioData data = BoxInTheNorthDoorway();

            // Put the box a little further in so the door can open first.
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(-2500, 4000), 400, 12000)
            };
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, 1);
            simulation.Step();
            simulation.Step();
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open), "It should have opened.");

            // Now slide the box into the gap and try to close it.
            simulation.LaunchObjectForTests(0, 0, 90);
            for (int t = 0; t < 2 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            simulation.QueueCommand(PlayerCommandType.ClickDoor, NorthDoor, simulation.Tick + 1);
            simulation.Step();
            simulation.Step();

            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorBlocked), Is.Not.Empty,
                "The box should have ended up in the doorway.");
            Assert.That(StateOf(simulation, NorthDoor), Is.EqualTo(DoorState.Open),
                "A door with something in the gap cannot be shut either.");
        }

        [Test]
        public void AJammedDoorway_IsLoggedWithWhatJammedItAndWhichDoor()
        {
            var simulation = new FireReactionSimulation(BoxInTheNorthDoorway());
            simulation.Step();

            List<CausalEvent> blocked = EventsOfType(simulation, FireReactionEventType.DoorBlocked);
            Assert.That(blocked, Is.Not.Empty, "Something resting in a doorway should be in the log.");
            Assert.That(blocked[0].SourceId, Is.EqualTo(TheBox), "It names the thing.");
            Assert.That(blocked[0].TargetId, Is.EqualTo(NorthDoor), "And the door it jammed.");
        }

        /// <summary>
        /// Somebody frightened running for the north door, which has a box wedged
        /// in it. Their strength decides whether they heave it clear or give up.
        /// </summary>
        private FireReactionScenarioData RunnerAtAJammedDoor(int strength)
        {
            FireReactionScenarioData data = BoxInTheNorthDoorway();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Somebody, new LogicalPosition(-2500, 2000), CardinalDirection.South,
                    new AgentTraitValues(strength, 5, 5, 5, 0, 3))
            };
            // Close enough to see, far enough away that they will still stand at
            // the door rather than bolt from it.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-2500, -2500, -700, -700);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;

            // Always willing to have a go at a door rather than give up at once.
            data.Exits.DoorForceChancePercent = 100;
            return data;
        }

        [Test]
        public void AStrongRunner_HeavesTheObstructionOutOfTheWay()
        {
            var simulation = new FireReactionSimulation(RunnerAtAJammedDoor(9));
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.AgentShovedObstruction).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> heaved = EventsOfType(simulation, FireReactionEventType.AgentShovedObstruction);
            Assert.That(heaved, Is.Not.Empty, "A strong person should shift whatever is wedged in the doorway.");
            Assert.That(heaved[0].SourceId, Is.EqualTo(Somebody));
            Assert.That(heaved[0].TargetId, Is.EqualTo(TheBox));

            // And then the door works again.
            for (int t = 0; t < 10 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorUnblocked), Is.Not.Empty,
                "Once heaved aside, the doorway is clear again.");
        }

        [Test]
        public void AWeakRunner_TreatsAJammedDoorLikeALockedOne()
        {
            var simulation = new FireReactionSimulation(RunnerAtAJammedDoor(2));
            for (int t = 0; t < 20 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentShovedObstruction), Is.Empty,
                "Somebody weak cannot shift it.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentTriedDoor), Is.Not.Empty,
                "But they should have gone up and tried it.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentGaveUpOnDoor), Is.Not.Empty,
                "And then given up on it, as they would on a locked door.");
        }

        // ------------------------------------------------------- on purpose

        /// <summary>
        /// One person sheltering in the storage closet with its door shut and the
        /// office beyond it alight, and a waste bin in the closet to wedge it with.
        /// </summary>
        private FireReactionScenarioData ShelteringWithTheFireNextDoor(AgentTraitValues who)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Tables = new FireReactionTableDefinition[0];
            data.Alarms = new FireReactionAlarmDefinition[0];
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(TheBox, PhysicsObjectKind.WasteBin, new LogicalPosition(7500, 3000), 320, 3000)
            };
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Somebody, new LogicalPosition(7500, 2000), CardinalDirection.West, who)
            };

            // A fire in the office, right outside the closet door.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(5250, 5250, 2250, 2250);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Hearing.FireHearingRadiusMillimetres = 4000;

            // The closet is small and the fire is just outside its door, so
            // somebody unwilling to go anywhere near the flames would never get
            // to the door at all. This person is prepared to get close.
            data.Panic.DangerDistanceMillimetres = 700;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Leadership.LeaderMinimum = AgentTraitValues.Maximum + 1;

            // Nowhere to run: every way out of the building is locked, so the
            // closet is where they are staying.
            return data;
        }

        /// <summary>
        /// The whole sequence a shelterer goes through: the closet door starts
        /// open so they can see the flames through it, which frightens them; they
        /// pull it shut; and then they wedge something against it.
        /// </summary>
        private static FireReactionSimulation Sheltering(FireReactionScenarioData data)
        {
            var simulation = new FireReactionSimulation(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
            return simulation;
        }

        [Test]
        public void AFrightenedShelterer_WedgesTheDoorAgainstTheFire()
        {
            // Very nervous, so they reach for something to jam the door with.
            FireReactionSimulation simulation = Sheltering(
                ShelteringWithTheFireNextDoor(new AgentTraitValues(5, 5, 5, 3, 0, 10)));
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.AgentBarricadedDoor).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> wedged = EventsOfType(simulation, FireReactionEventType.AgentBarricadedDoor);
            Assert.That(wedged, Is.Not.Empty, "Somebody frightened should wedge the door of the room they hide in.");
            Assert.That(wedged[0].SourceId, Is.EqualTo(Somebody));
            Assert.That(wedged[0].TargetId, Is.EqualTo(ClosetDoor));
            Assert.That(EventsOfType(simulation, FireReactionEventType.DoorBlocked), Is.Not.Empty,
                "And the door should then be jammed.");
        }

        [Test]
        public void ACalmSteadyShelterer_DoesNotBotherWedgingAnything()
        {
            FireReactionSimulation simulation = Sheltering(
                ShelteringWithTheFireNextDoor(new AgentTraitValues(5, 5, 8, 5, 0, 1)));
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentBarricadedDoor), Is.Empty,
                "Somebody steady just leaves.");
        }

        [Test]
        public void ABarricader_DoesNotFlingTheThingAwayTheMomentTheyPickItUp()
        {
            // The regression this feature dies quietly to: a frightened person
            // normally throws whatever they are holding, which would undo the
            // barricade before it was ever set down.
            FireReactionSimulation simulation = Sheltering(
                ShelteringWithTheFireNextDoor(new AgentTraitValues(5, 5, 5, 3, 0, 10)));
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.AgentBarricadedDoor).Count == 0; t++)
            {
                simulation.Step();
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.AgentBarricadedDoor), Is.Not.Empty,
                "They should have got it to the door and set it down.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.ItemThrown), Is.Empty,
                "A barricader keeps hold of what they are carrying.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The blank test levels (2026-09-30): the square room, the maze and
    /// the interaction room, each drawn by code on the office's tuning.
    /// These check the buildings themselves -- that a run accepts them and
    /// that they hold what they say they hold; what a crowd does in them is
    /// CrowdSwitchEditModeTests and MazeEditModeTests.
    /// </summary>
    public sealed class TestBuildingsEditModeTests
    {
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

        [TestCase(BuiltInBuilding.SquareRoom)]
        [TestCase(BuiltInBuilding.Maze)]
        [TestCase(BuiltInBuilding.InteractionRoom)]
        public void EveryTestBuilding_IsAcceptedByARun_AndHoldsEverybodyForASecond(BuiltInBuilding building)
        {
            ScenarioData data = TestBuildings.Apply(building, scenario.ToRuntimeData());
            Assert.That(() => data.Clone().Validate(), Throws.Nothing);
            using (var simulation = new Run(data, 7UL))
            {
                for (int tick = 0; tick < Run.TicksPerSecond; tick++)
                {
                    simulation.Step();
                }

                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    Assert.That(simulation.GetAgent(i).Participation, Is.EqualTo(AgentParticipation.Participating),
                        $"Person {i} left or was lost in a calm second.");
                    Assert.That(simulation.GetAgent(i).FearState, Is.EqualTo(AgentFearState.Calm),
                        $"Person {i} took fright with nothing to be frightened of.");
                }

                Assert.That(simulation.GetSnapshot().FireActive, Is.False, "Nothing burns until the player asks.");
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.BeforeEvent));
            }
        }

        [Test]
        public void NoBuiltInBuilding_LeavesTheScenarioExactlyAsItWas()
        {
            ScenarioData data = scenario.ToRuntimeData();
            Assert.That(TestBuildings.Apply(BuiltInBuilding.None, data), Is.SameAs(data));
            Assert.That(data.Agents, Has.Length.EqualTo(20), "The office, untouched.");
        }

        [Test]
        public void TheSquareRoom_IsOneRoomWithAWayOutInEveryWall_AndFortyPeople()
        {
            ScenarioData data = TestBuildings.SquareRoom(scenario.ToRuntimeData());
            Assert.That(data.Rooms, Has.Length.EqualTo(1));
            Assert.That(data.Agents, Has.Length.EqualTo(40));
            Assert.That(data.Doors, Has.Length.EqualTo(4));
            var sides = new HashSet<WallSide>();
            foreach (DoorDefinition door in data.Doors)
            {
                sides.Add(door.Side);
                Assert.That(door.StartsLocked, Is.False, "The doors start shut but unlocked, so people open them.");
            }

            Assert.That(sides, Has.Count.EqualTo(4), "One door in each wall.");
            using (var simulation = new Run(data, 7UL))
            {
                IReadOnlyList<DoorSnapshot> doors = simulation.GetSnapshot().Doors;
                int waysOut = 0;
                foreach (DoorSnapshot door in doors)
                {
                    waysOut += door.LeadsOutside ? 1 : 0;
                }

                Assert.That(waysOut, Is.EqualTo(4), "Every door leads out of the building.");
            }

            foreach (AgentDefinition person in data.Agents)
            {
                Assert.That(person.HasAuthoredTraits, Is.False, "The seed deals the crowd's personalities.");
            }
        }

        [Test]
        public void TheMaze_HasOneWayOut_AndAWindingWalkToIt()
        {
            ScenarioData data = TestBuildings.Maze(scenario.ToRuntimeData());
            Assert.That(data.Rooms, Has.Length.EqualTo(36), "Six cells by six.");
            using (var simulation = new Run(data, 7UL))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                int waysOut = 0;
                for (int door = 0; door < geometry.DoorCount; door++)
                {
                    waysOut += geometry.DoorLeadsOutside(door) ? 1 : 0;
                }

                Assert.That(waysOut, Is.EqualTo(1), "A maze has one way out.");

                // From the staff member's cell to the cell with the way out:
                // reachable, and a good deal longer than the building is wide
                // plus tall, or it is not a maze.
                int cell = TestBuildings.MazeCellMillimetres;
                LogicalPosition start = data.Agents[0].InitialPosition;
                var exitCell = new LogicalPosition(5 * cell + cell / 2, cell / 2);
                long walking = geometry.Routes.WalkingDistance(start, exitCell, data.World.OccupancyRadiusMillimetres);
                Assert.That(walking, Is.Not.EqualTo(long.MaxValue), "The way out cannot be reached from the start.");
                Assert.That(walking, Is.GreaterThan(48000L), $"{walking} mm is too short a walk for a maze of this size.");
            }

            Assert.That(data.Agents[0].Familiarity, Is.EqualTo(AgentFamiliarity.KnowsTheBuilding), "One person works here.");
            Assert.That(data.Agents[0].Traits.Leadership, Is.GreaterThanOrEqualTo(data.Leadership.LeaderMinimum),
                "And they are a leader, so a rally gathers the room.");
            for (int i = 1; i < data.Agents.Length; i++)
            {
                Assert.That(data.Agents[i].Familiarity, Is.EqualTo(AgentFamiliarity.Visitor), $"Person {i} should be a visitor.");
            }
        }

        [Test]
        public void TheInteractionRoom_HasOneOfEverything()
        {
            ScenarioData data = TestBuildings.InteractionRoom(scenario.ToRuntimeData());
            var kinds = new HashSet<PhysicsObjectKind>();
            foreach (PhysicsObjectDefinition thing in data.PhysicsObjects)
            {
                kinds.Add(thing.Kind);
            }

            foreach (PhysicsObjectKind wanted in new[]
                     {
                         PhysicsObjectKind.Box, PhysicsObjectKind.Chair, PhysicsObjectKind.OfficeChair,
                         PhysicsObjectKind.Extinguisher, PhysicsObjectKind.AlarmSounder, PhysicsObjectKind.StandingLamp,
                         PhysicsObjectKind.Bag, PhysicsObjectKind.WasteBin, PhysicsObjectKind.Laptop
                     })
            {
                Assert.That(kinds, Does.Contain(wanted), $"There should be a {wanted} to interact with.");
            }

            Assert.That(data.Tables, Has.Length.EqualTo(1), "A desk.");
            Assert.That(data.Alarms, Has.Length.EqualTo(1), "A pull station.");

            bool swing = false, archway = false, lockedExit = false, openExit = false, innerDoor = false;
            using (var simulation = new Run(data, 7UL))
            {
                foreach (DoorSnapshot placed in simulation.GetSnapshot().Doors)
                {
                    DoorDefinition door = System.Array.Find(data.Doors, d => d.DoorId == placed.DoorId);
                    bool outside = placed.LeadsOutside;
                    swing |= door.Swings;
                    archway |= door.IsOpening;
                    lockedExit |= outside && door.StartsLocked;
                    openExit |= outside && !door.StartsLocked;
                    innerDoor |= !outside && !door.Swings && !door.IsOpening;
                }
            }

            Assert.That(swing, "A pair of swing doors.");
            Assert.That(archway, "An archway.");
            Assert.That(innerDoor, "An ordinary shut door.");
            Assert.That(lockedExit, "A way out that starts locked.");
            Assert.That(openExit, "A way out that does not.");

            int visitors = 0;
            foreach (AgentDefinition person in data.Agents)
            {
                visitors += person.Familiarity == AgentFamiliarity.Visitor ? 1 : 0;
            }

            Assert.That(visitors, Is.EqualTo(1), "One person who does not know the way.");
        }

        [Test]
        public void TheTriggerOnTheInteractionRoom_LightsAFireInTheMiddle()
        {
            ScenarioData data = TestBuildings.InteractionRoom(scenario.ToRuntimeData());
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), 5);
                for (int tick = 0; tick < 30; tick++)
                {
                    simulation.Step();
                }

                RunSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.FireActive, Is.True);
                Assert.That(data.Rooms[0].Bounds.ContainsCircle(snapshot.FireOrigin, 0), "The fire starts in the main room.");
            }
        }

        /// <summary>
        /// Every number a test building uses is in a range of its own, so a
        /// test that lays office things in a test building never collides.
        /// </summary>
        [TestCase(BuiltInBuilding.SquareRoom)]
        [TestCase(BuiltInBuilding.Maze)]
        [TestCase(BuiltInBuilding.InteractionRoom)]
        public void TheTestBuildings_UseNumbersNothingElseDoes(BuiltInBuilding building)
        {
            ScenarioData data = TestBuildings.Apply(building, scenario.ToRuntimeData());
            var ids = new List<SimulationId>();
            foreach (RoomDefinition room in data.Rooms) ids.Add(room.RoomId);
            foreach (DoorDefinition door in data.Doors) ids.Add(door.DoorId);
            foreach (AgentDefinition person in data.Agents) ids.Add(person.AgentId);
            foreach (PhysicsObjectDefinition thing in data.PhysicsObjects) ids.Add(thing.ObjectId);
            foreach (TableDefinition table in data.Tables) ids.Add(table.TableId);
            foreach (AlarmDefinition alarm in data.Alarms) ids.Add(alarm.AlarmId);
            foreach (SimulationId id in ids)
            {
                Assert.That(id.Value, Is.GreaterThanOrEqualTo(40001UL).And.LessThan(50000UL), $"{id} is outside the test buildings' range.");
            }

            Assert.That(new HashSet<SimulationId>(ids), Has.Count.EqualTo(ids.Count), "No number is used twice.");
        }

        /// <summary>A level asset naming a built-in building plays that building by the level's rules on the office's tuning.</summary>
        [Test]
        public void ABuiltInLevel_PlaysItsBuildingByTheLevelsRules()
        {
            LevelDefinition level = LevelDefinition.CreateBuiltIn("square-test", "Square", BuiltInBuilding.SquareRoom, false);
            try
            {
                ScenarioData data = level.ToRuntimeData();
                Assert.That(data.Rooms, Has.Length.EqualTo(1));
                Assert.That(data.Agents, Has.Length.EqualTo(40));
                Assert.That(data.Round.HazardWaitsForTrigger, Is.True);
                Assert.That(data.Purse.Enabled, Is.True, "The doors are the owner's to work, so there is a purse.");
                Assert.That(data.Keycard.Enabled, Is.False, "No keycard in a blank room.");
                Assert.That(data.Director.ClimbsTheLadder, Is.False, "The ladder is the office's.");
                Assert.That(data.Alarm.PlayerMayPull, Is.True);
                Assert.That(level.OffersCrowdSwitch, Is.True);
                Assert.That(level.TriggerStartsAHazard, Is.False);
                Assert.That(level.BuiltInBuilding, Is.EqualTo(BuiltInBuilding.SquareRoom));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
            }
        }
    }
}

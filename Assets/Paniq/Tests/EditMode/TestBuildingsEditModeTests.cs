using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;
using UnityEditor;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The test levels (2026-10-01, the owner: "blank levels to test
    /// panicked crowds ... large square room with walls, maze to test
    /// following, interaction test level"). Each is a building drawn by code
    /// over the office's tuning: the square room, the maze and the
    /// interaction room. These tests pin that every one of them is a
    /// building the simulation accepts, that it runs, and that the level
    /// assets on disk name the buildings they claim to.
    /// </summary>
    public sealed class TestBuildingsEditModeTests
    {
        private const string LevelsFolder = "Assets/Paniq/Content/Levels/";

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

        private ScenarioData Built(BuiltInBuilding building) => TestBuildings.Apply(building, scenario.ToRuntimeData());

        [TestCase(BuiltInBuilding.SquareRoom)]
        [TestCase(BuiltInBuilding.Maze)]
        [TestCase(BuiltInBuilding.InteractionRoom)]
        public void EveryBuiltInBuilding_IsAValidScenario_WithTheOfficeTakenOut(BuiltInBuilding building)
        {
            ScenarioData data = Built(building);
            Assert.That(() => data.Validate(), Throws.Nothing, "A building drawn by code must pass the same checks a baked one does.");
            Assert.That(data.ScenarioId, Is.Not.EqualTo(scenario.ToRuntimeData().ScenarioId), "It is not the office.");
            Assert.That(data.Keycard.Enabled, Is.False, "There is no card to fetch on a test level.");
            Assert.That(data.Fire.ActivationTick, Is.EqualTo(int.MaxValue), "Nothing burns until the player asks.");
            Assert.That(data.Timetable, Is.Empty, "The office's day does not happen here.");
            Assert.That(data.TrapDefinitions, Is.Empty, "No tower, no stack.");
            Assert.That(data.Purse.Starting, Is.EqualTo(data.Purse.Maximum), "The purse opens full: the doors are the owner's to work.");
        }

        [TestCase(BuiltInBuilding.SquareRoom)]
        [TestCase(BuiltInBuilding.Maze)]
        [TestCase(BuiltInBuilding.InteractionRoom)]
        public void EveryBuiltInBuilding_RunsWithTheCrowdPanicked_WithoutAnException(BuiltInBuilding building)
        {
            // Eight seconds: long enough for forty people to reach the doors
            // and jam them; a whole minute would cost most on the square room
            // and prove nothing more about the building.
            using (var simulation = new Run(Built(building), 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.SetCrowdPanicked, default(SimulationId), 10);
                Assert.That(() =>
                {
                    for (int t = 0; t < 8 * Run.TicksPerSecond; t++)
                    {
                        simulation.Step();
                    }
                }, Throws.Nothing);
                Assert.That(simulation.GetSnapshot().CrowdHeldPanicked, Is.True);
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Running), "The switch begins the round.");
            }
        }

        [TestCase("Square", "square", BuiltInBuilding.SquareRoom, false, 40)]
        [TestCase("Maze", "maze", BuiltInBuilding.Maze, false, 11)]
        [TestCase("Interaction", "interaction", BuiltInBuilding.InteractionRoom, true, 8)]
        public void TheLevelAssets_NameTheBuildingsTheyClaim(string file, string levelId, BuiltInBuilding building,
            bool triggerStartsAHazard, int people)
        {
            string path = LevelsFolder + file + ".asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            Assert.That(level, Is.Not.Null, $"Missing {path}.");
            Assert.That(level.LevelId, Is.EqualTo(levelId));
            Assert.That(level.BuiltInBuilding, Is.EqualTo(building));
            Assert.That(level.OffersCrowdSwitch, Is.True, "Every test level has the Crowd button.");
            Assert.That(level.TriggerStartsAHazard, Is.EqualTo(triggerStartsAHazard));

            ScenarioData data = level.ToRuntimeData();
            Assert.That(data.Agents, Has.Length.EqualTo(people));
            Assert.That(data.Purse.Enabled, Is.True, "The test levels keep the purse, so the doors cost what they cost.");
            Assert.That(() => data.Validate(), Throws.Nothing);
        }

        [Test]
        public void TheMaze_HasOneWayOut_AndEveryCellIsReachableFromTheLeadersCell()
        {
            ScenarioData data = Built(BuiltInBuilding.Maze);
            using (var simulation = new Run(data, 42UL))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                int waysOut = 0;
                for (int d = 0; d < geometry.DoorCount; d++)
                {
                    if (geometry.DoorLeadsOutside(d))
                    {
                        waysOut++;
                    }
                }

                Assert.That(waysOut, Is.EqualTo(1), "A maze with two ways out is not a maze.");

                int cells = 0;
                foreach (string line in TestBuildings.MazePicture.Where((_, i) => i % 2 == 1))
                {
                    cells += line.Count(mark => mark == '.' || mark == 'L');
                }

                Assert.That(geometry.RoomCount, Is.EqualTo(cells), "Every cell in the picture is a room.");

                int start = geometry.RoomAt(data.Agents[0].InitialPosition);
                Assert.That(start, Is.GreaterThanOrEqualTo(0));
                var seen = new HashSet<int> { start };
                var queue = new Queue<int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int room = queue.Dequeue();
                    for (int d = 0; d < geometry.DoorCount; d++)
                    {
                        if (!geometry.DoorTouchesRoom(d, room) || !geometry.IsDoorOpen(d))
                        {
                            continue;
                        }

                        int beyond = geometry.RoomBeyond(d, room);
                        if (beyond >= 0 && seen.Add(beyond))
                        {
                            queue.Enqueue(beyond);
                        }
                    }
                }

                Assert.That(seen.Count, Is.EqualTo(geometry.RoomCount), "Every cell can be walked to through the archways; none is sealed off.");
            }
        }

        [Test]
        public void TheMaze_HasOneLeaderWhoKnowsTheBuilding_AndTenVisitors()
        {
            ScenarioData data = Built(BuiltInBuilding.Maze);
            AgentDefinition[] staff = data.Agents.Where(a => a.Familiarity == AgentFamiliarity.KnowsTheBuilding).ToArray();
            Assert.That(staff, Has.Length.EqualTo(1), "One person works here.");
            Assert.That(staff[0].Traits.Leadership, Is.GreaterThanOrEqualTo(9), "And is the strongest leader there is, so a rally gathers the room.");
            Assert.That(data.Agents.Count(a => a.Familiarity == AgentFamiliarity.Visitor), Is.EqualTo(10));
        }
    }
}

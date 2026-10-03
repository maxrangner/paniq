using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The cubicle landscape east of the crossbar (2026-10-02, the owner: "a
    /// new big room, on the other side of the T corridor, next to the exit. A
    /// large cubicle landscape"): three doors in its west wall, which make it
    /// the second way round when the junction or the arm to the way out is
    /// cut; low screens that are fixtures, not furniture; and fourteen people
    /// who work there.
    /// </summary>
    public sealed class CubicleLandscapeEditModeTests
    {
        private static readonly LogicalBounds TheLandscape = new LogicalBounds(16000, 28000, -3000, 17000);

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

        private static int RoomIndex(Run simulation, SimulationId room)
        {
            WorldGeometry geometry = simulation.GeometryForTests;
            for (int r = 0; r < geometry.RoomCount; r++)
            {
                if (geometry.RoomId(r) == room)
                {
                    return r;
                }
            }

            throw new KeyNotFoundException(room.ToString());
        }

        private static bool IsInTheLandscape(LogicalPosition where) =>
            where.X > TheLandscape.MinX && where.X < TheLandscape.MaxX && where.Z > TheLandscape.MinZ && where.Z < TheLandscape.MaxZ;

        /// <summary>
        /// The room's three doors, by where they lead: from its north aisle
        /// the way to the crossbar is the door beside the way out, from its
        /// south end the door opposite the stockroom's, and the stockroom
        /// itself is through the third. All shut and unlocked, like every
        /// door inside the building.
        /// </summary>
        [Test]
        public void TheLandscape_HasThreeDoors_TwoOntoTheCrossbarAndOneIntoTheStockroom()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            using (var simulation = new Run(data))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                int landscape = RoomIndex(simulation, PrototypeBuilding.CubicleLandscape);
                int crossbar = RoomIndex(simulation, PrototypeBuilding.Crossbar);
                int stockroom = RoomIndex(simulation, PrototypeBuilding.Stockroom);
                Agent walker = simulation.AgentForTests(0);

                Assert.That(geometry.TryFindRoute(landscape, new LogicalPosition(17000, 14000), crossbar, walker, out int north, out _, out _), Is.True);
                Assert.That(simulation.GetDoor(north).DoorId, Is.EqualTo(TheBuilding.CubicleDoorByTheWayOut),
                    "From the north of the west aisle, the crossbar is through the door beside the way out.");
                Assert.That(geometry.TryFindRoute(landscape, new LogicalPosition(17000, 1500), crossbar, walker, out int south, out _, out _), Is.True);
                Assert.That(simulation.GetDoor(south).DoorId, Is.EqualTo(TheBuilding.CubicleSouthDoor),
                    "From the south of it, through the door opposite the stockroom's.");
                Assert.That(geometry.TryFindRoute(landscape, new LogicalPosition(17000, -2000), stockroom, walker, out int stores, out _, out _), Is.True);
                Assert.That(simulation.GetDoor(stores).DoorId, Is.EqualTo(TheBuilding.CubicleToStockroom),
                    "And the stockroom's east lane is through the third.");

                foreach (int door in new[] { north, south, stores })
                {
                    DoorSnapshot snapshot = simulation.GetDoor(door);
                    Assert.That(snapshot.State, Is.EqualTo(DoorState.Unlocked), $"Door {snapshot.DoorId} starts shut and unlocked.");
                    Assert.That(snapshot.Side, Is.EqualTo(WallSide.West), $"Door {snapshot.DoorId} is in the landscape's west wall.");
                }
            }
        }

        /// <summary>
        /// The screens between the cubicles are fixtures: everybody in the
        /// building frightened and running for ten seconds, desks shoved and
        /// chairs sent rolling, and not one screen has moved.
        /// </summary>
        [Test]
        public void TheScreens_AreFixtures_AFrightenedCrowdShiftsNoneOfThem()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Timetable = Array.Empty<ScheduledCue>();
            data.Fire.ActivationTick = 5;
            data.Calming.Enabled = false;
            using (var simulation = new Run(data, 42UL))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                var stood = new Dictionary<int, LogicalBounds>();
                for (int t = 0; t < geometry.TableCount; t++)
                {
                    if (geometry.IsPartition(t))
                    {
                        stood[t] = geometry.TableBounds(t);
                    }
                }

                Assert.That(stood, Has.Count.EqualTo(22), "Two spines of three screens, and eight between neighbours on each.");

                for (int t = 0; t < 10; t++)
                {
                    simulation.Step();
                }

                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    simulation.FrightenForTests(i);
                }

                for (int t = 0; t < 10 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                foreach (KeyValuePair<int, LogicalBounds> screen in stood)
                {
                    LogicalBounds now = geometry.TableBounds(screen.Key);
                    Assert.That(now.MinX == screen.Value.MinX && now.MaxX == screen.Value.MaxX &&
                                now.MinZ == screen.Value.MinZ && now.MaxZ == screen.Value.MaxZ, Is.True,
                        $"Screen {screen.Key} stands where it was built.");
                }
            }
        }

        /// <summary>
        /// Fourteen people (the owner: "enough people for it to seem like
        /// it's a working office space"): twelve seated at desks whose
        /// chairs are their own, two on their feet at the coffee point, and
        /// every one of them works here, so they know the way out.
        /// </summary>
        [Test]
        public void TheLandscapesPeople_AreFourteen_TwelveAtTheirOwnDesks_AndAllWorkHere()
        {
            ScenarioData data = scenario.ToRuntimeData();
            int people = 0;
            int seatedAtTheirOwnDesk = 0;
            foreach (AgentDefinition person in data.Agents)
            {
                if (!IsInTheLandscape(person.InitialPosition))
                {
                    continue;
                }

                people++;
                Assert.That(person.Familiarity, Is.EqualTo(AgentFamiliarity.KnowsTheBuilding), $"Person {person.AgentId} works here.");
                if (person.StartsSeated)
                {
                    Assert.That(person.HomeObjectId, Is.EqualTo(person.SeatedOnObjectId), $"Person {person.AgentId} sits on their own chair.");
                    seatedAtTheirOwnDesk++;
                }
            }

            Assert.That(people, Is.EqualTo(14));
            Assert.That(seatedAtTheirOwnDesk, Is.EqualTo(12), "Twelve at their desks; the other two stand talking at the coffee point.");
            Assert.That(data.Agents, Has.Length.EqualTo(34), "With the twenty of the older rooms: thirty-four in the building.");
        }
    }
}

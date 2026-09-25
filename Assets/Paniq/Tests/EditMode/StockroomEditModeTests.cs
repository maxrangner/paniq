using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The stockroom behind the bathroom (2026-09-25): the office's second
    /// way to the way out, through a room full of boxes and up the crossbar's
    /// south arm. The owner asked for every room but the bathroom to have two
    /// ways out; the closet and the maintenance room are cupboards and the
    /// stalls are stalls, so they keep their one door.
    /// <para>
    /// Loose things are not on the map people steer by -- they only dodge a
    /// box when they reach it -- so whether the lane through the stores is
    /// really walkable can only be proven by walking it.
    /// </para>
    /// </summary>
    public sealed class StockroomEditModeTests
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

        private static int RoomIndex(ScenarioData data, SimulationId room)
        {
            return System.Array.FindIndex(data.Rooms, r => r.RoomId == room);
        }

        [Test]
        public void EveryRoomButTheBathroomAndTheCupboards_HasTwoWaysOut()
        {
            ScenarioData data = scenario.ToRuntimeData();
            var cupboards = new HashSet<SimulationId>
            {
                PrototypeBuilding.Closet, PrototypeBuilding.Maintenance,
                PrototypeBuilding.StallOne, PrototypeBuilding.StallTwo, PrototypeBuilding.StallThree
            };
            using (var simulation = new Run(data))
            {
                WorldGeometry geometry = simulation.GeometryForTests;
                var doorsOf = new int[data.Rooms.Length];
                for (int d = 0; d < simulation.DoorCount; d++)
                {
                    int room = geometry.DoorRoom(d);
                    doorsOf[room]++;
                    int beyond = geometry.RoomBeyond(d, room);
                    if (beyond >= 0)
                    {
                        doorsOf[beyond]++;
                    }
                }

                for (int r = 0; r < data.Rooms.Length; r++)
                {
                    if (data.Rooms[r].RoomId == PrototypeBuilding.Bathroom)
                    {
                        // One door onto the corridor and three into its stalls,
                        // which are no way out of anywhere.
                        Assert.That(doorsOf[r], Is.EqualTo(4), "The bathroom keeps its one way out; the owner said so.");
                    }
                    else if (cupboards.Contains(data.Rooms[r].RoomId))
                    {
                        Assert.That(doorsOf[r], Is.EqualTo(1), $"{data.Rooms[r].RoomId} is a cupboard with one door.");
                    }
                    else
                    {
                        Assert.That(doorsOf[r], Is.GreaterThanOrEqualTo(2), $"{data.Rooms[r].RoomId} has only one way out.");
                    }
                }
            }
        }

        /// <summary>
        /// The fire is at the office's corridor door, so the way north is
        /// through the heat. Somebody in the office's south-east corner takes
        /// the other way: through the stockroom door, along the lane between
        /// the boxes, and out into the crossbar under the way out.
        /// </summary>
        [Test]
        public void WithTheCorridorDoorInTheHeat_TheOfficeLeavesThroughTheStockroom()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), TheBuilding.OfficeSouthEastCorner, CardinalDirection.North,
                    new AgentTraitValues(5, 5, 5, 5, 2, 5, 5))
            };
            data.Alarms = new AlarmDefinition[0];
            data.Timetable = new ScheduledCue[0];
            data.Day.ToiletEveryTicks = 0;
            data.Fire.ActivationTick = 3;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            TheBuilding.FireAt(data, new LogicalPosition(250, 4750));
            int stockroom = RoomIndex(data, PrototypeBuilding.Stockroom);
            int crossbar = RoomIndex(data, PrototypeBuilding.Crossbar);
            using (var simulation = new Run(data, 7UL))
            {
                bool wentThroughTheStores = false;
                int reachedTheCrossbarAt = -1;
                for (int t = 0; t < 40 * Run.TicksPerSecond && reachedTheCrossbarAt < 0; t++)
                {
                    simulation.Step();
                    int room = simulation.GeometryForTests.RoomAt(simulation.GetAgent(0).Position);
                    wentThroughTheStores |= room == stockroom;
                    if (room == crossbar)
                    {
                        reachedTheCrossbarAt = t;
                    }
                }

                Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm), "They saw the fire.");
                Assert.That(wentThroughTheStores, Is.True, "They should have left through the stockroom, not past the flames.\n" + simulation.DescribeForTests(0));
                Assert.That(reachedTheCrossbarAt, Is.GreaterThan(0), "The lane through the boxes should bring them out into the crossbar.\n" + simulation.DescribeForTests(0));
            }
        }
    }
}

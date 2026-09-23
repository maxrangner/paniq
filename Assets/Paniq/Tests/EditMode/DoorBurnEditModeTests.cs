using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Fire and shut doors. A shut door holds the flames off, but only for a
    /// while: stand in them long enough and it burns through, and the fire
    /// comes on.
    /// <para>
    /// It used to stop fire for good, which meant a building with its doors
    /// closed had rooms nothing could ever reach. Shutting yourself in was a
    /// way to win rather than a way to buy time, and a round could be called
    /// finished with half the building untouched.
    /// </para>
    /// </summary>
    public sealed class DoorBurnEditModeTests
    {
        /// <summary>The office's east door, into the storage closet.</summary>
        private static readonly SimulationId ClosetDoor = new SimulationId(2002UL);

        /// <summary>The storage closet itself.</summary>
        private static readonly SimulationId Closet = new SimulationId(5002UL);

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

        /// <summary>
        /// One fire that never spreads, sitting right against the closet door,
        /// and one person shut in the meeting room at the far end of the
        /// building with nothing to do. A scenario has to have somebody in it;
        /// this is as close to nobody as it can get, so what the tests watch is
        /// the door and the flames and nothing else.
        /// </summary>
        private ScenarioData FireAgainstTheClosetDoor()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), TheBuilding.MeetingRoom,
                    CardinalDirection.East, AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Fire.ActivationTick = 1;

            // Just inside the office, up against the closet doorway.
            data.Fire.SpawnBounds = new LogicalBounds(5500, 5500, 2500, 2500);
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;
            return data;
        }

        private static DoorSnapshot DoorOf(Run simulation, SimulationId door)
        {
            for (int i = 0; i < simulation.DoorCount; i++)
            {
                if (simulation.GetDoor(i).DoorId == door)
                {
                    return simulation.GetDoor(i);
                }
            }

            throw new KeyNotFoundException(door.ToString());
        }

        [Test]
        public void AShutDoorInTheFlames_HoldsAndThenBurnsThrough()
        {
            ScenarioData data = FireAgainstTheClosetDoor();
            int through = data.Exits.DoorBurnThroughTicks;
            using (var simulation = new Run(data))
            {
                // Most of the way there: it is charring, but it is still a door.
                for (int t = 0; t < through - 20; t++)
                {
                    simulation.Step();
                }

                DoorSnapshot charring = DoorOf(simulation, ClosetDoor);
                Assert.That(charring.State, Is.EqualTo(DoorState.Unlocked),
                    "Eighteen seconds is the point, and this is short of it.");
                Assert.That(charring.ScorchPercent, Is.GreaterThan(50),
                    "It should be visibly charring well before it goes.");
                Assert.That(charring.DamagePercent, Is.Zero, "Nobody laid a shoulder on it.");
                Assert.That(charring.FailingPercent, Is.EqualTo(charring.ScorchPercent),
                    "Burning is what is wrong with it, so that is what the drawing should follow.");

                for (int t = 0; t < 40; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Broken),
                    "Once it has stood in the flames long enough it should be gone.");
            }
        }

        [Test]
        public void ADoorThatBurnsThrough_IsWrittenDownWithTheFireAsItsCause()
        {
            ScenarioData data = FireAgainstTheClosetDoor();
            using (var simulation = new Run(data))
            {
                for (int t = 0; t < data.Exits.DoorBurnThroughTicks + 20; t++)
                {
                    simulation.Step();
                }

                CausalEvent burnt = default;
                bool found = false;
                foreach (CausalEvent record in simulation.EventLog.Events)
                {
                    if (record.EventType == CausalEventType.DoorBurntThrough)
                    {
                        burnt = record;
                        found = true;
                    }
                }

                Assert.That(found, Is.True, "Burning through a door is something that happened, so it goes in the log.");
                Assert.That(burnt.TargetId, Is.EqualTo(ClosetDoor), "The event names the door.");
                Assert.That(burnt.HasCausalParent, Is.True, "And what ate it.");
                CausalEventType cause = simulation.EventLog.Get(burnt.CausalParentEventId).EventType;
                Assert.That(cause == CausalEventType.FireActivated || cause == CausalEventType.FireSpread,
                    Is.True, $"Its cause should be the burning square that reached it, but it was {cause}.");
            }
        }

        [Test]
        public void OnceItHasBurntThrough_TheFireCanGetIntoTheRoomBeyond()
        {
            ScenarioData data = FireAgainstTheClosetDoor();

            // The fire starts inside the storage closet instead, and spreads.
            // The closet is two metres square, so once its handful of squares
            // are alight the only place left for the fire to go is out through
            // the doorway -- which makes the shut door the only thing deciding
            // whether it can, and that is what this test is about.
            data.Rooms = ClosetFirst(data.Rooms);
            data.Fire.SpawnBounds = new LogicalBounds(6500, 6500, 2500, 2500);
            data.Fire.SpreadMinimumTicks = 15;
            data.Fire.SpreadMaximumTicks = 15;
            using (var simulation = new Run(data))
            {
                for (int t = 0; t < data.Exits.DoorBurnThroughTicks - 50; t++)
                {
                    simulation.Step();
                }

                Assert.That(DoorOf(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Unlocked),
                    "The door should still be holding at this point.");
                Assert.That(BurningInTheOffice(simulation), Is.False,
                    "While the door holds, the fire should be stuck in the closet.");

                for (int t = 0; t < 120 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                    if (BurningInTheOffice(simulation))
                    {
                        Assert.That(DoorOf(simulation, ClosetDoor).State, Is.EqualTo(DoorState.Broken),
                            "The only way out of the closet was the door it burnt through.");
                        return;
                    }
                }

                Assert.Fail("The fire never got out of the closet, so a shut door is still a firebreak for ever.");
            }
        }

        /// <summary>An open door has no leaf standing in the flames, so there is nothing to eat.</summary>
        [Test]
        public void AnOpenDoor_NeverChars()
        {
            ScenarioData data = FireAgainstTheClosetDoor();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, ClosetDoor, 1);
                for (int t = 0; t < data.Exits.DoorBurnThroughTicks * 2; t++)
                {
                    simulation.Step();
                }

                DoorSnapshot door = DoorOf(simulation, ClosetDoor);
                Assert.That(door.State, Is.EqualTo(DoorState.Open), "It was standing open the whole time.");
                Assert.That(door.ScorchPercent, Is.Zero, "Nothing was in the way of the flames to burn.");
            }
        }

        /// <summary>A door nowhere near the flames is left alone however long the fire burns.</summary>
        [Test]
        public void ADoorAcrossTheBuilding_IsLeftAlone()
        {
            ScenarioData data = FireAgainstTheClosetDoor();
            using (var simulation = new Run(data))
            {
                for (int t = 0; t < data.Exits.DoorBurnThroughTicks * 2; t++)
                {
                    simulation.Step();
                }

                // The way out of the building, at the far end of the meeting room.
                DoorSnapshot far = DoorOf(simulation, new SimulationId(2008UL));
                Assert.That(far.State, Is.EqualTo(DoorState.Locked));
                Assert.That(far.ScorchPercent, Is.Zero);
            }
        }

        /// <summary>
        /// The same rooms with the storage closet moved to the front, because a
        /// scenario's fire starts in the room it lists first. Doors name their
        /// room by ID, so the order makes no difference to anything else.
        /// </summary>
        private static RoomDefinition[] ClosetFirst(RoomDefinition[] rooms)
        {
            var reordered = new RoomDefinition[rooms.Length];
            int next = 1;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].RoomId == Closet)
                {
                    reordered[0] = rooms[i];
                }
                else
                {
                    reordered[next++] = rooms[i];
                }
            }

            Assert.That(reordered[0].RoomId, Is.EqualTo(Closet), "The building should have a storage closet in it.");
            return reordered;
        }

        /// <summary>Anything alight in the open-plan office, which is 12 m square around the origin.</summary>
        private static bool BurningInTheOffice(Run simulation)
        {
            foreach (FireCellSnapshot cell in simulation.GetSnapshot().FireCells)
            {
                if (cell.Centre.X > -6000 && cell.Centre.X < 6000 &&
                    cell.Centre.Z > -6000 && cell.Centre.Z < 6000)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// TNT. The player picks a wall and blows a ragged hole through it: wider
    /// than a door, permanently open, and impossible to lock, shut or batter,
    /// because it is not a door. The bang throws loose things away from it and
    /// knocks anybody nearby off their feet. A hole in an outside wall is a new
    /// way out of the building that nobody can take away.
    /// </summary>
    public sealed class BlastEditModeTests
    {
        private static readonly SimulationId Somebody = new SimulationId(1UL);
        private static readonly SimulationId TheBox = new SimulationId(3001UL);

        /// <summary>A point against the middle of the office's south wall, from the inside.</summary>
        private static readonly LogicalPosition SouthWall = new LogicalPosition(0, -5900);

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

        /// <summary>A quiet office with one person in it, no fire due, and nothing loose.</summary>
        private ScenarioData QuietOffice()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(0, 0), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Fire.ActivationTick = int.MaxValue;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Purse.Starting = 1000;
            data.Purse.Maximum = 1000;
            return data;
        }

        private static void Blast(Run simulation, LogicalPosition where)
        {
            simulation.QueueCommand(PlayerCommandType.BlastWall, where, simulation.Tick + 1);
            simulation.Step();
        }

        [Test]
        public void TNT_MakesAHoleInTheWallThePlayerPointsAt()
        {
            ScenarioData data = QuietOffice();
            var simulation = new Run(data);
            int openingsBefore = simulation.DoorCount;
            int chargesBefore = simulation.BlastChargesRemaining;
            Assert.That(chargesBefore, Is.GreaterThan(0), "The player should start with some TNT.");

            Blast(simulation, SouthWall);

            Assert.That(simulation.DoorCount, Is.EqualTo(openingsBefore + 1), "There should be one more way through.");
            Assert.That(simulation.BlastChargesRemaining, Is.EqualTo(chargesBefore - 1), "It should cost a charge.");
            Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting - data.Purse.CardCost));

            List<CausalEvent> blasted = EventsOfType(simulation, CausalEventType.PowerBlastedWall);
            Assert.That(blasted, Is.Not.Empty);
            Assert.That(blasted[0].CausalParentEventId, Is.Zero, "The player is the cause, so it is a root event.");

            DoorSnapshot hole = simulation.GetDoor(simulation.DoorCount - 1);
            Assert.That(hole.State, Is.EqualTo(DoorState.Broken), "A hole is permanently open.");
            Assert.That(hole.IsHole, Is.True);
            Assert.That(hole.WidthMillimetres, Is.EqualTo(data.Blast.HoleWidthMillimetres),
                "A hole is wider than a door.");
        }

        [Test]
        public void AHoleInAnOutsideWall_IsANewWayOut()
        {
            ScenarioData data = QuietOffice();

            // Frightened, and every authored way out is locked.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, 1500, 1500);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new Run(data);
            Blast(simulation, SouthWall);

            for (int t = 0; t < 30 * Run.TicksPerSecond &&
                            simulation.GetAgent(0).Outcome != AgentTerminalOutcome.Escaped; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                "They should have got out through the hole.");
            List<CausalEvent> escaped = EventsOfType(simulation, CausalEventType.AgentEscaped);
            Assert.That(escaped, Is.Not.Empty);
            Assert.That(simulation.EventLog.Get(escaped[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.PowerBlastedWall),
                "Getting out through a hole should name the blast as the reason.");
        }

        [Test]
        public void NobodyEverTriesToOpenShutOrBatterAHole()
        {
            ScenarioData data = QuietOffice();
            data.Agents = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()).Agents;
            data.PhysicsObjects = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()).PhysicsObjects;
            data.Tables = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()).Tables;
            data.Fire.ActivationTick = 250;
            var simulation = new Run(data, 42UL);
            Blast(simulation, SouthWall);
            SimulationId holeId = simulation.GetDoor(simulation.DoorCount - 1).DoorId;

            for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            foreach (CausalEvent record in simulation.EventLog.Events)
            {
                if (record.TargetId != holeId)
                {
                    continue;
                }

                Assert.That(record.EventType, Is.Not.EqualTo(CausalEventType.AgentTriedDoor));
                Assert.That(record.EventType, Is.Not.EqualTo(CausalEventType.AgentForcedDoor));
                Assert.That(record.EventType, Is.Not.EqualTo(CausalEventType.DoorClosed));
                Assert.That(record.EventType, Is.Not.EqualTo(CausalEventType.DoorLocked));
                Assert.That(record.EventType, Is.Not.EqualTo(CausalEventType.DoorBrokenDown));
            }

            // And a click on it does nothing at all.
            simulation.QueueCommand(PlayerCommandType.ClickDoor, holeId, simulation.Tick + 1);
            simulation.Step();
            Assert.That(simulation.GetDoor(simulation.DoorCount - 1).State, Is.EqualTo(DoorState.Broken));
        }

        [Test]
        public void TheBlast_ThrowsLooseThingsAwayFromItAndKnocksPeopleOver()
        {
            ScenarioData data = QuietOffice();
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(0, -4900), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheBox, PhysicsObjectKind.Box, new LogicalPosition(800, -4600), 400, 5000)
            };
            var simulation = new Run(data);
            LogicalPosition boxBefore = simulation.GetPhysicsObject(0).Position;

            Blast(simulation, SouthWall);
            for (int t = 0; t < Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.GetPhysicsObject(0).Position, Is.Not.EqualTo(boxBefore),
                "The blast should have flung the box.");

            List<CausalEvent> down = EventsOfType(simulation, CausalEventType.AgentKnockedDown);
            Assert.That(down, Is.Not.Empty, "Somebody a metre from the wall should be knocked over.");
            Assert.That(simulation.EventLog.Get(down[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.PowerBlastedWall));
        }

        [Test]
        public void BlastStrength_ScalesHowFarPeopleAreThrown()
        {
            long ThrownMillimetres(int strengthPercent)
            {
                ScenarioData data = QuietOffice();
                data.Agents = new[]
                {
                    new AgentDefinition(Somebody, new LogicalPosition(0, -4900), CardinalDirection.North,
                        AgentTraitValues.AllOrdinary)
                };
                data.PhysicsFeel.BlastStrengthPercent = strengthPercent;
                var simulation = new Run(data);
                LogicalPosition before = simulation.GetAgent(0).Position;
                Blast(simulation, SouthWall);
                for (int t = 0; t < Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                return IntegerMath.Sqrt(LogicalPosition.DistanceSquared(before, simulation.GetAgent(0).Position));
            }

            long full = ThrownMillimetres(100);
            long weak = ThrownMillimetres(40);
            Assert.That(full, Is.GreaterThan(300), "A full blast a metre away should throw somebody.");
            Assert.That(weak, Is.LessThan(full * 4 / 5),
                $"A blast at 40% should throw them less far ({weak} mm against {full} mm).");
        }

        [Test]
        public void TheBang_FrightensPeopleWhoCouldNotHaveSeenAnything()
        {
            ScenarioData data = QuietOffice();

            // Right across the office with their back to the wall, and no fire
            // anywhere in this scenario, so the only thing that could frighten
            // them is the bang.
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(0, 4000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };
            var simulation = new Run(data);
            Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm));

            Blast(simulation, SouthWall);
            simulation.Step();

            Assert.That(simulation.GetAgent(0).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                "A bang that big is heard right across the room.");
            Assert.That(simulation.FireActive, Is.False, "There was never any fire to see.");
        }

        [Test]
        public void TNT_IsRefusedWhereThereIsNoWallNearby()
        {
            ScenarioData data = QuietOffice();
            var simulation = new Run(data);
            int before = simulation.DoorCount;

            // The middle of the room, nowhere near a wall.
            Blast(simulation, new LogicalPosition(0, 0));

            Assert.That(simulation.DoorCount, Is.EqualTo(before), "No wall, no hole.");
            Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting), "And no charge spent.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerBlastedWall), Is.Empty);
        }

        [Test]
        public void TNT_IsRefusedWhereItWouldOverlapADoorThatIsAlreadyThere()
        {
            ScenarioData data = QuietOffice();
            var simulation = new Run(data);
            int before = simulation.DoorCount;

            // The office's door onto the corridor is centred on x = 0 in its
            // north wall at z = 6000.
            Blast(simulation, new LogicalPosition(0, 5900));

            Assert.That(simulation.DoorCount, Is.EqualTo(before), "A hole cannot be cut through a doorway.");
            Assert.That(simulation.Purse, Is.EqualTo(data.Purse.Starting));
        }

        [Test]
        public void TNT_RunsOutOfCharges()
        {
            ScenarioData data = QuietOffice();
            var simulation = new Run(data);
            int charges = simulation.BlastChargesRemaining;

            // Along the south wall, well spaced, plus two more than there are charges.
            for (int i = 0; i < charges + 2; i++)
            {
                Blast(simulation, new LogicalPosition(-5000 + i * 2200, -5900));
            }

            Assert.That(simulation.BlastChargesRemaining, Is.Zero);
            Assert.That(EventsOfType(simulation, CausalEventType.PowerBlastedWall).Count, Is.EqualTo(charges),
                "Once the TNT runs out the card does nothing.");
        }

        [Test]
        public void AHoleBetweenTwoRooms_LetsFireCross()
        {
            // Two big rooms sharing one long wall, with the only door between them
            // locked at the far end. The default building has no interior wall long
            // enough to take a hole clear of its door, so this needs its own plan.
            ScenarioData data = QuietOffice();
            var west = new SimulationId(5101UL);
            var east = new SimulationId(5102UL);
            data.Timetable = System.Array.Empty<ScheduledCue>();
            data.Rooms = new[]
            {
                new RoomDefinition(west, new LogicalBounds(-6000, 0, -6000, 6000)),
                new RoomDefinition(east, new LogicalBounds(0, 6000, -6000, 6000))
            };
            data.Doors = new[]
            {
                new DoorDefinition(new SimulationId(2001UL), west, WallSide.East, 5000, 1000)
            };
            data.Agents = new[]
            {
                new AgentDefinition(Somebody, new LogicalPosition(-5000, -5000), CardinalDirection.North,
                    AgentTraitValues.AllOrdinary)
            };

            // A fire in the west room, hard against the shared wall.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-250, -250, -2000, -2000);
            data.Fire.SpreadMinimumTicks = 10;
            data.Fire.SpreadMaximumTicks = 20;
            var simulation = new Run(data);
            int roomsAlightBefore = 0;

            // Blast the shared wall, well away from the door at the far end.
            Blast(simulation, new LogicalPosition(100, -2000));
            Assert.That(EventsOfType(simulation, CausalEventType.PowerBlastedWall), Is.Not.Empty,
                "The wall between the two rooms should have opened.");

            DoorSnapshot hole = simulation.GetDoor(simulation.DoorCount - 1);
            Assert.That(hole.LeadsOutside, Is.False, "A hole in a shared wall joins the two rooms.");

            for (int t = 0; t < 60 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.FireCellCount, Is.GreaterThan(roomsAlightBefore + 1),
                "The fire should have spread through the hole.");
        }

        /// <summary>
        /// The pool of spare openings must be completely inert until a charge is
        /// spent, or it would quietly change every run: an unplaced slot would
        /// read as a door leading outside, and people would score it and draw
        /// random numbers for it.
        /// </summary>
        [TestCase(42UL)]
        [TestCase(40UL)]
        [TestCase(46UL)]
        public void AnUnusedPoolOfTNT_ChangesNothingAtAll(ulong seed)
        {
            ulong WithPool(int holes)
            {
                ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
                var ids = new SimulationId[holes];
                for (int i = 0; i < holes; i++)
                {
                    ids[i] = new SimulationId(2901UL + (ulong)i);
                }

                data.BlastHoles = ids;
                var simulation = new Run(data, seed);
                for (int t = 0; t < 1500; t++)
                {
                    simulation.Step();
                }

                return simulation.Random.State;
            }

            Assert.That(WithPool(4), Is.EqualTo(WithPool(0)),
                "Spare openings nobody has used must not touch the run at all.");
        }
    }
}

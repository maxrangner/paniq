using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The frightened walk through doors (2026-09-27): somebody going for a
    /// bottle, for the flames or for a pull station opens the shut doors on
    /// the way, as a calm errand does. They used to walk into a shut door,
    /// push at it for a second, give up, pick the bottle again and walk into
    /// it again (the owner: "agents don't seem to go through closed doors
    /// with a fire extinguisher").
    /// </summary>
    public sealed class FrightenedWalksEditModeTests
    {
        private static readonly SimulationId WhereTheFireIs = new SimulationId(5001UL);
        private static readonly SimulationId NextDoor = new SimulationId(5002UL);
        private static readonly SimulationId BetweenThem = new SimulationId(2001UL);
        private static readonly SimulationId TheWayOut = new SimulationId(2002UL);
        private static readonly SimulationId TheBottle = new SimulationId(3001UL);
        private static readonly SimulationId TheOneWhoShouts = new SimulationId(1UL);
        private static readonly SimulationId TheBraveOne = new SimulationId(2UL);

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

        private static CausalEvent? AdvanceUntil(Run simulation, CausalEventType type, int limit)
        {
            for (int t = 0; t < limit; t++)
            {
                List<CausalEvent> found = EventsOfType(simulation, type);
                if (found.Count > 0)
                {
                    return found[0];
                }

                simulation.Step();
            }

            List<CausalEvent> last = EventsOfType(simulation, type);
            return last.Count > 0 ? last[0] : (CausalEvent?)null;
        }

        /// <summary>
        /// Two rooms joined by a door that starts shut (unlocked unless told
        /// otherwise). The fire is in the first, with somebody who shouts so
        /// the whole building hears; the brave one and the bottle go where
        /// each test puts them.
        /// </summary>
        private ScenarioData TwoRooms(bool locked, LogicalPosition braveOne, LogicalPosition bottle)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.BlastHoles = new SimulationId[0];
            data.ExitSigns = System.Array.Empty<ExitSignDefinition>();
            data.Timetable = System.Array.Empty<ScheduledCue>();
            data.Rooms = new[]
            {
                new RoomDefinition(WhereTheFireIs, new LogicalBounds(-6000, 6000, -6000, 6000)),
                new RoomDefinition(NextDoor, new LogicalBounds(6000, 18000, -6000, 6000))
            };
            data.Doors = new[]
            {
                new DoorDefinition(BetweenThem, WhereTheFireIs, WallSide.East, 0, 1000, locked),
                new DoorDefinition(TheWayOut, WhereTheFireIs, WallSide.West, 0, 1000, false)
            };
            data.PhysicsObjects = new[]
            {
                new PhysicsObjectDefinition(TheBottle, PhysicsObjectKind.Extinguisher, bottle, 300, 9000)
            };
            data.Agents = new[]
            {
                new AgentDefinition(TheOneWhoShouts, new LogicalPosition(-3000, 1500), CardinalDirection.South, AgentTraitValues.AllOrdinary),
                new AgentDefinition(TheBraveOne, braveOne, CardinalDirection.West,
                    new AgentTraitValues(AgentTraitValues.Ordinary, AgentTraitValues.Ordinary, AgentTraitValues.Maximum,
                        AgentTraitValues.Ordinary, AgentTraitValues.Minimum, AgentTraitValues.Minimum))
            };
            data.Fire.ActivationTick = 10;
            data.Fire.SpawnBounds = new LogicalBounds(-3000, -3000, 0, 0);
            data.Hearing.YellAlarmRadiusMillimetres = 40000;
            data.Hearing.YellHearingRadiusMillimetres = 40000;
            data.Fire.SpreadMinimumTicks = 1000000;
            data.Fire.SpreadMaximumTicks = 1000000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            return data;
        }

        private static bool OpenedBy(Run simulation, SimulationId door, SimulationId person)
        {
            var byId = new Dictionary<ulong, CausalEvent>();
            foreach (CausalEvent e in simulation.EventLog.Events)
            {
                byId[e.EventId] = e;
            }

            foreach (CausalEvent opened in EventsOfType(simulation, CausalEventType.DoorOpened))
            {
                if (opened.SourceId != door)
                {
                    continue;
                }

                ulong at = opened.CausalParentEventId;
                for (int hops = 0; hops < 10 && at != 0UL && byId.TryGetValue(at, out CausalEvent cause); hops++)
                {
                    if (cause.SourceId == person)
                    {
                        return true;
                    }

                    at = cause.CausalParentEventId;
                }
            }

            return false;
        }

        [Ignore("Set aside 2026-10-03 (level mode): the brave no longer fight the fire unasked. Re-aim at a hand on the bottle in the hardening pass.")]
        [Test]
        public void ABravePersonWithTheBottle_OpensTheShutDoor_ToGetToTheFire()
        {
            ScenarioData data = TwoRooms(false, new LogicalPosition(12000, 1500), new LogicalPosition(12000, 0));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 5);
                CausalEvent? sprayed = AdvanceUntil(simulation, CausalEventType.ExtinguisherSprayed, 60 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened).Exists(e => e.SourceId == BetweenThem), Is.True,
                    "The door between the rooms was opened, without the player touching it.");
                Assert.That(OpenedBy(simulation, BetweenThem, TheBraveOne), Is.True, "By the brave one, on their way to the fire.");
                Assert.That(sprayed.HasValue, "And the fire was fought.");
            }
        }

        [Ignore("Set aside 2026-10-03 (level mode): the brave no longer fight the fire unasked. Re-aim at a hand on the bottle in the hardening pass.")]
        [Test]
        public void ABravePersonBesideTheFire_OpensTheShutDoor_ToFetchTheBottle()
        {
            ScenarioData data = TwoRooms(false, new LogicalPosition(2000, 3000), new LogicalPosition(9000, 0));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 5);
                CausalEvent? took = AdvanceUntil(simulation, CausalEventType.AgentTookExtinguisher, 40 * Run.TicksPerSecond);
                Assert.That(took.HasValue, "They went next door for the bottle.");
                Assert.That(took.Value.SourceId, Is.EqualTo(TheBraveOne));
                Assert.That(OpenedBy(simulation, BetweenThem, TheBraveOne), Is.True, "Opening the door on the way.");
                CausalEvent? sprayed = AdvanceUntil(simulation, CausalEventType.ExtinguisherSprayed, 40 * Run.TicksPerSecond);
                Assert.That(sprayed.HasValue, "And came back through it to the fire.");
            }
        }

        [Ignore("Set aside 2026-10-03 (level mode): the brave no longer fight the fire unasked. Re-aim at a hand on the bottle in the hardening pass.")]
        [Test]
        public void ALockedDoor_IsTriedOnce_AndTheBottleBehindItIsGivenUp()
        {
            ScenarioData data = TwoRooms(true, new LogicalPosition(2000, 3000), new LogicalPosition(9000, 0));
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 5);
                for (int t = 0; t < 30 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                Assert.That(EventsOfType(simulation, CausalEventType.DoorOpened).Exists(e => e.SourceId == BetweenThem), Is.False, "Locked stays locked.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentTookExtinguisher), Is.Empty, "The bottle behind it is never fetched.");
                List<CausalEvent> tried = EventsOfType(simulation, CausalEventType.AgentTriedDoor);
                Assert.That(tried.FindAll(e => e.SourceId == TheBraveOne && e.TargetId == BetweenThem).Count, Is.InRange(1, 3),
                    "Tried, given up, and not walked into for the rest of the round.");
            }
        }
    }
}

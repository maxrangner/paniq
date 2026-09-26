using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The cable through the walls, which runs one way (2026-09-26): when the
    /// fuse box goes, a spark races out along the cable and every socket down
    /// the line pops in turn. A socket going off by itself lights nothing, so
    /// nothing ever climbs back up to the fuse box.
    /// </summary>
    public sealed class PowerSystemEditModeTests
    {
        private static readonly SimulationId OfficeSocket = new SimulationId(3271UL);
        private static readonly SimulationId FarSocket = new SimulationId(3272UL);
        private static readonly SimulationId TheFuseBox = new SimulationId(3281UL);

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

        /// <summary>A quiet building: nobody much about, nothing burning, just the cable.</summary>
        private ScenarioData Quiet()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), TheBuilding.MeetingRoom,
                    CardinalDirection.North, AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Purse.Starting = 500;
            data.Purse.Maximum = 500;
            return data;
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

        private static void Advance(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>The building is wired: every socket and the fuse box on one chain.</summary>
        [Test]
        public void TheDefaultBuilding_HasCableJoiningItsSocketsToTheFuseBox()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            Assert.That(data.PowerLines, Has.Length.EqualTo(3),
                "Three runs of cable: the fuse box to the first socket, and on down the chain.");
            foreach (PowerLineDefinition line in data.PowerLines)
            {
                Assert.That(line.LengthMillimetres, Is.GreaterThan(0), "A run of cable has to go somewhere.");
                Assert.That(line.Corners, Has.Length.GreaterThanOrEqualTo(2));
            }
        }

        /// <summary>
        /// The card pops the fuse box, and the spark then runs the other way:
        /// out of the maintenance room and along the line of sockets.
        /// </summary>
        [Test]
        public void TheCard_PopsTheFuseBoxAndSendsASparkOutAlongTheCable()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            LogicalPosition box = FuseBoxPosition(simulation);
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, 1);
            Advance(simulation, 3);

            Assert.That(EventsOfType(simulation, CausalEventType.PowerPoppedFuseBox), Has.Count.EqualTo(1));
            Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.True);
            Assert.That(EventsOfType(simulation, CausalEventType.PowerSparkStarted), Is.Not.Empty,
                "Popping the box should light the cable leaving it.");
        }

        /// <summary>It costs what the card says, and only once.</summary>
        [Test]
        public void PoppingTheFuseBox_CostsItsPriceOnce()
        {
            ScenarioData data = Quiet();
            int price = data.Purse.CardCost;
            var simulation = new Run(data);
            LogicalPosition box = FuseBoxPosition(simulation);
            int before = simulation.Purse;

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, 1);
            Advance(simulation, 2);
            Assert.That(simulation.Purse, Is.EqualTo(before - price));

            // A second card on a box that has already gone does nothing at all.
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, simulation.Tick + 1);
            Advance(simulation, 3);
            Assert.That(simulation.Purse, Is.EqualTo(before - price), "A refused card is free.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerPoppedFuseBox), Has.Count.EqualTo(1));
        }

        /// <summary>Aimed at nothing in particular, the card is refused and costs nothing.</summary>
        [Test]
        public void TheCardPlayedNowhereNearTheFuseBox_IsRefusedAndFree()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            int before = simulation.Purse;

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, TheBuilding.Cafeteria, 1);
            Advance(simulation, 3);

            Assert.That(simulation.Purse, Is.EqualTo(before), "Nothing happened, so nothing was spent.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerPoppedFuseBox), Is.Empty,
                "The log should not record something that did not happen.");
            Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.False);
        }

        /// <summary>Too poor to play it: refused, and the box is untouched.</summary>
        [Test]
        public void WithAnEmptyPurse_TheCardIsRefused()
        {
            ScenarioData data = Quiet();
            data.Purse.Starting = 1;
            data.Purse.Maximum = 1;
            var simulation = new Run(data);
            LogicalPosition box = FuseBoxPosition(simulation);

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, 1);
            Advance(simulation, 3);

            Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.False);
            Assert.That(simulation.Purse, Is.EqualTo(1));
        }

        /// <summary>
        /// The spark takes as long as the cable is long. A run measured in
        /// millimetres, crawled at a fixed speed, arrives when arithmetic says
        /// it should -- and the event it logs says so in advance.
        /// </summary>
        [Test]
        public void ASpark_ArrivesWhenTheLengthOfTheCableSaysItShould()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, FuseBoxPosition(simulation), 1);
            Advance(simulation, 2);

            List<CausalEvent> started = EventsOfType(simulation, CausalEventType.PowerSparkStarted);
            Assert.That(started, Is.Not.Empty);
            CausalEvent first = started[0];
            int predicted = first.DurationTicks;
            Assert.That(predicted, Is.GreaterThan(0), "The spark should say how long it will take.");

            Advance(simulation, predicted + 5);
            List<CausalEvent> arrived = EventsOfType(simulation, CausalEventType.PowerSparkArrived);
            Assert.That(arrived, Is.Not.Empty, "The spark never got there.");
            Assert.That(arrived[0].Tick - first.Tick, Is.EqualTo(predicted).Within(2),
                "It arrived at a different time from the one it predicted.");
        }

        /// <summary>
        /// Left alone, the whole chain goes: the spark reaches a socket, that
        /// socket pops, and the cable beyond it lights in turn.
        /// </summary>
        [Test]
        public void LeftAlone_TheWholeChainGoesOffOneAfterAnother()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, FuseBoxPosition(simulation), 1);
            Advance(simulation, 60 * Run.TicksPerSecond);

            var went = new HashSet<ulong>();
            foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.ObjectExploded))
            {
                went.Add(record.SourceId.Value);
            }

            Assert.That(went, Does.Contain(TheFuseBox.Value), "The fuse box itself.");
            Assert.That(went, Does.Contain(OfficeSocket.Value), "The first socket down the chain.");
            Assert.That(went, Does.Contain(FarSocket.Value), "And the one after it.");
        }

        /// <summary>
        /// Nothing goes off twice. A socket is wreckage once, however many
        /// sparks reach it, and the fuse box blows exactly once.
        /// </summary>
        [Test]
        public void NothingOnTheCable_EverGoesOffTwice()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, FuseBoxPosition(simulation), 1);
            Advance(simulation, 60 * Run.TicksPerSecond);

            var counts = new Dictionary<ulong, int>();
            foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.ObjectExploded))
            {
                counts.TryGetValue(record.SourceId.Value, out int n);
                counts[record.SourceId.Value] = n + 1;
            }

            foreach (KeyValuePair<ulong, int> pair in counts)
            {
                Assert.That(pair.Value, Is.EqualTo(1), $"Thing {pair.Key} went off {pair.Value} times.");
            }
        }

        /// <summary>A building with no cable in it runs perfectly happily.</summary>
        [Test]
        public void ABuildingWithNoCable_StillRuns()
        {
            ScenarioData data = Quiet();
            data.PowerLines = new PowerLineDefinition[0];
            var simulation = new Run(data);

            Assert.That(simulation.PowerForTests.LineCount, Is.Zero);
            Assert.DoesNotThrow(() => Advance(simulation, 100));
        }

        /// <summary>
        /// A socket the flames reach goes off with a bang, and that is all: it
        /// lights no cable, and the fuse box stays where it is. The owner's rule:
        /// "only if the fusebox goes, it should quickly cascade down to all
        /// outlets, but not the other way around".
        /// </summary>
        [Test]
        public void ASocketGoingOffByItself_LightsNoCable_AndNeverReachesTheFuseBox()
        {
            ScenarioData data = Quiet();
            using (var simulation = new Run(data))
            {
                Advance(simulation, 2);
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                objects.Detonate(objects.IndexOf(OfficeSocket), OfficeSocket, 0UL);
                Advance(simulation, 60 * Run.TicksPerSecond);

                Assert.That(EventsOfType(simulation, CausalEventType.PowerSparkStarted), Is.Empty, "No spark set off.");
                Assert.That(EventsOfType(simulation, CausalEventType.ObjectExploded).Exists(e => e.SourceId == TheFuseBox),
                    Is.False, "The fuse box never went.");
                Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.False);
            }
        }

        /// <summary>
        /// A socket already wrecked does not stop the spark: it arrives, finds
        /// nothing to set off, and carries on down the line to the next.
        /// </summary>
        [Test]
        public void TheFuseBox_SendsItsSparkOnPastASocketAlreadyGone()
        {
            ScenarioData data = Quiet();
            using (var simulation = new Run(data))
            {
                Advance(simulation, 2);
                PhysicsObjectSystem objects = simulation.ObjectsForTests;
                objects.Detonate(objects.IndexOf(OfficeSocket), OfficeSocket, 0UL);
                simulation.QueueCommand(PlayerCommandType.PopFuseBox, FuseBoxPosition(simulation), simulation.Tick + 1);
                Advance(simulation, 10 * Run.TicksPerSecond);

                var went = new HashSet<ulong>();
                foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.ObjectExploded))
                {
                    went.Add(record.SourceId.Value);
                }

                Assert.That(went, Does.Contain(FarSocket.Value), "The socket beyond the wrecked one still went.");
                Assert.That(went, Does.Contain(new SimulationId(3273UL).Value), "And the one beyond that.");
            }
        }

        /// <summary>
        /// Quick: every socket in the office goes within three seconds of the
        /// fuse box. The spark used to crawl slower than somebody running,
        /// while it crept up to the fuse box; now the fuse box going is the big
        /// moment, and the whole building follows it at once.
        /// </summary>
        [Test]
        public void EverySocket_GoesWithinThreeSecondsOfTheFuseBox()
        {
            ScenarioData data = Quiet();
            using (var simulation = new Run(data))
            {
                simulation.QueueCommand(PlayerCommandType.PopFuseBox, FuseBoxPosition(simulation), 1);
                Advance(simulation, 10 * Run.TicksPerSecond);

                int fuseBoxWent = -1;
                int lastSocketWent = -1;
                int sockets = 0;
                foreach (CausalEvent record in EventsOfType(simulation, CausalEventType.ObjectExploded))
                {
                    if (record.SourceId == TheFuseBox)
                    {
                        fuseBoxWent = record.Tick;
                    }
                    else if (record.SourceId.Value >= 3271UL && record.SourceId.Value <= 3273UL)
                    {
                        sockets++;
                        lastSocketWent = System.Math.Max(lastSocketWent, record.Tick);
                    }
                }

                Assert.That(fuseBoxWent, Is.GreaterThan(0));
                Assert.That(sockets, Is.EqualTo(3), "All three sockets went.");
                Assert.That(lastSocketWent - fuseBoxWent, Is.LessThanOrEqualTo(3 * Run.TicksPerSecond));
            }
        }

        private static LogicalPosition FuseBoxPosition(Run simulation)
        {
            Assert.That(simulation.PowerForTests.TryFindTheFuseBox(out LogicalPosition where), Is.True,
                "The building should have a fuse box.");
            return where;
        }
    }
}

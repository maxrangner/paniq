using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The cable through the walls. A socket popping lights it like a fuse on
    /// a stick of dynamite, and the spark crawls along the wall to whatever is
    /// at the far end, which pops in turn, all the way to the fuse box.
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
            data.Influence.Starting = 500;
            data.Influence.Maximum = 500;
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
            int price = data.Influence.CardCost;
            var simulation = new Run(data);
            LogicalPosition box = FuseBoxPosition(simulation);
            int before = simulation.Influence;

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, 1);
            Advance(simulation, 2);
            Assert.That(simulation.Influence, Is.EqualTo(before - price));

            // A second card on a box that has already gone does nothing at all.
            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, simulation.Tick + 1);
            Advance(simulation, 3);
            Assert.That(simulation.Influence, Is.EqualTo(before - price), "A refused card is free.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerPoppedFuseBox), Has.Count.EqualTo(1));
        }

        /// <summary>Aimed at nothing in particular, the card is refused and costs nothing.</summary>
        [Test]
        public void TheCardPlayedNowhereNearTheFuseBox_IsRefusedAndFree()
        {
            ScenarioData data = Quiet();
            var simulation = new Run(data);
            int before = simulation.Influence;

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, TheBuilding.Cafeteria, 1);
            Advance(simulation, 3);

            Assert.That(simulation.Influence, Is.EqualTo(before), "Nothing happened, so nothing was spent.");
            Assert.That(EventsOfType(simulation, CausalEventType.PowerPoppedFuseBox), Is.Empty,
                "The log should not record something that did not happen.");
            Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.False);
        }

        /// <summary>Too poor to play it: refused, and the box is untouched.</summary>
        [Test]
        public void WithAnEmptyPurse_TheCardIsRefused()
        {
            ScenarioData data = Quiet();
            data.Influence.Starting = 1;
            data.Influence.Maximum = 1;
            var simulation = new Run(data);
            LogicalPosition box = FuseBoxPosition(simulation);

            simulation.QueueCommand(PlayerCommandType.PopFuseBox, box, 1);
            Advance(simulation, 3);

            Assert.That(simulation.PowerForTests.FuseBoxHasBlown, Is.False);
            Assert.That(simulation.Influence, Is.EqualTo(1));
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
        /// The spark crawls. It used to travel at six metres a second, which is
        /// faster than anybody in the building can run, so the whole chain went
        /// off within a few seconds of the first socket and there was nothing to
        /// see and nothing to do about it.
        /// </summary>
        [Test]
        public void TheSpark_TravelsSlowerThanSomebodyRunning()
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            int sparkPerTick = data.Power.SparkSpeedMillimetresPerTick;
            Assert.That(sparkPerTick, Is.LessThan(data.Panic.SpeedMaximum),
                "A fuse that outruns the people watching it is just a delayed explosion.");
        }

        private static LogicalPosition FuseBoxPosition(Run simulation)
        {
            Assert.That(simulation.PowerForTests.TryFindTheFuseBox(out LogicalPosition where), Is.True,
                "The building should have a fuse box.");
            return where;
        }
    }
}

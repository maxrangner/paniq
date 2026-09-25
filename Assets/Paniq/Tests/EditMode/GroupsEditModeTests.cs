using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The "Stick together" card (the owner asked, 2026-09-25): everybody the
    /// throw catches becomes a group that keeps together once frightened. Not
    /// a leash -- the cruel walk off, the brave feel it least -- and a throw
    /// that catches fewer than two people makes no group and is free.
    /// </summary>
    public sealed class GroupsEditModeTests
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

        /// <summary>A bare office with only these people in it, every card in hand, and a fire that starts at once and never spreads.</summary>
        private ScenarioData Quiet(params AgentDefinition[] people)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = people;
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];
            data.Alarms = new AlarmDefinition[0];
            data.Timetable = new ScheduledCue[0];
            data.Day.ToiletEveryTicks = 0;
            data.Fire.ActivationTick = 3;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Panic.HesitateChancePercent = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Leadership.LeaderMinimum = AgentTraitValues.Maximum + 1;
            TheBuilding.FireAt(data, new LogicalPosition(4500, -4500));
            return data;
        }

        private static AgentDefinition Someone(ulong id, int x, int z, int speed, int evil = 2, int leadership = 5)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), CardinalDirection.East,
                new AgentTraitValues(5, speed, 5, 5, evil, 5, leadership));
        }

        /// <summary>
        /// Four people in the office: a row of three across, and the slowest
        /// of them a step and a half behind the row, all facing the fire in
        /// the far corner. One throw at the middle of them catches all four.
        /// </summary>
        private ScenarioData FourInARow(int evilOfTheFastest = 2)
        {
            return Quiet(
                Someone(1UL, -2800, -3200, 3),
                Someone(2UL, -3400, -1500, 5),
                Someone(3UL, -2800, -1500, 7),
                Someone(4UL, -2200, -1500, 9, evilOfTheFastest));
        }

        private static readonly LogicalPosition MiddleOfTheFour = new LogicalPosition(-2800, -2350);

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

        /// <summary>
        /// How many ticks pass between the first and the last of them
        /// crossing a line three metres short of the office's corridor door
        /// (short of it, so the queue at the door itself does not count), or
        /// -1 if they do not all get there inside the limit.
        /// </summary>
        private static int ArrivalGap(Run simulation, int limitTicks)
        {
            const int line = 3000;
            var crossedAt = new int[simulation.AgentCount];
            for (int i = 0; i < crossedAt.Length; i++)
            {
                crossedAt[i] = -1;
            }

            int first = -1;
            for (int t = 0; t < limitTicks; t++)
            {
                simulation.Step();
                bool all = true;
                for (int i = 0; i < crossedAt.Length; i++)
                {
                    if (crossedAt[i] < 0 && simulation.GetAgent(i).Position.Z >= line)
                    {
                        crossedAt[i] = t;
                        first = first < 0 ? t : first;
                    }

                    all &= crossedAt[i] >= 0;
                }

                if (all)
                {
                    return t - first;
                }
            }

            return -1;
        }

        [Test]
        public void TheThrow_BindsThoseInsideTheCircle_AndNotOutside()
        {
            ScenarioData data = Quiet(
                Someone(1UL, -3000, 0, 5),
                Someone(2UL, -2500, 500, 5),
                Someone(3UL, -3500, 500, 5),
                Someone(4UL, 2000, 2000, 5));
            using (var simulation = new Run(data, 7UL))
            {
                int purse = simulation.Influence;
                simulation.QueueCommand(PlayerCommandType.StickTogether, new LogicalPosition(-3000, 300), 1);
                simulation.Step();

                Assert.That(simulation.GetAgent(0).GroupId, Is.EqualTo(0));
                Assert.That(simulation.GetAgent(1).GroupId, Is.EqualTo(0));
                Assert.That(simulation.GetAgent(2).GroupId, Is.EqualTo(0));
                Assert.That(simulation.GetAgent(3).GroupId, Is.EqualTo(-1), "Out of the circle: not bound.");
                List<CausalEvent> bound = EventsOfType(simulation, CausalEventType.PowerStickTogether);
                Assert.That(bound, Has.Count.EqualTo(3), "One event per person caught.");
                Assert.That(simulation.Influence, Is.EqualTo(purse - data.Influence.CardCost));
            }
        }

        [Test]
        public void OnePersonCaught_MakesNoGroup_AndIsFree()
        {
            ScenarioData data = Quiet(Someone(1UL, -3000, 0, 5), Someone(2UL, 2000, 2000, 5));
            using (var simulation = new Run(data, 7UL))
            {
                int purse = simulation.Influence;
                int cards = simulation.GetSnapshot().Hand.Count;
                simulation.QueueCommand(PlayerCommandType.StickTogether, new LogicalPosition(-3000, 0), 1);
                simulation.Step();

                Assert.That(simulation.GetAgent(0).GroupId, Is.EqualTo(-1), "A group of one is no group.");
                Assert.That(EventsOfType(simulation, CausalEventType.PowerStickTogether), Is.Empty);
                Assert.That(simulation.Influence, Is.EqualTo(purse), "A miss is free.");
                Assert.That(simulation.GetSnapshot().Hand.Count, Is.EqualTo(cards), "And the card stays in hand.");
            }
        }

        /// <summary>
        /// Four people of very different speeds run for the corridor door.
        /// Bound, the fast ones hang back and they arrive as a knot; loose,
        /// the sprinter is long gone while the slowest is still crossing.
        /// </summary>
        [Test]
        public void AGroup_CrossesTheOffice_AsAKnot()
        {
            int bound;
            int loose;
            using (var simulation = new Run(FourInARow(), 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.StickTogether, MiddleOfTheFour, 1);
                bound = ArrivalGap(simulation, 30 * Run.TicksPerSecond);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerStickTogether), Has.Count.EqualTo(4), "All four caught.");
            }

            using (var simulation = new Run(FourInARow(), 7UL))
            {
                loose = ArrivalGap(simulation, 30 * Run.TicksPerSecond);
            }

            Assert.That(bound, Is.GreaterThanOrEqualTo(0), "The group should all get across.");
            Assert.That(loose, Is.GreaterThanOrEqualTo(0), "So should the loose crowd.");
            Assert.That(bound, Is.LessThan(loose), $"Bound, the last is {bound} ticks behind the first; loose, {loose}.");
        }

        /// <summary>The cruel walk off: a bastard sprinter leaves the rest behind, bound or not.</summary>
        [Test]
        public void TheCruel_WalkOff()
        {
            int withAKindSprinter;
            int withACruelSprinter;
            using (var simulation = new Run(FourInARow(evilOfTheFastest: 2), 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.StickTogether, MiddleOfTheFour, 1);
                withAKindSprinter = ArrivalGap(simulation, 30 * Run.TicksPerSecond);
            }

            using (var simulation = new Run(FourInARow(evilOfTheFastest: 9), 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.StickTogether, MiddleOfTheFour, 1);
                withACruelSprinter = ArrivalGap(simulation, 30 * Run.TicksPerSecond);
            }

            Assert.That(withAKindSprinter, Is.GreaterThanOrEqualTo(0));
            Assert.That(withACruelSprinter, Is.GreaterThanOrEqualTo(0));
            Assert.That(withACruelSprinter, Is.GreaterThan(withAKindSprinter),
                $"With the cruel one the last is {withACruelSprinter} ticks behind; with a kind one, {withAKindSprinter}.");
        }

        /// <summary>A visitor who knows no way out, bound to somebody who works here, is told it.</summary>
        [Test]
        public void AVisitor_BoundToSomebodyWhoWorksHere_LearnsTheWayOut()
        {
            ScenarioData data = Quiet(
                Someone(1UL, -3000, -4500, 5),
                Someone(2UL, -2500, -4500, 5).WithFamiliarity(AgentFamiliarity.Visitor));
            using (var simulation = new Run(data, 7UL))
            {
                simulation.QueueCommand(PlayerCommandType.StickTogether, new LogicalPosition(-2750, -4500), 1);
                for (int t = 0; t < 6 * Run.TicksPerSecond; t++)
                {
                    simulation.Step();
                }

                bool told = false;
                foreach (CausalEvent found in EventsOfType(simulation, CausalEventType.AgentFoundTheWayOut))
                {
                    told |= found.SourceId.Value == 2UL && found.Strength == (int)WayLearned.Told;
                }

                Assert.That(told, Is.True, "The visitor should have been told the way out by the other member.\n" + simulation.DescribeForTests(1));
            }
        }
    }
}

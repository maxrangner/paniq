using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Shoving people aside. Running into somebody is an accident that needs
    /// speed; taking hold of whoever is in the way and heaving them out of it is
    /// deliberate, works at walking pace, and only the cruel do it.
    /// </summary>
    public sealed class ShovingEditModeTests
    {
        private static readonly SimulationId Shover = new SimulationId(1UL);
        private static readonly SimulationId InTheWay = new SimulationId(2UL);

        private static readonly AgentTraitValues Cruel = new AgentTraitValues(5, 5, 5, 1, 9, 5);

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

        /// <summary>
        /// One person fleeing down a bare room toward the way out with somebody
        /// standing in their way, at a pace too slow to run anybody over, so the
        /// only way past is to shove. The person in the way neither sees the
        /// fire nor hears the shouting, so they just stand there.
        /// </summary>
        private ScenarioData BlockedOnTheWayOut(AgentTraitValues shover, AgentTraitValues inTheWay)
        {
            // A way out in the office's south wall, so south is where the
            // shover bolts and the blockage is on their way to it. Both stand
            // on the line through that doorway, or the shover simply walks
            // round the blockage instead of having to deal with it.
            ScenarioData data =
                DoorsEditModeTests.WithAWayOutOfTheOffice(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Shover, new LogicalPosition(-2500, -1000), CardinalDirection.North, shover),
                new AgentDefinition(InTheWay, new LogicalPosition(-2500, -1800), CardinalDirection.South, inTheWay)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];

            // A fire just north of the shover, who is facing it and so sees it
            // at once, then bolts away from it: straight south into the other
            // person, and on toward the way out. The one in the way faces south
            // and never sees it.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-2500, -2500, 200, 200);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;

            // Walking pace while panicking, so a blocked step is never fast
            // enough to count as running into somebody.
            data.Panic.SpeedMinimum = 20;
            data.Panic.SpeedMaximum = 20;
            data.Traits.PanicSpeedJitter = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Panic.HesitateChancePercent = 0;

            // Nobody is alarmed by anything but seeing the fire themselves.
            data.Hearing.FireHearingRadiusMillimetres = 0;
            data.Hearing.YellAlarmRadiusMillimetres = 1;
            data.Hearing.YellHearingRadiusMillimetres = 1;
            data.Hearing.BumpSoundRadiusMillimetres = 1;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;

            // Everybody runs, and nobody helps, leads or fights the fire, so the
            // only thing that happens at the blockage is the shove, or no shove.
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;
            data.Extinguishers.SaveMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Leadership.LeaderMinimum = AgentTraitValues.Maximum + 1;
            return data;
        }

        private static void Advance(Run simulation, int seconds)
        {
            for (int t = 0; t < seconds * Run.TicksPerSecond; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void ACruelRunner_ShovesWhoeverIsInTheWay()
        {
            var simulation = new Run(BlockedOnTheWayOut(Cruel, AgentTraitValues.AllOrdinary));
            Advance(simulation, 12);

            List<CausalEvent> shoves = EventsOfType(simulation, CausalEventType.AgentShoved);
            Assert.That(shoves, Is.Not.Empty, "The cruel runner never shoved the person in the way.");
            Assert.That(shoves[0].SourceId, Is.EqualTo(Shover));
            Assert.That(shoves[0].TargetId, Is.EqualTo(InTheWay));
            Assert.That(simulation.EventLog.Get(shoves[0].CausalParentEventId).EventType,
                Is.EqualTo(CausalEventType.AgentScared), "A shove is caused by the shover being frightened.");
        }

        [Test]
        public void AnOrdinaryRunner_GoesRoundInsteadOfShoving()
        {
            var simulation = new Run(
                BlockedOnTheWayOut(AgentTraitValues.AllOrdinary, AgentTraitValues.AllOrdinary));
            Advance(simulation, 12);

            Assert.That(EventsOfType(simulation, CausalEventType.AgentShoved), Is.Empty,
                "Only the cruel lay hands on people.");
        }

        [Test]
        public void AShove_ActuallyMovesThePersonOutOfTheWay()
        {
            var simulation = new Run(BlockedOnTheWayOut(Cruel, AgentTraitValues.AllOrdinary));
            LogicalPosition before = simulation.GetAgent(InTheWay).Position;
            Advance(simulation, 12);

            Assert.That(EventsOfType(simulation, CausalEventType.AgentShoved), Is.Not.Empty);
            Assert.That(simulation.GetAgent(InTheWay).Position, Is.Not.EqualTo(before),
                "Being shoved should actually move somebody.");
        }

        /// <summary>
        /// A much stronger shover puts them on the floor; an evenly matched one
        /// only sends them reeling.
        /// </summary>
        [TestCase(9, 3, true)]
        [TestCase(5, 5, false)]
        public void AStrongEnoughShove_PutsThemOnTheFloor(int shoverStrength, int victimStrength, bool expectFloored)
        {
            var simulation = new Run(BlockedOnTheWayOut(
                new AgentTraitValues(shoverStrength, 5, 5, 1, 9, 5),
                new AgentTraitValues(victimStrength, 5, 5, 5, 0, 5)));
            Advance(simulation, 12);

            Assert.That(EventsOfType(simulation, CausalEventType.AgentShoved), Is.Not.Empty,
                "Nobody was shoved at all.");

            bool floored = false;
            foreach (CausalEvent down in EventsOfType(simulation, CausalEventType.AgentKnockedDown))
            {
                floored |= down.SourceId == InTheWay &&
                           simulation.EventLog.Get(down.CausalParentEventId).EventType == CausalEventType.AgentShoved;
            }

            Assert.That(floored, Is.EqualTo(expectFloored),
                expectFloored
                    ? "A much stronger person should floor whoever they shove."
                    : "An evenly matched shove should only send somebody reeling.");
        }

        [Test]
        public void Shoving_HasAPauseBetweenHeaves()
        {
            ScenarioData data = BlockedOnTheWayOut(Cruel, AgentTraitValues.AllOrdinary);
            int pause = data.Falls.ShoveIntervalTicks;
            var simulation = new Run(data);
            Advance(simulation, 12);

            List<CausalEvent> shoves = EventsOfType(simulation, CausalEventType.AgentShoved);
            Assert.That(shoves, Is.Not.Empty);
            for (int i = 1; i < shoves.Count; i++)
            {
                Assert.That(shoves[i].Tick - shoves[i - 1].Tick, Is.GreaterThanOrEqualTo(pause),
                    "Two shoves came closer together than the pause allows.");
            }
        }

        [Test]
        public void SomebodyFrozenForGood_CanStillBeHeavedAside()
        {
            // The frozen never move themselves, but somebody cruel can move them.
            ScenarioData data = BlockedOnTheWayOut(
                new AgentTraitValues(9, 5, 9, 1, 9, 0),
                new AgentTraitValues(3, 5, 0, 5, 0, 10));

            // One of the two freezes for good, and the deck deals that card to
            // the more fearful of them, which is the one standing in the way.
            data.Temperament.FreezeForeverPercent = 50;
            var simulation = new Run(data);
            Advance(simulation, 12);

            Assert.That(simulation.GetAgent(InTheWay).Temperament, Is.EqualTo(AgentPanicTemperament.FreezeForever),
                "The fearful one should have been dealt the freezing card.");
            Assert.That(EventsOfType(simulation, CausalEventType.AgentShoved), Is.Not.Empty,
                "A cruel person should heave even a frozen person out of the way.");
        }
    }
}

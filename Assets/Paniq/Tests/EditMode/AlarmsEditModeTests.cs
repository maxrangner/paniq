using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Fire alarms. Somebody who has seen the fire and is brave, thinks of
    /// other people or is used to being listened to breaks off to hit the pull
    /// station on the wall; every bell in the building then rings, again every
    /// few seconds, and everybody who hears one takes fright exactly as if
    /// they had seen the flames (the owner's rule, 2026-09-25: "pull it, and
    /// everybody panics"). A bell the flames reach pops and falls silent.
    /// </summary>
    public sealed class AlarmsEditModeTests
    {
        private static readonly SimulationId Raiser = new SimulationId(1UL);
        private static readonly SimulationId FarAway = new SimulationId(2UL);
        private static readonly SimulationId OfficeAlarm = new SimulationId(6001UL);

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
        /// One person in the office who sees a fire at once, and one in the
        /// meeting room two rooms away who cannot see it and is too far off to
        /// hear anybody shout. Only a bell can tell the second one anything.
        /// </summary>
        private ScenarioData OfficeAndMeetingRoom(AgentTraitValues raiser, AgentTraitValues faraway)
        {
            ScenarioData data = TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(Raiser, new LogicalPosition(-4500, 2000), CardinalDirection.South, raiser),
                new AgentDefinition(FarAway, TheBuilding.MeetingRoom, CardinalDirection.North, faraway)
            };
            data.PhysicsObjects = new PhysicsObjectDefinition[0];
            data.Tables = new TableDefinition[0];

            // A fire right in front of the person in the office.
            data.Fire.ActivationTick = 3;
            data.Fire.SpawnBounds = new LogicalBounds(-4500, -4500, 800, 800);
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;

            // Shouting carries nowhere, so the bell is the only news that travels.
            data.Hearing.FireHearingRadiusMillimetres = 0;
            data.Hearing.YellAlarmRadiusMillimetres = 1;
            data.Hearing.YellHearingRadiusMillimetres = 1;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
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

        /// <summary>Somebody kind, who will go for the alarm.</summary>
        private static AgentTraitValues Kind => new AgentTraitValues(5, 5, 5, 9, 0, 3);

        /// <summary>Somebody brave and nothing else: neither kind nor in charge.</summary>
        private static AgentTraitValues Brave => new AgentTraitValues(5, 5, 8, 3, 2, 3, 3);

        // ---------------------------------------------------------- the player's pull

        /// <summary>
        /// The player pulls a fire alarm for thirty (the owner's call,
        /// 2026-09-24): every bell in the building rings, the story names the
        /// player as the root cause, and the purse is thirty lighter.
        /// </summary>
        [Test]
        public void ThePlayer_CanPullAnAlarm_AndEveryBellRings()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.Influence.Starting = 30;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
                Advance(simulation, 1);

                List<CausalEvent> pulled = EventsOfType(simulation, CausalEventType.PowerPulledAlarm);
                Assert.That(pulled, Has.Count.EqualTo(1));
                Assert.That(pulled[0].TargetId, Is.EqualTo(OfficeAlarm));
                Assert.That(pulled[0].CausalParentEventId, Is.EqualTo(0UL), "The player is the root cause.");
                List<CausalEvent> rang = EventsOfType(simulation, CausalEventType.AlarmRang);
                Assert.That(rang, Has.Count.EqualTo(simulation.BellCount), "Every bell in the building.");
                foreach (CausalEvent bell in rang)
                {
                    Assert.That(bell.CausalParentEventId, Is.EqualTo(pulled[0].EventId));
                }

                Assert.That(simulation.Influence, Is.EqualTo(0), "Thirty of the thirty.");
                Assert.That(simulation.GetAgent(1).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                    "Somebody two rooms away heard the bell.");
            }
        }

        [Test]
        public void ThePlayer_TooPoorToPull_RingsNothingAndPaysNothing()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.Influence.Starting = 29;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
                Advance(simulation, 1);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerPulledAlarm), Is.Empty);
                Assert.That(EventsOfType(simulation, CausalEventType.AlarmRang), Is.Empty);
                Assert.That(simulation.Influence, Is.EqualTo(29));
            }
        }

        [Test]
        public void PullingAnAlarmThatIsAlreadyRinging_CostsNothing()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.Influence.Starting = 60;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
                simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 20);
                Advance(simulation, 1);
                Assert.That(EventsOfType(simulation, CausalEventType.PowerPulledAlarm), Has.Count.EqualTo(1));
                Assert.That(simulation.Influence, Is.EqualTo(30), "The second pull did nothing and cost nothing.");
            }
        }

        /// <summary>Somebody selfish, who will not.</summary>
        private static AgentTraitValues Selfish => new AgentTraitValues(5, 5, 5, 1, 8, 5, 1);

        [Test]
        public void SomebodyKind_GoesAndHitsTheAlarm()
        {
            var simulation = new Run(OfficeAndMeetingRoom(Kind, Kind));
            Advance(simulation, 10);

            List<CausalEvent> pulled = EventsOfType(simulation, CausalEventType.AlarmPulled);
            Assert.That(pulled, Is.Not.Empty, "Nobody raised the alarm.");
            Assert.That(pulled[0].SourceId, Is.EqualTo(Raiser));
            Assert.That(pulled[0].TargetId, Is.EqualTo(OfficeAlarm), "They should use the alarm in their own room.");
            Assert.That(simulation.AlarmsRinging, Is.True);
        }

        [Test]
        public void HittingOneAlarm_RingsEveryBellInTheBuilding()
        {
            var simulation = new Run(OfficeAndMeetingRoom(Kind, Kind));
            Advance(simulation, 10);

            Assert.That(EventsOfType(simulation, CausalEventType.AlarmPulled).Count, Is.EqualTo(1),
                "One alarm is hit, once.");
            Assert.That(EventsOfType(simulation, CausalEventType.AlarmRang).Count, Is.GreaterThanOrEqualTo(simulation.BellCount),
                "Every bell in the building should ring.");
        }

        [Test]
        public void ABell_TellsSomebodyTwoRoomsAwayWhoCouldNotHaveKnown()
        {
            var simulation = new Run(OfficeAndMeetingRoom(Kind, Kind));

            // The office sees the fire at tick 3; the bell takes a second or so
            // to be reached and hit, and until it rings the meeting room knows
            // nothing at all.
            for (int t = 0; t < 5; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.AlarmsRinging, Is.False, "The bell cannot have been reached yet.");
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.EqualTo(AgentFearState.Calm));

            Advance(simulation, 10);
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                "A bell should reach the meeting room.");
            Assert.That(simulation.GetAgent(FarAway).AlertSource, Is.EqualTo(AgentAlertSource.Alarm));
        }

        [Test]
        public void NobodySelfish_BothersWithTheAlarm()
        {
            var simulation = new Run(OfficeAndMeetingRoom(Selfish, Kind));
            Advance(simulation, 10);

            Assert.That(EventsOfType(simulation, CausalEventType.AlarmPulled), Is.Empty,
                "Somebody selfish leaves the alarm for somebody else.");
            Assert.That(simulation.AlarmsRinging, Is.False);
        }

        /// <summary>Somebody brave, and nothing else, goes for the alarm too (the owner asked, 2026-09-25).</summary>
        [Test]
        public void SomebodyBrave_GoesAndHitsTheAlarm()
        {
            var simulation = new Run(OfficeAndMeetingRoom(Brave, Selfish));
            Advance(simulation, 10);

            List<CausalEvent> pulled = EventsOfType(simulation, CausalEventType.AlarmPulled);
            Assert.That(pulled, Is.Not.Empty, "The brave raise the alarm.");
            Assert.That(pulled[0].SourceId, Is.EqualTo(Raiser));
        }

        [Test]
        public void AlarmsTurnedOff_AreNeverRung()
        {
            ScenarioData data = OfficeAndMeetingRoom(Kind, Kind);
            data.Alarm.Enabled = false;
            var simulation = new Run(data);
            Advance(simulation, 10);

            Assert.That(EventsOfType(simulation, CausalEventType.AlarmPulled), Is.Empty);
            Assert.That(simulation.AlarmsRinging, Is.False);
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.EqualTo(AgentFearState.Calm),
                "With the alarms off, the meeting room never finds out.");
        }

        // ------------------------------------------------------------ everybody panics

        /// <summary>
        /// A bell frightens whoever hears it exactly as the sight of flames
        /// would, whoever they are: the brave and steady used to walk out at
        /// a stroll (the owner's rule, 2026-09-25).
        /// </summary>
        [TestCase(9, 1)]
        [TestCase(1, 9)]
        public void ABell_FrightensEverybodyWhoHearsIt_WhoeverTheyAre(int bravery, int nervousness)
        {
            ScenarioData data = OfficeAndMeetingRoom(Kind,
                new AgentTraitValues(5, 5, bravery, 5, 0, nervousness));
            var simulation = new Run(data);
            int fastest = 0;
            for (int t = 0; t < 12 * Run.TicksPerSecond; t++)
            {
                simulation.Step();
                fastest = System.Math.Max(fastest, simulation.GetAgent(FarAway).SpeedMillimetresPerTick);
            }

            AgentSnapshot listener = simulation.GetAgent(FarAway);
            Assert.That(listener.FearState, Is.EqualTo(AgentFearState.Scared), "The bell should frighten them.");
            Assert.That(listener.AlertSource, Is.EqualTo(AgentAlertSource.Alarm));
            Assert.That(fastest, Is.GreaterThan(data.Calm.SpeedMaximum + data.Traits.CalmSpeedJitter),
                "They run for it; nobody walks out any more.");
        }

        /// <summary>Told by a bell rather than by the sight of flames, somebody dealt the freezing card still freezes.</summary>
        [Test]
        public void ABell_FreezesThoseWhoFreeze()
        {
            ScenarioData data = OfficeAndMeetingRoom(
                new AgentTraitValues(5, 5, 9, 9, 0, 1),
                new AgentTraitValues(5, 5, 8, 5, 0, 1));
            data.Temperament.FreezeForeverPercent = 50;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new Run(data);
            Advance(simulation, 12);

            AgentSnapshot listener = simulation.GetAgent(FarAway);
            Assert.That(listener.Temperament, Is.EqualTo(AgentPanicTemperament.FreezeForever),
                "The freezing card should have gone to the one in the meeting room.");
            Assert.That(listener.FearState, Is.EqualTo(AgentFearState.Scared));
            Assert.That(listener.ActivityState, Is.EqualTo(AgentActivityState.Frozen), "A bell freezes them as flames would.");
        }

        /// <summary>
        /// The player pulls an alarm before the fire exists. Everybody who
        /// hears it takes fright and goes for a way out all the same -- each a
        /// few ticks after the bell, and no two on one tick. Before 2026-09-25
        /// they stood startled, facing the bell, until the flames came.
        /// </summary>
        [Test]
        public void ABellPulledBeforeAnyFire_SendsEverybodyForTheWayOut_EachInTheirOwnTime()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.Round.HazardWaitsForTrigger = true;
            data.Influence.Starting = 30;
            var simulation = new Run(data, 42UL);
            LogicalPosition farAwayStart = simulation.GetAgent(FarAway).Position;
            simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
            Advance(simulation, 10);

            Assert.That(simulation.FireCellCount, Is.Zero, "No fire at all.");
            List<CausalEvent> rang = EventsOfType(simulation, CausalEventType.AlarmRang);
            Assert.That(rang, Is.Not.Empty);
            int bellTick = rang[0].Tick;
            var scaredTicks = new HashSet<int>();
            foreach (CausalEvent scared in EventsOfType(simulation, CausalEventType.AgentScared))
            {
                Assert.That(scared.Tick, Is.GreaterThan(bellTick), "Nobody is frightened on the bell's own tick.");
                Assert.That(scaredTicks.Add(scared.Tick), Is.True, "No two people take fright on one tick.");
            }

            Assert.That(scaredTicks, Has.Count.EqualTo(2), "Both of them heard a bell.");
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                AgentSnapshot person = simulation.GetAgent(i);
                Assert.That(person.FearState, Is.EqualTo(AgentFearState.Scared), $"{person.AgentId} should be frightened.");
                Assert.That(person.ActivityState, Is.Not.EqualTo(AgentActivityState.Reacting),
                    $"{person.AgentId} should have stopped staring at the bell and gone for a way out.");
            }

            Assert.That(IntegerMath.Distance(farAwayStart, simulation.GetAgent(FarAway).Position), Is.GreaterThan(2000),
                "The one in the meeting room should be well on their way.");
        }

        /// <summary>
        /// The bells ring again every few seconds, each on its own beat, so a
        /// door opened later lets the news through.
        /// </summary>
        [Test]
        public void TheBells_RingAgain_EachOnItsOwnBeat()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.Influence.Starting = 30;
            var simulation = new Run(data, 42UL);
            simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
            Advance(simulation, 16);

            List<CausalEvent> rang = EventsOfType(simulation, CausalEventType.AlarmRang);
            Assert.That(rang.Count, Is.GreaterThanOrEqualTo(simulation.BellCount * 2), "Every bell rang at least twice.");
            var secondRings = new HashSet<int>();
            var seen = new HashSet<ulong>();
            foreach (CausalEvent ring in rang)
            {
                if (!seen.Add(ring.SourceId.Value))
                {
                    secondRings.Add(ring.Tick);
                }
            }

            Assert.That(secondRings.Count, Is.GreaterThan(1), "The bells do not all ring again on the same tick.");
        }

        /// <summary>
        /// The bells are things on the walls, and the flames can reach one: it
        /// goes off with a crack and falls silent, and the others ring on.
        /// </summary>
        [Test]
        public void ABellTheFlamesReach_PopsAndFallsSilent_WhileTheOthersRingOn()
        {
            ScenarioData data = OfficeAndMeetingRoom(Selfish, Selfish);
            data.PhysicsObjects = System.Array.FindAll(scenario.ToRuntimeData().PhysicsObjects,
                thing => thing.Kind == PhysicsObjectKind.AlarmSounder);
            data.Influence.Starting = 30;
            var officeBell = new SimulationId(3601UL);

            // A fire in the office's south-west corner, right under its bell.
            data.Fire.SpawnBounds = new LogicalBounds(-5250, -5250, -1250, -1250);
            var simulation = new Run(data, 42UL);
            simulation.QueueCommand(PlayerCommandType.PullAlarm, OfficeAlarm, 5);
            Advance(simulation, 16);

            List<CausalEvent> bangs = EventsOfType(simulation, CausalEventType.ObjectExploded);
            Assert.That(bangs.Exists(bang => bang.SourceId == officeBell), Is.True, "The office bell should have gone off.");

            int officeRings = 0;
            int otherRings = 0;
            foreach (CausalEvent ring in EventsOfType(simulation, CausalEventType.AlarmRang))
            {
                officeRings += ring.SourceId == officeBell ? 1 : 0;
                otherRings += ring.SourceId == officeBell ? 0 : 1;
            }

            Assert.That(officeRings, Is.EqualTo(1), "The office bell rang once, before the flames reached it, and never again.");
            Assert.That(otherRings, Is.GreaterThanOrEqualTo((simulation.BellCount - 1) * 2), "The others ring on.");
            Assert.That(simulation.AlarmsRinging, Is.True, "The building has still been told.");
        }

        [Test]
        public void TheDefaultBuilding_HasAnAlarmInEachRoomPeopleUse()
        {
            var simulation = new Run(TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData()));
            Assert.That(simulation.AlarmCount, Is.EqualTo(5),
                "The office, the corridor, the cafeteria, the meeting room and the stockroom. The\n"
                + "closet, the stalls and the maintenance room have none: they are cupboards; and the\n"
                + "crossbar's, beside the way out, was taken out at the owner's request (2026-09-25).");
            Assert.That(simulation.BellCount, Is.EqualTo(7), "A bell in every room people use, the bathroom included.");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Fire alarms. Somebody who has seen the fire and thinks of other people
    /// breaks off to hit the alarm on the wall; every bell in the building then
    /// rings, and everybody who hears one learns there is a fire. What they do
    /// about it is personality: the brave and level-headed walk briskly out,
    /// while the nervous stampede — and composure lasts only until the fire
    /// actually comes at them.
    /// </summary>
    public sealed class FireReactionAlarmsEditModeTests
    {
        private static readonly SimulationId Raiser = new SimulationId(1UL);
        private static readonly SimulationId FarAway = new SimulationId(2UL);
        private static readonly SimulationId OfficeAlarm = new SimulationId(6001UL);

        private FireReactionScenario scenario;

        [SetUp]
        public void SetUp()
        {
            scenario = FireReactionScenario.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(scenario);
        }

        private static List<CausalEvent> EventsOfType(FireReactionSimulation simulation, FireReactionEventType type)
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
        private FireReactionScenarioData OfficeAndMeetingRoom(AgentTraitValues raiser, AgentTraitValues faraway)
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(Raiser, new LogicalPosition(-4500, 2000), CardinalDirection.South, raiser),
                new FireReactionAgentDefinition(FarAway, TheBuilding.MeetingRoom, CardinalDirection.North, faraway)
            };
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Tables = new FireReactionTableDefinition[0];

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

        private static void Run(FireReactionSimulation simulation, int seconds)
        {
            for (int t = 0; t < seconds * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }
        }

        /// <summary>Somebody kind, who will go for the alarm.</summary>
        private static AgentTraitValues Kind => new AgentTraitValues(5, 5, 6, 9, 0, 3);

        /// <summary>Somebody selfish, who will not.</summary>
        private static AgentTraitValues Selfish => new AgentTraitValues(5, 5, 5, 1, 8, 5, 1);

        [Test]
        public void SomebodyKind_GoesAndHitsTheAlarm()
        {
            var simulation = new FireReactionSimulation(OfficeAndMeetingRoom(Kind, Kind));
            Run(simulation, 10);

            List<CausalEvent> pulled = EventsOfType(simulation, FireReactionEventType.AlarmPulled);
            Assert.That(pulled, Is.Not.Empty, "Nobody raised the alarm.");
            Assert.That(pulled[0].SourceId, Is.EqualTo(Raiser));
            Assert.That(pulled[0].TargetId, Is.EqualTo(OfficeAlarm), "They should use the alarm in their own room.");
            Assert.That(simulation.AlarmsRinging, Is.True);
        }

        [Test]
        public void HittingOneAlarm_RingsEveryBellInTheBuilding()
        {
            var simulation = new FireReactionSimulation(OfficeAndMeetingRoom(Kind, Kind));
            Run(simulation, 10);

            Assert.That(EventsOfType(simulation, FireReactionEventType.AlarmPulled).Count, Is.EqualTo(1),
                "One alarm is hit, once.");
            Assert.That(EventsOfType(simulation, FireReactionEventType.AlarmRang).Count, Is.EqualTo(simulation.AlarmCount),
                "Every bell in the building should ring.");
        }

        [Test]
        public void ABell_TellsSomebodyTwoRoomsAwayWhoCouldNotHaveKnown()
        {
            var simulation = new FireReactionSimulation(OfficeAndMeetingRoom(Kind, Kind));

            // The office sees the fire at tick 3; the bell takes a second or so
            // to be reached and hit, and until it rings the meeting room knows
            // nothing at all.
            for (int t = 0; t < 5; t++)
            {
                simulation.Step();
            }

            Assert.That(simulation.AlarmsRinging, Is.False, "The bell cannot have been reached yet.");
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.EqualTo(AgentFearState.Calm));

            Run(simulation, 10);
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.Not.EqualTo(AgentFearState.Calm),
                "A bell should reach the meeting room.");
            Assert.That(simulation.GetAgent(FarAway).AlertSource, Is.EqualTo(AgentAlertSource.Alarm));
        }

        [Test]
        public void NobodySelfish_BothersWithTheAlarm()
        {
            var simulation = new FireReactionSimulation(OfficeAndMeetingRoom(Selfish, Kind));
            Run(simulation, 10);

            Assert.That(EventsOfType(simulation, FireReactionEventType.AlarmPulled), Is.Empty,
                "Somebody selfish leaves the alarm for somebody else.");
            Assert.That(simulation.AlarmsRinging, Is.False);
        }

        [Test]
        public void AlarmsTurnedOff_AreNeverRung()
        {
            FireReactionScenarioData data = OfficeAndMeetingRoom(Kind, Kind);
            data.Alarm.Enabled = false;
            var simulation = new FireReactionSimulation(data);
            Run(simulation, 10);

            Assert.That(EventsOfType(simulation, FireReactionEventType.AlarmPulled), Is.Empty);
            Assert.That(simulation.AlarmsRinging, Is.False);
            Assert.That(simulation.GetAgent(FarAway).FearState, Is.EqualTo(AgentFearState.Calm),
                "With the alarms off, the meeting room never finds out.");
        }

        // ------------------------------------------------------------ composure

        /// <summary>
        /// The brave and steady walk out; the nervous panic. Both are frightened
        /// and both head for a way out — the difference is how they do it.
        /// </summary>
        [TestCase(9, 1, true)]
        [TestCase(1, 9, false)]
        public void WhatABellDoesToSomebody_DependsWhoTheyAre(int bravery, int nervousness, bool expectComposed)
        {
            FireReactionScenarioData data = OfficeAndMeetingRoom(Kind,
                new AgentTraitValues(5, 5, bravery, 5, 0, nervousness));
            var simulation = new FireReactionSimulation(data);
            Run(simulation, 10);

            FireReactionAgentSnapshot listener = simulation.GetAgent(FarAway);
            Assert.That(listener.FearState, Is.EqualTo(AgentFearState.Scared), "The bell should frighten them either way.");
            Assert.That(listener.IsComposed, Is.EqualTo(expectComposed),
                expectComposed
                    ? "Somebody brave and steady should keep their head and walk out."
                    : "Somebody nervous should lose their head.");
        }

        [Test]
        public void SomebodyKeepingTheirHead_WalksOutRatherThanSprinting()
        {
            FireReactionScenarioData data = OfficeAndMeetingRoom(Kind, new AgentTraitValues(5, 5, 9, 5, 0, 1));
            var simulation = new FireReactionSimulation(data);

            int fastest = 0;
            for (int t = 0; t < 12 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
                FireReactionAgentSnapshot listener = simulation.GetAgent(FarAway);
                if (listener.IsComposed)
                {
                    fastest = System.Math.Max(fastest, listener.SpeedMillimetresPerTick);
                }
            }

            Assert.That(fastest, Is.GreaterThan(0), "They should actually be going somewhere.");
            Assert.That(fastest, Is.LessThanOrEqualTo(data.Calm.SpeedMaximum + data.Traits.CalmSpeedJitter),
                "Somebody keeping their head walks; they do not sprint.");
        }

        [Test]
        public void SomebodyKeepingTheirHead_LosesItWhenTheFireComesAtThem()
        {
            // The steady one in the meeting room, with a fire that starts right
            // beside them a few seconds after the bell.
            FireReactionScenarioData data = OfficeAndMeetingRoom(Kind, new AgentTraitValues(5, 5, 9, 5, 0, 1));
            var simulation = new FireReactionSimulation(data);
            Run(simulation, 6);
            Assert.That(simulation.GetAgent(FarAway).IsComposed, Is.True, "They should have kept their head so far.");

            // A fire at their feet, played by the player.
            simulation.QueueCommand(PlayerCommandType.SpawnFire, simulation.GetAgent(FarAway).Position,
                simulation.Tick + 1);
            Run(simulation, 3);

            Assert.That(simulation.GetAgent(FarAway).IsComposed, Is.False,
                "Composure should not survive the fire arriving.");
        }

        [Test]
        public void SomebodyKeepingTheirHead_NeverFreezes()
        {
            // Both are steady enough to keep their heads, but the one in the
            // meeting room is very slightly the more fearful of the two, so the
            // freezing card goes to them. Told by a bell rather than by the sight
            // of flames, they walk out instead of rooting to the spot.
            FireReactionScenarioData data = OfficeAndMeetingRoom(
                new AgentTraitValues(5, 5, 9, 9, 0, 1),
                new AgentTraitValues(5, 5, 8, 5, 0, 1));
            data.Temperament.FreezeForeverPercent = 50;
            data.Temperament.FreezeThenRunPercent = 0;
            var simulation = new FireReactionSimulation(data);
            Run(simulation, 12);

            FireReactionAgentSnapshot listener = simulation.GetAgent(FarAway);
            Assert.That(listener.Temperament, Is.EqualTo(AgentPanicTemperament.FreezeForever),
                "The freezing card should have gone to the one in the meeting room.");
            Assert.That(listener.IsComposed, Is.True);
            Assert.That(listener.ActivityState, Is.Not.EqualTo(AgentActivityState.Frozen),
                "Nobody keeping their head freezes.");
        }

        [Test]
        public void TheDefaultBuilding_HasAnAlarmInEachRoomPeopleUse()
        {
            var simulation = new FireReactionSimulation(scenario.ToRuntimeData());
            Assert.That(simulation.AlarmCount, Is.EqualTo(4),
                "The office, the corridor, the cafeteria and the meeting room. The closet, the\n"
                + "stalls and the maintenance room have none: they are cupboards.");
        }
    }
}

using System;
using System.Linq;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Visitors in a whole run: somebody who has never seen the floor, the way
    /// out standing open, and a slow fire back in the office. Where they look,
    /// what they read, who they follow, and whether they get out.
    /// <para>
    /// Like every test that builds a run these need Unity's physics engine,
    /// so they only run inside the editor. The rules they rest on are checked
    /// piece by piece, without it, in <see cref="WayfindingEditModeTests"/>.
    /// </para>
    /// </summary>
    public sealed class WayfindingRunEditModeTests
    {
        private static readonly SimulationId TheWayOut = TheBuilding.TheWayOut;

        /// <summary>Long enough to walk every room on the floor twice over.</summary>
        private const int TwoMinutes = 2 * 60 * Run.TicksPerSecond;

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
        /// The floor with these people on it and nothing else, the way out
        /// opened by the player at once, a fire in the office that barely
        /// spreads, and none of the excuses (freezing, tripping, dithering,
        /// stopping to help) that would make a test of where they walk pass or
        /// fail on luck.
        /// </summary>
        private ScenarioData Floor(bool withSigns, params AgentDefinition[] people)
        {
            // These tests open the way out in their first two ticks, and a
            // round now opens with an empty purse, so without this the clicks
            // are refused for want of purse points, the exit stays locked and
            // nobody gets out of the building at all. This file was written
            // before the economy landed; it is a test of where people walk,
            // not of what the player can afford.
            ScenarioData data =
                TheBuilding.WithThePlayerAbleToAct(TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData()));
            data.Agents = people;
            if (!withSigns)
            {
                data.ExitSigns = Array.Empty<ExitSignDefinition>();
            }

            data.PhysicsObjects = Array.Empty<PhysicsObjectDefinition>();
            data.Tables = Array.Empty<TableDefinition>();
            data.Alarms = Array.Empty<AlarmDefinition>();
            data.Fire.ActivationTick = 1;
            data.Fire.SpreadMinimumTicks = 100000;
            data.Fire.SpreadMaximumTicks = 100000;
            data.Temperament.FreezeThenRunPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            data.Falls.TripChancePercent = 0;
            data.Panic.HesitateChancePercent = 0;
            data.Panic.SwerveChancePercent = 0;
            data.Help.ShakeMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Help.DragMinimumCompassion = AgentTraitValues.Maximum + 1;
            data.Extinguishers.FightMinimumBravery = AgentTraitValues.Maximum + 1;

            // One square of fire that never spreads, far from most of them: a
            // visitor searching the far rooms would otherwise calm down and
            // stop looking. These tests are about finding the way, not about
            // how long a fright lasts.
            data.Calming.Enabled = false;
            return data;
        }

        private static AgentDefinition Visitor(ulong id, int x, int z, CardinalDirection facing,
            AgentTraitValues traits)
        {
            return new AgentDefinition(new SimulationId(id), new LogicalPosition(x, z), facing, traits)
                .WithFamiliarity(AgentFamiliarity.Visitor);
        }

        /// <summary>Somebody who falls in behind whoever shouts: timid, meek and nervous.</summary>
        private static readonly AgentTraitValues Follower = new AgentTraitValues(5, 5, 2, 5, 0, 9, 1);

        /// <summary>Opens the way out, runs until the fire has started, and frightens everybody at once.</summary>
        private static Run Start(ScenarioData data)
        {
            var simulation = new Run(data);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 1);
            simulation.QueueCommand(PlayerCommandType.ClickDoor, TheWayOut, 2);
            simulation.Step();
            simulation.Step();
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                simulation.FrightenForTests(i);
            }

            return simulation;
        }

        /// <summary>Runs until everybody is out, or the time is up.</summary>
        private static void RunUntilEverybodyIsOut(Run simulation, int ticks)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                bool allOut = true;
                for (int i = 0; i < simulation.AgentCount; i++)
                {
                    allOut &= simulation.GetAgent(i).Outcome == AgentTerminalOutcome.Escaped;
                }

                if (allOut)
                {
                    return;
                }

                simulation.Step();
            }
        }

        private static CausalEvent[] EventsOf(Run simulation, CausalEventType type,
            SimulationId who)
        {
            return simulation.EventLog.Events.Where(e => e.EventType == type && e.SourceId == who).ToArray();
        }

        /// <summary>
        /// Alone in the meeting room, no signs anywhere, nobody to follow: a
        /// visitor has to find the way out by looking. They go looking, and
        /// they get out.
        /// </summary>
        [Test]
        public void AStrangerOnTheirOwn_WithNoSigns_LooksForTheWayOutAndFindsIt()
        {
            var who = new SimulationId(1UL);
            Run simulation = Start(Floor(false,
                Visitor(1UL, -2000, 10500, CardinalDirection.South, AgentTraitValues.AllOrdinary)));

            RunUntilEverybodyIsOut(simulation, TwoMinutes);

            Assert.That(EventsOf(simulation, CausalEventType.AgentLookedForAWayOut, who), Is.Not.Empty,
                "Knowing of no way out, they should have gone looking for one.");
            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                "Two minutes is long enough to look in every room on the floor.");
        }

        /// <summary>
        /// Stood in the corridor facing along it, with a sign three metres
        /// ahead: they take it in, and it is the sign that tells them.
        /// </summary>
        [Test]
        public void ASign_ShowsAStrangerTheWay()
        {
            var who = new SimulationId(1UL);
            Run simulation = Start(Floor(true,
                Visitor(1UL, 0, 8000, CardinalDirection.East, AgentTraitValues.AllOrdinary)));

            RunUntilEverybodyIsOut(simulation, TwoMinutes);

            CausalEvent[] found = EventsOf(simulation, CausalEventType.AgentFoundTheWayOut, who);
            Assert.That(found, Is.Not.Empty, "They never learned of the way out.");
            Assert.That((WayLearned)found[0].Strength, Is.EqualTo(WayLearned.Sign));
            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
        }

        /// <summary>
        /// A host who works here and two clients who do not, in the meeting
        /// room, with no signs: the host calls them on, and in falling in
        /// behind they are told the way. All three get out.
        /// </summary>
        [Test]
        public void AHost_ShowsTheirClientsTheWayOut()
        {
            Run simulation = Start(Floor(false,
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2000, 11000),
                    CardinalDirection.South, new AgentTraitValues(6, 5, 7, 5, 1, 3, 9)),
                Visitor(2UL, -3500, 12000, CardinalDirection.East, Follower),
                Visitor(3UL, -500, 12000, CardinalDirection.West, Follower)));

            RunUntilEverybodyIsOut(simulation, TwoMinutes);

            foreach (ulong client in new[] { 2UL, 3UL })
            {
                CausalEvent[] found = EventsOf(simulation, CausalEventType.AgentFoundTheWayOut,
                    new SimulationId(client));
                Assert.That(found.Any(e => (WayLearned)e.Strength == WayLearned.Told), Is.True,
                    $"Client {client} was never told the way.");
            }

            for (int i = 0; i < simulation.AgentCount; i++)
            {
                Assert.That(simulation.GetAgent(i).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped),
                    $"Person {i + 1} did not get out.");
            }
        }

        /// <summary>
        /// The same floor with nobody made a visitor behaves as it always did:
        /// somebody who works here never looks for anything and never learns
        /// anything, because there is nothing they do not know.
        /// </summary>
        [Test]
        public void SomebodyWhoWorksHere_NeverSearchesAndNeverLearns()
        {
            Run simulation = Start(Floor(true,
                new AgentDefinition(new SimulationId(1UL), new LogicalPosition(-2000, 10500),
                    CardinalDirection.South, AgentTraitValues.AllOrdinary)));

            RunUntilEverybodyIsOut(simulation, TwoMinutes);

            Assert.That(simulation.EventLog.Events.Any(e =>
                    e.EventType == CausalEventType.AgentLookedForAWayOut ||
                    e.EventType == CausalEventType.AgentFoundADeadEnd ||
                    e.EventType == CausalEventType.AgentFoundTheWayOut),
                Is.False);
            Assert.That(simulation.GetAgent(0).Outcome, Is.EqualTo(AgentTerminalOutcome.Escaped));
        }
    }
}

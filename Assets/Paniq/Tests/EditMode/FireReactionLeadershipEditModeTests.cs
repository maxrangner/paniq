using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Taking charge: a leader sends the strong one at a door that will not
    /// open, sends somebody for an extinguisher, and gathers the people near
    /// them; the cruel never do as they are told.
    /// </summary>
    public sealed class FireReactionLeadershipEditModeTests
    {

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

        /// <summary>Str, Spd, Brv, Cmp, Evl, Nrv, Ldr.</summary>
        private static AgentTraitValues Person(int strength, int bravery, int evil, int nervousness, int leadership)
        {
            return new AgentTraitValues(strength, 5, bravery, 5, evil, nervousness, leadership);
        }

        /// <summary>
        /// A leader and one other person in the office with the fire between
        /// them and nothing but locked doors.
        /// </summary>
        private FireReactionScenarioData LeaderAnd(AgentTraitValues other, int leadership = 9)
        {
            // A way out of the office for the leader to send somebody at.
            FireReactionScenarioData data =
                FireReactionDoorsEditModeTests.WithAWayOutOfTheOffice(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new FireReactionAgentDefinition(new SimulationId(1UL), new LogicalPosition(-2500, 3200),
                    CardinalDirection.South, Person(4, 6, 0, 4, leadership)),
                new FireReactionAgentDefinition(new SimulationId(2UL), new LogicalPosition(-1400, 3200),
                    CardinalDirection.South, other)
            };
            data.Tables = new FireReactionTableDefinition[0];
            data.PhysicsObjects = new FireReactionPhysicsObjectDefinition[0];
            data.Fire.ActivationTick = 1;
            data.Fire.SpawnBounds = new LogicalBounds(0, 0, -2000, -2000);
            data.Fire.SpreadMinimumTicks = 400;
            data.Fire.SpreadMaximumTicks = 600;
            data.Perception.MaximumReactionDelayTicks = 0;
            data.Temperament.FreezeThenRunPercent = 0;

            // Nobody wandering off next door: this is about who gets sent at a
            // door, and a stroll through a doorway is somebody else's test.
            data.Calm.StrollNextDoorPercent = 0;
            data.Temperament.FreezeForeverPercent = 0;
            return data;
        }

        [Test]
        public void DefaultCrowd_HasLeadershipAmongItsTraits()
        {
            FireReactionScenarioData data = scenario.ToRuntimeData();
            int leaders = 0;
            foreach (FireReactionAgentDefinition agent in data.Agents)
            {
                Assert.That(agent.Traits.IsValid, Is.True, $"Agent {agent.AgentId} has a trait outside 0–10.");
                leaders += agent.Traits.Leadership >= data.Leadership.LeaderMinimum ? 1 : 0;
            }

            Assert.That(leaders, Is.EqualTo(2), "One natural leader in each big room.");
        }

        /// <summary>
        /// A chair left in a doorway seals that way out for everybody. Somebody
        /// taking charge does not need to have tried the door themselves — the
        /// obstruction is there to be seen — so they send whoever is strong
        /// enough to heave it aside.
        /// </summary>
        [Test]
        public void ALeader_SendsSomebodyStrongAtAWedgedDoor()
        {
            // Strong enough to shift a bin, and biddable. The door is unlocked,
            // so the only thing wrong with it is the box sitting in the gap.
            FireReactionScenarioData data = LeaderAnd(Person(8, 5, 0, 9, 2));
            data.Doors = FireReactionDoorsEditModeTests
                .WithAWayOutOfTheOffice(scenario.ToRuntimeData(), startsLocked: false).Doors;

            // The way out's gap is centred on x = -2500 in the office's south
            // wall at z = -6000.
            // 20 kg: more than the leader (strength 4) can lift, so they cannot
            // just throw it clear themselves, but not too much for somebody
            // strong -- which is what a leader is for.
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Box,
                    new LogicalPosition(-2500, -5800), 400, 20000)
            };

            var simulation = new FireReactionSimulation(data);
            // Shifted either way: thrown clear, or heaved along the wall.
            List<CausalEvent> Shifted()
            {
                var shifted = EventsOfType(simulation, FireReactionEventType.AgentShovedObstruction);
                shifted.AddRange(EventsOfType(simulation, FireReactionEventType.ItemThrown));
                return shifted;
            }

            for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond && Shifted().Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> orders = EventsOfType(simulation, FireReactionEventType.LeaderOrderedDoorBroken);
            Assert.That(orders, Is.Not.Empty, "The leader never sent anyone at the wedged door.");
            Assert.That(orders[0].SourceId, Is.EqualTo(new SimulationId(1UL)), "The order comes from the leader.");
            Assert.That(orders[0].TargetId, Is.EqualTo(new SimulationId(2UL)), "It names who was sent.");

            Assert.That(Shifted(), Is.Not.Empty, "And they shifted the box out of the doorway.");
        }

        [Test]
        public void ALeader_SendsAStrongPersonAtADoorThatWillNotOpen()
        {
            // The other person is strong enough to break a door and biddable.
            // Which door the leader heads for first is partly the luck of the
            // run: in some runs the strong one batters a door down on their own
            // before any order is given. So several runs, and at least one of
            // them must show the order being given and obeyed.
            FireReactionScenarioData data = LeaderAnd(Person(10, 5, 0, 9, 2));
            data.Exits.DoorStrength = 12;
            FireReactionSimulation simulation = null;
            List<CausalEvent> orders = null;
            for (ulong seed = 42UL; seed <= 49UL; seed++)
            {
                simulation = new FireReactionSimulation(data, seed);
                for (int t = 0; t < 60 * FireReactionSimulation.TicksPerSecond &&
                                EventsOfType(simulation, FireReactionEventType.DoorBrokenDown).Count == 0; t++)
                {
                    simulation.Step();
                }

                orders = EventsOfType(simulation, FireReactionEventType.LeaderOrderedDoorBroken);
                if (orders.Count > 0)
                {
                    break;
                }
            }

            Assert.That(orders, Is.Not.Empty, "The leader never sent anyone at a door.");
            Assert.That(orders[0].SourceId, Is.EqualTo(new SimulationId(1UL)), "The order comes from the leader.");
            Assert.That(orders[0].TargetId, Is.EqualTo(new SimulationId(2UL)), "It names who was sent.");

            List<CausalEvent> broken = EventsOfType(simulation, FireReactionEventType.DoorBrokenDown);
            Assert.That(broken, Is.Not.Empty, "The door was never broken down.");
            Assert.That(broken[0].SourceId, Is.EqualTo(new SimulationId(2UL)), "The strong one broke it, not the leader.");
        }

        [Test]
        public void ACruelPerson_NeverDoesAsTheyAreTold()
        {
            // Strong enough to be useful, but far too nasty to take orders.
            FireReactionScenarioData data = LeaderAnd(Person(10, 5, 9, 9, 2));
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 40 * FireReactionSimulation.TicksPerSecond; t++)
            {
                simulation.Step();
            }

            foreach (CausalEvent order in EventsOfType(simulation, FireReactionEventType.LeaderOrderedDoorBroken))
            {
                Assert.That(order.TargetId, Is.Not.EqualTo(new SimulationId(2UL)),
                    "A cruel person should never be the one who obeys.");
            }

            // The leader still shouts, but with nobody willing the order is never
            // given. (A strong, cruel person may batter a locked exit down on
            // their own account in a panic; that is not doing as they are told.)
            Assert.That(EventsOfType(simulation, FireReactionEventType.LeaderOrderedDoorBroken), Is.Empty,
                "Nobody was willing to be sent at the door.");
        }

        [Test]
        public void ALeader_GathersThePeopleNearThem()
        {
            FireReactionScenarioData data = LeaderAnd(Person(5, 3, 0, 9, 1));
            var simulation = new FireReactionSimulation(data);
            bool followed = false;
            for (int t = 0; t < 30 * FireReactionSimulation.TicksPerSecond && !followed; t++)
            {
                simulation.Step();
                followed |= simulation.GetAgent(1).ActivityState == AgentActivityState.Following;
            }

            Assert.That(EventsOfType(simulation, FireReactionEventType.LeaderCalledPeopleOn), Is.Not.Empty,
                "The leader never called anyone on.");
            Assert.That(followed, Is.True, "Nobody fell in behind the leader.");
        }

        [Test]
        public void ALeader_SendsSomeoneForAnExtinguisher()
        {
            FireReactionScenarioData data = LeaderAnd(Person(5, 6, 0, 9, 2));

            // A bottle in the room, and a leader too timid to fetch it themselves.
            data.PhysicsObjects = new[]
            {
                new FireReactionPhysicsObjectDefinition(new SimulationId(3001UL), PhysicsObjectKind.Extinguisher,
                    new LogicalPosition(-3500, 3000), 250, 7000)
            };
            var simulation = new FireReactionSimulation(data);
            for (int t = 0; t < 40 * FireReactionSimulation.TicksPerSecond &&
                            EventsOfType(simulation, FireReactionEventType.LeaderOrderedFireFought).Count == 0; t++)
            {
                simulation.Step();
            }

            List<CausalEvent> orders = EventsOfType(simulation, FireReactionEventType.LeaderOrderedFireFought);
            Assert.That(orders, Is.Not.Empty, "The leader never sent anyone for the extinguisher.");
            Assert.That(orders[0].TargetId, Is.EqualTo(new SimulationId(2UL)), "It names who was sent.");
            Assert.That(simulation.EventLog.Get(orders[0].CausalParentEventId).EventType,
                Is.EqualTo(FireReactionEventType.AgentScared), "The order traces back to the leader's fright.");
        }
    }
}

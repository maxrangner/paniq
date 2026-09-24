using System;
using System.Collections.Generic;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// The crowd is afraid of a threat, not of the fire. These put something
    /// that is not fire into the world -- a still, silent thing that trips
    /// anybody who touches it -- and check that people notice it, panic, run
    /// from it, are got by it, and that the round waits for it, with no fire
    /// ever lit. This is the seam the roadmap's hunter stone needs.
    /// </summary>
    public sealed class ThreatSeamEditModeTests
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

        /// <summary>
        /// A stand-in for a threat that is not fire: a fixed spot of a given
        /// size, silent, in one room, that trips whoever touches it. It starts
        /// on a tick of its own, or when the player triggers the event.
        /// </summary>
        private sealed class StationaryThreat : IThreat
        {
            private readonly SimulationContext context;
            private readonly LogicalPosition at;
            private readonly int radius;
            private readonly int startTick;
            private readonly int room;
            private readonly int personRadius;
            private readonly bool keepsChanging;
            private bool requested;

            public StationaryThreat(SimulationContext context, LogicalPosition at, int radius, int startTick, int room,
                int personRadius, bool keepsChanging = false)
            {
                this.context = context;
                this.at = at;
                this.radius = radius;
                this.startTick = startTick;
                this.room = room;
                this.personRadius = personRadius;
                this.keepsChanging = keepsChanging;
            }

            public bool Active { get; private set; }
            public bool StartRequested => Active || requested;
            public void RequestStart() => requested = true;
            public ulong RootEventId { get; private set; }
            public int Count => Active ? 1 : 0;

            /// <summary>Either something that changes every tick, or something that never does once it is there.</summary>
            public long Signature => !Active ? 0L : keepsChanging ? context.Tick : 1L;

            public int HeardWithinMillimetres => 0;

            public void Advance()
            {
                if (Active || (!requested && context.Tick < startTick))
                {
                    return;
                }

                Active = true;

                // A test double borrows the fire's start event; a real threat
                // would name its own.
                RootEventId = context.Events.Append(context.Tick, new SimulationId(777UL),
                    CausalEventType.FireActivated, at, radius).EventId;
            }

            public long NearestDistanceSquared(LogicalPosition from, out LogicalPosition point, out ulong causeEventId)
            {
                if (!Active)
                {
                    point = from;
                    causeEventId = 0UL;
                    return long.MaxValue;
                }

                point = at;
                causeEventId = RootEventId;
                long gap = Math.Max(0L, IntegerMath.Distance(from, at) - radius);
                return gap * gap;
            }

            public bool AnyCloserThan(LogicalPosition position, int distance) =>
                NearestDistanceSquared(position, out _, out _) < (long)distance * distance;

            public bool AnyCloserThanInRooms(LogicalPosition position, int distance, int roomA, int roomB) =>
                (room == roomA || room == roomB) && AnyCloserThan(position, distance);

            public bool IsInRoom(int r) => Active && r == room;

            public bool IsVisibleFrom(LogicalPosition eye, int heading, int range)
            {
                if (!Active)
                {
                    return false;
                }

                long dx = (long)at.X - eye.X;
                long dz = (long)at.Z - eye.Z;
                if (dx * dx + dz * dz > (long)range * range)
                {
                    return false;
                }

                LogicalPosition looking = IntegerMath.Direction(heading);
                long forward = dx * looking.X + dz * looking.Z;
                long lateral = dx * looking.Z - dz * looking.X;
                return forward >= 0L && Math.Abs(lateral) <= forward;
            }

            public bool RoutePassesNear(LogicalPosition from, LogicalPosition to, int clearance)
            {
                long reach = (long)clearance + radius;
                return Active && IntegerMath.SegmentPassesWithin(from, to, at, reach * reach);
            }

            public ulong Touching(LogicalPosition position)
            {
                long reach = (long)radius + personRadius;
                return Active && LogicalPosition.DistanceSquared(position, at) <= reach * reach ? RootEventId : 0UL;
            }

            public ulong TouchingAlong(LogicalPosition from, LogicalPosition to)
            {
                long reach = (long)radius + personRadius;
                return Active && IntegerMath.SegmentPassesWithin(from, to, at, reach * reach) ? RootEventId : 0UL;
            }

            /// <summary>Touching it trips you: a visible harm that is not catching fire.</summary>
            public void Harm(Agent agent, ulong causeEventId, BodySystem body) => body.Trip(agent, causeEventId);
        }

        /// <summary>One person alone in the office, the fire never due, and nothing to do but stand there.</summary>
        private ScenarioData OnePersonAndNoFire(LogicalPosition at, CardinalDirection facing)
        {
            ScenarioData data = TheBuilding.WithTheFireInTheOffice(scenario.ToRuntimeData());
            data.Agents = new[]
            {
                new AgentDefinition(new SimulationId(1UL), at, facing, AgentTraitValues.AllOrdinary)
            };
            data.Fire.ActivationTick = int.MaxValue;
            data.Round.HazardWaitsForTrigger = false;
            data.Calm.DecisionMinimumTicks = 100000;
            data.Calm.DecisionMaximumTicks = 100000;
            data.Temperament.FreezeForeverPercent = 0;
            data.Temperament.FreezeThenRunPercent = 0;
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

        private static void Step(Run simulation, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                simulation.Step();
            }
        }

        [Test]
        public void ACalmPerson_IsFrightenedByAThreatThatIsNotFire_AndBlamesIt()
        {
            ScenarioData data = OnePersonAndNoFire(TheBuilding.Office, CardinalDirection.North);
            using (var simulation = new Run(data, 42UL))
            {
                // Two metres straight ahead of them, inside their vision cone.
                var threat = new StationaryThreat(simulation.ContextForTests, new LogicalPosition(0, 2000), 300, 5, 0,
                    data.World.OccupancyRadiusMillimetres);
                simulation.AddThreatForTests(threat);
                Step(simulation, 80);

                RunSnapshot snapshot = simulation.GetSnapshot();
                Assert.That(snapshot.FireActive, Is.False, "No fire was ever lit.");
                Assert.That(snapshot.Agents[0].FearState, Is.EqualTo(AgentFearState.Scared),
                    "Somebody who can see a threat panics, whatever the threat is.");

                List<CausalEvent> alerts = EventsOfType(simulation, CausalEventType.AgentAlerted);
                Assert.That(alerts, Is.Not.Empty);
                Assert.That(alerts[0].CausalParentEventId, Is.EqualTo(threat.RootEventId),
                    "The fright names the threat they saw as its cause.");
                Assert.That(alerts[0].Strength, Is.EqualTo(1), "How bad things are is the threat's own count.");
                Assert.That(EventsOfType(simulation, CausalEventType.AgentScared), Is.Not.Empty);
            }
        }

        [Test]
        public void AFrightenedPerson_RunsAwayFromIt()
        {
            // Facing south at a threat two metres south; the office's door is
            // north, so away from the threat and out is the same way.
            ScenarioData data = OnePersonAndNoFire(TheBuilding.Office, CardinalDirection.South);
            using (var simulation = new Run(data, 42UL))
            {
                var at = new LogicalPosition(0, -2000);
                simulation.AddThreatForTests(new StationaryThreat(simulation.ContextForTests, at, 300, 5, 0,
                    data.World.OccupancyRadiusMillimetres));
                long before = IntegerMath.Distance(simulation.GetAgent(0).Position, at);
                Step(simulation, 200);

                AgentSnapshot person = simulation.GetAgent(0);
                Assert.That(person.FearState, Is.EqualTo(AgentFearState.Scared));
                Assert.That(person.BodyState, Is.EqualTo(AgentBodyState.Upright), "They never touched it.");
                Assert.That(IntegerMath.Distance(person.Position, at), Is.GreaterThan(before + 2000),
                    "Four seconds of running should have carried them well away from it.");
            }
        }

        [Test]
        public void TouchingIt_DoesWhatTheThreatSays_HereATrip()
        {
            ScenarioData data = OnePersonAndNoFire(TheBuilding.Office, CardinalDirection.North);
            using (var simulation = new Run(data, 42UL))
            {
                // Right on top of them, from tick five.
                var threat = new StationaryThreat(simulation.ContextForTests, TheBuilding.Office, 600, 5, 0,
                    data.World.OccupancyRadiusMillimetres);
                simulation.AddThreatForTests(threat);
                Step(simulation, 6);

                List<CausalEvent> trips = EventsOfType(simulation, CausalEventType.AgentTripped);
                Assert.That(trips, Is.Not.Empty, "Touching this threat trips you.");
                Assert.That(trips[0].CausalParentEventId, Is.EqualTo(threat.RootEventId));
                Assert.That(simulation.GetAgent(0).BodyState, Is.EqualTo(AgentBodyState.Fallen));
                Assert.That(EventsOfType(simulation, CausalEventType.AgentCaughtFire), Is.Empty,
                    "Nothing about it is fire.");
            }
        }

        [TestCase(true, RoundPhase.Running)]
        [TestCase(false, RoundPhase.Over)]
        public void TheRoundClock_WatchesTheThreat(bool keepsChanging, RoundPhase expected)
        {
            // One person standing still, a threat well behind them and out of
            // sight, and a short stall clock. A threat that keeps changing is
            // something still happening; one that has settled lets the round end.
            ScenarioData data = OnePersonAndNoFire(TheBuilding.Office, CardinalDirection.North);
            data.Round.StallTicks = 50;
            using (var simulation = new Run(data, 42UL))
            {
                simulation.AddThreatForTests(new StationaryThreat(simulation.ContextForTests,
                    new LogicalPosition(-4500, -4500), 300, 5, -1, data.World.OccupancyRadiusMillimetres, keepsChanging));
                Step(simulation, 400);

                Assert.That(simulation.GetAgent(0).FearState, Is.EqualTo(AgentFearState.Calm),
                    "Out of sight and silent: they never notice it, so only the clock decides.");
                Assert.That(simulation.Phase, Is.EqualTo(expected));
            }
        }

        [Test]
        public void TheTrigger_SetsEveryThreatGoing()
        {
            ScenarioData data = OnePersonAndNoFire(TheBuilding.Office, CardinalDirection.North);
            data.Round.HazardWaitsForTrigger = true;
            using (var simulation = new Run(data, 42UL))
            {
                var threat = new StationaryThreat(simulation.ContextForTests, new LogicalPosition(-4500, -4500), 300,
                    int.MaxValue, -1, data.World.OccupancyRadiusMillimetres);
                simulation.AddThreatForTests(threat);
                Step(simulation, 20);
                Assert.That(threat.Active, Is.False, "Nothing starts until the player says so.");

                simulation.QueueCommand(PlayerCommandType.TriggerEvent, default(SimulationId), simulation.Tick + 1);
                Step(simulation, 2);
                Assert.That(threat.Active, Is.True, "The trigger starts every threat in the level.");
                Assert.That(simulation.Phase, Is.EqualTo(RoundPhase.Running));
            }
        }
    }
}

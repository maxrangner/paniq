using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using Paniq.Diagnostics;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Not a check: fills a room with far more people than the prototype has
    /// and reports how long a second of simulation takes. This is the evidence
    /// behind any claim that the simulation can carry a bigger crowd, and the
    /// numbers belong in docs/technical-decisions.md. Run it on purpose with
    /// the "Measure" category; normal test runs skip it.
    ///
    /// It only uses the ordinary public surface, so the same file can be run
    /// against an older build to get a before-and-after pair.
    /// </summary>
    [Explicit, Category("Measure")]
    public sealed class CrowdScaleMeasurements
    {
        [TestCase(20)]
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(200)]
        public void HowLongASecondOfSimulationTakes(int people)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                FireReactionScenarioData data = scenario.ToRuntimeData();
                data.Agents = PeopleInTheBuilding(data, people);

                // No fire, so the crowd stays whole for the whole window.
                // A building emptying out would time itself getting easier: at
                // a hundred people the exit bursts and everybody is gone within
                // seconds, and the number would then say more about how fast
                // they leave than about how much a crowd costs. Calm people
                // still walk, steer round each other and use the furniture,
                // which is exactly the work this measures.
                data.Fire.ActivationTick = 1000000;

                // A busy office rather than a still one: everybody picks
                // something new to do constantly, so most of them are walking
                // at any moment. Walking is what costs -- steering round other
                // people, and checking each step against everyone nearby -- and
                // a room full of people standing about would not measure it.
                data.Calm.DecisionMinimumTicks = 1;
                data.Calm.DecisionMaximumTicks = 3;

                var simulation = new FireReactionSimulation(data, 42UL);

                // Long enough that everybody has chosen something to be doing.
                for (int tick = 0; tick < 200; tick++)
                {
                    simulation.Step();
                }

                int before = StillInside(simulation, people);
                const int measured = 500;
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int tick = 0; tick < measured; tick++)
                {
                    simulation.Step();
                }

                stopwatch.Stop();
                int after = StillInside(simulation, people);

                double millisecondsPerTick = stopwatch.Elapsed.TotalMilliseconds / measured;
                var report = new StringBuilder();
                report.Append(people.ToString(CultureInfo.InvariantCulture).PadLeft(4));
                report.Append(" people: ");
                report.Append(millisecondsPerTick.ToString("0.000", CultureInfo.InvariantCulture));
                report.Append(" ms per tick, ");
                report.Append((millisecondsPerTick * FireReactionSimulation.TicksPerSecond)
                    .ToString("0.0", CultureInfo.InvariantCulture));
                report.Append(" ms of work per second of game time");

                // At 50 ticks a second the simulation has 20 ms per tick before
                // it cannot keep up at all, and it shares that with drawing.
                report.Append(millisecondsPerTick <= 20.0 ? " (keeps up)" : " (TOO SLOW)");
                report.Append($"; {before} of them still inside at the start, {after} at the end");
                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        /// <summary>
        /// The physics plan's budgets, measured in the editor: a tick of the
        /// stress building with this many people and twice as many boxes, and
        /// the physics engine's share of it. The editor is slower than a built
        /// game; the numbers that count come from the stress profile player
        /// (menu Paniq > Profiling > Build Stress Profile Player).
        /// </summary>
        [TestCase(100, 200)]
        [TestCase(200, 400)]
        [TestCase(500, 1000)]
        public void HowLongAPackedBuildingTakes(int people, int things)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                FireReactionScenarioData data = StressBuilding.Build(scenario.ToRuntimeData(), people, things);
                using (var simulation = new FireReactionSimulation(data, 42UL))
                {
                    for (int tick = 0; tick < 100; tick++)
                    {
                        simulation.Step();
                    }

                    TimeSpan physicsBefore = simulation.PhysicsStepTime;
                    const int measured = 250;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    for (int tick = 0; tick < measured; tick++)
                    {
                        simulation.Step();
                    }

                    stopwatch.Stop();
                    double perTick = stopwatch.Elapsed.TotalMilliseconds / measured;
                    double physics = (simulation.PhysicsStepTime - physicsBefore).TotalMilliseconds / measured;
                    TestContext.WriteLine(
                        $"{people} people, {things} boxes (editor): " +
                        $"{perTick.ToString("0.00", CultureInfo.InvariantCulture)} ms a tick, of which physics " +
                        $"{physics.ToString("0.00", CultureInfo.InvariantCulture)} ms");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        /// <summary>How many of the crowd are still in the building and being simulated.</summary>
        private static int StillInside(FireReactionSimulation simulation, int people)
        {
            int inside = 0;
            for (int i = 0; i < people; i++)
            {
                if (simulation.GetAgent(i).Participation == AgentParticipation.Participating)
                {
                    inside++;
                }
            }

            return inside;
        }

        /// <summary>
        /// People stood on a lattice through every room, far enough apart to
        /// start clear of each other and of the furniture already there.
        /// </summary>
        private static FireReactionAgentDefinition[] PeopleInTheBuilding(FireReactionScenarioData data, int people)
        {
            const int margin = 600;
            const int spacing = 800;
            int personRadius = data.World.OccupancyRadiusMillimetres;
            var made = new FireReactionAgentDefinition[people];
            int placed = 0;

            foreach (FireReactionRoomDefinition definition in data.Rooms)
            {
                LogicalBounds room = definition.Bounds;
                int columns = (room.MaxX - room.MinX - 2 * margin) / spacing + 1;
                int rows = (room.MaxZ - room.MinZ - 2 * margin) / spacing + 1;
                for (int spot = 0; spot < columns * rows && placed < people; spot++)
                {
                    int x = room.MinX + margin + spot % columns * spacing;
                    int z = room.MinZ + margin + spot / columns * spacing;
                    var at = new LogicalPosition(x, z);
                    if (!IsClearFloor(data, at, personRadius))
                    {
                        continue;
                    }

                    made[placed] = new FireReactionAgentDefinition(
                        new SimulationId((ulong)(9000 + placed)),
                        at,
                        CardinalDirection.North,

                        // Everybody ordinary, and authored rather than drawn:
                        // the crowd is then the same at every size, so the only
                        // thing the timings differ by is how many people there are.
                        // Strength is one short of what it takes to break a door.
                        new AgentTraitValues(
                            AgentTraitValues.Maximum - 1,
                            AgentTraitValues.Ordinary,
                            AgentTraitValues.Ordinary,
                            AgentTraitValues.Ordinary,
                            AgentTraitValues.Ordinary,
                            AgentTraitValues.Ordinary));
                    placed++;
                }
            }

            if (placed < people)
            {
                throw new InvalidOperationException(
                    $"Only {placed} clear spots in the whole building, not {people}. " +
                    "The furniture leaves less floor than the lattice assumed.");
            }

            return made;
        }

        /// <summary>Floor with no authored furniture or loose object already standing on it.</summary>
        private static bool IsClearFloor(FireReactionScenarioData data, LogicalPosition at, int personRadius)
        {
            foreach (FireReactionPhysicsObjectDefinition thing in data.PhysicsObjects)
            {
                if (thing.StartsDormant)
                {
                    continue;
                }

                long reach = (long)personRadius + thing.RadiusMillimetres;
                if (LogicalPosition.DistanceSquared(at, thing.InitialPosition) < reach * reach)
                {
                    return false;
                }
            }

            foreach (FireReactionTableDefinition table in data.Tables)
            {
                LogicalBounds bounds = table.Bounds;
                if (at.X >= bounds.MinX - personRadius && at.X <= bounds.MaxX + personRadius &&
                    at.Z >= bounds.MinZ - personRadius && at.Z <= bounds.MaxZ + personRadius)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

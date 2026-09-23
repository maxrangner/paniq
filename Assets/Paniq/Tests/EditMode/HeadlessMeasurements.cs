using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Paniq.Gameplay;
using Paniq.Simulation;

namespace Paniq.Tests.EditMode
{
    /// <summary>
    /// Not a check: runs the default scenario headless for seeds 40–46 and
    /// prints how often each event happened, for the numbers recorded in
    /// docs/technical-decisions.md. Run it on purpose with the "Measure"
    /// category; normal test runs skip it.
    /// </summary>
    [Explicit, Category("Measure")]
    public sealed class HeadlessMeasurements
    {
        [TestCase(false, 1000)]
        [TestCase(true, 1000)]
        [TestCase(false, 3000)]
        public void CountEventsForSeeds40To46(bool openAllDoors, int ticks)
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                var report = new StringBuilder();
                report.AppendLine($"Doors {(openAllDoors ? "all opened at 5 s" : "locked")}, first {ticks / FireReactionSimulation.TicksPerSecond} s:");
                for (ulong seed = 40UL; seed <= 46UL; seed++)
                {
                    // The "doors all opened" half clicks every door twice, and a
                    // round opens with an empty purse, so without a purse behind
                    // the player the clicks were refused and both halves of this
                    // measurement printed the same run.
                    FireReactionScenarioData data = openAllDoors
                        ? TheBuilding.WithThePlayerAbleToAct(scenario.ToRuntimeData())
                        : scenario.ToRuntimeData();
                    var simulation = new FireReactionSimulation(data, seed);
                    if (openAllDoors)
                    {
                        for (int d = 0; d < data.Doors.Length; d++)
                        {
                            simulation.QueueCommand(PlayerCommandType.ClickDoor, data.Doors[d].DoorId, 250);
                            simulation.QueueCommand(PlayerCommandType.ClickDoor, data.Doors[d].DoorId, 251);
                        }
                    }

                    for (int tick = 0; tick < ticks; tick++)
                    {
                        simulation.Step();
                    }

                    var counts = new SortedDictionary<string, int>();
                    foreach (CausalEvent record in simulation.EventLog.Events)
                    {
                        string key = record.EventType.ToString();
                        counts.TryGetValue(key, out int count);
                        counts[key] = count + 1;
                    }

                    report.Append($"  seed {seed}:");
                    foreach (KeyValuePair<string, int> pair in counts)
                    {
                        if (pair.Key != "FireSpread")
                        {
                            report.Append($" {pair.Key}={pair.Value}");
                        }
                    }

                    report.AppendLine();
                }

                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        /// <summary>
        /// The one number the new economy stands or falls on: how long the
        /// player sits on their hands. A round opens with an empty purse, and
        /// the building's one way out costs unlocking plus opening, so this is
        /// the wait before the player can do the first thing they will want to
        /// do. It also prints the first card dealt and when, and the purse at
        /// half a minute and at a minute.
        /// </summary>
        [Test]
        public void HowLongBeforeThePlayerCanOpenTheWayOut()
        {
            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            try
            {
                var report = new StringBuilder();
                FireReactionScenarioData costs = scenario.ToRuntimeData();
                int wayOut = costs.Influence.UnlockDoorCost + costs.Influence.OpenDoorCost;
                report.AppendLine($"The way out costs {wayOut}. A card costs {costs.Influence.CardCost}.");

                for (ulong seed = 40UL; seed <= 46UL; seed++)
                {
                    FireReactionScenarioData data = scenario.ToRuntimeData();
                    var simulation = new FireReactionSimulation(data, seed);
                    int affordedAt = -1;
                    int firstCardAt = -1;
                    int atThirtySeconds = 0;

                    for (int tick = 0; tick < 3000; tick++)
                    {
                        simulation.Step();
                        if (affordedAt < 0 && simulation.Influence >= wayOut)
                        {
                            affordedAt = tick;
                        }

                        if (firstCardAt < 0 && simulation.GetSnapshot().Hand.Count > 0)
                        {
                            firstCardAt = tick;
                        }

                        if (tick == 30 * FireReactionSimulation.TicksPerSecond - 1)
                        {
                            atThirtySeconds = simulation.Influence;
                        }
                    }

                    report.AppendLine(
                        $"  seed {seed}: way out affordable at {Seconds(affordedAt)}, " +
                        $"first card at {Seconds(firstCardAt)}, " +
                        $"purse {atThirtySeconds} at 30 s and {simulation.Influence} at 60 s, " +
                        $"{simulation.GetSnapshot().Hand.Count} cards in hand");
                    simulation.Dispose();
                }

                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scenario);
            }
        }

        private static string Seconds(int tick)
        {
            return tick < 0 ? "never" : $"{tick / (float)FireReactionSimulation.TicksPerSecond:0.0} s";
        }
    }
}

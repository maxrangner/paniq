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
                    FireReactionScenarioData data = scenario.ToRuntimeData();
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
    }
}

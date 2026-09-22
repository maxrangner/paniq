using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Paniq.Gameplay;
using Paniq.Presentation;
using Paniq.Simulation;
using UnityEngine;

namespace Paniq.Diagnostics
{
    /// <summary>
    /// The one thing in the stress-profile player (menu Paniq > Profiling >
    /// Build Stress Profile Player). It measures what the physics plan set
    /// budgets for, on the machine it runs on, and writes a report:
    ///
    /// 1. A tick of the simulation, and the physics engine's share of it, with
    ///    100, 200 and 500 people and twice as many boxes in the stress
    ///    building. Nothing is drawn while this runs.
    /// 2. What a frame costs with no effects, then under a storm of particle
    ///    effects far worse than any real moment: several TNT-sized bangs every
    ///    frame, dozens of burning things, a burning floor and six extinguishers.
    ///
    /// The report goes to the file named after -paniqProfileOut on the command
    /// line, or stress-profile.txt beside the player. Then the player quits.
    /// </summary>
    public sealed class StressProfiler : MonoBehaviour
    {
        private const int WarmUpTicks = 100;
        private const int MeasuredTicks = 250;
        private const int MeasuredFrames = 300;

        private static readonly (int People, int Things)[] Crowds = { (100, 200), (200, 400), (500, 1000) };

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var report = new StringBuilder();
            report.AppendLine("Paniq stress profile");
            report.AppendLine($"When: {DateTime.Now:yyyy-MM-dd HH:mm}");
            report.AppendLine($"Build: Unity {Application.unityVersion}, {(Application.isEditor ? "EDITOR (not a standalone number)" : "standalone player")}, " +
                              $"{(UnityEngine.Debug.isDebugBuild ? "development build" : "release build")}");
            report.AppendLine($"Machine: {SystemInfo.processorType} ({SystemInfo.processorCount} threads), " +
                              $"{SystemInfo.systemMemorySize} MB, {SystemInfo.graphicsDeviceName}");
            report.AppendLine();
            report.AppendLine("Simulation, stress building, no fire, everybody busy (budgets: physics <= 3 ms a tick at 500 people; whole tick <= 5 ms at 200):");

            FireReactionScenario scenario = FireReactionScenario.CreateDefault();
            FireReactionScenarioData template = scenario.ToRuntimeData();
            foreach ((int people, int things) in Crowds)
            {
                string line;
                try
                {
                    line = MeasureSimulation(template, people, things);
                }
                catch (Exception failure)
                {
                    line = $"  {people} people, {things} boxes: FAILED - {failure.Message}";
                }

                report.AppendLine(line);
                yield return null;
            }

            Destroy(scenario);
            report.AppendLine();

            // A plain view to draw the particles into.
            var cameraObject = new GameObject("Stress camera", typeof(Camera));
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 14f, -14f), Quaternion.Euler(45f, 0f, 0f));
            Camera view = cameraObject.GetComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.1f, 0.11f, 0.14f);

            var holder = new GameObject("Particle stress");
            var effects = new ParticleEffects(null, holder.transform);
            var baseline = new List<float>();
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            for (int frame = 0; frame < MeasuredFrames; frame++)
            {
                yield return null;
                baseline.Add(Time.unscaledDeltaTime * 1000f);
            }

            var storm = new List<float>();
            var emitting = new List<float>();
            int mostAlive = 0;
            var flameCarries = new float[40];
            var floorCarries = new float[60];
            var sprayCarries = new float[6];
            for (int frame = 0; frame < MeasuredFrames + 30; frame++)
            {
                Stopwatch emit = Stopwatch.StartNew();
                effects.BeginFrame();
                float delta = Time.unscaledDeltaTime;
                for (int bang = 0; bang < 3; bang++)
                {
                    float angle = (frame * 3 + bang) * 2.3f;
                    effects.Bang(new Vector3(Mathf.Cos(angle) * 5f, 0.5f, Mathf.Sin(angle) * 5f), 4f,
                        (ulong)(frame * 10 + bang + 1));
                }

                for (int f = 0; f < flameCarries.Length; f++)
                {
                    effects.Flames(new Vector3(f % 8 - 4f, 0.4f, f / 8 - 2f), 0.2f, 0.8f, 0.15f, delta, ref flameCarries[f]);
                }

                for (int c = 0; c < floorCarries.Length; c++)
                {
                    effects.BurningFloor(new Vector3(c % 10 * 0.25f - 6f, 0f, c / 10 * 0.25f), 0.1f, delta, ref floorCarries[c]);
                }

                for (int s = 0; s < sprayCarries.Length; s++)
                {
                    effects.Spray(new Vector3(s - 3f, 0.55f, -4f), Vector3.forward, delta, ref sprayCarries[s]);
                }

                emit.Stop();
                yield return null;
                if (frame >= 30)
                {
                    storm.Add(Time.unscaledDeltaTime * 1000f);
                    emitting.Add((float)emit.Elapsed.TotalMilliseconds);
                    mostAlive = Math.Max(mostAlive, effects.LiveParticles);
                }
            }

            float quiet = Average(baseline);
            float busy = Average(storm);
            report.AppendLine("Particles, worst-case storm (budget: <= 2 ms a frame):");
            report.AppendLine($"  frame with no effects: {Format(quiet)} ms average, {Format(Percentile(baseline, 0.95f))} ms at the 95th percentile");
            report.AppendLine($"  frame in the storm:    {Format(busy)} ms average, {Format(Percentile(storm, 0.95f))} ms at the 95th percentile");
            report.AppendLine($"  particles' share:      {Format(busy - quiet)} ms a frame ({(busy - quiet <= 2f ? "within" : "OVER")} budget); " +
                              $"our own emitting code {Format(Average(emitting))} ms of that; at most {mostAlive} particles alive " +
                              $"(budget {effects.Settings.LiveParticleBudget})");
            effects.Dispose();

            string path = OutputPath();
            File.WriteAllText(path, report.ToString());
            UnityEngine.Debug.Log($"Paniq stress profile written to {path}\n{report}");
            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        private static string MeasureSimulation(FireReactionScenarioData template, int people, int things)
        {
            using (var simulation = new FireReactionSimulation(StressBuilding.Build(template, people, things), 42UL))
            {
                for (int tick = 0; tick < WarmUpTicks; tick++)
                {
                    simulation.Step();
                }

                TimeSpan physicsBefore = simulation.PhysicsStepTime;
                double worst = 0;
                Stopwatch whole = Stopwatch.StartNew();
                for (int tick = 0; tick < MeasuredTicks; tick++)
                {
                    long started = Stopwatch.GetTimestamp();
                    simulation.Step();
                    worst = Math.Max(worst, (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                }

                whole.Stop();
                double perTick = whole.Elapsed.TotalMilliseconds / MeasuredTicks;
                double physics = (simulation.PhysicsStepTime - physicsBefore).TotalMilliseconds / MeasuredTicks;
                return $"  {people,3} people, {things,4} boxes: {Format(perTick)} ms a tick, of which physics {Format(physics)} ms; " +
                       $"slowest tick {Format(worst)} ms";
            }
        }

        private static string OutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (arguments[i] == "-paniqProfileOut")
                {
                    return arguments[i + 1];
                }
            }

            return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "stress-profile.txt");
        }

        private static float Average(List<float> values)
        {
            float total = 0f;
            foreach (float value in values)
            {
                total += value;
            }

            return values.Count == 0 ? 0f : total / values.Count;
        }

        private static float Percentile(List<float> values, float share)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            var sorted = new List<float>(values);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(share * sorted.Count) - 1, 0, sorted.Count - 1)];
        }

        private static string Format(double milliseconds) => milliseconds.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

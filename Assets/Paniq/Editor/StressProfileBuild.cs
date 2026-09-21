using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Paniq.Editor
{
    /// <summary>
    /// Builds a small standalone Windows player that only measures: see
    /// <c>Paniq.Diagnostics.StressProfiler</c>, the one thing in
    /// Scenes/StressProfile.unity. Standalone, because the editor adds its own
    /// overhead and its numbers flatter nobody. The player lands in
    /// Builds/StressProfile (not tracked by Git); run it and it writes
    /// stress-profile.txt beside itself, then quits. What happened is written
    /// to Temp/PaniqTestBridge/build.txt, because Unity swallows an error in a
    /// menu command.
    /// </summary>
    internal static class StressProfileBuild
    {
        private const string ScenePath = "Assets/Paniq/Scenes/StressProfile.unity";
        private const string PlayerPath = "Builds/StressProfile/StressProfile.exe";
        private const string SummaryPath = "Temp/PaniqTestBridge/build.txt";

        [MenuItem("Paniq/Profiling/Build Stress Profile Player")]
        private static void Build()
        {
            string summary;
            try
            {
                if (!File.Exists(ScenePath))
                {
                    throw new FileNotFoundException("The stress profile scene is missing.", ScenePath);
                }

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = PlayerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });

                summary = $"result={report.summary.result}\n" +
                          $"player={Path.GetFullPath(PlayerPath)}\n" +
                          $"seconds={report.summary.totalTime.TotalSeconds:0}\n" +
                          $"errors={report.summary.totalErrors}\n";
            }
            catch (Exception failure)
            {
                summary = $"result=Failed\nerror={failure.Message}\n";
                Debug.LogException(failure);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
            File.WriteAllText(SummaryPath, summary);
            Debug.Log("Paniq: stress profile player build finished.\n" + summary);
        }
    }
}

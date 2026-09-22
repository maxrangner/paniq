using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Paniq.Editor
{
    /// <summary>
    /// Runs Unity's own test runner inside the editor that is already open,
    /// on request from outside it. Unity's batch-mode runner refuses to open a
    /// project the editor has open, and the physics tests need Unity itself,
    /// so this is how they run without asking anyone to close the editor.
    ///
    /// A request is a file, Temp/PaniqTestBridge/request.txt, written by
    /// tools/RunUnityTests.ps1. Its lines are key=value pairs: id, mode
    /// (EditMode or PlayMode), filter (part of a test's full name) and
    /// category. The bridge refreshes the asset database first so any code
    /// changed since the last compile is compiled, then runs the tests and
    /// writes Temp/PaniqTestBridge/result.txt, ending with a "done=id" line.
    /// A compile failure is reported in the same file instead of running.
    ///
    /// State that must survive the domain reload a compile causes lives in
    /// SessionState, which Unity keeps for the life of the editor process.
    /// </summary>
    [InitializeOnLoad]
    internal static class TestBridge
    {
        private const string Folder = "Temp/PaniqTestBridge";
        private const string RequestPath = Folder + "/request.txt";
        private const string ResultPath = Folder + "/result.txt";
        private const string StatusPath = Folder + "/status.txt";
        private const string CompileLogPath = Folder + "/compile.txt";

        /// <summary>Dropped by tools\RunUnityTests.ps1 -Reset: forget a run that will never report back.</summary>
        private const string ResetPath = Folder + "/reset.txt";

        private const string PendingKey = "Paniq.TestBridge.Pending";
        private const string RunningKey = "Paniq.TestBridge.Running";
        private const string RefreshedKey = "Paniq.TestBridge.Refreshed";

        private static double nextPoll;

        static TestBridge()
        {
            Directory.CreateDirectory(Folder);
            EditorApplication.update += Poll;
            CompilationPipeline.compilationStarted += _ => File.WriteAllText(CompileLogPath, string.Empty);
            CompilationPipeline.assemblyCompilationFinished += RecordCompilerMessages;

            // Unity keeps callbacks only for the domain they were registered
            // in, and a play-mode run reloads the domain, so register on
            // every load and let the callbacks check whether a run is ours.
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;

            if (File.Exists(ResetPath))
            {
                File.Delete(ResetPath);
                SessionState.SetBool(RunningKey, false);
                SessionState.EraseString(PendingKey);
                WriteStatus("idle");
            }

            if (SessionState.GetBool(RunningKey, false)) return;

            if (string.IsNullOrEmpty(SessionState.GetString(PendingKey, string.Empty)))
            {
                if (!File.Exists(RequestPath)) return;
                string request = File.ReadAllText(RequestPath);
                File.Delete(RequestPath);
                SessionState.SetString(PendingKey, request);
                SessionState.SetBool(RefreshedKey, false);
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            if (!SessionState.GetBool(RefreshedKey, false))
            {
                SessionState.SetBool(RefreshedKey, true);
                WriteStatus("compiling");
                AssetDatabase.Refresh();
                return;
            }

            Dictionary<string, string> fields = Parse(SessionState.GetString(PendingKey, string.Empty));
            SessionState.EraseString(PendingKey);
            string id = Get(fields, "id", "unknown");

            if (EditorUtility.scriptCompilationFailed)
            {
                string messages = File.Exists(CompileLogPath) ? File.ReadAllText(CompileLogPath) : string.Empty;
                File.WriteAllText(ResultPath, "compile=failed\n" + messages + "done=" + id + "\n");
                WriteStatus("idle");
                return;
            }

            if (EditorApplication.isPlaying)
            {
                File.WriteAllText(ResultPath, "error=The editor is in play mode; stop it and try again.\ndone=" + id + "\n");
                WriteStatus("idle");
                return;
            }

            if (Get(fields, "mode", string.Empty) == "Menu")
            {
                RunMenuItem(Get(fields, "menu", string.Empty), id);
                return;
            }

            TestMode mode = Get(fields, "mode", "EditMode") == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
            var filter = new Filter { testMode = mode };
            string nameFilter = Get(fields, "filter", string.Empty);
            if (nameFilter.Length > 0) filter.groupNames = new[] { System.Text.RegularExpressions.Regex.Escape(nameFilter) };
            string category = Get(fields, "category", string.Empty);
            if (category.Length > 0) filter.categoryNames = new[] { category };

            SessionState.SetString(Callbacks.IdKey, id);
            SessionState.SetBool(RunningKey, true);
            File.WriteAllText(ResultPath, string.Empty);
            WriteStatus("running " + mode);
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
        }

        /// <summary>
        /// Runs one of the editor's menu commands, such as building the stress
        /// profile player, and reports whether it ran. The command writes its
        /// own results wherever it says it does.
        /// </summary>
        private static void RunMenuItem(string item, string id)
        {
            WriteStatus("running menu " + item);
            string outcome;
            try
            {
                outcome = EditorApplication.ExecuteMenuItem(item)
                    ? "menu=ran\n"
                    : "error=There is no menu command called '" + item + "'.\n";
            }
            catch (System.Exception failure)
            {
                outcome = "error=" + failure.Message.Replace('\r', ' ').Replace('\n', ' ') + "\n";
            }

            File.WriteAllText(ResultPath, outcome + "done=" + id + "\n");
            WriteStatus("idle");
        }

        private static void RecordCompilerMessages(string assembly, CompilerMessage[] messages)
        {
            var text = new StringBuilder();
            foreach (CompilerMessage message in messages)
            {
                if (message.type != CompilerMessageType.Error) continue;
                text.Append("error=").Append(message.message.Replace('\n', ' ')).Append('\n');
            }

            if (text.Length > 0) File.AppendAllText(CompileLogPath, text.ToString());
        }

        private static void WriteStatus(string status) => File.WriteAllText(StatusPath, status + "\n");

        private static Dictionary<string, string> Parse(string text)
        {
            var fields = new Dictionary<string, string>();
            foreach (string line in text.Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals > 0) fields[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }

            return fields;
        }

        private static string Get(Dictionary<string, string> fields, string key, string fallback) =>
            fields.TryGetValue(key, out string value) ? value : fallback;

        private sealed class Callbacks : IErrorCallbacks
        {
            internal const string IdKey = "Paniq.TestBridge.Id";

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(RunningKey, false) || result.HasChildren) return;

                string status = result.TestStatus switch
                {
                    TestStatus.Passed => "PASSED",
                    TestStatus.Failed => "FAILED",
                    TestStatus.Skipped => "SKIPPED",
                    _ => "INCONCLUSIVE"
                };

                var line = new StringBuilder();
                line.Append(status).Append('\t').Append(result.FullName).Append('\t')
                    .Append(result.Duration.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                if (result.TestStatus != TestStatus.Passed && !string.IsNullOrEmpty(result.Message))
                    line.Append('\t').Append(result.Message.Replace('\r', ' ').Replace('\n', ' '));
                if (result.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(result.StackTrace))
                    line.Append("\tat ").Append(FirstFrame(result.StackTrace));
                if (!string.IsNullOrEmpty(result.Output))
                    foreach (string output in result.Output.Split('\n'))
                        if (output.Trim().Length > 0) line.Append("\n  | ").Append(output.TrimEnd());
                line.Append('\n');
                File.AppendAllText(ResultPath, line.ToString());
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(RunningKey, false)) return;

                SessionState.SetBool(RunningKey, false);
                File.AppendAllText(ResultPath,
                    $"passed={result.PassCount}\nfailed={result.FailCount}\nskipped={result.SkipCount}\n" +
                    $"inconclusive={result.InconclusiveCount}\ndone={SessionState.GetString(IdKey, "unknown")}\n");
                WriteStatus("idle");
            }

            /// <summary>
            /// Unity gave up on the run before any result, for example when a
            /// dialog interrupted it. Without this the bridge would wait forever.
            /// </summary>
            public void OnError(string message)
            {
                if (!SessionState.GetBool(RunningKey, false)) return;

                SessionState.SetBool(RunningKey, false);
                File.AppendAllText(ResultPath,
                    "error=" + message.Replace('\r', ' ').Replace('\n', ' ') +
                    "\ndone=" + SessionState.GetString(IdKey, "unknown") + "\n");
                WriteStatus("idle");
            }

            private static string FirstFrame(string stackTrace)
            {
                foreach (string frame in stackTrace.Split('\n'))
                    if (frame.Contains("Paniq")) return frame.Trim();
                return stackTrace.Split('\n')[0].Trim();
            }
        }
    }
}

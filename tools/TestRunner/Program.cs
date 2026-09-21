// Runs Paniq's edit-mode tests outside the Unity editor.
//
// Why this exists: Unity's own batch-mode test runner cannot open the project
// while the editor has it open (Temp/UnityLockfile), which is the normal state
// while working. The simulation is plain C# -- it touches one Unity type, and
// only as a serialization marker -- so it can be compiled and run on its own.
// See docs/development-workflow.md.
//
// This is a deliberately small stand-in for NUnit's runner: it finds [Test] and
// [TestCase] methods by reflection and drives them with their [SetUp] and
// [TearDown]. It is NOT a substitute for running Unity's own runners before
// calling a change verified in the engine.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Internal;

internal static class Program
{
    /// <summary>
    /// Tests that cannot run outside the editor, and why. They are reported as
    /// skipped -- never as passed -- so a green run here is never mistaken for
    /// full coverage.
    /// </summary>
    private static readonly (string Test, string Reason)[] CannotRunOutsideUnity =
    {
        ("BootstrapperEditModeTests",
            "loads Unity scenes through Paniq.App"),
        ("SimulationContractEditModeTests.FixedTimestep_MatchesTheReplayCompatibleSimulationContract",
            "reads Time.fixedDeltaTime from Unity's settings (the runner script checks TimeManager.asset instead)"),
        ("FireReactionSimulationEditModeTests.ScenarioAsset_MatchesTheCodeDefaults",
            "loads the .asset through AssetDatabase (the runner reads the saved YAML directly instead)"),
    };

    /// <summary>Where the repository is, so the scenario asset can be read. Set by --repo.</summary>
    private static string repositoryRoot;

    /// <summary>Whether to run the tests marked [Explicit], which are measurements rather than checks.</summary>
    private static bool includeExplicit;

    /// <summary>Pulls the run's own fingerprint out of the message the test writes on a mismatch.</summary>
    private static readonly Regex ActualFingerprint =
        new Regex("fingerprint is (0x[0-9A-F]{16}UL)", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        string filter = null;
        bool fingerprintsOnly = false;
        bool record = false;
        bool list = false;

        foreach (string argument in args)
        {
            if (argument.StartsWith("--filter=", StringComparison.Ordinal))
            {
                filter = argument.Substring("--filter=".Length);
            }
            else if (argument == "--fingerprints-only")
            {
                fingerprintsOnly = true;
            }
            else if (argument == "--record")
            {
                record = true;
                fingerprintsOnly = true;
            }
            else if (argument == "--include-explicit")
            {
                includeExplicit = true;
            }
            else if (argument == "--list")
            {
                list = true;
            }
            else if (argument.StartsWith("--repo=", StringComparison.Ordinal))
            {
                repositoryRoot = argument.Substring("--repo=".Length);
            }
            else
            {
                Console.Error.WriteLine("Unknown argument: " + argument);
                Console.Error.WriteLine("Usage: [--filter=<substring>] [--fingerprints-only] [--record] [--list]");
                return 2;
            }
        }

        if (fingerprintsOnly)
        {
            filter = "ReplayFingerprint";
        }

        List<TestCaseToRun> cases = Discover(filter);
        if (list)
        {
            foreach (TestCaseToRun test in cases)
            {
                Console.WriteLine(test.Name);
            }

            return 0;
        }

        if (cases.Count == 0)
        {
            Console.Error.WriteLine(filter == null
                ? "No tests found. The build probably did not include the test files."
                : "No tests matched: " + filter);
            return 2;
        }

        return Run(cases, record);
    }

    private static List<TestCaseToRun> Discover(string filter)
    {
        var found = new List<TestCaseToRun>();
        Type[] types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type type in types)
        {
            if ((HasAttribute<ExplicitAttribute>(type) && !includeExplicit) || HasAttribute<IgnoreAttribute>(type))
            {
                continue;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToArray();

            foreach (MethodInfo method in methods)
            {
                if ((HasAttribute<ExplicitAttribute>(method) && !includeExplicit) ||
                    HasAttribute<IgnoreAttribute>(method))
                {
                    continue;
                }

                TestCaseAttribute[] testCases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
                bool plainTest = HasAttribute<TestAttribute>(method);
                if (!plainTest && testCases.Length == 0)
                {
                    continue;
                }

                if (plainTest && method.GetParameters().Length == 0)
                {
                    Add(found, filter, new TestCaseToRun(type, method, null));
                }

                foreach (TestCaseAttribute testCase in testCases)
                {
                    Add(found, filter, new TestCaseToRun(type, method, testCase.Arguments));
                }
            }
        }

        return found;
    }

    private static void Add(List<TestCaseToRun> into, string filter, TestCaseToRun test)
    {
        if (NeedsTheEditor(test))
        {
            return;
        }

        if (filter == null || test.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            into.Add(test);
        }
    }

    /// <summary>
    /// True for a test named in <see cref="CannotRunOutsideUnity"/>, whether it
    /// was named as a whole fixture or as one method. Such a test is never run
    /// and never counted; it is listed as skipped at the end of the report.
    /// </summary>
    private static bool NeedsTheEditor(TestCaseToRun test)
    {
        foreach ((string named, string _) in CannotRunOutsideUnity)
        {
            if (test.Type.Name == named ||
                test.Name == named ||
                test.Name.StartsWith(named + ".", StringComparison.Ordinal) ||
                test.Name.StartsWith(named + "(", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int Run(List<TestCaseToRun> cases, bool record)
    {
        int passed = 0;
        var failures = new List<KeyValuePair<TestCaseToRun, Exception>>();
        var recorded = new List<string>();
        bool assetDrifted = false;
        Stopwatch stopwatch = Stopwatch.StartNew();

        // A few tests report a measurement through TestContext.WriteLine, which
        // writes into the running test's result. NUnit's own runner supplies
        // one; here we have to.
        var suite = new TestSuite("Paniq.Tests.Headless");
        TestExecutionContext.CurrentContext.CurrentTest = suite;

        foreach (TestCaseToRun test in cases)
        {
            TestExecutionContext.CurrentContext.CurrentResult = suite.MakeTestResult();
            Exception failure = Execute(test);
            string reported = TestExecutionContext.CurrentContext.CurrentResult.Output;
            if (!string.IsNullOrWhiteSpace(reported))
            {
                foreach (string line in reported.Split('\n'))
                {
                    if (line.Trim().Length > 0)
                    {
                        Console.WriteLine("        " + line.TrimEnd());
                    }
                }
            }

            if (failure == null)
            {
                passed++;
                if (record)
                {
                    recorded.Add(test.AsTestCaseLine(null));
                }

                continue;
            }

            failures.Add(new KeyValuePair<TestCaseToRun, Exception>(test, failure));
            if (record)
            {
                Match match = ActualFingerprint.Match(failure.Message ?? string.Empty);
                recorded.Add(match.Success
                    ? test.AsTestCaseLine(match.Groups[1].Value)
                    : "// COULD NOT RECORD " + test.Name + ": " + Summarise(failure));
            }
        }

        stopwatch.Stop();

        foreach (KeyValuePair<TestCaseToRun, Exception> entry in failures)
        {
            Exception failure = entry.Value;
            Console.WriteLine();
            Console.WriteLine("FAILED  " + entry.Key.Name);
            foreach (string line in (failure.Message ?? "(no message)").Split('\n'))
            {
                Console.WriteLine("        " + line.TrimEnd());
            }

            if (!(failure is AssertionException))
            {
                Console.WriteLine("        [" + failure.GetType().Name + "]");
                Console.WriteLine(Indent(failure.StackTrace, "        "));
            }
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} passed, {1} failed, {2} run in {3:0.0}s.",
            passed, failures.Count, cases.Count, stopwatch.Elapsed.TotalSeconds));

        // Stands in for ScenarioAsset_MatchesTheCodeDefaults: the saved copy of
        // the scenario must still match the code it was generated from.
        if (repositoryRoot != null && !record)
        {
            Console.WriteLine();
            List<string> drift = ScenarioAssetCheck.Run(repositoryRoot, out int valuesCompared);
            if (drift.Count == 0 && valuesCompared > 100)
            {
                Console.WriteLine("Scenario asset matches the code defaults (" + valuesCompared + " values compared).");
            }
            else if (drift.Count == 0)
            {
                Console.WriteLine("FAILED  The scenario asset comparison walked only " + valuesCompared +
                    " values; it may have stopped finding the settings.");
                assetDrifted = true;
            }
            else
            {
                Console.WriteLine("FAILED  The scenario asset is out of step with the code defaults.");
                Console.WriteLine("        Regenerate it in Unity: Paniq > Rewrite Scenario Asset.");
                foreach (string difference in drift.Take(20))
                {
                    Console.WriteLine("        " + difference);
                }

                if (drift.Count > 20)
                {
                    Console.WriteLine("        ...and " + (drift.Count - 20) + " more.");
                }

                assetDrifted = true;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Skipped -- these need the Unity editor and were NOT checked here:");
        foreach ((string test, string reason) in CannotRunOutsideUnity)
        {
            Console.WriteLine("  " + test);
            Console.WriteLine("      " + reason);
        }

        if (record)
        {
            Console.WriteLine();
            Console.WriteLine("Re-recorded fingerprints. Paste into ReplayFingerprintEditModeTests.cs, and bump");
            Console.WriteLine("SimulationCompatibilityVersion and ContentRevision in the same commit:");
            Console.WriteLine();
            foreach (string line in recorded)
            {
                Console.WriteLine("        " + line);
            }
        }

        return failures.Count == 0 && !assetDrifted ? 0 : 1;
    }

    /// <summary>Runs one case with its [SetUp] and [TearDown]; returns the failure, or null when it passed.</summary>
    private static Exception Execute(TestCaseToRun test)
    {
        object instance;
        try
        {
            instance = Activator.CreateInstance(test.Type);
        }
        catch (Exception construction)
        {
            return Unwrap(construction);
        }

        Exception outcome = null;
        try
        {
            foreach (MethodInfo setUp in Lifecycle<SetUpAttribute>(test.Type))
            {
                setUp.Invoke(instance, null);
            }

            test.Method.Invoke(instance, test.Arguments);
        }
        catch (Exception failure)
        {
            outcome = Unwrap(failure);
        }
        finally
        {
            foreach (MethodInfo tearDown in Lifecycle<TearDownAttribute>(test.Type))
            {
                try
                {
                    tearDown.Invoke(instance, null);
                }
                catch (Exception cleanup)
                {
                    if (outcome == null)
                    {
                        outcome = Unwrap(cleanup);
                    }
                }
            }
        }

        return outcome;
    }

    private static IEnumerable<MethodInfo> Lifecycle<T>(Type type) where T : Attribute
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<T>() != null && m.GetParameters().Length == 0)
            .OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    /// <summary>Reflection wraps whatever a test threw; the inner exception is the real one.</summary>
    private static Exception Unwrap(Exception exception)
    {
        var invocation = exception as TargetInvocationException;
        return invocation != null && invocation.InnerException != null
            ? invocation.InnerException
            : exception;
    }

    private static string Summarise(Exception failure)
    {
        string message = (failure.Message ?? failure.GetType().Name).Replace('\n', ' ').Replace('\r', ' ');
        return message.Length > 160 ? message.Substring(0, 160) + "..." : message;
    }

    private static string Indent(string text, string prefix)
    {
        if (string.IsNullOrEmpty(text))
        {
            return prefix + "(no stack trace)";
        }

        return string.Join(Environment.NewLine, text.Split('\n').Select(line => prefix + line.TrimEnd()));
    }

    private static bool HasAttribute<T>(MemberInfo member) where T : Attribute
    {
        return member.GetCustomAttribute<T>() != null;
    }

    private sealed class TestCaseToRun
    {
        public TestCaseToRun(Type type, MethodInfo method, object[] arguments)
        {
            Type = type;
            Method = method;
            Arguments = arguments;
        }

        public Type Type { get; }
        public MethodInfo Method { get; }
        public object[] Arguments { get; }

        public string Name
        {
            get
            {
                return Arguments == null || Arguments.Length == 0
                    ? Type.Name + "." + Method.Name
                    : Type.Name + "." + Method.Name + "(" + string.Join(", ", Arguments.Select(Literal)) + ")";
            }
        }

        /// <summary>
        /// This case written as source, optionally with its last argument --
        /// the expected fingerprint -- replaced by the value the run produced.
        /// </summary>
        public string AsTestCaseLine(string replacementForLastArgument)
        {
            if (Arguments == null || Arguments.Length == 0)
            {
                return "// " + Method.Name + " takes no arguments and cannot be recorded as a [TestCase].";
            }

            string[] written = Arguments.Select(Literal).ToArray();
            if (replacementForLastArgument != null)
            {
                written[written.Length - 1] = replacementForLastArgument;
            }

            return "[TestCase(" + string.Join(", ", written) + ")]";
        }

        private static string Literal(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is ulong)
            {
                ulong number = (ulong)value;
                return number > 0xFFFFUL
                    ? string.Format(CultureInfo.InvariantCulture, "0x{0:X16}UL", number)
                    : number.ToString(CultureInfo.InvariantCulture) + "UL";
            }

            if (value is string)
            {
                return "\"" + value + "\"";
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}

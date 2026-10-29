using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BoLike.Editor
{
    /// <summary>
    /// Runs every EditMode test when Builds/test-request.txt appears (written from the Mac terminal), and writes
    /// Builds/test-results.txt with a one-line summary plus each failure, so QA gets Test Runner results without
    /// anyone clicking Run All. Same stage handling as BuildRequestWatcher so a pull with script changes still works.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRequestWatcher
    {
        const string RequestPath = "Builds/test-request.txt";
        const string ResultPath = "Builds/test-results.txt";
        static double nextCheck;
        static bool running;

        static TestRequestWatcher() => EditorApplication.update += Tick;

        static void Tick()
        {
            if (running || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;

            string stage;
            try { stage = File.ReadAllText(RequestPath).Trim(); } catch { return; }
            if (stage == "requested")
            {
                File.WriteAllText(RequestPath, "refreshed");
                AssetDatabase.Refresh();
                return;
            }
            if (stage != "refreshed") return;

            try { File.Delete(RequestPath); } catch { }
            try { File.Delete(ResultPath); } catch { }
            running = true;
            Debug.Log("[TestRequest] running EditMode tests");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        class Callbacks : ICallbacks
        {
            readonly StringBuilder failures = new();
            int passed, failed, skipped;

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.Test.IsSuite) return;
                switch (r.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed: failed++; failures.AppendLine($"FAIL {r.Test.FullName}\n  {r.Message?.Trim()}\n  {r.StackTrace?.Split('\n')[0]}"); break;
                    default: skipped++; break;
                }
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                running = false;
                string head = $"{(failed == 0 ? "ALL GREEN" : "FAILED")} passed={passed} failed={failed} skipped={skipped} at {DateTime.Now:yyyy-MM-dd HH:mm:ss} commit={GitHead()}";
                File.WriteAllText(ResultPath, head + "\n" + failures);
                Debug.Log("[TestRequest] " + head);
            }

            static string GitHead()
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(File.Exists("/usr/bin/git") ? "/usr/bin/git" : "git", "rev-parse --short HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using var p = System.Diagnostics.Process.Start(psi);
                    string s = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(3000); return s;
                }
                catch { return "?"; }
            }
        }
    }
}

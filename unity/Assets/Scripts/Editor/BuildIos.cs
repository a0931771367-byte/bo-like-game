using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoLike.Editor
{
    /// <summary>
    /// Exports the Xcode project to unity/Builds/iOS. The build number auto-increments and, with the
    /// short commit hash, is written to Resources so the title screen shows which build a tester is on.
    /// Menu: BoLike > Build iOS (Xcode project). Batch: -executeMethod BoLike.Editor.BuildIos.Build
    /// </summary>
    public static class BuildIos
    {
        const string OutDir = "Builds/iOS";
        const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>Scripting define that compiles in the QA-only title entry (seed panel). Never set for store builds.</summary>
        public const string QaDefine = "BOLIKE_QA";

        /// <summary>Beta / TestFlight build: includes the hidden QA entry (tap the version label five times).</summary>
        [MenuItem("BoLike/Build iOS (Xcode project)")]
        public static void Build() => Build(qa: true);

        /// <summary>App Store submission build: no hidden entry (App Review guideline 2.3.1 forbids undocumented features).</summary>
        [MenuItem("BoLike/Build iOS for App Store (no QA entry)")]
        public static void BuildStore() => Build(qa: false);

        public static void Build(bool qa)
        {
            // Twice now a build went out from code the Mac hadn't pulled yet. Refuse when the branch is behind its remote.
            if (!Application.isBatchMode)
            {
                int behind = CommitsBehindRemote();
                if (behind == int.MaxValue) return;
                if (behind > 0 && !EditorUtility.DisplayDialog("程式碼還沒更新",
                        $"GitHub 上還有 {behind} 個新提交沒拉到這台 Mac。\n請等同步完成、Unity 重新編譯後再 Build。\n\n仍要用目前的程式碼建置嗎？", "仍要建置", "取消"))
                    return;
            }
            ProjectSetup.Run();
            if (!File.Exists(ScenePath)) ProjectSetup.SetupScene();

            int number = int.TryParse(PlayerSettings.iOS.buildNumber, out var n) ? n + 1 : 1;
            PlayerSettings.iOS.buildNumber = number.ToString();
            AssetDatabase.SaveAssets();
            string commit = GitShort();
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText("Assets/Resources/build-number.txt", number.ToString());
            File.WriteAllText("Assets/Resources/build-commit.txt", commit);
            AssetDatabase.Refresh();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
                extraScriptingDefines = qa ? new[] { QaDefine } : Array.Empty<string>(),
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            UnityEngine.Debug.Log($"[BuildIos] {summary.result} build {number} ({commit}) {(qa ? "beta+QA" : "store")} → {Path.GetFullPath(OutDir)} " +
                                  $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
            else if (!Application.isBatchMode && summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(Path.GetFullPath(OutDir), "Unity-iPhone.xcodeproj"));
        }

        /// <summary>Commits on the upstream branch that are not local yet (0 when unknown or up to date).</summary>
        static int CommitsBehindRemote()
        {
            try
            {
                var (fetchOk, fetchErr) = Git("fetch --quiet origin", 30000);
                var (ok, count) = Git("rev-list --count HEAD..@{u}", 5000);
                UnityEngine.Debug.Log($"[BuildIos] git fetch {(fetchOk ? "ok" : "FAILED: " + fetchErr)}; behind upstream: {(ok ? count : "unknown")}");
                if (!fetchOk && !EditorUtility.DisplayDialog("無法確認程式碼是否最新",
                        $"git fetch 失敗，無法確認這台 Mac 的程式碼是否已是最新：\n{fetchErr}\n\n仍要建置嗎？", "仍要建置", "取消"))
                    return int.MaxValue;
                return int.TryParse(count, out var n) ? n : 0;
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"[BuildIos] git check failed: {e.Message}"); return 0; }
        }

        static (bool ok, string output) Git(string args, int timeoutMs)
        {
            // Unity.app launched from the Dock has a minimal PATH and no terminal: pin git, run from the repo root,
            // and never let it block on a credential prompt.
            string git = File.Exists("/usr/bin/git") ? "/usr/bin/git" : "git";
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var psi = new ProcessStartInfo(git, $"-C \"{root}\" {args}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            using var p = Process.Start(psi);
            string outp = p.StandardOutput.ReadToEnd().Trim();
            string err = p.StandardError.ReadToEnd().Trim();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return (false, "timeout"); }
            return p.ExitCode == 0 ? (true, outp) : (false, err);
        }

        static string GitShort()
        {
            try
            {
                var (ok, s) = Git("rev-parse --short HEAD", 5000);
                return ok ? s : "";
            }
            catch { return ""; }
        }
    }
}

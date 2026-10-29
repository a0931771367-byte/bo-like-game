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

        [MenuItem("BoLike/Build iOS (Xcode project)")]
        public static void Build()
        {
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
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            UnityEngine.Debug.Log($"[BuildIos] {summary.result} build {number} ({commit}) → {Path.GetFullPath(OutDir)} " +
                                  $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
            else if (!Application.isBatchMode && summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(Path.GetFullPath(OutDir), "Unity-iPhone.xcodeproj"));
        }

        static string GitShort()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return string.IsNullOrEmpty(s) ? "" : s;
            }
            catch { return ""; }
        }
    }
}

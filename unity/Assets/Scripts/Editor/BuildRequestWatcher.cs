using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoLike.Editor
{
    /// <summary>
    /// Lets the Mac terminal trigger "BoLike → Build iOS" without anyone clicking Unity:
    ///   git pull && echo requested > unity/Builds/build-request.txt
    /// The open editor polls for that file, refreshes assets (so freshly pulled scripts and sprites import),
    /// waits for compilation to finish, then runs the same build as the menu item. The file's content tracks
    /// the stage so it survives the domain reload a script change causes. Delete the file to cancel.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildRequestWatcher
    {
        const string RequestPath = "Builds/build-request.txt";
        static double nextCheck;

        static BuildRequestWatcher() => EditorApplication.update += Tick;

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;

            string stage;
            try { stage = File.ReadAllText(RequestPath).Trim(); } catch { return; }

            if (stage == "requested" || stage == "store")
            {
                // Pull in whatever git just changed; a script change triggers a domain reload and we come back here.
                File.WriteAllText(RequestPath, stage == "store" ? "store-refreshed" : "refreshed");
                Debug.Log("[BuildRequest] refreshing assets before build");
                AssetDatabase.Refresh();
                return;
            }
            if (stage == "refreshed" || stage == "store-refreshed")
            {
                bool qa = stage == "refreshed";
                try { File.Delete(RequestPath); } catch { }
                Debug.Log($"[BuildRequest] starting {(qa ? "beta" : "store")} build");
                try { BuildIos.Build(qa, interactive: false); }
                catch (Exception e) { Debug.LogError($"[BuildRequest] build failed: {e}"); }
                return;
            }
            // Unknown content: ignore but don't spam.
        }
    }
}

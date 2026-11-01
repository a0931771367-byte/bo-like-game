using System;
using System.IO;
using UnityEngine;

namespace BoLike.Systems
{
    /// <summary>
    /// Writes errors and exceptions (with build, device and the last run context) to persistentDataPath/log-latest.txt
    /// so a tester's "it crashed" can be matched to a stack trace pulled off the device. Local only, never uploaded.
    /// The previous log is kept as log-previous.txt.
    /// </summary>
    public static class CrashLog
    {
        public static string PathOnDisk => Path.Combine(Application.persistentDataPath, "log-latest.txt");
        static bool started;
        /// <summary>Free-form context the game updates (class, seed, room) so a stack trace can be replayed.</summary>
        public static string Context = "";

        public static void Start(string build)
        {
            if (started) return;
            started = true;
            try
            {
                string prev = Path.Combine(Application.persistentDataPath, "log-previous.txt");
                if (File.Exists(PathOnDisk)) File.Copy(PathOnDisk, prev, true);
                File.WriteAllText(PathOnDisk, $"build {build} unity {Application.unityVersion} {SystemInfo.deviceModel} {SystemInfo.operatingSystem} started {DateTime.UtcNow:o}\n");
            }
            catch (Exception) { }
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try
            {
                File.AppendAllText(PathOnDisk, $"[{DateTime.UtcNow:HH:mm:ss}] {type} ({Context}) {condition}\n{stackTrace}\n");
            }
            catch (Exception) { }
        }
    }
}

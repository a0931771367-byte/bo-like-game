using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BoLike.Systems
{
    /// <summary>One room of a run, for the balance log.</summary>
    [Serializable]
    public class RoomLogEntry
    {
        public string roomId;
        public int waveSet;
        /// <summary>Simulation ms from entering the room to the last enemy dying (0 when the room was not cleared).</summary>
        public int clearMs;
        public int hpAtClear, maxHp;
        public int potionsLeft;
        public string perk;
    }

    /// <summary>One run, appended as a line of JSON to runs.jsonl next to the save.</summary>
    [Serializable]
    public class RunLogEntry
    {
        public string at;
        public string build;
        public string device;
        public string classId;
        public string seed;
        /// <summary>win / lose / abandon</summary>
        public string result;
        public int roomsCleared;
        public bool bossKilled;
        public int totalMs;
        public int stardust;
        public int level;
        public List<string> perks = new();
        public List<RoomLogEntry> rooms = new();
    }

    /// <summary>
    /// Local, append-only log of finished runs for the design team: per-room clear times, HP after each room,
    /// perks taken, and the run outcome. No network; the file sits in Application.persistentDataPath and is
    /// pulled off the device by hand (devicectl copies the app container) or read from the Editor's data folder.
    /// </summary>
    public static class RunLog
    {
        const string FileName = "runs.jsonl";
        public static string PathOnDisk => Path.Combine(Application.persistentDataPath, FileName);

        public static void Append(RunLogEntry e)
        {
            try
            {
                e.at ??= DateTime.UtcNow.ToString("o");
                e.device ??= SystemInfo.deviceModel;
                File.AppendAllText(PathOnDisk, JsonConvert.SerializeObject(e) + "\n");
            }
            catch (Exception ex) { Debug.LogWarning($"RunLog: could not append: {ex.Message}"); }
        }

        /// <summary>Reads every logged run; unreadable lines are skipped.</summary>
        public static List<RunLogEntry> ReadAll(string path = null)
        {
            var list = new List<RunLogEntry>();
            path ??= PathOnDisk;
            if (!File.Exists(path)) return list;
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try { var e = JsonConvert.DeserializeObject<RunLogEntry>(line); if (e != null) list.Add(e); }
                catch (Exception) { }
            }
            return list;
        }

        public static string FormatMs(int ms) => $"{ms / 60000}:{ms / 1000 % 60:00}";
    }
}

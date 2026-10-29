using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BoLike.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace BoLike.Systems
{
    public class ClassProgress
    {
        public int level = 1;
        public int exp;
        public Stats stats;
        public int statPoints;
        public int skillPoints;
        public Dictionary<string, int> skillLv = new();

        public static ClassProgress New(ClassDef cls) => new()
        {
            level = 1, exp = 0, stats = cls.baseStats.Clone(), statPoints = 3, skillPoints = 1,
            skillLv = cls.skills.Select((s, i) => (s.id, lv: i == 0 ? 1 : 0)).ToDictionary(x => x.id, x => x.lv),
        };
    }

    public class Potions
    {
        public int red = 3;
        public int blue = 2;
    }

    /// <summary>
    /// Version 1 is the Phaser save (same fields, so its export code imports here).
    /// Version 2 adds the Roguelite meta progress.
    /// </summary>
    public class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public Dictionary<string, ClassProgress> classes = new();
        public int gold = 50;
        public Potions potions = new();
        public List<string> cleared = new();

        // v2: Roguelite meta progress
        public int stardust;
        public List<string> unlocks = new();
        public int runsPlayed;
        public int bestRoom;
        public bool bossCleared;
        /// <summary>Seed code of the most recent run (for the debug seed panel and bug reports).</summary>
        public string lastSeedCode;

        public ClassProgress ProgressFor(ClassDef cls)
        {
            if (!classes.TryGetValue(cls.id, out var p) || p == null) classes[cls.id] = p = ClassProgress.New(cls);
            return p;
        }
    }

    /// <summary>
    /// Local JSON save in Application.persistentDataPath. Writes go to a temp file first and are then
    /// swapped in, so a crash or the app being killed mid-write never leaves a half-written save.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        public static string PathOnDisk => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Current { get; private set; }

        /// <summary>
        /// Loads the save. When a file exists but cannot be read (corrupt, or written by a newer build the
        /// player downgraded from via TestFlight), it is kept as save.unreadable-&lt;utc&gt;.json before a fresh
        /// save takes its place, so progress is never silently thrown away.
        /// </summary>
        public static SaveData Load()
        {
            var s = LoadFrom(PathOnDisk);
            if (s == null) PreserveUnreadable(PathOnDisk);
            return Current = s ?? new SaveData();
        }

        public static void PreserveUnreadable(string path)
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            foreach (var p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    string keep = Path.Combine(Path.GetDirectoryName(path), $"save.unreadable-{stamp}{(p.EndsWith(".bak") ? "-bak" : "")}.json");
                    File.Copy(p, keep, true);
                    Debug.LogWarning($"Save at {p} unreadable; copy kept at {keep}");
                }
                catch (Exception e) { Debug.LogWarning($"Could not preserve unreadable save {p}: {e.Message}"); }
            }
        }

        public static void Save() => WriteTo(PathOnDisk, Current ??= new SaveData());

        public static SaveData LoadFrom(string path)
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (File.Exists(p)) { var s = Parse(File.ReadAllText(p, Encoding.UTF8)); if (s != null) return s; }
                }
                catch (Exception e) { Debug.LogWarning($"Save at {p} unreadable: {e.Message}"); }
            }
            return null;
        }

        public static void WriteTo(string path, SaveData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data, GameDb.Json), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }

        /// <summary>Parses and migrates a save of any known version; null if it is not a save.</summary>
        public static SaveData Parse(string json)
        {
            var s = JsonConvert.DeserializeObject<SaveData>(json, GameDb.Json);
            if (s == null || s.version < 1 || s.version > SaveData.CurrentVersion) return null;
            s.classes ??= new();
            s.potions ??= new();
            s.cleared ??= new();
            s.unlocks ??= new();
            // A null entry (hand-edited or truncated file) is dropped so ProgressFor rebuilds it instead of returning null.
            foreach (var k in s.classes.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) s.classes.Remove(k);
            foreach (var kv in s.classes)
            {
                kv.Value.skillLv ??= new();
                if (kv.Value.stats == null && GameDbHasClass(kv.Key)) kv.Value.stats = GameDb.Class(kv.Key).baseStats.Clone();
            }
            s.version = SaveData.CurrentVersion; // v1 -> v2: new fields keep their defaults
            return s;
        }

        static bool GameDbHasClass(string id)
        {
            foreach (var c in GameDb.Classes) if (c.id == id) return true;
            return false;
        }

        /// <summary>Same format as the Phaser build's export code: base64 of the UTF-8 JSON.</summary>
        public static string ExportCode(SaveData s) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(s, GameDb.Json)));

        public static SaveData ImportCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            try { return Parse(Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim()))); }
            catch (FormatException) { return null; }
            catch (JsonException) { return null; }
        }
    }
}

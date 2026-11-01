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
    /// Version 3 (beta 2) adds chapter progress; cards, talents, story and the run snapshot follow in S2–S5.
    /// </summary>
    public class SaveData
    {
        public const int CurrentVersion = 3;

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
        /// <summary>Tutorial hints already shown (strings.json hint.* keys); hints only run in the first two runs anyway.</summary>
        public List<string> hintsSeen = new();

        // v3: chapter progress (S1). Chapter unlocks are derived from bossFirstKills, never stored.
        /// <summary>Stage ids (chapters.json) cleared at least once.</summary>
        public List<string> stagesCleared = new();
        /// <summary>Boss enemy ids killed at least once; a chapter opens when its unlock.bossKill is here.</summary>
        public List<string> bossFirstKills = new();
        /// <summary>Chapter the player last picked on the title screen.</summary>
        public string lastChapterId;
        /// <summary>The run in progress, written at every room start and cleared when the run ends (S5 續局). Null = none.</summary>
        public RunSnapshot runSnapshot;
        /// <summary>Epic cards owned, account-wide (S2); any class can equip them.</summary>
        public List<CardProgress> cards = new();
        /// <summary>Equipped card ids, at most <see cref="Cards.Slots"/> of them.</summary>
        public List<string> equippedCards = new();
        /// <summary>Story beats already shown ("&lt;stageId&gt;:before" / ":after"); a beat plays once per account (S4).</summary>
        public List<string> storySeen = new();
        /// <summary>Talent node ids bought (S3), across classes.</summary>
        public List<string> talents = new();

        public CardProgress Card(string id) => cards.Find(c => c.id == id);

        public ClassProgress ProgressFor(ClassDef cls)
        {
            if (!classes.TryGetValue(cls.id, out var p) || p == null) classes[cls.id] = p = ClassProgress.New(cls);
            return p;
        }
    }

    /// <summary>One owned epic card: stars 1–3 and the shards collected toward the next star.</summary>
    public class CardProgress
    {
        public string id;
        public int stars = 1;
        public int shards;
    }

    /// <summary>
    /// Everything needed to continue a run from the start of its current room (design doc 08 §1.3): the seed replays
    /// the room order, wave sets and perk offers; the rest is the player's state when the room began.
    /// </summary>
    public class RunSnapshot
    {
        public int version = 1;
        public string classId, stageId, seedCode;
        /// <summary>Index of the room to resume at; the player restarts it from its door.</summary>
        public int roomIndex;
        /// <summary>Perk ids in the order they were taken; replayed through DrawPerks/TakePerk so later offers match.</summary>
        public List<string> perks = new();
        public ClassProgress prog;
        public int hp, sp;
        public int red, blue, gold;
        public int stardustEarned, roomsCleared;
        public bool reviveUsed;
        /// <summary>Simulation ms played before this room, so the run's total time survives the restart.</summary>
        public int elapsedMs;
        public List<RoomLogEntry> rooms = new();
        public List<string> skillUps = new();
        /// <summary>How many times this run has been resumed (for runs.jsonl).</summary>
        public int resumes;
        public string savedAt;
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
            else BackupOlderVersion(PathOnDisk);
            return Current = s ?? new SaveData();
        }

        /// <summary>
        /// Keeps a copy of a save written by an older format as save.v&lt;n&gt;.json before the first write migrates it,
        /// so a tester can go back to the previous build without losing progress (QA gate D). One copy per version.
        /// </summary>
        public static void BackupOlderVersion(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                int v = VersionOf(File.ReadAllText(path, Encoding.UTF8));
                if (v <= 0 || v >= SaveData.CurrentVersion) return;
                string keep = Path.Combine(Path.GetDirectoryName(path), $"save.v{v}.json");
                if (!File.Exists(keep)) File.Copy(path, keep);
            }
            catch (Exception e) { Debug.LogWarning($"Could not back up older save {path}: {e.Message}"); }
        }

        /// <summary>The version field of a raw save file, or 0 when it has none.</summary>
        public static int VersionOf(string json)
        {
            try { return JsonConvert.DeserializeAnonymousType(json, new { version = 0 })?.version ?? 0; }
            catch (JsonException) { return 0; }
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
            s.hintsSeen ??= new();
            s.stagesCleared ??= new();
            s.bossFirstKills ??= new();
            s.cards ??= new();
            s.storySeen ??= new();
            s.talents ??= new();
            s.equippedCards ??= new();
            s.cards.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
            // v2 -> v3: the single bossCleared flag becomes the first boss's first kill and the first stage's clear.
            if (s.version < 3 && s.bossCleared)
            {
                var first = GameDb.DefaultStage;
                if (!string.IsNullOrEmpty(first.finale?.enemy) && !s.bossFirstKills.Contains(first.finale.enemy)) s.bossFirstKills.Add(first.finale.enemy);
                if (!s.stagesCleared.Contains(first.id)) s.stagesCleared.Add(first.id);
            }
            // A null entry (hand-edited or truncated file) is dropped so ProgressFor rebuilds it instead of returning null.
            foreach (var k in s.classes.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) s.classes.Remove(k);
            foreach (var kv in s.classes)
            {
                kv.Value.skillLv ??= new();
                if (kv.Value.stats == null && GameDbHasClass(kv.Key)) kv.Value.stats = GameDb.Class(kv.Key).baseStats.Clone();
            }
            s.version = SaveData.CurrentVersion; // older versions: new fields keep their defaults
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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace BoLike.Data
{
    /// <summary>Loads the exported data tables from Resources/Data once and indexes them by id.</summary>
    public static class GameDb
    {
        public static readonly JsonSerializerSettings Json = new()
        {
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()), new ScalingConverter() },
            NullValueHandling = NullValueHandling.Ignore,
        };

        static bool loaded;
        static List<ClassDef> classes;
        static Dictionary<string, ClassDef> classById;
        static Dictionary<string, EnemyDef> enemyById;
        static Dictionary<string, StageDef> stageById;
        static Dictionary<string, StageStory> storyById;
        static Dictionary<string, AnimSet> animById;
        static List<PerkDef> perks;
        static Dictionary<string, PerkDef> perkById;
        static Dictionary<string, RoomDef> roomById;
        static RoomTable roomTable;
        static MetaTable meta;
        static StringTable stringTable;
        static SkillUiTable skillUi;
        static ChapterTable chapterTable;
        static CardTable cardTable;
        static List<TalentDef> talents;
        static Dictionary<string, CardDef> cardById;
        static Dictionary<string, ChapterDef> chapterById;
        static Dictionary<string, RunStageDef> runStageById;

        public static IReadOnlyList<ClassDef> Classes { get { Load(); return classes; } }
        public static IEnumerable<EnemyDef> Enemies { get { Load(); return enemyById.Values; } }
        public static IEnumerable<StageDef> Stages { get { Load(); return stageById.Values; } }

        public static IReadOnlyList<PerkDef> Perks { get { Load(); return perks; } }
        public static IEnumerable<RoomDef> Rooms { get { Load(); return roomById.Values; } }
        public static IReadOnlyList<string> RoomPool { get { Load(); return roomTable.pool; } }
        public static string BossRoom { get { Load(); return roomTable.boss; } }
        public static RunRules Rules { get { Load(); return meta.rules; } }
        public static SkillUiTable SkillUi { get { Load(); return skillUi; } }
        public static IReadOnlyList<UnlockDef> Unlocks { get { Load(); return meta.unlocks; } }
        public static IReadOnlyList<string> StartingClasses { get { Load(); return meta.startingClasses; } }
        /// <summary>Room affixes (meta.json affixes); empty on older data.</summary>
        public static IReadOnlyList<RoomAffixDef> Affixes { get { Load(); return meta.affixes ?? new List<RoomAffixDef>(); } }
        public static bool HasAffix(string id) { Load(); return id != null && meta.affixes != null && meta.affixes.Exists(a => a.id == id); }
        public static RoomAffixDef Affix(string id) { Load(); return meta.affixes.Find(a => a.id == id); }
        public static EliteRules Elite { get { Load(); return meta.elite ?? new EliteRules(); } }
        /// <summary>Chapters in play order (chapters.json); beta 2 ships three, the third without stages until its data lands.</summary>
        public static IReadOnlyList<ChapterDef> Chapters { get { Load(); return chapterTable.chapters; } }
        public static IReadOnlyList<RunStageDef> RunStages { get { Load(); return chapterTable.stages; } }
        /// <summary>Epic cards (cards.json); empty when the table is missing so the beta-1 flow still runs.</summary>
        public static IReadOnlyList<CardDef> Cards { get { Load(); return cardTable.cards; } }
        public static IReadOnlyList<CardSetDef> CardSets { get { Load(); return cardTable.sets; } }
        /// <summary>Talent nodes of every class (talents.json), in tier order per class.</summary>
        public static IReadOnlyList<TalentDef> Talents { get { Load(); return talents; } }
        public static List<TalentDef> TalentsFor(string classId) { Load(); return talents.Where(t => t.classId == classId).OrderBy(t => t.tier).ToList(); }
        /// <summary>The stage a run starts in when none is chosen: the first chapter's first stage.</summary>
        public static RunStageDef DefaultStage { get { Load(); return runStageById[chapterTable.chapters[0].stages[0]]; } }

        /// <summary>UI copy by key; returns the key itself when missing so a typo is visible, never a crash.</summary>
        public static string Str(string key)
        {
            Load();
            return stringTable?.strings != null && stringTable.strings.TryGetValue(key, out var v) ? v : key;
        }
        public static BossLine BossLines(string enemyId)
        {
            Load();
            return stringTable?.bossLines != null && stringTable.bossLines.TryGetValue(enemyId, out var v) ? v : null;
        }

        public static PerkDef Perk(string id) { Load(); return perkById[id]; }
        public static RoomDef Room(string id) { Load(); return roomById[id]; }
        public static ClassDef Class(string id) { Load(); return classById[id]; }
        public static EnemyDef Enemy(string id) { Load(); return enemyById[id]; }
        public static StageDef Stage(string id) { Load(); return stageById[id]; }
        public static ChapterDef Chapter(string id) { Load(); return chapterById[id]; }
        public static RunStageDef RunStage(string id) { Load(); return runStageById[id]; }
        public static bool HasRunStage(string id) { Load(); return id != null && runStageById.ContainsKey(id); }
        public static CardDef Card(string id) { Load(); return cardById[id]; }
        public static bool HasCard(string id) { Load(); return id != null && cardById.ContainsKey(id); }
        public static StageStory Story(string stageId) { Load(); return storyById.TryGetValue(stageId, out var s) ? s : null; }
        public static AnimSet Anim(string classId) { Load(); return animById.TryGetValue(classId, out var a) ? a : null; }

        public static T Parse<T>(string json) => JsonConvert.DeserializeObject<T>(json, Json);

        public static void Load()
        {
            if (loaded) return;
            classes = Read<List<ClassDef>>("classes");
            classById = classes.ToDictionary(c => c.id);
            enemyById = Read<List<EnemyDef>>("enemies").ToDictionary(e => e.id);
            stageById = Read<List<StageDef>>("stages").ToDictionary(s => s.id);
            storyById = Read<List<StageStory>>("story").ToDictionary(s => s.stageId);
            animById = Read<List<AnimSet>>("anims").ToDictionary(a => a.classId);
            perks = Read<List<PerkDef>>("perks");
            perkById = perks.ToDictionary(p => p.id);
            roomTable = Read<RoomTable>("rooms");
            roomById = roomTable.rooms.ToDictionary(r => r.id);
            meta = Read<MetaTable>("meta");
            chapterTable = Read<ChapterTable>("chapters");
            chapterById = chapterTable.chapters.ToDictionary(c => c.id);
            runStageById = chapterTable.stages.ToDictionary(st => st.id);
            var cardAsset = Resources.Load<TextAsset>("Data/cards");
            cardTable = cardAsset != null ? Parse<CardTable>(cardAsset.text) : new CardTable();
            cardById = cardTable.cards.ToDictionary(c => c.id);
            var talentAsset = Resources.Load<TextAsset>("Data/talents");
            talents = talentAsset != null ? Parse<List<TalentDef>>(talentAsset.text) : new List<TalentDef>();
            var uiAsset = Resources.Load<TextAsset>("Data/skillUi");
            skillUi = uiAsset != null ? Parse<SkillUiTable>(uiAsset.text) : new SkillUiTable();
            var strAsset = Resources.Load<TextAsset>("Data/strings");
            stringTable = strAsset != null ? Parse<StringTable>(strAsset.text) : new StringTable();
            loaded = true;
        }

        static T Read<T>(string name)
        {
            var asset = Resources.Load<TextAsset>("Data/" + name);
            if (asset == null) throw new System.IO.FileNotFoundException($"Resources/Data/{name}.json missing; run tools/export_unity_data.mjs");
            return Parse<T>(asset.text);
        }
    }
}

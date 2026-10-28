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

        public static IReadOnlyList<ClassDef> Classes { get { Load(); return classes; } }
        public static IEnumerable<EnemyDef> Enemies { get { Load(); return enemyById.Values; } }
        public static IEnumerable<StageDef> Stages { get { Load(); return stageById.Values; } }

        public static ClassDef Class(string id) { Load(); return classById[id]; }
        public static EnemyDef Enemy(string id) { Load(); return enemyById[id]; }
        public static StageDef Stage(string id) { Load(); return stageById[id]; }
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

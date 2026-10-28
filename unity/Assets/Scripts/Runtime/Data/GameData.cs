using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoLike.Data
{
    // Mirrors src/data/types.ts. Field names match the exported JSON (tools/export_unity_data.mjs).
    // Distances are pixels and times are milliseconds, as in the Phaser build; convert with Core.Units.

    public enum Element { Neutral, Fire, Water, Wind, Earth, Holy, Dark, Poison }
    public enum StatKey { Str, Agi, Vit, Int, Dex, Luk }
    public enum SkillType { Melee, Aoe, Projectile, Buff, Debuff, Wall, Strikes, Heal, Dash, Passive }
    public enum Weapon { Sword, Staff, Axe, Mace, Dagger, Bow }
    public enum EnemyBehavior { Hopper, Melee, Lunger, BossSlime, Flyer, Turret, BossTreant }
    public enum Passive { DoubleAttack, GoldFind }
    public enum Side { Left, Right }

    /// <summary>[value at Lv1, increase per extra level], stored as a two-number array in JSON.</summary>
    public struct Scaling
    {
        public float Base;
        public float PerLevel;

        public Scaling(float b, float perLevel) { Base = b; PerLevel = perLevel; }

        public float At(int level) => Base + PerLevel * (level - 1);
    }

    [Serializable]
    public class Stats
    {
        public int str, agi, vit, @int, dex, luk;

        public Stats Clone() => (Stats)MemberwiseClone();

        public int this[StatKey k]
        {
            get => k switch
            {
                StatKey.Str => str, StatKey.Agi => agi, StatKey.Vit => vit,
                StatKey.Int => @int, StatKey.Dex => dex, _ => luk,
            };
            set
            {
                switch (k)
                {
                    case StatKey.Str: str = value; break;
                    case StatKey.Agi: agi = value; break;
                    case StatKey.Vit: vit = value; break;
                    case StatKey.Int: @int = value; break;
                    case StatKey.Dex: dex = value; break;
                    default: luk = value; break;
                }
            }
        }
    }

    public class ComboHit
    {
        public float power, range, startup, active, recovery, knock, launch, stun;
    }

    /// <summary>Temporary stat changes granted by buff skills.</summary>
    public class BuffEffect
    {
        public float? atkMult, matkMult, allStats, moveMult, aspdMult;
        public bool endure, hidden;
        public int? shieldHits;
    }

    public class SkillDef
    {
        public string id, name, @short, desc;
        public SkillType type;
        public int maxLv;
        public Scaling sp;
        public float cooldown, cast;
        public Scaling power;
        public Element element;
        public bool magic;
        public int color;
        public float? range, radius, knock, launch, stun, speed;
        public Scaling? stunChance, freezeChance, count, duration, defDown, goldCost, poison, heal, passiveValue, dashDistance;
        public Passive? passive;
        /// <summary>Buff per skill level; index 0 is Lv1. Null for non-buff skills.</summary>
        public List<BuffEffect> buffByLevel;

        public BuffEffect BuffAt(int level) =>
            buffByLevel == null || buffByLevel.Count == 0 ? null : buffByLevel[Math.Clamp(level, 1, buffByLevel.Count) - 1];
    }

    public class ClassDef
    {
        public string id, name, role;
        public int color, accent;
        public Weapon weapon;
        public bool playable, ranged;
        public float hpBase, hpPerLv, spBase, spPerLv, weaponAtk;
        public Stats baseStats;
        public List<ComboHit> combo;
        public ComboHit air;
        public List<SkillDef> skills;
    }

    public class EnemyDef
    {
        public string id, name, sprite;
        public float hp, atk, def, mdef;
        public Element element;
        public int exp, gold;
        public float speed, w, h;
        public int color;
        public int? tint;
        public EnemyBehavior behavior;
        public float attackRange, windup, attackCd, spriteH;
        public bool boss;

        public string SpriteId => string.IsNullOrEmpty(sprite) ? id : sprite;
    }

    public class SpawnDef
    {
        public string enemy;
        public Side side;
        public float delay;
    }

    public class WaveDef
    {
        public float triggerX;
        public List<SpawnDef> spawns;
    }

    public class StageDef
    {
        public string id, name, chapter, desc, bg;
        public float width, bgGroundY;
        public int? tint;
        public List<WaveDef> waves;
    }

    public class StoryLine
    {
        public string who, name, portrait, text;
    }

    public class StageStory
    {
        public string stageId;
        public List<StoryLine> before, after;
    }

    public class AnimSet
    {
        public string classId;
        public int idle, walk, attack;
    }

    /// <summary>Reads and writes <see cref="Scaling"/> (and Scaling?) as a two-number JSON array.</summary>
    public class ScalingConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Scaling) || t == typeof(Scaling?);

        public override object ReadJson(JsonReader reader, Type objectType, object existing, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var a = serializer.Deserialize<float[]>(reader);
            return new Scaling(a[0], a.Length > 1 ? a[1] : 0f);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var s = (Scaling)value;
            serializer.Serialize(writer, new[] { s.Base, s.PerLevel });
        }
    }
}

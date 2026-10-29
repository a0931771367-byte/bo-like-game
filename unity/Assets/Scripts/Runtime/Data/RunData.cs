using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BoLike.Data
{
    // Roguelite run data, mirrors the run types in src/data/types.ts, perks.ts, rooms.ts and meta.ts.

    public enum PerkRarity { Common, Rare, Epic }
    public enum MultTarget { MaxHp, Atk, Matk, Move, Aspd, Cooldown, SpCost, SpRegen, Knock, PotionHeal, SkillPower, Stardust }
    public enum PerkEffectKind { Mult, Crit, SkillLvAll, OnKillHeal, OnKillAoe, RoomClearHeal, Potion, Revive, ComboFinisherAoe, ProjectileCount, Pierce }
    public enum RoomKind { Combat, Boss }
    public enum UnlockKind { Class, StartPotion, RareChance, StartPerk }

    /// <summary>
    /// One effect of a perk. The TypeScript is a tagged union; here every field is optional and
    /// <see cref="kind"/> says which ones are set (see the comments on PerkEffect in types.ts).
    /// </summary>
    public class PerkEffect
    {
        public PerkEffectKind kind;
        public MultTarget? target;
        public float value;
        public float chanceAdd, damageMultAdd;
        public int add;
        public float fraction;
        public float power, radius;
        public int red, blue;
        public float invuln;
    }

    public class PerkDef
    {
        public string id, name, desc;
        public PerkRarity rarity;
        /// <summary>Empty = offered to every class.</summary>
        public List<string> classes = new();
        public int maxStacks = 1;
        public List<PerkEffect> effects = new();

        public bool OfferedTo(string classId) => classes == null || classes.Count == 0 || classes.Contains(classId);
    }

    /// <summary>
    /// A wave starts when the previous one is cleared, or `timeout` ms after it started, whichever is first.
    /// timeout 0 starts the next wave at once (boss adds run on their spawn delays while the boss lives).
    /// </summary>
    public class RoomWaveDef
    {
        public List<SpawnDef> spawns;
        public float timeout;
    }

    public class RoomDef
    {
        public string id, name, scene, bg;
        public RoomKind kind;
        public float width, bgGroundY;
        public int? tint;
        /// <summary>The run seed picks one wave set per visit.</summary>
        public List<List<RoomWaveDef>> waveSets;
    }

    public class RoomTable
    {
        public List<RoomDef> rooms;
        /// <summary>Combat rooms a run draws from, without repeats.</summary>
        public List<string> pool;
        public string boss;
        /// <summary>Scene tables (grassland, mistForest …); the beta only runs the first in <see cref="sceneOrder"/>.</summary>
        public List<SceneDef> scenes;
        public List<string> sceneOrder;
    }

    public class SceneDef
    {
        public string id;
        public string name;
        public List<string> pool;
        public string boss;
    }

    public class BossLine { public string enter; public string die; }

    /// <summary>UI copy exported from src/data/strings.ts.</summary>
    public class StringTable
    {
        public Dictionary<string, string> strings;
        public Dictionary<string, BossLine> bossLines;
    }

    public class UnlockDef
    {
        public string id, name, desc;
        public int cost;
        public UnlockKind kind;
        /// <summary>A class id for <see cref="UnlockKind.Class"/>, a number otherwise.</summary>
        public JToken value;

        public string ValueId => value?.ToObject<string>();
        public float ValueNumber => value?.ToObject<float>() ?? 0f;
    }

    public class RoomScaling
    {
        public List<float> hp, atk;
        public Multipliers boss;

        public class Multipliers { public float hp = 1, atk = 1; }

        public float HpAt(int slot) => hp[System.Math.Clamp(slot, 0, hp.Count - 1)];
        public float AtkAt(int slot) => atk[System.Math.Clamp(slot, 0, atk.Count - 1)];
    }

    public class StardustRewards
    {
        /// <summary>Given at the end of every run, won or lost, so the first runs always buy something.</summary>
        public int runEnd;
        public int roomClear, bossKill, runClear, firstClearBonus;
    }

    /// <summary>Constraints on each three-card perk offer (growth curve v2 §2).</summary>
    public class PerkOffer
    {
        /// <summary>At least this many common cards per offer.</summary>
        public int minCommon = 1;
        /// <summary>At most this many epic cards per offer.</summary>
        public int maxEpic = 1;
    }

    /// <summary>In-run levelling (growth curve v2 §1).</summary>
    public class LevelUp
    {
        /// <summary>EXP multiplier per room slot; the boss room's minions use the last entry.</summary>
        public List<float> expScalingBySlot = new() { 1 };
        public float bossExpMult = 1;
        /// <summary>Fraction of max HP healed on level up.</summary>
        public float heal = 0.3f;
        public int statsPerLevel = 3;
        /// <summary>Level at which skill i unlocks; after all are unlocked each level raises the lowest skill.</summary>
        public List<int> skillUnlockLevels = new() { 1, 2, 3, 4 };

        public float ExpMultAt(int slot) => expScalingBySlot == null || expScalingBySlot.Count == 0 ? 1 : expScalingBySlot[System.Math.Clamp(slot, 0, expScalingBySlot.Count - 1)];
    }

    /// <summary>Enemy damage = max(atk × minFraction, atk × power − def × defCoef) (growth curve v2 §4).</summary>
    public class EnemyDamage
    {
        public float minFraction = 0;
        public float defCoef = 0.6f;
    }

    public class RunRules
    {
        public int combatRooms, perkChoices, startSkillLv;
        public Dictionary<PerkRarity, float> rarityWeights;
        public PerkOffer perkOffer = new();
        public LevelUp levelUp = new();
        public EnemyDamage enemyDamage = new();
        public PotionCount startPotions;
        public float roomClearHeal;
        public RoomScaling roomScaling;
        public float roomTransition;
        public StardustRewards stardust;
        public float targetRunMs;

        public class PotionCount { public int red, blue; }
    }

    public class MetaTable
    {
        public RunRules rules;
        public List<UnlockDef> unlocks;
        /// <summary>Classes available before any unlock.</summary>
        public List<string> startingClasses;
    }
}

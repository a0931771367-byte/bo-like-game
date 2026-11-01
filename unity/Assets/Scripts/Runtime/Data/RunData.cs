using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BoLike.Data
{
    // Roguelite run data, mirrors the run types in src/data/types.ts, perks.ts, rooms.ts and meta.ts.

    public enum PerkRarity { Common, Rare, Epic }
    public enum MultTarget { MaxHp, Atk, Matk, Move, Aspd, Cooldown, SpCost, SpRegen, Knock, PotionHeal, SkillPower, Stardust }
    /// <summary>Every effect kind in src/data/types.ts (perks, epic cards, talents). RunMods ignores kinds it does not
    /// implement yet, so new data never breaks loading; the beta-2 list is implemented system by system (design doc 08 §2.4).</summary>
    public enum PerkEffectKind
    {
        Mult, Crit, SkillLvAll, OnKillHeal, OnKillAoe, RoomClearHeal, Potion, Revive, ComboFinisherAoe, ProjectileCount, Pierce,
        // perks 16–30
        GuardMult, Dodge, RoomStartBuff, ComboExtraHit, OnHitStatus, OnKillSp, KillStackAtk, CooldownReset, Counter,
        // epic cards
        OnComboHitSpawn, SummonAlly, DodgeOverride, LowHpShield, FirstHitImmune, IdleRegen, KillStardust, OnHurtKnock, StartPotion,
        // talents
        ComboDebuff, AfterDodgeBuff, ComboHitMult, ElementResonance, GuardInvuln, GuardReflect, ReflectProjectile, RoomStart, SpEmptyFree, StartSkillPoints,
    }
    public enum RoomKind { Combat, Boss }
    public enum UnlockKind { Class, StartPotion, RareChance, StartPerk, CardSlot }

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
        // ---- beta-2 fields (cards, talents, perks 16–30); every variant's fields live here, unused ones stay 0/null ----
        public int hit, count, max;
        /// <summary>onComboHitSpawn: stickyBlob | beam. onHitStatus: freeze | burn | poison. summonAlly trigger: room | kill.</summary>
        public string what, status, trigger, enemy;
        public float duration, chance, knock, hpFraction, cooldown, auraPower, after, hpMult, lifetime, respawn;
        public float defMult, invulnAdd, distanceMult, cooldownAdd, mult, perKill, window, aspdMult, spFraction, hiddenMs;
        public int extraProjectiles;
        public bool? passThrough, onGuard;
        public LandingDef landing;

        public PerkEffect Clone()
        {
            var c = (PerkEffect)MemberwiseClone();
            if (landing != null) c.landing = (LandingDef)landing.MemberwiseCloneDef();
            return c;
        }
    }

    /// <summary>dodgeOverride.landing: a zone left where the dodge ends.</summary>
    public class LandingDef
    {
        public string what; // thornWall | quicksand | iceBurst | spikeTrap | decoy
        public float duration, power, slow, radius;
        public object MemberwiseCloneDef() => MemberwiseClone();
    }

    public enum CardRarity { Epic, Legendary }

    /// <summary>Boss drop, account-wide, kept forever (design doc 08 §2.2). Effects reuse <see cref="PerkEffect"/>.</summary>
    public class CardDef
    {
        public string id, name, desc, boss, icon;
        public CardRarity rarity;
        /// <summary>Guaranteed on that boss's first kill; one per boss.</summary>
        public bool signature;
        public List<PerkEffect> effects = new();
        /// <summary>Each star above 1 scales the numbers of <see cref="effects"/> by (1 + starScale); 0 = stars only collect shards.</summary>
        public float starScale;
    }

    /// <summary>Wearing <see cref="need"/> cards of one boss adds these effects.</summary>
    public class CardSetDef
    {
        public string boss, name, desc;
        public int need = 2;
        public List<PerkEffect> effects = new();
    }

    public class CardTable
    {
        public List<CardDef> cards = new();
        public List<CardSetDef> sets = new();
    }

    /// <summary>One node of a class's 10-node talent line (design doc 08 §2.3): bought in tier order with stardust, free reset.</summary>
    public class TalentDef
    {
        public string id, classId, name, desc;
        public int tier, cost;
        public bool keystone;
        public List<PerkEffect> effects = new();
    }

    public class TalentRules
    {
        public int nodesPerClass = 10;
        public bool resetFree = true, linear = true;
    }

    public class CardRules
    {
        public int startSlots = 2, maxSlots = 3;
        public bool firstKillSignature = true;
        public int dropsPerKill = 1, shardsPerDuplicate = 1, shardsPerStar = 3, maxStars = 3;
        public bool legendaryEnabled;
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
        /// <summary>Legacy: the first chapter's combat pool and boss room. Runs now read <see cref="RunStageDef"/>.</summary>
        public List<string> pool;
        public string boss;
    }

    // ---- 章／關／房池（設計部 07 §9；src/data/chapters.ts → Resources/Data/chapters.json） ----

    public enum TimeOfDay { Day, Dusk, Night }
    public enum FinaleKind { None, MidBoss, Boss }

    public class ChapterDef
    {
        public string id, name, desc, bg;
        public int order;
        /// <summary>Stage ids in unlock order; beta 2 has one per chapter (the boss stage), 1.0 inserts -1 and -2 in front.</summary>
        public List<string> stages = new();
        public ChapterUnlock unlock;
        /// <summary>AudioManager music / ambience keys; bossTrack is the chapter boss's own track (chapter-end bosses only).</summary>
        public string music, ambience, bossTrack;

        /// <summary>No stages yet (data not delivered): shown in the menu as "coming soon", never startable.</summary>
        public bool Playable => stages != null && stages.Count > 0;
    }

    public class ChapterUnlock { public string bossKill; }

    public class StageFinale
    {
        public FinaleKind kind;
        public string enemy, room;
    }

    public class RunStageDef
    {
        public string id, chapterId, name;
        public int index;
        public TimeOfDay timeOfDay;
        public int combatRooms;
        public List<string> pool = new();
        public StageFinale finale = new();
        /// <summary>Per-stage enemy multipliers; null falls back to <see cref="RunRules.roomScaling"/>.</summary>
        public RoomScaling scaling;
        public List<string> affixes = new();
        public string desc;
        /// <summary>STORY keys played before the first room and after the finale (chapters.json stages[].story).</summary>
        public StageStoryKeys story;

        public bool HasBossRoom => finale != null && finale.kind == FinaleKind.Boss && !string.IsNullOrEmpty(finale.room);
    }

    public class StageStoryKeys { public string before, after; }

    public class ChapterTable
    {
        public List<ChapterDef> chapters = new();
        public List<RunStageDef> stages = new();
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
        /// <summary>Skill slot i can take points once the run level is at least skillUnlockLevels[i].</summary>
        public List<int> skillUnlockLevels = new() { 1, 2, 3, 4 };
        /// <summary>Skill points per level, spent by the player in the skill panel (design doc 06).</summary>
        public int skillPointsPerLevel = 1;
        public int skillLearnCost = 1;
        public int skillRaiseCost = 1;
        public int startSkills = 1;
        public bool panelPausesGame = true;

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
        public CardRules cards = new();
        public TalentRules talents = new();
        public AffixRules affixes = new();
        public EnemyDamage enemyDamage = new();
        public PotionCount startPotions;
        public float roomClearHeal;
        public RoomScaling roomScaling;
        public float roomTransition;
        public StardustRewards stardust;
        public float targetRunMs;

        public class PotionCount { public int red, blue; }
    }

    /// <summary>One room affix's parameters (meta.ts ROOM_AFFIXES); which fields matter depends on <see cref="kind"/>:
    /// fog (visionMult, warnMs), poisonGround (zones, dpsFraction), treasure (extraPerkPicks), elite (forceElite, stardustMult), rush (waveTimeoutMult, hpMult).</summary>
    public class RoomAffixEffect
    {
        public string kind;
        public float visionMult = 1f, warnMs, dpsFraction, waveTimeoutMult = 1f, hpMult = 1f, stardustMult = 1f;
        public int zones, extraPerkPicks;
        public bool forceElite;
    }

    public class RoomAffixDef
    {
        public string id, name, desc;
        public RoomAffixEffect effect = new();
    }

    /// <summary>Elite enemies (meta.ts ELITE): one per room at most, gold outline, bigger, one modifier.</summary>
    public class EliteRules
    {
        public List<float> chanceByChapterOrder = new();
        public float hpMult = 2.5f, atkMult = 1.4f, sizeMult = 1.3f;
        public int stardust = 10;
        /// <summary>shield | haste | volatile</summary>
        public List<string> modifiers = new();
        public int shieldHits = 3;
        public float hasteMult = 1.4f, volatilePower = 1f, volatileRadius = 140f;
        public List<EnemyBehavior> excludeBehaviors = new();

        public float ChanceFor(int chapterOrder)
        {
            if (chanceByChapterOrder == null || chanceByChapterOrder.Count == 0) return 0f;
            return chanceByChapterOrder[System.Math.Clamp(chapterOrder - 1, 0, chanceByChapterOrder.Count - 1)];
        }

        public bool Eligible(EnemyDef def) => def != null && !def.boss && (excludeBehaviors == null || !excludeBehaviors.Contains(def.behavior));
    }

    public class AffixRules
    {
        public float chancePerRoom = 0.5f;
        public int maxPerRoom = 1;
        public bool onlyAfterStageClear = true, skipFirstRoom = true;
    }

    public class MetaTable
    {
        public RunRules rules;
        public List<UnlockDef> unlocks;
        public List<RoomAffixDef> affixes = new();
        public EliteRules elite = new();
        /// <summary>Classes available before any unlock.</summary>
        public List<string> startingClasses;
    }
}

import type { EliteRules, PerkRarity, RoomAffixDef, UnlockDef } from './types';

/** Rules of one run (局). Times ms, distances px, fractions of max HP. */
export const RUN_RULES = {
  /** Combat rooms before the boss. */
  combatRooms: 4,
  /** Perk choices offered after each combat room. */
  perkChoices: 3,
  rarityWeights: { common: 55, rare: 35, epic: 10 } as Record<PerkRarity, number>,
  /** Every offer has at least `minCommon` common cards and at most `maxEpic` epic cards, so there is always a
   *  "safe" pick and the epic never shows up twice on one screen. */
  perkOffer: { minCommon: 1, maxEpic: 1 },
  /** Starting loadout: class at Lv1 base stats with its 3 starting stat points already spent (two best stats +1,
   *  VIT +1), only the first skill learned at Lv1. The other three skills unlock on run levels (see levelUp). */
  startSkillLv: 1,
  startPotions: { red: 2, blue: 1 },
  /** In-run levelling: EXP is kept inside one run and resets every run. Levels are the "feel stronger every
   *  room" layer; perks are the "build" layer; stardust is the cross-run layer. */
  levelUp: {
    /** Enemy EXP multiplier by room slot (index 0 = first room). Later rooms give more so a level lands about once per room. */
    expScalingBySlot: [1.0, 1.3, 1.6, 2.0],
    bossExpMult: 2.0,
    /** HP restored on level up (fraction of max HP). Not a full heal: that made the run trivial in simulation. */
    heal: 0.3,
    /** Each level: +1 to the class's two highest base stats, +1 VIT (auto-allocated, no menu in a run). */
    statsPerLevel: 3,
    /** Each level grants this many skill points. The player spends them in the skill panel (learn a skill 0→1 or
     *  raise one by 1, up to SkillDef.maxLv). Points bank until spent; nothing is auto-spent (罐子 10/30). */
    skillPointsPerLevel: 1,
    /** Skill n (0-based) can be learned once the run level is at least skillUnlockLevels[n]; the panel shows
     *  「Lv N 解鎖」 before that. Keeps the "one new option per room" pacing while the player still chooses. */
    skillUnlockLevels: [1, 2, 3, 4],
    /** Learning a skill costs this many points; raising a learned skill by one level costs this many. */
    skillLearnCost: 1,
    skillRaiseCost: 1,
    /** Points the first skill costs at run start: it is pre-learned at Lv1 for free. */
    startSkills: 1,
    /** Opening the skill panel pauses the fight (single player, no timer). */
    panelPausesGame: true,
  },
  /** HP restored when a combat room is cleared. */
  roomClearHeal: 0.2,
  /** Enemy multipliers by room slot (index 0 = first room). Boss room uses `boss`. */
  roomScaling: {
    hp: [1.5, 2.2, 3.0, 4.0],
    atk: [1.0, 1.25, 1.5, 1.8],
    boss: { hp: 2.5, atk: 1.4 },
  },
  /** Enemy → player damage: max(atk × minFraction, atk × power − DEF × defCoef). The floor keeps high-DEF
   *  classes from becoming immune to early enemies once run levels pile on VIT. */
  enemyDamage: { minFraction: 0.35, defCoef: 0.6 },
  /** Ms between the door opening and the next room (walk-through transition). */
  roomTransition: 1200,
  /** Extra score on the result screen is not used in the beta; stardust is the only reward. */
  stardust: {
    /** Every finished run, win or lose, even a death in room 1: no run is ever "for nothing". */
    runEnd: 5,
    roomClear: 10,
    bossKill: 40,
    runClear: 30,
    /** One-time bonus the first time the boss is beaten (unlock hook). */
    firstClearBonus: 60,
  },
  /** Beta target: a run of ~4 min; first clear around the 4th–8th run. */
  targetRunMs: 240000,

  // ---- 封測 2（11/1 凍結）----
  /** Epic cards (CARDS). Account-wide, any class, kept forever (罐子 11/1). */
  cards: {
    /** Equip slots before unlocks; `cardSlot` unlock adds one. */
    startSlots: 2,
    maxSlots: 3,
    /** The boss's signature card drops on its first kill; every later kill drops one random card of its pool. */
    firstKillSignature: true,
    dropsPerKill: 1,
    /** A duplicate becomes this many shards; `shardsPerStar` shards raise the card one star up to `maxStars`. */
    shardsPerDuplicate: 1,
    shardsPerStar: 3,
    maxStars: 3,
    /** Legendary cards are not in beta 2 pools; kept for the 1.0 drop rule (紫霧等級 ≥ 3). */
    legendaryEnabled: false,
  },
  /** Class talent lines (TALENTS). */
  talents: {
    nodesPerClass: 10,
    /** Reset refunds every stardust spent (罐子 11/1: 可重置). */
    resetFree: true,
    /** Nodes must be bought in tier order. */
    linear: true,
  },
  /** Room affixes (ROOM_AFFIXES): rolled per combat room, only on stages the player has cleared before. */
  affixes: {
    chancePerRoom: 0.5,
    maxPerRoom: 1,
    onlyAfterStageClear: true,
    /** Never on the first room of a run. */
    skipFirstRoom: true,
  },
  /** Mid-run resume (罐子 11/1): the run is saved at every room start and restored to that room's start. */
  resume: { enabled: true, savePoint: 'roomStart' as const },
};

/** 房間詞綴 v1（5 種）。同一章第 2 次玩起才出現，讓重玩同章也有變化。 */
export const ROOM_AFFIXES: RoomAffixDef[] = [
  { id: 'fog', name: '濃霧', desc: '視野縮小，敵人出現前有紫光提示', effect: { kind: 'fog', visionMult: 0.6, warnMs: 800 } },
  { id: 'poisonGround', name: '毒沼', desc: '地面兩塊區域持續扣血', effect: { kind: 'poisonGround', zones: 2, dpsFraction: 0.01 } },
  { id: 'treasure', name: '寶箱房', desc: '清完多一次三選一', effect: { kind: 'treasure', extraPerkPicks: 1 } },
  { id: 'elite', name: '精英房', desc: '必定出現精英，星塵 ×2', effect: { kind: 'elite', forceElite: true, stardustMult: 2 } },
  { id: 'rush', name: '急襲', desc: '波次間隔減半，敵人 HP -20%', effect: { kind: 'rush', waveTimeoutMult: 0.5, hpMult: 0.8 } },
];

/** 精英怪：金色輪廓、放大、一個詞綴。第 1 章關閉，第 2 章 15%，第 3 章 25%。 */
export const ELITE: EliteRules = {
  chanceByChapterOrder: [0, 0.15, 0.25],
  hpMult: 2.5,
  atkMult: 1.4,
  sizeMult: 1.3,
  stardust: 10,
  modifiers: ['shield', 'haste', 'volatile'],
  shieldHits: 3,
  hasteMult: 1.4,
  volatilePower: 1.0,
  volatileRadius: 140,
  excludeBehaviors: ['turret', 'burrower', 'bossSlime', 'bossTreant', 'bossGuardian'],
};

/** 局外解鎖表 v1。花星塵購買，永久生效。 */
export const UNLOCKS: UnlockDef[] = [
  // 價格依「第 2–3 局買到第一項、第 8 局前買完」排：三局輸局約 45–75 星塵，一次通關約 120。
  { id: 'extraPotion', name: '旅行藥袋', desc: '每局開始多 1 瓶紅藥水', cost: 40, kind: 'startPotion', value: 1 },
  { id: 'classMage', name: '法師加入', desc: '解鎖職業：法師', cost: 70, kind: 'class', value: 'mage' },
  { id: 'luckyStar', name: '幸運星', desc: '稀有與史詩強化出現率提高', cost: 90, kind: 'rareChance', value: 0.15 },
  { id: 'classArcher', name: '遊俠加入', desc: '解鎖職業：遊俠', cost: 110, kind: 'class', value: 'archer' },
  { id: 'veteran', name: '老練冒險者', desc: '每局開始時先選一個強化', cost: 150, kind: 'startPerk', value: 1 },
  // 封測 2：第 3 個卡片格（罐子 11/1：起始 2 格）。
  { id: 'cardSlot3', name: '卡匣擴充', desc: '史詩卡片裝備格 +1', cost: 120, kind: 'cardSlot', value: 1 },
];

/** Classes available before any unlock. */
export const STARTING_CLASSES = ['swordsman'];

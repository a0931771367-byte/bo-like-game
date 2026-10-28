import type { PerkRarity, UnlockDef } from './types';

/** Rules of one run (局). Times ms, distances px, fractions of max HP. */
export const RUN_RULES = {
  /** Combat rooms before the boss. */
  combatRooms: 4,
  /** Perk choices offered after each combat room. */
  perkChoices: 3,
  rarityWeights: { common: 60, rare: 32, epic: 8 } as Record<PerkRarity, number>,
  /** Starting loadout: class at Lv1 base stats, every skill at Lv1, no stat points. */
  startSkillLv: 1,
  startPotions: { red: 2, blue: 1 },
  /** HP restored when a combat room is cleared. */
  roomClearHeal: 0.2,
  /** Enemy multipliers by room slot (index 0 = first room). Boss room uses `boss`. */
  roomScaling: {
    hp: [1.0, 1.2, 1.4, 1.6],
    atk: [1.0, 1.1, 1.2, 1.35],
    boss: { hp: 1.0, atk: 1.0 },
  },
  /** Ms between the door opening and the next room (walk-through transition). */
  roomTransition: 1200,
  /** Extra score on the result screen is not used in the beta; stardust is the only reward. */
  stardust: {
    roomClear: 8,
    bossKill: 40,
    runClear: 20,
    /** One-time bonus the first time the boss is beaten (unlock hook). */
    firstClearBonus: 60,
  },
  /** Beta target: a run of ~4 min; first clear around the 4th–8th run. */
  targetRunMs: 240000,
};

/** 局外解鎖表 v1。花星塵購買，永久生效。 */
export const UNLOCKS: UnlockDef[] = [
  { id: 'extraPotion', name: '旅行藥袋', desc: '每局開始多 1 瓶紅藥水', cost: 40, kind: 'startPotion', value: 1 },
  { id: 'classMage', name: '法師加入', desc: '解鎖職業：法師', cost: 80, kind: 'class', value: 'mage' },
  { id: 'luckyStar', name: '幸運星', desc: '稀有與史詩強化出現率提高', cost: 100, kind: 'rareChance', value: 0.1 },
  { id: 'classArcher', name: '遊俠加入', desc: '解鎖職業：遊俠', cost: 120, kind: 'class', value: 'archer' },
  { id: 'veteran', name: '老練冒險者', desc: '每局開始時先選一個強化', cost: 160, kind: 'startPerk', value: 1 },
];

/** Classes available before any unlock. */
export const STARTING_CLASSES = ['swordsman'];

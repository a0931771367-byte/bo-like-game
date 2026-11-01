import type { EnemyDef } from './types';

export const ENEMIES: Record<string, EnemyDef> = {
  jelly: { id: 'jelly', name: '果凍怪', hp: 48, atk: 11, def: 0, mdef: 0, element: 'neutral', exp: 9, gold: 6, speed: 95,
    w: 46, h: 40, color: 0xff8fb1, behavior: 'hopper', attackRange: 60, windup: 380, attackCd: 1500, spriteH: 72 },
  bug: { id: 'bug', name: '甲殼蟲', hp: 80, atk: 13, def: 4, mdef: 1, element: 'earth', exp: 14, gold: 9, speed: 70,
    w: 58, h: 38, color: 0x7aa84a, behavior: 'melee', attackRange: 75, windup: 520, attackCd: 1800, spriteH: 78 },
  wolf: { id: 'wolf', name: '野狼', hp: 70, atk: 16, def: 2, mdef: 0, element: 'earth', exp: 18, gold: 11, speed: 170,
    w: 70, h: 44, color: 0x8a8f9c, behavior: 'lunger', attackRange: 240, windup: 450, attackCd: 2200, spriteH: 88 },
  jellyKing: { id: 'jellyKing', name: '巨大果凍王', hp: 1100, atk: 20, def: 3, mdef: 2, element: 'neutral', exp: 160, gold: 120, speed: 80,
    w: 150, h: 130, color: 0xff6f9c, behavior: 'bossSlime', attackRange: 120, windup: 650, attackCd: 1600, boss: true, spriteH: 210 },

  // ---- chapter 1 variants (recoloured) ----
  fireJelly: { id: 'fireJelly', sprite: 'jelly', tint: 0xff8a5a, name: '火焰果凍', hp: 70, atk: 15, def: 1, mdef: 2, element: 'fire', exp: 15, gold: 9, speed: 120,
    w: 46, h: 40, color: 0xff6a3a, behavior: 'hopper', attackRange: 60, windup: 320, attackCd: 1300, spriteH: 72 },
  ironBug: { id: 'ironBug', sprite: 'bug', tint: 0xb8c4d6, name: '鐵甲蟲', hp: 140, atk: 16, def: 10, mdef: 3, element: 'earth', exp: 24, gold: 14, speed: 60,
    w: 58, h: 38, color: 0x9aa4b4, behavior: 'melee', attackRange: 75, windup: 560, attackCd: 1900, spriteH: 82 },
  nightWolf: { id: 'nightWolf', sprite: 'wolf', tint: 0x8a7ccf, name: '夜狼', hp: 110, atk: 21, def: 3, mdef: 1, element: 'dark', exp: 28, gold: 16, speed: 195,
    w: 70, h: 44, color: 0x6c5fb0, behavior: 'lunger', attackRange: 260, windup: 400, attackCd: 2000, spriteH: 88 },
  // ---- chapter 2: misty forest ----
  bee: { id: 'bee', name: '大蜜蜂', hp: 85, atk: 17, def: 1, mdef: 2, element: 'wind', exp: 22, gold: 12, speed: 150,
    w: 56, h: 46, color: 0xf5c518, behavior: 'flyer', attackRange: 80, windup: 320, attackCd: 1500, spriteH: 82 },
  flower: { id: 'flower', name: '食人花', hp: 130, atk: 19, def: 3, mdef: 4, element: 'earth', exp: 28, gold: 15, speed: 0,
    w: 70, h: 110, color: 0xd93b3b, behavior: 'turret', attackRange: 520, windup: 600, attackCd: 2400, spriteH: 150 },
  mushroom: { id: 'mushroom', name: '毒蘑菇', hp: 170, atk: 21, def: 6, mdef: 6, element: 'poison', exp: 32, gold: 17, speed: 50,
    w: 80, h: 70, color: 0x9b59b6, behavior: 'melee', attackRange: 80, windup: 600, attackCd: 2000, spriteH: 104 },
  treant: { id: 'treant', name: '樹精母體', hp: 2600, atk: 30, def: 8, mdef: 6, element: 'earth', exp: 420, gold: 320, speed: 60,
    w: 160, h: 180, color: 0x6b8e3a, behavior: 'bossTreant', attackRange: 170, windup: 700, attackCd: 1800, boss: true, spriteH: 300 },

  wolfLord: { id: 'wolfLord', sprite: 'wolf', tint: 0xd8d8e8, name: '銀牙狼王', hp: 1700, atk: 26, def: 6, mdef: 3, element: 'earth', exp: 260, gold: 200, speed: 190,
    w: 120, h: 70, color: 0xdcdcf0, behavior: 'lunger', attackRange: 320, windup: 500, attackCd: 1900, boss: true, spriteH: 150 },

  // ---- chapter 3: desert ruins (封測 2，11/1 凍結；名稱待美術定稿後可微調，id 不改) ----
  // 基礎值比第 2 章高一階（蘑菇 170/21 → 木乃伊 170/25、沙蟹 210/23），章內再吃 RUN_STAGES 的倍率。
  sandworm: { id: 'sandworm', name: '沙蟲', hp: 110, atk: 24, def: 3, mdef: 2, element: 'earth', exp: 30, gold: 16, speed: 90,
    w: 64, h: 50, color: 0xd9b36b, behavior: 'burrower', attackRange: 90, windup: 420, attackCd: 1700, spriteH: 96,
    burrow: { triggerRange: 220, emergeDelay: 1200, rest: 4000 } },
  cactusTurret: { id: 'cactusTurret', name: '仙人掌哨', hp: 140, atk: 22, def: 8, mdef: 4, element: 'earth', exp: 30, gold: 16, speed: 0,
    w: 60, h: 110, color: 0x5fae5a, behavior: 'turret', attackRange: 560, windup: 500, attackCd: 2500, spriteH: 150, volley: 3 },
  mummy: { id: 'mummy', name: '小木乃伊', hp: 170, atk: 25, def: 5, mdef: 5, element: 'dark', exp: 36, gold: 18, speed: 75,
    w: 56, h: 80, color: 0xe8dcc0, behavior: 'reviver', attackRange: 80, windup: 560, attackCd: 1900, spriteH: 112,
    revive: { delay: 3000, pileHp: 60, fraction: 0.5 } },
  sandCrab: { id: 'sandCrab', name: '沙蟹', hp: 210, atk: 23, def: 12, mdef: 3, element: 'earth', exp: 38, gold: 20, speed: 110,
    w: 90, h: 50, color: 0xd98a5a, behavior: 'crab', attackRange: 85, windup: 480, attackCd: 1800, spriteH: 84,
    facingDef: { front: 0.5, back: 1.6 } },
  // Boss：砸地震波（跳躍可躲）、雷射掃射（前搖 700 ms 紅線預告、閃避穿過）、HP 50% 石化護甲 4 秒並召 2 木乃伊。
  ruinGuardian: { id: 'ruinGuardian', name: '遺跡守衛石像', hp: 3400, atk: 34, def: 10, mdef: 8, element: 'neutral', exp: 520, gold: 400, speed: 55,
    w: 170, h: 200, color: 0xb8a98a, behavior: 'bossGuardian', attackRange: 200, windup: 700, attackCd: 2000, boss: true, spriteH: 320,
    phases: [{ at: 0.5, action: 'petrify', duration: 4000, spawn: ['mummy', 'mummy'] }, { at: 0.2, action: 'enrage', mult: 1.3 }] },
};

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
};

import type { RoomDef, RoomWaveDef, SpawnDef } from './types';

const sp = (enemy: string, side: 'left' | 'right', delay: number): SpawnDef => ({ enemy, side, delay });
const wave = (timeout: number, ...spawns: SpawnDef[]): RoomWaveDef => ({ spawns, timeout });

/**
 * 草原房間 v1（封測版）。一局 = 4 個戰鬥房（由 ROOM_POOL 以種子不重複抽出並隨機排序）+ Boss 房。
 * 每房 3 波；波次在前一波清完或 timeout 到時開始，所以最壞情況一房也有時間上限。
 * 房間編號越後面的槽位越難，數值放大見 RUN_RULES.roomScaling；房間本身不寫倍率。
 */
export const ROOMS: Record<string, RoomDef> = {
  meadow: {
    id: 'meadow', name: '草原小徑', scene: 'grassland', kind: 'combat', width: 1800, bg: 'bg_chapter1', bgGroundY: 528,
    waveSets: [
      [
        wave(12000, sp('jelly', 'right', 0), sp('jelly', 'right', 500), sp('jelly', 'left', 1200)),
        wave(14000, sp('jelly', 'right', 0), sp('bug', 'right', 600), sp('jelly', 'left', 1000), sp('jelly', 'left', 1600)),
        wave(16000, sp('bug', 'left', 0), sp('jelly', 'right', 400), sp('jelly', 'right', 800), sp('wolf', 'left', 2200)),
      ],
      [
        wave(12000, sp('jelly', 'left', 0), sp('jelly', 'left', 400), sp('bug', 'right', 1000)),
        wave(14000, sp('jelly', 'right', 0), sp('jelly', 'left', 300), sp('jelly', 'right', 900), sp('bug', 'left', 1800)),
        wave(16000, sp('wolf', 'right', 0), sp('jelly', 'left', 600), sp('jelly', 'left', 900), sp('bug', 'right', 2000)),
      ],
    ],
  },
  beetleNest: {
    id: 'beetleNest', name: '甲蟲巢', scene: 'grassland', kind: 'combat', width: 1700, bg: 'bg_chapter1', bgGroundY: 528, tint: 0xd8e8c0,
    waveSets: [
      [
        wave(14000, sp('bug', 'right', 0), sp('bug', 'left', 600), sp('jelly', 'right', 1400)),
        wave(16000, sp('bug', 'right', 0), sp('bug', 'right', 400), sp('bug', 'left', 1200), sp('jelly', 'left', 2000)),
        wave(18000, sp('bug', 'left', 0), sp('bug', 'right', 300), sp('fireJelly', 'right', 1500), sp('bug', 'left', 2400)),
      ],
      [
        wave(14000, sp('bug', 'left', 0), sp('jelly', 'right', 500), sp('bug', 'right', 1200)),
        wave(16000, sp('bug', 'right', 0), sp('bug', 'left', 200), sp('jelly', 'left', 1400), sp('jelly', 'right', 1800)),
        wave(18000, sp('bug', 'right', 0), sp('bug', 'left', 400), sp('bug', 'right', 1600), sp('wolf', 'left', 2600)),
      ],
    ],
  },
  wolfAmbush: {
    id: 'wolfAmbush', name: '狼群突襲', scene: 'grassland', kind: 'combat', width: 2000, bg: 'bg_chapter1', bgGroundY: 528, tint: 0xb8c4e8,
    waveSets: [
      [
        wave(12000, sp('wolf', 'right', 0), sp('wolf', 'left', 900)),
        wave(14000, sp('wolf', 'right', 0), sp('jelly', 'left', 400), sp('wolf', 'left', 1600), sp('jelly', 'right', 2000)),
        wave(16000, sp('nightWolf', 'right', 0), sp('wolf', 'left', 800), sp('wolf', 'right', 1800)),
      ],
      [
        wave(12000, sp('wolf', 'left', 0), sp('jelly', 'right', 600), sp('wolf', 'right', 1400)),
        wave(14000, sp('wolf', 'right', 0), sp('wolf', 'right', 300), sp('bug', 'left', 1200), sp('wolf', 'left', 2200)),
        wave(16000, sp('nightWolf', 'left', 0), sp('nightWolf', 'right', 1200), sp('jelly', 'left', 2000)),
      ],
    ],
  },
  emberField: {
    id: 'emberField', name: '餘燼草地', scene: 'grassland', kind: 'combat', width: 1800, bg: 'bg_chapter1', bgGroundY: 528, tint: 0xffb488,
    waveSets: [
      [
        wave(12000, sp('fireJelly', 'right', 0), sp('jelly', 'left', 500), sp('fireJelly', 'left', 1200)),
        wave(14000, sp('fireJelly', 'right', 0), sp('fireJelly', 'right', 400), sp('jelly', 'left', 1000), sp('bug', 'left', 1800)),
        wave(16000, sp('fireJelly', 'left', 0), sp('fireJelly', 'right', 300), sp('fireJelly', 'right', 600), sp('wolf', 'left', 2000), sp('jelly', 'right', 2600)),
      ],
      [
        wave(12000, sp('jelly', 'right', 0), sp('fireJelly', 'left', 400), sp('jelly', 'left', 1000), sp('fireJelly', 'right', 1600)),
        wave(14000, sp('fireJelly', 'left', 0), sp('bug', 'right', 500), sp('fireJelly', 'right', 1200), sp('fireJelly', 'left', 2200)),
        wave(16000, sp('fireJelly', 'right', 0), sp('fireJelly', 'left', 0), sp('nightWolf', 'right', 1500), sp('jelly', 'left', 2400)),
      ],
    ],
  },
  jellyThrone: {
    id: 'jellyThrone', name: '果凍王的王座', scene: 'grassland', kind: 'boss', width: 1600, bg: 'bg_chapter1', bgGroundY: 528, tint: 0xf0c8d8,
    waveSets: [
      [
        wave(0, sp('jellyKing', 'right', 600)),
        // Boss adds: start on a timer, not on clear (the boss is still alive).
        wave(0, sp('jelly', 'left', 15000), sp('jelly', 'left', 15400), sp('fireJelly', 'right', 30000), sp('fireJelly', 'left', 30400)),
      ],
    ],
  },
};

/** Combat rooms the seed draws from (4 picked per run, no repeats, random order). */
export const ROOM_POOL = ['meadow', 'beetleNest', 'wolfAmbush', 'emberField'];
export const BOSS_ROOM = 'jellyThrone';

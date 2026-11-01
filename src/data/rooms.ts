import type { ChapterDef, RoomDef, RoomWaveDef, RunStageDef, SpawnDef } from './types';

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

  // ---- 蘑菇森林（封測 2 第 2 章；敵人與美術已存在）----
  mistTrail: {
    id: 'mistTrail', name: '迷霧小徑', scene: 'mistForest', kind: 'combat', width: 1800, bg: 'bg_chapter2', bgGroundY: 546,
    waveSets: [
      [
        wave(12000, sp('bee', 'right', 0), sp('bee', 'right', 500), sp('jelly', 'left', 1200)),
        wave(14000, sp('bee', 'right', 0), sp('bee', 'left', 400), sp('flower', 'right', 1200), sp('bee', 'left', 2000)),
        wave(16000, sp('flower', 'left', 0), sp('bee', 'right', 500), sp('bee', 'right', 800), sp('mushroom', 'left', 2200)),
      ],
      [
        wave(12000, sp('bee', 'left', 0), sp('bee', 'right', 400), sp('bug', 'right', 1200)),
        wave(14000, sp('flower', 'right', 0), sp('bee', 'left', 600), sp('bee', 'left', 900), sp('bee', 'right', 1800)),
        wave(16000, sp('mushroom', 'right', 0), sp('bee', 'left', 400), sp('bee', 'left', 700), sp('flower', 'right', 2000)),
      ],
    ],
  },
  sporeHollow: {
    id: 'sporeHollow', name: '孢子窪地', scene: 'mistForest', kind: 'combat', width: 1700, bg: 'bg_chapter2', bgGroundY: 546, tint: 0xbfe6d8,
    waveSets: [
      [
        wave(14000, sp('mushroom', 'right', 0), sp('bee', 'left', 600), sp('mushroom', 'left', 1600)),
        wave(16000, sp('mushroom', 'right', 0), sp('mushroom', 'left', 300), sp('bee', 'right', 1200), sp('bee', 'left', 1500)),
        wave(18000, sp('mushroom', 'right', 0), sp('flower', 'left', 400), sp('mushroom', 'left', 1600), sp('bee', 'right', 2400)),
      ],
      [
        wave(14000, sp('mushroom', 'left', 0), sp('mushroom', 'right', 400), sp('bee', 'right', 1200)),
        wave(16000, sp('flower', 'right', 0), sp('mushroom', 'left', 500), sp('mushroom', 'right', 1400), sp('bee', 'left', 2200)),
        wave(18000, sp('mushroom', 'right', 0), sp('mushroom', 'left', 0), sp('bee', 'right', 1200), sp('bee', 'left', 1500), sp('flower', 'right', 2600)),
      ],
    ],
  },
  thornGarden: {
    id: 'thornGarden', name: '荊棘花園', scene: 'mistForest', kind: 'combat', width: 2000, bg: 'bg_chapter2', bgGroundY: 546, tint: 0xd8c0e8,
    waveSets: [
      [
        wave(14000, sp('flower', 'right', 0), sp('flower', 'left', 0), sp('bee', 'right', 1000)),
        wave(16000, sp('flower', 'right', 0), sp('bee', 'left', 500), sp('bee', 'left', 800), sp('mushroom', 'right', 1800)),
        wave(18000, sp('flower', 'left', 0), sp('flower', 'right', 300), sp('bee', 'right', 1200), sp('bee', 'left', 1500), sp('nightWolf', 'right', 2600)),
      ],
      [
        wave(14000, sp('flower', 'left', 0), sp('bee', 'right', 400), sp('bee', 'right', 700)),
        wave(16000, sp('flower', 'right', 0), sp('flower', 'left', 400), sp('mushroom', 'left', 1400), sp('bee', 'right', 2000)),
        wave(18000, sp('flower', 'right', 0), sp('bee', 'left', 300), sp('bee', 'left', 600), sp('mushroom', 'right', 1600), sp('nightWolf', 'left', 2600)),
      ],
    ],
  },
  wolfDen: {
    id: 'wolfDen', name: '夜狼巢穴', scene: 'mistForest', kind: 'combat', width: 2000, bg: 'bg_chapter2', bgGroundY: 546, tint: 0x9aa8d0,
    waveSets: [
      [
        wave(12000, sp('nightWolf', 'right', 0), sp('bee', 'left', 600), sp('nightWolf', 'left', 1400)),
        wave(14000, sp('nightWolf', 'right', 0), sp('nightWolf', 'left', 300), sp('mushroom', 'right', 1500), sp('bee', 'left', 2200)),
        wave(16000, sp('nightWolf', 'right', 0), sp('nightWolf', 'right', 400), sp('nightWolf', 'left', 800), sp('flower', 'left', 2000)),
      ],
      [
        wave(12000, sp('nightWolf', 'left', 0), sp('nightWolf', 'right', 800), sp('bee', 'right', 1600)),
        wave(14000, sp('mushroom', 'right', 0), sp('nightWolf', 'left', 500), sp('nightWolf', 'right', 1300), sp('bee', 'left', 2000)),
        wave(16000, sp('nightWolf', 'right', 0), sp('nightWolf', 'left', 300), sp('bee', 'right', 1000), sp('bee', 'left', 1300), sp('nightWolf', 'right', 2400)),
      ],
    ],
  },
  heartOfTheWood: {
    id: 'heartOfTheWood', name: '古樹之心', scene: 'mistForest', kind: 'boss', width: 1600, bg: 'bg_chapter2', bgGroundY: 546, tint: 0xa8bcc8,
    waveSets: [
      [
        wave(0, sp('treant', 'right', 800)),
        wave(0, sp('bee', 'left', 15000), sp('bee', 'left', 15300), sp('flower', 'left', 30000), sp('bee', 'right', 45000), sp('bee', 'right', 45300)),
      ],
    ],
  },

  // ---- 沙漠遺跡（封測 2 第 3 章，11/1 凍結）----
  // 房間主題：沙丘教潛行竄出、仙人掌練走位與掩體、墓廳教「打碎繃帶堆」、蟹灘教繞背。
  sandDune: {
    id: 'sandDune', name: '流沙沙丘', scene: 'desertRuins', kind: 'combat', width: 1900, bg: 'bg_chapter3', bgGroundY: 540,
    waveSets: [
      [
        wave(13000, sp('sandworm', 'right', 0), sp('sandworm', 'left', 800), sp('bug', 'right', 1600)),
        wave(15000, sp('sandworm', 'right', 0), sp('sandworm', 'right', 400), sp('sandCrab', 'left', 1200)),
        wave(17000, sp('sandworm', 'left', 0), sp('sandworm', 'right', 300), sp('cactusTurret', 'right', 900), sp('sandworm', 'left', 2200)),
      ],
      [
        wave(13000, sp('sandworm', 'left', 0), sp('bug', 'right', 500), sp('sandworm', 'right', 1300)),
        wave(15000, sp('sandworm', 'right', 0), sp('sandworm', 'left', 300), sp('sandworm', 'left', 900), sp('mummy', 'right', 2000)),
        wave(17000, sp('sandCrab', 'right', 0), sp('sandworm', 'left', 600), sp('sandworm', 'right', 1200), sp('sandworm', 'left', 2400)),
      ],
    ],
  },
  cactusGrove: {
    id: 'cactusGrove', name: '仙人掌林', scene: 'desertRuins', kind: 'combat', width: 2000, bg: 'bg_chapter3', bgGroundY: 540, tint: 0xf0dca8,
    waveSets: [
      [
        wave(15000, sp('cactusTurret', 'right', 0), sp('cactusTurret', 'left', 0), sp('sandworm', 'right', 1200)),
        wave(17000, sp('cactusTurret', 'right', 0), sp('sandCrab', 'left', 600), sp('sandworm', 'right', 1500), sp('sandworm', 'left', 2200)),
        wave(19000, sp('cactusTurret', 'left', 0), sp('cactusTurret', 'right', 300), sp('mummy', 'right', 1200), sp('sandworm', 'left', 2000), sp('sandCrab', 'right', 2800)),
      ],
      [
        wave(15000, sp('cactusTurret', 'left', 0), sp('sandworm', 'right', 500), sp('sandworm', 'right', 900)),
        wave(17000, sp('cactusTurret', 'right', 0), sp('cactusTurret', 'left', 400), sp('mummy', 'left', 1400), sp('sandworm', 'right', 2200)),
        wave(19000, sp('cactusTurret', 'right', 0), sp('sandCrab', 'left', 400), sp('sandCrab', 'left', 800), sp('sandworm', 'right', 1800), sp('cactusTurret', 'left', 2600)),
      ],
    ],
  },
  tombHall: {
    id: 'tombHall', name: '陵墓長廊', scene: 'desertRuins', kind: 'combat', width: 1800, bg: 'bg_chapter3', bgGroundY: 540, tint: 0xc8b8d8,
    waveSets: [
      [
        wave(15000, sp('mummy', 'right', 0), sp('mummy', 'left', 700), sp('sandworm', 'right', 1600)),
        wave(17000, sp('mummy', 'right', 0), sp('mummy', 'right', 400), sp('cactusTurret', 'left', 1200), sp('sandworm', 'left', 2000)),
        wave(19000, sp('mummy', 'left', 0), sp('mummy', 'right', 300), sp('mummy', 'right', 600), sp('sandCrab', 'left', 2000), sp('nightWolf', 'right', 2800)),
      ],
      [
        wave(15000, sp('mummy', 'left', 0), sp('sandworm', 'right', 600), sp('mummy', 'right', 1400)),
        wave(17000, sp('mummy', 'right', 0), sp('mummy', 'left', 300), sp('sandCrab', 'right', 1500), sp('sandworm', 'left', 2200)),
        wave(19000, sp('mummy', 'right', 0), sp('mummy', 'left', 0), sp('cactusTurret', 'right', 1000), sp('mummy', 'left', 1800), sp('nightWolf', 'right', 2600)),
      ],
    ],
  },
  crabShore: {
    id: 'crabShore', name: '乾河蟹灘', scene: 'desertRuins', kind: 'combat', width: 2000, bg: 'bg_chapter3', bgGroundY: 540, tint: 0xf8c8a0,
    waveSets: [
      [
        wave(14000, sp('sandCrab', 'right', 0), sp('sandCrab', 'left', 800), sp('sandworm', 'right', 1600)),
        wave(16000, sp('sandCrab', 'right', 0), sp('sandCrab', 'right', 400), sp('cactusTurret', 'left', 1200), sp('sandworm', 'left', 2000)),
        wave(18000, sp('sandCrab', 'left', 0), sp('sandCrab', 'right', 300), sp('mummy', 'right', 1200), sp('sandCrab', 'left', 2000), sp('sandworm', 'right', 2600)),
      ],
      [
        wave(14000, sp('sandCrab', 'left', 0), sp('sandworm', 'right', 500), sp('sandCrab', 'right', 1300)),
        wave(16000, sp('sandCrab', 'right', 0), sp('sandCrab', 'left', 300), sp('mummy', 'left', 1400), sp('sandworm', 'right', 2200)),
        wave(18000, sp('sandCrab', 'right', 0), sp('sandCrab', 'left', 0), sp('cactusTurret', 'right', 1000), sp('sandworm', 'left', 1800), sp('sandCrab', 'right', 2600)),
      ],
    ],
  },
  guardianAltar: {
    id: 'guardianAltar', name: '守衛的祭壇', scene: 'desertRuins', kind: 'boss', width: 1600, bg: 'bg_chapter3', bgGroundY: 540, tint: 0xe8d0a8,
    waveSets: [
      [
        wave(0, sp('ruinGuardian', 'right', 800)),
        // 定時加小怪；HP 50% 的石化階段另由 ENEMIES.ruinGuardian.phases 召 2 木乃伊。
        wave(0, sp('sandworm', 'left', 20000), sp('sandworm', 'left', 20400), sp('cactusTurret', 'left', 40000), sp('sandworm', 'right', 60000), sp('sandworm', 'right', 60400)),
      ],
    ],
  },
};

/** 第 1 章房池（封測 1 相容名）。 */
export const ROOM_POOL = ['meadow', 'beetleNest', 'wolfAmbush', 'emberField'];
export const BOSS_ROOM = 'jellyThrone';

/**
 * 章／關表（11/1 製作部凍結格式，見 design/07 §9）。封測 2 每章只有一關 `<chapter>-3`（4 房 + Boss）；
 * 正式版把 `-1`、`-2` 插到 stages 前面，關 id 與房間 id 不改，存檔沿用。
 * 解鎖：前一章 Boss 首殺（progress.bossFirstKills）。已解鎖的章可直接選擇開局（罐子 11/1）。
 */
export const CHAPTERS: Record<string, ChapterDef> = {
  grassland: { id: 'grassland', order: 1, name: '果凍草原', bg: 'bg_chapter1', stages: ['grassland-3'] },
  mistForest: { id: 'mistForest', order: 2, name: '蘑菇森林', bg: 'bg_chapter2', stages: ['mistForest-3'], unlock: { bossKill: 'jellyKing' }, bossTrack: 'boss_treant' },
  desertRuins: { id: 'desertRuins', order: 3, name: '沙漠遺跡', bg: 'bg_chapter3', stages: ['desertRuins-3'], unlock: { bossKill: 'treant' }, bossTrack: 'boss_guardian' },
};
export const CHAPTER_ORDER = ['grassland', 'mistForest', 'desertRuins'];

/**
 * 倍率：第 1 章沿用封測 1 校正中的 RUN_RULES.roomScaling；第 2、3 章敵人基礎值已高一階，槽位倍率從 1.0 以下起跳。
 * 模擬（tools/sim_run.mjs --stage=…，Lv1 無天賦無卡片的保守玩家，600 局）：第 2 章 劍 100／法 92／俠 76%，
 * 第 3 章 劍 100／法 96／俠 77%；第 1 章同條件為 100／88／89%。遊俠在模擬裡的風箏模型偏弱，實機應高於此。待 runs.jsonl 校正。
 */
export const RUN_STAGES: Record<string, RunStageDef> = {
  'grassland-3': {
    id: 'grassland-3', chapterId: 'grassland', index: 3, name: '布丁巨像之丘', desc: '果凍怪與野狼出沒的草原，盡頭是暴走的布丁巨像。',
    timeOfDay: 'day', combatRooms: 4, pool: ROOM_POOL, finale: { kind: 'boss', enemy: 'jellyKing', room: BOSS_ROOM },
    scaling: { hp: [1.5, 2.2, 3.0, 4.0], atk: [1.0, 1.25, 1.5, 1.8], boss: { hp: 2.5, atk: 1.4 } },
    affixes: ['fog', 'treasure', 'rush'], story: { before: '1-1', after: '1-1' },
  },
  'mistForest-3': {
    id: 'mistForest-3', chapterId: 'mistForest', index: 3, name: '古樹之心', desc: '紫霧籠罩的森林，蜜蜂、食人花與毒蘑菇埋伏其中。',
    timeOfDay: 'dusk', combatRooms: 4, pool: ['mistTrail', 'sporeHollow', 'thornGarden', 'wolfDen'], finale: { kind: 'boss', enemy: 'treant', room: 'heartOfTheWood' },
    scaling: { hp: [1.0, 1.4, 1.9, 2.5], atk: [0.9, 1.05, 1.2, 1.4], boss: { hp: 1.5, atk: 1.15 } },
    affixes: ['fog', 'poisonGround', 'treasure', 'elite', 'rush'], story: { before: '2-1', after: '2-3' },
  },
  'desertRuins-3': {
    id: 'desertRuins-3', chapterId: 'desertRuins', index: 3, name: '守衛的祭壇', desc: '流沙與陵墓之間，沉睡百年的守衛石像再度睜眼。',
    timeOfDay: 'day', combatRooms: 4, pool: ['sandDune', 'cactusGrove', 'tombHall', 'crabShore'], finale: { kind: 'boss', enemy: 'ruinGuardian', room: 'guardianAltar' },
    scaling: { hp: [0.8, 1.1, 1.5, 1.9], atk: [0.8, 0.9, 1.05, 1.2], boss: { hp: 1.15, atk: 1.0 } },
    affixes: ['fog', 'poisonGround', 'treasure', 'elite', 'rush'], story: { before: '3-1', after: '3-1' },
  },
};

/** @deprecated 相容封測 1 的程式；請改讀 CHAPTERS / RUN_STAGES。 */
export const SCENES: Record<string, { name: string; pool: string[]; boss: string }> = Object.fromEntries(
  CHAPTER_ORDER.map((c) => {
    const st = RUN_STAGES[CHAPTERS[c].stages[CHAPTERS[c].stages.length - 1]];
    return [c, { name: CHAPTERS[c].name, pool: st.pool, boss: st.finale.room! }];
  }),
);
export const SCENE_ORDER = CHAPTER_ORDER;

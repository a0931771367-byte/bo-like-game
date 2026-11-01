import type { ChapterDef, RunStageDef } from './types';

/**
 * 章 → 關 → 房池（設計部 07 §9）。封測 2 每章只有 Boss 關（<chapter>-3）；正式版把 -1、-2 插到前面，
 * 既有關 id 與房間 id 不改。章節解鎖只看「前一章 Boss 首殺」（存檔 bossFirstKills）。
 * 沙漠遺跡的房間、敵人與 Boss 等企劫 11/8 資料，到貨前 stages 為空，選單顯示「即將開放」。
 */
export const CHAPTERS: Record<string, ChapterDef> = {
  grassland: {
    id: 'grassland', order: 1, name: '果凍草原', desc: '王都郊外，發狂的果凍怪與野狼。', bg: 'bg_chapter1',
    stages: ['grassland-3'], music: 'field', ambience: 'amb_ch1_grassland', bossTrack: 'boss',
  },
  mistForest: {
    id: 'mistForest', order: 2, name: '蘑菇森林', desc: '紫霧籠罩的林間，飛行的蜜蜂與毒蘑菇。', bg: 'bg_chapter2',
    stages: ['mistForest-3'], unlock: { bossKill: 'jellyKing' }, music: 'forest', ambience: 'amb_ch2_forest', bossTrack: 'boss',
  },
  desertRuins: {
    id: 'desertRuins', order: 3, name: '沙漠遺跡', desc: '百年前封印紫霧的古王國遺跡。', bg: 'bg_chapter3',
    stages: [], unlock: { bossKill: 'treant' }, music: 'field', bossTrack: 'boss',
  },
};

export const CHAPTER_ORDER = ['grassland', 'mistForest', 'desertRuins'];

export const RUN_STAGES: Record<string, RunStageDef> = {
  'grassland-3': {
    id: 'grassland-3', chapterId: 'grassland', index: 3, name: '果凍王之丘', timeOfDay: 'day', combatRooms: 4,
    pool: ['meadow', 'beetleNest', 'wolfAmbush', 'emberField'],
    finale: { kind: 'boss', enemy: 'jellyKing', room: 'jellyThrone' },
    scaling: { hp: [1.5, 2.2, 3.0, 4.0], atk: [1.0, 1.25, 1.5, 1.8], boss: { hp: 2.5, atk: 1.4 } },
    affixes: [],
    storyBefore: '1-1', storyAfter: '1-1',
  },
  'mistForest-3': {
    id: 'mistForest-3', chapterId: 'mistForest', index: 3, name: '古樹之心', timeOfDay: 'dusk', combatRooms: 4,
    pool: ['mistTrail', 'sporeHollow', 'thornGarden', 'wolfDen'],
    finale: { kind: 'boss', enemy: 'treant', room: 'heartOfTheWood' },
    // 設計部 04 §2：每場景從 ×1.25 重新起跳，基礎敵人本身更強；樹精 HP 2600 不再吃 ×2.5（待設計部校正）。
    scaling: { hp: [1.25, 1.8, 2.4, 3.2], atk: [1.0, 1.2, 1.4, 1.6], boss: { hp: 1.0, atk: 1.0 } },
    affixes: [],
    storyBefore: '2-1', storyAfter: '2-1',
  },
};

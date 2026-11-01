import type { ChapterDef, RunStageDef } from './types';
import { BOSS_ROOM, ROOM_POOL } from './rooms';

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

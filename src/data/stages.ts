import type { StageDef } from './types';

const sp = (enemy: string, side: 'left' | 'right', delay: number) => ({ enemy, side, delay });

export const STAGES: Record<string, StageDef> = {
  '1-1': {
    id: '1-1', chapter: '第 1 章', name: '王都郊外草原', desc: '果凍怪與野狼出沒的草原，適合熟悉操作。', width: 4200,
    bg: 'bg_chapter1', bgGroundY: 420,
    waves: [
      { triggerX: 500, spawns: [sp('jelly', 'right', 0), sp('jelly', 'right', 400), sp('jelly', 'left', 1200)] },
      { triggerX: 1500, spawns: [sp('jelly', 'right', 0), sp('bug', 'right', 300), sp('jelly', 'left', 900), sp('bug', 'left', 2500)] },
      { triggerX: 2500, spawns: [sp('wolf', 'right', 0), sp('jelly', 'left', 400), sp('wolf', 'left', 1800), sp('jelly', 'right', 2400), sp('bug', 'right', 3200)] },
      { triggerX: 3500, spawns: [sp('jellyKing', 'right', 600)] },
    ],
  },
  '1-2': {
    id: '1-2', chapter: '第 1 章', name: '黃昏林道', desc: '夕陽下的林道，火焰果凍與鐵甲蟲成群出現。', width: 4800,
    bg: 'bg_chapter1', bgGroundY: 420, tint: 0xffb488,
    waves: [
      { triggerX: 500, spawns: [sp('jelly', 'right', 0), sp('fireJelly', 'right', 500), sp('jelly', 'left', 1000), sp('fireJelly', 'left', 1800)] },
      { triggerX: 1400, spawns: [sp('ironBug', 'right', 0), sp('jelly', 'right', 600), sp('bug', 'left', 1200), sp('fireJelly', 'right', 2600), sp('fireJelly', 'left', 3000)] },
      { triggerX: 2400, spawns: [sp('wolf', 'right', 0), sp('wolf', 'left', 300), sp('ironBug', 'right', 1500), sp('fireJelly', 'left', 2200), sp('jelly', 'right', 2800)] },
      { triggerX: 3400, spawns: [sp('ironBug', 'right', 0), sp('ironBug', 'left', 200), sp('fireJelly', 'right', 900), sp('fireJelly', 'right', 1200), sp('wolf', 'left', 2400), sp('bug', 'right', 3000)] },
      { triggerX: 4200, spawns: [sp('jellyKing', 'right', 400), sp('fireJelly', 'left', 3000), sp('fireJelly', 'left', 3400)] },
    ],
  },
  '1-3': {
    id: '1-3', chapter: '第 1 章', name: '月夜狼嚎', desc: '夜狼群的地盤，深處有銀牙狼王鎮守。', width: 5200,
    bg: 'bg_chapter1', bgGroundY: 420, tint: 0x7a80cc,
    waves: [
      { triggerX: 500, spawns: [sp('wolf', 'right', 0), sp('jelly', 'left', 400), sp('wolf', 'left', 1400), sp('nightWolf', 'right', 2400)] },
      { triggerX: 1400, spawns: [sp('nightWolf', 'right', 0), sp('ironBug', 'left', 400), sp('fireJelly', 'right', 1200), sp('nightWolf', 'left', 2000), sp('bug', 'right', 2800)] },
      { triggerX: 2400, spawns: [sp('nightWolf', 'right', 0), sp('nightWolf', 'left', 300), sp('ironBug', 'right', 1600), sp('ironBug', 'left', 1800), sp('fireJelly', 'right', 2600), sp('jelly', 'left', 3000)] },
      { triggerX: 3400, spawns: [sp('wolf', 'right', 0), sp('nightWolf', 'right', 400), sp('nightWolf', 'left', 800), sp('fireJelly', 'left', 1600), sp('fireJelly', 'right', 2000), sp('ironBug', 'left', 3000)] },
      { triggerX: 4500, spawns: [sp('wolfLord', 'right', 600), sp('nightWolf', 'left', 6000), sp('nightWolf', 'left', 12000)] },
    ],
  },
  '2-1': {
    id: '2-1', chapter: '第 2 章', name: '迷霧森林入口', desc: '紫霧籠罩的林間，蜜蜂與食人花埋伏其中。', width: 4800,
    bg: 'bg_chapter2', bgGroundY: 420,
    waves: [
      { triggerX: 500, spawns: [sp('bee', 'right', 0), sp('bee', 'right', 600), sp('jelly', 'left', 1200), sp('bee', 'left', 2000)] },
      { triggerX: 1400, spawns: [sp('flower', 'right', 0), sp('bee', 'right', 800), sp('bee', 'left', 1200), sp('bug', 'left', 2000), sp('bee', 'right', 3000)] },
      { triggerX: 2400, spawns: [sp('flower', 'right', 0), sp('flower', 'left', 300), sp('bee', 'right', 1200), sp('bee', 'right', 1500), sp('wolf', 'left', 2400)] },
      { triggerX: 3400, spawns: [sp('mushroom', 'right', 0), sp('bee', 'right', 700), sp('bee', 'left', 1000), sp('flower', 'left', 1800), sp('bee', 'right', 2800), sp('bee', 'left', 3200)] },
      { triggerX: 4200, spawns: [sp('mushroom', 'right', 0), sp('mushroom', 'left', 400), sp('flower', 'right', 1200), sp('bee', 'right', 2000), sp('bee', 'left', 2400), sp('bee', 'right', 2800)] },
    ],
  },
  '2-2': {
    id: '2-2', chapter: '第 2 章', name: '毒蘑菇窪地', desc: '潮濕的窪地長滿毒蘑菇，霧氣越來越濃。', width: 5200,
    bg: 'bg_chapter2', bgGroundY: 420, tint: 0xbfe6d8,
    waves: [
      { triggerX: 500, spawns: [sp('mushroom', 'right', 0), sp('bee', 'left', 500), sp('mushroom', 'left', 1400), sp('bee', 'right', 2000)] },
      { triggerX: 1400, spawns: [sp('flower', 'right', 0), sp('mushroom', 'right', 600), sp('bee', 'left', 1000), sp('bee', 'left', 1300), sp('mushroom', 'left', 2400)] },
      { triggerX: 2400, spawns: [sp('mushroom', 'right', 0), sp('mushroom', 'right', 300), sp('flower', 'left', 800), sp('bee', 'right', 1600), sp('bee', 'left', 2000), sp('ironBug', 'right', 2800)] },
      { triggerX: 3400, spawns: [sp('flower', 'right', 0), sp('flower', 'right', 400), sp('mushroom', 'left', 1000), sp('bee', 'right', 1800), sp('bee', 'right', 2100), sp('bee', 'left', 2400), sp('mushroom', 'right', 3200)] },
      { triggerX: 4500, spawns: [sp('mushroom', 'right', 0), sp('mushroom', 'left', 0), sp('mushroom', 'right', 1200), sp('flower', 'left', 1600), sp('bee', 'right', 2400), sp('bee', 'left', 2800), sp('bee', 'right', 3200)] },
    ],
  },
  '2-3': {
    id: '2-3', chapter: '第 2 章', name: '古樹之心', desc: '森林深處的巨木正在發出紫光，樹精母體在此等待。', width: 5600,
    bg: 'bg_chapter2', bgGroundY: 420, tint: 0xa8bcc8,
    waves: [
      { triggerX: 500, spawns: [sp('bee', 'right', 0), sp('bee', 'right', 300), sp('flower', 'left', 800), sp('mushroom', 'right', 1600)] },
      { triggerX: 1500, spawns: [sp('mushroom', 'right', 0), sp('flower', 'right', 400), sp('bee', 'left', 900), sp('bee', 'left', 1200), sp('nightWolf', 'right', 2200)] },
      { triggerX: 2500, spawns: [sp('flower', 'right', 0), sp('flower', 'left', 200), sp('mushroom', 'right', 1000), sp('mushroom', 'left', 1300), sp('bee', 'right', 2200), sp('bee', 'right', 2500)] },
      { triggerX: 3600, spawns: [sp('nightWolf', 'right', 0), sp('nightWolf', 'left', 300), sp('mushroom', 'right', 1200), sp('bee', 'left', 1800), sp('bee', 'right', 2200), sp('flower', 'left', 2800)] },
      { triggerX: 4800, spawns: [sp('treant', 'right', 800), sp('bee', 'left', 8000), sp('bee', 'left', 8300), sp('flower', 'left', 16000)] },
    ],
  },
};

/** Play order; a stage unlocks once the one before it is cleared. */
export const STAGE_ORDER = ['1-1', '1-2', '1-3', '2-1', '2-2', '2-3'];
export const nextStage = (id: string) => STAGE_ORDER[STAGE_ORDER.indexOf(id) + 1];
export const stageUnlocked = (id: string, cleared: string[]) => {
  const i = STAGE_ORDER.indexOf(id);
  return i === 0 || cleared.includes(STAGE_ORDER[i - 1]);
};

import type { StageDef } from './types';

const sp = (enemy: string, side: 'left' | 'right', delay: number) => ({ enemy, side, delay });

export const STAGES: Record<string, StageDef> = {
  '1-1': {
    id: '1-1', chapter: '第 1 章', name: '王都郊外草原', desc: '果凍怪與野狼出沒的草原，適合熟悉操作。', width: 4200,
    bg: 'bg_chapter1', bgGroundY: 528,
    waves: [
      { triggerX: 500, spawns: [sp('jelly', 'right', 0), sp('jelly', 'right', 400), sp('jelly', 'left', 1200)] },
      { triggerX: 1500, spawns: [sp('jelly', 'right', 0), sp('bug', 'right', 300), sp('jelly', 'left', 900), sp('bug', 'left', 2500)] },
      { triggerX: 2500, spawns: [sp('wolf', 'right', 0), sp('jelly', 'left', 400), sp('wolf', 'left', 1800), sp('jelly', 'right', 2400), sp('bug', 'right', 3200)] },
      { triggerX: 3500, spawns: [sp('jellyKing', 'right', 600)] },
    ],
  },
  '1-2': {
    id: '1-2', chapter: '第 1 章', name: '黃昏林道', desc: '夕陽下的林道，火焰果凍與鐵甲蟲成群出現。', width: 4800,
    bg: 'bg_chapter1', bgGroundY: 528, tint: 0xffb488,
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
    bg: 'bg_chapter1', bgGroundY: 528, tint: 0x7a80cc,
    waves: [
      { triggerX: 500, spawns: [sp('wolf', 'right', 0), sp('jelly', 'left', 400), sp('wolf', 'left', 1400), sp('nightWolf', 'right', 2400)] },
      { triggerX: 1400, spawns: [sp('nightWolf', 'right', 0), sp('ironBug', 'left', 400), sp('fireJelly', 'right', 1200), sp('nightWolf', 'left', 2000), sp('bug', 'right', 2800)] },
      { triggerX: 2400, spawns: [sp('nightWolf', 'right', 0), sp('nightWolf', 'left', 300), sp('ironBug', 'right', 1600), sp('ironBug', 'left', 1800), sp('fireJelly', 'right', 2600), sp('jelly', 'left', 3000)] },
      { triggerX: 3400, spawns: [sp('wolf', 'right', 0), sp('nightWolf', 'right', 400), sp('nightWolf', 'left', 800), sp('fireJelly', 'left', 1600), sp('fireJelly', 'right', 2000), sp('ironBug', 'left', 3000)] },
      { triggerX: 4500, spawns: [sp('wolfLord', 'right', 600), sp('nightWolf', 'left', 6000), sp('nightWolf', 'left', 12000)] },
    ],
  },
};

/** Play order; a stage unlocks once the one before it is cleared. */
export const STAGE_ORDER = ['1-1', '1-2', '1-3'];
export const nextStage = (id: string) => STAGE_ORDER[STAGE_ORDER.indexOf(id) + 1];
export const stageUnlocked = (id: string, cleared: string[]) => {
  const i = STAGE_ORDER.indexOf(id);
  return i === 0 || cleared.includes(STAGE_ORDER[i - 1]);
};

import type { StageDef } from './types';

export const STAGES: Record<string, StageDef> = {
  '1-1': {
    id: '1-1', chapter: '第 1 章', name: '王都郊外草原', width: 4200,
    sky: [0x7ec8f2, 0xd9f1ff], ground: 0x6bb35a, hills: 0x4f9a52,
    waves: [
      { triggerX: 500, spawns: [
        { enemy: 'jelly', side: 'right', delay: 0 }, { enemy: 'jelly', side: 'right', delay: 400 }, { enemy: 'jelly', side: 'left', delay: 1200 } ] },
      { triggerX: 1500, spawns: [
        { enemy: 'jelly', side: 'right', delay: 0 }, { enemy: 'bug', side: 'right', delay: 300 }, { enemy: 'jelly', side: 'left', delay: 900 },
        { enemy: 'bug', side: 'left', delay: 2500 } ] },
      { triggerX: 2500, spawns: [
        { enemy: 'wolf', side: 'right', delay: 0 }, { enemy: 'jelly', side: 'left', delay: 400 }, { enemy: 'wolf', side: 'left', delay: 1800 },
        { enemy: 'jelly', side: 'right', delay: 2400 }, { enemy: 'bug', side: 'right', delay: 3200 } ] },
      { triggerX: 3500, spawns: [ { enemy: 'jellyKing', side: 'right', delay: 600 } ] },
    ],
  },
};

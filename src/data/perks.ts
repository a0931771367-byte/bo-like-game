import type { PerkDef } from './types';

/**
 * 強化池 v1（封測版 15 個）。清完一個戰鬥房後三選一。
 * 稀有度抽取權重見 RUN_RULES.rarityWeights；同一個強化在一局內最多拿 maxStacks 次。
 */
export const PERKS: PerkDef[] = [
  // ---- 全職業：數值 ----
  { id: 'toughBody', name: '強韌體魄', desc: '最大 HP +20%', rarity: 'common', classes: [], maxStacks: 3,
    effects: [{ kind: 'mult', target: 'maxHp', value: 1.2 }] },
  { id: 'sharpen', name: '磨刃', desc: '攻擊力與魔攻 +12%', rarity: 'common', classes: [], maxStacks: 3,
    effects: [{ kind: 'mult', target: 'atk', value: 1.12 }, { kind: 'mult', target: 'matk', value: 1.12 }] },
  { id: 'swiftStep', name: '疾風步', desc: '移動速度 +12%、攻擊速度 +8%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'move', value: 1.12 }, { kind: 'mult', target: 'aspd', value: 1.08 }] },
  { id: 'coolMind', name: '冷靜', desc: '技能冷卻時間 -15%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'cooldown', value: 0.85 }] },
  { id: 'deepBreath', name: '回氣', desc: 'SP 回復速度 +50%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'spRegen', value: 1.5 }] },
  { id: 'medicBag', name: '藥袋', desc: '立刻獲得 2 瓶紅藥水，藥水回復量 +50%', rarity: 'common', classes: [], maxStacks: 1,
    effects: [{ kind: 'potion', red: 2 }, { kind: 'mult', target: 'potionHeal', value: 1.5 }] },
  { id: 'campfire', name: '營火', desc: '每清完一個房間額外回復 15% HP', rarity: 'common', classes: [], maxStacks: 1,
    effects: [{ kind: 'roomClearHeal', fraction: 0.15 }] },
  // ---- 全職業：機制 ----
  { id: 'bloodDrink', name: '飲血', desc: '每擊殺一名敵人回復 3% HP', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'onKillHeal', fraction: 0.03 }] },
  { id: 'deadlyAim', name: '致命一擊', desc: '爆擊率 +10%、爆擊傷害 +25%', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'crit', chanceAdd: 10, damageMultAdd: 0.25 }] },
  { id: 'mastery', name: '技能精通', desc: '所有技能等級 +1', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'skillLvAll', add: 1 }] },
  { id: 'burstKill', name: '爆裂擊殺', desc: '敵人死亡時爆炸，對周圍造成 60% 攻擊力的傷害', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'onKillAoe', power: 0.6, radius: 130 }] },
  { id: 'phoenix', name: '不死鳥', desc: '一局一次：死亡時以 50% HP 復活並短暫無敵', rarity: 'epic', classes: [], maxStacks: 1,
    effects: [{ kind: 'revive', fraction: 0.5, invuln: 1500 }] },
  // ---- 職業專屬 ----
  { id: 'greatSwing', name: '巨劍迴旋', desc: '連段最後一擊變成範圍攻擊', rarity: 'epic', classes: ['swordsman'], maxStacks: 1,
    effects: [{ kind: 'comboFinisherAoe', radius: 170 }] },
  { id: 'splitBolt', name: '魔彈分裂', desc: '魔法飛彈數量 +1', rarity: 'rare', classes: ['mage'], maxStacks: 2,
    effects: [{ kind: 'projectileCount', add: 1 }] },
  { id: 'pierceArrow', name: '穿透箭', desc: '箭矢可多穿透 1 名敵人', rarity: 'rare', classes: ['archer'], maxStacks: 2,
    effects: [{ kind: 'pierce', add: 1 }] },
];

export const getPerk = (id: string) => {
  const p = PERKS.find((k) => k.id === id);
  if (!p) throw new Error(`unknown perk ${id}`);
  return p;
};

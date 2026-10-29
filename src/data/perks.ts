import type { PerkDef } from './types';

/**
 * 強化池 v1（封測版 17 個）。清完一個戰鬥房後三選一。
 * 稀有度抽取權重見 RUN_RULES.rarityWeights；同一個強化在一局內最多拿 maxStacks 次。
 */
export const PERKS: PerkDef[] = [
  // ---- 全職業：數值 ----
  { id: 'toughBody', name: '強韌體魄', desc: '最大 HP +25%', rarity: 'common', classes: [], maxStacks: 3,
    effects: [{ kind: 'mult', target: 'maxHp', value: 1.25 }] },
  { id: 'sharpen', name: '磨刃', desc: '攻擊力與魔攻 +15%', rarity: 'common', classes: [], maxStacks: 3,
    effects: [{ kind: 'mult', target: 'atk', value: 1.15 }, { kind: 'mult', target: 'matk', value: 1.15 }] },
  { id: 'swiftStep', name: '疾風步', desc: '移動速度 +15%、攻擊速度 +10%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'move', value: 1.15 }, { kind: 'mult', target: 'aspd', value: 1.1 }] },
  { id: 'coolMind', name: '冷靜', desc: '技能冷卻時間 -20%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'cooldown', value: 0.8 }] },
  { id: 'deepBreath', name: '回氣', desc: 'SP 回復速度 +60%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'spRegen', value: 1.6 }] },
  { id: 'medicBag', name: '藥袋', desc: '立刻獲得 2 瓶紅藥水，藥水回復量 +50%', rarity: 'common', classes: [], maxStacks: 1,
    effects: [{ kind: 'potion', red: 2 }, { kind: 'mult', target: 'potionHeal', value: 1.5 }] },
  { id: 'campfire', name: '營火', desc: '每清完一個房間額外回復 20% HP', rarity: 'common', classes: [], maxStacks: 1,
    effects: [{ kind: 'roomClearHeal', fraction: 0.2 }] },
  { id: 'frugal', name: '省力', desc: '技能 SP 消耗 -20%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'spCost', value: 0.8 }] },
  { id: 'heavyHand', name: '重手', desc: '擊退距離 +40%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'mult', target: 'knock', value: 1.4 }] },
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

/**
 * 強化池 v2（正式版 18–30）。封測不入池，等 Unity 支援新的 effect kind 後併入 PERKS。
 * 設計目標：普通強化要「立刻有感」（防禦、閃避、開場爆發），稀有以上做機制而不是堆數字。
 */
export const PERKS_NEXT: PerkDef[] = [
  // ---- 普通：立刻有感 ----
  { id: 'ironGuard', name: '鐵壁', desc: '防禦時受到的傷害減為 10%', rarity: 'common', classes: [], maxStacks: 1,
    effects: [{ kind: 'guardMult', value: 0.1 }] },
  { id: 'afterimage', name: '殘影', desc: '閃避無敵時間 +0.12 秒、距離 +20%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'dodge', invulnAdd: 120, distanceMult: 1.2 }] },
  { id: 'openingRush', name: '開場衝鋒', desc: '每間房開始 5 秒內攻擊力 +30%', rarity: 'common', classes: [], maxStacks: 2,
    effects: [{ kind: 'roomStartBuff', duration: 5000, mult: 1.3 }] },
  // ---- 稀有：機制 ----
  { id: 'comboMaster', name: '連擊大師', desc: '普攻連段多一擊', rarity: 'rare', classes: [], maxStacks: 1,
    effects: [{ kind: 'comboExtraHit', add: 1 }] },
  { id: 'frostTouch', name: '冰封之觸', desc: '普攻 10% 機率凍結敵人 1 秒', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'onHitStatus', status: 'freeze', chance: 0.1, duration: 1000 }] },
  { id: 'burningEdge', name: '燃燒之刃', desc: '普攻使敵人燃燒 3 秒，每秒 4 點傷害', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'onHitStatus', status: 'burn', chance: 1, duration: 3000, value: 4 }] },
  { id: 'soulDrain', name: '吸魂', desc: '每擊殺回復 5% SP', rarity: 'rare', classes: [], maxStacks: 2,
    effects: [{ kind: 'onKillSp', fraction: 0.05 }] },
  { id: 'battleFervor', name: '戰意', desc: '每擊殺攻擊力 +2%（最多 +30%），換房重算', rarity: 'rare', classes: [], maxStacks: 1,
    effects: [{ kind: 'killStackAtk', perKill: 0.02, max: 0.3 }] },
  // ---- 史詩 ----
  { id: 'timeRift', name: '時間裂隙', desc: '施放技能有 15% 機率立刻重置冷卻', rarity: 'epic', classes: [], maxStacks: 1,
    effects: [{ kind: 'cooldownReset', chance: 0.15 }] },
  { id: 'doubleHarvest', name: '雙倍收穫', desc: '本局星塵 +50%', rarity: 'epic', classes: [], maxStacks: 1,
    effects: [{ kind: 'mult', target: 'stardust', value: 1.5 }] },
  // ---- 職業專屬 ----
  { id: 'riposte', name: '反擊', desc: '防禦成功後 0.5 秒內的普攻傷害 ×2', rarity: 'rare', classes: ['swordsman'], maxStacks: 1,
    effects: [{ kind: 'counter', window: 500, mult: 2 }] },
  { id: 'overload', name: '魔力過載', desc: '技能 SP 消耗 ×2，技能傷害 ×1.6', rarity: 'epic', classes: ['mage'], maxStacks: 1,
    effects: [{ kind: 'mult', target: 'spCost', value: 2 }, { kind: 'mult', target: 'skillPower', value: 1.6 }] },
  { id: 'hawkEye', name: '鷹眼', desc: '爆擊率 +15%', rarity: 'rare', classes: ['archer'], maxStacks: 2,
    effects: [{ kind: 'crit', chanceAdd: 15 }] },
];

export const getPerk = (id: string) => {
  const p = [...PERKS, ...PERKS_NEXT].find((k) => k.id === id);
  if (!p) throw new Error(`unknown perk ${id}`);
  return p;
};

import type { TalentDef } from './types';

/**
 * 職業天賦線 v1（封測 2：劍士、法師、遊俠各 10 格，11/1 凍結）。
 * 一直線依序購買，星塵價 30→150 遞增，一條線 750 星塵 ≈ 12–15 局。第 4、7、10 格是「改玩法」節點（keystone）。
 * 可免費重置（RUN_RULES.talents.resetFree）。效果重用 PerkEffect；數值類在局內與強化相乘。
 */
const COST = [30, 40, 50, 70, 80, 90, 100, 110, 120, 150];
const t = (classId: string, tier: number, id: string, name: string, desc: string, effects: TalentDef['effects'], keystone = false): TalentDef =>
  ({ id: `${classId}.${id}`, classId, tier, name, desc, cost: COST[tier - 1], keystone, effects });

export const TALENTS: TalentDef[] = [
  // ---- 劍士：正面硬碰、防禦反擊 ----
  t('swordsman', 1, 'vigor', '壯碩', '最大 HP +10%', [{ kind: 'mult', target: 'maxHp', value: 1.1 }]),
  t('swordsman', 2, 'ironGuard', '穩固防禦', '防禦時受傷 25% → 15%', [{ kind: 'guardMult', value: 0.15 }]),
  t('swordsman', 3, 'quickShield', '快盾', '盾衝冷卻 -200 ms', [{ kind: 'dodge', cooldownAdd: -200 }]),
  t('swordsman', 4, 'reflect', '反彈', '★ 盾衝期間把投射物反彈回敵人', [{ kind: 'reflectProjectile' }], true),
  t('swordsman', 5, 'heavyFinisher', '重擊收尾', '連段第 3 擊傷害 +30%', [{ kind: 'comboHitMult', hit: 3, mult: 1.3 }]),
  t('swordsman', 6, 'bloodlust', '戰鬥呼吸', '每擊殺回復 2% HP', [{ kind: 'onKillHeal', fraction: 0.02 }]),
  t('swordsman', 7, 'armorBreak', '破甲連段', '★ 連段完整打完使目標防禦 -50%，持續 3 秒', [{ kind: 'comboDebuff', defMult: 0.5, duration: 3000 }], true),
  t('swordsman', 8, 'headStart', '充分準備', '每局開始多 1 技能點', [{ kind: 'startSkillPoints', add: 1 }]),
  t('swordsman', 9, 'warcry', '開戰怒吼', '每間房前 5 秒攻擊力 +20%', [{ kind: 'roomStartBuff', duration: 5000, mult: 1.2 }]),
  t('swordsman', 10, 'unyielding', '不屈', '★ HP 低於 30% 時，防禦變成 1 秒無敵，冷卻 20 秒', [{ kind: 'guardInvuln', hpFraction: 0.3, invuln: 1000, cooldown: 20000 }], true),

  // ---- 法師：資源管理、元素串招 ----
  t('mage', 1, 'manaPool', '魔力池', '最大 SP +10%', [{ kind: 'mult', target: 'spRegen', value: 1.1 }]),
  t('mage', 2, 'quickCast', '速詠', '技能冷卻 -10%', [{ kind: 'mult', target: 'cooldown', value: 0.9 }]),
  t('mage', 3, 'farBlink', '遠距瞬移', '瞬移距離 +60 px', [{ kind: 'dodge', distanceMult: 1.21 }]),
  t('mage', 4, 'frostBlink', '冰霜瞬移', '★ 瞬移起點留下冰爆，凍結周圍敵人 0.8 秒', [{ kind: 'dodgeOverride', landing: { what: 'iceBurst', duration: 800, power: 0.5, radius: 120 } }], true),
  t('mage', 5, 'arcanePower', '魔力增幅', '法術傷害 +12%', [{ kind: 'mult', target: 'skillPower', value: 1.12 }]),
  t('mage', 6, 'soulSiphon', '魂汲', '每擊殺回復 4% SP', [{ kind: 'onKillSp', fraction: 0.04 }]),
  t('mage', 7, 'resonance', '元素共鳴', '★ 2 秒內接著施放不同屬性的技能，傷害 +50%', [{ kind: 'elementResonance', mult: 1.5, window: 2000 }], true),
  t('mage', 8, 'headStart', '充分準備', '每局開始多 1 技能點', [{ kind: 'startSkillPoints', add: 1 }]),
  t('mage', 9, 'fullCharge', '滿盈', '每間房開始時 SP 全滿', [{ kind: 'roomStart', spFraction: 1 }]),
  t('mage', 10, 'overload', '過載', '★ SP 歸零時，下一招免費且傷害 ×2', [{ kind: 'spEmptyFree', mult: 2 }], true),

  // ---- 遊俠：機動、滾翻接射 ----
  t('archer', 1, 'lightFoot', '輕足', '移動速度 +8%', [{ kind: 'mult', target: 'move', value: 1.08 }]),
  t('archer', 2, 'keenEye', '銳眼', '爆擊率 +8%', [{ kind: 'crit', chanceAdd: 8 }]),
  t('archer', 3, 'rollShot', '滾翻速射', '滾翻後 0.5 秒攻擊速度 +50%', [{ kind: 'afterDodgeBuff', duration: 500, aspdMult: 1.5 }]),
  t('archer', 4, 'twinShot', '雙射', '★ 滾翻起身的那一下普攻射出 2 支箭', [{ kind: 'afterDodgeBuff', duration: 150, extraProjectiles: 1 }], true),
  t('archer', 5, 'pierceArrows', '穿透箭矢', '箭多穿透 1 名敵人', [{ kind: 'pierce', add: 1 }]),
  t('archer', 6, 'huntersMark', '獵人節奏', '每擊殺回復 3% SP', [{ kind: 'onKillSp', fraction: 0.03 }]),
  t('archer', 7, 'trapper', '陷阱射手', '★ 滾翻落地放置地刺，踩到的敵人定身 1 秒', [{ kind: 'dodgeOverride', landing: { what: 'spikeTrap', duration: 6000, power: 0.4, radius: 60 } }], true),
  t('archer', 8, 'headStart', '充分準備', '每局開始多 1 技能點', [{ kind: 'startSkillPoints', add: 1 }]),
  t('archer', 9, 'ambush', '潛行入場', '每間房開始 3 秒內敵人不會注意你', [{ kind: 'roomStart', hiddenMs: 3000 }]),
  t('archer', 10, 'decoy', '影分身', '★ 滾翻留下假身吸引敵人 2 秒', [{ kind: 'dodgeOverride', landing: { what: 'decoy', duration: 2000 } }], true),
];

export const talentsOf = (classId: string) => TALENTS.filter((x) => x.classId === classId).sort((a, b) => a.tier - b.tier);

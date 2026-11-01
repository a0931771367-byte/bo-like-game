import type { CardDef, CardSetDef } from './types';

/**
 * 史詩卡片 v1（封測 2，12 張，11/1 凍結）。
 * 規則（RUN_RULES.cards）：每隻 Boss 一個卡池 4 張；首殺必掉招牌卡（signature），之後每次擊殺掉該池 1 張隨機卡；
 * 重複卡轉碎片，3 碎片升 1 星（最高 3 星，升星放大數值不改效果）。全帳號共用、永久保留、起始 2 格裝備。
 * 效果直接重用 PerkEffect；★ 標記為「改玩法」。音效屬性見 design/07 §3.4。
 */
export const CARDS: CardDef[] = [
  // ---- 布丁巨像（jellyKing）----
  { id: 'jellyCrown', name: '果凍王冠', boss: 'jellyKing', rarity: 'epic', signature: true, starScale: 0.25,
    desc: '★ 連段第 3 擊命中時落下一團果凍，黏住敵人 1.2 秒',
    effects: [{ kind: 'onComboHitSpawn', hit: 3, what: 'stickyBlob', duration: 1200, radius: 70 }] },
  { id: 'bouncyShell', name: '彈力外殼', boss: 'jellyKing', rarity: 'epic', starScale: 0.2,
    desc: '受傷時 30% 機率把攻擊者彈開 150 px',
    effects: [{ kind: 'onHurtKnock', chance: 0.3, knock: 150 }] },
  { id: 'splitCore', name: '分裂之核', boss: 'jellyKing', rarity: 'epic', starScale: 0.2,
    desc: '★ 擊殺敵人時 25% 機率分出一隻友方小果凍，持續 6 秒',
    effects: [{ kind: 'summonAlly', trigger: 'kill', enemy: 'jelly', count: 1, chance: 0.25, hpMult: 0.6, lifetime: 6000, respawn: 0 }] },
  { id: 'sugarRush', name: '糖分補給', boss: 'jellyKing', rarity: 'epic', starScale: 0.2,
    desc: '藥水回復量 +40%，每局開始多 1 瓶紅藥水',
    effects: [{ kind: 'mult', target: 'potionHeal', value: 1.4 }, { kind: 'startPotion', red: 1 }] },

  // ---- 樹精母體（treant）----
  { id: 'thornHeart', name: '荊棘之心', boss: 'treant', rarity: 'epic', signature: true, starScale: 0.25,
    desc: '★ 閃避落地處長出荊棘牆 2 秒，碰到的敵人受傷並減速',
    effects: [{ kind: 'dodgeOverride', landing: { what: 'thornWall', duration: 2000, power: 0.6, slow: 0.5, radius: 90 } }] },
  { id: 'sporeBreath', name: '孢子呼吸', boss: 'treant', rarity: 'epic', starScale: 0.25,
    desc: '站著不動 1 秒後，每秒回復 2% HP',
    effects: [{ kind: 'idleRegen', after: 1000, fraction: 0.02 }] },
  { id: 'beeGuard', name: '蜜蜂衛隊', boss: 'treant', rarity: 'epic', starScale: 0.2,
    desc: '★ 每間房開始時召出 2 隻友方蜜蜂，死亡後 20 秒重生',
    effects: [{ kind: 'summonAlly', trigger: 'room', enemy: 'bee', count: 2, hpMult: 0.5, lifetime: 0, respawn: 20000 }] },
  { id: 'ancientRing', name: '古樹年輪', boss: 'treant', rarity: 'epic', starScale: 0,
    desc: '每間房第一次受到的攻擊不扣血',
    effects: [{ kind: 'firstHitImmune' }] },

  // ---- 遺跡守衛石像（ruinGuardian）----
  { id: 'guardianEye', name: '守衛之眼', boss: 'ruinGuardian', rarity: 'epic', signature: true, starScale: 0.25,
    desc: '★ 連段完整打完時射出一道雷射，貫穿直線上所有敵人',
    effects: [{ kind: 'onComboHitSpawn', hit: 0, what: 'beam', power: 1.2 }] },
  { id: 'stoneArmor', name: '石化護甲', boss: 'ruinGuardian', rarity: 'epic', starScale: 0.2,
    desc: '★ HP 低於 30% 時石化 2 秒：無敵但不能動，冷卻 25 秒',
    effects: [{ kind: 'lowHpShield', hpFraction: 0.3, duration: 2000, cooldown: 25000 }] },
  { id: 'quicksandStep', name: '流沙步伐', boss: 'ruinGuardian', rarity: 'epic', starScale: 0.2,
    desc: '閃避落地處留下流沙 2 秒，敵人移動速度 -50%',
    effects: [{ kind: 'dodgeOverride', landing: { what: 'quicksand', duration: 2000, slow: 0.5, radius: 110 } }] },
  { id: 'ruinTreasure', name: '遺跡秘寶', boss: 'ruinGuardian', rarity: 'epic', starScale: 0.3,
    desc: '每擊殺一名敵人 +1 星塵，每局最多 30',
    effects: [{ kind: 'killStardust', add: 1, max: 30 }] },
];

/** 同 Boss 2 張 → 套裝效果（不另配音，沿用招牌卡音變奏）。 */
export const CARD_SETS: CardSetDef[] = [
  { boss: 'jellyKing', name: '布丁巨像套裝', desc: '被黏住的敵人受到的傷害 +30%', need: 2,
    effects: [{ kind: 'comboDebuff', defMult: 0.7, duration: 1200 }] },
  { boss: 'treant', name: '樹精母體套裝', desc: '友方單位（果凍、蜜蜂）傷害 +50%', need: 2,
    effects: [{ kind: 'mult', target: 'skillPower', value: 1.0 }] },
  { boss: 'ruinGuardian', name: '遺跡守衛套裝', desc: '石化期間周圍敵人每秒受到 50% ATK 的傷害', need: 2,
    effects: [{ kind: 'lowHpShield', hpFraction: 0.3, duration: 2000, cooldown: 25000, auraPower: 0.5 }] },
];

export const getCard = (id: string) => {
  const c = CARDS.find((k) => k.id === id);
  if (!c) throw new Error(`unknown card ${id}`);
  return c;
};

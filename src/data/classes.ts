import type { ClassDef, ComboHit } from './types';

const hit = (power: number, range: number, startup: number, active: number, recovery: number, knock: number, launch = 0, stun = 320): ComboHit =>
  ({ power, range, startup, active, recovery, knock, launch, stun });

export const CLASSES: ClassDef[] = [
  {
    id: 'swordsman', name: '劍士', role: '近戰・耐打・新手友善', color: 0x4a7bd1, accent: 0xd8dde8, weapon: 'sword', playable: true,
    hpBase: 150, hpPerLv: 18, spBase: 30, spPerLv: 3, weaponAtk: 18,
    baseStats: { str: 6, agi: 3, vit: 6, int: 1, dex: 3, luk: 1 },
    combo: [
      hit(1.0, 105, 90, 90, 160, 60),
      hit(1.0, 105, 80, 90, 170, 60),
      hit(1.7, 125, 140, 110, 300, 380, 480, 500),
    ],
    air: hit(1.2, 100, 60, 180, 120, 160, 0, 360),
    skills: [
      { id: 'bash', name: '裂甲斬', short: '裂甲', ref: 'Bash', desc: '強力單體斬擊，高等級機率暈眩', type: 'melee', maxLv: 5,
        sp: [8, 1], cooldown: 1400, cast: 180, power: [2.2, 0.45], element: 'neutral', magic: false, color: 0xffd34d,
        range: 130, knock: 260, stun: 600, stunChance: [0.0, 0.08] },
      { id: 'magnum', name: '烈焰踏', short: '烈焰', ref: 'Magnum Break', desc: '周圍火屬性爆炸，擊飛敵人', type: 'aoe', maxLv: 5,
        sp: [15, 1], cooldown: 6000, cast: 260, power: [1.6, 0.25], element: 'fire', magic: false, color: 0xff6a2b,
        radius: 170, knock: 480, launch: 520, stun: 500 },
      { id: 'provoke', name: '戰吼', short: '戰吼', ref: 'Provoke', desc: '降低周圍敵人防禦 8 秒', type: 'debuff', maxLv: 5,
        sp: [6, 0], cooldown: 9000, cast: 200, power: [0, 0], element: 'neutral', magic: false, color: 0xff4d6d,
        radius: 340, duration: [8000, 0], defDown: [0.3, 0.07] },
      { id: 'endure', name: '不屈', short: '不屈', ref: 'Endure', desc: '一段時間內受擊不硬直', type: 'buff', maxLv: 5,
        sp: [10, 0], cooldown: 16000, cast: 150, power: [0, 0], element: 'neutral', magic: false, color: 0x9be15d,
        duration: [5000, 1000] },
    ],
  },
  {
    id: 'mage', name: '法師', role: '遠程屬性魔法・脆皮高傷', color: 0x8a4fd1, accent: 0xf3d36b, weapon: 'staff', playable: true,
    hpBase: 110, hpPerLv: 11, spBase: 60, spPerLv: 7, weaponAtk: 9,
    baseStats: { str: 1, agi: 3, vit: 3, int: 8, dex: 6, luk: 1 },
    combo: [
      hit(0.8, 90, 100, 90, 180, 50),
      hit(0.8, 90, 100, 90, 200, 50),
      hit(1.2, 110, 150, 100, 320, 320, 380, 450),
    ],
    air: hit(0.9, 90, 70, 160, 140, 140, 0, 320),
    skills: [
      { id: 'firebolt', name: '火球術', short: '火球', ref: 'Fire Bolt', desc: '連發火屬性魔彈，等級越高發數越多', type: 'projectile', maxLv: 5,
        sp: [8, 2], cooldown: 1300, cast: 320, power: [1.05, 0.05], element: 'fire', magic: true, color: 0xff7b2e,
        count: [2, 1], speed: 760, knock: 120, stun: 300 },
      { id: 'coldbolt', name: '冰矛術', short: '冰矛', ref: 'Cold Bolt / Frost Diver', desc: '水屬性冰矛，機率凍結敵人', type: 'projectile', maxLv: 5,
        sp: [12, 2], cooldown: 2200, cast: 360, power: [1.3, 0.15], element: 'water', magic: true, color: 0x6fd3ff,
        count: [1, 0.5], speed: 640, knock: 80, stun: 350, freezeChance: [0.3, 0.1] },
      { id: 'lightning', name: '雷擊術', short: '雷擊', ref: 'Lightning Bolt / Thunderstorm', desc: '在前方降下多道落雷', type: 'strikes', maxLv: 5,
        sp: [24, 3], cooldown: 7000, cast: 520, power: [1.0, 0.12], element: 'wind', magic: true, color: 0xfff27a,
        count: [4, 1], range: 460, radius: 90, knock: 160, stun: 450 },
      { id: 'firewall', name: '火牆', short: '火牆', ref: 'Fire Wall', desc: '在前方立起火牆，阻擋並灼燒敵人', type: 'wall', maxLv: 5,
        sp: [20, 2], cooldown: 9000, cast: 380, power: [0.55, 0.08], element: 'fire', magic: true, color: 0xff4f1f,
        duration: [4000, 600], range: 150, knock: 340, stun: 300 },
    ],
  },
  // 以下四職業將在第 3 步實作，資料先列出供標題畫面顯示
  { id: 'merchant', name: '商人', role: '重擊・花金幣換傷害', color: 0xc98a2b, accent: 0xf2e1b5, weapon: 'axe', playable: false,
    hpBase: 140, hpPerLv: 16, spBase: 35, spPerLv: 3, weaponAtk: 20, baseStats: { str: 6, agi: 2, vit: 5, int: 1, dex: 3, luk: 4 },
    combo: [], air: hit(1, 100, 60, 180, 120, 160), skills: [] },
  { id: 'acolyte', name: '補師', role: '自補・增益・剋不死系', color: 0xe8e0c8, accent: 0x6fb7ff, weapon: 'mace', playable: false,
    hpBase: 125, hpPerLv: 13, spBase: 55, spPerLv: 6, weaponAtk: 12, baseStats: { str: 3, agi: 3, vit: 4, int: 6, dex: 4, luk: 1 },
    combo: [], air: hit(1, 100, 60, 180, 120, 160), skills: [] },
  { id: 'thief', name: '盜賊', role: '高速連擊・毒・閃避', color: 0x3f8f5a, accent: 0xb9f5c8, weapon: 'dagger', playable: false,
    hpBase: 120, hpPerLv: 13, spBase: 30, spPerLv: 3, weaponAtk: 14, baseStats: { str: 4, agi: 7, vit: 3, int: 1, dex: 4, luk: 2 },
    combo: [], air: hit(1, 100, 60, 180, 120, 160), skills: [] },
  { id: 'archer', name: '弓箭手', role: '遠程物理・範圍箭雨', color: 0x6a9a3a, accent: 0xe6d38a, weapon: 'bow', playable: false,
    hpBase: 115, hpPerLv: 12, spBase: 35, spPerLv: 4, weaponAtk: 16, baseStats: { str: 2, agi: 5, vit: 3, int: 2, dex: 8, luk: 1 },
    combo: [], air: hit(1, 100, 60, 180, 120, 160), skills: [] },
];

export const getClass = (id: string) => {
  const c = CLASSES.find((k) => k.id === id);
  if (!c) throw new Error(`unknown class ${id}`);
  return c;
};

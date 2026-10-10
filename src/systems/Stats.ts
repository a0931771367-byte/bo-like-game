import type { ClassDef, Element } from '../data/types';
import type { ClassProgress } from './Save';

export interface Derived {
  maxHp: number; maxSp: number; atk: number; matk: number; def: number; mdef: number;
  crit: number; flee: number; aspd: number; move: number; castMult: number; spRegen: number; hpRegen: number;
}

export const MAX_LEVEL = 50;
export const expToNext = (level: number) => Math.floor(28 * Math.pow(level, 1.65));

export function derive(cls: ClassDef, p: ClassProgress): Derived {
  const { str, agi, vit, int, dex, luk } = p.stats;
  const L = p.level;
  return {
    maxHp: Math.round(cls.hpBase + cls.hpPerLv * (L - 1) + vit * 12),
    maxSp: Math.round(cls.spBase + cls.spPerLv * (L - 1) + int * 4),
    atk: cls.weaponAtk + str * 2 + Math.floor(dex / 3) + Math.floor(luk / 5) + L,
    matk: int * 3 + Math.floor((int * int) / 20) + L,
    def: vit + Math.floor(L / 2),
    mdef: Math.floor(int / 2) + Math.floor(vit / 4),
    crit: 1 + luk * 0.35,
    flee: Math.min(35, agi * 0.5 + luk * 0.15),
    aspd: 1 + agi * 0.012 + dex * 0.003,
    move: 230 + agi * 1.2,
    castMult: 1 - Math.min(0.5, dex * 0.012),
    spRegen: 1 + int * 0.18,
    hpRegen: vit * 0.12,
  };
}

/** Adds exp, returns the number of levels gained. */
export function addExp(p: ClassProgress, amount: number) {
  let gained = 0;
  p.exp += amount;
  while (p.level < MAX_LEVEL && p.exp >= expToNext(p.level)) {
    p.exp -= expToNext(p.level);
    p.level++;
    p.statPoints += 3;
    p.skillPoints += 1;
    gained++;
  }
  if (p.level >= MAX_LEVEL) p.exp = 0;
  return gained;
}

const STRONG: Partial<Record<Element, Element>> = { fire: 'earth', water: 'fire', wind: 'water', earth: 'wind', holy: 'dark', dark: 'holy' };

export function elementMult(atk: Element, def: Element) {
  if (atk === 'neutral' || def === 'neutral') return 1;
  if (STRONG[atk] === def) return atk === 'holy' || atk === 'dark' ? 1.75 : 1.5;
  if (STRONG[def] === atk) return 0.75;
  if (atk === def) return 0.5;
  return 1;
}

export interface Attacker { atk: number; matk: number; crit: number }
export interface Defender { def: number; mdef: number; element: Element; defMult: number }

export function calcDamage(a: Attacker, d: Defender, power: number, element: Element, magic: boolean) {
  const crit = !magic && Math.random() * 100 < a.crit;
  let dmg = (magic ? a.matk : a.atk) * power;
  if (!crit) dmg -= (magic ? d.mdef : d.def) * d.defMult;
  dmg = Math.max(1, dmg) * elementMult(element, d.element) * (0.9 + Math.random() * 0.2);
  if (crit) dmg *= 1.5;
  return { damage: Math.max(1, Math.round(dmg)), crit, mult: elementMult(element, d.element) };
}

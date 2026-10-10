export type Element = 'neutral' | 'fire' | 'water' | 'wind' | 'earth' | 'holy' | 'dark' | 'poison';
export type StatKey = 'str' | 'agi' | 'vit' | 'int' | 'dex' | 'luk';
export type Stats = Record<StatKey, number>;

/** [value at Lv1, increase per extra level] */
export type Scaling = [number, number];

export interface ComboHit {
  power: number;
  range: number;
  startup: number; // ms
  active: number; // ms
  recovery: number; // ms
  knock: number;
  launch: number;
  stun: number; // ms
}

export type SkillType = 'melee' | 'aoe' | 'projectile' | 'buff' | 'debuff' | 'wall' | 'strikes';

export interface SkillDef {
  id: string;
  name: string;
  short: string; // 2-char label for buttons
  ref: string; // original game reference, dev only
  desc: string;
  type: SkillType;
  maxLv: number;
  sp: Scaling;
  cooldown: number; // ms
  cast: number; // ms
  power: Scaling;
  element: 'neutral' | Element;
  magic: boolean;
  color: number;
  range?: number;
  radius?: number;
  knock?: number;
  launch?: number;
  stun?: number;
  stunChance?: Scaling;
  freezeChance?: Scaling;
  count?: Scaling;
  speed?: number;
  duration?: Scaling;
  defDown?: Scaling;
}

export interface ClassDef {
  id: string;
  name: string;
  role: string;
  color: number;
  accent: number;
  weapon: 'sword' | 'staff' | 'axe' | 'mace' | 'dagger' | 'bow';
  playable: boolean;
  hpBase: number;
  hpPerLv: number;
  spBase: number;
  spPerLv: number;
  weaponAtk: number;
  baseStats: Stats;
  combo: ComboHit[];
  air: ComboHit;
  skills: SkillDef[];
}

export type EnemyBehavior = 'hopper' | 'melee' | 'lunger' | 'bossSlime';

export interface EnemyDef {
  id: string;
  name: string;
  hp: number;
  atk: number;
  def: number;
  mdef: number;
  element: Element;
  exp: number;
  gold: number;
  speed: number;
  w: number;
  h: number;
  color: number;
  behavior: EnemyBehavior;
  attackRange: number;
  windup: number;
  attackCd: number;
  boss?: boolean;
  /** On-screen sprite height in px (hitbox uses w/h). */
  spriteH: number;
}

export interface SpawnDef {
  enemy: string;
  side: 'left' | 'right';
  delay: number;
}

export interface WaveDef {
  triggerX: number;
  spawns: SpawnDef[];
}

export interface StageDef {
  id: string;
  name: string;
  chapter: string;
  width: number;
  sky: [number, number];
  ground: number;
  hills: number;
  waves: WaveDef[];
}

export const lv = (s: Scaling, level: number) => s[0] + s[1] * (level - 1);

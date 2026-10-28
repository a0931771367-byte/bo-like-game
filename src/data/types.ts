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

export type SkillType = 'melee' | 'aoe' | 'projectile' | 'buff' | 'debuff' | 'wall' | 'strikes' | 'heal' | 'dash' | 'passive';

/** Temporary stat changes granted by buff skills. */
export interface BuffEffect {
  atkMult?: number;
  matkMult?: number;
  allStats?: number;
  moveMult?: number;
  aspdMult?: number;
  endure?: boolean;
  hidden?: boolean;
  shieldHits?: number;
}

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
  goldCost?: Scaling;
  poison?: Scaling; // damage per second for 5 s
  heal?: Scaling; // fraction of max HP
  buff?: (level: number) => BuffEffect;
  passive?: 'doubleAttack' | 'goldFind';
  passiveValue?: Scaling;
  /** Dash skills: distance travelled over `duration`. */
  dashDistance?: Scaling;
}

export interface ClassDef {
  id: string;
  name: string;
  role: string;
  color: number;
  accent: number;
  weapon: 'sword' | 'staff' | 'axe' | 'mace' | 'dagger' | 'bow';
  playable: boolean;
  /** Normal attacks fire arrows instead of swinging. */
  ranged?: boolean;
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

export type EnemyBehavior = 'hopper' | 'melee' | 'lunger' | 'bossSlime' | 'flyer' | 'turret' | 'bossTreant';

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
  /** Texture id to draw with (defaults to `id`), for recoloured variants. */
  sprite?: string;
  tint?: number;
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
  desc: string;
  width: number;
  /** Background texture key and the y where its walkable floor strip starts. */
  bg: string;
  bgGroundY: number;
  /** Optional colour wash over the background (time of day). */
  tint?: number;
  waves: WaveDef[];
}

export const lv = (s: Scaling, level: number) => s[0] + s[1] * (level - 1);

// ---------------------------------------------------------------------------
// Roguelite run data (口袋冒險團 封測版). Pure data: no functions, so the Unity
// exporter (tools/export_unity_data.mjs) can emit it as-is.
// ---------------------------------------------------------------------------

export type PerkRarity = 'common' | 'rare' | 'epic';

/** Multiplicative targets a perk can scale. 1.2 = +20 %. `cooldown` and `spCost` below 1 are reductions. */
export type MultTarget = 'maxHp' | 'atk' | 'matk' | 'move' | 'aspd' | 'cooldown' | 'spCost' | 'spRegen' | 'knock' | 'potionHeal' | 'skillPower' | 'stardust';

export type PerkEffect =
  | { kind: 'mult'; target: MultTarget; value: number }
  /** Flat add to crit chance (percentage points) and/or crit damage multiplier. */
  | { kind: 'crit'; chanceAdd?: number; damageMultAdd?: number }
  /** Every learned skill gains this many levels (capped at maxLv). */
  | { kind: 'skillLvAll'; add: number }
  /** Heal this fraction of max HP on each kill. */
  | { kind: 'onKillHeal'; fraction: number }
  /** Enemies explode on death: neutral physical hit of `power` × ATK in `radius` px. */
  | { kind: 'onKillAoe'; power: number; radius: number }
  /** Heal this fraction of max HP when a room is cleared (adds to RUN_RULES.roomClearHeal). */
  | { kind: 'roomClearHeal'; fraction: number }
  /** Grant potions immediately. */
  | { kind: 'potion'; red?: number; blue?: number }
  /** Once per run: on death, revive with this fraction of max HP and `invuln` ms of invulnerability. */
  | { kind: 'revive'; fraction: number; invuln: number }
  /** The last combo hit also deals its damage in a circle of `radius` px. */
  | { kind: 'comboFinisherAoe'; radius: number }
  /** Projectile skills and ranged normal attacks fire this many extra projectiles. */
  | { kind: 'projectileCount'; add: number }
  /** Projectiles pass through this many extra targets. */
  | { kind: 'pierce'; add: number }
  // ---- v2 kinds (正式版強化池 16–30，封測不實作) ----
  /** Damage taken while guarding is multiplied by this instead of the default 0.25. */
  | { kind: 'guardMult'; value: number }
  /** Dodge gains extra invulnerability (ms) and distance (multiplier). */
  | { kind: 'dodge'; invulnAdd: number; distanceMult: number }
  /** For the first `duration` ms of every room, ATK/MATK are multiplied by `mult`. */
  | { kind: 'roomStartBuff'; duration: number; mult: number }
  /** The normal combo repeats its second hit once more before the finisher. */
  | { kind: 'comboExtraHit'; add: number }
  /** Normal attacks have `chance` (0–1) to apply a status for `duration` ms; `value` is dps for burn/poison. */
  | { kind: 'onHitStatus'; status: 'freeze' | 'burn' | 'poison'; chance: number; duration: number; value?: number }
  /** Restore this fraction of max SP on each kill. */
  | { kind: 'onKillSp'; fraction: number }
  /** Each kill adds `perKill` to an ATK multiplier (max `max`), reset when the room ends. */
  | { kind: 'killStackAtk'; perKill: number; max: number }
  /** Using a skill has `chance` to refund its cooldown. */
  | { kind: 'cooldownReset'; chance: number }
  /** A normal attack within `window` ms after a successful guard deals `mult` damage. */
  | { kind: 'counter'; window: number; mult: number };

export interface PerkDef {
  id: string;
  name: string;
  desc: string;
  rarity: PerkRarity;
  /** Empty = offered to every class. */
  classes: string[];
  /** How many times the same perk may be taken in one run. */
  maxStacks: number;
  effects: PerkEffect[];
}

export type RoomKind = 'combat' | 'boss';

/** A wave starts when the previous wave is cleared (or after `timeout` ms, whichever is first). */
export interface RoomWaveDef {
  spawns: SpawnDef[];
  timeout: number;
}

export interface RoomDef {
  id: string;
  name: string;
  scene: string;
  kind: RoomKind;
  width: number;
  bg: string;
  bgGroundY: number;
  tint?: number;
  /** The seed picks one wave set per visit; each set is one full room. */
  waveSets: RoomWaveDef[][];
}

export interface UnlockDef {
  id: string;
  name: string;
  desc: string;
  cost: number;
  /** What the unlock does; `value` meaning depends on `kind`. */
  kind: 'class' | 'startPotion' | 'rareChance' | 'startPerk';
  value: string | number;
}

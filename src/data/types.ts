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

export type DodgeKind = 'shieldDash' | 'blink' | 'roll' | 'jetDash' | 'graceStep' | 'shadowStep';

/** Per-class dodge. Distances px, times ms. Every kind grants invulnerability for `invuln` ms from the press. */
export interface DodgeDef {
  kind: DodgeKind;
  name: string;
  desc: string;
  /** Total distance travelled (blink: teleport distance). */
  distance: number;
  /** Time the dodge state lasts; the player cannot act until it ends (blink: the pre-teleport wind-up). */
  duration: number;
  invuln: number;
  cooldown: number;
  /** Passes through enemies instead of being blocked by them. */
  passThrough?: boolean;
  /** Enemies touched during the dodge are pushed back this far (no damage). */
  knock?: number;
  /** Fraction of max HP restored when an attack is avoided by the invulnerability. */
  onAvoidHeal?: number;
  /** Normal attacks and skills may start this many ms before `duration` ends (roll out into an attack). */
  cancelWindow?: number;
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
  dodge: DodgeDef;
  skills: SkillDef[];
}

/**
 * burrower: hides underground (sand mound, untargetable) until the player is within `burrow.triggerRange`, then
 *   emerges after `burrow.emergeDelay` ms and fights as a melee; re-burrows after `burrow.rest` ms without a target.
 * reviver: on death leaves a pile (`revive.pileHp`) for `revive.delay` ms; if the pile is not destroyed the enemy
 *   comes back once at `revive.fraction` of max HP.
 * crab: only moves sideways toward the player; damage from the side it faces is multiplied by `facingDef.front`,
 *   from behind by `facingDef.back`.
 * bossGuardian: ruin guardian statue (chapter 3 boss), see ENEMIES.ruinGuardian for the move list.
 */
export type EnemyBehavior = 'hopper' | 'melee' | 'lunger' | 'bossSlime' | 'flyer' | 'turret' | 'bossTreant'
  | 'burrower' | 'reviver' | 'crab' | 'bossGuardian';

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
  /** burrower only. */
  burrow?: { triggerRange: number; emergeDelay: number; rest: number };
  /** reviver only. `fraction` of max HP on return. */
  revive?: { delay: number; pileHp: number; fraction: number };
  /** crab only: damage multipliers by the side hit. */
  facingDef?: { front: number; back: number };
  /** turret only: projectiles per volley (default 1). */
  volley?: number;
  /** Boss only: scripted phases. `at` is the HP fraction that triggers the phase. */
  phases?: { at: number; action: 'petrify' | 'summon' | 'enrage'; duration?: number; spawn?: string[]; mult?: number }[];
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
  | { kind: 'dodge'; invulnAdd?: number; distanceMult?: number; cooldownAdd?: number }
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
  | { kind: 'counter'; window: number; mult: number }
  // ---- 封測 2：卡片與天賦用的 kind（11/1）。數值類仍用上面的 kind；下面全是「改玩法」 ----
  /** Combo hit number `hit` (1-based; 0 = finisher) spawns `what` at the target: a sticky blob (root `duration` ms)
   *  or a piercing beam of `power` × ATK along the facing line. */
  | { kind: 'onComboHitSpawn'; hit: number; what: 'stickyBlob' | 'beam'; duration?: number; power?: number; radius?: number }
  /** Friendly units. `trigger` room: at room start; kill: `chance` on each kill. `enemy` is the ENEMIES id to copy
   *  (friendly, `hpMult` × HP, attacks enemies); `lifetime` ms (0 = until death); `respawn` ms after death (0 = none). */
  | { kind: 'summonAlly'; trigger: 'room' | 'kill'; enemy: string; count: number; chance?: number; hpMult: number; lifetime: number; respawn: number }
  /** Overrides parts of the class dodge (kind and animation stay). `landing` leaves a zone where the dodge ends. */
  | { kind: 'dodgeOverride'; passThrough?: boolean; knock?: number; landing?: { what: 'thornWall' | 'quicksand' | 'iceBurst' | 'spikeTrap' | 'decoy'; duration: number; power?: number; slow?: number; radius?: number } }
  /** Below `hpFraction`, become invulnerable and immobile for `duration` ms; `cooldown` ms between triggers. */
  | { kind: 'lowHpShield'; hpFraction: number; duration: number; cooldown: number; auraPower?: number }
  /** While dodging (or guarding when `onGuard`), projectiles are reflected back at their shooter. */
  | { kind: 'reflectProjectile'; onGuard?: boolean }
  /** Finishing the full combo applies a debuff to the target: DEF × `defMult` for `duration` ms. */
  | { kind: 'comboDebuff'; defMult: number; duration: number }
  /** Combo hit `hit` (1-based) deals `mult` damage. */
  | { kind: 'comboHitMult'; hit: number; mult: number }
  /** Casting a skill of a different element than the previous one within `window` ms multiplies its damage. */
  | { kind: 'elementResonance'; mult: number; window: number }
  /** For `duration` ms after a dodge ends, ASPD × `aspdMult`; ranged normal attacks fire `extraProjectiles` more. */
  | { kind: 'afterDodgeBuff'; duration: number; aspdMult?: number; extraProjectiles?: number }
  /** Extra skill points at run start. */
  | { kind: 'startSkillPoints'; add: number }
  /** At room start: SP refilled to this fraction, and/or hidden (enemies ignore the player) for `hiddenMs`. */
  | { kind: 'roomStart'; spFraction?: number; hiddenMs?: number }
  /** When SP hits 0, the next skill costs nothing and deals `mult` damage. */
  | { kind: 'spEmptyFree'; mult: number }
  /** Below `hpFraction`, guarding grants `invuln` ms of invulnerability; `cooldown` ms between triggers. */
  | { kind: 'guardInvuln'; hpFraction: number; invuln: number; cooldown: number }
  /** Damage taken has `chance` to push the attacker back `knock` px. */
  | { kind: 'onHurtKnock'; chance: number; knock: number }
  /** Standing still for `after` ms starts regenerating `fraction` of max HP per second. */
  | { kind: 'idleRegen'; after: number; fraction: number }
  /** The first hit taken in every room deals no damage. */
  | { kind: 'firstHitImmune' }
  /** Guarding reflects `fraction` of blocked damage to the attacker. */
  | { kind: 'guardReflect'; fraction: number }
  /** Each kill grants `add` stardust, up to `max` per run. */
  | { kind: 'killStardust'; add: number; max: number }
  /** Grant extra potions at run start. */
  | { kind: 'startPotion'; red?: number; blue?: number };

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
  kind: 'class' | 'startPotion' | 'rareChance' | 'startPerk' | 'cardSlot';
  value: string | number;
}

// ---------------------------------------------------------------------------
// 封測 2（11/1 凍結範圍）：史詩卡片、天賦、章／關、房間詞綴、精英。
// ---------------------------------------------------------------------------

export type CardRarity = 'epic' | 'legendary';

/** Boss drop, account-wide (any class can equip), kept forever. Effects reuse PerkEffect. */
export interface CardDef {
  id: string;
  name: string;
  desc: string;
  /** ENEMIES id of the boss whose pool this card is in. */
  boss: string;
  rarity: CardRarity;
  /** Guaranteed on that boss's first kill. One per boss. */
  signature?: boolean;
  effects: PerkEffect[];
  /** Each star above 1 scales the numeric values of `effects` by (1 + starScale). 0 = stars only add shards. */
  starScale: number;
  /** Icon: boss portrait + rarity frame; `icon` overrides when art ships one. */
  icon?: string;
}

/** Wearing `need` cards of the same boss adds these effects. */
export interface CardSetDef {
  boss: string;
  name: string;
  desc: string;
  need: number;
  effects: PerkEffect[];
}

/** One node of a class's 10-node talent line. Bought with stardust in order; free reset refunds everything. */
export interface TalentDef {
  id: string;
  classId: string;
  /** 1–10; node n requires node n-1. */
  tier: number;
  name: string;
  desc: string;
  cost: number;
  /** Marks the three "changes how you play" nodes (tiers 4, 7, 10). */
  keystone?: boolean;
  effects: PerkEffect[];
}

export interface ChapterDef {
  id: string;
  order: number;
  name: string;
  /** Background key shared by every stage of the chapter. */
  bg: string;
  /** Stage ids in play order. Beta 2: one stage per chapter (`<chapter>-3`); 1.0 inserts -1 and -2 in front. */
  stages: string[];
  /** Previous chapter's boss id; the chapter opens once it is in progress.bossFirstKills. Absent = open. */
  unlock?: { bossKill: string };
  /** Boss music id (chapter-end and final bosses only). */
  bossTrack?: string;
}

export interface RunStageDef {
  id: string;
  chapterId: string;
  /** 1 opening, 2 middle, 3 boss stage. */
  index: 1 | 2 | 3;
  name: string;
  desc: string;
  timeOfDay: 'day' | 'dusk' | 'night';
  combatRooms: number;
  /** Room ids drawn from (no repeats). */
  pool: string[];
  finale: { kind: 'none' | 'midBoss' | 'boss'; enemy?: string; room?: string };
  /** Per-slot enemy multipliers (index 0 = first room); overrides RUN_RULES.roomScaling. */
  scaling: { hp: number[]; atk: number[]; boss: { hp: number; atk: number } };
  /** Room affix ids allowed here; [] = none. Affixes only roll once the stage has been cleared before. */
  affixes: string[];
  /** STORY keys shown before the first room and after the finale. */
  story?: { before?: string; after?: string };
}

export type RoomAffixEffect =
  | { kind: 'fog'; visionMult: number; warnMs: number }
  | { kind: 'poisonGround'; zones: number; dpsFraction: number }
  | { kind: 'treasure'; extraPerkPicks: number }
  | { kind: 'elite'; forceElite: boolean; stardustMult: number }
  | { kind: 'rush'; waveTimeoutMult: number; hpMult: number };

export interface RoomAffixDef {
  id: string;
  name: string;
  desc: string;
  effect: RoomAffixEffect;
}

export type EliteModifier = 'shield' | 'haste' | 'volatile';

export interface EliteRules {
  /** Chance one enemy in a room is elite (rolled per room). 0 for chapters where elites are off. */
  chanceByChapterOrder: number[];
  hpMult: number;
  atkMult: number;
  sizeMult: number;
  stardust: number;
  /** One modifier is picked per elite. shield: blocks the first `shieldHits` hits; haste: speed × hasteMult;
   *  volatile: explodes on death for `volatilePower` × ATK in `volatileRadius` px. */
  modifiers: EliteModifier[];
  shieldHits: number;
  hasteMult: number;
  volatilePower: number;
  volatileRadius: number;
  /** Never elite: bosses and these behaviours. */
  excludeBehaviors: EnemyBehavior[];
}

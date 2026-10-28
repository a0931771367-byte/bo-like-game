/**
 * 一局平衡模擬器（遊戲設計部）。粗略模擬「選職業 → 4 房 → Boss」的一局，
 * 估算各房用時、死亡房間分布與通關率，用來在 Unity 版跑通前先調 RUN_RULES 的倍率。
 *
 * 用法：node tools/sim_run.mjs [runs=2000] [skill=0.55]
 *   skill = 玩家躲掉敵人攻擊的比例（0.55 ≈ 新手，0.75 ≈ 熟手）。
 *
 * 模型刻意簡化：玩家永遠打最近一隻、技能受 SP 預算限制、AoE 技能平均打 1.6 隻、
 * 敵人生成後 1.5 秒內接近並以 attackCd 節奏攻擊。數字只看相對變化，不當絕對值。
 */
import { build } from 'esbuild';
import { rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = dirname(dirname(fileURLToPath(import.meta.url)));
const TMP = join(ROOT, 'node_modules/.cache/sim_run.mjs');
await build({
  stdin: {
    contents: `
      export { CLASSES } from './src/data/classes';
      export { ENEMIES } from './src/data/enemies';
      export { ROOMS, ROOM_POOL, BOSS_ROOM } from './src/data/rooms';
      export { PERKS } from './src/data/perks';
      export { RUN_RULES } from './src/data/meta';
      export { lv } from './src/data/types';
      export { derive } from './src/systems/Stats';
      export { newProgress } from './src/systems/Save';`,
    resolveDir: ROOT, loader: 'ts',
  },
  bundle: true, format: 'esm', platform: 'node', outfile: TMP, logLevel: 'error',
  banner: { js: 'globalThis.localStorage ??= { getItem: () => null, setItem() {} };' },
});
const D = await import(pathToFileURL(TMP).href);
rmSync(TMP);

const RUNS = Number(process.argv[2] ?? 2000);
const SKILL = Number(process.argv[3] ?? 0.55);
const R = D.RUN_RULES;
const DT = 0.1;

// ---- seeded rng -----------------------------------------------------------
function rng(seed) {
  let s = seed >>> 0 || 1;
  return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296);
}
const pick = (rand, arr) => arr[Math.floor(rand() * arr.length)];
const shuffle = (rand, arr) => { const a = [...arr]; for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rand() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };

// ---- player model -----------------------------------------------------------
function makePlayer(cls) {
  const d = D.derive(cls, D.newProgress(cls));
  return {
    cls, d, hp: d.maxHp, sp: d.maxSp,
    mult: { maxHp: 1, atk: 1, matk: 1, move: 1, aspd: 1, cooldown: 1, spCost: 1, spRegen: 1, knock: 1, potionHeal: 1 },
    critAdd: 0, critDmgAdd: 0, skillLvAdd: 0, onKillHeal: 0, roomHeal: R.roomClearHeal, revive: null, revived: false,
    potions: { ...R.startPotions }, taken: {}, dpsMult: 1,
  };
}

function applyPerk(p, perk) {
  p.taken[perk.id] = (p.taken[perk.id] ?? 0) + 1;
  for (const e of perk.effects) {
    switch (e.kind) {
      case 'mult': p.mult[e.target] *= e.value; break;
      case 'crit': p.critAdd += e.chanceAdd ?? 0; p.critDmgAdd += e.damageMultAdd ?? 0; break;
      case 'skillLvAll': p.skillLvAdd += e.add; break;
      case 'onKillHeal': p.onKillHeal += e.fraction; break;
      case 'roomClearHeal': p.roomHeal += e.fraction; break;
      case 'potion': p.potions.red += e.red ?? 0; p.potions.blue += e.blue ?? 0; break;
      case 'revive': p.revive = e; break;
      case 'onKillAoe': p.dpsMult *= 1.15; break;
      case 'comboFinisherAoe': p.dpsMult *= 1.25; break;
      case 'projectileCount': p.dpsMult *= 1.35; break;
      case 'pierce': p.dpsMult *= 1.2; break;
    }
  }
}

function offerPerks(rand, p) {
  const eligible = D.PERKS.filter((k) => (k.classes.length === 0 || k.classes.includes(p.cls.id)) && (p.taken[k.id] ?? 0) < k.maxStacks);
  const out = [];
  for (let i = 0; i < R.perkChoices && eligible.length; i++) {
    const w = R.rarityWeights;
    let roll = rand() * (w.common + w.rare + w.epic);
    let rarity = roll < w.common ? 'common' : roll < w.common + w.rare ? 'rare' : 'epic';
    let pool = eligible.filter((k) => k.rarity === rarity && !out.includes(k));
    if (!pool.length) pool = eligible.filter((k) => !out.includes(k));
    if (!pool.length) break;
    out.push(pick(rand, pool));
  }
  return out;
}

/** Player damage per second against one target with the given def, plus how many targets the AoE share hits. */
function playerDps(p, enemyDef, enemyMdef) {
  const { d, cls } = p;
  const atk = d.atk * p.mult.atk, matk = d.matk * p.mult.matk;
  const crit = Math.min(100, d.crit + p.critAdd) / 100, critMult = 1.5 + p.critDmgAdd;
  const combo = cls.combo;
  const comboMs = combo.reduce((a, h) => a + h.startup + h.active + h.recovery, 0) / (d.aspd * p.mult.aspd);
  const comboDmg = combo.reduce((a, h) => a + Math.max(1, atk * h.power - enemyDef), 0) * (1 - crit + crit * critMult);
  const normal = (comboDmg / (comboMs / 1000)) * 0.7; // 30 % of the time spent walking / dodging
  // Skills: damage per cooldown cycle, limited by SP income.
  let skillDps = 0, spPerSec = 0;
  const spIncome = d.spRegen * p.mult.spRegen + d.maxSp / 60; // pool amortised over a minute
  for (const s of cls.skills) {
    const L = Math.min(s.maxLv, R.startSkillLv + p.skillLvAdd);
    if (s.power[0] <= 0) continue;
    const stat = s.magic ? matk : atk, def = s.magic ? enemyMdef : enemyDef;
    const count = s.count ? D.lv(s.count, L) : 1;
    const targets = s.type === 'aoe' || s.type === 'strikes' || s.type === 'wall' ? 1.6 : 1;
    const dmg = Math.max(1, stat * D.lv(s.power, L) - def) * count * targets * (s.type === 'wall' ? 4 : 1);
    const cycle = Math.max(s.cooldown * p.mult.cooldown, s.cast + 400) / 1000;
    const cost = D.lv(s.sp, L) * p.mult.spCost;
    skillDps += dmg / cycle; spPerSec += cost / cycle;
  }
  if (spPerSec > spIncome) skillDps *= spIncome / spPerSec;
  return (normal + skillDps) * p.dpsMult;
}

// ---- run model ------------------------------------------------------------
function simRoom(rand, p, room, slot, log) {
  const scale = room.kind === 'boss' ? R.roomScaling.boss : { hp: R.roomScaling.hp[slot], atk: R.roomScaling.atk[slot] };
  const set = pick(rand, room.waveSets);
  let t = 0, wave = 0, waveStart = 0, alive = [], pending = [];
  const spawnWave = (i) => { waveStart = t; pending = set[i].spawns.map((s) => ({ ...D.ENEMIES[s.enemy], at: t + s.delay / 1000 })); };
  spawnWave(0);
  const bossRoom = room.kind === 'boss';
  const def = p.d.def * (p.mult.maxHp > 1 ? 1 : 1);
  while (t < 180) {
    t += DT;
    // spawns
    for (const s of pending.filter((s) => s.at <= t)) {
      alive.push({ def: s, hp: s.hp * scale.hp, atk: s.atk * scale.atk, engageAt: t + 1.5, nextAtk: t + 1.5 + s.windup / 1000, boss: !!s.boss });
    }
    pending = pending.filter((s) => s.at > t);
    // next wave
    const waveDone = alive.filter((e) => bossRoom ? !e.boss : true).length === 0 && pending.length === 0;
    const timeout = set[wave].timeout > 0 && t - waveStart >= set[wave].timeout / 1000;
    if (wave < set.length - 1 && (waveDone || timeout)) { wave++; spawnWave(wave); }
    if (wave === set.length - 1 && alive.length === 0 && pending.length === 0) break;
    if (bossRoom && !alive.some((e) => e.boss) && !pending.some((s) => s.boss) && t > 1) break; // boss dead ends the room
    // player attacks the nearest (first) target
    const target = alive[0];
    if (target) {
      const dps = playerDps(p, target.def.def, target.def.mdef);
      target.hp -= dps * DT;
      if (target.hp <= 0) {
        alive.shift();
        p.hp = Math.min(p.d.maxHp * p.mult.maxHp, p.hp + p.onKillHeal * p.d.maxHp * p.mult.maxHp);
      }
    }
    // enemies attack
    for (const e of alive) {
      if (t < e.nextAtk) continue;
      e.nextAtk = t + (e.def.attackCd + e.def.windup) / 1000;
      const dodged = rand() < SKILL || rand() * 100 < p.d.flee;
      if (dodged) continue;
      const dmg = Math.max(1, Math.round(e.atk * (e.boss ? 1.2 : 1) - def * 0.6));
      p.hp -= dmg;
      if (p.hp <= 0) {
        if (p.revive && !p.revived) { p.revived = true; p.hp = p.d.maxHp * p.mult.maxHp * p.revive.fraction; continue; }
        log.deathSlot = bossRoom ? 4 : slot; return { cleared: false, t };
      }
    }
    // potion when low
    if (p.hp < p.d.maxHp * p.mult.maxHp * 0.4 && p.potions.red > 0) {
      p.potions.red--; p.hp = Math.min(p.d.maxHp * p.mult.maxHp, p.hp + p.d.maxHp * p.mult.maxHp * 0.35 * p.mult.potionHeal);
    }
  }
  return { cleared: true, t };
}

function simRun(seed, cls) {
  const rand = rng(seed);
  const p = makePlayer(cls);
  const rooms = shuffle(rand, D.ROOM_POOL).slice(0, R.combatRooms).map((id) => D.ROOMS[id]);
  const log = { roomTimes: [], perks: [], deathSlot: -1, cleared: false, total: 0 };
  for (let slot = 0; slot < rooms.length; slot++) {
    const r = simRoom(rand, p, rooms[slot], slot, log);
    log.roomTimes.push(r.t); log.total += r.t + R.roomTransition / 1000;
    if (!r.cleared) return log;
    p.hp = Math.min(p.d.maxHp * p.mult.maxHp, p.hp + p.d.maxHp * p.mult.maxHp * p.roomHeal);
    const offer = offerPerks(rand, p);
    // Greedy-ish player: prefer rare/epic, else random.
    const choice = offer.find((k) => k.rarity === 'epic') ?? offer.find((k) => k.rarity === 'rare') ?? offer[0];
    if (choice) { applyPerk(p, choice); log.perks.push(choice.id); }
  }
  const b = simRoom(rand, p, D.ROOMS[D.BOSS_ROOM], 4, log);
  log.roomTimes.push(b.t); log.total += b.t;
  log.cleared = b.cleared;
  return log;
}

// ---- report ---------------------------------------------------------------
const fmt = (n, d = 1) => n.toFixed(d);
console.log(`runs=${RUNS} playerSkill=${SKILL}  roomScaling hp=${R.roomScaling.hp} atk=${R.roomScaling.atk}`);
for (const cls of D.CLASSES.filter((c) => ['swordsman', 'mage', 'archer'].includes(c.id))) {
  const logs = Array.from({ length: RUNS }, (_, i) => simRun(i * 7919 + 17, cls));
  const clears = logs.filter((l) => l.cleared);
  const deaths = [0, 0, 0, 0, 0];
  for (const l of logs) if (l.deathSlot >= 0) deaths[l.deathSlot]++;
  const roomAvg = [0, 1, 2, 3, 4].map((i) => { const xs = logs.filter((l) => l.roomTimes[i] !== undefined && (l.cleared || l.deathSlot !== i)).map((l) => l.roomTimes[i]); return xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : NaN; });
  const perkCount = {};
  for (const l of logs) for (const id of l.perks) perkCount[id] = (perkCount[id] ?? 0) + 1;
  const top = Object.entries(perkCount).sort((a, b) => b[1] - a[1]).slice(0, 5).map(([k, v]) => `${k}:${v}`).join(' ');
  console.log(`\n${cls.name}(${cls.id})  通關率 ${fmt(clears.length / RUNS * 100)}%  通關平均用時 ${fmt((clears.reduce((a, l) => a + l.total, 0) / Math.max(1, clears.length)) / 60, 2)} 分`);
  console.log(`  各房平均用時(秒) 房1 ${fmt(roomAvg[0])} 房2 ${fmt(roomAvg[1])} 房3 ${fmt(roomAvg[2])} 房4 ${fmt(roomAvg[3])} Boss ${fmt(roomAvg[4])}`);
  console.log(`  死亡分布 房1 ${deaths[0]} 房2 ${deaths[1]} 房3 ${deaths[2]} 房4 ${deaths[3]} Boss ${deaths[4]}`);
  console.log(`  最常被選的強化 ${top}`);
}

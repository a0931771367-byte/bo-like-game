import Phaser from 'phaser';
import { GROUND_BOTTOM, GROUND_TOP } from '../config';
import { ANIMS, animKey, type AnimSet } from '../data/anims';
import { lv, type BuffEffect, type ClassDef, type ComboHit, type SkillDef } from '../data/types';
import type { GameScene } from '../scenes/GameScene';
import { controls, type Action } from '../systems/Controls';
import { session, type ClassProgress } from '../systems/Save';
import { calcDamage, derive, type Derived } from '../systems/Stats';
import { sfx } from '../systems/Sfx';
import type { Enemy } from './Enemy';
import { Fighter, type HitInfo } from './Fighter';

const JUMP_V = 680;
const DODGE_MS = 260;
const DODGE_SPEED = 640;
const PLAYER_H = 132;
const SKILL_KEYS: Action[] = ['skill0', 'skill1', 'skill2', 'skill3'];
const ARROW_SPEED = 1150;

export interface ActiveBuff { id: string; name: string; color: number; until: number; eff: BuffEffect; hits?: number }

export class Player extends Fighter {
  readonly team = 'player';
  d: Derived;
  sp = 0;
  maxSp = 0;
  combo = 0;
  queued = false;
  airAttackUsed = false;
  cooldowns: Record<string, number> = {};
  buffs: ActiveBuff[] = [];
  potionReadyAt = 0;
  dodgeDir: 1 | -1 = 1;
  dodgeMs = DODGE_MS;
  dodgeSpeed = DODGE_SPEED;
  private shotFired = false;
  private lastStepAt = 0;
  private lastGhostAt = 0;
  casting: SkillDef | null = null;
  private body: Phaser.GameObjects.Image;
  private aura: Phaser.GameObjects.Ellipse;
  private anim?: AnimSet;
  private bodyScale: number;

  constructor(scene: GameScene, x: number, y: number, public cls: ClassDef, public prog: ClassProgress) {
    super(scene, x, y, 46, 96);
    this.d = derive(cls, prog);
    this.maxHp = this.hp = this.d.maxHp;
    this.maxSp = this.sp = this.d.maxSp;

    this.aura = scene.add.ellipse(0, -60, 110, 150, 0x9be15d, 0).setStrokeStyle(4, 0x9be15d, 0);
    this.aura.setData('noflash', true);
    this.anim = ANIMS[cls.id] && scene.textures.exists(animKey(cls.id, 'idle', 0)) ? ANIMS[cls.id] : undefined;
    this.body = scene.add.image(0, 0, this.anim ? animKey(cls.id, 'idle', 0) : `${cls.id}_side`).setOrigin(0.5, 1);
    // Animation frames share one canvas with the feet on the bottom edge, so one scale fits every frame.
    this.bodyScale = (this.anim ? PLAYER_H * 1.08 : PLAYER_H) / this.body.height;
    this.body.setScale(this.bodyScale);
    this.view.add([this.aura, this.body]);
  }

  /** Recalculate derived stats after leveling or allocating points. */
  recompute(fullHeal = false) {
    const hpRatio = this.hp / this.maxHp;
    const spRatio = this.sp / this.maxSp;
    const all = this.buffs.reduce((a, b) => a + (b.eff.allStats ?? 0), 0);
    const stats = { ...this.prog.stats };
    if (all) for (const k of Object.keys(stats) as (keyof typeof stats)[]) stats[k] += all;
    this.d = derive(this.cls, { ...this.prog, stats });
    for (const b of this.buffs) {
      this.d.atk = Math.round(this.d.atk * (b.eff.atkMult ?? 1));
      this.d.matk = Math.round(this.d.matk * (b.eff.matkMult ?? 1));
      this.d.move *= b.eff.moveMult ?? 1;
      this.d.aspd *= b.eff.aspdMult ?? 1;
    }
    this.maxHp = this.d.maxHp;
    this.maxSp = this.d.maxSp;
    this.hp = fullHeal ? this.maxHp : Math.max(1, Math.round(this.maxHp * hpRatio));
    this.sp = fullHeal ? this.maxSp : Math.round(this.maxSp * spRatio);
  }

  skillLv(s: SkillDef) { return this.prog.skillLv[s.id] ?? 0; }
  skillCost(s: SkillDef) { return Math.round(lv(s.sp, Math.max(1, this.skillLv(s)))); }
  cooldownLeft(s: SkillDef) { return Math.max(0, (this.cooldowns[s.id] ?? 0) - this.now); }
  skillGold(s: SkillDef) { return s.goldCost ? Math.round(lv(s.goldCost, Math.max(1, this.skillLv(s)))) : 0; }
  /** Value of a learned passive (0 when not learned). */
  passiveValue(kind: NonNullable<SkillDef['passive']>) {
    const s = this.cls.skills.find((k) => k.passive === kind);
    if (!s || !s.passiveValue) return 0;
    const L = this.skillLv(s);
    return L > 0 ? lv(s.passiveValue, L) : 0;
  }
  get hidden() { return this.buffs.some((b) => b.eff.hidden); }
  get endure() { return this.buffs.some((b) => b.eff.endure); }

  addBuff(s: SkillDef, eff: BuffEffect, duration: number) {
    this.buffs = this.buffs.filter((b) => b.id !== s.id);
    this.buffs.push({ id: s.id, name: s.name, color: s.color, until: this.now + duration, eff, hits: eff.shieldHits });
    this.recompute();
  }

  /** Attacking or casting reveals a hidden player. */
  private breakHidden() {
    if (!this.hidden) return;
    this.buffs = this.buffs.filter((b) => !b.eff.hidden);
    this.recompute();
  }

  private expireBuffs() {
    const n = this.buffs.length;
    this.buffs = this.buffs.filter((b) => this.now < b.until && (b.hits === undefined || b.hits > 0));
    if (this.buffs.length !== n) this.recompute();
  }

  private get bounds() { return this.scene.playerBounds(); }
  private get enemies(): Enemy[] { return this.scene.enemies; }

  update(dt: number) {
    const ms = dt * 1000;
    this.stateTime += ms;
    if (!this.dead) {
      this.sp = Math.min(this.maxSp, this.sp + this.d.spRegen * dt);
      this.hp = Math.min(this.maxHp, this.hp + this.d.hpRegen * dt);
      this.expireBuffs();
      this.tryPotions();
    }
    const [xMin, xMax] = this.bounds;
    const grounded = !this.airborne;

    switch (this.state) {
      case 'idle':
      case 'walk':
        this.freeMove(dt);
        break;
      case 'guard':
        this.vx = this.vy = 0;
        if (!controls.guard) this.setState('idle');
        else if (this.tryDodge()) break;
        else if (this.trySkill()) break;
        break;
      case 'attack':
        this.updateAttack();
        break;
      case 'jump':
        this.vx = controls.moveX * this.d.move * 0.85;
        this.vy = controls.moveY * this.d.move * 0.4;
        if (controls.moveX) this.facing = controls.moveX > 0 ? 1 : -1;
        if (!this.airAttackUsed && controls.consume('attack')) {
          this.airAttackUsed = true;
          this.struck.clear();
          this.shotFired = false;
          this.breakHidden();
          this.stateTime = 0;
          this.setState('airAttack');
        }
        break;
      case 'airAttack': {
        const a = this.cls.air;
        this.vx = controls.moveX * this.d.move * 0.6;
        if (this.stateTime >= a.startup && this.stateTime <= a.startup + a.active) {
          if (this.cls.ranged) this.shootArrow(a);
          else this.strikeFront(a, 30);
        }
        break;
      }
      case 'dodge':
        this.vx = this.dodgeDir * this.dodgeSpeed * (1 - this.stateTime / this.dodgeMs * 0.6);
        this.vy = controls.moveY * 200;
        if (this.stateTime >= this.dodgeMs) this.setState('idle');
        break;
      case 'cast':
        this.vx *= 0.8;
        this.vy = 0;
        if (this.casting && this.stateTime >= this.casting.cast * this.d.castMult) {
          const s = this.casting;
          this.casting = null;
          if (!this.executeSkill(s)) this.setState('castEnd');
        }
        break;
      case 'castEnd':
        this.vx = 0;
        if (this.stateTime >= 160) this.setState('idle');
        break;
      case 'hurt':
        this.decel(dt);
        if (this.now >= this.stunUntil) this.setState('idle');
        break;
      case 'knock':
        break;
      case 'down':
        this.decel(dt);
        if (this.stateTime >= 650 && !this.dead) {
          this.invulnUntil = this.now + 700;
          this.setState('idle');
        }
        break;
      case 'dead':
        this.decel(dt);
        break;
    }

    const landed = this.physics(dt, xMin, xMax);
    if (landed) this.onLand(grounded);
    if (this.state === 'walk' && this.now - this.lastStepAt > 280) {
      this.lastStepAt = this.now;
      this.scene.fx.dust(this.x - this.facing * 10, this.y, 2, 0.6);
    }
    if (this.state === 'dodge' && this.now - this.lastGhostAt > 40) {
      this.lastGhostAt = this.now;
      this.scene.fx.ghost(this.body, this.x, this.y - this.z, this.facing, this.view.angle, this.cls.accent);
    }
  }

  private onLand(_wasGrounded: boolean) {
    this.scene.fx.dust(this.x, this.y, 6);
    sfx.play('land', 0.7);
    if (this.state === 'jump' || this.state === 'airAttack') {
      this.setState('idle');
      this.vx = 0;
    } else if (this.state === 'knock') {
      this.scene.cameras.main.shake(80, 0.004);
      this.setState(this.dead ? 'dead' : 'down');
      if (this.dead) this.scene.onPlayerDead();
    }
  }

  private freeMove(dt: number) {
    if (controls.consume('jump')) {
      this.vz = JUMP_V;
      this.z = 1;
      this.airAttackUsed = false;
      this.setState('jump');
      sfx.play('jump');
      this.scene.fx.dust(this.x, this.y, 4, 0.8);
      return;
    }
    if (controls.guard) {
      if (this.tryDodge()) return;
      this.setState('guard');
      return;
    }
    if (controls.consume('attack')) {
      this.startAttack(0);
      return;
    }
    if (this.trySkill()) return;
    const mx = controls.moveX;
    const my = controls.moveY;
    this.vx = mx * this.d.move;
    this.vy = my * this.d.move * 0.6;
    if (mx) this.facing = mx > 0 ? 1 : -1;
    const moving = Math.abs(mx) > 0.1 || Math.abs(my) > 0.1;
    if (moving && this.state !== 'walk') this.setState('walk');
    if (!moving && this.state !== 'idle') this.setState('idle');
    void dt;
  }

  private tryDodge() {
    const mx = controls.moveX;
    if (Math.abs(mx) < 0.5) return false;
    this.dodgeDir = mx > 0 ? 1 : -1;
    this.dodgeMs = DODGE_MS;
    this.dodgeSpeed = DODGE_SPEED;
    this.invulnUntil = this.now + DODGE_MS;
    this.setState('dodge');
    this.scene.fx.aura(this.x, this.y - 20, 0xffffff);
    this.scene.fx.dust(this.x, this.y, 5);
    sfx.play('dodge');
    return true;
  }

  private decel(dt: number) {
    const k = 1500 * dt;
    this.vx = Math.abs(this.vx) <= k ? 0 : this.vx - Math.sign(this.vx) * k;
    this.vy = 0;
  }

  // ---------- normal attacks ----------

  private timing(h: ComboHit) {
    const s = this.d.aspd;
    return { startup: h.startup / s, active: h.active / s, recovery: h.recovery / s };
  }

  private startAttack(i: number) {
    this.combo = i;
    this.queued = false;
    this.struck.clear();
    this.shotFired = false;
    this.breakHidden();
    if (controls.moveX) this.facing = controls.moveX > 0 ? 1 : -1;
    this.setState('attack');
    if (!this.cls.ranged) this.scene.time.delayedCall(this.timing(this.cls.combo[i]).startup * 0.6, () => { if (this.state === 'attack') sfx.play('swing', 0.8 + i * 0.1); });
  }

  private updateAttack() {
    const h = this.cls.combo[this.combo];
    const t = this.timing(h);
    this.vy = 0;
    this.vx = this.stateTime < t.startup ? this.facing * 70 : 0;
    const activeEnd = t.startup + t.active;
    if (this.stateTime >= t.startup && this.stateTime <= activeEnd) {
      if (this.cls.ranged) this.shootArrow(h);
      else {
        if (this.stateTime - this.scene.lastDt * 1000 < t.startup) {
          this.scene.fx.slash(this.x + this.facing * 40, this.y - 60 - this.z, this.facing, this.cls.accent, 60 + this.combo * 8, this.y);
        }
        this.strikeFront(h, 40);
      }
    }
    if (this.stateTime > t.startup) {
      if (this.combo < this.cls.combo.length - 1 && controls.consume('attack')) this.queued = true;
      if (this.stateTime > activeEnd && this.trySkill()) return;
    }
    if (this.stateTime >= activeEnd + t.recovery * (this.queued ? 0.35 : 1)) {
      if (this.queued) this.startAttack(this.combo + 1);
      else this.setState('idle');
    }
  }

  private strikeFront(h: ComboHit, depth: number) {
    const n = this.scene.strikeFront(this, this.enemies, h.range, depth, (t) => this.normalHit(t as Enemy, h));
    if (n > 0) this.scene.hitStop(h.launch > 0 ? 90 : 55);
  }

  /** Ranged classes fire one arrow per combo step instead of swinging. */
  private shootArrow(h: ComboHit) {
    if (this.shotFired) return;
    this.shotFired = true;
    const fire = (zOff: number) => this.scene.spawnProjectile({
      owner: this, x: this.x + this.facing * 30, y: this.y, z: this.z + 70 + zOff, dir: this.facing,
      speed: ARROW_SPEED, color: this.cls.accent, range: h.range, arrow: true,
      make: (t) => this.makeHit(t as Enemy, h.power, 'neutral', false, h.knock, h.launch, h.stun),
    });
    fire(0);
    sfx.play('arrow');
    this.scene.fx.spark(this.x + this.facing * 36, this.y - 70 - this.z, this.cls.accent, 0.6);
    const da = this.passiveValue('doubleAttack');
    if (da && Math.random() < da) this.scene.time.delayedCall(80, () => { if (!this.dead) fire(-12); });
  }

  /** A normal-attack hit, with the thief's double-attack passive rolled in. */
  private normalHit(t: Enemy, h: ComboHit): HitInfo {
    const hit = this.makeHit(t, h.power, 'neutral', false, h.knock, h.launch, h.stun);
    const da = this.passiveValue('doubleAttack');
    if (da && Math.random() < da) {
      this.scene.time.delayedCall(70, () => {
        if (t.dead || this.dead) return;
        this.scene.fx.number(t.x, t.y - t.h - 70 - t.z, '連擊', '#b9f5c8');
        this.scene.fx.slash(t.x, t.y - 60 - t.z, this.facing, this.cls.accent, 50, t.y);
        t.takeHit(this.makeHit(t, h.power, 'neutral', false, h.knock * 0.5, 0, h.stun));
      });
    }
    return hit;
  }

  makeHit(t: Enemy, power: number, element: SkillDef['element'], magic: boolean, knock: number, launch: number, stun: number): HitInfo {
    const r = calcDamage(this.d, t.defender(), power, element, magic);
    const dir = (Math.sign(t.x - this.x) || this.facing) as 1 | -1;
    return { ...r, element, dir, knock, launch, stun, source: this };
  }

  // ---------- skills ----------

  private trySkill() {
    for (let i = 0; i < SKILL_KEYS.length; i++) {
      if (!controls.peek(SKILL_KEYS[i])) continue;
      controls.consume(SKILL_KEYS[i]);
      const s = this.cls.skills[i];
      if (!s) continue;
      if (this.skillLv(s) < 1) { this.scene.toast(`尚未學習「${s.name}」，按選單加技能點`); continue; }
      if (s.type === 'passive') { this.scene.toast(`「${s.name}」是被動技能，已自動生效`); continue; }
      if (this.cooldownLeft(s) > 0) continue;
      const cost = this.skillCost(s);
      if (this.sp < cost) { this.scene.fx.number(this.x, this.y - 120, 'SP 不足', '#7fb2ff'); continue; }
      const gold = this.skillGold(s);
      if (session.save.gold < gold) { this.scene.fx.number(this.x, this.y - 120, '金幣不足', '#ffd34d'); continue; }
      if (gold) { session.save.gold -= gold; this.scene.fx.number(this.x, this.y - 150, `-${gold} G`, '#ffd34d'); }
      this.sp -= cost;
      this.cooldowns[s.id] = this.now + s.cooldown * this.d.castMult;
      if (controls.moveX) this.facing = controls.moveX > 0 ? 1 : -1;
      this.casting = s;
      this.struck.clear();
      this.setState('cast');
      this.scene.fx.aura(this.x, this.y - 10, s.color);
      sfx.play(s.magic ? 'cast' : 'skill', 0.8);
      return true;
    }
    return false;
  }

  /** Returns true when the skill took over the state machine (e.g. a dash). */
  private executeSkill(s: SkillDef): boolean {
    const L = this.skillLv(s);
    const power = lv(s.power, L);
    const el = s.element;
    const fx = this.scene.fx;
    if (!s.buff?.(L).hidden) this.breakHidden();
    const skillSound = { melee: 'hitHeavy', aoe: 'fire', debuff: 'buff', buff: 'buff', heal: 'heal', dash: 'dodge', passive: 'select',
      projectile: el === 'water' ? 'ice' : el === 'fire' ? 'fire' : el === 'holy' ? 'cast' : 'arrow', strikes: el === 'wind' ? 'thunder' : 'arrow', wall: 'fire' } as const;
    sfx.play(skillSound[s.type]);
    switch (s.type) {
      case 'melee': {
        fx.slash(this.x + this.facing * 50, this.y - 60, this.facing, s.color, 80, this.y);
        const stunChance = s.stunChance ? lv(s.stunChance, L) : 0;
        const n = this.scene.strikeFront(this, this.enemies, s.range ?? 120, 45, (t) => {
          const stun = Math.random() < stunChance ? 1800 : s.stun ?? 400;
          const h = this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 200, s.launch ?? 0, stun);
          if (s.poison) h.poison = { dps: lv(s.poison, L), duration: 5000 };
          if (stun > 1000) this.scene.time.delayedCall(10, () => fx.number(t.x, t.y - t.h - 50, '暈眩', '#ffe066'));
          return h;
        });
        if (n) { this.scene.hitStop(110); this.scene.cameras.main.shake(90, 0.006); }
        break;
      }
      case 'aoe': {
        const r = s.radius ?? 150;
        fx.ring(this.x, this.y, r, s.color, this.y - 1);
        fx.spark(this.x, this.y - 40, s.color, 2);
        this.scene.cameras.main.shake(160, 0.008);
        const n = this.scene.strikeCircle(this, this.enemies, this.x, this.y, r, (t) =>
          this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 300, s.launch ?? 0, s.stun ?? 400));
        if (n) this.scene.hitStop(120);
        break;
      }
      case 'debuff': {
        const r = s.radius ?? 300;
        fx.ring(this.x, this.y, r, s.color, this.y - 1);
        fx.number(this.x, this.y - 130, s.name, '#ff8fa3');
        const down = s.defDown ? lv(s.defDown, L) : 0.3;
        const dur = s.duration ? lv(s.duration, L) : 8000;
        for (const e of this.enemies) {
          if (e.dead || Phaser.Math.Distance.Between(e.x, e.y * 2, this.x, this.y * 2) > r * 1.4) continue;
          e.applyDefDown(down, dur);
        }
        break;
      }
      case 'buff': {
        const dur = s.duration ? lv(s.duration, L) : 5000;
        this.addBuff(s, s.buff ? s.buff(L) : { endure: true }, dur);
        fx.aura(this.x, this.y, s.color);
        fx.number(this.x, this.y - 130, s.name, '#b6f28a');
        break;
      }
      case 'heal': {
        const v = Math.round(this.maxHp * (s.heal ? lv(s.heal, L) : 0.2) + this.d.matk * 0.6);
        this.hp = Math.min(this.maxHp, this.hp + v);
        fx.aura(this.x, this.y, s.color);
        fx.ring(this.x, this.y, s.radius ?? 150, s.color, this.y - 1);
        fx.number(this.x, this.y - 130, `+${v}`, '#7dff9a', true);
        // Holy light also burns the undead nearby.
        const undead = this.enemies.filter((e) => e.def.element === 'dark');
        this.scene.strikeCircle(this, undead, this.x, this.y, s.radius ?? 150, (t) =>
          this.makeHit(t as Enemy, power, 'holy', true, s.knock ?? 200, 0, s.stun ?? 400));
        break;
      }
      case 'dash': {
        const dist = s.dashDistance ? lv(s.dashDistance, L) : 220;
        const dur = s.duration ? lv(s.duration, L) : 260;
        this.dodgeDir = -this.facing as 1 | -1;
        this.dodgeMs = dur;
        this.dodgeSpeed = dist / (dur / 1000) / 0.7;
        this.invulnUntil = this.now + dur + 60;
        fx.aura(this.x, this.y - 20, s.color);
        fx.spark(this.x, this.y - 60, s.color, 1.2);
        this.setState('dodge');
        return true;
      }
      case 'passive':
        break;
      case 'projectile': {
        const n = Math.floor(s.count ? lv(s.count, L) : 1);
        const freeze = s.freezeChance ? lv(s.freezeChance, L) : 0;
        for (let i = 0; i < n; i++) {
          this.scene.time.delayedCall(i * 120, () => {
            if (this.dead) return;
            this.scene.spawnProjectile({
              owner: this, x: this.x + this.facing * 40, y: this.y, z: 60 + (i % 2) * 14, dir: this.facing,
              speed: s.speed ?? 700, color: s.color, range: 900,
              make: (t) => {
                const h = this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 100, 0, s.stun ?? 300);
                if (freeze && Math.random() < freeze) h.freeze = 2200;
                return h;
              },
            });
          });
        }
        break;
      }
      case 'strikes': {
        const n = Math.floor(s.count ? lv(s.count, L) : 3);
        const range = s.range ?? 400;
        const r = s.radius ?? 80;
        const ox = this.x;
        const dir = this.facing;
        for (let i = 0; i < n; i++) {
          this.scene.time.delayedCall(i * 130, () => {
            const px = ox + dir * (110 + Math.random() * range);
            const py = Phaser.Math.Clamp(this.y + Phaser.Math.Between(-70, 70), GROUND_TOP, GROUND_BOTTOM);
            fx.pillar(px, py, s.color);
            this.scene.cameras.main.shake(60, 0.004);
            const target = { x: px } as Fighter;
            this.scene.strikeCircle(this, this.enemies, px, py, r, (t) => {
              const h = this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 150, 0, s.stun ?? 400);
              h.dir = (Math.sign(t.x - target.x) || dir) as 1 | -1;
              return h;
            }, true);
          });
        }
        break;
      }
      case 'wall': {
        const dur = s.duration ? lv(s.duration, L) : 4000;
        this.scene.spawnWall({
          owner: this, x: this.x + this.facing * (s.range ?? 150), y: this.y, duration: dur, color: s.color,
          make: (t) => this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 300, 0, s.stun ?? 300),
        });
        break;
      }
    }
    return false;
  }

  // ---------- items ----------

  private tryPotions() {
    const pots = session.save.potions;
    if (controls.consume('pot0') && this.now >= this.potionReadyAt) {
      if (pots.red <= 0) { this.scene.toast('紅色藥水用完了'); return; }
      pots.red--;
      const v = Math.round(this.maxHp * 0.35);
      this.hp = Math.min(this.maxHp, this.hp + v);
      this.potionReadyAt = this.now + 800;
      this.scene.fx.number(this.x, this.y - 120, `+${v}`, '#7dff9a');
      this.scene.fx.aura(this.x, this.y, 0xff5d5d);
      sfx.play('potion');
    }
    if (controls.consume('pot1') && this.now >= this.potionReadyAt) {
      if (pots.blue <= 0) { this.scene.toast('藍色藥水用完了'); return; }
      pots.blue--;
      const v = Math.round(this.maxSp * 0.4);
      this.sp = Math.min(this.maxSp, this.sp + v);
      this.potionReadyAt = this.now + 800;
      this.scene.fx.number(this.x, this.y - 120, `+${v} SP`, '#7fb2ff');
      this.scene.fx.aura(this.x, this.y, 0x5d8bff);
      sfx.play('potion');
    }
  }

  // ---------- receiving damage ----------

  takeHit(h: HitInfo) {
    if (this.dead || this.now < this.invulnUntil || this.state === 'dodge') return false;
    const fx = this.scene.fx;
    const shield = this.buffs.find((b) => (b.hits ?? 0) > 0);
    if (shield) {
      shield.hits!--;
      fx.ring(this.x, this.y, 80, shield.color, this.y - 1);
      fx.number(this.x, this.y - 120 - this.z, '結界', '#cfe8ff');
      if (shield.hits! <= 0) this.expireBuffs();
      return true;
    }
    if (Math.random() * 100 < this.d.flee) {
      fx.number(this.x, this.y - 110 - this.z, 'MISS', '#d0d0d0');
      return false;
    }
    const guarding = this.state === 'guard' && Math.sign(h.source.x - this.x) === this.facing;
    if (guarding) {
      const dmg = Math.max(1, Math.round(h.damage * 0.25));
      this.hp = Math.max(1, this.hp - dmg);
      this.vx = h.dir * 160;
      fx.spark(this.x + this.facing * 25, this.y - 60, 0x9fd8ff);
      fx.number(this.x, this.y - 110, `${dmg}`, '#9fd8ff');
      sfx.play('guard');
      return true;
    }
    this.hp -= h.damage;
    fx.number(this.x, this.y - 110 - this.z, `${h.damage}`, '#ff5a5a', h.damage > this.maxHp * 0.2);
    fx.spark(this.x, this.y - 55 - this.z, 0xff7070);
    this.flash(0xff4444, 90);
    this.scene.cameras.main.shake(90, 0.005);
    sfx.play('hurt');
    this.casting = null;
    if (this.hp <= 0) {
      this.hp = 0;
      this.dead = true;
      this.vz = 520;
      this.z = 1;
      this.vx = h.dir * 260;
      this.setState('knock');
      return true;
    }
    if (this.endure) return true;
    if (h.launch > 0 || this.airborne) {
      this.vz = Math.max(h.launch, 380);
      this.z = Math.max(this.z, 1);
      this.vx = h.dir * Math.max(180, h.knock * 0.6);
      this.setState('knock');
    } else {
      this.vx = h.dir * h.knock;
      this.stunUntil = this.now + h.stun;
      this.setState('hurt');
    }
    return true;
  }

  // ---------- presentation ----------

  /** Which drawn frame fits the current state (classes with an animation sheet). */
  private frameKey(a: AnimSet) {
    const id = this.cls.id;
    const t = this.stateTime;
    switch (this.state) {
      case 'walk': return animKey(id, 'walk', Math.floor(t / 80) % a.walk);
      case 'attack': {
        const h = this.timing(this.cls.combo[this.combo]);
        const i = t < h.startup ? 0 : t < h.startup + h.active ? 1 : t < h.startup + h.active + h.recovery * 0.45 ? 2 : 3;
        return animKey(id, 'attack', Math.min(i, a.attack - 1));
      }
      case 'airAttack': return animKey(id, 'attack', Math.min(t < this.cls.air.startup ? 0 : 1, a.attack - 1));
      case 'jump': return animKey(id, 'walk', Math.min(1, a.walk - 1));
      case 'dodge': return animKey(id, 'walk', Math.min(2, a.walk - 1));
      case 'cast': case 'castEnd': return animKey(id, 'attack', Math.min(t < 200 ? 0 : 3, a.attack - 1));
      default: return animKey(id, 'idle', Math.floor(this.now / 170) % a.idle);
    }
  }

  syncView() {
    super.syncView();
    const t = this.stateTime;
    const base = this.bodyScale;
    const a = this.anim;
    if (a) {
      const key = this.frameKey(a);
      if (this.body.texture.key !== key) this.body.setTexture(key);
    }
    const k = a ? 0.35 : 1; // frames carry most of the motion; keep a little squash on top
    let sx = 1, sy = 1, lean = 0, bob = 0;
    const breath = Math.sin(this.now / 420);
    switch (this.state) {
      case 'idle': sy = 1 + breath * 0.028 * k; sx = 1 - breath * 0.014 * k; lean = Math.sin(this.now / 840) * 1.5 * k; break;
      case 'walk': {
        const ph = t / 75;
        bob = Math.abs(Math.sin(ph)) * 6 * k;
        sy = 1 + Math.sin(ph) * 0.04 * k;
        sx = 1 - Math.sin(ph) * 0.03 * k;
        lean = (7 + Math.sin(ph * 2) * 2.5) * k;
        break;
      }
      case 'guard': sx = 0.92; sy = 1 + breath * 0.015; lean = -6; break;
      case 'attack': {
        const h = this.timing(this.cls.combo[this.combo]);
        if (t < h.startup) { sx = 1 - 0.1 * k * (1 - t / h.startup); lean = -8 * k; }
        else if (t < h.startup + h.active) { sx = 1 + 0.15 * k; sy = 1 - 0.05 * k; lean = 10 * k; }
        else { sx = 1 + 0.05 * k; lean = 6 * k; }
        break;
      }
      case 'airAttack': lean = 18; sx = 1.1; break;
      case 'jump': sy = 1.08; sx = 0.95; lean = 5; break;
      case 'cast': sy = 1.05 + Math.sin(t / 50) * 0.02; lean = -4; break;
      case 'castEnd': sx = 1.1; lean = 8; break;
      case 'dodge': lean = 22; sx = 1.1; sy = 0.9; break;
      case 'hurt': lean = -14; sx = 0.92; break;
      case 'knock': lean = -45; break;
      case 'down': case 'dead': lean = -90; break;
    }
    this.body.setScale(base * sx, base * sy);
    this.view.angle = lean * this.facing;
    this.view.y -= bob;
    const glow = this.buffs.find((b) => b.eff.endure || (b.hits ?? 0) > 0 || b.eff.atkMult || b.eff.allStats || b.eff.aspdMult);
    if (glow) this.aura.setStrokeStyle(4, glow.color, 0.6 + Math.sin(this.now / 90) * 0.3);
    else this.aura.setStrokeStyle(4, 0x9be15d, 0);
    const blink = this.now < this.invulnUntil && this.state !== 'dodge';
    this.view.alpha = this.hidden ? 0.3 : blink ? (Math.floor(this.now / 60) % 2 ? 0.35 : 1) : this.state === 'dodge' ? 0.6 : 1;
  }
}

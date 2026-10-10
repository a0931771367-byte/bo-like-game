import Phaser from 'phaser';
import { GROUND_BOTTOM, GROUND_TOP } from '../config';
import { lv, type ClassDef, type ComboHit, type SkillDef } from '../data/types';
import type { GameScene } from '../scenes/GameScene';
import { controls, type Action } from '../systems/Controls';
import { session, type ClassProgress } from '../systems/Save';
import { calcDamage, derive, type Derived } from '../systems/Stats';
import type { Enemy } from './Enemy';
import { Fighter, type HitInfo } from './Fighter';

const JUMP_V = 680;
const DODGE_MS = 260;
const DODGE_SPEED = 640;
const SKILL_KEYS: Action[] = ['skill0', 'skill1', 'skill2', 'skill3'];

export class Player extends Fighter {
  readonly team = 'player';
  d: Derived;
  sp = 0;
  maxSp = 0;
  combo = 0;
  queued = false;
  airAttackUsed = false;
  cooldowns: Record<string, number> = {};
  endureUntil = 0;
  potionReadyAt = 0;
  dodgeDir: 1 | -1 = 1;
  casting: SkillDef | null = null;
  private arm: Phaser.GameObjects.Container;
  private torso: Phaser.GameObjects.Rectangle;
  private aura: Phaser.GameObjects.Ellipse;

  constructor(scene: GameScene, x: number, y: number, public cls: ClassDef, public prog: ClassProgress) {
    super(scene, x, y, 46, 96);
    this.d = derive(cls, prog);
    this.maxHp = this.hp = this.d.maxHp;
    this.maxSp = this.sp = this.d.maxSp;

    this.aura = scene.add.ellipse(0, -48, 90, 120, 0x9be15d, 0).setStrokeStyle(4, 0x9be15d, 0);
    const legs = scene.add.rectangle(0, -16, 30, 32, 0x2b2d42);
    this.torso = scene.add.rectangle(0, -50, 40, 40, cls.color);
    const belt = scene.add.rectangle(0, -32, 40, 6, 0x3a2a1a);
    const head = scene.add.circle(2, -84, 17, 0xf5d0b0);
    const hair = scene.add.rectangle(-4, -96, 34, 12, cls.accent);
    const eye = scene.add.rectangle(10, -86, 4, 6, 0x1a1020);
    this.arm = scene.add.container(6, -56);
    if (cls.weapon === 'staff') {
      this.arm.add(scene.add.rectangle(0, -34, 7, 86, 0x7a5230));
      this.arm.add(scene.add.circle(0, -80, 10, cls.accent));
    } else {
      this.arm.add(scene.add.rectangle(0, -10, 16, 6, 0x5a4630));
      this.arm.add(scene.add.rectangle(0, -44, 10, 62, cls.accent));
    }
    this.view.add([this.aura, legs, this.torso, belt, head, hair, eye, this.arm]);
  }

  /** Recalculate derived stats after leveling or allocating points. */
  recompute(fullHeal = false) {
    const hpRatio = this.hp / this.maxHp;
    const spRatio = this.sp / this.maxSp;
    this.d = derive(this.cls, this.prog);
    this.maxHp = this.d.maxHp;
    this.maxSp = this.d.maxSp;
    this.hp = fullHeal ? this.maxHp : Math.max(1, Math.round(this.maxHp * hpRatio));
    this.sp = fullHeal ? this.maxSp : Math.round(this.maxSp * spRatio);
  }

  skillLv(s: SkillDef) { return this.prog.skillLv[s.id] ?? 0; }
  skillCost(s: SkillDef) { return Math.round(lv(s.sp, Math.max(1, this.skillLv(s)))); }
  cooldownLeft(s: SkillDef) { return Math.max(0, (this.cooldowns[s.id] ?? 0) - this.now); }

  private get bounds() { return this.scene.playerBounds(); }
  private get enemies(): Enemy[] { return this.scene.enemies; }

  update(dt: number) {
    const ms = dt * 1000;
    this.stateTime += ms;
    if (!this.dead) {
      this.sp = Math.min(this.maxSp, this.sp + this.d.spRegen * dt);
      this.hp = Math.min(this.maxHp, this.hp + this.d.hpRegen * dt);
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
          this.stateTime = 0;
          this.setState('airAttack');
        }
        break;
      case 'airAttack': {
        const a = this.cls.air;
        this.vx = controls.moveX * this.d.move * 0.6;
        if (this.stateTime >= a.startup && this.stateTime <= a.startup + a.active) {
          this.strikeFront(a, 'neutral', false, 30);
        }
        break;
      }
      case 'dodge':
        this.vx = this.dodgeDir * DODGE_SPEED * (1 - this.stateTime / DODGE_MS * 0.6);
        this.vy = controls.moveY * 200;
        if (this.stateTime >= DODGE_MS) this.setState('idle');
        break;
      case 'cast':
        this.vx *= 0.8;
        this.vy = 0;
        if (this.casting && this.stateTime >= this.casting.cast * this.d.castMult) {
          this.executeSkill(this.casting);
          this.casting = null;
          this.setState('castEnd');
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
  }

  private onLand(_wasGrounded: boolean) {
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
    this.invulnUntil = this.now + DODGE_MS;
    this.setState('dodge');
    this.scene.fx.aura(this.x, this.y - 20, 0xffffff);
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
    if (controls.moveX) this.facing = controls.moveX > 0 ? 1 : -1;
    this.setState('attack');
  }

  private updateAttack() {
    const h = this.cls.combo[this.combo];
    const t = this.timing(h);
    this.vy = 0;
    this.vx = this.stateTime < t.startup ? this.facing * 70 : 0;
    const activeEnd = t.startup + t.active;
    if (this.stateTime >= t.startup && this.stateTime <= activeEnd) {
      if (this.stateTime - this.scene.lastDt * 1000 < t.startup) {
        this.scene.fx.slash(this.x + this.facing * 40, this.y - 60 - this.z, this.facing, this.cls.accent, 60 + this.combo * 8, this.y);
      }
      this.strikeFront(h, 'neutral', false, 40);
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

  private strikeFront(h: ComboHit, element: SkillDef['element'], magic: boolean, depth: number) {
    const n = this.scene.strikeFront(this, this.enemies, h.range, depth, (t) =>
      this.makeHit(t as Enemy, h.power, element, magic, h.knock, h.launch, h.stun));
    if (n > 0) this.scene.hitStop(h.launch > 0 ? 90 : 55);
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
      if (this.cooldownLeft(s) > 0) continue;
      const cost = this.skillCost(s);
      if (this.sp < cost) { this.scene.fx.number(this.x, this.y - 120, 'SP 不足', '#7fb2ff'); continue; }
      this.sp -= cost;
      this.cooldowns[s.id] = this.now + s.cooldown * this.d.castMult;
      if (controls.moveX) this.facing = controls.moveX > 0 ? 1 : -1;
      this.casting = s;
      this.struck.clear();
      this.setState('cast');
      this.scene.fx.aura(this.x, this.y - 10, s.color);
      return true;
    }
    return false;
  }

  private executeSkill(s: SkillDef) {
    const L = this.skillLv(s);
    const power = lv(s.power, L);
    const el = s.element;
    const fx = this.scene.fx;
    switch (s.type) {
      case 'melee': {
        fx.slash(this.x + this.facing * 50, this.y - 60, this.facing, s.color, 80, this.y);
        const stunChance = s.stunChance ? lv(s.stunChance, L) : 0;
        const n = this.scene.strikeFront(this, this.enemies, s.range ?? 120, 45, (t) => {
          const stun = Math.random() < stunChance ? 1800 : s.stun ?? 400;
          const h = this.makeHit(t as Enemy, power, el, s.magic, s.knock ?? 200, s.launch ?? 0, stun);
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
        this.endureUntil = this.now + dur;
        fx.aura(this.x, this.y, s.color);
        fx.number(this.x, this.y - 130, s.name, '#b6f28a');
        break;
      }
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
    }
    if (controls.consume('pot1') && this.now >= this.potionReadyAt) {
      if (pots.blue <= 0) { this.scene.toast('藍色藥水用完了'); return; }
      pots.blue--;
      const v = Math.round(this.maxSp * 0.4);
      this.sp = Math.min(this.maxSp, this.sp + v);
      this.potionReadyAt = this.now + 800;
      this.scene.fx.number(this.x, this.y - 120, `+${v} SP`, '#7fb2ff');
      this.scene.fx.aura(this.x, this.y, 0x5d8bff);
    }
  }

  // ---------- receiving damage ----------

  takeHit(h: HitInfo) {
    if (this.dead || this.now < this.invulnUntil || this.state === 'dodge') return false;
    const fx = this.scene.fx;
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
      return true;
    }
    this.hp -= h.damage;
    fx.number(this.x, this.y - 110 - this.z, `${h.damage}`, '#ff5a5a', h.damage > this.maxHp * 0.2);
    fx.spark(this.x, this.y - 55 - this.z, 0xff7070);
    this.flash(0xff4444, 90);
    this.scene.cameras.main.shake(90, 0.005);
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
    if (this.now < this.endureUntil) return true;
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

  syncView() {
    super.syncView();
    let angle = 25;
    let bob = 0;
    let lean = 0;
    const t = this.stateTime;
    switch (this.state) {
      case 'walk': bob = Math.abs(Math.sin(t / 80)) * 4; angle = 25 + Math.sin(t / 80) * 8; break;
      case 'guard': angle = -10; lean = -4; break;
      case 'attack': {
        const h = this.timing(this.cls.combo[this.combo]);
        if (t < h.startup) angle = Phaser.Math.Linear(25, -100, t / h.startup);
        else if (t < h.startup + h.active) angle = Phaser.Math.Linear(-100, 120, (t - h.startup) / h.active);
        else angle = 120;
        lean = 6;
        break;
      }
      case 'airAttack': angle = Math.min(160, -60 + t * 1.2); break;
      case 'jump': angle = -30; break;
      case 'cast': angle = -150 + Math.sin(t / 50) * 10; break;
      case 'castEnd': angle = 90; lean = 5; break;
      case 'dodge': lean = 18; angle = 60; break;
      case 'hurt': lean = -14; angle = 60; break;
      case 'knock': case 'down': case 'dead': lean = -80; angle = 90; break;
    }
    this.arm.angle = angle;
    this.view.angle = lean * this.facing;
    this.view.y -= bob;
    if (this.state === 'down' || this.state === 'dead') this.view.angle = -90 * this.facing;
    const armored = this.now < this.endureUntil;
    this.aura.setStrokeStyle(4, 0x9be15d, armored ? 0.6 + Math.sin(this.now / 90) * 0.3 : 0);
    const blink = this.now < this.invulnUntil && this.state !== 'dodge';
    this.view.alpha = blink ? (Math.floor(this.now / 60) % 2 ? 0.35 : 1) : this.state === 'dodge' ? 0.6 : 1;
  }
}

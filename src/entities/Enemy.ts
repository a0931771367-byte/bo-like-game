import Phaser from 'phaser';
import type { EnemyDef } from '../data/types';
import type { GameScene } from '../scenes/GameScene';
import { sfx } from '../systems/Sfx';
import { Fighter, type HitInfo } from './Fighter';
import type { Player } from './Player';

export class Enemy extends Fighter {
  readonly team = 'enemy';
  attackReadyAt = 0;
  defDownMult = 1;
  defDownUntil = 0;
  hitCount = 0;
  lastHitAt = 0;
  poisonDps = 0;
  poisonUntil = 0;
  private poisonNext = 0;
  private body: Phaser.GameObjects.Image;
  private baseScale = 1;
  private hpBar?: Phaser.GameObjects.Rectangle;
  private lungeDir = 1;
  private retreatUntil = 0;
  private phase = Math.random() * 1000;

  constructor(scene: GameScene, x: number, y: number, public def: EnemyDef) {
    super(scene, x, y, def.w, def.h);
    this.maxHp = this.hp = def.hp;
    this.facing = x > scene.player.x ? -1 : 1;
    const { w } = def;
    this.body = scene.add.image(0, 0, `${def.sprite ?? def.id}_side`).setOrigin(0.5, 1);
    if (def.tint !== undefined) this.body.setTint(def.tint);
    this.baseScale = def.spriteH / this.body.height;
    this.body.setScale(this.baseScale);
    this.view.add(this.body);
    if (!def.boss) {
      this.view.add(scene.add.rectangle(0, -def.spriteH - 14, w, 6, 0x000000, 0.5).setData('noflash', true));
      this.hpBar = scene.add.rectangle(-w / 2, -def.spriteH - 14, w, 6, 0xff4d4d).setOrigin(0, 0.5).setData('noflash', true);
      this.view.add(this.hpBar);
    }
  }

  defender() {
    return { def: this.def.def, mdef: this.def.mdef, element: this.def.element, defMult: this.now < this.defDownUntil ? this.defDownMult : 1 };
  }

  applyDefDown(amount: number, duration: number) {
    this.defDownMult = Math.max(0, 1 - amount);
    this.defDownUntil = this.now + duration;
    this.scene.fx.number(this.x, this.y - this.h - 40, '防禦↓', '#ff8fa3');
  }

  private get player(): Player { return this.scene.player; }

  update(dt: number) {
    const ms = dt * 1000;
    this.stateTime += ms;
    const [xMin, xMax] = this.scene.enemyBounds();
    if (!this.dead && this.now < this.poisonUntil && this.now >= this.poisonNext) {
      this.poisonNext = this.now + 500;
      this.applyDot(Math.max(1, Math.round(this.poisonDps * 0.5)), '#c47eff');
    }
    if (this.frozen && !this.dead) {
      this.vx = this.vy = 0;
      if (this.state !== 'knock') this.setState('frozen');
    } else if (this.state === 'frozen') this.setState('idle');

    switch (this.state) {
      case 'idle':
      case 'walk':
        this.think(dt);
        break;
      case 'windup':
        this.vx = this.vy = 0;
        if (this.stateTime >= this.def.windup) this.doAttack();
        break;
      case 'lunge':
        this.vx = this.lungeDir * this.def.speed * 2.6;
        this.vy = 0;
        if (this.stateTime > 90 && this.stateTime < 420) this.tryHitPlayer(this.def.attackRange * 0.35, 1.1, 260, 0, 500);
        if (this.stateTime >= 480) { this.setState('recover'); this.vx = 0; }
        break;
      case 'attack':
        this.vx = this.vy = 0;
        if (this.stateTime >= 70 && this.stateTime <= 190) this.tryHitPlayer(this.def.attackRange, 1, 220, 0, 420);
        if (this.stateTime >= 360) this.setState('recover');
        break;
      case 'slam':
        this.vx = 0;
        break;
      case 'recover':
        this.vx *= 0.85;
        if (this.stateTime >= (this.def.boss ? 500 : 400)) {
          this.retreatUntil = this.def.behavior === 'lunger' ? this.now + 900 : 0;
          this.setState('idle');
        }
        break;
      case 'hurt':
        this.vx *= 0.86;
        this.vy = 0;
        if (this.now >= this.stunUntil) this.setState('idle');
        break;
      case 'knock':
        break;
      case 'down':
        this.vx *= 0.8;
        if (this.stateTime >= 700) this.setState('idle');
        break;
      case 'frozen':
        break;
      case 'dying':
        break;
    }
    const landed = this.physics(dt, xMin, xMax);
    if (landed) this.onLand();
  }

  private onLand() {
    this.scene.fx.dust(this.x, this.y, this.def.boss ? 8 : 3, this.def.boss ? 1.6 : 0.8);
    if (this.state === 'knock') {
      this.scene.fx.ring(this.x, this.y, 40, 0xffffff, this.y - 1);
      if (this.dead) this.die();
      else {
        this.vx = 0;
        this.setState(this.def.boss ? 'recover' : 'down');
      }
    } else if (this.state === 'slam') {
      this.scene.cameras.main.shake(180, 0.012);
      this.scene.fx.ring(this.x, this.y, 190, 0xff9fc0, this.y - 1);
      this.tryHitPlayer(170, 1.3, 400, 460, 500, true);
      this.setState('recover');
    } else if (this.state === 'idle' || this.state === 'walk') {
      this.vx = 0;
    }
  }

  /** Damage that never interrupts (poison ticks). */
  private applyDot(dmg: number, color: string) {
    this.hp -= dmg;
    this.scene.fx.number(this.x + Phaser.Math.Between(-10, 10), this.y - this.h - 20 - this.z, `${dmg}`, color);
    if (this.hp <= 0) {
      this.hp = 0;
      this.dead = true;
      this.frozenUntil = 0;
      this.vz = 420;
      this.z = Math.max(this.z, 1);
      this.vx = -this.facing * 220;
      this.setState('knock');
      this.scene.onEnemyKilled(this);
    }
  }

  private think(dt: number) {
    const p = this.player;
    if (p.dead || p.hidden) {
      // Nothing to chase: stand around (hidden players are invisible to enemies).
      this.vx = this.vy = 0;
      if (this.state !== 'idle') this.setState('idle');
      return;
    }
    const dx = p.x - this.x;
    const dy = p.y - this.y;
    const dist = Math.abs(dx);
    this.facing = dx >= 0 ? 1 : -1;
    const sp = this.def.speed;
    const canAttack = this.now >= this.attackReadyAt;
    const aligned = Math.abs(dy) < 36;

    switch (this.def.behavior) {
      case 'hopper':
        if (!this.airborne) {
          if (dist < this.def.attackRange && aligned && canAttack) { this.setState('windup'); return; }
          this.vz = 330;
          this.z = 1;
          this.vx = Math.sign(dx) * sp * (dist > 60 ? 1 : 0);
          this.vy = Phaser.Math.Clamp(dy, -1, 1) * sp * 0.5;
        }
        return;
      case 'melee':
        if (dist < this.def.attackRange && aligned && canAttack) { this.setState('windup'); return; }
        this.vx = dist > this.def.attackRange * 0.7 ? Math.sign(dx) * sp : 0;
        this.vy = Phaser.Math.Clamp(dy / 20, -1, 1) * sp * 0.7;
        break;
      case 'lunger':
        if (this.now < this.retreatUntil) {
          this.vx = -Math.sign(dx) * sp * 0.8;
          this.vy = Phaser.Math.Clamp(-dy / 20, -1, 1) * sp * 0.4;
          break;
        }
        if (dist < this.def.attackRange && dist > 90 && aligned && canAttack) { this.lungeDir = Math.sign(dx) || 1; this.setState('windup'); return; }
        this.vx = Math.sign(dx) * sp * (dist > 150 ? 1 : -0.6);
        this.vy = Phaser.Math.Clamp(dy / 20, -1, 1) * sp * 0.7;
        break;
      case 'bossSlime':
        if (canAttack && aligned) {
          if (dist < this.def.attackRange) { this.setState('windup'); return; }
          if (dist < 420 && Math.random() < 0.012) { this.startSlam(dx); return; }
        }
        this.vx = Math.sign(dx) * sp * (dist > 90 ? 1 : 0);
        this.vy = Phaser.Math.Clamp(dy / 20, -1, 1) * sp * 0.6;
        break;
    }
    const moving = Math.abs(this.vx) + Math.abs(this.vy) > 1;
    if (moving && this.state !== 'walk') this.setState('walk');
    if (!moving && this.state !== 'idle') this.setState('idle');
    void dt;
  }

  private startSlam(dx: number) {
    this.scene.fx.number(this.x, this.y - this.h - 40, '!', '#ffe066', true);
    this.vz = 820;
    this.z = 1;
    this.vx = Phaser.Math.Clamp(dx / 0.9, -520, 520);
    this.attackReadyAt = this.now + this.def.attackCd + 800;
    this.setState('slam');
  }

  private doAttack() {
    this.attackReadyAt = this.now + this.def.attackCd;
    if (this.def.behavior === 'lunger') { this.setState('lunge'); return; }
    this.setState('attack');
    this.scene.fx.slash(this.x + this.facing * 30, this.y - this.h * 0.5, this.facing, 0xffffff, 40 + this.def.w * 0.3, this.y);
  }

  private tryHitPlayer(range: number, powerMult: number, knock: number, launch: number, stun: number, circle = false) {
    const p = this.player;
    if (this.struck.has(p) || p.dead) return;
    const dx = p.x - this.x;
    const inRange = circle
      ? Phaser.Math.Distance.Between(this.x, this.y * 2, p.x, p.y * 2) < range
      : Math.sign(dx) === this.facing && Math.abs(dx) < range + p.w / 2 && Math.abs(p.y - this.y) < 50;
    if (!inRange || p.z > 90) return;
    this.struck.add(p);
    const dmg = Math.max(1, Math.round((this.def.atk * powerMult - p.d.def * 0.6) * (0.9 + Math.random() * 0.2)));
    p.takeHit({ damage: dmg, crit: false, mult: 1, element: this.def.element, dir: (Math.sign(dx) || 1) as 1 | -1, knock, launch, stun, source: this });
  }

  takeHit(h: HitInfo) {
    if (this.dead) return false;
    this.hp -= h.damage;
    this.hitCount = this.now - this.lastHitAt < 1500 ? this.hitCount + 1 : 1;
    this.lastHitAt = this.now;
    const fx = this.scene.fx;
    if (h.source === this.scene.player) this.scene.registerHit();
    sfx.play(h.crit ? 'crit' : h.launch > 0 ? 'hitHeavy' : 'hit');
    const color = h.crit ? '#ffe066' : h.mult > 1 ? '#ff9f43' : h.mult < 1 ? '#a0a0ff' : '#ffffff';
    fx.number(this.x, this.y - this.h - 30 - this.z, h.crit ? `${h.damage}!` : `${h.damage}`, color, h.crit || h.mult > 1);
    fx.spark(this.x, this.y - this.h / 2 - this.z, 0xffffff, h.launch > 0 ? 1.5 : 1);
    this.flash(0xffffff, 70);
    if (h.poison) {
      this.poisonDps = Math.max(this.poisonDps, h.poison.dps);
      this.poisonUntil = this.now + h.poison.duration;
      this.poisonNext = this.now + 500;
      fx.number(this.x, this.y - this.h - 60, '中毒', '#c47eff');
    }
    if (h.freeze) {
      this.frozenUntil = this.now + h.freeze;
      fx.number(this.x, this.y - this.h - 60, '凍結', '#9fe8ff');
      this.scene.time.delayedCall(80, () => this.restoreColors());
    }
    this.struck.clear();
    if (this.hp <= 0) {
      this.hp = 0;
      this.dead = true;
      this.frozenUntil = 0;
      this.vz = 480;
      this.z = Math.max(this.z, 1);
      this.vx = h.dir * 320;
      this.setState('knock');
      this.scene.onEnemyKilled(this);
      return true;
    }
    if (this.def.boss) {
      // Bosses have super armor except against launchers; they flinch briefly.
      if (h.launch > 0 && this.hitCount % 4 === 0) {
        this.vz = 300; this.z = Math.max(this.z, 1); this.vx = h.dir * 120; this.setState('knock');
      } else if (this.state !== 'windup' && this.state !== 'slam') {
        this.vx = h.dir * h.knock * 0.25;
        this.stunUntil = this.now + Math.min(h.stun, 220);
        this.setState('hurt');
      }
      return true;
    }
    if (h.launch > 0 || this.airborne || this.state === 'knock') {
      this.vz = Math.max(h.launch, 420);
      this.z = Math.max(this.z, 1);
      this.vx = h.dir * Math.max(200, h.knock * 0.7);
      this.setState('knock');
    } else {
      this.vx = h.dir * h.knock;
      this.stunUntil = this.now + h.stun;
      this.setState('hurt');
    }
    return true;
  }

  private die() {
    this.setState('dying');
    this.scene.fx.spark(this.x, this.y - this.h / 2, this.def.color, this.def.boss ? 3 : 1.6);
    this.scene.fx.dust(this.x, this.y, this.def.boss ? 14 : 6, this.def.boss ? 2 : 1);
    if (this.def.boss) { this.scene.fx.flash(0.7, 400); this.scene.cameras.main.shake(400, 0.012); }
    this.scene.tweens.add({ targets: this.view, alpha: 0, scaleY: 0.1, y: 10, duration: 260, onComplete: () => this.scene.removeEnemy(this) });
    this.scene.tweens.add({ targets: this.shadow, alpha: 0, duration: 260 });
  }

  syncView() {
    super.syncView();
    if (this.hpBar) this.hpBar.width = this.w * Math.max(0, this.hp / this.maxHp);
    const t = this.stateTime;
    let sx = 1, sy = 1, lean = 0;
    const b = this.def.behavior;
    const wob = Math.sin((this.now + this.phase) / (b === 'hopper' ? 260 : this.def.boss ? 520 : 380));
    switch (this.state) {
      case 'idle':
        if (b === 'hopper' || b === 'bossSlime') { sy = 1 + wob * 0.07; sx = 1 - wob * 0.05; }
        else if (b === 'lunger') { sy = 1 + wob * 0.03; lean = wob * 2; }
        else { sx = 1 + wob * 0.03; sy = 1 - wob * 0.02; lean = wob * 1.5; }
        break;
      case 'walk': sy = 1 + Math.sin(t / 90) * 0.05; sx = 1 - Math.sin(t / 90) * 0.04; lean = b === 'lunger' ? 6 + Math.sin(t / 90) * 3 : Math.sin(t / 90) * 4; break;
      case 'windup': sx = 1.15; sy = 0.85; lean = -8; break;
      case 'attack': case 'lunge': lean = 14; sx = 1.1; break;
      case 'slam': sy = 1.25; sx = 0.85; break;
      case 'hurt': lean = -10; sx = 0.92; break;
      case 'knock': lean = -60; break;
      case 'down': lean = -90; sy = 0.6; break;
      case 'frozen': lean = 0; break;
    }
    if (this.airborne && this.state !== 'knock' && this.state !== 'slam') { sy = 1.1; sx = 0.92; }
    this.body.setScale(this.baseScale * sx, this.baseScale * sy);
    this.view.angle = lean * this.facing;
    if (!this.frozen) {
      const weak = this.now < this.defDownUntil;
      const poisoned = this.now < this.poisonUntil;
      const tint = weak ? 0xffb0b0 : poisoned ? 0xc9a0ff : this.def.tint ?? 0;
      if (tint && (this.body.tintTopLeft !== tint || this.body.tintFill)) this.body.setTint(tint);
      else if (!tint && this.body.isTinted) this.body.clearTint();
    }
  }
}

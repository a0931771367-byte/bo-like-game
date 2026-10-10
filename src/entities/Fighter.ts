import Phaser from 'phaser';
import { GRAVITY, GROUND_BOTTOM, GROUND_TOP } from '../config';
import type { Element } from '../data/types';
import type { GameScene } from '../scenes/GameScene';

export type Team = 'player' | 'enemy';

export interface HitInfo {
  damage: number;
  crit: boolean;
  mult: number;
  element: Element;
  dir: 1 | -1;
  knock: number;
  launch: number;
  stun: number;
  freeze?: number;
  /** Damage over time: dps for duration ms. */
  poison?: { dps: number; duration: number };
  source: Fighter;
}

export abstract class Fighter {
  x: number;
  y: number;
  z = 0;
  vx = 0;
  vy = 0;
  vz = 0;
  facing: 1 | -1 = 1;
  hp = 1;
  maxHp = 1;
  state = 'idle';
  stateTime = 0;
  invulnUntil = 0;
  superArmorUntil = 0;
  frozenUntil = 0;
  stunUntil = 0;
  dead = false;
  /** Targets already struck by the current attack. */
  struck = new Set<Fighter>();
  readonly root: Phaser.GameObjects.Container;
  readonly view: Phaser.GameObjects.Container;
  readonly shadow: Phaser.GameObjects.Ellipse;
  abstract readonly team: Team;

  constructor(public scene: GameScene, x: number, y: number, public w: number, public h: number) {
    this.x = x;
    this.y = y;
    this.shadow = scene.add.ellipse(0, 0, w * 1.1, Math.max(12, w * 0.28), 0x000000, 0.28);
    this.view = scene.add.container(0, 0);
    this.root = scene.add.container(x, y, [this.shadow, this.view]);
  }

  get now() { return this.scene.time.now; }
  get airborne() { return this.z > 0.5 || this.vz > 0; }
  get frozen() { return this.now < this.frozenUntil; }

  setState(s: string) {
    this.state = s;
    this.stateTime = 0;
  }

  /** Integrates motion. Returns true on the frame the fighter lands. */
  physics(dt: number, xMin: number, xMax: number) {
    let landed = false;
    if (this.airborne) {
      this.vz -= GRAVITY * dt;
      this.z += this.vz * dt;
      if (this.z <= 0) {
        this.z = 0;
        this.vz = 0;
        landed = true;
      }
    }
    this.x = Phaser.Math.Clamp(this.x + this.vx * dt, xMin, xMax);
    this.y = Phaser.Math.Clamp(this.y + this.vy * dt, GROUND_TOP, GROUND_BOTTOM);
    return landed;
  }

  syncView() {
    this.root.setPosition(this.x, this.y);
    this.root.setDepth(this.y);
    this.view.y = -this.z;
    this.view.scaleX = this.facing;
    const s = Math.max(0.45, 1 - this.z / 400);
    this.shadow.setScale(s);
  }

  flash(color = 0xffffff, ms = 80) {
    this.eachVisual((o) => {
      if (o instanceof Phaser.GameObjects.Image) o.setTintFill(color);
      else {
        if (o.getData('base') === undefined) o.setData('base', o.fillColor);
        o.setFillStyle(color, o.fillAlpha);
      }
    });
    this.scene.time.delayedCall(ms, () => this.restoreColors());
  }

  restoreColors() {
    if (this.dead && !this.root.active) return;
    this.eachVisual((o) => {
      if (o instanceof Phaser.GameObjects.Image) {
        if (this.frozen) o.setTint(0x9fe8ff); else o.clearTint();
        return;
      }
      const base = o.getData('base') as number | undefined;
      if (base === undefined) return;
      o.setFillStyle(this.frozen ? 0x9fe8ff : base, o.fillAlpha);
    });
  }

  private eachVisual(fn: (o: Phaser.GameObjects.Image | Phaser.GameObjects.Shape) => void) {
    if (!this.view.active) return;
    this.view.each((o: Phaser.GameObjects.GameObject) => {
      if (!o.active || o.getData('noflash')) return;
      if (o instanceof Phaser.GameObjects.Image) fn(o);
      else if ((o as Phaser.GameObjects.Shape).isFilled !== undefined) fn(o as Phaser.GameObjects.Shape);
    });
  }

  destroy() {
    this.root.destroy();
  }

  abstract update(dt: number): void;
  abstract takeHit(h: HitInfo): boolean;
}

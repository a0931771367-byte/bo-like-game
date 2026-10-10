import Phaser from 'phaser';
import type { GameScene } from '../scenes/GameScene';
import type { Fighter, HitInfo } from './Fighter';

export interface ProjectileOpts {
  owner: Fighter; x: number; y: number; z: number; dir: 1 | -1; speed: number; color: number; range: number;
  make: (target: Fighter) => HitInfo;
}

export class Projectile {
  readonly g: Phaser.GameObjects.Container;
  travelled = 0;
  done = false;
  constructor(private scene: GameScene, public o: ProjectileOpts) {
    const core = scene.add.ellipse(0, 0, 34, 18, o.color);
    const glow = scene.add.ellipse(-10, 0, 46, 24, o.color, 0.35);
    const hot = scene.add.ellipse(6, 0, 16, 8, 0xffffff, 0.9);
    this.g = scene.add.container(o.x, o.y - o.z, [glow, core, hot]).setDepth(o.y + 1).setScale(o.dir, 1);
  }
  update(dt: number, targets: Fighter[]) {
    const step = this.o.speed * dt;
    this.o.x += this.o.dir * step;
    this.travelled += step;
    this.g.x = this.o.x;
    this.g.y = this.o.y - this.o.z + Math.sin(this.travelled / 40) * 3;
    for (const t of targets) {
      if (t.dead || Math.abs(t.y - this.o.y) > 50) continue;
      if (Math.abs(t.x - this.o.x) < t.w / 2 + 14 && t.z < this.o.z + 40) {
        t.takeHit(this.o.make(t));
        this.scene.hitStop(40);
        this.finish();
        return;
      }
    }
    if (this.travelled > this.o.range) this.finish();
  }
  private finish() {
    this.done = true;
    this.scene.fx.spark(this.o.x, this.o.y - this.o.z, this.o.color);
    this.g.destroy();
  }
}

export interface WallOpts {
  owner: Fighter; x: number; y: number; duration: number; color: number;
  make: (target: Fighter) => HitInfo;
}

export class Wall {
  readonly g: Phaser.GameObjects.Container;
  private until: number;
  private lastTick = new Map<Fighter, number>();
  done = false;
  constructor(private scene: GameScene, public o: WallOpts) {
    this.until = scene.time.now + o.duration;
    const flames: Phaser.GameObjects.GameObject[] = [];
    for (let i = -2; i <= 2; i++) {
      const f = scene.add.triangle(i * 20, 0, 0, 0, 30, -110 - Math.abs(i) * -20, 60, 0, o.color, 0.9).setOrigin(0.5, 1);
      flames.push(f);
      scene.tweens.add({ targets: f, scaleY: 0.75, duration: 180 + i * 30, yoyo: true, repeat: -1 });
    }
    this.g = scene.add.container(o.x, o.y, flames).setDepth(o.y);
    scene.fx.ring(o.x, o.y, 90, o.color, o.y - 1);
  }
  update(targets: Fighter[]) {
    const now = this.scene.time.now;
    if (now > this.until) { this.done = true; this.scene.tweens.add({ targets: this.g, alpha: 0, duration: 200, onComplete: () => this.g.destroy() }); return; }
    for (const t of targets) {
      if (t.dead || Math.abs(t.x - this.o.x) > 45 + t.w / 2 || Math.abs(t.y - this.o.y) > 80 || t.z > 60) continue;
      const last = this.lastTick.get(t) ?? 0;
      if (now - last < 350) continue;
      this.lastTick.set(t, now);
      const h = this.o.make(t);
      h.dir = (Math.sign(t.x - this.o.x) || 1) as 1 | -1;
      t.takeHit(h);
    }
  }
}

import Phaser from 'phaser';
import { FONT } from '../config';

const TOP = 100000;

/** Short-lived visual effects in world space. */
export class Fx {
  constructor(private scene: Phaser.Scene) {}

  number(x: number, y: number, value: string, color: string, big = false) {
    const t = this.scene.add.text(x + Phaser.Math.Between(-10, 10), y, value, {
      fontFamily: FONT, fontSize: big ? '38px' : '26px', fontStyle: 'bold', color, stroke: '#1a1020', strokeThickness: 5,
    }).setOrigin(0.5).setDepth(TOP + 2);
    if (big) t.setScale(1.4);
    this.scene.tweens.add({ targets: t, scale: 1, duration: 120 });
    this.scene.tweens.add({ targets: t, y: y - 70, alpha: 0, delay: 250, duration: 650, ease: 'Cubic.easeOut', onComplete: () => t.destroy() });
  }

  spark(x: number, y: number, color = 0xffffff, size = 1) {
    for (let i = 0; i < 7; i++) {
      const a = Math.random() * Math.PI * 2;
      const r = this.scene.add.rectangle(x, y, 10 * size, 3 * size, color).setDepth(TOP).setRotation(a);
      this.scene.tweens.add({
        targets: r, x: x + Math.cos(a) * 55 * size, y: y + Math.sin(a) * 55 * size, alpha: 0, scaleX: 0.2,
        duration: 220, ease: 'Cubic.easeOut', onComplete: () => r.destroy(),
      });
    }
    const c = this.scene.add.circle(x, y, 16 * size, 0xffffff, 0.9).setDepth(TOP);
    this.scene.tweens.add({ targets: c, scale: 1.8, alpha: 0, duration: 140, onComplete: () => c.destroy() });
  }

  slash(x: number, y: number, facing: number, color: number, radius: number, depth: number) {
    const g = this.scene.add.graphics().setDepth(depth + 1);
    g.lineStyle(10, color, 0.9);
    const start = facing > 0 ? -1.2 : Math.PI + 1.2;
    const end = facing > 0 ? 0.9 : Math.PI - 0.9;
    g.beginPath();
    g.arc(x, y, radius, start, end, facing < 0);
    g.strokePath();
    this.scene.tweens.add({ targets: g, alpha: 0, duration: 160, onComplete: () => g.destroy() });
  }

  ring(x: number, y: number, radius: number, color: number, depth: number) {
    const e = this.scene.add.ellipse(x, y, 20, 9, color, 0.55).setDepth(depth).setStrokeStyle(6, color, 1);
    this.scene.tweens.add({ targets: e, width: radius * 2, height: radius * 0.9, alpha: 0, duration: 360, ease: 'Cubic.easeOut', onComplete: () => e.destroy() });
  }

  pillar(x: number, y: number, color: number) {
    const r = this.scene.add.rectangle(x, y, 26, 520, color, 0.9).setOrigin(0.5, 1).setDepth(y + 2);
    const glow = this.scene.add.ellipse(x, y, 140, 46, color, 0.6).setDepth(y + 1);
    this.scene.tweens.add({ targets: [r, glow], alpha: 0, scaleX: 0.3, duration: 260, onComplete: () => { r.destroy(); glow.destroy(); } });
  }

  aura(x: number, y: number, color: number) {
    for (let i = 0; i < 10; i++) {
      const p = this.scene.add.rectangle(x + Phaser.Math.Between(-30, 30), y - Phaser.Math.Between(0, 30), 6, 14, color, 0.9).setDepth(TOP);
      this.scene.tweens.add({ targets: p, y: p.y - 90, alpha: 0, delay: i * 30, duration: 500, onComplete: () => p.destroy() });
    }
  }

  banner(text: string, color = '#ffffff', size = 64) {
    const cam = this.scene.cameras.main;
    const t = this.scene.add.text(cam.width / 2, cam.height * 0.36, text, {
      fontFamily: FONT, fontSize: `${size}px`, fontStyle: 'bold', color, stroke: '#140c1c', strokeThickness: 8,
    }).setOrigin(0.5).setScrollFactor(0).setDepth(TOP + 10).setScale(0.4).setAlpha(0);
    this.scene.tweens.add({ targets: t, scale: 1, alpha: 1, duration: 220, ease: 'Back.easeOut' });
    this.scene.tweens.add({ targets: t, alpha: 0, delay: 1300, duration: 400, onComplete: () => t.destroy() });
  }
}

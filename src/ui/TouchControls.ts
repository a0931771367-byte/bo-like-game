import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import type { Player } from '../entities/Player';
import { controls, type Action } from '../systems/Controls';

const DEPTH = 300000;

/** Landscape on-screen controls: left stick, right action cluster, skill row. */
export class TouchControls {
  private stickBase: Phaser.GameObjects.Arc;
  private stickKnob: Phaser.GameObjects.Arc;
  private stickPointer: Phaser.Input.Pointer | null = null;
  private stickOrigin = new Phaser.Math.Vector2();
  private guardPointer: Phaser.Input.Pointer | null = null;
  private skillBtns: { cover: Phaser.GameObjects.Rectangle; label: Phaser.GameObjects.Image; skill: number }[] = [];

  constructor(scene: Phaser.Scene, private player: Player) {
    const add = scene.add;
    scene.input.addPointer(3);
    const fixed = <T extends Phaser.GameObjects.Components.ScrollFactor & Phaser.GameObjects.Components.Depth>(o: T) => o.setScrollFactor(0).setDepth(DEPTH);

    // Stick zone (left 40% of the screen)
    this.stickOrigin.set(180, H - 170);
    this.stickBase = fixed(add.circle(this.stickOrigin.x, this.stickOrigin.y, 78, 0xffffff, 0.12).setStrokeStyle(3, 0xffffff, 0.35));
    this.stickKnob = fixed(add.circle(this.stickOrigin.x, this.stickOrigin.y, 38, 0xffffff, 0.35));
    const zone = add.zone(0, 120, W * 0.42, H - 120).setOrigin(0).setScrollFactor(0).setInteractive().setDepth(DEPTH - 1);
    zone.on('pointerdown', (p: Phaser.Input.Pointer) => {
      this.stickPointer = p;
      this.stickOrigin.set(p.x, p.y);
      this.stickBase.setPosition(p.x, p.y).setAlpha(1);
      this.stickKnob.setPosition(p.x, p.y);
    });
    scene.input.on('pointerup', (p: Phaser.Input.Pointer) => {
      if (p === this.stickPointer) { this.stickPointer = null; controls.touchX = controls.touchY = 0; this.stickKnob.setPosition(this.stickOrigin.x, this.stickOrigin.y); this.stickBase.setAlpha(0.7); }
      if (p === this.guardPointer) { this.guardPointer = null; controls.touchGuard = false; }
    });

    // Action buttons (right)
    const mk = (x: number, y: number, r: number, color: number, text: string, action: Action | 'guard', size = 22) => {
      const c = fixed(add.circle(x, y, r, color, 0.55).setStrokeStyle(3, 0xffffff, 0.6)).setInteractive();
      const t = fixed(add.text(x, y, text, { fontFamily: FONT, fontSize: `${size}px`, fontStyle: 'bold', color: '#fff', stroke: '#000', strokeThickness: 3 }).setOrigin(0.5));
      c.on('pointerdown', (p: Phaser.Input.Pointer) => {
        c.setAlpha(1);
        if (action === 'guard') { this.guardPointer = p; controls.touchGuard = true; }
        else controls.press(action);
      });
      c.on('pointerup', () => c.setAlpha(0.7));
      c.on('pointerout', () => c.setAlpha(0.7));
      c.setAlpha(0.7);
      return { c, t };
    };
    mk(W - 130, H - 150, 62, 0xff5a5a, '攻擊', 'attack', 26);
    mk(W - 260, H - 90, 46, 0x5db2ff, '跳躍', 'jump');
    mk(W - 250, H - 215, 46, 0x9be15d, '防禦', 'guard');

    // Skills (right column above actions)
    const cls = player.cls;
    cls.skills.forEach((s, i) => {
      const bx = W - 150 + (i % 2) * 90;
      const by = 150 + Math.floor(i / 2) * 90;
      const bg = fixed(add.rectangle(bx, by, 76, 76, 0x000000, 0.35).setStrokeStyle(2, 0xffffff, 0.6)).setInteractive();
      const label = fixed(add.image(bx, by, `icon_${s.id}`).setDisplaySize(72, 72));
      const cover = fixed(add.rectangle(bx - 38, by + 38, 76, 0, 0x000000, 0.65).setOrigin(0, 1));
      bg.on('pointerdown', () => controls.press(`skill${i}` as Action));
      this.skillBtns.push({ cover, label, skill: i });
    });

    // Potions (top right, below HUD text)
    const pot = (x: number, color: number, text: string, action: Action) => {
      const b = fixed(add.rectangle(x, 70, 90, 36, color, 0.6).setStrokeStyle(2, 0xffffff, 0.5)).setInteractive();
      fixed(add.text(x, 70, text, { fontFamily: FONT, fontSize: '15px', color: '#fff', stroke: '#000', strokeThickness: 2 }).setOrigin(0.5));
      b.on('pointerdown', () => controls.press(action));
    };
    pot(W - 170, 0xff5d5d, '紅藥水', 'pot0');
    pot(W - 70, 0x5d8bff, '藍藥水', 'pot1');

    // Pause
    const pause = fixed(add.rectangle(W / 2, 28, 110, 40, 0x000000, 0.5).setStrokeStyle(2, 0xffffff, 0.5)).setInteractive();
    fixed(add.text(W / 2, 28, '選單', { fontFamily: FONT, fontSize: '18px', color: '#fff' }).setOrigin(0.5));
    pause.on('pointerdown', () => controls.press('menu'));
  }

  update() {
    if (this.stickPointer && this.stickPointer.isDown) {
      const dx = this.stickPointer.x - this.stickOrigin.x;
      const dy = this.stickPointer.y - this.stickOrigin.y;
      const len = Math.hypot(dx, dy);
      const max = 70;
      const k = len > max ? max / len : 1;
      this.stickKnob.setPosition(this.stickOrigin.x + dx * k, this.stickOrigin.y + dy * k);
      const dead = 14;
      controls.touchX = Math.abs(dx) > dead ? Phaser.Math.Clamp(dx / 50, -1, 1) : 0;
      controls.touchY = Math.abs(dy) > dead ? Phaser.Math.Clamp(dy / 50, -1, 1) : 0;
    }
    const p = this.player;
    for (const b of this.skillBtns) {
      const s = p.cls.skills[b.skill];
      const left = p.cooldownLeft(s);
      b.cover.height = left > 0 ? 76 * Math.min(1, left / (s.cooldown * p.d.castMult)) : 0;
      b.label.setAlpha(p.skillLv(s) > 0 ? 1 : 0.35);
    }
  }
}

export const isTouchDevice = () => window.matchMedia('(pointer: coarse)').matches || navigator.maxTouchPoints > 0 && !window.matchMedia('(pointer: fine)').matches;

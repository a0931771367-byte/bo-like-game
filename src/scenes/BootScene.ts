import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { ANIMS, animKey } from '../data/anims';
import { CLASSES } from '../data/classes';
import { ENEMIES } from '../data/enemies';

/** Loads every image once, with a simple progress bar, then opens the title. */
export class BootScene extends Phaser.Scene {
  constructor() { super('boot'); }

  preload() {
    this.cameras.main.setBackgroundColor('#0b0d14');
    const bar = this.add.rectangle(W / 2 - 200, H / 2, 0, 14, 0xffd34d).setOrigin(0, 0.5);
    this.add.rectangle(W / 2, H / 2, 404, 18).setStrokeStyle(2, 0xffffff, 0.6);
    const text = this.add.text(W / 2, H / 2 - 40, '載入中…', { fontFamily: FONT, fontSize: '22px', color: '#ffffff' }).setOrigin(0.5);
    this.load.on('progress', (v: number) => { bar.width = 400 * v; });
    this.load.on('complete', () => text.setText('完成'));

    this.load.setPath('');
    for (const c of CLASSES) {
      this.load.image(`${c.id}_side`, `sprites/${c.id}_side.png`);
      this.load.image(`${c.id}_front`, `sprites/${c.id}_front.png`);
      c.skills.forEach((s, i) => this.load.image(`icon_${s.id}`, `icons/skill_${c.id}_${i}.png`));
      const anim = ANIMS[c.id];
      if (anim) {
        for (const label of ['idle', 'walk', 'attack'] as const) {
          for (let i = 0; i < anim[label]; i++) this.load.image(animKey(c.id, label, i), `sprites/anim/${c.id}_${label}_${i}.png`);
        }
      }
    }
    for (const key of new Set(Object.values(ENEMIES).map((e) => e.sprite ?? e.id))) this.load.image(`${key}_side`, `sprites/${key}_side.png`);
    this.load.image('bg_chapter1', 'bg/chapter1.jpg');
    this.load.image('bg_chapter2', 'bg/chapter2.jpg');
    this.load.image('title_keyart', 'bg/title.jpg');
  }

  create() {
    // Skills without a rendered icon yet get a generated placeholder (class colour + 2-char label).
    for (const c of CLASSES) {
      for (const s of c.skills) {
        const key = `icon_${s.id}`;
        if (this.textures.exists(key)) continue;
        const tex = this.textures.createCanvas(key, 128, 128);
        if (!tex) continue;
        const ctx = tex.getContext();
        const hex = (n: number) => `#${n.toString(16).padStart(6, '0')}`;
        ctx.fillStyle = hex(c.color);
        ctx.fillRect(0, 0, 128, 128);
        ctx.fillStyle = hex(s.color);
        ctx.beginPath();
        ctx.arc(64, 64, 44, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = '#ffffff';
        ctx.strokeStyle = '#000000';
        ctx.lineWidth = 6;
        ctx.font = `bold 44px ${FONT}`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.strokeText(s.short, 64, 68);
        ctx.fillText(s.short, 64, 68);
        tex.refresh();
      }
    }
    this.scene.start('title');
  }
}

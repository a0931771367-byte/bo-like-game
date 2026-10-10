import Phaser from 'phaser';
import { FONT, H, W } from '../config';
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
    }
    for (const e of Object.values(ENEMIES)) this.load.image(`${e.id}_side`, `sprites/${e.id}_side.png`);
    this.load.image('bg_chapter1', 'bg/chapter1.jpg');
    this.load.image('title_keyart', 'bg/title.jpg');
  }

  create() {
    this.scene.start('title');
  }
}

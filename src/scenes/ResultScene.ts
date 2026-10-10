import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { getClass } from '../data/classes';
import { progressFor, session } from '../systems/Save';

export class ResultScene extends Phaser.Scene {
  private win = false;
  constructor() { super('result'); }
  init(d: { win: boolean }) { this.win = d.win; }
  create() {
    const cls = getClass(session.classId);
    const p = progressFor(cls);
    this.add.rectangle(W / 2, H / 2, W, H, this.win ? 0x1b2a3a : 0x2a1016);
    this.add.text(W / 2, 200, this.win ? '關卡完成！' : '挑戰失敗', { fontFamily: FONT, fontSize: '72px', fontStyle: 'bold', color: this.win ? '#ffe066' : '#ff8080', stroke: '#000', strokeThickness: 8 }).setOrigin(0.5);
    this.add.text(W / 2, 300, `${cls.name} Lv.${p.level}    金幣 ${session.save.gold} G`, { fontFamily: FONT, fontSize: '26px', color: '#fff' }).setOrigin(0.5);
    this.add.text(W / 2, 350, this.win ? '原型只做到第 1 章第 1 關，之後章節會在第 3 步補上。' : '已獲得的經驗值保留一半，加點後再試一次吧。', { fontFamily: FONT, fontSize: '18px', color: '#cfc8e8' }).setOrigin(0.5);
    const btn = (x: number, t: string, cb: () => void) => {
      const r = this.add.rectangle(x, 480, 300, 60, 0xffffff, 0.15).setStrokeStyle(2, 0xffffff, 0.6).setInteractive({ useHandCursor: true });
      this.add.text(x, 480, t, { fontFamily: FONT, fontSize: '24px', color: '#fff' }).setOrigin(0.5);
      r.on('pointerdown', cb);
    };
    btn(W / 2 - 170, '加點 / 再玩一次', () => this.scene.start('levelup', { from: 'title' }));
    btn(W / 2 + 170, '回標題', () => this.scene.start('title'));
  }
}

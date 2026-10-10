import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { getClass } from '../data/classes';
import { STAGES, nextStage } from '../data/stages';
import { progressFor, session, writeSave } from '../systems/Save';

export class ResultScene extends Phaser.Scene {
  private win = false;
  constructor() { super('result'); }
  init(d: { win: boolean }) { this.win = d.win; }
  create() {
    const cls = getClass(session.classId);
    const p = progressFor(cls);
    this.cameras.main.fadeIn(300);
    this.add.rectangle(W / 2, H / 2, W, H, this.win ? 0x1b2a3a : 0x2a1016);
    this.add.text(W / 2, 200, this.win ? '關卡完成！' : '挑戰失敗', { fontFamily: FONT, fontSize: '72px', fontStyle: 'bold', color: this.win ? '#ffe066' : '#ff8080', stroke: '#000', strokeThickness: 8 }).setOrigin(0.5);
    this.add.text(W / 2, 300, `${cls.name} Lv.${p.level}    金幣 ${session.save.gold} G`, { fontFamily: FONT, fontSize: '26px', color: '#fff' }).setOrigin(0.5);
    const next = this.win ? nextStage(session.stageId) : undefined;
    const stage = STAGES[session.stageId];
    const msg = !this.win ? '已獲得的經驗值保留一半，加點後再試一次吧。'
      : next ? `${stage.chapter} ${stage.name} 完成！下一關：${STAGES[next].name}` : '目前的關卡都打完了，之後的章節陸續加入。';
    this.add.text(W / 2, 350, msg, { fontFamily: FONT, fontSize: '18px', color: '#cfc8e8' }).setOrigin(0.5);
    const btn = (x: number, t: string, cb: () => void, color = 0xffffff) => {
      const r = this.add.rectangle(x, 480, 280, 60, color, 0.2).setStrokeStyle(2, 0xffffff, 0.6).setInteractive({ useHandCursor: true });
      this.add.text(x, 480, t, { fontFamily: FONT, fontSize: '24px', color: '#fff' }).setOrigin(0.5);
      r.on('pointerdown', cb);
    };
    if (next) btn(W / 2 - 300, '下一關', () => { session.stageId = next; writeSave(session.save); this.scene.start('dialogue', { stageId: next, phase: 'before', next: 'game' }); }, 0x4a7bd1);
    btn(next ? W / 2 : W / 2 - 150, this.win ? '加點 / 選關' : '加點 / 再玩一次', () => this.scene.start('levelup', { from: 'title' }));
    btn(next ? W / 2 + 300 : W / 2 + 150, '回標題', () => this.scene.start('title'));
  }
}

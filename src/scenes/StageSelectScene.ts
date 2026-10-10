import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { getClass } from '../data/classes';
import { STAGES, STAGE_ORDER, stageUnlocked } from '../data/stages';
import { progressFor, session, writeSave } from '../systems/Save';

/** Pick a stage; locked ones need the previous stage cleared. */
export class StageSelectScene extends Phaser.Scene {
  constructor() { super('stageselect'); }

  create() {
    const cls = getClass(session.classId);
    const p = progressFor(cls);
    this.cameras.main.fadeIn(300);
    this.add.image(W / 2, H / 2, 'title_keyart').setDisplaySize(W, H).setAlpha(0.35);
    this.add.rectangle(W / 2, H / 2, W, H, 0x0b0d14, 0.55);
    const txt = { fontFamily: FONT, color: '#ffffff', stroke: '#000', strokeThickness: 4 };
    this.add.text(W / 2, 60, '選擇關卡', { ...txt, fontSize: '44px', fontStyle: 'bold' }).setOrigin(0.5);
    this.add.text(W / 2, 110, `${cls.name} Lv.${p.level}    金幣 ${session.save.gold} G`, { ...txt, fontSize: '20px', color: '#ffd34d' }).setOrigin(0.5);

    const cardW = 360, cardH = 230, gap = 24, perRow = 3;
    const startX = W / 2 - (perRow * (cardW + gap) - gap) / 2 + cardW / 2;
    const cleared = session.save.cleared;
    STAGE_ORDER.forEach((id, i) => {
      const st = STAGES[id];
      const x = startX + (i % perRow) * (cardW + gap), y = 270 + Math.floor(i / perRow) * (cardH + gap);
      const open = stageUnlocked(id, cleared);
      const done = cleared.includes(id);
      const card = this.add.rectangle(x, y, cardW, cardH, open ? 0x000000 : 0x151515, open ? 0.5 : 0.75)
        .setStrokeStyle(3, done ? 0xffd34d : open ? 0x8fb8ff : 0x444444, 1);
      this.add.text(x, y - 90, `${st.chapter}  第 ${(i % perRow) + 1} 關`, { ...txt, fontSize: '16px', color: open ? '#9fd0ff' : '#777777' }).setOrigin(0.5);
      this.add.text(x, y - 58, st.name, { ...txt, fontSize: '28px', fontStyle: 'bold', color: open ? '#ffffff' : '#777777' }).setOrigin(0.5);
      this.add.text(x, y - 28, open ? st.desc : '先通過上一關才能解鎖', { fontFamily: FONT, fontSize: '14px', color: open ? '#dcd6f0' : '#777777', wordWrap: { width: cardW - 40 }, align: 'center' }).setOrigin(0.5, 0);
      if (done) this.add.text(x + cardW / 2 - 16, y - cardH / 2 + 14, '✓', { ...txt, fontSize: '20px', color: '#ffd34d' }).setOrigin(1, 0);
      else if (!open) this.add.text(x, y + 60, '🔒', { fontFamily: FONT, fontSize: '30px' }).setOrigin(0.5);
      if (open) {
        const go = this.add.rectangle(x, y + 75, 180, 42, 0x4a7bd1, 0.9).setStrokeStyle(2, 0xffffff, 0.7).setInteractive({ useHandCursor: true });
        this.add.text(x, y + 75, done ? '再挑戰' : '出發', { ...txt, fontSize: '20px' }).setOrigin(0.5);
        const start = () => {
          session.stageId = id;
          writeSave(session.save);
          this.cameras.main.fadeOut(250);
          this.time.delayedCall(260, () => this.scene.start('dialogue', { stageId: id, phase: 'before', next: 'game' }));
        };
        go.on('pointerdown', start);
        card.setInteractive({ useHandCursor: true }).on('pointerdown', start);
      }
    });

    const back = this.add.rectangle(W / 2, H - 60, 240, 50, 0x555a70, 0.9).setStrokeStyle(2, 0xffffff, 0.6).setInteractive({ useHandCursor: true });
    this.add.text(W / 2, H - 60, '← 回加點畫面', { ...txt, fontSize: '20px' }).setOrigin(0.5);
    back.on('pointerdown', () => this.scene.start('levelup', { from: 'title' }));
  }
}

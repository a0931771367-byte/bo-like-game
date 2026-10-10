import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { CLASSES } from '../data/classes';
import { exportCode, importCode, progressFor, session, writeSave } from '../systems/Save';

const btnStyle = { fontFamily: FONT, fontSize: '22px', color: '#ffffff', stroke: '#000', strokeThickness: 3 };

export class TitleScene extends Phaser.Scene {
  constructor() { super('title'); }

  create() {
    const g = this.add.graphics();
    g.fillGradientStyle(0x1b1f3a, 0x1b1f3a, 0x3a2a55, 0x3a2a55, 1);
    g.fillRect(0, 0, W, H);
    for (let i = 0; i < 60; i++) this.add.circle(Phaser.Math.Between(0, W), Phaser.Math.Between(0, H * 0.6), Phaser.Math.Between(1, 3), 0xffffff, Phaser.Math.FloatBetween(0.3, 0.9));

    this.add.text(W / 2, 90, '劍與魔法亂鬥', { fontFamily: FONT, fontSize: '72px', fontStyle: 'bold', color: '#ffe9a8', stroke: '#2a1a40', strokeThickness: 10 }).setOrigin(0.5);
    this.add.text(W / 2, 150, '原型版 v0.1  ·  暫定名稱', { fontFamily: FONT, fontSize: '20px', color: '#cfc8e8' }).setOrigin(0.5);
    this.add.text(W / 2, 200, '選擇職業', { fontFamily: FONT, fontSize: '26px', color: '#ffffff' }).setOrigin(0.5);

    const cardW = 180, gap = 20;
    const startX = W / 2 - (CLASSES.length * (cardW + gap) - gap) / 2 + cardW / 2;
    CLASSES.forEach((c, i) => {
      const x = startX + i * (cardW + gap);
      const y = 360;
      const card = this.add.rectangle(x, y, cardW, 240, c.playable ? 0x000000 : 0x111111, c.playable ? 0.45 : 0.6)
        .setStrokeStyle(3, c.playable ? c.color : 0x444444, 1);
      // mannequin
      this.add.rectangle(x, y - 10, 36, 44, c.playable ? c.color : 0x555555);
      this.add.circle(x, y - 48, 18, c.playable ? 0xf5d0b0 : 0x777777);
      this.add.rectangle(x - 4, y - 62, 36, 12, c.playable ? c.accent : 0x666666);
      this.add.rectangle(x, y + 30, 30, 30, c.playable ? 0x2b2d42 : 0x444444);
      this.add.text(x, y + 68, c.name, { ...btnStyle, fontSize: '26px', fontStyle: 'bold', color: c.playable ? '#ffffff' : '#888888' }).setOrigin(0.5);
      this.add.text(x, y + 98, c.playable ? c.role : '第 3 步製作', { fontFamily: FONT, fontSize: '13px', color: c.playable ? '#dcd6f0' : '#777777', wordWrap: { width: cardW - 20 }, align: 'center' }).setOrigin(0.5, 0);
      if (c.playable) {
        const p = progressFor(c);
        this.add.text(x, y - 105, `Lv.${p.level}`, { ...btnStyle, fontSize: '16px', color: '#ffd34d' }).setOrigin(0.5);
        card.setInteractive({ useHandCursor: true });
        card.on('pointerover', () => card.setFillStyle(c.color, 0.35));
        card.on('pointerout', () => card.setFillStyle(0x000000, 0.45));
        card.on('pointerdown', () => {
          session.classId = c.id;
          session.stageId = '1-1';
          writeSave(session.save);
          this.cameras.main.fadeOut(300);
          this.time.delayedCall(320, () => this.scene.start('levelup', { from: 'title' }));
        });
      }
    });

    this.add.text(W / 2, 560, '點選職業開始 → 會先進入加點畫面，再進入第 1 章第 1 關', { fontFamily: FONT, fontSize: '18px', color: '#cfc8e8' }).setOrigin(0.5);

    this.button(W / 2 - 170, 630, '匯出存檔碼', () => {
      const code = exportCode(session.save);
      navigator.clipboard?.writeText(code).catch(() => undefined);
      window.prompt('存檔碼（已嘗試複製到剪貼簿）', code);
    });
    this.button(W / 2 + 170, 630, '匯入存檔碼', () => {
      const code = window.prompt('貼上存檔碼');
      if (!code) return;
      const s = importCode(code);
      if (!s) { window.alert('存檔碼無效'); return; }
      session.save = s;
      writeSave(s);
      this.scene.restart();
    });
    this.add.text(W / 2, 690, '存檔會自動保存在這個瀏覽器；換裝置或清除資料前請先匯出存檔碼', { fontFamily: FONT, fontSize: '14px', color: '#9a93b8' }).setOrigin(0.5);
  }

  private button(x: number, y: number, text: string, onClick: () => void) {
    const r = this.add.rectangle(x, y, 260, 48, 0xffffff, 0.12).setStrokeStyle(2, 0xffffff, 0.5).setInteractive({ useHandCursor: true });
    this.add.text(x, y, text, btnStyle).setOrigin(0.5);
    r.on('pointerover', () => r.setFillStyle(0xffffff, 0.25));
    r.on('pointerout', () => r.setFillStyle(0xffffff, 0.12));
    r.on('pointerdown', onClick);
  }
}

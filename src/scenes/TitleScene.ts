import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { CLASSES } from '../data/classes';
import { exportCode, importCode, progressFor, session, writeSave } from '../systems/Save';
import { sfx } from '../systems/Sfx';

const btnStyle = { fontFamily: FONT, fontSize: '22px', color: '#ffffff', stroke: '#000', strokeThickness: 3 };

export class TitleScene extends Phaser.Scene {
  constructor() { super('title'); }

  create() {
    this.cameras.main.fadeIn(300);
    sfx.startMusic('title');
    this.events.once('shutdown', () => sfx.stopMusic());
    const art = this.add.image(W / 2, H / 2, 'title_keyart');
    art.setScale(Math.max(W / art.width, H / art.height));
    this.add.rectangle(W / 2, H / 2, W, H, 0x0b0d14, 0.35);

    this.add.text(W / 2, 90, '劍與魔法亂鬥', { fontFamily: FONT, fontSize: '72px', fontStyle: 'bold', color: '#ffe9a8', stroke: '#2a1a40', strokeThickness: 10 }).setOrigin(0.5);
    this.add.text(W / 2, 150, '原型版 v0.1  ·  暫定名稱', { fontFamily: FONT, fontSize: '20px', color: '#cfc8e8' }).setOrigin(0.5);
    this.add.text(W / 2, 200, '選擇職業', { fontFamily: FONT, fontSize: '26px', color: '#ffffff' }).setOrigin(0.5);

    const cardW = 180, gap = 20;
    const startX = W / 2 - (CLASSES.length * (cardW + gap) - gap) / 2 + cardW / 2;
    CLASSES.forEach((c, i) => {
      const x = startX + i * (cardW + gap);
      const y = 360;
      const card = this.add.rectangle(x, y, cardW, 240, c.playable ? 0x000000 : 0x111111, c.playable ? 0.55 : 0.7)
        .setStrokeStyle(3, c.playable ? c.color : 0x444444, 1);
      const portrait = this.add.image(x, y + 45, `${c.id}_front`).setOrigin(0.5, 1);
      portrait.setScale(150 / portrait.height);
      if (!c.playable) portrait.setTint(0x555555).setAlpha(0.7);
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

    this.add.text(W / 2, 560, '點選職業開始 → 加點 → 選關卡', { fontFamily: FONT, fontSize: '18px', color: '#cfc8e8' }).setOrigin(0.5);

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

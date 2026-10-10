import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { getClass } from '../data/classes';
import { STAGES } from '../data/stages';
import { STORY, type StoryLine } from '../data/story';
import { queueStage, showLoading } from '../systems/Assets';
import { session } from '../systems/Save';
import { sfx } from '../systems/Sfx';

export interface DialogueData {
  stageId: string;
  phase: 'before' | 'after';
  /** Scene to start when the dialogue ends. */
  next: string;
  nextData?: object;
}

/** Visual-novel style story beats: portrait, name, typewriter text; tap / J / Space / Enter to advance, Esc or 跳過 to skip. */
export class DialogueScene extends Phaser.Scene {
  private d!: DialogueData;
  private lines: StoryLine[] = [];
  private i = -1;
  private textObj!: Phaser.GameObjects.Text;
  private nameObj!: Phaser.GameObjects.Text;
  private portrait?: Phaser.GameObjects.Image;
  private cursor!: Phaser.GameObjects.Text;
  private full = '';
  private shown = 0;
  private typeTimer?: Phaser.Time.TimerEvent;
  private done = false;

  constructor() { super('dialogue'); }
  init(d: DialogueData) { this.d = d; }

  preload() {
    queueStage(this, this.d.stageId, session.classId);
    showLoading(this);
  }

  create() {
    const cls = getClass(session.classId);
    const story = STORY[this.d.stageId];
    this.lines = (story ? story[this.d.phase] : []).map((l) => ({ ...l, text: l.text.replace(/\{name\}/g, cls.name) }));
    if (this.lines.length === 0) { this.finish(); return; }
    this.i = -1;
    this.done = false;

    // Backdrop: the stage's own painting, darkened, with letterbox bars.
    const stage = STAGES[this.d.stageId];
    const bg = this.add.image(W / 2, H / 2, stage.bg).setDisplaySize(W, H).setTint(stage.tint ?? 0xffffff).setAlpha(0.8);
    this.add.rectangle(W / 2, H / 2, W, H, 0x000000, 0.35);
    this.add.rectangle(W / 2, 45, W, 90, 0x000000, 0.85);
    this.add.rectangle(W / 2, H - 45, W, 90, 0x000000, 0.85);
    this.tweens.add({ targets: bg, scale: bg.scale * 1.05, duration: 12000, ease: 'Sine.easeInOut', yoyo: true, repeat: -1 });

    const style = { fontFamily: FONT, color: '#ffffff', stroke: '#000', strokeThickness: 3 };
    this.add.text(30, 30, `${stage.chapter}  ${stage.name}`, { ...style, fontSize: '20px', color: '#cfd4e6' });

    // Dialogue box
    const boxY = H - 90 - 110;
    this.add.rectangle(W / 2, boxY, W - 80, 170, 0x14101c, 0.88).setStrokeStyle(3, 0xc9a86a, 1);
    this.add.rectangle(W / 2, boxY, W - 92, 158, 0x000000, 0).setStrokeStyle(1, 0xc9a86a, 0.5);
    this.nameObj = this.add.text(70, boxY - 70, '', { ...style, fontSize: '22px', fontStyle: 'bold', color: '#ffd98a' });
    this.textObj = this.add.text(70, boxY - 32, '', { ...style, fontSize: '24px', wordWrap: { width: W - 180 }, lineSpacing: 8 });
    this.cursor = this.add.text(W - 70, boxY + 60, '▼', { ...style, fontSize: '20px', color: '#ffd98a' }).setOrigin(1, 0.5);
    this.tweens.add({ targets: this.cursor, y: boxY + 66, duration: 400, yoyo: true, repeat: -1 });

    // Skip button
    const skip = this.add.rectangle(W - 90, 45, 130, 44, 0xffffff, 0.12).setStrokeStyle(2, 0xffffff, 0.6).setInteractive({ useHandCursor: true });
    this.add.text(W - 90, 45, '跳過 ▶', { ...style, fontSize: '20px' }).setOrigin(0.5);
    skip.on('pointerdown', (p: Phaser.Input.Pointer, _x: number, _y: number, ev: Phaser.Types.Input.EventData) => { ev.stopPropagation(); void p; this.finish(); });

    this.input.on('pointerdown', () => this.advance());
    this.input.keyboard?.on('keydown', (e: KeyboardEvent) => {
      if (e.repeat) return;
      if (e.code === 'Escape') this.finish();
      else if (['Space', 'Enter', 'KeyJ', 'KeyZ', 'KeyK', 'ArrowRight'].includes(e.code)) { e.preventDefault(); this.advance(); }
    });
    this.cameras.main.fadeIn(350);
    this.advance();
  }

  private advance() {
    if (this.done) return;
    sfx.unlock();
    if (this.shown < this.full.length) { this.shown = this.full.length; this.textObj.setText(this.full); this.cursor.setVisible(true); return; }
    this.i++;
    if (this.i >= this.lines.length) { this.finish(); return; }
    sfx.play('select', 0.5);
    this.show(this.lines[this.i]);
  }

  private show(l: StoryLine) {
    const cls = getClass(session.classId);
    this.portrait?.destroy();
    this.portrait = undefined;
    const key = l.who === 'player' ? `${cls.id}_front` : l.portrait;
    if (key && this.textures.exists(key)) {
      const right = l.who !== 'player';
      this.portrait = this.add.image(right ? W - 170 : 170, H - 90 - 190, key).setOrigin(0.5, 1);
      const sc = Math.min(360 / this.portrait.height, 320 / this.portrait.width);
      this.portrait.setScale(sc * (right ? -1 : 1), sc).setAlpha(0);
      this.tweens.add({ targets: this.portrait, alpha: 1, x: this.portrait.x + (right ? -20 : 20), duration: 220, ease: 'Cubic.easeOut' });
    }
    const name = l.who === 'player' ? cls.name : l.who === 'narrator' ? '' : l.name ?? '';
    this.nameObj.setText(name);
    this.textObj.setStyle({ fontStyle: l.who === 'narrator' ? 'italic' : 'normal', color: l.who === 'narrator' ? '#cfd4e6' : '#ffffff' });
    this.full = l.text;
    this.shown = 0;
    this.textObj.setText('');
    this.cursor.setVisible(false);
    this.typeTimer?.remove();
    this.typeTimer = this.time.addEvent({
      delay: 28, repeat: this.full.length,
      callback: () => {
        if (this.shown >= this.full.length) { this.cursor.setVisible(true); this.typeTimer?.remove(); return; }
        this.shown++;
        this.textObj.setText(this.full.slice(0, this.shown));
        if (this.shown % 3 === 0) sfx.play('text', 0.35);
      },
    });
  }

  private finish() {
    if (this.done) return;
    this.done = true;
    this.typeTimer?.remove();
    this.cameras.main.fadeOut(300);
    this.time.delayedCall(320, () => this.scene.start(this.d.next, this.d.nextData));
  }
}

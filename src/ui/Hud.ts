import Phaser from 'phaser';
import { FONT, W } from '../config';
import type { Player } from '../entities/Player';
import type { Enemy } from '../entities/Enemy';
import { sfx } from '../systems/Sfx';
import { session } from '../systems/Save';
import { expToNext } from '../systems/Stats';

const DEPTH = 200000;

export class Hud {
  private hpBar: Phaser.GameObjects.Rectangle;
  private spBar: Phaser.GameObjects.Rectangle;
  private expBar: Phaser.GameObjects.Rectangle;
  private hpText: Phaser.GameObjects.Text;
  private spText: Phaser.GameObjects.Text;
  private lvText: Phaser.GameObjects.Text;
  private goldText: Phaser.GameObjects.Text;
  private potText: Phaser.GameObjects.Text;
  private bossBar?: Phaser.GameObjects.Rectangle;
  private bossName?: Phaser.GameObjects.Text;
  private toastText: Phaser.GameObjects.Text;
  private skillCd: { bg: Phaser.GameObjects.Rectangle; cover: Phaser.GameObjects.Rectangle; label: Phaser.GameObjects.Image; key: Phaser.GameObjects.Text }[] = [];
  private goText: Phaser.GameObjects.Text;
  private buffText: Phaser.GameObjects.Text;
  private comboText: Phaser.GameObjects.Text;
  private comboTween?: Phaser.Tweens.Tween;

  constructor(private scene: Phaser.Scene, private player: Player, touch: boolean) {
    const add = scene.add;
    const fixed = <T extends Phaser.GameObjects.Components.ScrollFactor & Phaser.GameObjects.Components.Depth>(o: T) => o.setScrollFactor(0).setDepth(DEPTH);
    const x0 = 24, y0 = 18;
    fixed(add.rectangle(x0, y0, 330, 86, 0x0b0d14, 0.55).setOrigin(0));
    fixed(add.rectangle(x0 + 10, y0 + 32, 300, 16, 0x3a1020).setOrigin(0));
    this.hpBar = fixed(add.rectangle(x0 + 10, y0 + 32, 300, 16, 0xff4d5a).setOrigin(0));
    fixed(add.rectangle(x0 + 10, y0 + 54, 300, 10, 0x10203a).setOrigin(0));
    this.spBar = fixed(add.rectangle(x0 + 10, y0 + 54, 300, 10, 0x4da3ff).setOrigin(0));
    fixed(add.rectangle(x0 + 10, y0 + 70, 300, 5, 0x2a2a1a).setOrigin(0));
    this.expBar = fixed(add.rectangle(x0 + 10, y0 + 70, 0, 5, 0xffd34d).setOrigin(0));
    const style = { fontFamily: FONT, fontSize: '15px', color: '#ffffff', stroke: '#000', strokeThickness: 3 };
    this.lvText = fixed(add.text(x0 + 10, y0 + 6, '', { ...style, fontSize: '18px', fontStyle: 'bold' }));
    this.hpText = fixed(add.text(x0 + 310, y0 + 31, '', { ...style, fontSize: '13px' }).setOrigin(1, 0));
    this.spText = fixed(add.text(x0 + 310, y0 + 51, '', { ...style, fontSize: '11px' }).setOrigin(1, 0));
    this.goldText = fixed(add.text(x0 + 200, y0 + 8, '', { ...style, color: '#ffd34d' }));
    this.potText = fixed(add.text(W - 24, 22, '', { ...style, fontSize: '16px' }).setOrigin(1, 0));
    this.buffText = fixed(add.text(x0 + 4, y0 + 92, '', { ...style, fontSize: '14px', color: '#b6f28a' }));
    this.comboText = fixed(add.text(W - 40, 250, '', { ...style, fontSize: '40px', fontStyle: 'bold', color: '#ffe066', strokeThickness: 6 }).setOrigin(1, 0.5)).setAlpha(0);
    const mute = fixed(add.text(W - 24, 48, sfx.muted ? '🔇' : '🔊', { ...style, fontSize: '18px' }).setOrigin(1, 0)).setInteractive({ useHandCursor: true });
    mute.on('pointerdown', (_p: Phaser.Input.Pointer, _x: number, _y: number, ev: Phaser.Types.Input.EventData) => { ev.stopPropagation(); mute.setText(sfx.toggleMute() ? '🔇' : '🔊'); });

    const cls = player.cls;
    if (!touch) {
      const keys = ['U', 'I', 'O', 'P'];
      cls.skills.forEach((s, i) => {
        const bx = W / 2 - 150 + i * 76, by = 20;
        const bg = fixed(add.rectangle(bx, by, 64, 64, 0x000000, 0.4).setOrigin(0).setStrokeStyle(2, 0xffffff, 0.6));
        const label = fixed(add.image(bx + 32, by + 32, `icon_${s.id}`).setDisplaySize(60, 60));
        const cover = fixed(add.rectangle(bx, by + 64, 64, 0, 0x000000, 0.65).setOrigin(0, 1));
        const key = fixed(add.text(bx + 4, by + 2, keys[i], { ...style, fontSize: '12px' }));
        this.skillCd.push({ bg, cover, label, key });
      });
      fixed(add.text(W / 2, 92, 'J 攻擊  K 跳躍  L 防禦(+方向閃躲)  1/2 藥水  Esc 選單', { ...style, fontSize: '13px', color: '#cfd4e6' }).setOrigin(0.5, 0));
    }

    this.toastText = fixed(add.text(W / 2, 150, '', { ...style, fontSize: '20px', color: '#fff2b3' }).setOrigin(0.5)).setAlpha(0);
    this.goText = fixed(add.text(W - 60, 300, 'GO →', { ...style, fontSize: '44px', fontStyle: 'bold', color: '#ffe066' }).setOrigin(1, 0.5)).setAlpha(0);
  }

  showBoss(e: Enemy) {
    const fixed = <T extends Phaser.GameObjects.Components.ScrollFactor & Phaser.GameObjects.Components.Depth>(o: T) => o.setScrollFactor(0).setDepth(DEPTH);
    const y = 118;
    fixed(this.scene.add.rectangle(W / 2, y, 620, 22, 0x000000, 0.6).setStrokeStyle(2, 0xffffff, 0.5));
    this.bossBar = fixed(this.scene.add.rectangle(W / 2 - 308, y, 616, 16, 0xc0304a).setOrigin(0, 0.5));
    this.bossName = fixed(this.scene.add.text(W / 2, y - 26, e.def.name, { fontFamily: FONT, fontSize: '18px', fontStyle: 'bold', color: '#ffb3c6', stroke: '#000', strokeThickness: 4 }).setOrigin(0.5));
    this.boss = e;
  }
  private boss?: Enemy;

  toast(msg: string) {
    this.toastText.setText(msg).setAlpha(1);
    this.scene.tweens.killTweensOf(this.toastText);
    this.scene.tweens.add({ targets: this.toastText, alpha: 0, delay: 1600, duration: 400 });
  }

  showGo(show: boolean) {
    if (show && this.goText.alpha === 0) {
      this.goText.setAlpha(1);
      this.scene.tweens.add({ targets: this.goText, x: W - 40, duration: 400, yoyo: true, repeat: -1 });
    } else if (!show && this.goText.alpha > 0) {
      this.scene.tweens.killTweensOf(this.goText);
      this.goText.setAlpha(0).setX(W - 60);
    }
  }

  /** Shows the hit counter with a pop; fades when the chain breaks. */
  combo(n: number) {
    if (n < 2) return;
    this.comboText.setText(`${n} HITS`).setAlpha(1).setScale(1.35);
    this.comboTween?.remove();
    this.comboTween = this.scene.tweens.add({ targets: this.comboText, scale: 1, duration: 120 });
    this.scene.tweens.add({ targets: this.comboText, alpha: 0, delay: 1500, duration: 300 });
  }

  update() {
    const p = this.player;
    this.hpBar.width = 300 * Phaser.Math.Clamp(p.hp / p.maxHp, 0, 1);
    this.spBar.width = 300 * Phaser.Math.Clamp(p.sp / p.maxSp, 0, 1);
    this.expBar.width = 300 * Phaser.Math.Clamp(p.prog.exp / expToNext(p.prog.level), 0, 1);
    this.hpText.setText(`${Math.ceil(p.hp)} / ${p.maxHp}`);
    this.spText.setText(`${Math.floor(p.sp)} / ${p.maxSp}`);
    this.lvText.setText(`${p.cls.name}  Lv.${p.prog.level}`);
    this.goldText.setText(`${session.save.gold} G`);
    this.buffText.setText(p.buffs.map((b) => `${b.name} ${Math.ceil((b.until - p.now) / 1000)}s${b.hits ? ` ×${b.hits}` : ''}`).join('   '));
    const pots = session.save.potions;
    this.potText.setText(`[1] 紅藥水 ×${pots.red}    [2] 藍藥水 ×${pots.blue}`);
    this.skillCd.forEach((b, i) => {
      const s = p.cls.skills[i];
      const learned = p.skillLv(s) > 0;
      b.bg.setAlpha(learned ? 0.4 : 0.2);
      b.label.setAlpha(learned ? 1 : 0.3);
      const left = p.cooldownLeft(s);
      b.cover.height = left > 0 ? 64 * Math.min(1, left / (s.cooldown * p.d.castMult)) : 0;
      b.cover.y = 20 + 64;
      b.key.setText(learned ? `${['U', 'I', 'O', 'P'][i]} Lv${p.skillLv(s)}` : ['U', 'I', 'O', 'P'][i]);
    });
    if (this.boss && this.bossBar) {
      this.bossBar.width = 616 * Phaser.Math.Clamp(this.boss.hp / this.boss.maxHp, 0, 1);
      if (this.boss.dead) { this.bossBar.setVisible(false); this.bossName?.setVisible(false); }
    }
  }
}

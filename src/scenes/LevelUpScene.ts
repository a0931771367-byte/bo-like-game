import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { getClass } from '../data/classes';
import type { StatKey } from '../data/types';
import { progressFor, session, writeSave } from '../systems/Save';
import { derive, expToNext } from '../systems/Stats';

const STAT_LABELS: Record<StatKey, string> = { str: '力量 STR', agi: '敏捷 AGI', vit: '體力 VIT', int: '智力 INT', dex: '靈巧 DEX', luk: '幸運 LUK' };
const STAT_HINT: Record<StatKey, string> = { str: '近戰傷害', agi: '移動與攻速、閃避', vit: 'HP 與防禦', int: '魔法傷害、SP', dex: '命中、詠唱速度', luk: '爆擊率' };
const txt = { fontFamily: FONT, fontSize: '20px', color: '#ffffff', stroke: '#000', strokeThickness: 3 };

/** Stat / skill point allocation. Opened from the title (before a stage) or paused in-game. */
export class LevelUpScene extends Phaser.Scene {
  private from: 'title' | 'game' = 'title';
  private refresh: (() => void)[] = [];

  constructor() { super('levelup'); }

  init(data: { from: 'title' | 'game' }) { this.from = data.from ?? 'title'; }

  create() {
    this.refresh = [];
    const cls = getClass(session.classId);
    const p = progressFor(cls);
    this.add.rectangle(W / 2, H / 2, W, H, 0x0b0d14, this.from === 'game' ? 0.82 : 1);
    this.add.text(W / 2, 40, `${cls.name}  Lv.${p.level}`, { ...txt, fontSize: '36px', fontStyle: 'bold' }).setOrigin(0.5);
    const expText = this.add.text(W / 2, 80, '', { ...txt, fontSize: '16px', color: '#ffd34d' }).setOrigin(0.5);
    this.refresh.push(() => expText.setText(`EXP ${p.exp} / ${expToNext(p.level)}    金幣 ${session.save.gold} G`));

    // ---- stats (left) ----
    const sx = 120, sy = 140;
    const spText = this.add.text(sx, sy, '', { ...txt, fontSize: '22px', color: '#9be15d' });
    this.refresh.push(() => spText.setText(`屬性點：${p.statPoints}`));
    (Object.keys(STAT_LABELS) as StatKey[]).forEach((k, i) => {
      const y = sy + 50 + i * 56;
      this.add.text(sx, y, STAT_LABELS[k], txt);
      this.add.text(sx, y + 24, STAT_HINT[k], { ...txt, fontSize: '13px', color: '#9a93b8' });
      const v = this.add.text(sx + 220, y + 10, '', { ...txt, fontSize: '26px', fontStyle: 'bold' }).setOrigin(0.5);
      this.refresh.push(() => v.setText(`${p.stats[k]}`));
      this.smallBtn(sx + 290, y + 10, '+', () => { if (p.statPoints > 0) { p.statPoints--; p.stats[k]++; this.update_(); } });
    });

    // ---- derived (middle) ----
    const dx = 480, dy = 190;
    const dText = this.add.text(dx, dy, '', { ...txt, fontSize: '17px', lineSpacing: 8, color: '#dcd6f0' });
    this.refresh.push(() => {
      const d = derive(cls, p);
      dText.setText([`HP ${d.maxHp}`, `SP ${d.maxSp}`, `物攻 ${d.atk}`, `魔攻 ${d.matk}`, `物防 ${d.def}  魔防 ${d.mdef}`,
        `爆擊 ${d.crit.toFixed(1)}%  閃避 ${d.flee.toFixed(1)}%`, `攻速 ×${d.aspd.toFixed(2)}  移速 ${Math.round(d.move)}`, `詠唱時間 ×${d.castMult.toFixed(2)}`].join('\n'));
    });

    // ---- skills (right) ----
    const kx = 720, ky = 140;
    const skText = this.add.text(kx, ky, '', { ...txt, fontSize: '22px', color: '#7fb2ff' });
    this.refresh.push(() => skText.setText(`技能點：${p.skillPoints}`));
    cls.skills.forEach((s, i) => {
      const y = ky + 50 + i * 100;
      this.add.rectangle(kx + 24, y + 24, 48, 48, s.color).setStrokeStyle(2, 0xffffff, 0.6);
      this.add.text(kx + 48, y + 24, s.short, { ...txt, fontSize: '16px', fontStyle: 'bold' }).setOrigin(0.5);
      this.add.text(kx + 64, y, s.name, { ...txt, fontSize: '20px' });
      this.add.text(kx + 64, y + 26, s.desc, { ...txt, fontSize: '13px', color: '#9a93b8', wordWrap: { width: 300 } });
      const lvT = this.add.text(kx + 64, y + 54, '', { ...txt, fontSize: '15px', color: '#ffd34d' });
      this.refresh.push(() => {
        const L = p.skillLv[s.id] ?? 0;
        lvT.setText(`Lv ${L} / ${s.maxLv}   消耗 SP ${Math.round(s.sp[0] + s.sp[1] * Math.max(0, L - 1))}   冷卻 ${(s.cooldown / 1000).toFixed(1)}s`);
      });
      this.smallBtn(kx + 430, y + 24, '+', () => {
        const L = p.skillLv[s.id] ?? 0;
        if (p.skillPoints > 0 && L < s.maxLv) { p.skillPoints--; p.skillLv[s.id] = L + 1; this.update_(); }
      });
    });

    // ---- footer ----
    const label = this.from === 'game' ? '返回戰鬥' : '出發：第 1 章第 1 關';
    this.bigBtn(W / 2 + 160, H - 60, label, 0x4a7bd1, () => {
      writeSave(session.save);
      if (this.from === 'game') {
        const game = this.scene.get('game') as Phaser.Scene & { player: { recompute: (full: boolean) => void } };
        game.player.recompute(false);
        this.scene.stop();
        this.scene.resume('game');
      } else {
        this.scene.start('game');
      }
    });
    this.bigBtn(W / 2 - 160, H - 60, this.from === 'game' ? '放棄關卡回標題' : '回標題', 0x555a70, () => {
      writeSave(session.save);
      if (this.from === 'game') this.scene.stop('game');
      this.scene.start('title');
    });
    if (this.from === 'title') {
      this.add.text(W / 2, H - 110, '提示：第一次玩建議劍士加 力量/體力，法師加 智力/靈巧。技能要先加點才能使用。', { ...txt, fontSize: '15px', color: '#cfc8e8' }).setOrigin(0.5);
    }
    this.update_();
    this.input.keyboard?.on('keydown-ESC', () => { if (this.from === 'game') { writeSave(session.save); this.scene.stop(); this.scene.resume('game'); } });
  }

  private update_() { this.refresh.forEach((f) => f()); }

  private smallBtn(x: number, y: number, t: string, cb: () => void) {
    const r = this.add.rectangle(x, y, 44, 44, 0x9be15d, 0.85).setStrokeStyle(2, 0xffffff, 0.7).setInteractive({ useHandCursor: true });
    this.add.text(x, y, t, { ...txt, fontSize: '28px', fontStyle: 'bold', color: '#102010', strokeThickness: 0 }).setOrigin(0.5);
    r.on('pointerdown', () => { cb(); r.setScale(0.9); this.time.delayedCall(80, () => r.setScale(1)); });
  }

  private bigBtn(x: number, y: number, t: string, color: number, cb: () => void) {
    const r = this.add.rectangle(x, y, 300, 56, color, 0.9).setStrokeStyle(2, 0xffffff, 0.6).setInteractive({ useHandCursor: true });
    this.add.text(x, y, t, { ...txt, fontSize: '22px', fontStyle: 'bold' }).setOrigin(0.5);
    r.on('pointerover', () => r.setAlpha(1)); r.on('pointerout', () => r.setAlpha(0.9));
    r.on('pointerdown', cb);
  }
}

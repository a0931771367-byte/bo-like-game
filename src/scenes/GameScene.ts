import Phaser from 'phaser';
import { FONT, GROUND_BOTTOM, GROUND_TOP, H, W } from '../config';
import { ENEMIES } from '../data/enemies';
import { getClass } from '../data/classes';
import { STAGES } from '../data/stages';
import type { StageDef } from '../data/types';
import { Enemy } from '../entities/Enemy';
import type { Fighter, HitInfo } from '../entities/Fighter';
import { Projectile, Wall, type ProjectileOpts, type WallOpts } from '../entities/Hazards';
import { Player } from '../entities/Player';
import { controls } from '../systems/Controls';
import { progressFor, session, writeSave } from '../systems/Save';
import { addExp } from '../systems/Stats';
import { sfx } from '../systems/Sfx';
import { Fx } from '../ui/Fx';
import { Hud } from '../ui/Hud';
import { TouchControls, isTouchDevice } from '../ui/TouchControls';

export class GameScene extends Phaser.Scene {
  player!: Player;
  enemies: Enemy[] = [];
  projectiles: Projectile[] = [];
  walls: Wall[] = [];
  fx!: Fx;
  hud!: Hud;
  touch?: TouchControls;
  stage!: StageDef;
  waveIndex = 0;
  pendingSpawns = 0;
  lockRight = 0;
  lastDt = 0.016;
  private hitStopUntil = 0;
  private finished = false;
  private boss?: Enemy;
  private cleared = false;
  comboHits = 0;
  comboUntil = 0;

  constructor() { super('game'); }

  create() {
    this.stage = STAGES[session.stageId];
    const cls = getClass(session.classId);
    this.fx = new Fx(this);
    this.finished = false;
    this.cleared = false;
    this.waveIndex = 0;
    this.enemies = [];
    this.projectiles = [];
    this.walls = [];
    this.lockRight = W;
    controls.reset();

    this.drawBackground();
    this.player = new Player(this, 200, (GROUND_TOP + GROUND_BOTTOM) / 2, cls, progressFor(cls));
    this.player.recompute(true);

    const touch = isTouchDevice();
    this.hud = new Hud(this, this.player, touch);
    if (touch) this.touch = new TouchControls(this, this.player);
    this.bindKeyboard();

    this.cameras.main.setBounds(0, 0, this.stage.width, H);
    this.cameras.main.startFollow(this.player.root, true, 0.12, 0.12, -150, 0);
    this.cameras.main.fadeIn(400);
    this.fx.banner(`${this.stage.chapter}  ${this.stage.name}`, '#ffffff', 48);
    this.comboHits = 0;
    sfx.startMusic(this.stage.tint && this.stage.tint < 0xa0a0ff ? 'night' : 'field');
    this.events.once('shutdown', () => sfx.stopMusic());
  }

  /** Counts consecutive player hits for the combo display. */
  registerHit() {
    this.comboHits = this.time.now < this.comboUntil ? this.comboHits + 1 : 1;
    this.comboUntil = this.time.now + 1600;
    this.hud.combo(this.comboHits);
  }

  // ---------- setup ----------

  private drawBackground() {
    const { width } = this.stage;
    const key = this.stage.bg;
    const tex = this.textures.get(key);
    const src = tex.getSourceImage() as HTMLImageElement;
    const groundY = this.stage.bgGroundY; // where the walkable floor strip starts in the painting
    if (!tex.has('sky')) {
      tex.add('sky', 0, 0, 0, src.width, groundY);
      tex.add('ground', 0, 0, groundY, src.width, src.height - groundY);
    }
    // far layer: sky, castle and hills scroll at half speed
    const sky = this.add.tileSprite(0, 0, width / 2 + W, groundY, key, 'sky').setOrigin(0).setScrollFactor(0.5, 0).setDepth(-100);
    // near layer: grass and soil move with the player and sit under the fighters
    const gh = H - (GROUND_TOP - 30);
    const ground = this.add.tileSprite(0, GROUND_TOP - 30, width + W, gh, key, 'ground').setOrigin(0).setDepth(-50);
    ground.tileScaleY = gh / (src.height - groundY);
    if (this.stage.tint !== undefined) { sky.setTint(this.stage.tint); ground.setTint(this.stage.tint); }
    // goal flag
    const gx = width - 120;
    this.add.rectangle(gx, GROUND_BOTTOM - 10, 8, 170, 0x6b4a2b).setOrigin(0.5, 1).setDepth(GROUND_BOTTOM - 11);
    this.add.triangle(gx + 4, GROUND_BOTTOM - 180, 0, 0, 70, 20, 0, 40, 0xffd34d).setOrigin(0, 0).setDepth(GROUND_BOTTOM - 11);
  }

  private bindKeyboard() {
    const kb = this.input.keyboard;
    if (!kb) return;
    const map: Record<string, Parameters<typeof controls.press>[0]> = {
      KeyJ: 'attack', KeyK: 'jump', KeyU: 'skill0', KeyI: 'skill1', KeyO: 'skill2', KeyP: 'skill3',
      Digit1: 'pot0', Digit2: 'pot1', Escape: 'menu', Space: 'jump', KeyZ: 'attack', KeyX: 'jump',
    };
    kb.on('keydown', (e: KeyboardEvent) => {
      if (e.repeat) return;
      if (e.code === 'KeyM') { this.toast(sfx.toggleMute() ? '音效已靜音' : '音效已開啟'); return; }
      const a = map[e.code];
      if (a) { controls.press(a); e.preventDefault(); }
    });
    const left = [kb.addKey('LEFT'), kb.addKey('A')];
    const right = [kb.addKey('RIGHT'), kb.addKey('D')];
    const up = [kb.addKey('UP'), kb.addKey('W')];
    const down = [kb.addKey('DOWN'), kb.addKey('S')];
    const guard = [kb.addKey('L'), kb.addKey('C'), kb.addKey('SHIFT')];
    this.events.on('update', () => {
      const any = (ks: Phaser.Input.Keyboard.Key[]) => ks.some((k) => k.isDown);
      controls.kbX = (any(right) ? 1 : 0) - (any(left) ? 1 : 0);
      controls.kbY = (any(down) ? 1 : 0) - (any(up) ? 1 : 0);
      controls.kbGuard = any(guard);
    });
  }

  // ---------- world queries used by entities ----------

  playerBounds(): [number, number] {
    const cam = this.cameras.main;
    return [cam.scrollX + 30, Math.min(this.stage.width - 30, this.lockRight - 30)];
  }

  enemyBounds(): [number, number] {
    const cam = this.cameras.main;
    return [cam.scrollX - 80, Math.min(this.stage.width + 80, Math.max(this.lockRight, cam.scrollX + W) + 80)];
  }

  /** Front arc hit test; returns the number of new targets struck. */
  strikeFront(src: Fighter, targets: Fighter[], range: number, depthY: number, make: (t: Fighter) => HitInfo) {
    let n = 0;
    for (const t of targets) {
      if (t.dead || src.struck.has(t)) continue;
      const dx = t.x - src.x;
      if (Math.sign(dx) !== src.facing && Math.abs(dx) > t.w * 0.5) continue;
      if (Math.abs(dx) > range + t.w / 2 || Math.abs(t.y - src.y) > depthY || Math.abs(t.z - src.z) > 120) continue;
      src.struck.add(t);
      if (t.takeHit(make(t))) n++;
    }
    return n;
  }

  strikeCircle(src: Fighter, targets: Fighter[], x: number, y: number, r: number, make: (t: Fighter) => HitInfo, ignoreStruck = false) {
    let n = 0;
    for (const t of targets) {
      if (t.dead || (!ignoreStruck && src.struck.has(t))) continue;
      if (Phaser.Math.Distance.Between(t.x, t.y * 2, x, y * 2) > r + t.w / 2) continue;
      if (!ignoreStruck) src.struck.add(t);
      if (t.takeHit(make(t))) n++;
    }
    return n;
  }

  spawnProjectile(o: ProjectileOpts) { this.projectiles.push(new Projectile(this, o)); }
  spawnWall(o: WallOpts) { this.walls.push(new Wall(this, o)); }

  hitStop(ms: number) { this.hitStopUntil = Math.max(this.hitStopUntil, this.time.now + ms); }

  toast(msg: string) { this.hud.toast(msg); }

  // ---------- waves ----------

  private spawnWave() {
    const wave = this.stage.waves[this.waveIndex];
    this.waveIndex++;
    this.lockRight = Math.min(this.stage.width, this.cameras.main.scrollX + W);
    this.pendingSpawns = wave.spawns.length;
    for (const s of wave.spawns) {
      this.time.delayedCall(s.delay, () => {
        this.pendingSpawns--;
        if (this.finished) return;
        const def = ENEMIES[s.enemy];
        const cam = this.cameras.main;
        const rooted = def.speed === 0;
        // Rooted enemies (plants) can't walk in, so they sprout inside the view instead.
        const x = rooted
          ? Phaser.Math.Clamp(s.side === 'right' ? cam.scrollX + W - 160 : cam.scrollX + 160, this.player.x - 500, this.player.x + 500)
          : s.side === 'right' ? cam.scrollX + W + 60 : cam.scrollX - 60;
        const y = Phaser.Math.Between(GROUND_TOP + 20, GROUND_BOTTOM - 20);
        const e = new Enemy(this, x, y, def);
        if (rooted) {
          this.fx.dust(x, y, 10, 1.4);
          this.fx.ring(x, y, 90, def.color, y - 1);
          e.attackReadyAt = this.time.now + 900;
          e.view.scaleY = 0.2;
          this.tweens.add({ targets: e.view, scaleY: 1, duration: 350, ease: 'Back.easeOut' });
        }
        this.enemies.push(e);
        if (def.boss) {
          this.boss = e;
          this.hud.showBoss(e);
          this.fx.banner(`${def.name} 出現！`, '#ffb3c6', 56);
          this.cameras.main.shake(300, 0.01);
          sfx.play('bossAppear');
          sfx.startMusic('boss');
        }
      });
    }
  }

  onEnemyKilled(e: Enemy) {
    const p = this.player;
    const gold = Math.round(e.def.gold * (1 + p.passiveValue('goldFind')));
    session.save.gold += gold;
    if (gold > e.def.gold) this.fx.number(e.x + 30, e.y - e.h - 30, `+${gold} G`, '#ffd34d');
    const gained = addExp(p.prog, e.def.exp);
    this.fx.number(e.x, e.y - e.h - 60, `+${e.def.exp} EXP`, '#ffd34d');
    sfx.play(e.def.boss ? 'bossDie' : 'enemyDie');
    this.time.delayedCall(150, () => sfx.play('gold', 0.6));
    if (e.def.boss) sfx.stopMusic();
    if (gained > 0) {
      p.recompute(true);
      sfx.play('levelup');
      this.fx.banner('LEVEL UP!', '#ffe066', 72);
      this.fx.aura(p.x, p.y, 0xffe066);
      this.fx.ring(p.x, p.y, 160, 0xffe066, p.y - 1);
    }
    if (e.def.boss) this.time.delayedCall(900, () => this.stageClear());
  }

  removeEnemy(e: Enemy) {
    e.destroy();
    this.enemies = this.enemies.filter((k) => k !== e);
  }

  // ---------- flow ----------

  onPlayerDead() {
    if (this.finished) return;
    this.finished = true;
    this.player.prog.exp = Math.floor(this.player.prog.exp / 2);
    writeSave(session.save);
    this.fx.banner('你倒下了…', '#ff8080', 64);
    sfx.stopMusic();
    sfx.play('lose');
    this.time.delayedCall(1400, () => this.scene.start('result', { win: false }));
  }

  private stageClear() {
    if (this.cleared) return;
    this.finished = true;
    this.cleared = true;
    if (!session.save.cleared.includes(this.stage.id)) session.save.cleared.push(this.stage.id);
    writeSave(session.save);
    this.fx.banner('STAGE CLEAR!', '#ffe066', 72);
    sfx.play('clear');
    this.time.delayedCall(1800, () => {
      this.cameras.main.fadeOut(300);
      this.time.delayedCall(320, () => this.scene.start('dialogue', { stageId: this.stage.id, phase: 'after', next: 'result', nextData: { win: true } }));
    });
  }

  private openMenu() {
    writeSave(session.save);
    this.scene.pause();
    this.scene.launch('levelup', { from: 'game' });
  }

  update(_t: number, delta: number) {
    let dt = Math.min(delta, 50) / 1000;
    const frozen = this.time.now < this.hitStopUntil;
    if (frozen) dt *= 0.08;
    this.lastDt = dt;

    if (controls.consume('menu') && !this.finished) { this.openMenu(); return; }
    const cam = this.cameras.main;
    cam.followOffset.x = Phaser.Math.Linear(cam.followOffset.x, -150 * this.player.facing, 0.03);

    this.player.update(dt);
    for (const e of this.enemies) e.update(dt);
    for (const p of this.projectiles) p.update(dt, p.o.team === 'player' ? [this.player] : this.enemies);
    this.projectiles = this.projectiles.filter((p) => !p.done);
    for (const w of this.walls) w.update(this.enemies);
    this.walls = this.walls.filter((w) => !w.done);

    this.player.syncView();
    for (const e of this.enemies) e.syncView();

    // Wave progression
    const alive = this.enemies.filter((e) => !e.dead).length;
    const waveActive = alive > 0 || this.pendingSpawns > 0;
    if (!waveActive && !this.cleared) {
      if (this.waveIndex < this.stage.waves.length) {
        if (this.player.x >= this.stage.waves[this.waveIndex].triggerX) this.spawnWave();
        else this.lockRight = this.stage.width;
      } else if (!this.finished && !this.player.dead) {
        // Stages without a boss end once every wave is beaten.
        this.time.delayedCall(600, () => this.stageClear());
        this.finished = true;
      }
    }
    this.hud.showGo(!waveActive && this.waveIndex < this.stage.waves.length && !this.finished);

    this.hud.update();
    this.touch?.update();
    void this.boss;
  }
}

export const sceneFont = FONT;

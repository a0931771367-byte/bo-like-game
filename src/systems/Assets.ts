import Phaser from 'phaser';
import { FONT, H, W } from '../config';
import { ANIMS, animKey } from '../data/anims';
import { CLASSES } from '../data/classes';
import { ENEMIES } from '../data/enemies';
import { STAGES } from '../data/stages';
import { STORY } from '../data/story';

/**
 * Lazy asset loading. Boot fetches only what the title / level-up / stage-select screens show;
 * each stage's painting, the chosen class's animation frames and that stage's enemies are
 * fetched right before the stage's opening dialogue, and only once (textures already in the
 * cache are skipped), so the first screen appears after ~0.5 MB instead of ~10 MB.
 */

const image = (scene: Phaser.Scene, key: string, url: string) => {
  if (!scene.textures.exists(key)) scene.load.image(key, url);
};

/** Title key art, class portraits and skill icons: everything the menus need. */
export function queueCore(scene: Phaser.Scene) {
  image(scene, 'title_keyart', 'bg/title.webp');
  for (const c of CLASSES) {
    image(scene, `${c.id}_front`, `sprites/${c.id}_front.webp`);
    c.skills.forEach((s, i) => image(scene, `icon_${s.id}`, `icons/skill_${c.id}_${i}.webp`));
  }
}

/** Everything one stage needs for the given class: painting, player frames, enemy sprites, story portraits. */
export function queueStage(scene: Phaser.Scene, stageId: string, classId: string) {
  const stage = STAGES[stageId];
  if (!stage) return;
  image(scene, stage.bg, `bg/${stage.bg.replace(/^bg_/, '')}.webp`);
  image(scene, `${classId}_side`, `sprites/${classId}_side.webp`);
  image(scene, `${classId}_front`, `sprites/${classId}_front.webp`);
  const anim = ANIMS[classId];
  if (anim) {
    for (const label of ['idle', 'walk', 'attack'] as const) {
      for (let i = 0; i < anim[label]; i++) image(scene, animKey(classId, label, i), `sprites/anim/${classId}_${label}_${i}.webp`);
    }
  }
  const sprites = new Set<string>();
  for (const w of stage.waves) for (const s of w.spawns) {
    const def = ENEMIES[s.enemy];
    if (def) sprites.add(def.sprite ?? def.id);
  }
  const story = STORY[stageId];
  if (story) for (const l of [...story.before, ...story.after]) if (l.portrait) sprites.add(l.portrait.replace(/_side$/, ''));
  for (const key of sprites) image(scene, `${key}_side`, `sprites/${key}_side.webp`);
}

/** Draws a progress bar while this scene's loader runs (no-op when nothing was queued). Call at the end of preload(). */
export function showLoading(scene: Phaser.Scene, label = '載入中…') {
  if (scene.load.list.size === 0) return;
  const dim = scene.add.rectangle(W / 2, H / 2, W, H, 0x0b0d14, 1).setDepth(10000);
  const bar = scene.add.rectangle(W / 2 - 200, H / 2, 0, 14, 0xffd34d).setOrigin(0, 0.5).setDepth(10001);
  const frame = scene.add.rectangle(W / 2, H / 2, 404, 18).setStrokeStyle(2, 0xffffff, 0.6).setDepth(10001);
  const text = scene.add.text(W / 2, H / 2 - 40, label, { fontFamily: FONT, fontSize: '22px', color: '#ffffff' }).setOrigin(0.5).setDepth(10001);
  scene.load.on('progress', (v: number) => { bar.width = 400 * v; });
  scene.load.once('complete', () => { dim.destroy(); bar.destroy(); frame.destroy(); text.destroy(); });
}

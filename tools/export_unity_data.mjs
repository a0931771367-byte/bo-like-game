/**
 * Export the game data tables (src/data) to JSON for the Unity project.
 * Usage: node tools/export_unity_data.mjs
 * Writes unity/Assets/Resources/Data/*.json and unity/Assets/Tests/EditMode/stats_vectors.json.
 * Skill `buff` functions are evaluated per level into `buffByLevel` so Unity needs no code from here.
 */
import { build } from 'esbuild';
import { mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const ROOT = dirname(dirname(fileURLToPath(import.meta.url)));
const OUT = join(ROOT, 'unity/Assets/Resources/Data');
const TEST_OUT = join(ROOT, 'unity/Assets/Tests/EditMode');
const TMP = join(ROOT, 'node_modules/.cache/export_unity_data.mjs');

await build({
  stdin: {
    contents: `
      export { CLASSES } from './src/data/classes';
      export { ENEMIES } from './src/data/enemies';
      export { STAGES } from './src/data/stages';
      export { STORY } from './src/data/story';
      export { ANIMS } from './src/data/anims';
      export { PERKS } from './src/data/perks';
      export { ROOMS, ROOM_POOL, BOSS_ROOM } from './src/data/rooms';
      export { RUN_RULES, UNLOCKS, STARTING_CLASSES } from './src/data/meta';
      export { derive, expToNext, elementMult, MAX_LEVEL } from './src/systems/Stats';
      export { newProgress } from './src/systems/Save';`,
    resolveDir: ROOT, loader: 'ts',
  },
  bundle: true, format: 'esm', platform: 'node', outfile: TMP, logLevel: 'error',
  // Save.ts touches localStorage at import time; give it a harmless stand-in.
  banner: { js: 'globalThis.localStorage ??= { getItem: () => null, setItem() {} };' },
});
const D = await import(pathToFileURL(TMP).href);
rmSync(TMP);

const classes = D.CLASSES.map((c) => ({
  ...c,
  skills: c.skills.map(({ buff, ...s }) => ({
    ...s,
    ...(buff ? { buffByLevel: Array.from({ length: s.maxLv }, (_, i) => buff(i + 1)) } : {}),
  })),
}));

const write = (dir, name, data) => {
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, name), JSON.stringify(data, null, 1) + '\n');
};
write(OUT, 'classes.json', classes);
write(OUT, 'enemies.json', Object.values(D.ENEMIES));
write(OUT, 'stages.json', Object.values(D.STAGES));
write(OUT, 'story.json', Object.entries(D.STORY).map(([stageId, s]) => ({ stageId, ...s })));
write(OUT, 'perks.json', D.PERKS);
write(OUT, 'rooms.json', { rooms: Object.values(D.ROOMS), pool: D.ROOM_POOL, boss: D.BOSS_ROOM });
write(OUT, 'meta.json', { rules: D.RUN_RULES, unlocks: D.UNLOCKS, startingClasses: D.STARTING_CLASSES });
write(OUT, 'anims.json', Object.entries(D.ANIMS).map(([classId, a]) => ({ classId, ...a })));

// Reference values from the TypeScript formulas, checked by the Unity EditMode tests.
const ELEMENTS = ['neutral', 'fire', 'water', 'wind', 'earth', 'holy', 'dark', 'poison'];
const derived = [];
for (const cls of D.CLASSES) {
  for (const level of [1, 7, 23, 50]) {
    const p = D.newProgress(cls);
    p.level = level;
    p.stats = Object.fromEntries(Object.entries(cls.baseStats).map(([k, v], i) => [k, v + ((level * (i + 3)) % 17)]));
    derived.push({ classId: cls.id, level, stats: p.stats, expected: D.derive(cls, p) });
  }
}
write(TEST_OUT, 'stats_vectors.json', {
  maxLevel: D.MAX_LEVEL,
  expToNext: Array.from({ length: D.MAX_LEVEL }, (_, i) => D.expToNext(i + 1)),
  elementMult: ELEMENTS.flatMap((a) => ELEMENTS.map((d) => ({ atk: a, def: d, mult: D.elementMult(a, d) }))),
  derived,
});
console.log(`exported ${classes.length} classes, ${Object.keys(D.ENEMIES).length} enemies, ${Object.keys(D.STAGES).length} stages, ${D.PERKS.length} perks, ${Object.keys(D.ROOMS).length} rooms, ${D.UNLOCKS.length} unlocks`);

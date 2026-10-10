import type { ClassDef, Stats } from '../data/types';

export interface ClassProgress {
  level: number;
  exp: number;
  stats: Stats;
  statPoints: number;
  skillPoints: number;
  skillLv: Record<string, number>;
}

export interface SaveData {
  version: 1;
  classes: Record<string, ClassProgress>;
  gold: number;
  potions: { red: number; blue: number };
  cleared: string[];
}

const KEY = 'rbo-like-save-v1';

export const newSave = (): SaveData => ({ version: 1, classes: {}, gold: 0, potions: { red: 3, blue: 2 }, cleared: [] });

export const newProgress = (cls: ClassDef): ClassProgress => ({
  level: 1, exp: 0, stats: { ...cls.baseStats }, statPoints: 3, skillPoints: 1,
  skillLv: Object.fromEntries(cls.skills.map((s, i) => [s.id, i === 0 ? 1 : 0])),
});

export function loadSave(): SaveData {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) {
      const s = JSON.parse(raw) as SaveData;
      if (s.version === 1) return s;
    }
  } catch { /* storage unavailable */ }
  return newSave();
}

export function writeSave(s: SaveData) {
  try { localStorage.setItem(KEY, JSON.stringify(s)); } catch { /* storage unavailable */ }
}

export const exportCode = (s: SaveData) => btoa(unescape(encodeURIComponent(JSON.stringify(s))));

export function importCode(code: string): SaveData | null {
  try {
    const s = JSON.parse(decodeURIComponent(escape(atob(code.trim())))) as SaveData;
    return s.version === 1 ? s : null;
  } catch { return null; }
}

export const session = {
  save: loadSave(),
  classId: 'swordsman',
  stageId: '1-1',
};

export function progressFor(cls: ClassDef): ClassProgress {
  const s = session.save;
  if (!s.classes[cls.id]) s.classes[cls.id] = newProgress(cls);
  return s.classes[cls.id];
}

/** Frame counts of the per-class animation sheets exported to public/sprites/anim. Classes not listed use the static side sprite. */
export interface AnimSet { idle: number; walk: number; attack: number }

export const ANIMS: Record<string, AnimSet> = {
  swordsman: { idle: 4, walk: 6, attack: 4 },
  mage: { idle: 4, walk: 6, attack: 4 },
  merchant: { idle: 4, walk: 6, attack: 4 },
  acolyte: { idle: 3, walk: 6, attack: 4 },
  thief: { idle: 4, walk: 6, attack: 4 },
  archer: { idle: 4, walk: 6, attack: 4 },
};

export const animKey = (cls: string, label: keyof AnimSet, i: number) => `anim_${cls}_${label}_${i}`;

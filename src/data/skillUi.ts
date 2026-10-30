import type { SkillDef } from './types';

/**
 * 技能面板／長按技能介紹要顯示的欄位。每一列：Scaling 欄位名、標籤、顯示格式。
 * 面板依此表自動產生「Lv n → Lv n+1」的差異，不用手寫每個技能的升級說明。
 */
export type SkillStatFormat = 'mult' | 'int' | 'percent' | 'seconds' | 'px';

export interface SkillStatRow {
  /** Scaling field on SkillDef. */
  field: 'power' | 'sp' | 'count' | 'duration' | 'stunChance' | 'freezeChance' | 'defDown' | 'goldCost' | 'poison' | 'heal' | 'passiveValue' | 'dashDistance';
  label: string;
  format: SkillStatFormat;
  /** Only show when the skill has this type (omit = whenever the field exists). */
  onlyTypes?: SkillDef['type'][];
}

export const SKILL_STAT_ROWS: SkillStatRow[] = [
  { field: 'power', label: '威力', format: 'mult' },
  { field: 'count', label: '發數', format: 'int' },
  { field: 'duration', label: '持續', format: 'seconds' },
  { field: 'heal', label: '回復', format: 'percent' },
  { field: 'poison', label: '每秒毒傷', format: 'int' },
  { field: 'stunChance', label: '暈眩機率', format: 'percent' },
  { field: 'freezeChance', label: '凍結機率', format: 'percent' },
  { field: 'defDown', label: '減少防禦', format: 'percent' },
  { field: 'dashDistance', label: '距離', format: 'px' },
  { field: 'passiveValue', label: '機率／加成', format: 'percent', onlyTypes: ['passive'] },
  { field: 'sp', label: 'SP 消耗', format: 'int' },
  { field: 'goldCost', label: '金幣消耗', format: 'int' },
];

/** Buff fields shown for buff skills (from buffByLevel), label and format. */
export const BUFF_STAT_ROWS: { field: string; label: string; format: 'multPercent' | 'int' | 'flag' }[] = [
  { field: 'atkMult', label: '攻擊力', format: 'multPercent' },
  { field: 'matkMult', label: '魔攻', format: 'multPercent' },
  { field: 'aspdMult', label: '攻速', format: 'multPercent' },
  { field: 'moveMult', label: '移速', format: 'multPercent' },
  { field: 'allStats', label: '全屬性', format: 'int' },
  { field: 'shieldHits', label: '吸收次數', format: 'int' },
  { field: 'endure', label: '受擊不硬直', format: 'flag' },
  { field: 'hidden', label: '隱身', format: 'flag' },
];

/** Static labels for the non-numeric part of the card. */
export const SKILL_TYPE_LABEL: Record<SkillDef['type'], string> = {
  melee: '近戰', aoe: '範圍', projectile: '投射', buff: '增益', debuff: '減益', wall: '結界', strikes: '連擊', heal: '治癒', dash: '位移', passive: '被動',
};
export const ELEMENT_LABEL: Record<SkillDef['element'], string> = {
  neutral: '無', fire: '火', water: '水', wind: '風', earth: '地', holy: '聖', dark: '暗', poison: '毒',
};

/**
 * 封測版 UI 文案（繁中）。鍵名給 Unity 直接用；之後英文版加一份同鍵名的表。
 * 文案原則：短、口語、不用驚嘆號堆疊；教學提示一次只講一件事。
 */
export const STRINGS: Record<string, string> = {
  // ---- 標題與基地 ----
  'title.name': '口袋冒險團',
  'title.tap': '點擊開始',
  'title.beta': '封測版 · 感謝你一起試玩',
  'base.start': '出發',
  'base.unlocks': '解鎖',
  'base.settings': '設定',
  'base.stardust': '星塵',
  'base.feedback': '意見回饋',

  // ---- 選職業 ----
  'class.pick': '選一位冒險者',
  'class.locked': '尚未解鎖',
  'class.swordsman.blurb': '耐打、好上手。靠連段與裂甲斬正面硬碰。',
  'class.mage.blurb': '站遠一點。火球、冰矛、落雷，先手就是防守。',
  'class.archer.blurb': '邊退邊射。箭雨清小怪，擊退箭保距離。',

  // ---- 教學提示（房 1 依序出現，各顯示一次）----
  'hint.move': '左手搖桿移動，可以上下走位',
  'hint.attack': '連按攻擊打出連段，第三下會擊飛',
  'hint.skill': '技能鈕放技能，看 SP 夠不夠',
  'hint.dodge': '雙擊方向或按閃避，閃避中無敵',
  'hint.potion': 'HP 低了就喝藥水，開局有 2 瓶',
  'hint.door': '清完這一房，門會打開',

  // ---- 房間 ----
  'room.enter': '第 {n} 房',
  'room.boss': 'Boss',
  'room.clear': '清房！',
  'room.heal': 'HP 回復 {pct}%',
  'wave.next': '下一波',

  // ---- 三選一 ----
  'perk.pick': '選一個強化',
  'perk.rarity.common': '普通',
  'perk.rarity.rare': '稀有',
  'perk.rarity.epic': '史詩',
  'perk.stacks': '已拿 {n}/{max}',
  'perk.confirm': '就選這個',

  // ---- 結算 ----
  'result.clear': '通關！',
  'result.dead': '倒下了',
  'result.deadSub': '這一局拿到的星塵不會消失',
  'result.rooms': '清除房間 {n}/{total}',
  'result.time': '用時 {time}',
  'result.stardust': '獲得星塵 +{n}',
  'result.firstClear': '首次通關獎勵 +{n}',
  'result.nextUnlock': '再 {n} 星塵解鎖：{name}',
  'result.again': '再開一局',
  'result.base': '回基地',
  'result.seed': '種子 {seed}',

  // ---- 解鎖 ----
  'unlock.title': '用星塵解鎖',
  'unlock.buy': '解鎖（{cost} 星塵）',
  'unlock.owned': '已解鎖',
  'unlock.notEnough': '星塵不夠，再打一局吧',

  // ---- 設定 ----
  'settings.bgm': '音樂',
  'settings.sfx': '音效',
  'settings.vibration': '震動',
  'settings.resetSave': '清除存檔',
  'settings.resetConfirm': '星塵與解鎖都會消失，確定？',
  'settings.devUnlockAll': '開發者：全部解鎖（封測用）',

  // ---- 通用 ----
  'common.ok': '好',
  'common.cancel': '取消',
  'common.back': '返回',
  'common.pause': '暫停',
  'common.resume': '繼續',
  'common.quitRun': '放棄這一局',
  'common.quitConfirm': '已清房間的星塵照拿，確定放棄？',
};

/** Boss 登場與死亡台詞（封測只有果凍王）。 */
export const BOSS_LINES: Record<string, { enter: string; die: string }> = {
  jellyKing: { enter: '噗嚕……噗嚕嚕……', die: '噗……嚕……' },
};

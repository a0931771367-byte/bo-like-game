/**
 * Story beats shown before and after each stage. `{name}` is replaced by the class name.
 * All names and places are original to this game (see design doc: copyright principles).
 */
export interface StoryLine {
  who: 'narrator' | 'player' | 'npc' | 'enemy';
  /** Speaker label; the player's is filled in automatically. */
  name?: string;
  /** Texture key for the portrait: class `<id>_front` for the player (automatic), enemy `<id>_side`. */
  portrait?: string;
  text: string;
}

export interface StageStory { before: StoryLine[]; after: StoryLine[] }

const npc = (name: string, text: string): StoryLine => ({ who: 'npc', name, text });
const me = (text: string): StoryLine => ({ who: 'player', text });
const nar = (text: string): StoryLine => ({ who: 'narrator', text });
const foe = (name: string, portrait: string, text: string): StoryLine => ({ who: 'enemy', name, portrait, text });

export const STORY: Record<string, StageStory> = {
  '1-1': {
    before: [
      nar('王都「艾爾梅斯」的郊外，一向是新人冒險者練膽量的地方。'),
      nar('可是最近，連最溫馴的果凍怪都開始成群衝撞旅人……'),
      npc('公會接待員 莉婭', '{name}，你是今天唯一登記的冒險者。郊外的果凍怪不太對勁，能麻煩你去看看嗎？'),
      me('交給我吧。果凍怪而已，順便熱熱身。'),
      npc('公會接待員 莉婭', '別大意，有人說草原深處出現了「特別大隻」的……總之，平安回來。'),
    ],
    after: [
      foe('巨大果凍王', 'jellyKing_side', '噗嚕……噗嚕嚕……'),
      nar('果凍王崩成一灘，體內卻飄出一縷紫色的霧氣，往東方的林道散去。'),
      me('紫色的霧？果凍怪變得暴躁，該不會就是因為這個……'),
      npc('公會接待員 莉婭', '辛苦了！公會會把這件事往上報。東邊林道有商隊說遇襲，下一趟就拜託你了。'),
    ],
  },
  '1-2': {
    before: [
      nar('黃昏，通往東方的林道。商隊的車轍還很新，貨物卻散落一地。'),
      npc('商隊護衛 巴恩', '是公會派來的人嗎？太好了……那些果凍會噴火！還有蟲的殼硬得像鐵一樣！'),
      me('先把路清開，商隊才能撤走。你們躲到車後面。'),
      npc('商隊護衛 巴恩', '小心，霧越濃的地方怪物越多。'),
    ],
    after: [
      nar('夕陽沉入林道盡頭，散落的貨物終於能被收回車上。'),
      npc('商隊護衛 巴恩', '得救了……這條路通往「迷霧森林」，霧就是從那裡飄出來的。'),
      me('森林前面好像還有狼嚎的聲音。'),
      npc('商隊護衛 巴恩', '銀牙狼王的地盤。入夜以後，牠們會把路封住。'),
    ],
  },
  '1-3': {
    before: [
      nar('月夜。狼嚎從四面八方傳來，紫霧貼著地面流動。'),
      foe('銀牙狼王', 'wolf_side', '嗷嗚——！'),
      me('狼王……牠的眼睛也在發紫光。這些怪物不是天生就這麼兇的。'),
      nar('只要越過狼群，就能進入迷霧森林，找到霧氣的源頭。'),
    ],
    after: [
      foe('銀牙狼王', 'wolf_side', '嗚……嗚……'),
      nar('狼王倒下的瞬間，眼中的紫光熄滅，彷彿恢復成一隻普通的野狼，轉身消失在夜色裡。'),
      me('果然……牠們是被霧操控的。源頭就在森林裡。'),
      npc('公會接待員 莉婭', '{name}，公會正式發出迷霧森林的委託。霧的源頭，就拜託你了。'),
    ],
  },
  '2-1': {
    before: [
      nar('迷霧森林。陽光被層層樹冠切碎，紫霧貼著地面，像在呼吸。'),
      npc('森林守望者 艾爾', '站住……你是公會派來的？這片森林的蟲和花，最近都變得會主動攻擊人。'),
      me('霧是從森林深處飄出來的，我要去找源頭。'),
      npc('森林守望者 艾爾', '小心食人花，牠們不會動，但會吐種子。蜜蜂則是成群撲上來。'),
    ],
    after: [
      nar('林間的霧被打散了一些，隱約能看見深處有紫色的光在閃爍。'),
      npc('森林守望者 艾爾', '再往裡走是窪地，長滿毒蘑菇。別被牠們的孢子碰到。'),
      me('紫光……就是那裡了。'),
    ],
  },
  '2-2': {
    before: [
      nar('窪地積著一層紫色的水氣，毒蘑菇在霧裡緩慢地挪動。'),
      foe('毒蘑菇', 'mushroom_side', '……呼嚕……呼嚕……'),
      me('牠們在睡覺？不，是在等獵物靠近。'),
    ],
    after: [
      nar('穿過窪地，一棵巨大的古樹矗立在森林中央，樹幹上的裂縫透出紫光。'),
      npc('森林守望者 艾爾', '那是守護森林的母樹……它本該是溫柔的。霧讓它發狂了。'),
      me('那就把霧從它身上打出來。'),
    ],
  },
  '2-3': {
    before: [
      foe('樹精母體', 'treant_side', '誰……在吵……我的……睡眠……'),
      nar('古樹的枝幹緩緩抬起，燈籠一盞盞亮起紫光，整片森林發出低鳴。'),
      me('抱歉了，母樹。等打完這場，森林就會安靜下來。'),
    ],
    after: [
      foe('樹精母體', 'treant_side', '……霧……散了……謝謝你……'),
      nar('紫光從樹幹的裂縫中湧出，凝成一團，朝遠方的沙漠飛去。'),
      npc('森林守望者 艾爾', '它恢復了。那團霧飛往南方……沙漠遺跡的方向。'),
      me('看來事情沒那麼簡單。回報公會，接下來往南走。'),
      npc('森林守望者 艾爾', '{name}，森林會記得你。第 2 章到此告一段落，沙漠遺跡的委託之後開放。'),
    ],
  },
};

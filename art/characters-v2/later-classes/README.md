# 後期職業設計稿 v1（機關師、祭司、刺客）

依 `redesign-brief-v1.md` 第 3 節，畫風參考劍士 A 與法師三視圖。Higgsfield 生成，各 2 點。

| 檔案 | 職業 | 剪影鉤子 | 備註 |
|---|---|---|---|
| raw/artificer.png | 機關師（原商人） | 背上會走路的發條箱＋大扳手 | 側面圖多畫了一支扳手，拆件時只取一支 |
| raw/priest.png | 祭司（原補師） | 寬邊斗笠＋提燈杖 | 口袋藍外套、暖黃滾邊，無白兜帽長袍 |
| raw/assassin.png | 刺客（原盜賊） | 半張狐狸紙面具＋背後摺紙風箏 | 無拳刃，雙短刀 |

下一步：定稿後各出一張分件拆圖，用 `tools/seg.py`、`build.py` 做成骨架 PSB（同劍士流程）。

## 骨架分件 v1（已完成）

| 職業 | game PSB | 額外圖層 |
|---|---|---|
| 機關師 | artificer/artificer_game.psb（231×430） | prop＝發條箱（技能時展開成機械手臂的起點），weapon＝大扳手 |
| 祭司 | priest/priest_game.psb（234×430） | hat＝斗笠、cape＝披肩領、prop＝側背包、weapon＝提燈杖 |
| 刺客 | assassin/assassin_game.psb（261×430） | prop＝摺紙風箏（滑翔時展開）、scarf＝圍巾尾、weapon / weapon_2＝雙短刀 |

規格同劍士：PPU 300、pivot 底部中心、2D PSD Importer 用 Character Rig 匯入。組裝參數在 `tools/{artificer,priest,assassin}.json`，總覽圖 `later-classes-rigs.jpg`。

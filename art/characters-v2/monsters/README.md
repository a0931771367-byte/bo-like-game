# 吉祥物、果凍敵人、布丁巨像：去背素材 v1

來源：`art/style-board/raw/mascot-enemy-v2.png`、`boss-pudding-v2.png`（Higgsfield 生成，已記帳）。
切圖工具：`art/characters-v2/tools/cutout.py`（白底依空白欄切單隻、去白邊、透明 PNG）。

| 檔案 | 內容 | master 尺寸 | game 尺寸（PPU 300） |
|---|---|---|---|
| bobo_front / bobo_side | 吉祥物啵啵 正面／側面 | 約 600×740 | 高 240 px ≈ 0.8 單位 |
| jelly_front / jelly_side | 紫霧果凍敵人 正面／側面 | 約 600×750 | 高 240 px ≈ 0.8 單位 |
| colossus_front / colossus_side | 布丁巨像 正面／側面 | 約 1300×1430 | 高 900 px ≈ 3 單位 |

- `master/` 是原始解析度，`game/` 是遊戲用尺寸，PPU 300 與角色骨架同比例（玩家約 1.43 單位高）。
- Pivot 一律底部中心。
- 果凍類（啵啵、果凍敵人）不需要骨架，用 Unity 的縮放擠壓（squash & stretch）做 idle 彈跳、受擊、死亡即可；葉子／水晶可以之後再拆層。
- 側面圖朝右；朝左用 flipX。
- 布丁巨像要做 spoon_swing、jump_slam 這類大動作，另外出分件版（三層身體、湯匙、眼睛、煙、水晶）放在 `colossus-parts/`。

## 布丁巨像分件（colossus-parts/）

- `colossus_game.psb`（高 900 px，PPU 300 ≈ 3 單位）、`colossus_master.psb`（原尺寸），2D PSD Importer 用 Character Rig 匯入。
- 圖層：tier_bottom / tier_mid / tier_top（三層身體，各自綁一根骨頭做晃動和 jump_slam 壓扁）、spoon（spoon_swing 用，pivot 放在湯匙插入點）、eye_L / eye_R、eye_hurt_L / eye_hurt_R（預設隱藏，hurt 時 Sprite Swap）、smoke_a/b/c（煙霧飄動）、crystal_a～d（裝飾）。
- 原圖：`raw/parts-a.png`（Higgsfield 生成）、`raw/parts-a-4k.png`（Higgsfield 4K 放大）。組裝參數在 `tools/colossus.json`。

# 草原關卡背景素材 v1

風格沿用定稿背景 `art/style-board/raw/bg-pudding-hills-v3a.png`。`preview.jpg` 是組好的示意畫面（1920×1080）。

## 視差層 `parallax/`（高 1080 px，左右可無限重複）

| 檔案 | 寬 | 建議捲動速度（相對鏡頭） | 說明 |
|---|---|---|---|
| bg_sky.png | 5078 | 0.05 | 天空＋雲，不透明，最底層 |
| bg_far_castle.png | 1171×774 | 0.15 | 遠方城堡樹樁，單張，不重複，一個關卡放一次 |
| bg_far_hills.png | 5078 | 0.2 | 遠山帶（透明背景） |
| bg_mid_hills.png | 1788 | 0.5 | 中景山丘＋蘑菇（透明背景） |

- 每層都是「原圖＋左右鏡像」拼成，所以把同一張首尾相接重複即可無縫；SpriteRenderer 用 Tiled 模式或兩張輪替都行。
- 中景的山丘下緣會被地面蓋住，地面線以上約 160 px 開始擺。

## 地形磚 `tiles/`（用 SpriteRenderer Draw Mode = Tiled，Sprite Editor 設左右 border）

| 檔案 | 尺寸 | 左右 border（px） | 用途 |
|---|---|---|---|
| ground_grass.png | 560×384 | 220 / 220 | 地面頂層，中間 120 px 可無限延伸，兩端是收邊 |
| platform_grass.png | 656×200 | 268 / 268 | 浮空草地平台 |
| platform_jelly.png | 776×254 | 328 / 328 | 果凍單向平台 |
| soil_fill.png / soil_pebble.png | 256×256 | — | 地面以下的泥土填充，四邊無縫，可混著鋪 |

border 數值也寫在 `tiles/slice.json`。

## 前景道具 `props/`（13 件，透明 PNG，pivot 底部中心）

jelly_big / jelly_mid / jelly_small、bush_big / bush_small、flower_peach / flower_red / flower_blue、grass_tuft、mushroom_green / mushroom_lavender / mushroom_peach、rock。

## 工具與原圖

- `art/characters-v2/tools/parallax.py`（洋紅去背、城堡拆出、鏡像拼接）、`tiles.py`（地形磚、道具）。
- 原圖在 `raw/`（Higgsfield 生成，sky/far/mid 另有 4K 放大版）。

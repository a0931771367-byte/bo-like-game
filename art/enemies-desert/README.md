# 第 3 章沙漠敵人與石像王 遊戲用圖（11/1）

全部在 `out/`，畫布高 320、腳底在第 318 列、水平置中、朝右（沙蟹原稿朝左已鏡像）。直接放進 `Resources/Sprites/` 即可。

| id | 檔案 |
|---|---|
| sandworm | _side、_hurt、_knock、_down、_flat、_pile（地下沙堆） |
| cactusTurret | _side、_hurt、_knock、_down、_flat |
| mummy | _side、_hurt、_knock、_down、_flat、_pile（繃帶堆） |
| sandCrab | _side、_hurt、_knock、_down、_flat（石甲在右＝正面） |
| ruinGuardian | _side、_hurt、_knock、_down、_flat（崩成石堆）、_slam（砸地）、_beam（雷射，光束只畫起點）、_petrify（石化護甲）、_phase2（二階段碎甲） |

- 同一張圖稿的格子用同一比例，保留原畫的大小關係（例如沙蟲倒地比站姿寬）。
- 演出建議沿用 `art/enemies-hurt/README.md`。沙蟲竄出可用 _pile → _side 加向上位移 120 ms；石像震波、雷射光束可用 `art/third-party/kenney/` 粒子。
- 原稿 `raw/`、切塊 `parts/`、總覽 `preview.jpg`。工具 `art/characters-v2/tools/desertposes.py`。Higgsfield 6 張，12 點。

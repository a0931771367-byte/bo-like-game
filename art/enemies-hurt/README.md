# 魔物受擊／倒地姿勢圖（封測 8 隻）

罐子 10/30 回報「魔物攻擊倒地太假」。現況是 FighterView 把 `<id>_side.png` 轉 -60／-90 度加壓扁。
這裡提供每隻 4 張姿勢圖，畫布高 320 與 `<id>_side.png` 同比例、腳底同基準線、水平置中；比 side 寬的姿勢畫布左右對稱加寬，所以用 `spriteH / rect.height` 縮放時尺寸一致。

| 檔名 | 用途 | 建議演出 |
|---|---|---|
| `<id>_hurt.png` | Hurt | 換圖＋白閃 80 ms，不旋轉 |
| `<id>_knock.png` | Knock（空中） | 換圖，角度只跟拋物線 ±15 度 |
| `<id>_down.png` | Down／落地 | 落地瞬間 sy 0.85→1 回彈 120 ms |
| `<id>_flat.png` | Dying | down→flat 各 120 ms，再淡出 200 ms，影子一起縮 |

涵蓋 jelly、bug、wolf、bee、flower、mushroom、jellyKing、treant；fireJelly、ironBug、nightWolf、wolfLord 沿用對應底圖加 tint。
原圖 `raw/`（Higgsfield 8 張，16 點），工具 `art/characters-v2/tools/enemyposes.py`，`preview.jpg` 是 side＋4 姿勢對照。

# 跑步循環（取代舊版滑步走路格，封測用舊造型）

| 職業 id | 格數 | 畫布 | 備註 |
|---|---|---|---|
| swordsman | 8 | 485×244 | 第 3、7 格騰空 |
| mage | 8 | 462×237 | 長袍，步幅較小；原圖朝左，已鏡像成朝右（舊待機格也朝左，程式部已鏡像） |
| archer | 8 | 238×232 | |
| acolyte | 8 | 338×232 | |
| merchant | 6 | 403×268 | 原圖朝左，已鏡像成朝右；`idle-fix/` 是鏡像後的待機 4 格（舊待機格朝左、攻擊格朝右，原本就不一致） |
| thief | 8 | 310×242 | |

- 檔名 `<id>_walk_<n>.png`，畫布、腳底線、身高都對齊 Unity 現有 walk_0，直接覆蓋即可；騰空格已抬高 7 px。
- `<id>_run_preview.gif` 是 80 ms 一格的預覽。
- 原圖 `raw/`（Higgsfield 生成），工具 `art/characters-v2/tools/runframes.py`（劍士）、`runauto.py`（其餘）。

交付規則：所有角色格一律朝右，交付前逐職業比對 idle／walk／attack 朝向。
交付流程（QA 規則）：新增或修改角色動畫格後，先跑 `python3 tools/check_sprite_facing.py --sheet` 產生對照表，逐格確認朝右再 `--approve <格名>`，否則 CI 會擋。只交 Unity 用 PNG，網頁版已停止維護，不再出 webp。

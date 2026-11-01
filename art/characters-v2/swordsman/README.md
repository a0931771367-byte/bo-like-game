# 劍士（Swordsman）骨架動畫素材 v1

2026-10-28 ｜ 美術部。來源：Higgsfield 分件拆圖 raw/parts-a.png（以風格標準板劍士 A 為參考），自動去背切件後組裝。

| 檔案 | 用途 |
|---|---|
| swordsman_game.psb | **進 Unity 用**。原尺寸 879×1511 縮成 250×430 px，11 個圖層，已擺好站姿 |
| swordsman_master.psb | 原尺寸母檔（高 1511 px），之後重繪或加細節用 |
| parts/*.png | 各圖層單張 PNG（原尺寸，透明底） |
| layout.json | 各圖層在母檔畫布上的位置 |
| swordsman-assembled.png | 組裝後預覽 |
| raw/ | Higgsfield 原圖（parts-b 有關節標記圓點，未使用；side-idle 為側面參考） |

## 圖層（由後到前）
cape、hair_back、leg_L、leg_R、body、arm_L、weapon、arm_R、head、hair_front、shield

## Unity 設定
- 用 2D PSD Importer 匯入 `swordsman_game.psb`，Import Mode：**Individual Sprites (Mosaic)**，勾 **Character Rig**、**Use Layer Grouping** 關。
- Pixels Per Unit：**300**（角色高 430 px ≈ 1.43 單位，等於網頁版 PLAYER_H 132 px × 1.08）。
- Pivot：Bottom Center。
- 骨架建議：root(hip) → spine → neck → head；hip → leg_L / leg_R（各 2 節，膝蓋在腿片中段）；spine → arm_L / arm_R（各 2 節，手肘在手臂片中段）；arm_L 末端 → shield；arm_R 末端 → weapon；spine → cape（2–3 節做飄動）；head → hair_front、hair_back（1 節做晃動）。
- 手臂與腿是單片（含前臂/小腿），靠骨架權重彎曲；Auto Geometry 的 Outline Detail 調高到 40 以上，關節處多放頂點。
- 動畫事件名稱照 unity-art-import-spec.md：footstep、swing、hit、cast、land、hurt。

## 已知限制
- 臉是 3/4 正面，身體也是偏正面的 3/4 視角，符合 Q 版橫向動作遊戲常見做法；轉向用 X 軸翻轉。
- 盾牌已縮成 78%，避免遮住身體。
- 第二套部件（法師、遊俠）用同一流程製作。

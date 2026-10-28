# Unity 版（iOS）

Phaser 網頁版的 Unity 移植，目標 iOS App Store。排程與範圍見專案文件 `departments/engineering/unity-two-week-sprint.md`。

## 第一次打開

1. 安裝 Unity Hub，再裝 **Unity 6.3 LTS**，模組勾選 **iOS Build Support**。
2. Unity Hub → Add → Add project from disk → 選這個 `unity/` 資料夾，版本選 6.3 LTS。
3. 第一次開啟會：
   - 下載套件（URP、Input System、Cinemachine、Newtonsoft Json、Test Framework）。
   - 跳出「啟用新的 Input System」對話框 → 選 **Yes**，編輯器會重開。
   - 自動執行 `BoLike > Setup Project`：建立 URP 2D Renderer、設定 iOS 橫向。之後也能從選單手動重跑。
4. Window → General → Test Runner → EditMode → Run All，應該全部通過。

## 目錄

- `Assets/Resources/Data/` 由網頁版資料表匯出的 JSON（**不要手改**，改 `src/data/*.ts` 後重新匯出）
- `Assets/Scripts/Runtime/Data/` 資料結構與載入（`GameDb`）
- `Assets/Scripts/Runtime/Systems/` 數值公式、存檔、輸入
- `Assets/Scripts/Editor/` 專案設定與編輯器工具
- `Assets/Tests/EditMode/` 單元測試；`stats_vectors.json` 是 TypeScript 公式算出的對照值

## 更新資料

在 repo 根目錄：

```bash
npm install
node tools/export_unity_data.mjs
```

## 單位換算

資料沿用網頁版的像素與毫秒，程式內用 `Core.Units` 換算：100 像素 = 1 Unity 單位，毫秒 ÷ 1000 = 秒。

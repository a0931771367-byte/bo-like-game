# 劍與魔法亂鬥（暫名）

以 Ragnarok Battle Offline 玩法為藍本的單機 2D 橫向動作遊戲原型。電腦瀏覽器與手機瀏覽器（橫式）皆可遊玩。

- 引擎：Phaser 3 + TypeScript + Vite
- 試玩：推上 `main` 後由 GitHub Actions 自動部署到 GitHub Pages

## 開發

```bash
npm install
npm run dev      # 本機開發伺服器
npm run build    # 產出 dist/
```

## 目錄

- `src/data/` 職業、技能、敵人、關卡資料（改數值不用改程式）
- `src/entities/` 玩家、敵人、飛行道具與火牆
- `src/systems/` 輸入、數值公式、存檔
- `src/scenes/` 標題、加點、戰鬥、結算畫面
- `src/ui/` HUD、觸控按鍵、特效

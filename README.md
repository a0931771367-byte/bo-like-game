# 口袋冒險團 Pocket Adventurers

Q 版橫向動作 RPG，六種職業、一次買斷。本倉庫為網頁版原型（Phaser），正式版以 Unity 製作並上架 iOS。電腦瀏覽器與手機瀏覽器（橫式）皆可遊玩。

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

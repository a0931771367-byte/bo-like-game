import Phaser from 'phaser';
import { H, W } from './config';
import { GameScene } from './scenes/GameScene';
import { LevelUpScene } from './scenes/LevelUpScene';
import { ResultScene } from './scenes/ResultScene';
import { TitleScene } from './scenes/TitleScene';

const game = new Phaser.Game({
  type: Phaser.AUTO,
  parent: 'game',
  width: W,
  height: H,
  backgroundColor: '#0b0d14',
  scale: { mode: Phaser.Scale.FIT, autoCenter: Phaser.Scale.CENTER_BOTH },
  input: { activePointers: 4 },
  render: { antialias: true, roundPixels: false },
  scene: [TitleScene, LevelUpScene, GameScene, ResultScene],
});

(window as unknown as { __game: Phaser.Game }).__game = game;

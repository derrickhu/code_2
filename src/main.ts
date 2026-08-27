/**
 * 对上了 · 共用启动。微信 runtime 与浏览器 boot 都走这里。
 */
import '@/core/pixiUnsafeEvalPatch';
import { Game } from '@/core/Game';
import { SceneManager } from '@/core/SceneManager';
import { Platform } from '@/core/PlatformService';
import { preloadSliceArt } from '@/core/TextureLoader';
import { PlayScene } from '@/scenes/PlayScene';

declare const GameGlobal: any;
declare const wx: unknown;
declare const tt: unknown;

export async function startGame(canvas: unknown): Promise<void> {
  if (!canvas) {
    console.error('[main] 找不到 canvas');
    return;
  }

  Game.init(canvas);
  if (!Game.app?.renderer) {
    console.error('[main] 渲染器初始化失败');
    return;
  }

  preloadSliceArt();
  console.log(`[main] 启动 平台=${Platform.name} 版本=${__APP_VERSION__}`);

  SceneManager.register(new PlayScene());
  SceneManager.switchTo('play');

  await Game.warmScenePresent();
  if (typeof GameGlobal !== 'undefined') GameGlobal.__gameRendered = true;
}

function isNativeMinigame(): boolean {
  return typeof wx !== 'undefined' || typeof tt !== 'undefined';
}

if (isNativeMinigame()) {
  const canvas = typeof GameGlobal !== 'undefined' ? GameGlobal.canvas : null;
  startGame(canvas).catch((e) => {
    console.error('[main] 启动失败:', e);
  });
}

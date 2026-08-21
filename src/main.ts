/**
 * 对上了 · 小游戏入口
 *
 * 第一版只做一件事：进来就能对。没有登录、没有选关、没有 Loading。
 * 「十秒可懂」是直玩投放的硬门槛（docs/00-体验目标.md）。
 */
import '@/core/pixiUnsafeEvalPatch';
import { Game } from '@/core/Game';
import { SceneManager } from '@/core/SceneManager';
import { Platform } from '@/core/PlatformService';
import { PlayScene } from '@/scenes/PlayScene';

declare const GameGlobal: any;

async function main(): Promise<void> {
  const canvas = GameGlobal?.canvas ?? null;
  if (!canvas) {
    console.error('[main] 找不到 canvas');
    return;
  }

  Game.init(canvas);
  if (!Game.app?.renderer) {
    console.error('[main] 渲染器初始化失败');
    return;
  }

  console.log(`[main] 启动 平台=${Platform.name} 版本=${__APP_VERSION__}`);

  SceneManager.register(new PlayScene());
  SceneManager.switchTo('play');

  await Game.warmScenePresent();
  if (typeof GameGlobal !== 'undefined') GameGlobal.__gameRendered = true;
}

main().catch((e) => {
  console.error('[main] 启动失败:', e);
});

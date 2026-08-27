import { startGame } from '@/main';

const canvas = document.getElementById('game');
if (!canvas) {
  throw new Error('[web] 找不到 #game');
}
void startGame(canvas);

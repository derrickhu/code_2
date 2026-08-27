import { Platform } from '@/core/PlatformService';

const FILE: Record<string, string> = {
  tap: 'audio/tap.mp3',
  hit: 'audio/hit.mp3',
  stamp: 'audio/stamp.mp3',
  miss: 'audio/miss.mp3',
  yes: 'audio/yes.mp3',
  bgm: 'audio/bgm.mp3',
};

export type BuzzKind = 'tap' | 'rewrite' | 'miss' | 'stamp' | 'continue';

const KIND_FILE: Record<BuzzKind, string> = {
  tap: 'tap',
  rewrite: 'yes',
  stamp: 'stamp',
  miss: 'miss',
  continue: 'stamp',
};

const lastAt = new Map<string, number>();
const dead = new Set<string>();
const pool = new Map<string, ReturnType<typeof Platform.createInnerAudioContext>>();

let bgm: ReturnType<typeof Platform.createInnerAudioContext> = null;
let unlocked = false;

function acquire(name: string) {
  let ctx = pool.get(name);
  if (ctx) return ctx;
  ctx = Platform.createInnerAudioContext();
  if (!ctx) return null;
  ctx.src = FILE[name];
  ctx.volume = 0.78;
  pool.set(name, ctx);
  return ctx;
}

function playFile(name: string, gapMs = 70): void {
  const src = FILE[name];
  if (!src || dead.has(name)) return;
  const now = Date.now();
  if ((lastAt.get(name) ?? 0) + gapMs > now) return;
  lastAt.set(name, now);
  try {
    const ctx = acquire(name);
    if (!ctx) return;
    try { ctx.stop?.(); } catch { /* */ }
    try { ctx.seek?.(0); } catch { /* */ }
    ctx.volume = 0.78;
    ctx.play();
    ctx.onError?.(() => { dead.add(name); });
  } catch {
    /* 缺文件不炸 */
  }
}

export function unlockAudio(): void {
  if (unlocked) {
    startBgm();
    return;
  }
  unlocked = true;
  for (const name of ['tap', 'hit', 'stamp', 'miss', 'yes', 'bgm']) {
    if (FILE[name]) acquire(name);
  }
  startBgm();
}

export function startBgm(): void {
  if (bgm) {
    try { bgm.play(); } catch { /* */ }
    return;
  }
  const ctx = Platform.createInnerAudioContext();
  if (!ctx) return;
  ctx.src = FILE.bgm;
  ctx.loop = true;
  ctx.volume = 0.38;
  ctx.onError?.(() => { dead.add('bgm'); });
  bgm = ctx;
  try { ctx.play(); } catch { /* */ }
}

export function stopBgm(): void {
  try { bgm?.stop?.(); } catch { /* */ }
  try { bgm?.destroy(); } catch { /* */ }
  bgm = null;
}

export function buzz(kind: BuzzKind): void {
  unlockAudio();
  playFile(KIND_FILE[kind]);
  if (kind === 'rewrite') playFile('hit', 0);
  if (kind === 'rewrite' || kind === 'stamp' || kind === 'continue') {
    Platform.vibrateShort('medium');
    return;
  }
  if (kind === 'miss') {
    Platform.vibrateShort('heavy');
    return;
  }
  Platform.vibrateShort('light');
}

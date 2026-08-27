import { Platform } from '@/core/PlatformService';

const KEY = 'code2_run_memory';

export interface RunMemory {
  bestRewrites: number;
  lastWords: string[];
  daysPlayed: number;
  lastDoubled: boolean;
}

function empty(): RunMemory {
  return { bestRewrites: 0, lastWords: [], daysPlayed: 0, lastDoubled: false };
}

export function loadMemory(): RunMemory {
  try {
    const raw = Platform.getStorageSync(KEY);
    if (!raw) return empty();
    const parsed = JSON.parse(raw) as Partial<RunMemory>;
    return {
      bestRewrites: Math.max(0, Number(parsed.bestRewrites) || 0),
      lastWords: Array.isArray(parsed.lastWords) ? parsed.lastWords.map(String) : [],
      daysPlayed: Math.max(0, Number(parsed.daysPlayed) || 0),
      lastDoubled: Boolean(parsed.lastDoubled),
    };
  } catch {
    return empty();
  }
}

export function rememberRun(words: readonly string[], doubled: boolean): RunMemory {
  const prev = loadMemory();
  const score = words.length * (doubled ? 2 : 1);
  const next: RunMemory = {
    bestRewrites: Math.max(prev.bestRewrites, score),
    lastWords: [...words],
    daysPlayed: prev.daysPlayed + 1,
    lastDoubled: doubled,
  };
  try {
    Platform.setStorageSync(KEY, JSON.stringify(next));
  } catch {
    /* */
  }
  return next;
}

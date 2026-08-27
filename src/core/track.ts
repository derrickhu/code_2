/**
 * 切片验收埋点。先落 console + 本地环，不上报后台。
 * 事件名对齐 docs/01 §10。
 */
import { Platform } from '@/core/PlatformService';

const KEY = 'code2_track_ring';
const RING = 80;

export type TrackFields = Record<string, string | number | boolean | string[] | null | undefined>;

export interface TrackEvent {
  event: string;
  t: number;
  fields: TrackFields;
}

export function track(event: string, fields: TrackFields = {}): void {
  const row: TrackEvent = { event, t: Date.now(), fields };
  console.log(`[track] ${event}`, fields);
  try {
    const prev = readRing();
    prev.push(row);
    Platform.setStorageSync(KEY, JSON.stringify(prev.slice(-RING)));
  } catch {
    /* 模拟器写失败不挡局 */
  }
}

export function readRing(): TrackEvent[] {
  try {
    const raw = Platform.getStorageSync(KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as TrackEvent[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

/**
 * 本地贴图。微信必须走 createImage + src，不能 PIXI.Texture.from(路径)。
 */
import * as PIXI from 'pixi.js';
import { Platform } from '@/core/PlatformService';

const cache = new Map<string, PIXI.Texture>();
const missing = new Set<string>();
const inflight = new Set<string>();
const readyWatchers = new Set<() => void>();

export const SLICE_IMAGES = [
  'images/bg_desk.jpg',
  'images/paper_notice.jpg',
  'images/tile_paper.png',
  'images/stamp_seal.png',
  'images/ink_smear.png',
  'images/panel_paper.png',
] as const;

export function watchArt(fn: () => void): () => void {
  readyWatchers.add(fn);
  return () => { readyWatchers.delete(fn); };
}

function notifyReady(): void {
  for (const fn of readyWatchers) fn();
}

export function tex(path: string): PIXI.Texture | null {
  const hit = cache.get(path);
  if (hit?.baseTexture.valid) return hit;
  if (!missing.has(path)) kick(path);
  return null;
}

export function preloadSliceArt(): void {
  for (const p of SLICE_IMAGES) kick(p);
}

function kick(path: string): void {
  if (cache.has(path) || missing.has(path) || inflight.has(path)) return;
  inflight.add(path);

  const img = Platform.createImage();
  if (img) {
    img.onload = () => {
      try {
        cache.set(path, new PIXI.Texture(PIXI.BaseTexture.from(img)));
        notifyReady();
      } catch {
        missing.add(path);
      }
      inflight.delete(path);
    };
    img.onerror = () => {
      missing.add(path);
      inflight.delete(path);
    };
    img.src = path;
    return;
  }

  try {
    const t = PIXI.Texture.from(path);
    const ready = (): void => {
      if (t.baseTexture.valid) {
        cache.set(path, t);
        notifyReady();
      } else missing.add(path);
      inflight.delete(path);
    };
    if (t.baseTexture.valid) {
      ready();
      return;
    }
    t.baseTexture.once('loaded', ready);
    t.baseTexture.once('error', () => {
      missing.add(path);
      inflight.delete(path);
    });
  } catch {
    missing.add(path);
    inflight.delete(path);
  }
}

export function fillCover(
  g: PIXI.Graphics,
  texture: PIXI.Texture,
  x: number,
  y: number,
  w: number,
  h: number,
): void {
  const tw = texture.width || 1;
  const th = texture.height || 1;
  if (tw <= 1 || th <= 1) return;
  const scale = Math.max(w / tw, h / th);
  const matrix = new PIXI.Matrix();
  matrix.scale(scale, scale);
  matrix.translate(x + (w - tw * scale) / 2, y + (h - th * scale) / 2);
  g.beginTextureFill({ texture, matrix });
  g.drawRect(x, y, w, h);
  g.endFill();
}

export function fillContain(
  g: PIXI.Graphics,
  texture: PIXI.Texture,
  x: number,
  y: number,
  w: number,
  h: number,
): void {
  const tw = texture.width || 1;
  const th = texture.height || 1;
  if (tw <= 1 || th <= 1) return;
  const scale = Math.min(w / tw, h / th);
  const dw = tw * scale;
  const dh = th * scale;
  const ox = x + (w - dw) / 2;
  const oy = y + (h - dh) / 2;
  const matrix = new PIXI.Matrix();
  matrix.scale(scale, scale);
  matrix.translate(ox, oy);
  g.beginTextureFill({ texture, matrix });
  g.drawRect(ox, oy, dw, dh);
  g.endFill();
}

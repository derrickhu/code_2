import { Platform } from '@/core/PlatformService';
import { track } from '@/core/track';

/** 真机填 adUnitId；空字符串走 Platform 的 700ms mock，视为播完。 */
export const AD_UNITS = {
  continue: '',
  double: '',
} as const;

export type AdPlacement = keyof typeof AD_UNITS;

export async function playAd(placement: AdPlacement): Promise<boolean> {
  track('ad_show', { placement });
  const ok = await Platform.showRewardedVideo(AD_UNITS[placement]);
  track('ad_close', { placement, completed: ok });
  return ok;
}

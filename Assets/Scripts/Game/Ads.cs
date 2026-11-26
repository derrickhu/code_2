#if (UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || TUANJIE_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR
#define LIANGSHAN_WX_AD
#endif
using System;
using Liangshan.Sim;
using UnityEngine;
#if LIANGSHAN_WX_AD
using WeChatWASM;
#endif

namespace Liangshan
{
    /// <summary>
    /// 微信小游戏流量主广告位清单（AppId: wxde332003e4d0ef23）。
    /// 在 mp.weixin.qq.com「流量主 -> 广告管理」注册后，把对应的 adunit-xxxxxxxxxxxxxxxx 填入下方常量即可生效。
    /// 任一常量留空时自动回退到 DefaultRewarded（或 CloudConfig.RewardedAdUnitId）；全部留空时走内置 2 秒演示片。
    /// </summary>
    public static class AdUnits
    {
        /// <summary>兜底通用激励视频位（若初期只建 1 个激励视频位做测试，填这里即可覆盖全部激励点）。</summary>
        public const string DefaultRewarded = "";

        // ==================== 激励视频广告位（共 9 个业务分组） ====================

        /// <summary>1. 战败复活（每局限 1 次：寨墙回半血并震退全场官军）</summary>
        public const string Revive = "";

        /// <summary>2. 结算双倍（胜利结算双倍领取 / 二败离开抚恤双倍）</summary>
        public const string SettleDouble = "adunit-d3fa4463a6960c63";

        /// <summary>3. 局内强化选卡（强化三选一刷新 / 天降神运三张全收）</summary>
        public const string BattlePerk = "adunit-a84e13ff308b739c";

        /// <summary>4. 宝箱摇奖重转（生辰纲老虎机鸿运重转）</summary>
        public const string ReelJackpot = "adunit-6ed8b0cfe451ccb0";

        /// <summary>5. 战场特权辅助（卡关 2 星援军入场 / 战中换将令 / 今日全天 ×2.0 极速）</summary>
        public const string BattleAssist = "adunit-28f2074c86bfce4d";

        /// <summary>6. 招募与寻宝（聚义厅每日免费单抽 / 兽魂祭坛每日免费寻宝）</summary>
        public const string GachaPull = "adunit-299e6b43b1c6e609";

        /// <summary>7. 锻造开箱（无库存时每日免费开启神兵装备箱）</summary>
        public const string GearBox = "adunit-49bb014d506c40f6";

        /// <summary>8. 体力与集市补给（体力弹窗 +30 / 集市免费体力 / 集市免费元宝 / 每日福利再领一份）</summary>
        public const string Supply = "adunit-a5c4648f0684f5dd";

        /// <summary>9. 巡山与副本加次（快速巡山 2 小时收益 / 日常挑战加次数 / 智取生辰纲加次数）</summary>
        public const string PatrolDungeon = "adunit-62f998d1facdad7d";

        // ==================== 插屏广告位（共 1 个） ====================

        /// <summary>10. 战后返回插屏（通关第 3 关后、未看结算视频返回主界面时低频展示）</summary>
        public const string InterstitialReturn = "";

        // ==================== 原生模板广告位（可选 1 个，留空不展示） ====================

        /// <summary>11. 主界面悬浮单格原生模板（可选；留空完全不创建，不影响界面）</summary>
        public const string CustomFloat = "";

        /// <summary>游戏内全部 18 个激励视频细分埋点场景。</summary>
        public static readonly string[] AllRewardedScenes =
        {
            "revive",
            "double", "lose_double",
            "reroll", "take_all",
            "reel_jackpot",
            "start_2star", "swap_unit", "speed2",
            "recruit", "totem_pull",
            "gear_box",
            "stamina", "shop_stamina", "shop_ingot", "welfare_double",
            "idle", "daily_extra", "escort_extra",
        };

        /// <summary>按细分场景解析微信激励视频 adUnitId。</summary>
        public static string RewardedOf(string scene)
        {
            string specific;
            switch (scene)
            {
                case "revive":
                    specific = Revive;
                    break;
                case "double":
                case "lose_double":
                    specific = SettleDouble;
                    break;
                case "reroll":
                case "take_all":
                    specific = BattlePerk;
                    break;
                case "reel_jackpot":
                    specific = ReelJackpot;
                    break;
                case "start_2star":
                case "swap_unit":
                case "speed2":
                    specific = BattleAssist;
                    break;
                case "recruit":
                case "totem_pull":
                    specific = GachaPull;
                    break;
                case "gear_box":
                    specific = GearBox;
                    break;
                case "stamina":
                case "shop_stamina":
                case "shop_ingot":
                case "welfare_double":
                    specific = Supply;
                    break;
                case "idle":
                case "daily_extra":
                case "escort_extra":
                    specific = PatrolDungeon;
                    break;
                default:
                    specific = "";
                    break;
            }
            if (!string.IsNullOrEmpty(specific)) return specific;
            if (!string.IsNullOrEmpty(DefaultRewarded)) return DefaultRewarded;
            return CloudConfig.RewardedAdUnitId ?? "";
        }

        /// <summary>场景所属的业务组键名（未填真实 adUnitId 时用于经分归类）。</summary>
        public static string GroupKeyOf(string scene)
        {
            switch (scene)
            {
                case "revive": return "reward_revive";
                case "double":
                case "lose_double": return "reward_settle_double";
                case "reroll":
                case "take_all": return "reward_battle_perk";
                case "reel_jackpot": return "reward_reel_jackpot";
                case "start_2star":
                case "swap_unit":
                case "speed2": return "reward_battle_assist";
                case "recruit":
                case "totem_pull": return "reward_gacha_pull";
                case "gear_box": return "reward_gear_box";
                case "stamina":
                case "shop_stamina":
                case "shop_ingot":
                case "welfare_double": return "reward_supply";
                case "idle":
                case "daily_extra":
                case "escort_extra": return "reward_patrol_dungeon";
                default: return "reward_default";
            }
        }
    }

    /// <summary>
    /// 统一广告服务：
    /// 1. 激励视频（WXRewardedVideoAd）：单实例现建现拆 + multiton，规避多广告位切换不发奖与鸿蒙多实例卡顿；播放期自动静音恢复。
    /// 2. 插屏广告（WXInterstitialAd）：新手前 3 关免弹、启动 180s 免弹、间隔 180s、看完激励视频 90s 免弹、每日上限 6 次。
    /// 3. 原生模板（WXCustomAd）：预留悬浮单格，仅在填写 AdUnits.CustomFloat 且处于出征主页时展示。
    /// </summary>
    public static class Ads
    {
        /// <summary>自检截图模式缩短演示时长并关闭插屏。</summary>
        public static bool Fast;

        public const int InterstitialMinCleared = 3;
        public const float InterstitialBootGraceSec = 180f;
        public const float InterstitialIntervalSec = 180f;
        public const float InterstitialAfterRewardSec = 90f;

        static float _bootAt = -1f;
        static float _lastRewardedAt = -999f;
        static float _lastInterstitialAt = -999f;

        static float BootAt
        {
            get
            {
                if (_bootAt < 0f) _bootAt = Time.realtimeSinceStartup;
                return _bootAt;
            }
        }

#if LIANGSHAN_WX_AD
        static WXRewardedVideoAd _wxAd;
        static Action<bool> _wxClosed;
        static bool _wxBusy;
        static bool _wxDidShow;
        static int _wxGen;
        static bool _onShowHooked;

        static WXInterstitialAd _interstitial;
        static bool _interstitialBusy;

        static WXCustomAd _customAd;
        static bool _customWantShow;
#endif

        /// <summary>播放激励视频：完整看完才触发 done 并发放奖励、累计每日看广告任务。</summary>
        public static void Show(string scene, Action done, Action onFail = null)
        {
            _ = BootAt;
            string unit = AdUnits.RewardedOf(scene);
            string trackUnit = !string.IsNullOrEmpty(unit) ? unit : AdUnits.GroupKeyOf(scene);
            Analytics.AdRequest(scene, trackUnit, "reward");

#if LIANGSHAN_WX_AD
            // 微信开发者工具播不出激励视频，Show/Load 经常永不回调，按钮会一直停在「广告加载中」。
            if (Gm.Allowed)
            {
                if (_wxBusy) { SettleRewarded(_wxGen); DropRewarded(); }
                ShowFallback(scene, trackUnit, done);
                return;
            }
            if (!string.IsNullOrEmpty(unit))
            {
                if (_wxBusy)
                {
                    Ui.Toast("广告加载中，请稍候", 180);
                    return;
                }
                HookWxOnShow();
                try
                {
                    DropRewarded();
                    _wxBusy = true;
                    _wxDidShow = false;
                    int gen = ++_wxGen;

                    _wxAd = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
                    {
                        adUnitId = unit,
                        multiton = true,
                    });
                    _wxAd.OnClose(r =>
                    {
                        if (!_wxBusy || gen != _wxGen)
                        {
                            DropRewarded();
                            return;
                        }
                        bool ended = r == null || r.isEnded;
                        var cb = _wxClosed;
                        SettleRewarded(gen);
                        DropRewarded();
                        cb?.Invoke(ended);
                    });
                    _wxAd.OnError(e =>
                    {
                        string msg = e != null ? e.errCode + " " + e.errMsg : "unknown";
                        Debug.LogWarning("[Ads] wx reward error (" + scene + "): " + msg);
                        if (_wxBusy && !_wxDidShow && gen == _wxGen)
                            FailWx(scene, trackUnit, gen, msg, onFail);
                    });

                    _wxClosed = ended =>
                    {
                        Analytics.AdClose(scene, ended, trackUnit, "reward");
                        if (!ended)
                        {
                            Ui.Toast("完整观看视频才可获得奖励", 200);
                            onFail?.Invoke();
                            return;
                        }
                        _lastRewardedAt = Time.realtimeSinceStartup;
                        GrantAdTask();
                        done?.Invoke();
                    };

                    _wxAd.Show(_ => OnRewardedShown(scene, trackUnit, gen), _ =>
                    {
                        if (!_wxBusy || gen != _wxGen || _wxAd == null) return;
                        _wxAd.Load(
                            __ =>
                            {
                                if (!_wxBusy || gen != _wxGen || _wxAd == null) return;
                                _wxAd.Show(
                                    ___ => OnRewardedShown(scene, trackUnit, gen),
                                    ___ => FailWx(scene, trackUnit, gen, "show_after_load_failed", onFail));
                            },
                            err => FailWx(scene, trackUnit, gen, err != null ? err.errCode + " " + err.errMsg : "load_failed", onFail));
                    });
                    if (Game.I != null)
                        Tween.Run(Game.I, 8f, _ => { }, () =>
                        {
                            if (_wxBusy && !_wxDidShow && gen == _wxGen)
                                FailWx(scene, trackUnit, gen, "timeout", onFail);
                        });
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ads] wx create failed: " + e.Message);
                    SettleRewarded(_wxGen);
                    DropRewarded();
                }
            }
#endif
            ShowFallback(scene, trackUnit, done);
        }

        /// <summary>在自然转场点（如未看双倍视频从结算页返回主界面）尝试展示插屏广告。不满足频控时静默跳过。</summary>
        public static bool TryInterstitial(string scene = "return_home")
        {
            if (Application.isEditor || Profile.Volatile || Fast) return false;
            var s = Profile.S;
            if (s == null || s.Cleared < InterstitialMinCleared) return false;
            if (s.AdInterstitial >= Meta.InterstitialDailyMax) return false;

            float now = Time.realtimeSinceStartup;
            if (now - BootAt < InterstitialBootGraceSec) return false;
            if (_lastInterstitialAt > 0f && now - _lastInterstitialAt < InterstitialIntervalSec) return false;
            if (_lastRewardedAt > 0f && now - _lastRewardedAt < InterstitialAfterRewardSec) return false;

#if LIANGSHAN_WX_AD
            string unit = AdUnits.InterstitialReturn;
            if (string.IsNullOrEmpty(unit) || _wxBusy || _interstitialBusy) return false;
            try
            {
                DropInterstitial();
                _interstitialBusy = true;
                Analytics.AdRequest(scene, unit, "interstitial");
                _interstitial = WX.CreateInterstitialAd(new WXCreateInterstitialAdParam { adUnitId = unit });
                _interstitial.OnClose(() =>
                {
                    _interstitialBusy = false;
                    Sfx.SetAdMute(false);
                    Analytics.AdClose(scene, true, unit, "interstitial");
                    DropInterstitial();
                });
                _interstitial.OnError(e =>
                {
                    string msg = e != null ? e.errCode + " " + e.errMsg : "unknown";
                    Debug.LogWarning("[Ads] wx interstitial error: " + msg);
                    _interstitialBusy = false;
                    Sfx.SetAdMute(false);
                    Analytics.AdError(scene, msg, unit, "interstitial");
                    DropInterstitial();
                });
                _interstitial.Load(_ =>
                {
                    if (_interstitial == null || _wxBusy) { _interstitialBusy = false; DropInterstitial(); return; }
                    _interstitial.Show(__ =>
                    {
                        Sfx.SetAdMute(true);
                        _lastInterstitialAt = Time.realtimeSinceStartup;
                        if (Profile.S != null)
                        {
                            Profile.S.AdInterstitial++;
                            Profile.Save();
                        }
                        Analytics.AdShow(scene, unit, "interstitial");
                    }, __ =>
                    {
                        _interstitialBusy = false;
                        Sfx.SetAdMute(false);
                        DropInterstitial();
                    });
                }, _ =>
                {
                    _interstitialBusy = false;
                    DropInterstitial();
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] interstitial failed: " + e.Message);
                _interstitialBusy = false;
                Sfx.SetAdMute(false);
                DropInterstitial();
            }
#endif
            return false;
        }

        /// <summary>同步主界面可选单格原生模板广告显隐（未填 AdUnits.CustomFloat 时完全不创建）。</summary>
        public static void SyncCustomFloat(bool visible)
        {
#if LIANGSHAN_WX_AD
            string unit = AdUnits.CustomFloat;
            if (string.IsNullOrEmpty(unit)) return;
            _customWantShow = visible;
            try
            {
                if (!visible)
                {
                    if (_customAd != null) _customAd.Hide(_ => { }, _ => { });
                    return;
                }
                if (_customAd == null)
                {
                    var win = WX.GetWindowInfo();
                    int left = win != null ? Mathf.Max(8, (int)win.windowWidth - 76) : 12;
                    int top = win != null ? Mathf.Max(96, (int)(win.windowHeight * 0.42f)) : 260;
                    _customAd = WX.CreateCustomAd(new WXCreateCustomAdParam
                    {
                        adUnitId = unit,
                        adIntervals = 60,
                        style = new CustomStyle { left = left, top = top, width = 68 },
                    });
                    _customAd.OnLoad(_ =>
                    {
                        if (_customWantShow && _customAd != null)
                            _customAd.Show(__ => Analytics.AdShow("home_custom", unit, "custom"), __ => { });
                    });
                    _customAd.OnError(e =>
                    {
                        Debug.LogWarning("[Ads] custom ad error: " + (e != null ? e.errCode + " " + e.errMsg : "?"));
                    });
                    return;
                }
                _customAd.Show(_ => { }, _ => { });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] custom ad exception: " + e.Message);
            }
#endif
        }

        static void GrantAdTask()
        {
            if (Profile.S == null) return;
            Meta.Progress(Profile.S, Act.Ad);
            Profile.Save();
        }

#if LIANGSHAN_WX_AD
        static void OnRewardedShown(string scene, string trackUnit, int gen)
        {
            if (!_wxBusy || gen != _wxGen || _wxDidShow) return;
            _wxDidShow = true;
            _lastRewardedAt = Time.realtimeSinceStartup;
            Sfx.SetAdMute(true);
            Analytics.AdShow(scene, trackUnit, "reward");
        }

        static void FailWx(string scene, string trackUnit, int gen, string errMsg, Action onFail)
        {
            if (!_wxBusy || gen != _wxGen) return;
            SettleRewarded(gen);
            DropRewarded();
            Analytics.AdError(scene, errMsg, trackUnit, "reward");
            Analytics.AdClose(scene, false, trackUnit, "reward");
            Ui.Toast("暂无可播放的广告，请稍后再试", 200);
            onFail?.Invoke();
        }

        static void SettleRewarded(int gen)
        {
            if (!_wxBusy || gen != _wxGen) return;
            _wxBusy = false;
            _wxDidShow = false;
            _wxGen++;
            _wxClosed = null;
            Sfx.SetAdMute(false);
        }

        static void DropRewarded()
        {
            var ad = _wxAd;
            _wxAd = null;
            if (ad == null) return;
            try { ad.Destroy(); }
            catch (Exception) { }
        }

        static void DropInterstitial()
        {
            var ad = _interstitial;
            _interstitial = null;
            if (ad == null) return;
            try { ad.Destroy(); }
            catch (Exception) { }
        }

        static void HookWxOnShow()
        {
            if (_onShowHooked) return;
            _onShowHooked = true;
            try
            {
                WX.OnShow(_ =>
                {
                    // 极端情况下宿主关闭广告回到前台却丢了 OnClose，延迟兜底解封音源与忙状态。
                    if (!_wxBusy || !_wxDidShow || Game.I == null) return;
                    int snapGen = _wxGen;
                    Tween.Run(Game.I, 2.2f, __ => { }, () =>
                    {
                        if (_wxBusy && _wxDidShow && snapGen == _wxGen)
                        {
                            var cb = _wxClosed;
                            SettleRewarded(snapGen);
                            DropRewarded();
                            cb?.Invoke(true);
                        }
                    });
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ads] WX.OnShow hook failed: " + e.Message);
            }
        }
#endif

        static void ShowFallback(string scene, string trackUnit, Action done)
        {
            Analytics.AdShow(scene, trackUnit, "reward");
            var layer = Ui.Rect(Ui.Root, "ad");
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsLastSibling();
            Ui.Fill(layer, new Color(0, 0, 0, 0.92f), true);
            Ui.Icon(layer, "ic_ad", 160, new Vector2(0, 160));
            var t = Ui.Label(layer, "", 44, Color.white, new Vector2(0, -40), new Vector2(900, 200), TextAnchor.MiddleCenter, Fonts.Heavy);
            float dur = Fast ? 0.5f : 2f;
            Tween.Run(layer, dur, k => t.text = $"（演示）激励视频播放中…\n{Mathf.CeilToInt(dur * (1 - k))}", () =>
            {
                _lastRewardedAt = Time.realtimeSinceStartup;
                Analytics.AdClose(scene, true, trackUnit, "reward");
                UnityEngine.Object.Destroy(layer.gameObject);
                GrantAdTask();
                done?.Invoke();
            });
        }
    }
}

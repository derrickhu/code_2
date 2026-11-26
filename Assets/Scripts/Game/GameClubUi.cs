#if (UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || TUANJIE_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR
#define LIANGSHAN_WX_CLUB
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;
#if LIANGSHAN_WX_CLUB
using WeChatWASM;
#endif

namespace Liangshan
{
    /// <summary>
    /// 微信游戏圈福利页（全手绘九宫格皮肤组装，零程序绘制图形）：
    /// 1) 顶部鎏金铆钉框（gacha_dock_gold）镶嵌智多星吴用国风主题原画（club_banner）；
    /// 2) 中段木栓挂板（quest_tray）承载入圈说明与「前往游戏圈」原生按钮热区（前往本身不发奖，真实加圈才发奖）；
    /// 3) 下部标准三栏任务队列（row / row_claim / row_done + bar_bg）呈现入圈破冰、累计点赞/评论三级前置链与今日日常；
    /// 4) 底部挂接图鉴同款手绘底页签（codex_btab_on / codex_btab_off）与「每日福利」无缝互切。
    /// </summary>
    public static partial class MetaUi
    {
        public static void WelfareBottomTabs(Transform p, Transform layer, float panelHalfH, int activeTab)
        {
            int today = Profile.Today;
            int welBadge = (Meta.CanWelfare(S, today) ? 1 : 0) + (Meta.CanAdWelfare(S, today) ? 1 : 0);
            int clubBadge = Meta.ClubClaimableCount(S, today) + (S.ClubJoined == 0 && S.ClubSeen == 0 ? 1 : 0);

            string[] names = { "每日福利", "游戏圈" };
            int[] badges = { welBadge, clubBadge };
            const float btabW = 248f, btabH = 98f, btabGap = 272f;
            RectTransform activeRt = null;

            for (int i = 0; i < 2; i++)
            {
                int ti = i;
                bool on = i == activeTab;
                var bt = Ui.Rect(p, "wbtab" + i);
                bt.anchoredPosition = new Vector2((i - 0.5f) * btabGap, -panelHalfH - (on ? 44f : 48f));
                bt.sizeDelta = new Vector2(btabW, btabH);
                if (on) activeRt = bt;
                else bt.SetAsFirstSibling();

                var face = Ui.Skin(bt, on ? "codex_btab_on" : "codex_btab_off", new Vector2(btabW, btabH), Vector2.zero, 1f, null, "face");
                if (on)
                    Ui.Label(bt, names[i], 33, WarmTitle, Vector2.zero, new Vector2(216f, 52f), TextAnchor.MiddleCenter, Fonts.Heavy);
                else
                    Ui.Outlined(bt, names[i], 31, Pal.Hex("D6E6F2"), new Vector2(0f, -4f), new Vector2(216f, 52f), Pal.Hex("1D3346"), 1.8f);

                if (badges[i] > 0) Dot(bt, new Vector2(btabW * 0.5f - 22f, btabH * 0.5f - 32f), badges[i]);
                Ui.Tap(face, () =>
                {
                    if (ti == activeTab) return;
                    if (ti == 0) { CloseIfOpen(layer, "club"); Welfare(layer); }
                    else { CloseIfOpen(layer, "welfare"); GameClub(layer); }
                }, bt);
            }
            if (activeRt != null) activeRt.SetAsLastSibling();
        }

        static void CloseIfOpen(Transform layer, string key)
        {
            var old = layer.Find("modal_" + key);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        public static void GameClub(Transform layer)
        {
            int today = Profile.Today;
            Meta.EnsureClubDay(S, today);
            if (S.ClubSeen == 0)
            {
                S.ClubSeen = 1;
                Save();
            }

            float ph = Mathf.Clamp(Ui.Height - Ui.SafeTop - 224f, 1340f, 1520f);
            var m = Modal(layer, "club", "微信游戏圈", new Vector2(980f, ph), out var p, Pal.Gold, 24f);
            float top = ph * 0.5f;

            // 底部手绘挂签先建，确保未选中页签自然压在面板底沿后方
            WelfareBottomTabs(p, layer, top, 1);

            // 1. 顶部鎏金包角原画框（gacha_dock_gold 九宫格金属框 + 完整无遮挡国风插画）
            const float banW = 892f, banH = 442f;
            float banY = top - 280f;
            Ui.Skin(p, "gacha_dock_gold", new Vector2(banW + 24f, banH + 24f), new Vector2(0f, banY), 1f, null, "banFrame");

            var banVp = Ui.Rect(p, "banViewport");
            banVp.anchoredPosition = new Vector2(0f, banY);
            banVp.sizeDelta = new Vector2(banW, banH);
            banVp.gameObject.AddComponent<RectMask2D>();

            var banSpr = Art.Img("club_banner") ?? Art.Img("recruit_banner");
            var banImg = Ui.Box(banVp, new Vector2(banW, banW * 603f / 1080f), new Vector2(0f, 6f), Color.white, banSpr, "art");
            banImg.type = Image.Type.Simple;

            // 2. 中段木栓暖米挂板（quest_tray 手绘皮肤）：左侧宋江金框头像 + 中部入圈说明 + 右侧「前往游戏圈」
            float guideY = banY - banH * 0.5f - 78f;
            var guide = Ui.Rect(p, "guideDeck");
            guide.anchoredPosition = new Vector2(0f, guideY);
            guide.sizeDelta = new Vector2(900f, 114f);
            Ui.Skin(guide, "quest_tray", new Vector2(900f, 114f), Vector2.zero, 1f, null, "tray");

            Slot(guide, "slot_orange", 88f, new Vector2(-390f, 0f));
            Ui.Icon(guide, "lord_songjiang", 78f, new Vector2(-390f, 2f));

            bool joined = S.ClubJoined != 0;
            Ui.Label(guide, "加入梁山游戏圈 · 领天书神兵助战礼", 28, WarmTitle,
                new Vector2(-68f, 20f), new Vector2(516f, 38f), TextAnchor.MiddleLeft, Fonts.Heavy);
            Fit(Ui.Label(guide, joined ? "已加入游戏圈 · 圈内点赞评论返回自动同步领奖" : "加圈即领 200元宝 + 3招募令 + 神兵装备箱",
                22, joined ? Pal.Hex("1E7A40") : WarmSub, new Vector2(-68f, -20f), new Vector2(516f, 34f), TextAnchor.MiddleLeft, Fonts.Heavy), 18);

            RectTransform goBtn = null;
            goBtn = Ui.Btn(guide, "前往游戏圈", new Vector2(236f, 80f), new Vector2(320f, 0f), Pal.Blue, () =>
            {
                if (Application.isEditor || Gm.Allowed)
                {
                    Meta.SimulateClubStep(S, Profile.Today);
                    Save();
                    Ui.Toast("开发工具模拟：已同步加圈及点赞/评论/发帖+1", 220);
                    GameClub(layer);
                    return;
                }
                Ui.Toast("正在前往微信游戏圈…", 160);
            }, 30);

            // 3. 下部微凹槽任务滚动列表：可领优先 → 已解锁进行中 → 前置锁链待解锁 → 已领取沉底
            var order = new List<int>(Meta.ClubTasks.Length);
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < Meta.ClubTasks.Length; i++)
                {
                    bool taken = Meta.ClubTaskTaken(S, today, i);
                    bool unlocked = Meta.ClubTaskUnlocked(S, i);
                    bool can = Meta.CanClaimClubTask(S, today, i);
                    if (pass == 0 && can) order.Add(i);
                    else if (pass == 1 && unlocked && !taken && !can) order.Add(i);
                    else if (pass == 2 && !unlocked && !taken) order.Add(i);
                    else if (pass == 3 && taken) order.Add(i);
                }
            }

            float listTop = guideY - 72f;
            float listBot = -top + 38f;
            const float step = 146f;
            var content = ScrollList(p, 900f, listTop, listBot, order.Count * step + 14f);
            for (int k = 0; k < order.Count; k++)
            {
                int idx = order[k];
                var r = TopChild(content, "clubTask" + idx, -74f - k * step, new Vector2(884f, 136f));
                BuildClubTaskRow(r, idx, today, layer, goBtn, () =>
                {
                    var got = Meta.ClaimClubTask(S, Profile.Today, idx);
                    if (got == null) return;
                    Save();
                    GameClub(layer);
                    ShowRewards(layer, "游戏圈奖励", got);
                });
            }

            // 4. 真机挂载微信原生透明游戏圈按钮 + 隐私校验与切回自动解密同步
            var watcher = m.gameObject.AddComponent<GameClubWatcher>();
            watcher.Setup(m, layer, goBtn);
        }

        static void BuildClubTaskRow(RectTransform r, int idx, int today, Transform tipLayer, RectTransform goBtn, Action onClaim)
        {
            var t = Meta.ClubTasks[idx];
            bool taken = Meta.ClubTaskTaken(S, today, idx);
            bool unlocked = Meta.ClubTaskUnlocked(S, idx);
            bool can = Meta.CanClaimClubTask(S, today, idx);
            int cur = Mathf.Min(t.Need, Meta.ClubTaskValue(S, today, idx));

            const float w = 884f;
            string rowSkin = can ? "row_claim" : (!unlocked || taken) ? "row_done" : "row";
            Ui.Skin(r, rowSkin, new Vector2(w, 136f), Vector2.zero, 1f, null, "card");

            // 左栏：3 枚等大 92px 高清雕花品质道具格（与每日任务/七日目标 100% 同规对齐）
            const float tileSz = 92f, tileStep = 100f;
            float x = -w * 0.5f + 66f;
            if (t.Rewards != null)
            {
                foreach (var it in t.Rewards)
                {
                    var tl = ItemTile(r, it, new Vector2(x, 1f), tileSz, false, tipLayer);
                    if (!unlocked || taken)
                    {
                        foreach (var img in tl.GetComponentsInChildren<Image>())
                            img.color = img.color.Mul(taken ? 0.82f : 0.88f);
                    }
                    x += tileStep;
                }
            }

            float textLeft = x - tileSz * 0.5f + 18f;

            // 前置未解锁态：干净原色 row_done 卡底 + 锁图标与明确前置名称
            if (!unlocked && !taken)
            {
                string preName = t.PreId >= 0 && t.PreId < Meta.ClubTasks.Length ? Meta.ClubTasks[t.PreId].Name : "前置任务";
                float spanRight = w * 0.5f - 28f;
                float spanW = spanRight - textLeft;
                float spanCenterX = textLeft + spanW * 0.5f;

                Fit(Ui.Label(r, $"{t.Name}（{cur}/{t.Need}）", 28, Pal.Hex("6D645A"),
                    new Vector2(spanCenterX, 22f), new Vector2(spanW, 40f), TextAnchor.MiddleLeft, Fonts.Heavy), 20);
                Ui.Icon(r, "ic_lock", 34f, new Vector2(textLeft + 17f, -22f));
                Fit(Ui.Label(r, $"完成「{preName}」后解锁", 23, WarmSub,
                    new Vector2(spanCenterX + 22f, -22f), new Vector2(spanW - 44f, 34f), TextAnchor.MiddleLeft, Fonts.Heavy), 18);

                var lockHit = Ui.Box(r, new Vector2(spanW, 116f), new Vector2(spanCenterX, 0f), new Color(0, 0, 0, 0), Art.White, "lockHit");
                Ui.Tap(lockHit, () => Ui.Toast($"请先完成并领取「{preName}」", 200), r);
                return;
            }

            // 已解锁态（可领 / 进行中 / 已领）：中栏标题 + 手绘九宫格进度条，右栏标准立体按钮
            const float btnW = 176f;
            float btnCenterX = w * 0.5f - btnW * 0.5f - 22f;
            float textRight = btnCenterX - btnW * 0.5f - 20f;
            float midW = Mathf.Max(180f, textRight - textLeft);
            float midCenterX = textLeft + midW * 0.5f;

            Color titleCol = taken ? Pal.Hex("8C8274") : WarmTitle;
            Fit(Ui.Label(r, t.Name, 28, titleCol, new Vector2(midCenterX, 24f), new Vector2(midW, 42f), TextAnchor.MiddleLeft, Fonts.Heavy), 20);

            float frac = cur / (float)Mathf.Max(1, t.Need);
            Color barFill = can || cur >= t.Need ? Pal.Hex("58C2EE") : Grass;
            Bar(r, new Vector2(midCenterX, -24f), new Vector2(midW, 24f), frac, barFill, $"{cur}/{t.Need}");

            Vector2 bpos = new Vector2(btnCenterX, 1f);
            if (taken)
            {
                const float mark = 40f, gap = 16f, wordW = 112f;
                float left = btnCenterX - (mark + gap + wordW) * 0.5f;
                Ui.Icon(r, "ic_check", mark, new Vector2(left + mark * 0.5f, 1f));
                var done = Ui.Label(r, "已领取", 26, Pal.Hex("8A8276"),
                    new Vector2(left + mark + gap + wordW * 0.5f, 1f), new Vector2(wordW, 44f), TextAnchor.MiddleLeft, Fonts.Heavy);
                done.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else if (can)
            {
                var cb = Ui.Btn(r, "领取", new Vector2(btnW, 80f), bpos, Claim, onClaim, 32);
                Dot(cb, new Vector2(btnW * 0.5f - 10f, 32f));
            }
            else
            {
                Ui.Btn(r, "未完成", new Vector2(btnW, 78f), bpos, Pal.Slate, () =>
                {
                    if (goBtn != null) Tween.Pop(goBtn, 0.24f);
                    Ui.Toast("请点上方【前往游戏圈】完成后返回领奖", 200);
                }, 28);
            }
        }
    }

    /// <summary>
    /// 负责微信真机的两件事：
    /// 1) 首页后台静默同步（玩家从游戏圈点赞/加圈回到主页即刻入账并亮红点，无需先打开弹窗）；
    /// 2) 弹窗打开时在固定栏「前往游戏圈」上叠放透明 WXGameClubButton，遮挡/关闭时自动隐藏销毁。
    /// </summary>
    public sealed class GameClubWatcher : MonoBehaviour
    {
        static bool _bgHooked;
        static bool _globalSyncing;

        RectTransform _modal;
        Transform _layer;
        RectTransform _goBtn;
#if LIANGSHAN_WX_CLUB
        WXGameClubButton _wxBtn;
        bool _wxShown;
        Action<OnShowListenerResult> _onShowCb;
#endif

        public static void EnsureBackgroundHook()
        {
#if LIANGSHAN_WX_CLUB
            if (_bgHooked || Gm.Allowed) return;
            _bgHooked = true;
            WX.OnShow(_ =>
            {
                if (Game.I != null)
                    Game.I.StartCoroutine(SyncRoutine(false, null, null));
            });
            if (Game.I != null)
                Game.I.StartCoroutine(SyncRoutine(false, null, null));
#endif
        }

        public void Setup(RectTransform modal, Transform layer, RectTransform goBtn)
        {
            _modal = modal;
            _layer = layer;
            _goBtn = goBtn;
#if LIANGSHAN_WX_CLUB
            if (!Gm.Allowed)
            {
                _onShowCb = _ =>
                {
                    if (this != null && isActiveAndEnabled)
                        StartCoroutine(PollOnReturn());
                };
                WX.OnShow(_onShowCb);
                StartCoroutine(PlaceNativeWhenSettled());
                StartCoroutine(SyncRoutine(true, _layer, _modal));
            }
#endif
        }

        void Update()
        {
#if LIANGSHAN_WX_CLUB
            if (_wxBtn == null || _modal == null) return;
            if (_modal.name.EndsWith("_closing", StringComparison.Ordinal))
            {
                DestroyNative();
                return;
            }
            bool topMost = _modal.parent != null && _modal.GetSiblingIndex() == _modal.parent.childCount - 1;
            if (topMost && !_wxShown)
            {
                _wxBtn.Show();
                _wxShown = true;
            }
            else if (!topMost && _wxShown)
            {
                _wxBtn.Hide();
                _wxShown = false;
            }
#endif
        }

        void OnDestroy()
        {
#if LIANGSHAN_WX_CLUB
            if (_onShowCb != null)
            {
                try { WX.OffShow(_onShowCb); } catch (Exception) { }
                _onShowCb = null;
            }
            DestroyNative();
#endif
        }

#if LIANGSHAN_WX_CLUB
        [Serializable]
        sealed class ClubSyncReq
        {
            public string encryptedData;
            public string iv;
        }

        IEnumerator PollOnReturn()
        {
            float[] delays = { 0.15f, 1.5f, 3.8f };
            float prev = 0f;
            foreach (float d in delays)
            {
                yield return new WaitForSecondsRealtime(d - prev);
                prev = d;
                if (this == null || _modal == null || _modal.name.EndsWith("_closing", StringComparison.Ordinal))
                    yield break;
                yield return SyncRoutine(false, _layer, _modal);
            }
        }

        IEnumerator PlaceNativeWhenSettled()
        {
            float still = 0f;
            float until = Time.realtimeSinceStartup + 1.5f;
            Rect last = default;
            while (_goBtn != null && still < 0.12f && Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (_goBtn == null || _modal == null || _modal.name.EndsWith("_closing", StringComparison.Ordinal))
                    yield break;
                Canvas.ForceUpdateCanvases();
                Rect now = ScreenRect(_goBtn);
                bool moved = Mathf.Abs(now.x - last.x) > 0.5f || Mathf.Abs(now.y - last.y) > 0.5f
                    || Mathf.Abs(now.width - last.width) > 0.5f || Mathf.Abs(now.height - last.height) > 0.5f
                    || now.width < 4f || now.height < 4f;
                if (moved) { last = now; still = 0f; }
                else still += Time.unscaledDeltaTime;
            }
            if (_goBtn == null || _modal == null || _modal.name.EndsWith("_closing", StringComparison.Ordinal))
                yield break;
            CreateNative(last);
        }

        void CreateNative(Rect screenRect)
        {
            DestroyNative();
            if (screenRect.width < 4f || screenRect.height < 4f) return;
            try
            {
                RectInt css = ToCss(screenRect);
                _wxBtn = WX.CreateGameClubButton(new WXCreateGameClubButtonParam
                {
                    type = GameClubButtonType.text,
                    text = "　　",
                    style = new GameClubButtonStyle
                    {
                        left = css.x,
                        top = css.y,
                        width = css.width,
                        height = css.height,
                        backgroundColor = "rgba(0, 0, 0, 0)",
                        borderColor = "rgba(0, 0, 0, 0)",
                        borderWidth = 0,
                        borderRadius = 0,
                        color = "rgba(0, 0, 0, 0)",
                        textAlign = GameClubButtonTextAlign.center,
                        fontSize = Mathf.Max(16, css.height),
                        lineHeight = Mathf.Max(16, css.height),
                    },
                });
                if (_wxBtn == null) return;
                bool topMost = _modal != null && _modal.parent != null && _modal.GetSiblingIndex() == _modal.parent.childCount - 1;
                if (topMost) { _wxBtn.Show(); _wxShown = true; }
                else { _wxBtn.Hide(); _wxShown = false; }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameClub] CreateGameClubButton failed: " + e.Message);
                _wxBtn = null;
                _wxShown = false;
            }
        }

        void DestroyNative()
        {
            if (_wxBtn != null)
            {
                try { _wxBtn.Destroy(); } catch (Exception) { }
                _wxBtn = null;
            }
            _wxShown = false;
        }

        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[2].x);
            float xMax = Mathf.Max(c[0].x, c[2].x);
            float yMin = Mathf.Min(c[0].y, c[2].y);
            float yMax = Mathf.Max(c[0].y, c[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static RectInt ToCss(Rect screenRect)
        {
            var info = WX.GetSystemInfoSync();
            float winW = info != null ? Convert.ToSingle(info.windowWidth) : Screen.width;
            float dpr = info != null ? Convert.ToSingle(info.pixelRatio) : 1f;
            if (dpr < 1f) dpr = 1f;
            float scale = winW > 0f && Screen.width < winW * 1.5f ? dpr : 1f;
            int px = Mathf.RoundToInt(screenRect.xMin * scale);
            int py = Mathf.RoundToInt((Screen.height - screenRect.yMax) * scale);
            int pw = Mathf.Max(1, Mathf.RoundToInt(screenRect.width * scale));
            int ph = Mathf.Max(1, Mathf.RoundToInt(screenRect.height * scale));
            return new RectInt(
                Mathf.RoundToInt(px / dpr),
                Mathf.RoundToInt(py / dpr),
                Mathf.Max(1, Mathf.RoundToInt(pw / dpr)),
                Mathf.Max(1, Mathf.RoundToInt(ph / dpr)));
        }

        static IEnumerator EnsurePrivacy(bool interactive, Action<bool> done)
        {
            bool back = false;
            bool needAuth = false;
            try
            {
                WX.GetPrivacySetting(new GetPrivacySettingOption
                {
                    success = r => { needAuth = r != null && r.needAuthorization; back = true; },
                    fail = _ => { back = true; },
                });
            }
            catch (Exception)
            {
                done(true);
                yield break;
            }
            float until = Time.realtimeSinceStartup + 3f;
            while (!back && Time.realtimeSinceStartup < until) yield return null;
            if (!needAuth) { done(true); yield break; }
            if (!interactive) { done(false); yield break; }

            bool authBack = false, authOk = false;
            try
            {
                WX.RequirePrivacyAuthorize(new RequirePrivacyAuthorizeOption
                {
                    success = _ => { authOk = true; authBack = true; },
                    fail = _ => { authOk = false; authBack = true; },
                });
            }
            catch (Exception)
            {
                done(false);
                yield break;
            }
            float authUntil = Time.realtimeSinceStartup + 20f;
            while (!authBack && Time.realtimeSinceStartup < authUntil) yield return null;
            done(authOk);
        }

        static IEnumerator SyncRoutine(bool interactivePrivacy, Transform layer, RectTransform modal)
        {
            if (_globalSyncing) yield break;
            _globalSyncing = true;
            try
            {
                bool privacyOk = true;
                yield return EnsurePrivacy(interactivePrivacy, ok => privacyOk = ok);
                if (!privacyOk) yield break;

                // 若微信侧 session_key 已过期，先重登换新 session_key 再拉加密数据
                bool sessDone = false, sessValid = true;
                try
                {
                    WX.CheckSession(new CheckSessionOption
                    {
                        success = _ => { sessValid = true; sessDone = true; },
                        fail = _ => { sessValid = false; sessDone = true; },
                    });
                }
                catch (Exception) { sessDone = true; }
                float sessUntil = Time.realtimeSinceStartup + 3f;
                while (!sessDone && Time.realtimeSinceStartup < sessUntil) yield return null;
                if (!sessValid)
                {
                    Backend.ClearToken();
                    string preLoginErr = null;
                    yield return Backend.EnsureToken(e => preLoginErr = e);
                    if (preLoginErr != null) yield break;
                }

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    string enc = null, iv = null, wxErr = null;
                    bool back = false;
                    try
                    {
                        WX.GetGameClubData(new GetGameClubDataOption
                        {
                            dataTypeList = new[]
                            {
                                new DataType { type = 1 },
                                new DataType { type = 4 },
                                new DataType { type = 5 },
                                new DataType { type = 6 },
                            },
                            success = r =>
                            {
                                enc = r != null ? r.encryptedData : null;
                                iv = r != null ? r.iv : null;
                                wxErr = string.IsNullOrEmpty(enc) ? "empty" : null;
                                back = true;
                            },
                            fail = e =>
                            {
                                wxErr = e != null ? e.errMsg : "fail";
                                back = true;
                            },
                        });
                    }
                    catch (Exception e)
                    {
                        wxErr = e.Message;
                        back = true;
                    }

                    float until = Time.realtimeSinceStartup + CloudConfig.RequestTimeoutSec;
                    while (!back && Time.realtimeSinceStartup < until) yield return null;
                    if (!back || wxErr != null)
                    {
                        if (wxErr != null) Debug.LogWarning("[GameClub] getGameClubData: " + wxErr);
                        yield break;
                    }

                    CloudReply reply = null;
                    string callErr = null;
                    string body = JsonUtility.ToJson(new ClubSyncReq { encryptedData = enc, iv = iv });
                    yield return Backend.Call(CloudConfig.GameClubSyncPath, body, (s, r, err) =>
                    {
                        reply = r;
                        callErr = err;
                    });

                    if (callErr == null && reply != null && reply.data != null)
                    {
                        var d = reply.data;
                        if (Meta.SyncClubData(Profile.S, Profile.Today, d.joined, d.dayLikes, d.dayComments, d.dayPosts))
                        {
                            Profile.Save();
                            if (layer != null && modal != null && !modal.name.EndsWith("_closing", StringComparison.Ordinal))
                                MetaUi.GameClub(layer);
                        }
                        yield break;
                    }

                    string code = reply != null ? reply.code : null;
                    if ((code != "NO_WX_SESSION" && code != "DECRYPT_FAIL") || attempt > 0)
                        yield break;

                    Backend.ClearToken();
                    string loginErr = null;
                    yield return Backend.EnsureToken(e => loginErr = e);
                    if (loginErr != null) yield break;
                }
            }
            finally
            {
                _globalSyncing = false;
            }
        }
#endif
    }
}

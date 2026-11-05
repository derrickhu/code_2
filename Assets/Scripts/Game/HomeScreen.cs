using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>主界面：顶栏资源、五个页签（商城/好汉/出征/寨主/山寨），弹窗都挂在 Layer 上。</summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        public const int TabShop = 0, TabHero = 1, TabBattle = 2, TabForge = 3, TabFort = 4;

        RectTransform _root, _page;
        public RectTransform Layer { get; private set; }
        int _tab = TabBattle;
        /// <summary>好汉页职业筛选：-1 全部。</summary>
        int _roleFilter = -1;
        float _rosterScroll;
        bool _dirty;
        Text _staminaText, _staminaWait;
        float _tickT;

        public int Tab => _tab;
        static SaveData S => Profile.S;

        public static HomeScreen Create(int tab)
        {
            var rt = Ui.Rect(Ui.Root, "Home");
            Stretch(rt);
            var h = rt.gameObject.AddComponent<HomeScreen>();
            h._root = rt;
            h._tab = tab;
            h.Layer = Ui.Rect(rt, "layer");
            Stretch(h.Layer);
            h.Rebuild(true);
            Profile.Changed += h.MarkDirty;
            return h;
        }

        void OnDestroy() => Profile.Changed -= MarkDirty;
        void MarkDirty() => _dirty = true;

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        float _slideDir;

        public void SetTab(int t)
        {
            if (t == _tab) return;
            _slideDir = t > _tab ? 1f : -1f;
            _tab = t;
            _swap = null;
            Sfx.Play(Snd.Tab, 0.8f);
            Rebuild(true);
        }

        /// <summary>任务“前往”：跳到能完成该任务的地方。</summary>
        public void Goto(Act a)
        {
            switch (a)
            {
                case Act.HeroUp: SetTab(TabHero); break;
                case Act.Forge: case Act.Talent: SetTab(TabForge); break;
                case Act.Ad: case Act.Recruit: SetTab(TabShop); break;
                default: SetTab(TabBattle); break;
            }
        }

        void LateUpdate()
        {
            if (_dirty)
            {
                _dirty = false;
                Rebuild(false);
            }
            if ((_tickT -= Time.unscaledDeltaTime) <= 0 && _staminaText)
            {
                _tickT = 0.5f;
                _staminaText.text = $"{S.Stamina}/{Meta.StaminaMax}";
                int w = Meta.StaminaWait(S, Profile.Now);
                _staminaWait.text = S.Stamina >= Meta.StaminaMax ? "" : $"{w / 60}:{w % 60:00}";
            }
        }

        void Rebuild(bool anim)
        {
            Ui.Fit();
            if (_page) DestroyImmediate(_page.gameObject);
            _page = Ui.Rect(_root, "page");
            Stretch(_page);
            _page.SetSiblingIndex(0);
            switch (_tab)
            {
                case TabShop: BuildShop(anim); break;
                case TabHero: BuildHeroes(anim); break;
                case TabForge: BuildForge(anim); break;
                case TabFort: BuildFort(anim); break;
                default: BuildBattle(anim); break;
            }
            // 页面内容收进 body，切页时只让 body 淡入滑入，顶栏和页签不闪
            var body = Ui.Rect(_page, "body");
            Stretch(body);
            while (_page.childCount > 1) _page.GetChild(0).SetParent(body, false);
            if (anim)
            {
                var cg = body.gameObject.AddComponent<CanvasGroup>();
                float dir = _slideDir;
                Tween.Run(body, 0.2f, k =>
                {
                    cg.alpha = Mathf.Lerp(0.2f, 1f, k);
                    body.anchoredPosition = new Vector2(dir * 60f * (1 - Tween.OutCubic(k)), 0);
                });
            }
            BuildTopBar();
            BuildTabs();
            Layer.SetAsLastSibling();
        }

        RectTransform Top(float x, float y, Vector2 size, string name) => Ui.Top(_page, x, y + Ui.SafeTop, size, name);
        RectTransform Bottom(float x, float y, Vector2 size, string name) => Ui.Bottom(_page, x, y, size, name);

        /// <summary>背景按屏幕铺满（cover），返回图片实际尺寸。</summary>
        Vector2 Cover(string art, Vector2 natural, Color tint, float centerY = 0.5f)
        {
            var bg = Ui.Box(_page, natural, Vector2.zero, tint, Art.Img(art), "bg");
            bg.type = Image.Type.Simple;
            float scale = Mathf.Max(Ui.W / natural.x, Ui.Height / natural.y);
            var size = natural * scale;
            bg.rectTransform.sizeDelta = size;
            bg.rectTransform.anchoredPosition = new Vector2(0, (centerY - 0.5f) * (size.y - Ui.Height));
            return size;
        }

        /// <summary>顶、底各压一层淡蓝渐变，只为托住顶栏和页签上的白字，不把画面压暗。</summary>
        void Shade(float top, float bottom)
        {
            var tint = Pal.Navy;
            if (top > 0)
            {
                var t = Ui.Box(_page, new Vector2(1080, 420), Vector2.zero, tint.A(top), Art.VGrad, "shadeTop");
                t.rectTransform.anchorMin = new Vector2(0, 1);
                t.rectTransform.anchorMax = new Vector2(1, 1);
                t.rectTransform.sizeDelta = new Vector2(0, 420);
                t.rectTransform.anchoredPosition = new Vector2(0, -210);
                t.rectTransform.localScale = new Vector3(1, -1, 1);
            }
            if (bottom > 0)
            {
                var b = Ui.Box(_page, new Vector2(1080, 760), Vector2.zero, tint.A(bottom), Art.VGrad, "shadeBottom");
                b.rectTransform.anchorMin = new Vector2(0, 0);
                b.rectTransform.anchorMax = new Vector2(1, 0);
                b.rectTransform.sizeDelta = new Vector2(0, 760);
                b.rectTransform.anchoredPosition = new Vector2(0, 380);
            }
        }

        // ---------------- 顶栏、页签 ----------------

        static readonly Color Glass = new Color(0.16f, 0.08f, 0.04f, 0.55f);
        static readonly Color TopPill = new Color(0.16f, 0.08f, 0.04f, 0.68f);

        void BuildTopBar()
        {
            float y = Ui.SafeTop + 62;

            // 头像：杏色框、珊瑚等级章、奶白名牌、经验条
            var av = Ui.Top(_page, -470, y, new Vector2(116, 116), "avatar");
            var frame = Ui.Icon(av, "ui/h_avatar", 116, Vector2.zero, "frame");
            Ui.Icon(av, "prop_songjiang", 96, new Vector2(0, 2));
            int lv = Meta.LordLevel(S, out float xp);
            Ui.Icon(av, "ui/h_round", 46, new Vector2(-44, -42), "lvBadge").color = Coral;
            Ui.Outlined(av, lv.ToString(), 24, Color.white, new Vector2(-44, -41), new Vector2(46, 40), CoralDeep, 1.5f);
            Ui.Tap(frame, () => SetTab(TabForge), av);
            var nm = Ui.Top(_page, -318, y - 18, new Vector2(176, 50), "name");
            Ui.Box(nm, new Vector2(176, 50), Vector2.zero, TopPill, Art.Pill);
            Ui.Outlined(nm, "宋江", 28, Color.white, new Vector2(0, 1), new Vector2(160, 46), Pal.Hex("1A0C06"), 1.5f);
            var xb = Ui.Top(_page, -318, y + 22, new Vector2(170, 18), "xp");
            Ui.Box(xb, new Vector2(164, 18), Vector2.zero, Pal.Hex("A8784A").A(0.55f), Art.Pill);
            var xf = Ui.Box(xb, new Vector2(Mathf.Max(16, 158 * xp), 12), new Vector2(-79, 0), Pal.Hex("7BE34E"), Art.Pill, "fill");
            xf.rectTransform.pivot = new Vector2(0, 0.5f);

            CurPill(-50, y - 14, "ic_ingot", Ui.Num(S.Ingots), () => SetTab(TabShop));
            CurPill(184, y - 14, "ic_coins", Ui.Num(S.Copper), null);
            var st = CurPill(414, y - 14, "ic_wine", $"{S.Stamina}/{Meta.StaminaMax}", () => MetaUi.StaminaShop(Layer));
            _staminaText = st.Find("text").GetComponent<Text>();
            _staminaWait = Ui.Outlined(st, "", 20, Color.white, new Vector2(8, -40), new Vector2(160, 26), Edge, 1.2f);
            _tickT = 0;
        }

        /// <summary>奶白面上的字。</summary>
        static readonly Color Cocoa = Pal.Hex("6B3E1E");
        /// <summary>压在场景上的白字描边，比纯黑轻。</summary>
        static readonly Color Edge = Pal.Hex("7A4A24");
        static readonly Color Coral = Pal.Hex("F2846A");
        static readonly Color CoralDeep = Pal.Hex("B4462E");
        static readonly Color LeafDeep = Pal.Hex("3E7A1C");

        /// <summary>资源胶囊：奶白面、杏色描边，可可色字；左侧图标压边，右侧绿色加号。</summary>
        RectTransform CurPill(float x, float y, string icon, string text, Action plus)
        {
            var c = Ui.Top(_page, x, y, new Vector2(204, 56), "cur_" + icon);
            Ui.Box(c, new Vector2(188, 48), new Vector2(8, 0), TopPill, Art.Pill, "bg");
            Ui.Icon(c, icon, 64, new Vector2(-82, 2));
            Ui.Outlined(c, text, 28, Color.white, new Vector2(plus != null ? 4 : 14, 1), new Vector2(130, 46), Pal.Hex("1A0C06"), 1.5f, TextAnchor.MiddleCenter, "text");
            if (plus != null)
            {
                var p = Ui.Skin(c, "btn_green", new Vector2(40, 40), new Vector2(84, 0), -1f);
                Ui.Outlined(p.transform, "+", 30, Color.white, new Vector2(0, 2), new Vector2(40, 40), Pal.GreenDeep, 1.2f);
                Ui.Tap(p, plus, c);
            }
            return c;
        }

        public const float TabH = 156;

        void BuildTabs()
        {
            var bar = Ui.Rect(_page, "tabs");
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.sizeDelta = new Vector2(0, TabH);
            bar.anchoredPosition = new Vector2(0, TabH * 0.5f);
            // 横条底下一直铺到屏幕底边（横条区也是这块奶白板），上沿杏色边压在页签区顶上。
            var under = Ui.Box(bar, new Vector2(1080, TabH), Vector2.zero, Pal.Hex("4A2A1A"), Art.White, "under");
            Stretch(under.rectTransform);
            under.rectTransform.offsetMin = new Vector2(0, -Ui.SafeBottom - 40);
            under.rectTransform.offsetMax = new Vector2(0, -40);
            under.raycastTarget = true;
            var plank = Ui.Skin(bar, "h_tabbar", new Vector2(1100, TabH + 20), new Vector2(0, 4), -1f, null, "plank");
            plank.raycastTarget = true;
            string[] names = { "商城", "好汉", "出征", "寨主", "山寨" };
            string[] icons = { "tab_shop", "tab_hero", "tab_battle", "tab_forge", "tab_fort" };
            bool[] dots = { ShopBadge(), HeroBadge(), BattleBadge(), ForgeBadge(), FortBadge() };
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 212;
                bool on = i == _tab;
                var tab = Ui.Rect(bar, "tab" + i);
                tab.anchoredPosition = new Vector2(x, 0);
                tab.sizeDelta = new Vector2(212, TabH);
                var face = Ui.Box(tab, new Vector2(204, TabH), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "face");
                face.raycastTarget = true;
                if (on)
                {
                    Ui.Box(tab, new Vector2(260, 200), new Vector2(0, 36), Pal.Gold.A(0.3f), Art.SoftDot, "glow");
                    Ui.Icon(tab, "ui/h_tab_on", 180, new Vector2(0, 30), "badge");
                }
                var ic = Ui.Icon(tab, icons[i], on ? 132 : 98, new Vector2(0, on ? 46 : 16));
                if (on) Tween.Run(ic, 999f, k => ic.rectTransform.anchoredPosition = new Vector2(0, 46 + 4 * Mathf.Sin(k * 999f * 3f)));
                if (on) Ui.Outlined(tab, names[i], 30, Color.white, new Vector2(0, -48), new Vector2(210, 40), Pal.Hex("7A1A0E"), 2f);
                else Ui.Outlined(tab, names[i], 27, Pal.Hex("F4E2C0"), new Vector2(0, -50), new Vector2(210, 40), Pal.Hex("2A140A"), 1.5f);
                if (dots[i]) MetaUi.Dot(tab, new Vector2(56, on ? 92 : 50));
                int ti = i;
                Ui.Tap(face, () => SetTab(ti), tab, false);
            }
        }

        // 看视频才有的次数不亮红点，只有手里有招募令才提醒。
        static bool ShopBadge() => S.Tokens >= 1;
        static bool HeroBadge()
        {
            for (int h = 0; h < Defs.HeroCount; h++) if (Meta.CanLevel(S, (HeroId)h)) return true;
            return false;
        }
        static bool ForgeBadge()
        {
            for (int i = 0; i < Meta.Equips.Length; i++) if (Meta.CanForge(S, i)) return true;
            return Meta.CanTalent(S) || Meta.CanPage(S) || Meta.CanTome(S) || Meta.CanFlagUp(S) || S.GearBoxes > 0;
        }
        static bool IdleBadge() => Meta.IdleHours(S, Profile.Now) >= 1;
        static bool MealBadge() => Meta.MealsReady(S, DateTime.Now.Hour) > 0;
        static bool ChestBadge(int ch)
        {
            for (int k = 0; k < 3; k++) if (Meta.CanOpenChest(S, ch, k)) return true;
            return false;
        }
        static bool BattleBadge()
        {
            if (Meta.CanSign(S, Profile.Today) || Meta.TaskBadge(S) > 0 || Meta.CodexBadge(S) > 0 || IdleBadge() || MealBadge() || GrowthBadge()) return true;
            for (int c = 1; c <= Defs.Chapters.Length; c++) if (ChestBadge(c)) return true;
            int n = Math.Min(S.Cleared, S.Stars.Length);
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 3; k++)
                    if (Meta.CanOpenLevelChest(S, i + 1, k)) return true;
            return false;
        }
        static bool FortBadge() => IdleBadge() || Meta.TaskBadge(S) > 0;
        static bool SevenBadge() => !Meta.SevenFinished(S) && Meta.SevenBadge(S, Profile.Today) > 0;
        static bool SprintBadge() => Meta.SprintTierBadge(S) + Meta.SprintTaskBadge(S) > 0;
        static bool GrowthBadge() => SevenBadge() || SprintBadge() || Meta.CanWelfare(S, Profile.Today);

        // ---------------- 出征 ----------------

        /// <summary>
        /// 参考视频的主页就是战场：城门口站着本关首领，中间是关卡水印，寨墙上站着出战的四位好汉，
        /// 下面一排三个关卡宝箱，再下面是开始挑战。功能入口收进两侧半透明竖条和顶部小图标。
        /// </summary>
        void BuildBattle(bool anim)
        {
            var st = Defs.Level(S.Selected);
            Cover("home_road", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0f, 0.1f);
            float sp = Ui.Spare;
            int day = Profile.Today;

            // 顶部小图标（资源条下面）
            // 尺寸和间距按参考主页量出来（换算到 1080 宽）：顶部一排小入口靠右，两侧入口是半透明竖条里的图标。
            bool dOpen = S.Cleared >= MetaUi.DailyUnlock, eOpen = S.Cleared >= MetaUi.EscortUnlock;
            float ly = 262 + sp * 0.04f, ry = 316 + sp * 0.04f;
            SideStrip(-455, ly, 3);
            SideBtn(-455, ly, 0, "ic_medal", "挑战", dOpen && S.DailyTries < Meta.DailyTriesMax, () =>
            {
                if (!dOpen) Ui.Toast($"通关第 {MetaUi.DailyUnlock} 关后开放", 200);
                else MetaUi.Daily(Layer);
            }, !dOpen);
            SideBtn(-455, ly, 1, "ic_cart", "生辰纲", eOpen && S.EscortTries < Meta.EscortTriesMax, () =>
            {
                if (!eOpen) Ui.Toast($"通关第 {MetaUi.EscortUnlock} 关后开放", 200);
                else MetaUi.Escort(Layer);
            }, !eOpen);
            SideBtn(-455, ly, 2, "tab_idle", "巡山", IdleBadge(), () => MetaUi.Idle(Layer));
            bool seven = !Meta.SevenFinished(S);
            SideStrip(458, ry, seven ? 4 : 3);
            SideBtn(458, ry, 0, "tab_task", "任务", Meta.TaskBadge(S) > 0, () => MetaUi.Tasks(Layer));
            SideBtn(458, ry, 1, "tab_codex", "图鉴", Meta.CodexBadge(S) > 0, () => MetaUi.Codex(Layer));
            int side = 2;
            if (seven) SideBtn(458, ry, side++, "tab_seven", "七日", SevenBadge(), () => MetaUi.Seven(Layer));
            SideBtn(458, ry, side, "tab_sprint", "冲刺", SprintBadge(), () => MetaUi.Sprint(Layer));

            // 背景台阶下沿约在图高 27.6%：首领站在城门口，脚踩台阶下沿；关卡名压在脚边。
            var bd = Defs.Enemy(st.BossKind);
            float bsz = st.IsChapterBoss ? 370 : 330;
            float stair = (530f / 1920f) * Ui.Height;
            float feet = stair - Ui.SafeTop + 80f;
            var boss = Top(0, feet - bsz * 0.5f, new Vector2(bsz, bsz), "boss");
            Ui.Box(boss, new Vector2(bsz * 0.8f, 70), new Vector2(0, -bsz * 0.47f), new Color(0, 0, 0, 0.3f), Art.SoftDot);
            Ui.Box(boss, new Vector2(bsz * 0.95f, 100), new Vector2(0, -bsz * 0.46f), (st.IsChapterBoss ? Pal.Red : Pal.Purple).A(0.5f), Art.SoftDot);
            var icon = Ui.Icon(boss, bd.Art, bsz, new Vector2(0, 0));
            Tween.Run(icon, 999f, k => icon.rectTransform.anchoredPosition = new Vector2(0, 5 * Mathf.Sin(k * 999f * 2f)));
            if (anim) Tween.Pop(boss, 0.35f);
            float tagY = feet + 26f;
            var tag = Top(0, tagY, new Vector2(620, 70), "levelTag");
            var nameT = Ui.Outlined(tag, $"{st.No}.{st.Name}", 42, Pal.Hex("E8508A"), new Vector2(30, 2), new Vector2(520, 60), Color.white, 3f);
            float tw = Mathf.Min(460, nameT.preferredWidth);
            var list = Ui.Box(tag, new Vector2(76, 76), new Vector2(30 - tw * 0.5f - 58, 2), Glass, Art.Pill, "list");
            for (int i = 0; i < 3; i++) Ui.Box(list.transform, new Vector2(34, 6), new Vector2(0, 11 - i * 11), Color.white, Art.Pill);
            Ui.Tap(list, () => MetaUi.LevelList(Layer), tag);

            // 顶部小入口压在首领上面
            float iy = Ui.SafeTop + 196;
            MiniBtn(-45, iy, "tab_welfare", "福利", Meta.CanWelfare(S, day), () => MetaUi.Welfare(Layer));
            MiniBtn(83, iy, "ic_feast", "聚义宴", MealBadge(), () => MetaUi.Meals(Layer));
            MiniBtn(206, iy, "tab_gift", "签到", Meta.CanSign(S, day), () => MetaUi.SignIn(Layer));
            MiniBtn(330, iy, "ic_map", "关卡", false, () => MetaUi.LevelList(Layer));
            MiniBtn(458, iy, "ic_gear", "设置", false, () => MetaUi.Settings(Layer));

            // 出战好汉：一排挨着站在玉色底座上，左边章节星级宝箱，右边扫荡
            float baseY = TabH;
            float wallY = baseY + 505 + sp * 0.27f;
            var podium = Bottom(0, wallY, new Vector2(1200, 277), "podium");
            var lu = Meta.Lineup(S);
            var arts = new Image[lu.Length];
            for (int i = 0; i < lu.Length; i++)
            {
                var d = Defs.Hero(lu[i]);
                var g = Ui.Rect(podium, "hero" + i);
                g.anchoredPosition = new Vector2((i - 1.5f) * 132, 96);
                g.sizeDelta = new Vector2(130, 200);
                Ui.Box(g, new Vector2(150, 60), new Vector2(0, -64), Pal.Hex("2E6A40").A(0.3f), Art.SoftDot, "aura");
                Ui.Icon(g, "ui/h_pedestal", 126, new Vector2(0, -76), "pedestal");
                // 立绘是紧裁的全身；攻击帧是脚底贴底的方画布。按身体高度放大，脚仍落在原来的位置。
                bool framed = Art.Animated(d.Art);
                float draw = 160f, standY = 5f;
                if (framed)
                {
                    float bodyH = 0.85f, foot = 0.01f;
                    if (AnimMeta.Info.TryGetValue(d.Art, out var meta)) { bodyH = meta.Body; foot = meta.Foot; }
                    draw = 150f / Mathf.Max(0.6f, bodyH);
                    standY = -75f - draw * (foot - 0.5f);
                }
                var art = Ui.Icon(g, d.Art, draw, new Vector2(0, standY), "art");
                if (framed)
                {
                    art.color = Color.white.A(0f);
                    string key = d.Art;
                    Art.Late(art, key, () =>
                    {
                        var fr = Art.Frames(key);
                        art.sprite = fr[0];
                        art.color = Color.white;
                    });
                }
                arts[i] = art;
                float ph = i * 0.8f;
                float bob = standY;
                Tween.Run(art, 999f, k => art.rectTransform.anchoredPosition = new Vector2(0, bob + 3 * Mathf.Sin(k * 999f * 2.2f + ph)));
                Ui.Tap(art, () => SetTab(TabHero), g);
                if (Meta.CanLevel(S, lu[i])) MetaUi.Dot(g, new Vector2(50, 80));
                if (anim) Tween.Pop(g, 0.3f, 0.1f + i * 0.05f);
            }
            var pw = Ui.Rect(podium, "power");
            pw.anchoredPosition = new Vector2(0, -46);
            pw.sizeDelta = new Vector2(210, 50);
            Ui.Box(pw, new Vector2(210, 50), Vector2.zero, Glass, Art.Pill);
            Ui.Icon(pw, "ic_sword", 42, new Vector2(-62, 1));
            Ui.Outlined(pw, Ui.Num(Meta.TeamPower(S)), 34, Pal.Hex("FFD75A"), new Vector2(18, 1), new Vector2(150, 46), Pal.Hex("5A3A10"), 2f);

            // 关卡三宝箱：一排三个，各自压一块浅影
            float chestY = baseY + 353 + sp * 0.18f;
            var row = Bottom(0, chestY, new Vector2(420, 120), "chests");
            for (int k = 0; k < 3; k++)
            {
                bool got = Meta.LevelChestGot(S, st.No, k);
                bool ready = Meta.CanOpenLevelChest(S, st.No, k);
                float x = (k - 1) * 128;
                Ui.Box(row, new Vector2(118, 96), new Vector2(x, -4), new Color(0, 0, 0, 0.18f), Art.Rounded, "plate" + k);
                var c = Ui.Icon(row, got ? "ic_chest_open" : MetaUi.ChestArt(k), 100, new Vector2(x, 4), "chest" + k);
                if (!got && !ready) c.color = new Color(1f, 1f, 1f, 0.55f);
                if (got) MetaUi.Check(row, new Vector2(x + 34, -26), 32);
                if (ready)
                {
                    MetaUi.Dot(c.transform, new Vector2(36, 36));
                    var ic = c;
                    float phase = k * 0.6f;
                    Tween.Run(ic, 999f, kk => ic.rectTransform.localRotation = Quaternion.Euler(0, 0, 6f * Mathf.Sin(kk * 999f * 6f + phase)));
                }
                int ck = k, lvl = st.No;
                Ui.Tap(c, () =>
                {
                    if (!Meta.CanOpenLevelChest(S, lvl, ck))
                    {
                        MetaUi.LevelChestCard(Layer, lvl, ck, c.transform);
                        return;
                    }
                    var gotR = Meta.OpenLevelChest(S, lvl, ck, new Rng(Environment.TickCount));
                    Profile.Save();
                    MetaUi.ShowRewards(Layer, "关卡宝箱", gotR);
                });
                if (anim) Tween.Pop(c.transform, 0.28f, 0.15f + k * 0.05f);
            }

            // 左：章节星级宝箱；右：扫荡
            int stars = Meta.ChapterStars(S, st.Chapter);
            float sideY = wallY + 20;
            var cs = Bottom(-400, sideY, new Vector2(170, 120), "chapterStars");
            var csBg = Ui.Skin(cs, "h_tile", new Vector2(160, 116), Vector2.zero, 0.5f);
            Ui.Icon(cs, "ic_chest_gold", 74, new Vector2(-34, 10));
            Ui.Icon(cs, "ic_star", 36, new Vector2(38, 22));
            Ui.Label(cs, $"{stars}/30", 24, Cocoa, new Vector2(30, -28), new Vector2(110, 32), TextAnchor.MiddleCenter, Fonts.Heavy);
            if (ChestBadge(st.Chapter)) MetaUi.Dot(cs, new Vector2(70, 46));
            int chap = st.Chapter;
            Ui.Tap(csBg, () => MetaUi.ChapterChests(Layer, chap), cs);
            if (Meta.CanSweep(S, st.No))
            {
                var sw = Bottom(400, sideY, new Vector2(150, 150), "sweep");
                var swBg = Ui.Icon(sw, "ui/h_round", 116, new Vector2(0, 14), "bg");
                Ui.Icon(sw, "ic_sweep", 82, new Vector2(0, 16));
                Ui.Outlined(sw, "扫荡", 28, Color.white, new Vector2(0, -44), new Vector2(150, 36), Edge, 2f);
                Ui.Outlined(sw, $"体力 -{Meta.BattleStamina}", 20, Pal.Cream, new Vector2(0, -72), new Vector2(170, 28), Pal.Ink, 1.2f);
                Ui.Tap(swBg, () => Sweep(st), sw);
            }

            // 路中间的关卡水印：关卡号、关卡名、宝箱剩余，夹在关卡名和好汉头顶之间
            float nameAbs = Ui.SafeTop + tagY + 36f, headAbs = Ui.Height - (wallY + 96f + 80f);
            float span = headAbs - nameAbs;
            var mark = Ui.Top(_page, 0, nameAbs + span * 0.56f, new Vector2(900, 300), "mark");
            mark.localScale = Vector3.one * Mathf.Clamp(span / 640f, 0.55f, 1f);
            Color ink = Pal.Hex("6B4A2E").A(0.38f);
            Ui.Label(mark, $"第 {st.No} 关", 54, ink, new Vector2(0, 96), new Vector2(700, 64), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(mark, st.Name, 92, ink, new Vector2(0, 14), new Vector2(900, 104), TextAnchor.MiddleCenter, Fonts.Heavy);
            int left = Meta.LevelChestLeft(S, st.No), readyN = 0;
            for (int k = 0; k < 3; k++) if (Meta.CanOpenLevelChest(S, st.No, k)) readyN++;
            string chestLine = readyN > 0 ? $"有 <color=#FFD75A>{readyN}</color> 个宝箱可开启"
                : left > 0 ? $"本关仍有 <color=#FFD75A>{left}</color> 个宝箱未获得" : "本关宝箱已全部获得";
            var sub = Ui.Rect(mark, "chestLeft");
            sub.anchoredPosition = new Vector2(0, -72);
            sub.sizeDelta = new Vector2(460, 50);
            Ui.Box(sub, new Vector2(460, 50), Vector2.zero, Pal.Hex("4A3020").A(0.28f), Art.Pill);
            Ui.Label(sub, chestLine, 25, Color.white.A(0.92f), new Vector2(0, 1), new Vector2(440, 46), TextAnchor.MiddleCenter, Fonts.Heavy);
            string elite = st.EliteWaves.Length > 0 ? " · 精英 " + Defs.Enemy(st.EliteKinds[0]).Name : "";
            string buff = st.BuffRole.HasValue ? $"  <color=#3E7A1C>增益：{st.BuffText}</color>" : "";
            Ui.Label(mark, $"{st.Waves} 波 · 首领 {bd.Name}{elite}{buff}", 22, Pal.Hex("6B4A2E").A(0.7f), new Vector2(0, -122), new Vector2(900, 34), TextAnchor.MiddleCenter, Fonts.Heavy);

            // 开始挑战：嫩绿大按钮，字画在面上，按下整块下沉。
            float startY = baseY + 174 + sp * 0.1f;
            const float bw = 470f, bh = 172f;
            var start = Ui.Rect(_page, "start");
            start.anchorMin = start.anchorMax = new Vector2(0.5f, 0);
            start.sizeDelta = new Vector2(bw, bh);
            start.anchoredPosition = new Vector2(0, startY);
            var body = Ui.Rect(start, "body");
            body.sizeDelta = new Vector2(bw, bh);
            var face = Ui.Skin(body, "h_start", new Vector2(bw, bh), Vector2.zero, 0.55f, null, "face");
            face.raycastTarget = false;
            Ui.Outlined(body, "开始挑战", 54, Color.white, new Vector2(0, 26), new Vector2(bw - 80, 66), LeafDeep, 3.5f);
            Ui.Box(body, new Vector2(150, 46), new Vector2(0, -38), LeafDeep.A(0.45f), Art.Pill, "cost");
            Ui.Icon(body, "ic_wine", 40, new Vector2(-28, -37));
            Ui.Outlined(body, Meta.BattleStamina.ToString(), 30, Color.white, new Vector2(14, -37), new Vector2(60, 40), LeafDeep, 2f);
            var hit = Ui.Box(start, new Vector2(bw, bh), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            Ui.Tap(hit, () => Game.I.StartLevel(S.Selected), body, true, 8f);
            var glow = Ui.Box(start, new Vector2(bw + 80, bh + 50), Vector2.zero, Pal.Hex("FFF2B0").A(0.3f), Art.SoftDot, "glow");
            glow.transform.SetAsFirstSibling();
            Tween.Run(start, 9999f, k =>
            {
                float t = k * 9999f;
                start.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(t * 4f));
                glow.color = Pal.Hex("FFF2B0").A(0.2f + 0.12f * Mathf.Sin(t * 4f));
            });
            float ax = bw * 0.5f + 62f;
            if (S.Selected > 1) ArrowBtn(-ax, startY, -1);
            if (S.Selected <= S.Cleared && S.Selected < Defs.LevelCount) ArrowBtn(ax, startY, 1);

            var raid = Ui.Rect(_page, "raid");
            raid.anchorMin = Vector2.zero;
            raid.anchorMax = Vector2.one;
            raid.offsetMin = raid.offsetMax = Vector2.zero;
            raid.SetAsLastSibling();
            raid.gameObject.AddComponent<HomeVolley>().Begin(icon, arts, lu, StrongestDealer(lu));
        }

        /// <summary>首页出手的人：上阵里战力最高的输出；全是辅助时退回战力最高的那位。</summary>
        int StrongestDealer(HeroId[] lu)
        {
            int best = -1, any = 0;
            for (int i = 0; i < lu.Length; i++)
            {
                int p = Meta.HeroPower(S, lu[i]);
                if (p > Meta.HeroPower(S, lu[any])) any = i;
                if (Defs.Hero(lu[i]).Coef > 0f && (best < 0 || p > Meta.HeroPower(S, lu[best]))) best = i;
            }
            return best >= 0 ? best : any;
        }

        void Sweep(LevelDef st)
        {
            if (S.Stamina < Meta.BattleStamina)
            {
                Sfx.Play(Snd.Deny);
                MetaUi.StaminaShop(Layer);
                return;
            }
            var r = Meta.Sweep(S, st, new Rng(Environment.TickCount));
            if (r == null) return;
            Profile.Save();
            Sfx.Play(Snd.Coin);
            MetaUi.ShowRewards(Layer, $"扫荡 第{st.No}关", r);
        }

        void ArrowBtn(float x, float y, int dir)
        {
            var b = Ui.Icon(_page, "ui/h_arrow", 76, Vector2.zero, "arrow");
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0.5f, 0);
            b.rectTransform.anchoredPosition = new Vector2(x, y);
            if (dir > 0) b.rectTransform.localScale = new Vector3(-1, 1, 1);
            Ui.Tap(b, () =>
            {
                S.Selected = Mathf.Clamp(S.Selected + dir, 1, Mathf.Min(Defs.LevelCount, S.Cleared + 1));
                Profile.Save();
            });
        }

        const float SideStep = 134;

        /// <summary>两侧入口底下的半透明深色竖条，盖住 n 个入口。</summary>
        void SideStrip(float x, float y, int n)
        {
            float h = (n - 1) * SideStep + 140f;
            Ui.Box(Top(x, y + (n - 1) * SideStep * 0.5f - 4f, new Vector2(108, h), "strip"), new Vector2(108, h), Vector2.zero, Glass, Art.Pill);
        }

        /// <summary>两侧竖排入口：竖条上直接放图标，字压在图标下沿。</summary>
        void SideBtn(float x, float y, int idx, string icon, string name, bool dot, Action act, bool locked = false)
        {
            var b = Top(x, y + idx * SideStep, new Vector2(108, 130), "side_" + name);
            var hit = Ui.Box(b, new Vector2(108, 130), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            var ic = Ui.Icon(b, icon, 86, new Vector2(0, 14));
            if (locked)
            {
                ic.color = new Color(0.55f, 0.55f, 0.6f);
                Ui.Icon(b, "ic_lock", 36, new Vector2(28, -6));
            }
            Ui.Outlined(b, name, 27, Color.white, new Vector2(0, -42), new Vector2(130, 34), Pal.Ink, 2f);
            if (dot) MetaUi.Dot(b, new Vector2(38, 52)).rectTransform.localScale = Vector3.one * 0.7f;
            Ui.Tap(hit, act, b);
        }

        /// <summary>资源条下面一排小入口：只放图标，下面小字。</summary>
        void MiniBtn(float x, float y, string icon, string name, bool dot, Action act)
        {
            var b = Ui.Top(_page, x, y, new Vector2(118, 110), "mini_" + name);
            var hit = Ui.Box(b, new Vector2(118, 110), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            Ui.Icon(b, icon, 66, new Vector2(0, 14));
            Ui.Outlined(b, name, 24, Color.white, new Vector2(0, -32), new Vector2(130, 30), Pal.Ink, 1.8f);
            if (dot) MetaUi.Dot(b, new Vector2(30, 42)).rectTransform.localScale = Vector3.one * 0.6f;
            Ui.Tap(hit, act, b);
        }

        // ---------------- 好汉 ----------------

        /// <summary>换人模式：详情里点了「上阵」的好汉，等玩家点顶上要替换的位置。</summary>
        HeroId? _swap;
        int _popSlot = -1;

        public void BeginSwap(HeroId h)
        {
            _swap = h;
            Sfx.Play(Snd.Swap, 0.6f);
            Rebuild(false);
        }

        void EndSwap(int slot)
        {
            if (_swap.HasValue && slot >= 0)
            {
                var h = _swap.Value;
                Meta.SetLineup(S, slot, h);
                Profile.Save();
                Sfx.Play(Snd.Swap);
                Ui.Toast($"{Defs.Hero(h).Name} 上阵", 200);
                _popSlot = slot;
            }
            _swap = null;
            Rebuild(false);
        }

        void BuildHeroes(bool anim)
        {
            Cover("hall_bg", new Vector2(1080, 1920), Color.white, 0.3f);
            Shade(0.3f, 0f);
            float sp = Ui.Spare;

            // 上半：出战的四位站在场景里，下方三个阵容方案
            var line = Meta.Lineup(S);
            float luY = 420 + sp * 0.16f;
            var lu = Top(0, luY, new Vector2(1000, 300), "lineup");
            lu.localScale = Vector3.one * (1f + Mathf.Min(0.1f, sp / 2500f));
            for (int i = 0; i < 4; i++)
            {
                var h = line[i];
                var d = Defs.Hero(h);
                var g = Ui.Rect(lu, "lu" + i);
                g.anchoredPosition = new Vector2((i - 1.5f) * 250, 0);
                g.sizeDelta = new Vector2(240, 300);
                Ui.Box(g, new Vector2(230, 80), new Vector2(0, -104), MetaUi.RarityColor(d.Rarity).A(0.55f), Art.SoftDot, "aura");
                Ui.Box(g, new Vector2(190, 52), new Vector2(0, -108), new Color(0, 0, 0, 0.35f), Art.SoftDot, "shadow");
                var art = Ui.Icon(g, d.Art, 250, new Vector2(0, 14), "art");
                float ph = i * 0.7f;
                Tween.Run(art, 999f, k => art.rectTransform.anchoredPosition = new Vector2(0, 14 + 4 * Mathf.Sin(k * 999f * 2.2f + ph)));
                var plate = Ui.Skin(g, "plate", new Vector2(206, 50), new Vector2(0, -128), -1f, MetaUi.RarityColor(d.Rarity).Mul(1.3f));
                Ui.Outlined(plate.transform, $"Lv.{S.HeroLv[(int)h]} {d.Name}", 25, Color.white, new Vector2(0, 1), new Vector2(206, 48), Pal.Navy, 1.5f);
                int slot = i;
                if (_swap.HasValue)
                {
                    var arrow = Ui.Icon(g, "ui/arrow", 86, new Vector2(0, 160), "swapArrow");
                    arrow.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    arrow.color = Pal.Hex("8CFF6A");
                    float off = i * 0.4f;
                    Tween.Run(arrow, 999f, k => arrow.rectTransform.anchoredPosition = new Vector2(0, 160 + 12 * Mathf.Sin(k * 999f * 6f + off)));
                    Ui.Tap(art, () => EndSwap(slot), g);
                    Ui.Tap(arrow, () => EndSwap(slot), g);
                }
                else
                {
                    Ui.Tap(art, () => MetaUi.HeroDetail(Layer, h), g);
                    if (Meta.CanLevel(S, h)) MetaUi.Dot(g, new Vector2(98, -104));
                }
                if (anim) Tween.Pop(g, 0.3f, 0.05f + i * 0.05f);
                if (_popSlot == i)
                {
                    Tween.Pop(g, 0.35f);
                    MetaFx.Burst(lu, g.anchoredPosition, 10, 180);
                }
            }
            _popSlot = -1;

            float pgY = luY + 200;
            for (int i = 0; i < Meta.PresetCount; i++)
            {
                bool on = i == S.Preset;
                var dot = Top((i - 1) * 96, pgY, new Vector2(80, 80), "preset" + i);
                var face = Ui.Box(dot, Vector2.one * (on ? 72 : 60), Vector2.zero, on ? Pal.Gold : Pal.Hex("C8D4E0"), Art.Circle, "face");
                Ui.Box(dot, Vector2.one * (on ? 60 : 50), Vector2.zero, on ? Pal.Hex("FFE48A") : Pal.Hex("E6EEF6"), Art.Circle, "inner");
                Ui.Outlined(dot, (i + 1).ToString(), on ? 36 : 30, on ? Pal.Hex("7A4A08") : Pal.Hex("5A6A7A"), new Vector2(0, 2), new Vector2(80, 60), Color.white.A(0.6f), 1f);
                int pi = i;
                Ui.Tap(face, () =>
                {
                    if (pi == S.Preset) return;
                    Meta.UsePreset(S, pi);
                    Profile.Save();
                    Sfx.Play(Snd.Swap);
                    Ui.Toast($"阵容方案 {pi + 1}", 200);
                    _swap = null;
                    Rebuild(false);
                    var l = _page.Find("body/lineup");
                    if (l) for (int k = 0; k < l.childCount; k++) Tween.Pop(l.GetChild(k), 0.28f, k * 0.04f);
                }, dot);
            }

            var pw = Top(0, pgY + 86, new Vector2(420, 76), "power");
            Ui.Skin(pw, "plate", new Vector2(380, 70), Vector2.zero, -1f);
            Ui.Icon(pw, "ic_sword", 70, new Vector2(-120, 2));
            Ui.Outlined(pw, Ui.Num(Meta.TeamPower(S)), 40, Color.white, new Vector2(24, 2), new Vector2(240, 60), Pal.Navy, 2.5f);

            var cd = Top(0, pgY + 160, new Vector2(820, 64), "critBand");
            Ui.Skin(cd, "pill", new Vector2(760, 62), Vector2.zero, -1f);
            Ui.Outlined(cd, $"暴击伤害增幅：<color=#FFC93C>{Meta.BuildLoadout(S).CritDmg * 100:0.#}%</color>", 32, Color.white, new Vector2(-20, 1), new Vector2(680, 58), Pal.Ink, 2f);
            var tip = Ui.Box(cd, Vector2.one * 46, new Vector2(330, 0), Pal.Hex("E6EEF6"), Art.Circle, "tip");
            Ui.Outlined(tip.transform, "!", 32, Pal.Navy, new Vector2(0, 2), new Vector2(46, 46), Color.white, 1f);
            Ui.Tap(tip, () => Ui.Toast("任意好汉每升 1 级，全军暴击伤害 +1%", 200));

            // 下半：深色面板里的好汉卡
            float py = pgY + 210;
            var panel = Top(0, py, new Vector2(1060, 10), "roster");
            panel.pivot = new Vector2(0.5f, 1f);
            float ph2 = Mathf.Max(820f, Ui.Height - Ui.SafeTop - py - 176f);
            Ui.Skin(panel, "dark", new Vector2(1060, ph2), new Vector2(0, -ph2 * 0.5f), 1f);

            var roles = new List<int> { -1 };
            foreach (var r in Defs.RoleOrder) roles.Add((int)r);
            int owned = 0;
            for (int h = 0; h < Defs.HeroCount; h++) if (Meta.Owned(S, (HeroId)h)) owned++;
            for (int i = 0; i < roles.Count; i++)
            {
                int rf = roles[i];
                bool sel = rf == _roleFilter;
                var col = rf < 0 ? Pal.Hex("C89A4A") : MetaUi.RoleColor((Role)rf);
                var pos = new Vector2((i - (roles.Count - 1) * 0.5f) * 144, -54);
                var tb = Ui.Skin(panel, sel ? Ui.BtnSkin(col) : "row", new Vector2(136, 64), pos, -1f, sel ? Color.white : new Color(0.86f, 0.88f, 0.92f));
                if (rf >= 0) Ui.Icon(tb.transform, MetaUi.RoleArt((Role)rf), 38, new Vector2(-40, 2));
                Ui.Outlined(tb.transform, rf < 0 ? $"全部{owned}" : Defs.RoleName((Role)rf), rf < 0 ? 24 : 26, sel ? Color.white : col.Mul(0.75f), new Vector2(rf < 0 ? 0 : 16, 2), new Vector2(110, 60), sel ? Pal.Ink : Color.clear, sel ? 1.5f : 0f);
                Ui.Tap(tb, () =>
                {
                    if (_roleFilter == rf) return;
                    _roleFilter = rf;
                    _rosterScroll = 0;
                    Sfx.Play(Snd.Tab, 0.6f);
                    Rebuild(false);
                }, tb.transform, false);
            }

            var order = new List<HeroId>();
            for (int h = 0; h < Defs.HeroCount; h++)
                if (_roleFilter < 0 || (int)Defs.Hero((HeroId)h).Kind == _roleFilter) order.Add((HeroId)h);
            order.Sort((a, b) =>
            {
                bool oa = Meta.Owned(S, a), ob = Meta.Owned(S, b);
                if (oa != ob) return oa ? -1 : 1;
                bool la = Array.IndexOf(S.Lineup, (int)a) >= 0, lb = Array.IndexOf(S.Lineup, (int)b) >= 0;
                if (la != lb) return la ? -1 : 1;
                int r = Defs.Hero(b).Rarity.CompareTo(Defs.Hero(a).Rarity);
                if (r != 0) return r;
                if (!oa)
                {
                    int ua = Meta.UnlockLevelOf(a), ub = Meta.UnlockLevelOf(b);
                    if (ua != ub) return (ua == 0 ? 999 : ua).CompareTo(ub == 0 ? 999 : ub);
                }
                return S.HeroLv[(int)b] != S.HeroLv[(int)a] ? S.HeroLv[(int)b].CompareTo(S.HeroLv[(int)a]) : a.CompareTo(b);
            });

            float viewH = ph2 - 106;
            var view = Ui.Rect(panel, "view");
            view.anchorMin = view.anchorMax = new Vector2(0.5f, 1f);
            view.pivot = new Vector2(0.5f, 1f);
            view.sizeDelta = new Vector2(1030, viewH);
            view.anchoredPosition = new Vector2(0, -96);
            view.gameObject.AddComponent<RectMask2D>();
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit");
            var content = Ui.Rect(view, "content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            const float rowH = 318;
            int rows = (order.Count + 3) / 4;
            content.sizeDelta = new Vector2(1030, rows * rowH + 80);
            var top = Ui.Rect(content, "top");
            top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
            top.sizeDelta = Vector2.zero;
            for (int i = 0; i < order.Count; i++)
            {
                var c = HeroCard(top, order[i], new Vector2(((i % 4) - 1.5f) * 250, -(162 + (i / 4) * rowH)));
                if (anim && i < 12) Tween.Pop(c, 0.28f, 0.04f + i * 0.025f);
            }
            Ui.Outlined(top, "碎片来源：通关 · 擂鼓招募 · 签到 · 章节宝箱", 26, Pal.Cream, new Vector2(0, -(rows * rowH + 34)), new Vector2(900, 44), Pal.Ink, 1.5f);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30;
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(_rosterScroll, 0, Mathf.Max(0, content.sizeDelta.y - viewH)));
            sr.onValueChanged.AddListener(_ => _rosterScroll = content.anchoredPosition.y);

            if (_swap.HasValue)
            {
                var sh = _swap.Value;
                var dim = Ui.Box(panel, new Vector2(1060, ph2), new Vector2(0, -ph2 * 0.5f), new Color(0.02f, 0.05f, 0.1f, 0.78f), Art.Rounded, "swapDim");
                dim.raycastTarget = true;
                Ui.Tap(dim, () => EndSwap(-1), null, false);
                var card = HeroCard(panel, sh, new Vector2(0, -ph2 * 0.36f));
                card.localScale = Vector3.one * 1.3f;
                foreach (var g in card.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                var glow = Ui.Box(panel, new Vector2(520, 560), new Vector2(0, -ph2 * 0.36f), Pal.Gold.A(0.4f), Art.SoftDot, "glow");
                glow.transform.SetSiblingIndex(card.GetSiblingIndex());
                Ui.Outlined(panel, "点击上方要替换的好汉", 40, Pal.Hex("FFE08A"), new Vector2(0, -ph2 * 0.36f - 260), new Vector2(900, 56), Pal.Ink, 2.5f);
                Ui.Btn(panel, "取消", new Vector2(240, 96), new Vector2(0, -ph2 * 0.36f - 360), Pal.Hex("A08A70"), () => EndSwap(-1), 38);
            }
        }

        /// <summary>好汉卡：职业圆标、出战中、等级圈、碎片条、可升级绿箭头；未获得的压暗加锁并写来源。</summary>
        RectTransform HeroCard(Transform parent, HeroId h, Vector2 pos)
        {
            var d = Defs.Hero(h);
            int lv = S.HeroLv[(int)h];
            bool owned = lv > 0;
            var c = Ui.Rect(parent, "card_" + h);
            c.anchoredPosition = pos;
            c.sizeDelta = new Vector2(236, 300);
            var face = MetaUi.Card(c, owned ? MetaUi.RarityCard(d.Rarity) : "card_lock", new Vector2(230, 250), new Vector2(0, 22));
            var art = Ui.Icon(c, d.Art, 216, new Vector2(0, 44));
            if (!owned) art.color = new Color(0.1f, 0.1f, 0.13f, 0.85f);

            Ui.Box(c, Vector2.one * 58, new Vector2(-84, 120), Color.white, Art.Circle, "roleRing");
            Ui.Box(c, Vector2.one * 50, new Vector2(-84, 120), owned ? MetaUi.RoleColor(d.Kind) : Pal.Slate, Art.Circle, "roleBg");
            Ui.Icon(c, MetaUi.RoleArt(d.Kind), 38, new Vector2(-84, 121));
            Ui.Outlined(c, d.Name, 30, owned ? Color.white : Pal.Hex("B8C0CC"), new Vector2(0, -128), new Vector2(230, 42), Pal.Ink, 2f);

            if (owned)
            {
                if (Array.IndexOf(S.Lineup, (int)h) >= 0)
                {
                    var on = Ui.Icon(c, "ui/tag", 118, new Vector2(-54, -30), "onTag");
                    Ui.Outlined(on.transform, "出战中", 24, Color.white, new Vector2(-6, 2), new Vector2(110, 50), Pal.RedDeep, 1.5f);
                }
                var lvc = Ui.Icon(c, "ui/lv", 60, new Vector2(-86, -76), "lv");
                Ui.Outlined(lvc.transform, lv.ToString(), 28, Pal.Hex("FFE7B0"), new Vector2(0, 1), new Vector2(60, 40), Pal.Ink, 1.5f);
                int need = Meta.ShardCost(lv);
                bool max = lv >= Meta.MaxHeroLv;
                bool can = Meta.CanLevel(S, h);
                MetaUi.Bar(c, new Vector2(16, -76), new Vector2(128, 26), max ? 1 : S.Shards[(int)h] / (float)need, can ? Pal.Hex("4FB548") : Pal.Hex("4FA8D8"),
                    max ? "满级" : $"{S.Shards[(int)h]}/{need}");
                if (can)
                {
                    var up = Ui.Icon(c, "ic_fast", 46, new Vector2(98, -70), "up");
                    up.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    up.color = Pal.Hex("7CFF5A");
                    Tween.Run(up, 999f, k => up.rectTransform.anchoredPosition = new Vector2(98, -70 + 5 * Mathf.Sin(k * 999f * 6f)));
                    MetaUi.Dot(c, new Vector2(100, 136));
                }
            }
            else
            {
                Ui.Icon(c, "ic_lock", 50, new Vector2(84, 120));
                int at = Meta.UnlockLevelOf(h);
                string src = at > 0 ? $"第{at}关入伙" : d.Rarity >= 4 && !Meta.OrangeOpened(S) ? $"第{Meta.OrangeOpen}关后可招" : "招募获得";
                Ui.Outlined(c, src, 24, Pal.Hex("D8D0C0"), new Vector2(0, -76), new Vector2(220, 36), Pal.Ink, 1.5f);
            }
            Ui.Tap(face, () => MetaUi.HeroDetail(Layer, h), c);
            return c;
        }

        // ---------------- 寨主（装备） ----------------

        void BuildForge(bool anim)
        {
            Cover("forge_bg", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0.3f, 0.25f);
            float sp = Ui.Spare;
            var hdr = Top(0, 228 + sp * 0.05f, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "寨主 · 宋江", Vector2.zero, 520);
            var lo = Meta.BuildLoadout(S);
            var st = Top(0, 340 + sp * 0.08f, new Vector2(980, 80), "stats");
            Ui.Skin(st, "pill", new Vector2(960, 70), Vector2.zero, -1f);
            Ui.Icon(st, "ic_sword", 54, new Vector2(-420, 0));
            Ui.Outlined(st, $"攻击力 {lo.Atk:0}    暴击 {lo.Crit * 100:0.#}%    暴伤 {lo.CritDmg * 100:0}%", 32, Pal.Hex("FFE7B0"), new Vector2(20, 0), new Vector2(860, 64), Pal.Ink, 2f);
            var sk = Top(0, 418 + sp * 0.1f, new Vector2(580, 60), "lordSkill");
            var skBg = Ui.Box(sk, new Vector2(580, 58), Vector2.zero, Pal.Hex("3A2410").A(0.85f), Art.Pill);
            Ui.Icon(sk, "ic_sword", 46, new Vector2(-252, 0)).color = Pal.Gold;
            Ui.Outlined(sk, $"{Defs.LordSkillName}：每{lo.LordCd:0.#}秒 · {lo.LordPct * 100:0}%攻击力", 27, Pal.Hex("FFD27A"), new Vector2(20, 1), new Vector2(530, 54), Pal.Ink, 2f);
            Ui.Tap(skBg, () => Ui.Toast($"天赋每10层 +{Meta.LordPctStep * 100:0}% 伤害，号角减冷却", 360), sk);

            var sj = Top(0, 1000 + sp * 0.4f, new Vector2(640, 640), "songjiang");
            sj.localScale = Vector3.one * (1f + Mathf.Min(0.2f, sp / 2000f));
            Ui.Box(sj, new Vector2(700, 380), new Vector2(0, -200), Pal.Gold.A(0.35f), Art.SoftDot);
            Ui.Box(sj, new Vector2(400, 90), new Vector2(0, -290), new Color(0, 0, 0, 0.3f), Art.SoftDot);
            var art = Ui.Icon(sj, "prop_songjiang", 640, Vector2.zero);
            Tween.Run(art, 999f, k => art.rectTransform.anchoredPosition = new Vector2(0, 5 * Mathf.Sin(k * 999f * 1.6f)));
            if (anim) Tween.Pop(sj, 0.35f);

            for (int i = 0; i < Meta.Equips.Length; i++)
            {
                float x = i < 3 ? -390 : 390;
                float y = 500 + sp * 0.22f + (i % 3) * (270 + sp * 0.17f);
                var e = Meta.Equips[i];
                int lv = S.EquipLv[i];
                int q = S.EquipQ[i];
                var c = Top(x, y, new Vector2(220, 230), "eq" + i);
                var face = MetaUi.Slot(c, MetaUi.QualitySlot(q), 196, new Vector2(0, 26));
                Ui.Icon(c, e.Icon, 150, new Vector2(0, 30));
                var lvb = Ui.Skin(c, "pill", new Vector2(116, 44), new Vector2(46, -50), -1f);
                Ui.Outlined(lvb.transform, "Lv." + lv, 26, Color.white, new Vector2(0, 1), new Vector2(116, 44), Pal.Ink, 1.5f);
                if (q > 0) Ui.Outlined(c, Meta.QualityName[q], 22, MetaUi.QualityColor(q), new Vector2(-58, 106), new Vector2(80, 30), Pal.Ink, 1.5f);
                Ui.Outlined(c, e.Name, 30, Color.white, new Vector2(0, -96), new Vector2(220, 44), Pal.Navy, 2.5f);
                if (Meta.CanForge(S, i)) MetaUi.Dot(c, new Vector2(88, 112));
                int si = i;
                Ui.Tap(face, () => MetaUi.ForgeSlot(Layer, si), c);
                if (anim) Tween.Pop(c, 0.3f, 0.1f + i * 0.05f);
            }

            var sum = Bottom(0, 440 + sp * 0.12f, new Vector2(980, 300), "summary");
            Ui.Panel(sum, new Vector2(940, 280), Vector2.zero);
            for (int i = 0; i < Meta.Equips.Length; i++)
            {
                int lv = S.EquipLv[i];
                string brk = Meta.Breaks(lv) > 0 ? $" <color=#2E9A38>{Defs.RoleName(Meta.Equips[i].Role)}+{Meta.Breaks(lv) * 4}%</color>" : "";
                Ui.Label(sum, Meta.EquipText(i, lv, S.EquipQ[i]) + brk, 26, Pal.Ink, new Vector2(i < 3 ? -220 : 220, 84 - (i % 3) * 50), new Vector2(440, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            }
            Ui.Label(sum, $"强化上限 Lv.{Meta.EquipMax(S)}（随通关提升）", 26, Pal.Hex("8A4A1A"), new Vector2(-170, -88), new Vector2(560, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
            MetaUi.Pill(sum, "ic_iron", Ui.Num(S.Iron), new Vector2(320, -88), 220);

            float ey = 640 + sp * 0.16f;
            Action re = () => Rebuild(false);
            ForgeEntry(-375, ey, "ic_scroll", "天赋", $"{S.Talent}/{Meta.TalentCap(S)}", Meta.CanTalent(S), () => MetaUi.TalentTree(Layer, re));
            ForgeEntry(-125, ey, "ic_tome", "天书", $"上卷 Lv.{S.PageLv}", Meta.CanPage(S) || Meta.CanTome(S), () => MetaUi.Tome(Layer, re));
            ForgeEntry(125, ey, "ic_flag", "杏黄旗", S.FlagLv > 0 ? $"Lv.{S.FlagLv}" : "未打造", Meta.CanFlagUp(S), () => MetaUi.Flag(Layer, re));
            ForgeEntry(375, ey, "ic_gearbox", "装备箱", $"×{S.GearBoxes}", S.GearBoxes > 0, () => MetaUi.GearBox(Layer, re));
        }

        void ForgeEntry(float x, float y, string icon, string name, string sub, bool dot, Action act)
        {
            var b = Bottom(x, y, new Vector2(236, 120), "entry_" + name);
            var bg = Ui.Box(b, new Vector2(236, 116), Vector2.zero, Glass.A(0.82f), Art.Rounded, "bg");
            Ui.Box(b, new Vector2(230, 110), Vector2.zero, Pal.Hex("6E4A32").A(0.6f), Art.Rounded, "inner");
            Ui.Icon(b, icon, 84, new Vector2(-66, 2));
            Ui.Outlined(b, name, 30, Pal.Hex("FFE7B0"), new Vector2(38, 18), new Vector2(150, 40), Pal.Ink, 1.5f);
            Ui.Outlined(b, sub, 22, Color.white, new Vector2(38, -22), new Vector2(150, 32), Pal.Ink, 1.2f);
            if (dot) MetaUi.Dot(b, new Vector2(104, 46)).rectTransform.localScale = Vector3.one * 0.7f;
            Ui.Tap(bg, act, b);
        }

        // ---------------- 山寨地图 ----------------

        static readonly (string Name, float X, float Y, string Icon)[] Buildings =
        {
            ("聚义厅", 0.52f, 0.16f, "tab_task"), ("铁匠铺", 0.20f, 0.28f, "tab_forge"), ("校场", 0.79f, 0.34f, "tab_hero"),
            ("酒馆", 0.19f, 0.48f, "tab_shop"), ("金库", 0.82f, 0.55f, "tab_idle"), ("水寨", 0.30f, 0.68f, "en_porter"), ("山门", 0.51f, 0.84f, "tab_battle"),
        };

        void BuildFort(bool anim)
        {
            var size = Cover("map_fort", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0.3f, 0.2f);
            var hdr = Top(0, 228, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "水泊梁山", Vector2.zero, 520);
            string[] tips = { "每日任务", "强化装备", "好汉升级", "擂鼓招募", "巡山收益", "截取生辰纲", "出征" };
            for (int i = 0; i < Buildings.Length; i++)
            {
                var b = Buildings[i];
                var pos = new Vector2((b.X - 0.5f) * size.x, (0.5f - b.Y) * size.y);
                var c = Ui.Rect(_page, "bld" + i);
                c.anchoredPosition = pos + new Vector2(0, -60);
                c.sizeDelta = new Vector2(240, 120);
                var plate = Ui.Skin(c, "plate", new Vector2(250, 68), Vector2.zero, -1f);
                Ui.Icon(c, b.Icon, 74, new Vector2(-82, 4));
                Ui.Outlined(c, b.Name, 32, Color.white, new Vector2(22, 1), new Vector2(160, 50), Pal.Navy, 2f);
                Ui.Label(c, tips[i], 22, Color.white, new Vector2(0, -46), new Vector2(240, 30), TextAnchor.MiddleCenter, Fonts.Heavy).gameObject.AddComponent<Outline>().effectColor = Pal.Navy;
                bool dot = i == 0 ? Meta.TaskBadge(S) > 0 : i == 1 ? ForgeBadge() : i == 2 ? HeroBadge() : i == 3 ? ShopBadge() : i == 4 ? IdleBadge() :
                    i == 5 ? S.Cleared >= MetaUi.EscortUnlock && S.EscortTries < Meta.EscortTriesMax : false;
                if (dot) MetaUi.Dot(c, new Vector2(100, 28));
                if (i == 4)
                {
                    var r = Meta.IdleRewards(S, Meta.IdleHours(S, Profile.Now));
                    var bub = Ui.Skin(c, "row", new Vector2(190, 58), new Vector2(0, 84));
                    Ui.Icon(bub.transform, "ic_coins", 52, new Vector2(-66, 2));
                    Ui.Label(bub.transform, Ui.Num(r.Get(Cur.Copper)), 28, Pal.Ink, new Vector2(16, 0), new Vector2(130, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
                    Tween.Run(bub, 999f, k => bub.rectTransform.anchoredPosition = new Vector2(0, 82 + 6 * Mathf.Sin(k * 999f * 3f)));
                }
                int bi = i;
                Ui.Tap(plate, () => OnBuilding(bi), c);
                if (anim) Tween.Pop(c, 0.3f, 0.1f + i * 0.05f);
            }
        }

        void OnBuilding(int i)
        {
            switch (i)
            {
                case 0: MetaUi.Tasks(Layer); break;
                case 1: SetTab(TabForge); break;
                case 2: SetTab(TabHero); break;
                case 3: SetTab(TabShop); break;
                case 4: MetaUi.Idle(Layer); break;
                case 5:
                    if (S.Cleared < MetaUi.EscortUnlock) Ui.Toast($"通关第 {MetaUi.EscortUnlock} 关后开放", 200);
                    else MetaUi.Escort(Layer);
                    break;
                default: SetTab(TabBattle); break;
            }
        }

        // ---------------- 商城（招募 + 商店） ----------------

        void BuildShop(bool anim)
        {
            Cover("hall_bg", new Vector2(1080, 1920), Color.white, 0.3f);
            Shade(0.3f, 0f);
            float sp = Ui.Spare;
            var ban = Top(0, 560 + sp * 0.1f, new Vector2(1040, 540), "banner");
            Ui.Skin(ban, "panel", new Vector2(1036, 576), Vector2.zero);
            var img = Ui.Box(ban, new Vector2(900, 506), Vector2.zero, Color.white, Art.Img("recruit_banner"));
            img.type = Image.Type.Simple;
            Ui.Box(ban, new Vector2(900, 116), new Vector2(0, -195), Pal.Navy.A(0.6f), Art.White);
            var hdr = Top(0, 228 + sp * 0.08f, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "擂鼓招募", Vector2.zero, 520);
            if (Meta.OrangeOpened(S))
            {
                Ui.Outlined(ban, $"传说 {Meta.OrangeRate(S) * 100:0}% · 史诗 {Meta.PurpleRate(S) * 100:0}% · 十连必出史诗", 28, Pal.Hex("FFE7B0"), new Vector2(0, -170), new Vector2(880, 40), Pal.Ink, 1.5f);
                int left = Meta.OrangePity - S.Pity;
                Ui.Outlined(ban, $"再招募 {left} 次必得传说好汉", 30, Pal.Hex("FFB050"), new Vector2(0, -218), new Vector2(880, 44), Pal.Ink, 2f);
            }
            else
            {
                Ui.Outlined(ban, $"史诗 {Meta.PurpleRate(S) * 100:0}% · 十连必出史诗", 28, Pal.Hex("FFE7B0"), new Vector2(0, -170), new Vector2(880, 40), Pal.Ink, 1.5f);
                Ui.Outlined(ban, $"传说好汉：通关第 {Meta.OrangeOpen} 关后开放", 30, Pal.Hex("FFB050"), new Vector2(0, -218), new Vector2(880, 44), Pal.Ink, 2f);
            }
            var lv = Ui.Rect(ban, "lv");
            lv.anchoredPosition = new Vector2(-250, 170);
            Ui.Skin(lv, "pill", new Vector2(380, 74), Vector2.zero, -1f);
            Ui.Outlined(lv, $"招募等级 {S.RecruitLv}", 28, Pal.Hex("FFE08A"), new Vector2(-70, 12), new Vector2(220, 36), Pal.Ink, 1.5f);
            MetaUi.Bar(lv, new Vector2(0, -16), new Vector2(330, 18), S.RecruitLv >= Meta.RecruitMaxLv ? 1 : S.RecruitExp / (float)Meta.RecruitNeed(S.RecruitLv), Pal.Gold);
            MetaUi.Pill(ban, "ic_token", S.Tokens.ToString(), new Vector2(340, 170), 200);

            int adLeft = Meta.AdRecruitMax - S.AdRecruit;
            var b1 = Ui.Btn(_page, "招募", new Vector2(300, 120), Vector2.zero, Pal.Hex("E8862A"), () => DoRecruit(1, false), 42, "ic_token");
            float by = 910 + sp * 0.18f;
            Place(b1, -340, by);
            Ui.Outlined(b1, "招募令×1", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            var b10 = Ui.Btn(_page, "十连", new Vector2(300, 120), Vector2.zero, Pal.Hex("D8452F"), () => DoRecruit(10, false), 42, "ic_token");
            Place(b10, 0, by);
            Ui.Outlined(b10, "招募令×10", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            var ba = Ui.Btn(_page, adLeft > 0 ? "免费" : "明日", new Vector2(300, 120), Vector2.zero, adLeft > 0 ? Pal.Teal : Pal.Slate, () => DoRecruit(1, true), 42, "ic_ad");
            Place(ba, 340, by);
            Ui.Outlined(ba, $"今日剩余 {adLeft} 次", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            if (S.Tokens >= 1) MetaUi.Dot(b1, new Vector2(140, 54));

            float my = 1040 + sp * 0.26f;
            var market = Top(0, my, new Vector2(1040, 10), "market");
            market.pivot = new Vector2(0.5f, 1f);
            float mh = Mathf.Max(600f, Ui.Height - Ui.SafeTop - my - 186f);
            float me = mh - 620f;
            Ui.Skin(market, "dark", new Vector2(1040, mh), new Vector2(0, -mh * 0.5f));
            var shopHdr = Top(0, my, new Vector2(900, 60), "shopHdr");
            Ui.Skin(shopHdr, "plaque", new Vector2(380, 72), Vector2.zero, -1f);
            Ui.Outlined(shopHdr, "梁山集市", 34, Pal.Hex("FFE08A"), new Vector2(0, 2), new Vector2(360, 60), Pal.Ink, 2f);
            var items = Meta.Shop(S);
            for (int i = 0; i < items.Length; i++)
            {
                var c = ShopCard(items[i], i);
                Place(c, ((i % 4) - 1.5f) * 255, my + 175 + me * 0.25f + (i / 4) * (270 + me * 0.2f));
                if (anim) Tween.Pop(c, 0.3f, 0.05f + i * 0.04f);
            }
        }

        void Place(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(x, -(y + Ui.SafeTop));
        }

        RectTransform ShopCard(Meta.ShopDef d, int i)
        {
            var c = Ui.Rect(_page, "shop" + i);
            c.sizeDelta = new Vector2(240, 250);
            var face = Ui.Skin(c, "tab_on", new Vector2(240, 250), Vector2.zero);
            Ui.Label(c, d.Name, 28, Pal.Hex("5A3418"), new Vector2(0, 92), new Vector2(220, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Icon(c, d.Icon, 110, new Vector2(0, 26));
            Ui.Outlined(c, "×" + Ui.Num(d.Get.N), 28, Color.white, new Vector2(54, -16), new Vector2(120, 36), Pal.Ink, 2f);
            int used = d.Ad ? (d.Get.Cur == Cur.Ingot ? S.AdIngots : S.AdStamina) : 0;
            int max = d.Ad ? (d.Get.Cur == Cur.Ingot ? Meta.AdIngotsMax : Meta.AdStaminaMax) : 0;
            bool soldOut = d.Ad && used >= max;
            var pb = Ui.Skin(c, Ui.BtnSkin(soldOut ? Pal.Slate : d.Ad ? Pal.Teal : Pal.Hex("F2B632")), new Vector2(206, 68), new Vector2(0, -84), -1f);
            if (d.Ad)
            {
                Ui.Icon(pb.transform, "ic_ad", 48, new Vector2(-66, 2));
                Ui.Outlined(pb.transform, soldOut ? "已领完" : $"免费 {max - used}", 26, Color.white, new Vector2(18, 1), new Vector2(150, 56), Pal.Ink, 1.5f);
            }
            else
            {
                Ui.Icon(pb.transform, "ic_ingot", 52, new Vector2(-62, 2));
                Ui.Outlined(pb.transform, d.Price.ToString(), 28, Color.white, new Vector2(18, 1), new Vector2(140, 56), Pal.Ink, 1.5f);
            }
            Ui.Tap(face, () => Buy(d), c);
            Ui.Tap(pb, () => Buy(d), c);
            return c;
        }

        void Buy(Meta.ShopDef d)
        {
            if (d.Ad)
            {
                bool ingot = d.Get.Cur == Cur.Ingot;
                if ((ingot ? S.AdIngots >= Meta.AdIngotsMax : S.AdStamina >= Meta.AdStaminaMax)) { Ui.Toast("今日次数已用完", 200); return; }
                Ads.Show(ingot ? "shop_ingot" : "shop_stamina", () =>
                {
                    if (ingot) S.AdIngots++;
                    else S.AdStamina++;
                    Meta.Give(S, d.Get);
                    Profile.Save();
                    MetaUi.ShowRewards(Layer, "获得", new List<Item> { d.Get });
                });
                return;
            }
            if (!Meta.Spend(S, Cur.Ingot, d.Price))
            {
                Sfx.Play(Snd.Deny);
                Ui.Toast("元宝不足：看广告、做任务可得", 200);
                return;
            }
            Meta.Give(S, d.Get);
            Profile.Save();
            Sfx.Play(Snd.Coin);
            MetaUi.ShowRewards(Layer, "购买成功", new List<Item> { d.Get });
        }

        public void DoRecruit(int n, bool ad)
        {
            if (ad)
            {
                if (S.AdRecruit >= Meta.AdRecruitMax) { Ui.Toast("今日免费次数已用完", 200); return; }
                Ads.Show("recruit", () =>
                {
                    S.AdRecruit++;
                    var res = Meta.Recruit(S, 1, new Rng(Environment.TickCount));
                    Profile.Save();
                    MetaUi.RecruitResult(Layer, res);
                });
                return;
            }
            if (!Meta.Spend(S, Cur.Token, n))
            {
                Sfx.Play(Snd.Deny);
                Ui.Toast("招募令不足：集市用元宝兑换", 200);
                return;
            }
            var r = Meta.Recruit(S, n, new Rng(Environment.TickCount));
            Profile.Save();
            MetaUi.RecruitResult(Layer, r);
        }
    }
}

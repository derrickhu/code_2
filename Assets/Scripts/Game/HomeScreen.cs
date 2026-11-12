using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>主界面：顶栏资源、五个页签（商城/好汉/出征/寨主/山寨），弹窗都挂在 Layer 上。</summary>
    public sealed partial class HomeScreen : MonoBehaviour
    {
        public const int TabShop = 0, TabHero = 1, TabBattle = 2, TabForge = 3, TabFort = 4;

        RectTransform _root, _page;
        public RectTransform Layer { get; private set; }
        int _tab = TabBattle;
        /// <summary>好汉页职业筛选：-1 全部。</summary>
        int _roleFilter = -1;
        float _rosterScroll;
        int _forgeSubTab, _totemFilter = -1, _selectedTotemId, _shopSubTab;
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
            BuildGm();
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

        /// <summary>顶、底各压一层墨褐渐变，只为托住顶栏和页签上的白字，不把画面压暗。</summary>
        void Shade(float top, float bottom)
        {
            var tint = Ink;
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

        static readonly Color Glass = new Color(0.06f, 0.13f, 0.21f, 0.54f);
        static readonly Color DockEdge = Pal.Hex("5EC4E8").A(0.32f);
        static readonly Color TopPill = new Color(0.08f, 0.15f, 0.24f, 0.72f);

        /// <summary>冷白瓷面上的深靛字。</summary>
        static readonly Color Cocoa = Pal.Hex("243B53");
        /// <summary>压在晴空场景上的白字描边。</summary>
        static readonly Color Edge = Pal.Hex("12283E");
        /// <summary>主界面「青绿水泊 · 霁蓝宋瓷」：深靛描边、鎏金字、冷瓷白字、霁蓝描边、鎏金深琥珀描边。</summary>
        static readonly Color Ink = Pal.Hex("0F1E2E");
        static readonly Color Gold = Pal.Hex("FFE066");
        static readonly Color Paper = Pal.Hex("F5FAFF");
        static readonly Color AzureDeep = Pal.Hex("0E3554");
        static readonly Color GoldStroke = Pal.Hex("6B4206");

        void BuildTopBar()
        {
            float y = Ui.SafeTop + 62;

            // 头像：香槟金青瓷双线框 + 半身统帅胸像裁切（避免全身立绘缩成蚂蚁）
            var av = Ui.Top(_page, -470, y, new Vector2(116, 116), "avatar");
            var frame = Ui.Icon(av, "ui/h_avatar", 116, Vector2.zero, "frame");
            var avMask = Ui.Rect(av, "avMask");
            avMask.sizeDelta = new Vector2(94, 94);
            avMask.anchoredPosition = new Vector2(0, 1);
            avMask.gameObject.AddComponent<RectMask2D>();
            Ui.Icon(avMask, "lord_songjiang", 204, new Vector2(0, -48));
            int lv = Meta.LordLevel(S, out float xp);
            Ui.Icon(av, "ui/h_round", 48, new Vector2(-43, -41), "lvBadge");
            Ui.Outlined(av, lv.ToString(), 24, Gold, new Vector2(-43, -40), new Vector2(46, 40), Ink, 1.5f);
            Ui.Tap(frame, () => SetTab(TabForge), av);
            var nm = Ui.Top(_page, -318, y - 18, new Vector2(176, 52), "name");
            Ui.Skin(nm, "h_capsule", new Vector2(176, 52), Vector2.zero, -1f);
            Ui.Outlined(nm, "宋江", 28, Paper, new Vector2(0, 1), new Vector2(160, 46), Ink, 1.5f);
            var xb = Ui.Top(_page, -318, y + 24, new Vector2(170, 18), "xp");
            Ui.Box(xb, new Vector2(164, 18), Vector2.zero, Ink.A(0.85f), Art.Pill);
            var xf = Ui.Box(xb, new Vector2(Mathf.Max(16, 158 * xp), 12), new Vector2(-79, 0), Pal.Hex("4AD6C8"), Art.Pill, "fill");
            xf.rectTransform.pivot = new Vector2(0, 0.5f);

            CurPill(-50, y - 14, "ic_ingot", Ui.Num(S.Ingots), () => SetTab(TabShop));
            CurPill(184, y - 14, "ic_coins", Ui.Num(S.Copper), null);
            var st = CurPill(414, y - 14, "ic_wine", $"{S.Stamina}/{Meta.StaminaMax}", () => MetaUi.StaminaShop(Layer));
            _staminaText = st.Find("text").GetComponent<Text>();
            _staminaWait = Ui.Outlined(st, "", 20, Color.white, new Vector2(8, -40), new Vector2(160, 26), Edge, 1.2f);
            _tickT = 0;
        }

        /// <summary>资源胶囊：深靛银边，冷瓷白字；左侧图标压边，右侧高清翠绿圆形加号。</summary>
        RectTransform CurPill(float x, float y, string icon, string text, Action plus)
        {
            var c = Ui.Top(_page, x, y, new Vector2(204, 56), "cur_" + icon);
            Ui.Skin(c, "h_capsule", new Vector2(188, 52), new Vector2(8, 0), -1f, null, "bg");
            Ui.Icon(c, icon, 60, new Vector2(-82, 1));
            Ui.Outlined(c, text, 28, Paper, new Vector2(plus != null ? 4 : 14, 1), new Vector2(130, 46), Ink, 1.6f, TextAnchor.MiddleCenter, "text");
            if (plus != null)
            {
                var pRim = Ui.Box(c, new Vector2(38, 38), new Vector2(84, 0), Pal.Hex("123E22"), Art.Circle, "plusRim");
                Ui.Box(pRim.transform, new Vector2(33, 33), Vector2.zero, Pal.Hex("FFFFFF").A(0.85f), Art.Circle);
                var pFace = Ui.Box(pRim.transform, new Vector2(29, 29), Vector2.zero, Pal.Hex("42D86B"), Art.Circle, "plus");
                Ui.Outlined(pRim.transform, "+", 26, Color.white, new Vector2(0, 2), new Vector2(38, 38), Pal.Hex("124424"), 1.3f);
                Ui.Tap(pFace, plus, c);
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
            var under = Ui.Box(bar, new Vector2(1080, TabH), Vector2.zero, Pal.Hex("0E1B2A"), Art.White, "under");
            Stretch(under.rectTransform);
            under.rectTransform.offsetMin = new Vector2(0, -Ui.SafeBottom - 40);
            under.rectTransform.offsetMax = new Vector2(0, -40);
            under.raycastTarget = true;
            var plank = Ui.Skin(bar, "h_tabbar", new Vector2(1100, TabH + 20), new Vector2(0, 4), -1f, null, "plank");
            plank.raycastTarget = true;
            string[] names = { "商城", "好汉", "出征", "寨主", "山寨" };
            string[] icons = { "tab_shop", "tab_hero", "tab_battle", "tab_forge", "tab_fort" };
            // 降噪原则：当前激活页签不重复挂红点；出征与山寨不在底栏重复亮红点（功能入口本身已有角标）
            bool[] dots = { ShopBadge(), HeroBadge(), false, ForgeBadge(), false };
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
                    Ui.Box(tab, new Vector2(250, 190), new Vector2(0, 34), Pal.Hex("5AD4FF").A(0.28f), Art.SoftDot, "glow");
                    Ui.Icon(tab, "ui/h_tab_on", 178, new Vector2(0, 28), "badge");
                }
                var ic = Ui.Icon(tab, icons[i], on ? 128 : 94, new Vector2(0, on ? 44 : 16));
                if (on) Tween.Run(ic, 999f, k => ic.rectTransform.anchoredPosition = new Vector2(0, 44 + 4 * Mathf.Sin(k * 999f * 3f)));
                if (on) Ui.Outlined(tab, names[i], 30, Color.white, new Vector2(0, -48), new Vector2(210, 40), AzureDeep, 2f);
                else Ui.Outlined(tab, names[i], 26, Pal.Hex("B8CDE0"), new Vector2(0, -50), new Vector2(210, 40), Ink, 1.5f);
                if (dots[i] && !on) MetaUi.Dot(tab, new Vector2(54, 48));
                int ti = i;
                Ui.Tap(face, () => SetTab(ti), tab, false);
            }
        }

        // 看视频才有的次数不亮红点，只有手里有招募令或寻宝锤才提醒。
        static bool ShopBadge() => S.Tokens >= 1 || S.Hammers >= 1;
        static bool HeroBadge()
        {
            for (int h = 0; h < Defs.HeroCount; h++) if (Meta.CanLevel(S, (HeroId)h)) return true;
            return false;
        }
        static bool ForgeBadge()
        {
            for (int i = 0; i < Meta.Equips.Length; i++) if (Meta.CanForge(S, i)) return true;
            return Meta.CanTalent(S) || Meta.CanPage(S) || Meta.CanTome(S) || Meta.CanFlagUp(S) || Meta.CanCloakUp(S) || Meta.CanMergeAnyTotem(S) || S.GearBoxes > 0;
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
        /// 主页为青绿水泊梁山寨门：门楼挂书法标题，城门前站本关首领，脚下霁蓝关卡绶带；
        /// 青石御道站出战四好汉（脚踏青玉石台），下方承托式宝箱进度卡，底部唯一鎏金主行动按钮「开始挑战」。
        /// 左右两列功能入口收纳进半透明深靛毛玻璃侧坞（左：补给挂机；右：目标图鉴），视觉分区井然有序。
        /// </summary>
        void BuildBattle(bool anim)
        {
            var st = Defs.Level(S.Selected);
            Cover("home_road", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0.24f, 0.22f);
            float sp = Ui.Spare;
            int day = Profile.Today;

            var gear = Top(-474, 158, new Vector2(76, 76), "settings");
            var gearBg = Ui.Icon(gear, "ui/h_round", 68, Vector2.zero, "bg");
            Ui.Icon(gear, "ic_gear", 46, new Vector2(0, 1));
            Ui.Tap(gearBg, () => MetaUi.Settings(Layer), gear);

            float colY = 264 + sp * 0.04f;
            float colStep = 132 + sp * 0.025f;
            SideDock(-468, colY, colStep, 4);
            SideBtn(-468, colY, "tab_welfare", "福利", Meta.CanWelfare(S, day), () => MetaUi.Welfare(Layer));
            SideBtn(-468, colY + colStep, "tab_gift", "签到", Meta.CanSign(S, day), () => MetaUi.SignIn(Layer));
            SideBtn(-468, colY + colStep * 2, "ic_feast", "聚义宴", MealBadge(), () => MetaUi.Meals(Layer));
            SideBtn(-468, colY + colStep * 3, "tab_idle", "巡山", IdleBadge(), () => MetaUi.Idle(Layer));

            int rightCount = Meta.SevenFinished(S) ? 3 : 4;
            SideDock(468, colY, colStep, rightCount);
            int side = 0;
            SideBtn(468, colY + colStep * side++, "tab_task", "任务", Meta.TaskBadge(S) > 0, () => MetaUi.Tasks(Layer));
            if (!Meta.SevenFinished(S)) SideBtn(468, colY + colStep * side++, "tab_seven", "七日", SevenBadge(), () => MetaUi.Seven(Layer));
            SideBtn(468, colY + colStep * side++, "tab_sprint", "冲刺", SprintBadge(), () => MetaUi.Sprint(Layer));
            SideBtn(468, colY + colStep * side, "tab_codex", "图鉴", Meta.CodexBadge(S) > 0, () => MetaUi.Codex(Layer));

            // 背景台阶下沿在图高 34%：首领站在城门口，脚踩台阶下沿。
            var bd = Defs.Enemy(st.BossKind);
            float bsz = st.IsChapterBoss ? 350 : 312;
            float stair = 0.342f * Ui.Height;
            float feet = stair - Ui.SafeTop + 40f;

            // 标题压在门楼上，背后衬一层柔和云白光晕，与晴空青瓦清晰分离。
            float logoTop = 148f, logoBottom = feet - bsz * 0.86f;
            float logoH = Mathf.Clamp(logoBottom - logoTop, 118f, 166f), logoW = logoH * 900f / 268f;
            var logo = Top(0, logoTop + logoH * 0.5f, new Vector2(logoW, logoH), "logo");
            Ui.Box(logo, new Vector2(logoW * 1.1f, logoH * 1.35f), Vector2.zero, Color.white.A(0.32f), Art.SoftDot, "glow");
            Ui.Icon(logo, "logo", logoW, Vector2.zero, "art");
            if (anim) Tween.Pop(logo, 0.35f);

            var boss = Top(0, feet - bsz * 0.5f, new Vector2(bsz, bsz), "boss");
            Ui.Box(boss, new Vector2(bsz * 0.78f, 64), new Vector2(0, -bsz * 0.47f), new Color(0.04f, 0.1f, 0.18f, 0.42f), Art.SoftDot);
            Ui.Box(boss, new Vector2(bsz * 0.92f, 92), new Vector2(0, -bsz * 0.46f), (st.IsChapterBoss ? Pal.Purple : Pal.Hex("38A8D8")).A(0.36f), Art.SoftDot);
            var icon = Ui.Icon(boss, bd.Art, bsz, new Vector2(0, 0));
            Tween.Run(icon, 999f, k => icon.rectTransform.anchoredPosition = new Vector2(0, 5 * Mathf.Sin(k * 999f * 2f)));
            if (anim) Tween.Pop(boss, 0.35f);

            // 关卡绶带：霁蓝金边丝带 + 左右天青切关圆钮；下方深靛胶囊展示波数、首领与词条。
            float tagY = feet + 44f;
            var tag = Top(0, tagY, new Vector2(540, 100), "levelTag");
            var ribbon = Ui.Skin(tag, "h_ribbon", new Vector2(540, 100), Vector2.zero, -1f, null, "ribbon");
            Ui.Outlined(tag, $"第{st.No}关 · {st.Name}", 39, Paper, new Vector2(0, 5), new Vector2(390, 60), AzureDeep, 2.5f);
            Ui.Tap(ribbon, () => MetaUi.LevelList(Layer), tag);
            if (S.Selected > 1) ArrowBtn(tag, -304, -1);
            if (S.Selected <= S.Cleared && S.Selected < Defs.LevelCount) ArrowBtn(tag, 304, 1);
            string elite = st.EliteWaves.Length > 0 ? " · 精英 " + Defs.Enemy(st.EliteKinds[0]).Name : "";
            string buff = st.BuffRole.HasValue ? $" · <color=#78F0A8>{st.BuffText}</color>" : "";
            string plain = $"{st.Waves} 波 · 首领 {bd.Name}{elite}";
            int infoLen = plain.Length + (st.BuffRole.HasValue ? st.BuffText.Length + 3 : 0);
            string info = plain + buff;
            var infoBox = Top(0, tagY + 76f, new Vector2(560, 44), "levelInfo");
            Ui.Skin(infoBox, "h_capsule", new Vector2(Mathf.Clamp(infoLen * 22f + 80f, 300f, 650f), 44), Vector2.zero, -1f);
            Ui.Label(infoBox, info, 23, Pal.Hex("DCEAF7"), new Vector2(0, 1), new Vector2(630, 40), TextAnchor.MiddleCenter, Fonts.Heavy);

            // 出战好汉：站在青瓷白玉石台上；上阵好汉不再挂刺眼红点，改用柔和翠绿小升级标（好汉页签已有汇总提醒）
            float baseY = TabH;
            float wallY = baseY + 505 + sp * 0.27f;
            var podium = Bottom(0, wallY, new Vector2(1200, 277), "podium");
            var lu = Meta.Lineup(S);
            var arts = new Image[lu.Length];
            for (int i = 0; i < lu.Length; i++)
            {
                var d = Defs.Hero(lu[i]);
                var g = Ui.Rect(podium, "hero" + i);
                g.anchoredPosition = new Vector2((i - 1.5f) * 134, 96);
                g.sizeDelta = new Vector2(130, 200);
                Ui.Box(g, new Vector2(146, 56), new Vector2(0, -72), new Color(0.04f, 0.1f, 0.18f, 0.28f), Art.SoftDot, "aura");
                Ui.Icon(g, "ui/h_pedestal", 126, new Vector2(0, -76), "pedestal");
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
                if (Meta.CanLevel(S, lu[i]))
                {
                    var up = Ui.Icon(g, "ic_fast", 30, new Vector2(44, -52), "upHint");
                    up.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    up.color = Pal.Hex("68F098");
                }
                if (anim) Tween.Pop(g, 0.3f, 0.1f + i * 0.05f);
            }
            var pw = Ui.Rect(podium, "power");
            pw.anchoredPosition = new Vector2(0, -46);
            pw.sizeDelta = new Vector2(226, 52);
            Ui.Skin(pw, "h_capsule", new Vector2(226, 52), Vector2.zero, -1f);
            Ui.Icon(pw, "ic_sword", 42, new Vector2(-70, 1));
            Ui.Outlined(pw, Ui.Num(Meta.TeamPower(S)), 33, Gold, new Vector2(16, 1), new Vector2(156, 48), Ink, 2f);

            // 关卡三宝箱：承托在半透明深靛玉盘底座上，层次分明
            float chestY = baseY + 334 + sp * 0.18f;
            var row = Bottom(0, chestY, new Vector2(440, 136), "chests");
            Ui.Box(row, new Vector2(436, 118), new Vector2(0, -6), DockEdge, Art.Rounded, "trayRim");
            Ui.Box(row, new Vector2(430, 112), new Vector2(0, -6), Glass, Art.Rounded, "tray");
            int left = Meta.LevelChestLeft(S, st.No), readyN = 0;
            for (int k = 0; k < 3; k++) if (Meta.CanOpenLevelChest(S, st.No, k)) readyN++;
            string chestLine = readyN > 0 ? $"可开启 <color=#FFE066>{readyN}</color> 个关卡宝箱"
                : left > 0 ? $"本关还可夺 <color=#7CE8FF>{left}</color> 个宝箱" : "本关宝箱已全部收入囊中";
            Ui.Outlined(row, chestLine, 22, Pal.Hex("E6F2FC"), new Vector2(0, 68), new Vector2(440, 32), Ink, 1.8f);
            for (int k = 0; k < 3; k++)
            {
                bool got = Meta.LevelChestGot(S, st.No, k);
                bool ready = Meta.CanOpenLevelChest(S, st.No, k);
                float x = (k - 1) * 132;
                Ui.Box(row, new Vector2(106, 32), new Vector2(x, -38), new Color(0, 0, 0, 0.28f), Art.SoftDot, "plate" + k);
                var c = Ui.Icon(row, got ? "ic_chest_open" : MetaUi.ChestArt(k), 92, new Vector2(x, 0), "chest" + k);
                if (!got && !ready) c.color = new Color(0.86f, 0.92f, 0.98f, 0.62f);
                if (got) MetaUi.Check(row, new Vector2(x + 32, -26), 30);
                if (ready)
                {
                    MetaUi.Dot(c.transform, new Vector2(34, 34));
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

            // 左：章节星级宝箱（深靛青瓷方牌）；右：扫荡
            int stars = Meta.ChapterStars(S, st.Chapter);
            float sideY = wallY + 12;
            var cs = Bottom(-410, sideY, new Vector2(164, 118), "chapterStars");
            var csBg = Ui.Skin(cs, "h_tile", new Vector2(156, 114), Vector2.zero, 1f);
            Ui.Icon(cs, "ic_chest_gold", 68, new Vector2(-32, 10));
            Ui.Icon(cs, "ic_star", 34, new Vector2(36, 20));
            Ui.Outlined(cs, $"{stars}/30", 23, Paper, new Vector2(28, -26), new Vector2(106, 30), Ink, 1.5f);
            if (ChestBadge(st.Chapter)) MetaUi.Dot(cs, new Vector2(66, 44));
            int chap = st.Chapter;
            Ui.Tap(csBg, () => MetaUi.ChapterChests(Layer, chap), cs);
            if (Meta.CanSweep(S, st.No))
            {
                var sw = Bottom(410, sideY, new Vector2(144, 144), "sweep");
                var swBg = Ui.Icon(sw, "ui/h_round", 108, new Vector2(0, 14), "bg");
                Ui.Icon(sw, "ic_sweep", 74, new Vector2(0, 16));
                Ui.Outlined(sw, "扫荡", 26, Paper, new Vector2(0, -42), new Vector2(140, 34), Ink, 1.8f);
                Ui.Outlined(sw, $"体力 -{Meta.BattleStamina}", 19, Pal.Hex("B8DCFA"), new Vector2(0, -68), new Vector2(160, 26), Ink, 1.4f);
                Ui.Tap(swBg, () => Sweep(st), sw);
            }

            // 开始挑战：全场唯一高饱和鎏金琥珀主行动按钮（10% 焦点色），从青绿冷色场景中强力跃出
            float startY = baseY + 146 + sp * 0.1f;
            const float bw = 496f, bh = 148f;
            var start = Ui.Rect(_page, "start");
            start.anchorMin = start.anchorMax = new Vector2(0.5f, 0);
            start.sizeDelta = new Vector2(bw, bh);
            start.anchoredPosition = new Vector2(0, startY);
            Ui.Box(start, new Vector2(bw - 6, bh - 6), new Vector2(0, -9), Pal.Hex("664610"), Art.Rounded, "lipRim");
            Ui.Box(start, new Vector2(bw - 12, bh - 12), new Vector2(0, -9), Pal.Hex("C48C1C"), Art.Rounded, "lip");
            var body = Ui.Rect(start, "body");
            body.sizeDelta = new Vector2(bw, bh);
            var face = Ui.Skin(body, "h_start", new Vector2(bw, bh), Vector2.zero, 1f, null, "face");
            face.raycastTarget = false;
            var stTitle = Ui.Label(body, "开始挑战", 54, Pal.Hex("56340C"), new Vector2(0, 21), new Vector2(bw - 120, 68), TextAnchor.MiddleCenter, Fonts.Heavy);
            stTitle.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 0.97f, 0.86f, 0.58f);
            Ui.Box(body, new Vector2(138, 38), new Vector2(0, -35), new Color(0.32f, 0.20f, 0.04f, 0.62f), Art.Pill, "cost");
            Ui.Icon(body, "ic_wine", 36, new Vector2(-24, -34));
            Ui.Outlined(body, Meta.BattleStamina.ToString(), 27, Color.white, new Vector2(16, -34), new Vector2(60, 38), GoldStroke, 1.6f);
            var hit = Ui.Box(start, new Vector2(bw, bh), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            Ui.Tap(hit, () => Game.I.StartLevel(S.Selected), body, true, 8f);
            var glow = Ui.Box(start, new Vector2(bw + 84, bh + 54), Vector2.zero, Pal.Gold.A(0.28f), Art.SoftDot, "glow");
            glow.transform.SetAsFirstSibling();
            Tween.Run(start, 9999f, k =>
            {
                float t = k * 9999f;
                start.localScale = Vector3.one * (1f + 0.018f * Mathf.Sin(t * 3.6f));
                glow.color = Pal.Gold.A(0.18f + 0.10f * Mathf.Sin(t * 3.6f));
            });

            // 两侧副玩法（挑战 / 生辰纲）：不挂红点，保持底部主视觉焦点纯粹集中在「开始挑战」
            bool dOpen = S.Cleared >= MetaUi.DailyUnlock, eOpen = S.Cleared >= MetaUi.EscortUnlock;
            float modeY = Ui.Height - Ui.SafeTop - startY - 6f;
            SideBtn(-410, modeY, "ic_medal", "挑战", false, () =>
            {
                if (!dOpen) Ui.Toast($"通关第 {MetaUi.DailyUnlock} 关后开放", 200);
                else MetaUi.Daily(Layer);
            }, !dOpen);
            SideBtn(410, modeY, "ic_cart", "生辰纲", false, () =>
            {
                if (!eOpen) Ui.Toast($"通关第 {MetaUi.EscortUnlock} 关后开放", 200);
                else MetaUi.Escort(Layer);
            }, !eOpen);

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

        /// <summary>关卡绶带两侧的朱红圆箭头。原图朝左，向右的翻过来。</summary>
        void ArrowBtn(RectTransform tag, float x, int dir)
        {
            var b = Ui.Icon(tag, "ui/h_arrow", 80, new Vector2(x, 4), "arrow");
            if (dir > 0) b.rectTransform.localScale = new Vector3(-1, 1, 1);
            Ui.Tap(b, () =>
            {
                S.Selected = Mathf.Clamp(S.Selected + dir, 1, Mathf.Min(Defs.LevelCount, S.Cleared + 1));
                Profile.Save();
            });
        }

        /// <summary>左右功能侧坞：半透明深靛毛玻璃竖舱，把零散入口收纳成清爽模块。</summary>
        void SideDock(float x, float firstY, float step, int count)
        {
            if (count <= 0) return;
            float h = (count - 1) * step + 144f;
            float cy = firstY + (count - 1) * step * 0.5f - 2f;
            var d = Top(x, cy, new Vector2(118, h), x < 0 ? "dockL" : "dockR");
            Ui.Box(d, new Vector2(120, h + 4), Vector2.zero, DockEdge, Art.Rounded, "rim");
            Ui.Box(d, new Vector2(114, h - 2), Vector2.zero, Glass, Art.Rounded, "fill");
        }

        /// <summary>入口圆章：深靛银边圆底托住图标，冷瓷白字清楚利落。</summary>
        void SideBtn(float x, float y, string icon, string name, bool dot, Action act, bool locked = false)
        {
            var b = Top(x, y, new Vector2(112, 128), "side_" + name);
            var hit = Ui.Box(b, new Vector2(112, 128), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            Ui.Icon(b, "ui/h_round", 92, new Vector2(0, 12), "bg");
            var ic = Ui.Icon(b, icon, 66, new Vector2(0, 14));
            if (locked)
            {
                ic.color = new Color(0.52f, 0.58f, 0.65f);
                Ui.Icon(b, "ic_lock", 34, new Vector2(26, -6));
            }
            Ui.Outlined(b, name, 24, Paper, new Vector2(0, -44), new Vector2(130, 32), Ink, 1.9f);
            if (dot) MetaUi.Dot(b, new Vector2(34, 48)).rectTransform.localScale = Vector3.one * 0.72f;
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
            Cover("hero_stage_bg", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0.24f, 0f);
            float sp = Ui.Spare;

            // 上半：出战四好汉稳踏梁山山巅青石点将台（脚底落定于石坪贴地暗影，绝不踩屋顶悬空）
            var line = Meta.Lineup(S);
            float luY = 434 + sp * 0.15f;
            var lu = Top(0, luY, new Vector2(1000, 300), "lineup");
            lu.localScale = Vector3.one * (1f + Mathf.Min(0.1f, sp / 2500f));
            for (int i = 0; i < 4; i++)
            {
                var h = line[i];
                var d = Defs.Hero(h);
                var g = Ui.Rect(lu, "lu" + i);
                g.anchoredPosition = new Vector2((i - 1.5f) * 246, 0);
                g.sizeDelta = new Vector2(240, 300);
                // 直接踩在点将台石砖面上，仅保留柔和暖褐贴地接触阴影，彻底消除悬浮贴片感
                Ui.Box(g, new Vector2(162, 34), new Vector2(0, -103), new Color(0.18f, 0.11f, 0.05f, 0.46f), Art.GroundShadow, "shadow");
                var art = Ui.Icon(g, d.Art, 242, Vector2.zero, "art");
                art.rectTransform.pivot = new Vector2(0.5f, 0f);
                art.rectTransform.anchoredPosition = new Vector2(0, -104);
                float ph = i * 0.7f;
                Tween.Run(art, 999f, k => art.rectTransform.localScale = new Vector3(1f, 1f + 0.012f * Mathf.Sin(k * 999f * 2.2f + ph), 1f));
                var plate = Ui.Skin(g, "h_capsule", new Vector2(206, 48), new Vector2(0, -132), -1f);
                Ui.Outlined(plate.transform, $"Lv.{S.HeroLv[(int)h]} {d.Name}", 24, Paper, new Vector2(0, 1), new Vector2(200, 44), Ink, 1.5f);
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
                var face = Ui.Box(dot, Vector2.one * (on ? 70 : 58), Vector2.zero, on ? Pal.Azure : Pal.Hex("1C3044").A(0.85f), Art.Circle, "face");
                Ui.Box(dot, Vector2.one * (on ? 58 : 48), Vector2.zero, on ? Pal.Hex("54B8EE") : Pal.Hex("29425C"), Art.Circle, "inner");
                Ui.Outlined(dot, (i + 1).ToString(), on ? 34 : 28, on ? Color.white : Pal.Hex("B8CCE0"), new Vector2(0, 2), new Vector2(80, 60), Ink, 1.4f);
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

            var pw = Top(0, pgY + 86, new Vector2(420, 74), "power");
            Ui.Skin(pw, "h_capsule", new Vector2(360, 66), Vector2.zero, -1f);
            Ui.Icon(pw, "ic_sword", 62, new Vector2(-116, 2));
            Ui.Outlined(pw, Ui.Num(Meta.TeamPower(S)), 38, Gold, new Vector2(22, 2), new Vector2(230, 56), Ink, 2f);

            var cd = Top(0, pgY + 158, new Vector2(820, 62), "critBand");
            Ui.Skin(cd, "h_capsule", new Vector2(740, 58), Vector2.zero, -1f);
            Ui.Outlined(cd, $"全军暴击伤害：<color=#FFE066>{Meta.BuildLoadout(S).CritDmg * 100:0.#}%</color>", 30, Paper, new Vector2(-20, 1), new Vector2(660, 54), Ink, 1.8f);
            var tip = Ui.Box(cd, Vector2.one * 42, new Vector2(322, 0), Pal.Azure, Art.Circle, "tip");
            Ui.Outlined(tip.transform, "?", 28, Color.white, new Vector2(0, 1), new Vector2(42, 42), AzureDeep, 1.2f);
            Ui.Tap(tip, () => Ui.Toast("任意好汉每升 1 级，全军暴击伤害 +1%", 200));

            // 下半：深靛青面板里的好汉卡
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
                var col = rf < 0 ? Pal.Azure : MetaUi.RoleColor((Role)rf);
                var pos = new Vector2((i - (roles.Count - 1) * 0.5f) * 144, -54);
                var tb = Ui.Skin(panel, sel ? "btn_teal" : "h_capsule", new Vector2(136, 60), pos, -1f);
                if (rf >= 0) Ui.Icon(tb.transform, MetaUi.RoleArt((Role)rf), 36, new Vector2(-40, 2));
                Ui.Outlined(tb.transform, rf < 0 ? $"全部 {owned}" : Defs.RoleName((Role)rf), rf < 0 ? 23 : 25, sel ? Color.white : Pal.Hex("C6DAE8"), new Vector2(rf < 0 ? 0 : 16, 1), new Vector2(114, 56), Ink, 1.4f);
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
            var artMask = Ui.Rect(c, "artMask");
            artMask.sizeDelta = new Vector2(216, 184);
            artMask.anchoredPosition = new Vector2(0, 50);
            artMask.gameObject.AddComponent<RectMask2D>();
            var art = Ui.Icon(artMask, d.Art, 206, new Vector2(0, -4));
            if (!owned) art.color = new Color(0.14f, 0.16f, 0.20f, 0.88f);

            Ui.Box(c, Vector2.one * 58, new Vector2(-84, 120), Color.white, Art.Circle, "roleRing");
            Ui.Box(c, Vector2.one * 50, new Vector2(-84, 120), owned ? MetaUi.RoleColor(d.Kind) : Pal.Slate, Art.Circle, "roleBg");
            Ui.Icon(c, MetaUi.RoleArt(d.Kind), 38, new Vector2(-84, 121));
            Ui.Outlined(c, d.Name, 30, owned ? Color.white : Pal.Hex("B8C6D6"), new Vector2(0, -128), new Vector2(230, 42), Pal.Ink, 2f);

            if (owned)
            {
                if (Array.IndexOf(S.Lineup, (int)h) >= 0)
                {
                    var on = Ui.Icon(c, "ui/tag", 118, new Vector2(-54, -30), "onTag");
                    Ui.Outlined(on.transform, "出战中", 24, Color.white, new Vector2(-6, 2), new Vector2(110, 50), Pal.GreenDeep, 1.5f);
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
                    var up = Ui.Box(c, new Vector2(40, 40), new Vector2(98, -70), Color.white, Art.UpArrow, "up");
                    Tween.Run(up, 999f, k => up.rectTransform.anchoredPosition = new Vector2(98, -70 + 5 * Mathf.Sin(k * 999f * 6f)));
                }
            }
            else
            {
                Ui.Icon(c, "ic_lock", 48, new Vector2(84, 120));
                int at = Meta.UnlockLevelOf(h);
                string src = at > 0 ? $"第{at}关入伙" : d.Rarity >= 4 && !Meta.OrangeOpened(S) ? $"第{Meta.OrangeOpen}关后可招" : "招募获得";
                Ui.Label(c, src, 22, Pal.Hex("425264"), new Vector2(0, -75), new Vector2(210, 34), TextAnchor.MiddleCenter, Fonts.Heavy);
            }
            Ui.Tap(face, () => MetaUi.HeroDetail(Layer, h), c);
            return c;
        }

        // ---------------- 寨主（统帅 + 2x3装备 + 图腾/装备/材料三页签抽屉，对标参考图2/3/4） ----------------

        public void SetForgeSubTab(int sub, int selectTotemId = 0)
        {
            _forgeSubTab = Mathf.Clamp(sub, 0, 2);
            if (selectTotemId > 0) _selectedTotemId = selectTotemId;
            if (_tab != TabForge) SetTab(TabForge);
            else Rebuild(false);
        }

        public void SetShopSubTab(int sub)
        {
            _shopSubTab = Mathf.Clamp(sub, 0, 1);
            if (_tab != TabShop) SetTab(TabShop);
            else Rebuild(false);
        }

        void BuildForge(bool anim)
        {
            Cover("forge_bg", new Vector2(1080, 1920), Color.white, 0.25f);
            var fbg = _page.Find("body/bg") as RectTransform ?? _page.Find("bg") as RectTransform;
            if (fbg != null)
            {
                fbg.sizeDelta *= 1.16f;
                fbg.anchoredPosition += new Vector2(0, 138f);
            }
            Shade(0.24f, 0f);
            float sp = Ui.Spare;
            Action re = () => Rebuild(false);

            // 1. 左侧竖排 5 枚养成系统圆章（武器天赋 / 披风战氅 / 玄女天书 / 杏黄战旗 / 图鉴藏品）
            float leftStart = 212f + sp * 0.04f;
            float leftStep = 122f + sp * 0.02f;
            LordLeftBadge(-462, leftStart + leftStep * 0, "eq_weapon", "武器", Meta.CanTalent(S), false, () => MetaUi.TalentTree(Layer, re));
            LordLeftBadge(-462, leftStart + leftStep * 1, "eq_cloak", "披风", Meta.CanCloakUp(S), false, () => MetaUi.Cloak(Layer, re));
            LordLeftBadge(-462, leftStart + leftStep * 2, "ic_tome", "兵书", Meta.CanPage(S) || Meta.CanTome(S), false, () => MetaUi.Tome(Layer, re));
            LordLeftBadge(-462, leftStart + leftStep * 3, "ic_flag", "战旗", false, Meta.CanFlagUp(S), () => MetaUi.Flag(Layer, re));
            LordLeftBadge(-462, leftStart + leftStep * 4, "tab_codex", "藏品", false, Meta.CodexBadge(S) > 0, () => MetaUi.Codex(Layer));

            // 2. 中央统帅立绘：与 Loading 页形象完全一致的全身宋江，实打实踩在演武石坪上（去掉悬浮蓝光圆盘）
            float stageY = 536f + sp * 0.16f;
            var sj = Top(-126, stageY, new Vector2(460, 520), "songjiang");
            Ui.Box(sj, new Vector2(216, 40), new Vector2(0, -211), new Color(0.16f, 0.10f, 0.05f, 0.48f), Art.GroundShadow, "shadow");
            var art = Ui.Icon(sj, "lord_songjiang", 464, Vector2.zero, "art");
            art.rectTransform.pivot = new Vector2(0.5f, 0f);
            art.rectTransform.anchoredPosition = new Vector2(0, -212);
            Tween.Run(art, 999f, k => art.rectTransform.localScale = new Vector3(1f, 1f + 0.012f * Mathf.Sin(k * 999f * 2.0f), 1f));
            Ui.Tap(art, () => MetaUi.LordStats(Layer), sj);
            if (anim) Tween.Pop(sj, 0.32f);

            // 战力条 + 详情感叹号
            var pw = Top(-126, stageY + 232f, new Vector2(304, 58), "lordPower");
            Ui.Box(pw, new Vector2(272, 52), Vector2.zero, Pal.Hex("0D1926").A(0.92f), Art.Pill);
            Ui.Box(pw, new Vector2(268, 48), Vector2.zero, Pal.Hex("5B82A6").A(0.85f), Art.Pill);
            Ui.Box(pw, new Vector2(263, 43), Vector2.zero, Pal.Hex("162A3E").A(0.94f), Art.Pill);
            Ui.Icon(pw, "ic_sword", 42, new Vector2(-94, 1));
            Ui.Outlined(pw, Ui.Num(Meta.TeamPower(S)), 36, Gold, new Vector2(-2, 1), new Vector2(156, 48), Pal.Hex("101C28"), 2.0f);
            var infoRim = Ui.Box(pw, new Vector2(44, 44), new Vector2(106, 0), Pal.Hex("0E1B28"), Art.Circle, "infoRim");
            Ui.Box(infoRim.transform, new Vector2(40, 40), Vector2.zero, Pal.Hex("7298BA"), Art.Circle);
            var infoBtn = Ui.Box(infoRim.transform, new Vector2(35, 35), Vector2.zero, Pal.Hex("233E58"), Art.Circle, "info");
            Ui.Outlined(infoRim.transform, "!", 27, Color.white, new Vector2(0, 1), new Vector2(42, 42), Pal.Hex("0E1B28"), 1.4f);
            Ui.Tap(infoBtn, () => MetaUi.LordStats(Layer), infoRim.transform);

            // 3. 右侧 2x3 清爽装备矩阵 + 左上图腾章 + 底部一键强化大按钮
            float eqTop = 214f + sp * 0.05f;
            var totemQuick = Top(40, eqTop + 26f, new Vector2(96, 112), "totemQuick");
            Ui.Box(totemQuick, new Vector2(88, 88), new Vector2(0, 10), new Color(0.05f, 0.11f, 0.18f, 0.36f), Art.Hex6);
            Ui.Box(totemQuick, new Vector2(86, 86), new Vector2(0, 12), Pal.Hex("112030"), Art.Hex6);
            Ui.Box(totemQuick, new Vector2(80, 80), new Vector2(0, 12), Pal.Hex("749EC0"), Art.Hex6);
            var tqHex = Ui.Box(totemQuick, new Vector2(72, 72), new Vector2(0, 12), Pal.Hex("1E364E"), Art.Hex6);
            Ui.Icon(totemQuick, "totem_wolf", 54, new Vector2(0, 12));
            Ui.Outlined(totemQuick, "图腾", 22, Paper, new Vector2(0, -39), new Vector2(96, 30), Ink, 1.5f);
            Ui.Tap(tqHex, () => SetForgeSubTab(0), totemQuick);

            // 部位顺序按参考图排列：玉佩、盘龙角 / 凤翅盔、龙泉剑 / 踏云靴、明光铠
            int[] displayOrder = { 4, 5, 1, 0, 3, 2 };
            for (int idx = 0; idx < 6; idx++)
            {
                int slot = displayOrder[idx];
                int col = idx % 2, row = idx / 2;
                float cx = 188f + col * 194f;
                float cy = eqTop + 68f + row * 168f;
                BuildEquipCard(cx, cy, slot, anim, idx);
            }

            // 右下琥珀金「强化」主按钮
            bool anyForge = false;
            int bestSlot = 0;
            for (int i = 0; i < Meta.Equips.Length; i++)
            {
                if (!Meta.CanForge(S, i)) continue;
                if (!anyForge || S.EquipLv[i] < S.EquipLv[bestSlot]) bestSlot = i;
                anyForge = true;
            }
            var enhBtn = Top(285, eqTop + 564f, new Vector2(372, 96), "quickForge");
            var enhCreated = Ui.Btn(enhBtn, "强化", new Vector2(372, 96), Vector2.zero, Pal.Gold, () =>
            {
                if (anyForge)
                {
                    float before = Meta.TeamPower(S);
                    Meta.Forge(S, bestSlot);
                    Profile.Save();
                    Sfx.Play(Snd.Forge, 0.9f);
                    Rebuild(false);
                    MetaFx.StatUp(Layer, $"{Meta.Equips[bestSlot].Name} Lv.{S.EquipLv[bestSlot]} · 战力", before, Meta.TeamPower(S));
                }
                else MetaUi.ForgeSlot(Layer, 0);
            }, 42, null, Pal.Hex("56340C"));
            if (anyForge) MetaUi.Dot(enhCreated, new Vector2(164, 38));

            // 4. 下半屏温润宋瓷米白圆角抽屉（修复顶锚点偏移，完整包裹切换栏与全部背包行）
            float drawerTop = 852f + sp * 0.24f;
            float drawerH = Mathf.Max(684f, Ui.Height - Ui.SafeTop - drawerTop - TabH + 18f);
            var drawer = Top(0, drawerTop, new Vector2(1080, drawerH), "drawer");
            drawer.pivot = new Vector2(0.5f, 1f);
            drawer.anchoredPosition = new Vector2(0, -drawerTop);

            static void TopStretch(RectTransform rt, float y, Vector2 sz)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = sz;
                rt.anchoredPosition = new Vector2(0, y);
            }

            var dShadow = Ui.Box(drawer, Vector2.one, Vector2.zero, new Color(0.05f, 0.09f, 0.15f, 0.20f), Art.Rounded, "dShadow");
            TopStretch(dShadow.rectTransform, 6f, new Vector2(1086, drawerH + 42f));
            var dOuter = Ui.Box(drawer, Vector2.one, Vector2.zero, Pal.Hex("8A7F6D"), Art.Rounded, "dOuter");
            TopStretch(dOuter.rectTransform, 0f, new Vector2(1086, drawerH + 36f));
            var dTrim = Ui.Box(drawer, Vector2.one, Vector2.zero, Pal.Hex("E4DDD0"), Art.Rounded, "dTrim");
            TopStretch(dTrim.rectTransform, -2.5f, new Vector2(1084, drawerH + 33f));
            var dFace = Ui.Box(drawer, Vector2.one, Vector2.zero, Pal.Hex("F3EFE6"), Art.Rounded, "dFace");
            TopStretch(dFace.rectTransform, -11f, new Vector2(1084, drawerH + 25f));
            dFace.raycastTarget = true;

            // 顶沿装饰双细线 + 中央菱花扣（对标参考图抽屉顶饰）
            var dLine = Ui.Box(drawer, new Vector2(980, 2.5f), new Vector2(0, -13f), Pal.Hex("C9BFA9"), Art.White, "dLine");
            dLine.rectTransform.anchorMin = dLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            var dDiaRim = Ui.Box(drawer, new Vector2(20, 20), new Vector2(0, -13f), Pal.Hex("B5A88E"), Art.RoundedSmall, "dDiaRim");
            dDiaRim.rectTransform.anchorMin = dDiaRim.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dDiaRim.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var dDia = Ui.Box(drawer, new Vector2(13, 13), new Vector2(0, -13f), Pal.Hex("EFECE2"), Art.RoundedSmall, "dDia");
            dDia.rectTransform.anchorMin = dDia.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dDia.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // 三段式胶囊切换栏：图腾 / 装备 / 材料
            var seg = Ui.Rect(drawer, "seg");
            seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 1f);
            seg.anchoredPosition = new Vector2(0, -64);
            seg.sizeDelta = new Vector2(924, 72);
            Ui.Box(seg, new Vector2(924, 72), Vector2.zero, Pal.Hex("6D675D"), Art.Pill);
            Ui.Box(seg, new Vector2(918, 66), Vector2.zero, Pal.Hex("857E73"), Art.Pill);
            string[] subNames = { "图腾", "装备", "材料" };
            bool[] subDots = { Meta.CanMergeAnyTotem(S), S.GearBoxes > 0, false };
            for (int i = 0; i < 3; i++)
            {
                bool on = i == _forgeSubTab;
                float sx = (i - 1) * 302f;
                if (on)
                {
                    var ptrRim = Ui.Box(seg, new Vector2(22, 22), new Vector2(sx, -28), Pal.Hex("9E7D2B"), Art.RoundedSmall);
                    ptrRim.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                    Ui.Box(seg, new Vector2(310, 66), new Vector2(sx, 0), Pal.Hex("9E7D2B"), Art.Pill);
                    var ptrFill = Ui.Box(seg, new Vector2(18, 18), new Vector2(sx, -26), Pal.Hex("F1D476"), Art.RoundedSmall);
                    ptrFill.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                }
                var pill = Ui.Box(seg, new Vector2(304, 60), new Vector2(sx, 0), on ? Pal.Hex("F1D476") : new Color(0, 0, 0, 0), Art.Pill, "sub" + i);
                Ui.Label(seg, subNames[i], 31, on ? Pal.Hex("56350E") : Pal.Hex("F7F4EC"), new Vector2(sx, 1), new Vector2(260, 56),
                    TextAnchor.MiddleCenter, on ? Fonts.Heavy : Fonts.Bold);
                if (subDots[i] && !on) MetaUi.Dot(seg, new Vector2(sx + 108, 22));
                int idx = i;
                Ui.Tap(pill, () =>
                {
                    if (_forgeSubTab == idx) return;
                    _forgeSubTab = idx;
                    Sfx.Play(Snd.Tab, 0.7f);
                    Rebuild(false);
                }, null, false);
            }

            if (_forgeSubTab == 0) BuildTotemDrawer(drawer, drawerH);
            else if (_forgeSubTab == 1) BuildEquipDrawer(drawer, drawerH);
            else BuildMaterialDrawer(drawer, drawerH);
        }

        void LordLeftBadge(float x, float y, string icon, string title, bool upArrow, bool redDot, Action onTap)
        {
            var b = Top(x, y, new Vector2(106, 114), "lord_" + title);
            var hit = Ui.Box(b, new Vector2(106, 114), Vector2.zero, new Color(0, 0, 0, 0), Art.White, "hit");
            Ui.Icon(b, "ui/h_round", 92, new Vector2(0, 10), "bg");
            Ui.Icon(b, icon, 56, new Vector2(0, 11));
            Ui.Outlined(b, title, 23, Paper, new Vector2(0, -37), new Vector2(112, 30), Ink, 1.6f);
            if (upArrow)
                Ui.Box(b, new Vector2(36, 36), new Vector2(31, 37), Color.white, Art.UpArrow, "up");
            else if (redDot)
                MetaUi.Dot(b, new Vector2(32, 38)).rectTransform.localScale = Vector3.one * 0.82f;
            Ui.Tap(hit, onTap, b);
        }

        void BuildEquipCard(float x, float y, int slot, bool anim, int orderIdx)
        {
            var e = Meta.Equips[slot];
            int lv = S.EquipLv[slot];
            int q = S.EquipQ[slot];
            const float cw = 182f, ch = 156f;
            var c = Top(x, y, new Vector2(cw, ch), "eq" + slot);

            // 双线清晰边框（深色外框 + 纯白内嵌框 + 清爽粉彩底 + 纯白镶孔底栏），即使压在白云上也轮廓分明
            Color qOuter = q >= 3 ? Pal.Hex("7A581C") : q == 2 ? Pal.Hex("544482") : q == 1 ? Pal.Hex("456D60") : Pal.Hex("4B6878");
            Color qBg = q >= 3 ? Pal.Hex("F6D580") : q == 2 ? Pal.Hex("BEB4EE") : q == 1 ? Pal.Hex("9AD6C2") : Pal.Hex("9BCBE4");

            Ui.Box(c, new Vector2(cw + 4, ch + 4), new Vector2(0, -4), new Color(0.04f, 0.09f, 0.15f, 0.24f), Art.RoundedSmall);
            var face = Ui.Box(c, new Vector2(cw, ch), Vector2.zero, qOuter, Art.RoundedSmall);
            face.raycastTarget = true;
            Ui.Box(c, new Vector2(cw - 5f, ch - 5f), Vector2.zero, Color.white, Art.RoundedSmall);
            Ui.Box(c, new Vector2(cw - 10f, ch - 40f), new Vector2(0, 15f), qBg, Art.RoundedSmall);
            Ui.Box(c, new Vector2(cw - 10f, 14f), new Vector2(0, -ch * 0.5f + 41f), qBg, Art.White);
            Ui.Box(c, new Vector2(cw - 10f, 1.5f), new Vector2(0, -ch * 0.5f + 34f), qOuter.A(0.28f), Art.White);

            Ui.Icon(c, e.Icon, 94, new Vector2(0, 14));

            // 左上橙色等级小签（细描边白字，彻底告别粗黑框糊字）
            Ui.Box(c, new Vector2(70, 27), new Vector2(-cw * 0.5f + 40, ch * 0.5f - 18.5f), Pal.Hex("9E5410"), Art.RoundedSmall);
            var lvTag = Ui.Box(c, new Vector2(66, 23), new Vector2(-cw * 0.5f + 40, ch * 0.5f - 18.5f), Pal.Hex("EE9430"), Art.RoundedSmall);
            Ui.Outlined(lvTag.transform, $"{lv}级", 19, Color.white, new Vector2(0, 0.5f), new Vector2(66, 24), Pal.Hex("7C3E06"), 1.2f);

            int brk = Meta.Breaks(lv);
            if (brk > 0 || lv > 0)
                Ui.Outlined(c, $"+{lv}", 21, Color.white, new Vector2(cw * 0.5f - 42, -ch * 0.5f + 49), new Vector2(68, 26), Pal.Hex("182634"), 1.4f, TextAnchor.MiddleRight);

            // 底部白瓷栏 4 枚带细框宝石镶孔
            int baseIdx = slot * Meta.TotemSocketsPerSlot;
            for (int k = 0; k < Meta.TotemSocketsPerSlot; k++)
            {
                bool open = Meta.TotemSocketOpen(S, k);
                int tid = open ? S.TotemEquipped[baseIdx + k] : 0;
                var td = tid > 0 ? Meta.FindTotem(S, tid) : null;
                Color rimCol = !open ? Pal.Hex("C2C8CE") : td != null ? MetaUi.TotemTileRim(td.Rarity) : Pal.Hex("7E90A0");
                Color coreCol = !open ? Pal.Hex("E4E7EB") : td != null ? MetaUi.TotemRarityColor(td.Rarity) : Pal.Hex("BCCAD6");
                Vector2 pp = new Vector2((k - 1.5f) * 28f, -ch * 0.5f + 19f);
                var pr = Ui.Box(c, new Vector2(20, 20), pp, rimCol, Art.Hex6);
                pr.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                var pc = Ui.Box(c, new Vector2(15, 15), pp, coreCol, Art.Hex6);
                pc.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            }

            if (Meta.CanForge(S, slot))
                Ui.Box(c, new Vector2(34, 34), new Vector2(cw * 0.5f - 19, ch * 0.5f - 17), Color.white, Art.UpArrow, "up");

            int si = slot;
            Ui.Tap(face, () => MetaUi.ForgeSlot(Layer, si), c);
            if (anim) Tween.Pop(c, 0.26f, 0.06f + orderIdx * 0.04f);
        }

        void BuildTotemDrawer(RectTransform drawer, float drawerH)
        {
            var list = new List<TotemData>();
            foreach (var t in S.Totems)
                if (_totemFilter < 0 || t.Slot == _totemFilter) list.Add(t);
            list.Sort((a, b) =>
            {
                bool ea = Meta.IsTotemEquipped(S, a.Id), eb = Meta.IsTotemEquipped(S, b.Id);
                if (ea != eb) return ea ? -1 : 1;
                if (b.Rarity != a.Rarity) return b.Rarity.CompareTo(a.Rarity);
                return a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Id.CompareTo(b.Id);
            });

            float viewH = drawerH - 220f;
            var view = Ui.Rect(drawer, "totemView");
            view.anchorMin = view.anchorMax = new Vector2(0.5f, 1f);
            view.pivot = new Vector2(0.5f, 1f);
            view.sizeDelta = new Vector2(1000, viewH);
            view.anchoredPosition = new Vector2(0, -112);
            view.gameObject.AddComponent<RectMask2D>();
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit");

            const float cell = 176f, step = 192f;
            int rows = Mathf.Max(1, (list.Count + 4) / 5);
            var content = Ui.Rect(view, "content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(1000, Mathf.Max(viewH, rows * step + 20f));
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;

            if (list.Count == 0)
            {
                Ui.Label(content, "暂无该部位兽魂图腾 · 点击右下【寻宝】获取", 28, Pal.Hex("7C7870"),
                    new Vector2(0, -viewH * 0.45f), new Vector2(820, 60), TextAnchor.MiddleCenter, Fonts.Heavy);
            }
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var tile = Ui.Rect(content, "totem_" + t.Id);
                tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 1f);
                tile.sizeDelta = new Vector2(cell, cell);
                tile.anchoredPosition = new Vector2(((i % 5) - 2) * step, -100 - (i / 5) * step);
                bool sel = t.Id == _selectedTotemId;
                if (sel)
                    Ui.Box(tile, new Vector2(cell + 8, cell + 8), Vector2.zero, Pal.Gold, Art.RoundedSmall);
                string slotSkin = t.Rarity >= 3 ? "slot_orange" : t.Rarity == 2 ? "slot_purple" : "slot_blue";
                var face = MetaUi.Slot(tile, slotSkin, cell - 4f, Vector2.zero);
                face.raycastTarget = true;

                Ui.Icon(tile, Meta.TotemIcon(t), 124, new Vector2(0, 6));
                // 左下部位小标
                var partBox = Ui.Box(tile, new Vector2(44, 44), new Vector2(-cell * 0.5f + 31, -cell * 0.5f + 31), new Color(0.1f, 0.16f, 0.24f, 0.80f), Art.RoundedSmall);
                Ui.Icon(partBox.transform, Meta.Equips[t.Slot].Icon, 34, Vector2.zero);
                if (Meta.IsTotemEquipped(S, t.Id))
                {
                    var eqTag = Ui.Box(tile, new Vector2(66, 28), new Vector2(cell * 0.5f - 42, cell * 0.5f - 22), Pal.Teal, Art.Pill);
                    Ui.Outlined(eqTag.transform, "已镶", 18, Color.white, Vector2.zero, new Vector2(66, 28), Ink, 1.2f);
                }
                if (t.Locked)
                    Ui.Icon(tile, "ic_lock", 32, new Vector2(-cell * 0.5f + 26, cell * 0.5f - 26));

                int tid = t.Id;
                Ui.Tap(face, () =>
                {
                    _selectedTotemId = tid;
                    MetaUi.TotemDetail(Layer, tid, () => Rebuild(false));
                }, tile);
            }

            // 抽屉底栏：筛选 + 寻宝 + 三合一合成（对标参考图4右下角操作区）
            float barY = -drawerH + 58f;
            string filterText = _totemFilter < 0 ? "部位：全部" : $"部位：{Meta.Equips[_totemFilter].Name}";
            var filterBtn = Ui.Btn(drawer, filterText, new Vector2(236, 76), new Vector2(-346, barY), Pal.Slate, () =>
            {
                _totemFilter = _totemFilter >= Meta.Equips.Length - 1 ? -1 : _totemFilter + 1;
                Rebuild(false);
            }, 28);
            filterBtn.anchorMin = filterBtn.anchorMax = new Vector2(0.5f, 1f);

            var huntBtn = Ui.Btn(drawer, $"寻宝 ({S.Hammers})", new Vector2(236, 76), new Vector2(40, barY), Pal.Gold, () => SetShopSubTab(1), 28, "ic_hammer");
            huntBtn.anchorMin = huntBtn.anchorMax = new Vector2(0.5f, 1f);

            bool canMerge = Meta.CanMergeAnyTotem(S);
            var mergeBtn = Ui.Btn(drawer, "合成", new Vector2(236, 76), new Vector2(346, barY), canMerge ? Pal.Teal : Pal.Slate, () =>
            {
                var got = Meta.MergeAllTotems(S, new Rng(Environment.TickCount));
                if (got.Count == 0)
                {
                    Sfx.Play(Snd.Deny);
                    Ui.Toast("需要 3 个同品质且未镶嵌、未上锁的图腾才能合成", 200);
                    return;
                }
                Profile.Save();
                Sfx.Play(Snd.Merge);
                Rebuild(false);
                MetaUi.TotemPullResult(Layer, got, null);
            }, 32);
            mergeBtn.anchorMin = mergeBtn.anchorMax = new Vector2(0.5f, 1f);
            if (canMerge) MetaUi.Dot(mergeBtn, new Vector2(104, 30));
        }

        void BuildEquipDrawer(RectTransform drawer, float drawerH)
        {
            Action re = () => Rebuild(false);
            // 顶部：装备箱快捷开启横条（双行严格左对齐，单行不折行，彻底根除文字重叠）
            var boxRow = Ui.Rect(drawer, "gearBoxRow");
            boxRow.anchorMin = boxRow.anchorMax = new Vector2(0.5f, 1f);
            boxRow.anchoredPosition = new Vector2(0, -168);
            boxRow.sizeDelta = new Vector2(960, 118);
            Ui.Skin(boxRow, "row", new Vector2(960, 118), Vector2.zero, 1f, Pal.Hex("F6F2E8"));
            Ui.Icon(boxRow, "ic_gearbox", 86, new Vector2(-412, 1));
            var gbTitle = Ui.Label(boxRow, $"神兵装备箱 ×{S.GearBoxes}", 30, Pal.Ink, new Vector2(-48, 22), new Vector2(600, 38), TextAnchor.MiddleLeft, Fonts.Heavy);
            gbTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            var gbDesc = Ui.Label(boxRow, "开启必得精良~传说装备，低阶自动熔炼精铁", 20, Pal.SubInk, new Vector2(-48, -21), new Vector2(600, 32), TextAnchor.MiddleLeft);
            gbDesc.horizontalOverflow = HorizontalWrapMode.Overflow;
            var openBtn = Ui.Btn(boxRow, "开启", new Vector2(188, 76), new Vector2(360, 0), S.GearBoxes > 0 ? Pal.Gold : Pal.Slate, () => MetaUi.GearBox(Layer, re), 34);
            if (S.GearBoxes > 0) MetaUi.Dot(openBtn, new Vector2(80, 31));

            // 六部位属性与突破一览双列卡
            for (int i = 0; i < Meta.Equips.Length; i++)
            {
                var e = Meta.Equips[i];
                int lv = S.EquipLv[i], q = S.EquipQ[i];
                var card = Ui.Rect(drawer, "eqRow" + i);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
                card.sizeDelta = new Vector2(470, 128);
                card.anchoredPosition = new Vector2((i % 2 == 0 ? -1 : 1) * 244f, -306 - (i / 2) * 140f);
                Ui.Skin(card, "row", new Vector2(470, 128), Vector2.zero);
                var hit = Ui.Box(card, new Vector2(470, 128), Vector2.zero, new Color(0, 0, 0, 0), Art.White);
                MetaUi.Slot(card, MetaUi.QualitySlot(q), 94, new Vector2(-174, 0));
                Ui.Icon(card, e.Icon, 76, new Vector2(-174, 2));
                var l1 = Ui.Label(card, $"{Meta.QualityName[q]}·{e.Name} Lv.{lv}", 24, Pal.Ink, new Vector2(46, 34), new Vector2(320, 32), TextAnchor.MiddleLeft, Fonts.Heavy);
                var l2 = Ui.Label(card, Meta.EquipText(i, lv, q), 21, Pal.GreenDeep, new Vector2(46, 0), new Vector2(320, 30), TextAnchor.MiddleLeft, Fonts.Heavy);
                var l3 = Ui.Label(card, Meta.Breaks(lv) > 0 ? Meta.BreakText(i, lv) : $"满10级突破：{Defs.RoleName(e.Role)}+4%", 18, Pal.SubInk, new Vector2(46, -34), new Vector2(320, 28), TextAnchor.MiddleLeft);
                l1.horizontalOverflow = l2.horizontalOverflow = l3.horizontalOverflow = HorizontalWrapMode.Overflow;
                int si = i;
                Ui.Tap(hit, () => MetaUi.ForgeSlot(Layer, si), card);
            }
        }

        void BuildMaterialDrawer(RectTransform drawer, float drawerH)
        {
            var items = new List<Item>();
            Cur[] bagOrder = { Cur.Refine, Cur.Hammer, Cur.Meteor, Cur.Tome, Cur.Page, Cur.GearBox, Cur.Token, Cur.Silk, Cur.Iron, Cur.Ingot };
            foreach (var c in bagOrder)
            {
                int n = Meta.Have(S, c);
                if (n > 0) items.Add(new Item(c, n));
            }
            for (int h = 0; h < Defs.HeroCount; h++)
                if (S.Shards[h] > 0) items.Add(new Item(Cur.Shard, S.Shards[h], (HeroId)h));

            float viewH = drawerH - 138f;
            var view = Ui.Rect(drawer, "matView");
            view.anchorMin = view.anchorMax = new Vector2(0.5f, 1f);
            view.pivot = new Vector2(0.5f, 1f);
            view.sizeDelta = new Vector2(1000, viewH);
            view.anchoredPosition = new Vector2(0, -112);
            view.gameObject.AddComponent<RectMask2D>();
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit");

            const float cell = 176f, step = 192f;
            int rows = Mathf.Max(1, (items.Count + 4) / 5);
            var content = Ui.Rect(view, "content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(1000, Mathf.Max(viewH, rows * step + 24f));
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var tile = MetaUi.ItemTile(content, it, Vector2.zero, cell - 10f, false, Layer);
                tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 1f);
                tile.anchoredPosition = new Vector2(((i % 5) - 2) * step, -98 - (i / 5) * step);
            }
        }

        // ---------------- 山寨地图 ----------------

        static readonly (string Name, float X, float Y, string Icon)[] Buildings =
        {
            // 聚义厅贴在殿门口的石阶上：再往上会和寨名绶带叠住，再往下就看不出是这座殿。
            ("聚义厅", 0.50f, 0.24f, "tab_task"), ("铁匠铺", 0.20f, 0.28f, "tab_forge"), ("校场", 0.79f, 0.34f, "tab_hero"),
            ("酒馆", 0.19f, 0.48f, "tab_shop"), ("金库", 0.82f, 0.55f, "tab_idle"), ("水寨", 0.30f, 0.68f, "en_porter"), ("山门", 0.51f, 0.84f, "tab_battle"),
        };

        void BuildFort(bool anim)
        {
            var size = Cover("map_fort", new Vector2(1080, 1920), Color.white, 0.5f);
            Shade(0.3f, 0.2f);
            // 寨名贴在顶栏下面的空里，当页眉，不压在殿顶上。
            var hdr = Top(0, 164, new Vector2(520, 96), "hdr");
            Ui.Ribbon(hdr, "水泊梁山", Vector2.zero, 400);
            string[] tips = { "每日任务", "强化装备", "好汉升级", "擂鼓招募", "巡山收益", "截取生辰纲", "出征" };
            for (int i = 0; i < Buildings.Length; i++)
            {
                var b = Buildings[i];
                var pos = new Vector2((b.X - 0.5f) * size.x, (0.5f - b.Y) * size.y);
                var c = Ui.Rect(_page, "bld" + i);
                c.anchoredPosition = pos + new Vector2(0, -60);
                c.sizeDelta = new Vector2(240, 120);
                var plate = Ui.Skin(c, "h_capsule", new Vector2(246, 64), Vector2.zero, -1f);
                Ui.Icon(c, b.Icon, 72, new Vector2(-82, 4));
                Ui.Outlined(c, b.Name, 31, Paper, new Vector2(22, 1), new Vector2(160, 50), Ink, 1.8f);
                Ui.Outlined(c, tips[i], 21, Pal.Hex("DCEAF7"), new Vector2(0, -46), new Vector2(240, 30), Ink, 1.5f);
                bool dot = i == 0 ? Meta.TaskBadge(S) > 0 : i == 1 ? ForgeBadge() : i == 2 ? HeroBadge() : i == 3 ? ShopBadge() : i == 4 ? IdleBadge() : false;
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

        // ---------------- 商城（双池祈愿：好汉招募·心愿UP + 兽魂寻宝 + 梁山集市） ----------------

        void BuildShop(bool anim)
        {
            Cover("hall_bg", new Vector2(1080, 1920), Color.white, 0.3f);
            Shade(0.3f, 0f);
            float sp = Ui.Spare;

            // 顶部双卡池切换（好汉招募 / 兽魂寻宝）
            var poolTabs = Top(0, 196 + sp * 0.04f, new Vector2(680, 74), "poolTabs");
            Ui.Box(poolTabs, new Vector2(680, 72), Vector2.zero, new Color(0.06f, 0.13f, 0.22f, 0.82f), Art.Pill);
            string[] pNames = { "擂鼓招募", "兽魂寻宝" };
            bool[] pDots = { S.Tokens >= 1, S.Hammers >= 1 };
            for (int i = 0; i < 2; i++)
            {
                bool on = i == _shopSubTab;
                float px = (i - 0.5f) * 332f;
                var pb = Ui.Skin(poolTabs, on ? "btn_gold" : "h_capsule", new Vector2(328, 64), new Vector2(px, 0), -1f);
                if (on)
                {
                    var lbl = Ui.Label(pb.transform, pNames[i], 32, Pal.Hex("56340C"), new Vector2(0, 1), new Vector2(300, 56), TextAnchor.MiddleCenter, Fonts.Heavy);
                    lbl.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 0.97f, 0.86f, 0.55f);
                }
                else Ui.Outlined(pb.transform, pNames[i], 31, Paper, new Vector2(0, 1), new Vector2(300, 56), Ink, 1.8f);
                if (pDots[i] && !on) MetaUi.Dot(pb.transform, new Vector2(138, 24));
                int idx = i;
                Ui.Tap(pb, () => SetShopSubTab(idx), pb.transform, false);
            }

            var ban = Top(0, 536 + sp * 0.1f, new Vector2(1040, 520), "banner");
            Ui.Skin(ban, "panel", new Vector2(1036, 556), Vector2.zero);
            var banInner = Ui.Rect(ban, "banInner");
            banInner.sizeDelta = new Vector2(996, 516);
            banInner.gameObject.AddComponent<RectMask2D>();
            float by = 890 + sp * 0.18f;

            if (_shopSubTab == 0)
            {
                var img = Ui.Box(banInner, new Vector2(1000, 520), Vector2.zero, Color.white, Art.Img("recruit_banner"));
                img.type = Image.Type.Simple;
                Ui.Box(banInner, new Vector2(1000, 122), new Vector2(0, -198), new Color(0.06f, 0.13f, 0.22f, 0.84f), Art.White);
                if (Meta.OrangeOpened(S))
                {
                    Ui.Outlined(ban, $"传说 {Meta.OrangeRate(S) * 100:0}% · 史诗 {Meta.PurpleRate(S) * 100:0}% · 十连必出史诗", 26, Paper, new Vector2(-80, -172), new Vector2(720, 40), Ink, 1.5f);
                    int left = Meta.OrangePity - S.Pity;
                    Ui.Outlined(ban, $"再招募 {left} 次必得传说好汉", 28, Gold, new Vector2(-80, -216), new Vector2(720, 44), Ink, 1.8f);
                }
                else
                {
                    Ui.Outlined(ban, $"史诗 {Meta.PurpleRate(S) * 100:0}% · 十连必出史诗 · 心愿 50% UP", 26, Paper, new Vector2(-80, -172), new Vector2(720, 40), Ink, 1.5f);
                    Ui.Outlined(ban, $"传说好汉：通关第 {Meta.OrangeOpen} 关后开放", 28, Gold, new Vector2(-80, -216), new Vector2(720, 44), Ink, 1.8f);
                }

                // 左上招募等级 + 右上招募令 + 右下心愿好汉祈愿槽
                var lv = Ui.Rect(ban, "lv");
                lv.anchoredPosition = new Vector2(-250, 168);
                Ui.Skin(lv, "h_capsule", new Vector2(380, 74), Vector2.zero, -1f);
                Ui.Outlined(lv, $"招募等级 {S.RecruitLv}", 27, Gold, new Vector2(-70, 12), new Vector2(220, 36), Ink, 1.5f);
                MetaUi.Bar(lv, new Vector2(0, -16), new Vector2(330, 18), S.RecruitLv >= Meta.RecruitMaxLv ? 1 : S.RecruitExp / (float)Meta.RecruitNeed(S.RecruitLv), Pal.Gold);
                MetaUi.Pill(ban, "ic_token", S.Tokens.ToString(), new Vector2(336, 174), 200);

                var wish = Meta.GetWish(S);
                var wBox = Ui.Rect(ban, "wishSlot");
                wBox.anchoredPosition = new Vector2(346, -152);
                wBox.sizeDelta = new Vector2(168, 178);
                var wBg = Ui.Box(wBox, new Vector2(154, 154), new Vector2(0, 10), Pal.Gold, Art.Rounded);
                wBg.raycastTarget = true;
                Ui.Box(wBox, new Vector2(144, 144), new Vector2(0, 10), Pal.Hex("1B3148"), Art.Rounded);
                if (wish.HasValue)
                {
                    var wd = Defs.Hero(wish.Value);
                    Ui.Icon(wBox, wd.Art, 128, new Vector2(0, 16));
                    Ui.Outlined(wBox, $"UP·{wd.Name}", 22, Gold, new Vector2(0, -68), new Vector2(168, 30), Ink, 1.8f);
                }
                else
                {
                    Ui.Label(wBox, "+", 68, Gold, new Vector2(0, 16), new Vector2(120, 90), TextAnchor.MiddleCenter, Fonts.Heavy);
                    Ui.Outlined(wBox, "心愿单", 23, Gold, new Vector2(0, -68), new Vector2(168, 30), Ink, 1.8f);
                }
                Ui.Tap(wBg, () => MetaUi.WishPicker(Layer, () => Rebuild(false)), wBox);

                int adLeft = Meta.AdRecruitMax - S.AdRecruit;
                var b1 = Ui.Btn(_page, "招募", new Vector2(300, 114), Vector2.zero, Pal.Azure, () => DoRecruit(1, false), 42, "ic_token");
                Place(b1, -340, by);
                Ui.Outlined(b1, "招募令×1", 22, Paper, new Vector2(0, -76), new Vector2(240, 30), Ink, 1.5f);
                var b10 = Ui.Btn(_page, "十连", new Vector2(300, 114), Vector2.zero, Pal.Gold, () => DoRecruit(10, false), 42, "ic_token");
                Place(b10, 0, by);
                Ui.Outlined(b10, "招募令×10", 22, Paper, new Vector2(0, -76), new Vector2(240, 30), Ink, 1.5f);
                var ba = Ui.Btn(_page, adLeft > 0 ? "免费" : "明日", new Vector2(300, 114), Vector2.zero, adLeft > 0 ? Pal.Green : Pal.Slate, () => DoRecruit(1, true), 42, "ic_ad");
                Place(ba, 340, by);
                Ui.Outlined(ba, $"今日剩余 {adLeft} 次", 22, Paper, new Vector2(0, -76), new Vector2(240, 30), Ink, 1.5f);
                if (S.Tokens >= 1 && S.Tokens < 10) MetaUi.Dot(b1, new Vector2(138, 50));
            }
            else
            {
                // 兽魂寻宝祭坛横幅
                Ui.Box(banInner, new Vector2(996, 516), Vector2.zero, Pal.Hex("162A40"), Art.Rounded);
                Ui.Box(banInner, new Vector2(680, 340), new Vector2(0, 40), Pal.Hex("4AD0FF").A(0.24f), Art.SoftDot);
                string[] showBeasts = { "totem_tiger", "totem_qilin", "totem_bear" };
                for (int k = 0; k < 3; k++)
                {
                    float bx = (k - 1) * 236f;
                    float sz = k == 1 ? 216f : 168f;
                    Ui.Box(banInner, Vector2.one * (sz * 1.15f), new Vector2(bx, 48), (k == 1 ? Pal.Gold : Pal.Purple).A(0.32f), Art.SoftDot);
                    var bi = Ui.Icon(banInner, showBeasts[k], sz, new Vector2(bx, 48 + (k == 1 ? 14 : -6)));
                    float ph = k * 1.2f;
                    float baseY2 = 48 + (k == 1 ? 14 : -6);
                    Tween.Run(bi, 999f, t => bi.rectTransform.anchoredPosition = new Vector2(bx, baseY2 + 5f * Mathf.Sin(t * 999f * 2.2f + ph)));
                }
                Ui.Box(banInner, new Vector2(1000, 122), new Vector2(0, -198), new Color(0.05f, 0.11f, 0.19f, 0.88f), Art.White);
                Ui.Outlined(ban, "敲开星宿灵石 · 获取稀有~绝世兽魂图腾 · 十连必出史诗+", 26, Paper, new Vector2(0, -172), new Vector2(880, 40), Ink, 1.5f);
                Ui.Outlined(ban, $"再寻宝 {Meta.TotemOrangePity - S.TotemPity} 次必得传说及以上图腾（十连额外赠洗炼石×1）", 27, Gold, new Vector2(0, -216), new Vector2(880, 42), Ink, 1.8f);
                MetaUi.Pill(ban, "ic_refine", S.RefineStones.ToString(), new Vector2(-326, 174), 200);
                MetaUi.Pill(ban, "ic_hammer", S.Hammers.ToString(), new Vector2(336, 174), 200);

                var t1 = Ui.Btn(_page, "寻宝1次", new Vector2(360, 114), Vector2.zero, Pal.Azure, () => DoTotemPull(1), 40, S.Hammers >= 1 ? "ic_hammer" : "ic_ingot");
                Place(t1, -240, by);
                Ui.Outlined(t1, S.Hammers >= 1 ? "寻宝锤×1" : $"元宝×{Meta.TotemPullIngot1}", 22, Paper, new Vector2(0, -76), new Vector2(280, 30), Ink, 1.5f);
                var t10 = Ui.Btn(_page, "十连寻宝", new Vector2(360, 114), Vector2.zero, Pal.Gold, () => DoTotemPull(10), 40, S.Hammers >= 10 ? "ic_hammer" : "ic_ingot");
                Place(t10, 240, by);
                Ui.Outlined(t10, S.Hammers >= 10 ? "寻宝锤×10" : $"元宝×{Meta.TotemPullIngot10}（九折）", 22, Paper, new Vector2(0, -76), new Vector2(320, 30), Ink, 1.5f);
                if (S.Hammers >= 1) MetaUi.Dot(S.Hammers >= 10 ? t10 : t1, new Vector2(164, 50));
            }

            float my = 1032 + sp * 0.26f;
            var market = Top(0, my, new Vector2(1040, 10), "market");
            market.pivot = new Vector2(0.5f, 1f);
            float mh = Mathf.Max(600f, Ui.Height - Ui.SafeTop - my - 186f);
            float me = mh - 620f;
            Ui.Skin(market, "dark", new Vector2(1040, mh), new Vector2(0, -mh * 0.5f));
            var shopHdr = Top(0, my, new Vector2(900, 68), "shopHdr");
            Ui.Skin(shopHdr, "h_ribbon", new Vector2(390, 74), Vector2.zero, -1f);
            Ui.Outlined(shopHdr, "梁山集市", 34, Paper, new Vector2(0, 3), new Vector2(340, 58), AzureDeep, 2.2f);
            var items = Meta.Shop(S);
            for (int i = 0; i < items.Length; i++)
            {
                var c = ShopCard(items[i], i);
                Place(c, ((i % 4) - 1.5f) * 255, my + 175 + me * 0.25f + (i / 4) * (270 + me * 0.2f));
                if (anim) Tween.Pop(c, 0.3f, 0.05f + i * 0.04f);
            }
        }

        public void DoTotemPull(int n)
        {
            if (S.Hammers >= n) S.Hammers -= n;
            else
            {
                int cost = n >= 10 ? Meta.TotemPullIngot10 : Meta.TotemPullIngot1 * n;
                if (!Meta.Spend(S, Cur.Ingot, cost))
                {
                    Sfx.Play(Snd.Deny);
                    Ui.Toast("寻宝锤或元宝不足", 200);
                    return;
                }
            }
            if (n >= 10) S.RefineStones += 1;
            var pulls = Meta.PullTotems(S, n, new Rng(Environment.TickCount));
            Profile.Save();
            Rebuild(false);
            MetaUi.TotemPullResult(Layer, pulls, () => DoTotemPull(n));
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
            Ui.Label(c, d.Name, 27, Pal.Ink, new Vector2(0, 92), new Vector2(220, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Icon(c, d.Icon, 110, new Vector2(0, 26));
            Ui.Outlined(c, "×" + Ui.Num(d.Get.N), 28, Color.white, new Vector2(54, -16), new Vector2(120, 36), Ink, 2f);
            int used = d.Ad ? (d.Get.Cur == Cur.Ingot ? S.AdIngots : S.AdStamina) : 0;
            int max = d.Ad ? (d.Get.Cur == Cur.Ingot ? Meta.AdIngotsMax : Meta.AdStaminaMax) : 0;
            bool soldOut = d.Ad && used >= max;
            var pb = Ui.Skin(c, Ui.BtnSkin(soldOut ? Pal.Slate : d.Ad ? Pal.Green : Pal.Azure), new Vector2(206, 66), new Vector2(0, -84), -1f);
            if (d.Ad)
            {
                Ui.Icon(pb.transform, "ic_ad", 46, new Vector2(-64, 2));
                Ui.Outlined(pb.transform, soldOut ? "已领完" : $"免费 {max - used}", 25, Color.white, new Vector2(18, 1), new Vector2(150, 54), Ink, 1.5f);
            }
            else
            {
                Ui.Icon(pb.transform, "ic_ingot", 50, new Vector2(-60, 2));
                Ui.Outlined(pb.transform, d.Price.ToString(), 27, Color.white, new Vector2(18, 1), new Vector2(140, 54), Ink, 1.5f);
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

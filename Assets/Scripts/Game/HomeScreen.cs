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

        public void SetTab(int t)
        {
            if (t == _tab) return;
            _tab = t;
            Rebuild(true);
        }

        /// <summary>任务“前往”：跳到能完成该任务的地方。</summary>
        public void Goto(int task)
        {
            int[] tabs = { TabBattle, TabBattle, TabHero, TabForge, TabShop, TabShop };
            SetTab(tabs[Mathf.Clamp(task, 0, tabs.Length - 1)]);
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

        void Shade(float top, float bottom)
        {
            if (top > 0)
            {
                var t = Ui.Box(_page, new Vector2(1080, 420), Vector2.zero, new Color(0.05f, 0.03f, 0.02f, top), Art.VGrad, "shadeTop");
                t.rectTransform.anchorMin = new Vector2(0, 1);
                t.rectTransform.anchorMax = new Vector2(1, 1);
                t.rectTransform.sizeDelta = new Vector2(0, 420);
                t.rectTransform.anchoredPosition = new Vector2(0, -210);
                t.rectTransform.localScale = new Vector3(1, -1, 1);
            }
            if (bottom > 0)
            {
                var b = Ui.Box(_page, new Vector2(1080, 760), Vector2.zero, new Color(0.05f, 0.03f, 0.02f, bottom), Art.VGrad, "shadeBottom");
                b.rectTransform.anchorMin = new Vector2(0, 0);
                b.rectTransform.anchorMax = new Vector2(1, 0);
                b.rectTransform.sizeDelta = new Vector2(0, 760);
                b.rectTransform.anchoredPosition = new Vector2(0, 380);
            }
        }

        // ---------------- 顶栏、页签 ----------------

        void BuildTopBar()
        {
            var bar = Ui.Top(_page, 0, 0, new Vector2(1080, 150 + Ui.SafeTop), "topbar");
            bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = Vector2.zero;
            var bgi = Ui.Box(bar, new Vector2(1080, 150 + Ui.SafeTop), new Vector2(0, -(150 + Ui.SafeTop) * 0.5f), Pal.Ink.A(0.55f), Art.VGrad);
            bgi.rectTransform.localScale = new Vector3(1, -1, 1);
            float y = Ui.SafeTop + 72;

            var av = Ui.Top(_page, -462, y, new Vector2(124, 124), "avatar");
            var frame = Ui.Icon(av, "ui/medal", 128, Vector2.zero, "frame");
            Ui.Icon(av, "prop_songjiang", 108, new Vector2(0, 6));
            var pw = Ui.Skin(av, "btn_red", new Vector2(156, 44), new Vector2(0, -64), -1f);
            Ui.Outlined(pw.transform, "战力 " + Ui.Num(Meta.TeamPower(S)), 22, Pal.Hex("FFE7B0"), new Vector2(0, 1), new Vector2(156, 44), Pal.Ink, 1.5f);
            Ui.Tap(frame, () => SetTab(TabHero), av);

            CurPill(-250, y, "ic_ingot", Ui.Num(S.Ingots), () => SetTab(TabShop));
            CurPill(0, y, "ic_coins", Ui.Num(S.Copper), null);
            var st = CurPill(250, y, "ic_wine", $"{S.Stamina}/{Meta.StaminaMax}", () => MetaUi.StaminaShop(Layer));
            _staminaText = st.Find("text").GetComponent<Text>();
            _staminaWait = Ui.Outlined(st, "", 22, Pal.Hex("C8F0B0"), new Vector2(20, -40), new Vector2(160, 30), Pal.Ink, 1.5f);
            _tickT = 0;
        }

        RectTransform CurPill(float x, float y, string icon, string text, Action plus)
        {
            var c = Ui.Top(_page, x, y, new Vector2(230, 64), "cur_" + icon);
            Ui.Skin(c, "pill", new Vector2(214, 56), new Vector2(10, 0), -1f);
            Ui.Icon(c, icon, 76, new Vector2(-92, 2));
            Ui.Outlined(c, text, 30, Color.white, new Vector2(14, 1), new Vector2(150, 54), Pal.Ink, 2f, TextAnchor.MiddleCenter, "text");
            if (plus != null)
            {
                var p = Ui.Skin(c, "btn_green", new Vector2(48, 48), new Vector2(98, 0), -1f);
                Ui.Outlined(p.transform, "+", 34, Color.white, new Vector2(0, 3), new Vector2(48, 48), Pal.GreenDeep, 1.5f);
                Ui.Tap(p, plus, c);
            }
            return c;
        }

        void BuildTabs()
        {
            var bar = Ui.Rect(_page, "tabs");
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.sizeDelta = new Vector2(0, 190);
            bar.anchoredPosition = new Vector2(0, 95);
            var bgi = Ui.Skin(bar, "tabbar", new Vector2(1080, 190), Vector2.zero);
            Stretch(bgi.rectTransform);
            bgi.rectTransform.offsetMin = new Vector2(-40, -Ui.SafeBottom - 40);
            bgi.rectTransform.offsetMax = new Vector2(40, 0);
            string[] names = { "商城", "好汉", "出征", "寨主", "山寨" };
            string[] icons = { "tab_shop", "tab_hero", "tab_battle", "tab_forge", "tab_fort" };
            bool[] dots = { ShopBadge(), HeroBadge(), BattleBadge(), ForgeBadge(), FortBadge() };
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 216;
                bool on = i == _tab;
                var tab = Ui.Rect(bar, "tab" + i);
                tab.anchoredPosition = new Vector2(x, on ? 14 : 0);
                tab.sizeDelta = new Vector2(216, 190);
                var face = on ? Ui.Skin(tab, "tab_on", new Vector2(200, 206), new Vector2(0, 2))
                    : Ui.Box(tab, new Vector2(204, 176), new Vector2(0, -2), new Color(0, 0, 0, 0), Art.Rounded);
                face.raycastTarget = true;
                Ui.Icon(tab, icons[i], on ? 140 : 112, new Vector2(0, on ? 28 : 20));
                Ui.Outlined(tab, names[i], on ? 36 : 30, on ? Pal.Hex("FFF2C8") : Pal.Hex("C8C4D8"), new Vector2(0, -62), new Vector2(210, 50), on ? Pal.Hex("7A3A10") : Pal.Ink, 2f);
                if (dots[i]) MetaUi.Dot(tab, new Vector2(62, 64));
                int ti = i;
                Ui.Tap(face, () => SetTab(ti), tab);
            }
        }

        static bool ShopBadge() => S.AdIngots < Meta.AdIngotsMax || S.AdRecruit < Meta.AdRecruitMax || S.Tokens >= 1;
        static bool HeroBadge()
        {
            for (int h = 0; h < Defs.HeroCount; h++) if (Meta.CanLevel(S, (HeroId)h)) return true;
            return false;
        }
        static bool ForgeBadge()
        {
            for (int i = 0; i < Meta.Equips.Length; i++) if (Meta.CanForge(S, i)) return true;
            return false;
        }
        static bool IdleBadge() => Meta.IdleHours(S, Profile.Now) >= 1;
        static bool ChestBadge(int ch)
        {
            for (int k = 0; k < 3; k++) if (Meta.CanOpenChest(S, ch, k)) return true;
            return false;
        }
        static bool BattleBadge()
        {
            if (Meta.CanSign(S, Profile.Today) || Meta.TaskBadge(S) > 0 || Meta.CodexBadge(S) > 0 || IdleBadge()) return true;
            for (int c = 1; c <= Defs.Chapters.Length; c++) if (ChestBadge(c)) return true;
            return false;
        }
        static bool FortBadge() => IdleBadge() || Meta.TaskBadge(S) > 0;

        // ---------------- 出征 ----------------

        void BuildBattle(bool anim)
        {
            Cover("home_bg", new Vector2(1080, 1920), Color.white, 1f);
            Shade(0.6f, 0.75f);
            var st = Defs.Level(S.Selected);
            var ch = Defs.Chapters[st.Chapter - 1];

            var hdr = Top(0, 228, new Vector2(700, 110), "chapter");
            Ui.Ribbon(hdr, $"第{st.Chapter}章 · {ch.Name}", Vector2.zero, 640);

            // 章节星级宝箱
            int stars = Meta.ChapterStars(S, st.Chapter);
            var sb = Top(30, 336, new Vector2(760, 90), "starBar");
            Ui.Icon(sb, "ic_star", 66, new Vector2(-410, 0));
            Ui.Outlined(sb, $"{stars}/30", 30, Color.white, new Vector2(-410, -42), new Vector2(120, 40), Pal.Ink, 2f);
            MetaUi.Bar(sb, new Vector2(0, 0), new Vector2(640, 28), stars / 30f, Pal.Gold);
            for (int k = 0; k < 3; k++)
            {
                float x = -320 + 640 * (k + 1) / 3f;
                bool got = (S.ChestClaimed[st.Chapter - 1] & (1 << k)) != 0;
                bool can = Meta.CanOpenChest(S, st.Chapter, k);
                var ic = Ui.Icon(sb, got ? "ic_chest_open" : k == 0 ? "ic_chest_wood" : k == 1 ? "ic_chest_silver" : "ic_chest_gold", 96, new Vector2(x - (k == 2 ? 20 : 0), 6));
                Ui.Outlined(sb, Meta.ChestStars[k].ToString(), 24, Pal.Hex("FFE08A"), new Vector2(x - (k == 2 ? 20 : 0), -44), new Vector2(80, 34), Pal.Ink, 1.5f);
                if (can)
                {
                    MetaUi.Dot(ic.transform, new Vector2(36, 36));
                    Tween.Run(ic, 999f, kk => ic.transform.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(kk * 999f * 8f)));
                }
                int ck = k, chap = st.Chapter;
                Ui.Tap(ic, () => MetaUi.Chest(Layer, chap, ck));
            }

            // 两侧功能按钮
            int day = Profile.Today;
            SideBtn(-462, 470, "tab_gift", "签到", Meta.CanSign(S, day), () => MetaUi.SignIn(Layer));
            SideBtn(-462, 620, "tab_task", "任务", Meta.TaskBadge(S) > 0, () => MetaUi.Tasks(Layer));
            SideBtn(-462, 770, "tab_codex", "图鉴", Meta.CodexBadge(S) > 0, () => MetaUi.Codex(Layer));
            SideBtn(462, 470, "tab_idle", "巡山", IdleBadge(), () => MetaUi.Idle(Layer));
            bool dOpen = S.Cleared >= MetaUi.DailyUnlock, eOpen = S.Cleared >= MetaUi.EscortUnlock;
            SideBtn(462, 620, "ic_medal", "挑战", dOpen && S.DailyTries < Meta.DailyTriesMax, () =>
            {
                if (!dOpen) Ui.Toast($"通关第 {MetaUi.DailyUnlock} 关后开放", 200);
                else MetaUi.Daily(Layer);
            }, !dOpen);
            SideBtn(462, 770, "en_porter", "生辰纲", eOpen && S.EscortTries < Meta.EscortTriesMax, () =>
            {
                if (!eOpen) Ui.Toast($"通关第 {MetaUi.EscortUnlock} 关后开放", 200);
                else MetaUi.Escort(Layer);
            }, !eOpen);
            SideBtn(462, 920, "ic_gear", "设置", false, () => MetaUi.Settings(Layer));

            // 首领与关卡
            var bd = Defs.Enemy(st.BossKind);
            var boss = Top(0, 720, new Vector2(420, 420), "boss");
            Ui.Box(boss, new Vector2(460, 240), new Vector2(0, -140), (st.IsChapterBoss ? Pal.Red : Pal.Purple).A(0.4f), Art.SoftDot);
            Ui.Box(boss, new Vector2(300, 70), new Vector2(0, -180), new Color(0, 0, 0, 0.45f), Art.SoftDot);
            var icon = Ui.Icon(boss, bd.Art, st.IsChapterBoss ? 420 : 360, new Vector2(0, st.IsChapterBoss ? 10 : -20));
            Tween.Run(icon, 999f, k => icon.rectTransform.anchoredPosition = new Vector2(0, (st.IsChapterBoss ? 10 : -20) + 6 * Mathf.Sin(k * 999f * 2f)));
            if (anim) Tween.Pop(boss, 0.35f);

            var tag = Top(0, 960, new Vector2(760, 96), "levelTag");
            Ui.Skin(tag, "plate", new Vector2(580, 92), Vector2.zero, -1f, st.IsChapterBoss ? Pal.Hex("FFB4A0") : Color.white);
            Ui.Outlined(tag, $"第 {st.No} 关 · {st.Name}", 40, Pal.Hex("FFE7B0"), new Vector2(0, 2), new Vector2(540, 70), Pal.Ink, 2.5f);
            if (S.Selected > 1) ArrowBtn(tag, -330, -1);
            if (S.Selected <= S.Cleared && S.Selected < Defs.LevelCount) ArrowBtn(tag, 330, 1);
            int got3 = S.Stars[st.No - 1];
            for (int i = 0; i < 3; i++)
            {
                var s = Ui.Icon(tag, "ic_star", 46, new Vector2((i - 1) * 50, -60));
                if (i >= got3) s.color = new Color(0.2f, 0.18f, 0.16f, 0.85f);
            }

            var info = Top(0, 1080, new Vector2(760, 50), "info");
            Ui.Skin(info, "pill", new Vector2(700, 52), Vector2.zero, -1f);
            string elite = st.EliteWaves.Length > 0 ? " · 精英 " + Defs.Enemy(st.EliteKinds[0]).Name : "";
            Ui.Label(info, $"{st.Waves} 波 · 首领 {bd.Name}{elite}", 28, Pal.Cream, Vector2.zero, new Vector2(700, 50));
            if (st.BuffRole.HasValue)
            {
                var bf = Top(0, 1140, new Vector2(500, 50), "buff");
                Ui.Skin(bf, "pill", new Vector2(460, 50), Vector2.zero, -1f, new Color(0.55f, 1f, 0.55f));
                Ui.Outlined(bf, "本关增益：" + st.BuffText, 26, Pal.Hex("C8FFB0"), Vector2.zero, new Vector2(440, 48), Pal.Ink, 1.5f);
            }

            // 出战阵容
            var row = Bottom(0, 610, new Vector2(900, 220), "lineup");
            Ui.Skin(row, "pill", new Vector2(640, 50), new Vector2(0, 112), -1f);
            Ui.Outlined(row, $"出战好汉 · 战力 {Ui.Num(Meta.TeamPower(S))}", 28, Pal.Hex("FFE08A"), new Vector2(0, 112), new Vector2(600, 46), Pal.Ink, 1.5f);
            var lu = Meta.Lineup(S);
            for (int i = 0; i < lu.Length; i++)
            {
                var t = HeroTile(row, lu[i], new Vector2((i - 1.5f) * 176, -8), 156, false);
                if (anim) Tween.Pop(t, 0.3f, 0.1f + i * 0.05f);
            }

            var start = Ui.Btn(_page, "出 征", new Vector2(500, 150), Vector2.zero, Pal.Hex("F6B83A"), () => Game.I.StartLevel(S.Selected), 66);
            start.anchorMin = start.anchorMax = new Vector2(0.5f, 0);
            start.anchoredPosition = new Vector2(0, 330);
            var glow = Ui.Box(start, new Vector2(660, 250), Vector2.zero, Pal.Gold.A(0.3f), Art.SoftDot, "glow");
            glow.transform.SetAsFirstSibling();
            var cost = Ui.Skin(start, "pill", new Vector2(150, 48), new Vector2(170, 66), -1f);
            Ui.Icon(cost.transform, "ic_wine", 52, new Vector2(-46, 2));
            Ui.Outlined(cost.transform, "-" + Meta.BattleStamina, 28, Color.white, new Vector2(20, 1), new Vector2(80, 44), Pal.Ink, 1.5f);
            Tween.Run(start, 9999f, k =>
            {
                float t = k * 9999f;
                start.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 4f));
                glow.color = Pal.Gold.A(0.22f + 0.14f * Mathf.Sin(t * 4f));
            });
        }

        void ArrowBtn(Transform p, float x, int dir)
        {
            var b = Ui.Icon(p, "ui/arrow", 92, new Vector2(x, 0), "arrow");
            if (dir > 0) b.rectTransform.localScale = new Vector3(-1, 1, 1);
            Ui.Tap(b, () =>
            {
                S.Selected = Mathf.Clamp(S.Selected + dir, 1, Mathf.Min(Defs.LevelCount, S.Cleared + 1));
                Profile.Save();
            });
        }

        void SideBtn(float x, float y, string icon, string name, bool dot, Action act, bool locked = false)
        {
            var b = Top(x, y, new Vector2(130, 140), "side_" + name);
            var bg = Ui.Icon(b, "ui/medal", 128, new Vector2(0, 12), "frame");
            var ic = Ui.Icon(b, icon, 96, new Vector2(0, 14));
            if (locked)
            {
                ic.color = new Color(0.5f, 0.5f, 0.5f);
                Ui.Icon(b, "ic_lock", 46, new Vector2(36, -16));
            }
            Ui.Outlined(b, name, 28, Color.white, new Vector2(0, -56), new Vector2(150, 40), Pal.Ink, 2f);
            if (dot) MetaUi.Dot(b, new Vector2(44, 58));
            Ui.Tap(bg, act, b);
        }

        /// <summary>好汉头像格：稀有度底色、等级、职业。</summary>
        RectTransform HeroTile(Transform p, HeroId h, Vector2 pos, float size, bool detail)
        {
            var d = Defs.Hero(h);
            int lv = S.HeroLv[(int)h];
            var tile = Ui.Rect(p, "hero_" + h);
            tile.anchoredPosition = pos;
            tile.sizeDelta = Vector2.one * size;
            var face = MetaUi.Slot(tile, MetaUi.RaritySlot(d.Rarity), size, Vector2.zero);
            Ui.Icon(tile, d.Art, size * 0.86f, new Vector2(0, size * 0.06f));
            Ui.Skin(tile, "pill", new Vector2(size - 6, size * 0.25f), new Vector2(0, -size * 0.38f), -1f);
            Ui.Outlined(tile, $"Lv.{lv} {d.Name}", (int)(size * 0.15f), Color.white, new Vector2(0, -size * 0.38f), new Vector2(size, size * 0.24f), Pal.Ink, 1.5f);
            MetaUi.Badge(tile, d.Role, MetaUi.RoleColor(d.Kind), new Vector2(size * 0.44f, size * 0.24f), new Vector2(size * 0.3f, size * 0.38f), (int)(size * 0.13f));
            if (Meta.CanLevel(S, h)) MetaUi.Dot(tile, new Vector2(-size * 0.4f, size * 0.4f));
            Ui.Tap(face, () => { if (detail) MetaUi.HeroDetail(Layer, h); else SetTab(TabHero); }, tile);
            return tile;
        }

        // ---------------- 好汉 ----------------

        void BuildHeroes(bool anim)
        {
            Cover("battle_bg", new Vector2(1080, 1920), new Color(0.7f, 0.66f, 0.66f), 0.3f);
            Shade(0.5f, 0f);
            var hdr = Top(0, 228, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "梁山好汉", Vector2.zero, 520);

            // 上半：出战的四位站在场景里
            var line = Meta.Lineup(S);
            var lu = Top(0, 470, new Vector2(1000, 300), "lineup");
            for (int i = 0; i < 4; i++)
            {
                var h = line[i];
                var d = Defs.Hero(h);
                var g = Ui.Rect(lu, "lu" + i);
                g.anchoredPosition = new Vector2((i - 1.5f) * 240, 0);
                g.sizeDelta = new Vector2(230, 300);
                Ui.Box(g, new Vector2(210, 64), new Vector2(0, -112), new Color(0, 0, 0, 0.5f), Art.SoftDot);
                var art = Ui.Icon(g, d.Art, 250, new Vector2(0, 12));
                Ui.Skin(g, "plate", new Vector2(220, 58), new Vector2(0, -128), -1f);
                Ui.Outlined(g, $"{d.Name} Lv.{S.HeroLv[(int)h]}", 26, Pal.Hex("FFE7B0"), new Vector2(0, -126), new Vector2(210, 50), Pal.Ink, 1.5f);
                if (Meta.CanLevel(S, h)) MetaUi.Dot(g, new Vector2(96, -104));
                Ui.Tap(art, () => MetaUi.HeroDetail(Layer, h), g);
                if (anim) Tween.Pop(g, 0.3f, 0.05f + i * 0.05f);
            }
            var gl = Top(0, 668, new Vector2(900, 56), "global");
            Ui.Skin(gl, "pill", new Vector2(760, 54), Vector2.zero, -1f);
            Ui.Outlined(gl, $"好汉总等级加成：全军伤害 +{Meta.GlobalDmg(S) * 100:0.#}%", 28, Pal.Hex("C8FFB0"), Vector2.zero, new Vector2(760, 54), Pal.Ink, 1.5f);

            // 下半：深色面板里的好汉卡
            var panel = Top(0, 730, new Vector2(1040, 10), "roster");
            panel.pivot = new Vector2(0.5f, 1f);
            float ph = Mathf.Max(880f, Ui.Height - Ui.SafeTop - 730f - 186f);
            Ui.Skin(panel, "dark", new Vector2(1040, ph), new Vector2(0, -ph * 0.5f));
            int owned = 0;
            for (int h = 0; h < Defs.HeroCount; h++) if (Meta.Owned(S, (HeroId)h)) owned++;
            Ui.Skin(panel, "plaque", new Vector2(360, 74), new Vector2(0, -6), -1f);
            Ui.Outlined(panel, $"好汉 {owned}/{Defs.HeroCount}", 34, Pal.Hex("FFE08A"), new Vector2(0, -4), new Vector2(340, 60), Pal.Ink, 2f);

            var order = new List<HeroId>();
            for (int h = 0; h < Defs.HeroCount; h++) order.Add((HeroId)h);
            order.Sort((a, b) =>
            {
                bool oa = Meta.Owned(S, a), ob = Meta.Owned(S, b);
                if (oa != ob) return oa ? -1 : 1;
                int r = Defs.Hero(b).Rarity.CompareTo(Defs.Hero(a).Rarity);
                return r != 0 ? r : a.CompareTo(b);
            });
            for (int i = 0; i < order.Count; i++)
            {
                var c = HeroCard(panel, order[i], new Vector2(((i % 4) - 1.5f) * 250, -(250 + (i / 4) * 370)));
                if (anim) Tween.Pop(c, 0.3f, 0.05f + i * 0.04f);
            }
            Ui.Outlined(panel, "碎片来源：通关 · 擂鼓招募 · 签到 · 章节宝箱", 26, Pal.Cream, new Vector2(0, -846), new Vector2(900, 44), Pal.Ink, 1.5f);
        }

        RectTransform HeroCard(Transform parent, HeroId h, Vector2 pos)
        {
            var d = Defs.Hero(h);
            int lv = S.HeroLv[(int)h];
            bool owned = lv > 0;
            var c = Ui.Rect(parent, "card_" + h);
            c.anchoredPosition = pos;
            c.sizeDelta = new Vector2(232, 340);
            var face = MetaUi.Card(c, owned ? MetaUi.RarityCard(d.Rarity) : "card_lock", new Vector2(232, 340), Vector2.zero);
            var art = Ui.Icon(c, d.Art, 214, new Vector2(0, 34));
            if (!owned) art.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            Ui.Outlined(c, d.Name, 32, Color.white, new Vector2(0, -136), new Vector2(220, 44), Pal.Ink, 2f);
            MetaUi.Badge(c, d.Role, MetaUi.RoleColor(d.Kind), new Vector2(96, 46), new Vector2(-54, 138), 24);
            if (owned)
            {
                Ui.Icon(c, "ui/lv", 72, new Vector2(74, 132));
                Ui.Outlined(c, lv.ToString(), 30, Pal.Hex("FFE7B0"), new Vector2(74, 133), new Vector2(72, 44), Pal.Ink, 2f);
                int need = Meta.ShardCost(lv);
                MetaUi.Bar(c, new Vector2(0, -92), new Vector2(190, 26), lv >= Meta.MaxHeroLv ? 1 : S.Shards[(int)h] / (float)need, Pal.Hex("4FB548"),
                    lv >= Meta.MaxHeroLv ? "满级" : $"{S.Shards[(int)h]}/{need}");
                if (Array.IndexOf(S.Lineup, (int)h) >= 0)
                {
                    var on = Ui.Icon(c, "ui/tag", 108, new Vector2(-66, 70), "onTag");
                    Ui.Outlined(on.transform, "出战", 26, Color.white, new Vector2(-6, 2), new Vector2(100, 60), Pal.RedDeep, 1.5f);
                }
                if (Meta.CanLevel(S, h))
                {
                    MetaUi.Dot(c, new Vector2(100, 158));
                    var up = Ui.Icon(c, "ic_fast", 56, new Vector2(86, -40));
                    up.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            else
            {
                Ui.Outlined(c, "未获得", 28, Pal.Hex("D8D0C0"), new Vector2(0, -92), new Vector2(200, 40), Pal.Ink, 1.5f);
                Ui.Icon(c, "ic_lock", 84, new Vector2(0, 40));
            }
            Ui.Tap(face, () => MetaUi.HeroDetail(Layer, h), c);
            return c;
        }

        // ---------------- 寨主（装备） ----------------

        void BuildForge(bool anim)
        {
            Cover("forge_scene", new Vector2(1080, 810), new Color(0.55f, 0.48f, 0.42f), 0.5f);
            Shade(0.55f, 0.7f);
            var hdr = Top(0, 228, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "寨主 · 宋江", Vector2.zero, 520);

            var sj = Top(0, 1000, new Vector2(640, 640), "songjiang");
            Ui.Box(sj, new Vector2(700, 380), new Vector2(0, -200), Pal.Gold.A(0.35f), Art.SoftDot);
            Ui.Box(sj, new Vector2(400, 90), new Vector2(0, -290), new Color(0, 0, 0, 0.45f), Art.SoftDot);
            var art = Ui.Icon(sj, "prop_songjiang", 640, Vector2.zero);
            Tween.Run(art, 999f, k => art.rectTransform.anchoredPosition = new Vector2(0, 5 * Mathf.Sin(k * 999f * 1.6f)));
            if (anim) Tween.Pop(sj, 0.35f);

            for (int i = 0; i < Meta.Equips.Length; i++)
            {
                float x = i < 3 ? -390 : 390;
                float y = 500 + (i % 3) * 270;
                var e = Meta.Equips[i];
                int lv = S.EquipLv[i];
                var c = Top(x, y, new Vector2(220, 230), "eq" + i);
                var face = MetaUi.Slot(c, lv >= 30 ? "slot_orange" : lv >= 20 ? "slot_purple" : lv >= 10 ? "slot_blue" : "slot_grey", 196, new Vector2(0, 26));
                Ui.Icon(c, e.Icon, 150, new Vector2(0, 30));
                var lvb = Ui.Skin(c, "pill", new Vector2(116, 44), new Vector2(46, -50), -1f);
                Ui.Outlined(lvb.transform, "Lv." + lv, 26, Color.white, new Vector2(0, 1), new Vector2(116, 44), Pal.Ink, 1.5f);
                Ui.Outlined(c, e.Name, 30, Pal.Hex("FFE7B0"), new Vector2(0, -96), new Vector2(220, 44), Pal.Ink, 2f);
                if (Meta.CanForge(S, i)) MetaUi.Dot(c, new Vector2(88, 112));
                int si = i;
                Ui.Tap(face, () => MetaUi.ForgeSlot(Layer, si), c);
                if (anim) Tween.Pop(c, 0.3f, 0.1f + i * 0.05f);
            }

            var sum = Bottom(0, 440, new Vector2(980, 300), "summary");
            Ui.Panel(sum, new Vector2(940, 280), Vector2.zero);
            for (int i = 0; i < Meta.Equips.Length; i++)
                Ui.Label(sum, Meta.EquipText(i, S.EquipLv[i]), 28, Pal.Ink, new Vector2(i < 3 ? -220 : 220, 84 - (i % 3) * 50), new Vector2(430, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(sum, $"强化上限 Lv.{Meta.EquipMax(S)}（随通关提升）", 26, Pal.Hex("8A4A1A"), new Vector2(-170, -88), new Vector2(560, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
            MetaUi.Pill(sum, "ic_iron", Ui.Num(S.Iron), new Vector2(320, -88), 220);
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
            Shade(0.45f, 0.4f);
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
                Ui.Outlined(c, b.Name, 32, Pal.Hex("FFE7B0"), new Vector2(22, 1), new Vector2(160, 50), Pal.Ink, 2f);
                Ui.Label(c, tips[i], 22, Color.white, new Vector2(0, -46), new Vector2(240, 30), TextAnchor.MiddleCenter, Fonts.Heavy).gameObject.AddComponent<Outline>().effectColor = Pal.Ink;
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
            Cover("home_bg", new Vector2(1080, 1920), new Color(0.42f, 0.36f, 0.34f), 0.5f);
            Shade(0.3f, 0.5f);
            var ban = Top(0, 560, new Vector2(1040, 540), "banner");
            Ui.Skin(ban, "panel", new Vector2(1036, 576), Vector2.zero);
            var img = Ui.Box(ban, new Vector2(900, 506), Vector2.zero, Color.white, Art.Img("recruit_banner"));
            img.type = Image.Type.Simple;
            Ui.Box(ban, new Vector2(900, 116), new Vector2(0, -195), Pal.Ink.A(0.72f), Art.White);
            var hdr = Top(0, 228, new Vector2(700, 110), "hdr");
            Ui.Ribbon(hdr, "擂鼓招募", Vector2.zero, 520);
            Ui.Outlined(ban, $"传说 {Meta.OrangeRate(S) * 100:0}% · 史诗 {Meta.PurpleRate(S) * 100:0}% · 十连必出史诗", 28, Pal.Hex("FFE7B0"), new Vector2(0, -170), new Vector2(880, 40), Pal.Ink, 1.5f);
            int left = Meta.OrangePity - S.Pity;
            Ui.Outlined(ban, $"再招募 {left} 次必得传说好汉", 30, Pal.Hex("FFB050"), new Vector2(0, -218), new Vector2(880, 44), Pal.Ink, 2f);
            var lv = Ui.Rect(ban, "lv");
            lv.anchoredPosition = new Vector2(-250, 170);
            Ui.Skin(lv, "pill", new Vector2(380, 74), Vector2.zero, -1f);
            Ui.Outlined(lv, $"招募等级 {S.RecruitLv}", 28, Pal.Hex("FFE08A"), new Vector2(-70, 12), new Vector2(220, 36), Pal.Ink, 1.5f);
            MetaUi.Bar(lv, new Vector2(0, -16), new Vector2(330, 18), S.RecruitLv >= Meta.RecruitMaxLv ? 1 : S.RecruitExp / (float)Meta.RecruitNeed(S.RecruitLv), Pal.Gold);
            MetaUi.Pill(ban, "ic_token", S.Tokens.ToString(), new Vector2(340, 170), 200);

            int adLeft = Meta.AdRecruitMax - S.AdRecruit;
            var b1 = Ui.Btn(_page, "招募", new Vector2(300, 120), Vector2.zero, Pal.Hex("E8862A"), () => DoRecruit(1, false), 42, "ic_token");
            Place(b1, -340, 910);
            Ui.Outlined(b1, "招募令×1", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            var b10 = Ui.Btn(_page, "十连", new Vector2(300, 120), Vector2.zero, Pal.Hex("D8452F"), () => DoRecruit(10, false), 42, "ic_token");
            Place(b10, 0, 910);
            Ui.Outlined(b10, "招募令×10", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            var ba = Ui.Btn(_page, adLeft > 0 ? "免费" : "明日", new Vector2(300, 120), Vector2.zero, adLeft > 0 ? Pal.Teal : Pal.Slate, () => DoRecruit(1, true), 42, "ic_ad");
            Place(ba, 340, 910);
            Ui.Outlined(ba, $"今日剩余 {adLeft} 次", 22, Pal.Cream, new Vector2(0, -78), new Vector2(240, 30), Pal.Ink, 1.5f);
            if (S.Tokens >= 1) MetaUi.Dot(b1, new Vector2(140, 54));
            if (adLeft > 0) MetaUi.Dot(ba, new Vector2(140, 54));

            var shopHdr = Top(0, 1040, new Vector2(900, 60), "shopHdr");
            Ui.Skin(shopHdr, "plaque", new Vector2(380, 72), Vector2.zero, -1f);
            Ui.Outlined(shopHdr, "梁山集市", 34, Pal.Hex("FFE08A"), new Vector2(0, 2), new Vector2(360, 60), Pal.Ink, 2f);
            var items = Meta.Shop(S);
            for (int i = 0; i < items.Length; i++)
            {
                var c = ShopCard(items[i], i);
                Place(c, ((i % 4) - 1.5f) * 255, 1215 + (i / 4) * 270);
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
                if (!soldOut) MetaUi.Dot(c, new Vector2(100, 106));
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
                Ads.Show(() =>
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
                Ads.Show(() =>
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

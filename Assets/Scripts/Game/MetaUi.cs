using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Liangshan
{
    /// <summary>局外通用部件和弹窗：奖励格、红点、弹窗框，签到/任务/巡山/图鉴/设置/挑战/补给/招募结果/好汉详情/锻造。</summary>
    public static class MetaUi
    {
        static SaveData S => Profile.S;

        // ---------------- 颜色、图标 ----------------

        public static Color RoleColor(Role r) => Pal.Hex(Defs.RoleColors[(int)r]).Mul(0.85f);

        public static Color RarityColor(int r) => r >= 4 ? Pal.Hex("E0862A") : r == 3 ? Pal.Hex("9A5BD8") : Pal.Hex("4F86B8");
        public static string RarityName(int r) => r >= 4 ? "传说" : r == 3 ? "史诗" : "稀有";

        public static string CurIcon(Item it) =>
            it.Cur == Cur.Ingot ? "ic_ingot" : it.Cur == Cur.Copper ? "ic_coins" : it.Cur == Cur.Iron ? "ic_iron" :
            it.Cur == Cur.Token ? "ic_token" : it.Cur == Cur.Stamina ? "ic_wine" : Defs.Hero(it.Hero).Art;

        public static string CurName(Item it) =>
            it.Cur == Cur.Ingot ? "元宝" : it.Cur == Cur.Copper ? "铜钱" : it.Cur == Cur.Iron ? "精铁" :
            it.Cur == Cur.Token ? "招募令" : it.Cur == Cur.Stamina ? "体力" : Defs.Hero(it.Hero).Name + "碎片";

        static string SlotSkin(Item it) =>
            it.Cur == Cur.Ingot ? "slot_orange" : it.Cur == Cur.Copper ? "slot_wood" : it.Cur == Cur.Iron ? "slot_grey" :
            it.Cur == Cur.Token ? "slot_purple" : it.Cur == Cur.Stamina ? "slot_blue" : RaritySlot(Defs.Hero(it.Hero).Rarity);

        public static string RaritySlot(int r) => r >= 4 ? "slot_orange" : r == 3 ? "slot_purple" : "slot_blue";
        public static string RarityCard(int r) => r >= 4 ? "card_orange" : r == 3 ? "card_purple" : "card_blue";

        /// <summary>手绘方格，按宽度等比放大边框。</summary>
        public static Image Slot(Transform p, string skin, float size, Vector2 pos) =>
            Ui.Skin(p, skin, Vector2.one * size, pos, size / 133f, null, "slot");

        /// <summary>手绘卡框，按宽度等比放大边框，底部留名字条。</summary>
        public static Image Card(Transform p, string skin, Vector2 size, Vector2 pos) =>
            Ui.Skin(p, skin, size, pos, size.x / 188f, null, "card");

        /// <summary>按颜色挑手绘按钮皮做小徽章（职业、稀有度、等级标签）。</summary>
        public static Image Badge(Transform p, string text, Color c, Vector2 size, Vector2 pos, int font)
        {
            var b = Ui.Skin(p, Ui.BtnSkin(c), size, pos, -1f, null, "badge");
            Ui.Outlined(b.transform, text, font, Color.white, new Vector2(0, 2), size, Pal.Ink, 1.5f);
            return b;
        }

        // ---------------- 部件 ----------------

        public static RectTransform ItemTile(Transform p, Item it, Vector2 pos, float size = 150, bool label = true)
        {
            var t = Ui.Rect(p, "item");
            t.anchoredPosition = pos;
            t.sizeDelta = Vector2.one * size;
            Slot(t, SlotSkin(it), size + 8, Vector2.zero);
            Ui.Icon(t, CurIcon(it), size * 0.74f, new Vector2(0, size * 0.06f));
            if (it.Cur == Cur.Shard) Ui.Icon(t, "ic_shard", size * 0.36f, new Vector2(-size * 0.32f, size * 0.32f));
            Ui.Outlined(t, "×" + Ui.Num(it.N), (int)(size * 0.21f), Color.white, new Vector2(-size * 0.06f, -size * 0.33f),
                new Vector2(size, size * 0.3f), Pal.Ink, 2f, TextAnchor.MiddleRight);
            if (label) Ui.Outlined(t, CurName(it), (int)(size * 0.17f), Color.white, new Vector2(0, -size * 0.66f), new Vector2(size * 1.5f, size * 0.26f), Pal.Ink, 1.5f);
            return t;
        }

        public static void ItemRow(Transform p, List<Item> items, Vector2 center, float size, bool label = true)
        {
            float gap = size * 1.25f;
            for (int i = 0; i < items.Count; i++)
            {
                var t = ItemTile(p, items[i], center + new Vector2((i - (items.Count - 1) * 0.5f) * gap, 0), size, label);
                Tween.Pop(t, 0.3f, 0.15f + i * 0.08f);
            }
        }

        /// <summary>红点：n&gt;0 时显示数字。</summary>
        public static Image Dot(Transform p, Vector2 pos, int n = 0)
        {
            var d = Ui.Icon(p, "ui/dot", 44, pos, "dot");
            if (n > 0) Ui.Outlined(d.transform, n.ToString(), 24, Color.white, new Vector2(0, 1), new Vector2(44, 44), Pal.RedDeep, 1.5f);
            return d;
        }

        /// <summary>小号资源条：图标 + 数字。</summary>
        public static RectTransform Pill(Transform p, string icon, string text, Vector2 pos, float w = 220)
        {
            var c = Ui.Rect(p, "pill");
            c.anchoredPosition = pos;
            c.sizeDelta = new Vector2(w, 60);
            Ui.Skin(c, "pill", new Vector2(w - 20, 52), new Vector2(10, 0), -1f);
            Ui.Icon(c, icon, 70, new Vector2(-w * 0.5f + 28, 2));
            Ui.Outlined(c, text, 30, Color.white, new Vector2(22, 1), new Vector2(w - 70, 50), Pal.Ink, 2f, TextAnchor.MiddleCenter, "text");
            return c;
        }

        /// <summary>进度条：k 0..1。绿、金用原色手绘条，其它颜色给白条染色。</summary>
        public static void Bar(Transform p, Vector2 pos, Vector2 size, float k, Color fill, string text = null)
        {
            Ui.Skin(p, "bar_bg", size + new Vector2(10, 10), pos, -1f, null, "barBg");
            k = Mathf.Clamp01(k);
            if (k > 0.001f)
            {
                Color.RGBToHSV(fill, out float h, out float s, out _);
                bool green = s > 0.35f && h > 0.2f && h < 0.45f, gold = s > 0.35f && h > 0.1f && h < 0.18f;
                var f = Ui.Skin(p, green ? "bar_green" : gold ? "bar_gold" : "bar_white",
                    new Vector2(Mathf.Max(size.y * 1.2f, size.x * k), size.y), pos, -1f, green || gold ? Color.white : fill, "barFill");
                f.rectTransform.pivot = new Vector2(0, 0.5f);
                f.rectTransform.anchoredPosition = pos - new Vector2(size.x * 0.5f, 0);
            }
            if (text != null) Ui.Outlined(p, text, (int)(size.y * 0.78f), Color.white, pos + new Vector2(0, 1), size, Pal.Ink, 1.6f);
        }

        // ---------------- 弹窗框 ----------------

        /// <summary>同名弹窗再开时直接替换（数值刷新不重播弹出动画）。</summary>
        /// <summary>title 为 null 时不挂绶带（好汉详情这类自带抬头的弹窗）。</summary>
        public static RectTransform Modal(Transform layer, string key, string title, Vector2 size, out RectTransform panel, Color? ribbon = null, float y = -20)
        {
            var old = layer.Find("modal_" + key);
            bool again = old != null;
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = Ui.Rect(layer, "modal_" + key);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            var dim = Ui.Fill(root, new Color(0, 0, 0, 0.62f), true, "dim");
            var b = dim.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Close(root));
            panel = Ui.Panel(root, size, new Vector2(0, y));
            if (title != null) Ui.Ribbon(panel, title, new Vector2(0, size.y * 0.5f + 6), Mathf.Min(620, size.x - 80), ribbon);
            var r = root;
            var x = Ui.Icon(panel, "ui/close", 100, new Vector2(size.x * 0.5f - 4, size.y * 0.5f - 4), "close");
            Ui.Tap(x, () => Close(r), null, false);
            if (!again)
            {
                Sfx.Play(Snd.Open, 0.7f);
                Tween.Fade(cg, 0, 1, 0.14f);
                var pn = panel;
                pn.localScale = Vector3.one * 0.7f;
                Tween.Run(pn, 0.26f, k => pn.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Tween.OutBack(k)));
            }
            root.SetAsLastSibling();
            return root;
        }

        /// <summary>收起：先改名腾出位置（马上再开同名弹窗不受影响），缩小淡出后再销毁。</summary>
        public static void Close(RectTransform modal)
        {
            if (!modal || modal.name.EndsWith("_closing")) return;
            modal.name += "_closing";
            Sfx.Play(Snd.Close, 0.6f);
            foreach (var g in modal.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            var cg = modal.GetComponent<CanvasGroup>();
            var panel = modal.Find("panel");
            Tween.Run(modal, 0.14f, k =>
            {
                if (cg) cg.alpha = 1 - k;
                if (panel) panel.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, k);
            }, () => { if (modal) Object.Destroy(modal.gameObject); });
        }

        public static bool IsOpen(Transform layer, string key) => layer.Find("modal_" + key) != null;

        static void Save() => Profile.Save();

        // ---------------- 奖励弹窗 ----------------

        /// <summary>恭喜获得：有新好汉先全屏揭晓，再弹道具。</summary>
        public static void ShowRewards(Transform layer, string title, List<Item> items, HeroId? unlock = null, Action done = null)
        {
            void Items()
            {
                if (items.Count > 0) MetaFx.Gain(layer, title, items, done);
                else done?.Invoke();
            }
            if (unlock.HasValue) MetaFx.HeroReveal(layer, unlock.Value, Items);
            else Items();
        }

        public static void ShowRewards(Transform layer, string title, Rewards r) => ShowRewards(layer, title, r.Items, r.Unlock);

        // ---------------- 体力补给 ----------------

        public static void StaminaShop(Transform layer)
        {
            var m = Modal(layer, "stamina", "补充体力", new Vector2(880, 760), out var p, Pal.Hex("3A8A5A"));
            Ui.Icon(p, "ic_wine", 200, new Vector2(0, 200));
            Ui.Outlined(p, $"体力 {S.Stamina}/{Meta.StaminaMax}", 44, Pal.Ink, new Vector2(0, 70), new Vector2(700, 60), Pal.Cream, 1f);
            int wait = Meta.StaminaWait(S, Profile.Now);
            Ui.Label(p, S.Stamina >= Meta.StaminaMax ? "体力已满" : $"每 5 分钟回复 1 点（下一点 {wait / 60}:{wait % 60:00}）· 出征消耗 {Meta.BattleStamina}",
                28, Pal.Hex("6E4A2A"), new Vector2(0, 16), new Vector2(780, 44));
            int adLeft = Meta.AdStaminaMax - S.AdStamina;
            Row(p, -110, "ic_ad", "看广告领体力", $"+30 · 今日剩余 {adLeft} 次", adLeft > 0 ? "免费领" : "已用完", adLeft > 0 ? Pal.Teal : Pal.Slate, () =>
            {
                if (S.AdStamina >= Meta.AdStaminaMax) return;
                Ads.Show(() =>
                {
                    S.AdStamina++;
                    Meta.Give(S, new Item(Cur.Stamina, 30));
                    Save();
                    StaminaShop(layer);
                    Ui.Toast("体力 +30", 200);
                });
            });
            int buyLeft = Meta.ShopStaminaMax - S.ShopStamina;
            Row(p, -260, "ic_ingot", "元宝换体力", $"+60 · 花 80 元宝 · 剩余 {buyLeft} 次", buyLeft > 0 ? "兑换" : "已用完", buyLeft > 0 ? Pal.Hex("F2B632") : Pal.Slate, () =>
            {
                if (S.ShopStamina >= Meta.ShopStaminaMax) return;
                if (!Meta.Spend(S, Cur.Ingot, 80)) { Ui.Toast("元宝不足", 200); Sfx.Play(Snd.Deny); return; }
                S.ShopStamina++;
                Meta.Give(S, new Item(Cur.Stamina, 60));
                Save();
                StaminaShop(layer);
                Ui.Toast("体力 +60", 200);
            });
        }

        /// <summary>弹窗里的一行：图标、标题、说明、右侧按钮。</summary>
        static RectTransform Row(Transform p, float y, string icon, string title, string sub, string btn, Color face, Action act, float w = 780)
        {
            var r = Ui.Rect(p, "row");
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = new Vector2(w, 130);
            Ui.Skin(r, "row", new Vector2(w, 130), Vector2.zero);
            Slot(r, "slot_wood", 112, new Vector2(-w * 0.5f + 70, 0));
            Ui.Icon(r, icon, 96, new Vector2(-w * 0.5f + 70, 2));
            Ui.Label(r, title, 34, Pal.Ink, new Vector2(-w * 0.5f + 140 + 200, 22), new Vector2(400, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Label(r, sub, 26, Pal.Hex("7A5432"), new Vector2(-w * 0.5f + 140 + 200, -24), new Vector2(400, 44), TextAnchor.MiddleLeft);
            if (btn != null) Ui.Btn(r, btn, new Vector2(200, 88), new Vector2(w * 0.5f - 120, 0), face, act, 34);
            return r;
        }

        // ---------------- 签到 ----------------

        public static void SignIn(Transform layer)
        {
            Modal(layer, "sign", "七日签到", new Vector2(960, 1000), out var p);
            int day = Profile.Today;
            bool can = Meta.CanSign(S, day);
            for (int i = 0; i < Meta.SignRewards.Length; i++)
            {
                bool last = i == 6;
                var pos = last ? new Vector2(0, -250) : new Vector2(((i % 3) - 1) * 280, 280 - (i / 3) * 250);
                var size = last ? new Vector2(820, 210) : new Vector2(250, 220);
                var c = Ui.Rect(p, "day" + i);
                c.anchoredPosition = pos;
                c.sizeDelta = size;
                bool taken = i < S.SignDays;
                bool today = i == S.SignDays && can;
                var face = Ui.Skin(c, today ? "tab_on" : "row", size, Vector2.zero, 1f, !today && last ? Pal.Hex("E6CFF0") : Color.white);
                Ui.Outlined(c, $"第{i + 1}天", 30, Color.white, new Vector2(0, size.y * 0.5f - 28), new Vector2(240, 44), Pal.Ink, 2f);
                var it = Meta.SignRewards[i];
                if (last)
                {
                    var sd = Defs.Hero(it.Hero);
                    Ui.Icon(c, sd.Art, 190, new Vector2(-260, 0));
                    Ui.Outlined(c, $"{sd.RarityName}·{sd.Nick} {sd.Name}", 44, Pal.Hex("FFE08A"), new Vector2(80, 20), new Vector2(560, 60), Pal.Hex("5A2A8A"), 3f);
                    Ui.Label(c, "未拥有直接入伙，已拥有得 20 碎片", 28, Pal.Hex("5A2A6A"), new Vector2(80, -36), new Vector2(560, 44));
                }
                else ItemTile(c, it, new Vector2(0, -6), 120, false);
                if (taken)
                {
                    Ui.Box(c, size, Vector2.zero, new Color(0, 0, 0, 0.45f), Art.Rounded);
                    Ui.Icon(c, "ic_thumb", 90, Vector2.zero);
                }
                else if (today)
                {
                    var glow = Ui.Box(c, size + new Vector2(30, 30), Vector2.zero, Pal.Gold.A(0.4f), Art.Rounded);
                    glow.transform.SetAsFirstSibling();
                    Tween.Run(glow, 999f, k => glow.color = Pal.Gold.A(0.25f + 0.2f * Mathf.Sin(k * 999f * 5f)));
                    Ui.Tap(face, () =>
                    {
                        bool had = it.Cur != Cur.Shard || Meta.Owned(S, it.Hero);
                        var got = Meta.Sign(S, day);
                        if (got == null) return;
                        Save();
                        SignIn(layer);
                        var g = got.Value;
                        if (!had) ShowRewards(layer, "签到奖励", new List<Item>(), g.Hero);
                        else ShowRewards(layer, "签到奖励", new List<Item> { g });
                    }, c);
                }
            }
            Ui.Label(p, can ? "点亮今天的格子领取" : S.SignDays >= 7 ? "七日签到已全部领取" : "今日已签到，明天再来", 28, Pal.Hex("7A5432"), new Vector2(0, -420), new Vector2(800, 44));
        }

        // ---------------- 每日任务 ----------------

        public static void Tasks(Transform layer)
        {
            var m = Modal(layer, "tasks", "每日任务", new Vector2(960, 1260), out var p);
            for (int i = 0; i < Meta.Tasks.Length; i++)
            {
                var t = Meta.Tasks[i];
                float y = 440 - i * 150;
                var r = Ui.Rect(p, "task" + i);
                r.anchoredPosition = new Vector2(0, y);
                r.sizeDelta = new Vector2(860, 136);
                bool done = Meta.TaskDone(S, i), taken = Meta.TaskTaken(S, i);
                Ui.Skin(r, "row", new Vector2(860, 136), Vector2.zero, 1f, taken ? new Color(0.82f, 0.8f, 0.78f) : Color.white);
                ItemTile(r, t.Reward, new Vector2(-350, 0), 104, false);
                Ui.Label(r, t.Name, 34, Pal.Ink, new Vector2(-60, 28), new Vector2(420, 48), TextAnchor.MiddleLeft, Fonts.Heavy);
                Bar(r, new Vector2(-60, -26), new Vector2(400, 30), S.Task[i] / (float)t.Need, Pal.Hex("4FB548"), $"{S.Task[i]}/{t.Need}");
                int idx = i;
                if (taken) Ui.Outlined(r, "已领取", 34, Pal.Hex("8A7A60"), new Vector2(300, 0), new Vector2(200, 60), Pal.Cream, 1f);
                else if (done)
                {
                    var b = Ui.Btn(r, "领取", new Vector2(190, 90), new Vector2(300, 0), Pal.Hex("F2B632"), () =>
                    {
                        if (!Meta.ClaimTask(S, idx)) return;
                        Save();
                        Tasks(layer);
                        ShowRewards(layer, "任务奖励", new List<Item> { Meta.Tasks[idx].Reward });
                    }, 36);
                    Dot(b, new Vector2(88, 40));
                }
                else Ui.Btn(r, "前往", new Vector2(190, 90), new Vector2(300, 0), Pal.Teal, () => { Close(m); Game.I.Home?.Goto(idx); }, 36);
            }
            var bonus = Ui.Rect(p, "bonus");
            bonus.anchoredPosition = new Vector2(0, -480);
            bonus.sizeDelta = new Vector2(860, 170);
            Ui.Skin(bonus, "tab_on", new Vector2(860, 170), Vector2.zero);
            Ui.Icon(bonus, "ic_chest_gold", 150, new Vector2(-340, 6));
            Ui.Label(bonus, "全部完成额外奖励", 32, Pal.Ink, new Vector2(-80, 44), new Vector2(380, 44), TextAnchor.MiddleLeft, Fonts.Heavy);
            for (int i = 0; i < Meta.TaskBonus.Length; i++) ItemTile(bonus, Meta.TaskBonus[i], new Vector2(-170 + i * 120, -26), 90, false);
            bool all = Meta.AllTasksTaken(S), got = (S.TaskClaimed & Meta.BonusBit) != 0;
            if (got) Ui.Outlined(bonus, "已领取", 34, Pal.Hex("8A7A60"), new Vector2(300, 0), new Vector2(200, 60), Pal.Cream, 1f);
            else Ui.Btn(bonus, "领取", new Vector2(190, 90), new Vector2(300, 0), all ? Pal.Hex("F2B632") : Pal.Slate, () =>
            {
                if (!Meta.ClaimBonus(S)) { Ui.Toast("先领完全部任务奖励", 200); return; }
                Save();
                Tasks(layer);
                ShowRewards(layer, "任务宝箱", new List<Item>(Meta.TaskBonus));
            }, 36);
        }

        // ---------------- 巡山（挂机收益） ----------------

        public static void Idle(Transform layer)
        {
            Modal(layer, "idle", "巡山收益", new Vector2(920, 980), out var p, Pal.Hex("3A7A9A"));
            double h = Meta.IdleHours(S, Profile.Now);
            Ui.Icon(p, "tab_idle", 210, new Vector2(0, 290));
            int secs = (int)(h * 3600);
            Ui.Outlined(p, $"已巡山 {secs / 3600}小时{secs % 3600 / 60:00}分", 40, Pal.Ink, new Vector2(0, 150), new Vector2(760, 56), Pal.Cream, 1f);
            Bar(p, new Vector2(0, 92), new Vector2(700, 34), (float)(h / Meta.IdleCapHours), Pal.Hex("4FA8D8"), $"上限 {Meta.IdleCapHours} 小时");
            Ui.Label(p, $"每小时：铜钱 {Meta.IdleCopperPerHour(S)} · 精铁 {Meta.IdleIronPerHour(S):0.#}（随通关提升）", 28, Pal.Hex("6E4A2A"), new Vector2(0, 36), new Vector2(820, 44));
            var r = Meta.IdleRewards(S, h);
            r.Items.RemoveAll(x => x.N <= 0);
            if (r.Items.Count > 0) ItemRow(p, r.Items, new Vector2(0, -100), 140);
            else Ui.Label(p, "收益还在积攒，过一会儿再来", 32, Pal.Hex("7A5432"), new Vector2(0, -100), new Vector2(700, 60));
            Ui.Btn(p, "领取", new Vector2(300, 112), new Vector2(-180, -340), r.Items.Count > 0 ? Pal.Hex("F2B632") : Pal.Slate, () =>
            {
                if (r.Items.Count == 0) { Ui.Toast("还没有收益", 200); return; }
                var got = Meta.CollectIdle(S, Profile.Now);
                Save();
                Idle(layer);
                ShowRewards(layer, "巡山收益", got);
            }, 44);
            int adLeft = Meta.AdIdleMax - S.AdIdle;
            Ui.Btn(p, adLeft > 0 ? $"快速巡山 {adLeft}" : "明日再来", new Vector2(330, 112), new Vector2(180, -340), adLeft > 0 ? Pal.Teal : Pal.Slate, () =>
            {
                if (S.AdIdle >= Meta.AdIdleMax) return;
                Ads.Show(() =>
                {
                    S.AdIdle++;
                    var got = Meta.IdleRewards(S, 2);
                    Meta.Give(S, got);
                    Save();
                    Idle(layer);
                    ShowRewards(layer, "快速巡山（2小时）", got);
                });
            }, 36, "ic_ad");
        }

        // ---------------- 图鉴 ----------------

        public static void Codex(Transform layer, int tab = 0)
        {
            Modal(layer, "codex", "敌军图鉴", new Vector2(980, 1400), out var p, Pal.Hex("5A4A8A"));
            string[] tabs = { "小兵", "精英", "首领" };
            for (int i = 0; i < 3; i++)
            {
                int ti = i;
                int badge = 0;
                foreach (var d in Defs.Enemies) if (Group(d) == i && S.Seen[(int)d.Kind] == 1) badge++;
                var b = Ui.Btn(p, tabs[i], new Vector2(260, 86), new Vector2((i - 1) * 290, 560), i == tab ? Pal.Hex("F2B632") : Pal.Hex("A08A70"), () => Codex(layer, ti), 38);
                if (badge > 0) Dot(b, new Vector2(118, 36), badge);
            }
            var list = new List<EnemyDef>();
            foreach (var d in Defs.Enemies) if (Group(d) == tab) list.Add(d);
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                int st = S.Seen[(int)d.Kind];
                var pos = new Vector2(((i % 3) - 1) * 300, 340 - (i / 3) * 400);
                var c = Ui.Rect(p, "enemy" + i);
                c.anchoredPosition = pos;
                c.sizeDelta = new Vector2(280, 380);
                Card(c, st == 0 ? "card_lock" : d.IsBoss ? "card_orange" : d.Big ? "card_purple" : "card_blue", new Vector2(280, 380), Vector2.zero);
                var art = Ui.Icon(c, d.Art, 200, new Vector2(0, 70));
                if (st == 0)
                {
                    art.color = new Color(0, 0, 0, 0.85f);
                    Ui.Outlined(c, "???", 40, Color.white, new Vector2(0, -80), new Vector2(260, 50), Pal.Ink, 2f);
                    Ui.Label(c, "战斗中遇到后解锁", 24, Pal.Hex("D8D0C0"), new Vector2(0, -140), new Vector2(260, 60));
                    continue;
                }
                Ui.Outlined(c, d.Name, 34, Color.white, new Vector2(0, -44), new Vector2(270, 46), Pal.Ink, 2f);
                Ui.Stroke(Ui.Label(c, d.Desc, 20, Color.white, new Vector2(0, -94), new Vector2(250, 56), TextAnchor.MiddleCenter, Fonts.Heavy), Pal.Ink, 1.2f);
                if (st == 1)
                {
                    var k = d.Kind;
                    var b = Ui.Btn(c, $"领 {CodexRewardText(d)}", new Vector2(210, 54), new Vector2(0, -150), Pal.Hex("F2B632"), () =>
                    {
                        if (!Meta.ClaimCodex(S, k)) return;
                        Save();
                        Codex(layer, tab);
                        Sfx.Play(Snd.Coin);
                        Ui.Toast($"元宝 +{Meta.CodexReward(Defs.Enemy(k))}", 200);
                    }, 28, "ic_ingot");
                    Dot(b, new Vector2(100, 26));
                }
                else Ui.Outlined(c, "已收录", 26, Pal.Hex("B8F0A0"), new Vector2(0, -160), new Vector2(240, 40), Pal.Ink, 1.5f);
            }
        }

        static string CodexRewardText(EnemyDef d) => Meta.CodexReward(d).ToString();
        static int Group(EnemyDef d) => d.IsBoss ? 2 : d.Big ? 1 : 0;

        // ---------------- 设置 ----------------

        public static void Settings(Transform layer)
        {
            Modal(layer, "settings", "设置", new Vector2(820, 860), out var p, Pal.Hex("56677A"));
            Toggle(p, 230, "音效", S.Sound == 1, on => { S.Sound = on ? 1 : 0; Sfx.On = on; Save(); Settings(layer); });
            Toggle(p, 90, "音乐", S.Music == 1, on =>
            {
                S.Music = on ? 1 : 0;
                Sfx.Music = on;
                Save();
                if (on) Sfx.PlayMusic(Bgm.Menu);
                else Sfx.PlayMusic(Bgm.None);
                Settings(layer);
            });
            Ui.Label(p, $"通关 {S.Cleared}/{Defs.LevelCount} · 胜 {S.Wins} / 战 {S.Battles} · 招募 {S.Pulls} 次", 28, Pal.Hex("6E4A2A"), new Vector2(0, -40), new Vector2(740, 44));
            Ui.Btn(p, "重置存档", new Vector2(320, 100), new Vector2(0, -190), Pal.Red, () => Confirm(layer, "确定清空全部进度？此操作不可恢复。", () =>
            {
                Profile.Reset();
                Game.I.ShowHome();
            }), 38);
            Ui.Label(p, "宋江别慌张 v1.0 · 演示包广告为模拟播放", 24, Pal.Hex("8A7A60"), new Vector2(0, -330), new Vector2(740, 40));
        }

        static void Toggle(Transform p, float y, string name, bool on, Action<bool> set)
        {
            var r = Ui.Rect(p, "toggle_" + name);
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = new Vector2(680, 110);
            Ui.Skin(r, "row", new Vector2(680, 110), Vector2.zero);
            Ui.Label(r, name, 40, Pal.Ink, new Vector2(-180, 0), new Vector2(300, 60), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Btn(r, on ? "开" : "关", new Vector2(180, 80), new Vector2(220, 0), on ? Pal.Hex("4FB548") : Pal.Slate, () => set(!on), 38);
        }

        public static void Confirm(Transform layer, string text, Action yes)
        {
            var m = Modal(layer, "confirm", "提示", new Vector2(780, 520), out var p, Pal.Red);
            Ui.Label(p, text, 36, Pal.Ink, new Vector2(0, 50), new Vector2(660, 160));
            Ui.Btn(p, "取消", new Vector2(240, 100), new Vector2(-150, -150), Pal.Hex("A08A70"), () => Close(m), 40);
            Ui.Btn(p, "确定", new Vector2(240, 100), new Vector2(150, -150), Pal.Red, () => { Close(m); yes(); }, 40);
        }

        // ---------------- 日常挑战 / 截取生辰纲 ----------------

        public const int DailyUnlock = 3, EscortUnlock = 6;

        public static void Daily(Transform layer)
        {
            var st = Defs.DailyStage(S.Cleared, Profile.Today);
            var bd = Defs.Enemy(st.BossKind);
            Modal(layer, "daily", "日常挑战", new Vector2(920, 1180), out var p, Pal.Hex("8A3A2C"));
            Ui.Skin(p, "dark", new Vector2(840, 420), new Vector2(0, 300));
            Ui.Box(p, new Vector2(500, 200), new Vector2(0, 160), Pal.Red.A(0.35f), Art.SoftDot);
            Ui.Icon(p, bd.Art, 300, new Vector2(0, 270));
            Ui.Outlined(p, $"今日首领：{bd.Name}", 40, Pal.Hex("FFE08A"), new Vector2(0, 474), new Vector2(800, 56), Pal.Ink, 3f);
            Ui.Label(p, $"强化版第 {Mathf.Min(Defs.LevelCount, S.Cleared + 2)} 关 · 首领血量 ×1.2 · 每天轮换首领", 28, Pal.Hex("6E4A2A"), new Vector2(0, 50), new Vector2(820, 44));
            if (st.BuffRole.HasValue) Ui.Label(p, "本关增益：" + st.BuffText, 28, Pal.GreenDeep, new Vector2(0, 6), new Vector2(820, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(p, "胜利奖励", 30, Pal.Ink, new Vector2(0, -50), new Vector2(400, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            var r = Meta.BattleRewards(S, st, true, st.Waves, 1, 0, new Rng(1));
            ItemRow(p, r.Items, new Vector2(0, -160), 130);
            Ui.Label(p, "失败也按坚持的波数给部分奖励", 24, Pal.Hex("8A6A4A"), new Vector2(0, -300), new Vector2(700, 40));
            int left = Meta.DailyTriesMax - S.DailyTries;
            Ui.Btn(p, left > 0 ? $"挑战（剩 {left} 次）" : "今日已用完", new Vector2(460, 120), new Vector2(0, -420), left > 0 ? Pal.Hex("F2B632") : Pal.Slate, () =>
            {
                if (left <= 0) return;
                Game.I.StartDaily();
            }, 42);
            Ui.Label(p, $"消耗体力 {Meta.BattleStamina}", 24, Pal.Hex("6E4A2A"), new Vector2(0, -500), new Vector2(400, 36));
        }

        public static void Escort(Transform layer)
        {
            var st = Defs.EscortStage(S.Cleared);
            Modal(layer, "escort", "智取生辰纲", new Vector2(920, 1120), out var p, Pal.Hex("B9862C"));
            Ui.Skin(p, "dark", new Vector2(840, 400), new Vector2(0, 290));
            for (int i = 0; i < 3; i++) Ui.Icon(p, "en_porter", 220, new Vector2((i - 1) * 230, 270 - Mathf.Abs(i - 1) * 20));
            Ui.Outlined(p, "梁中书的生辰纲正路过黄泥冈", 36, Pal.Hex("FFE08A"), new Vector2(0, 450), new Vector2(820, 56), Pal.Ink, 3f);
            Ui.Label(p, $"共 {st.Waves} 波挑夫，每波 {st.PorterCount} 担。截下越多，铜钱越多；挑夫不触发宝箱。", 28, Pal.Hex("6E4A2A"), new Vector2(0, 50), new Vector2(820, 80));
            Ui.Label(p, "每担可得", 30, Pal.Ink, new Vector2(0, -40), new Vector2(400, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            var r = Meta.BattleRewards(S, st, true, st.Waves, 1, 1, new Rng(1));
            r.Items.RemoveAll(x => x.N <= 0);
            ItemRow(p, r.Items, new Vector2(0, -140), 120);
            Ui.Label(p, "每 3 担额外得精铁 1", 24, Pal.Hex("8A6A4A"), new Vector2(0, -240), new Vector2(600, 40));
            int left = Meta.EscortTriesMax - S.EscortTries;
            Ui.Btn(p, left > 0 ? $"出发（剩 {left} 次）" : "今日已用完", new Vector2(460, 120), new Vector2(0, -390), left > 0 ? Pal.Hex("F2B632") : Pal.Slate, () =>
            {
                if (left <= 0) return;
                Game.I.StartEscort();
            }, 42);
            Ui.Label(p, $"消耗体力 {Meta.BattleStamina}", 24, Pal.Hex("6E4A2A"), new Vector2(0, -470), new Vector2(400, 36));
        }

        // ---------------- 章节宝箱 ----------------

        public static void Chest(Transform layer, int chapter, int k)
        {
            if (Meta.CanOpenChest(S, chapter, k))
            {
                var r = Meta.OpenChest(S, chapter, k);
                Save();
                ShowRewards(layer, $"{Meta.ChestStars[k]} 星宝箱", r);
                return;
            }
            var m = Modal(layer, "chest", $"{Meta.ChestStars[k]} 星宝箱", new Vector2(820, 660), out var p);
            Ui.Icon(p, k == 0 ? "ic_chest_wood" : k == 1 ? "ic_chest_silver" : "ic_chest_gold", 200, new Vector2(0, 180));
            bool got = (S.ChestClaimed[chapter - 1] & (1 << k)) != 0;
            Ui.Label(p, got ? "已领取" : $"本章累计 {Meta.ChestStars[k]} 星可领（当前 {Meta.ChapterStars(S, chapter)} 星）", 30, Pal.Ink, new Vector2(0, 40), new Vector2(740, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            ItemRow(p, Meta.ChestReward(chapter, k).Items, new Vector2(0, -90), 120);
            Ui.Btn(p, "知道了", new Vector2(260, 100), new Vector2(0, -240), Pal.Hex("A08A70"), () => Close(m), 40);
        }

        // ---------------- 招募结果 ----------------

        /// <summary>招募结果：卡背朝上依次翻开，史诗、传说的新好汉翻完后逐个全屏揭晓。</summary>
        public static void RecruitResult(Transform layer, List<Pull> pulls)
        {
            int n = pulls.Count;
            RectTransform root = null;
            root = MetaFx.Overlay(layer, "pulls", 0.8f, null);
            MetaFx.Band(root, 60, n > 1 ? 820 : 560);
            MetaFx.GoldBanner(root, n > 1 ? "十连招募" : "招募结果", new Vector2(0, n > 1 ? 560 : 430), 680);
            Sfx.Play(Snd.Drum, 0.9f);
            var reveal = new List<HeroId>();
            float step = n > 1 ? 0.16f : 0.1f, t0 = 0.45f;
            for (int i = 0; i < n; i++)
            {
                var pl = pulls[i];
                var d = Defs.Hero(pl.Hero);
                if (pl.New && d.Rarity >= 3) reveal.Add(pl.Hero);
                var pos = n == 1 ? new Vector2(0, 80) : new Vector2(((i % 5) - 2) * 196, 230 - (i / 5) * 360);
                float w = n == 1 ? 300 : 180, h = n == 1 ? 400 : 290;
                var c = Ui.Rect(root, "pull" + i);
                c.anchoredPosition = pos;
                c.sizeDelta = new Vector2(w, h);
                var glow = Ui.Box(c, new Vector2(w * 1.6f, h * 1.35f), Vector2.zero, RarityColor(d.Rarity).A(0), Art.SoftDot, "glow");
                var front = Ui.Rect(c, "front");
                front.sizeDelta = c.sizeDelta;
                Card(front, RarityCard(d.Rarity), new Vector2(w, h), Vector2.zero);
                Ui.Icon(front, d.Art, w * 0.9f, new Vector2(0, h * 0.1f));
                Ui.Outlined(front, d.Name, (int)(w * 0.17f), Color.white, new Vector2(0, -h * 0.3f), new Vector2(w, h * 0.16f), Pal.Ink, 2f);
                if (pl.New) Badge(front, "新!", Pal.Red, new Vector2(w * 0.42f, h * 0.13f), new Vector2(w * 0.32f, h * 0.45f), (int)(w * 0.14f));
                Ui.Outlined(front, pl.New ? RarityName(d.Rarity) : $"碎片 +{pl.Shards}", (int)(w * 0.13f), pl.New ? RarityColor(d.Rarity).Mul(1.5f) : Pal.Cream,
                    new Vector2(0, -h * 0.42f), new Vector2(w, h * 0.12f), Pal.Ink, 1.5f);
                front.localScale = new Vector3(0, 1, 1);
                var back = Ui.Rect(c, "back");
                back.sizeDelta = c.sizeDelta;
                Card(back, "card_lock", new Vector2(w, h), Vector2.zero);
                Ui.Icon(back, "prop_drum", w * 0.62f, new Vector2(0, h * 0.06f));
                Tween.Pop(c, 0.22f, 0.15f + i * 0.04f);
                int ri = d.Rarity;
                float at = t0 + i * step;
                Tween.Run(back, 0.1f, k => back.localScale = new Vector3(1 - k, 1, 1), () =>
                {
                    Sfx.Play(ri >= 4 ? Snd.Unlock : Snd.Flip, ri >= 4 ? 0.9f : 0.7f, ri >= 3 ? 1.1f : 1f);
                    Tween.Run(front, 0.14f, k => front.localScale = new Vector3(Tween.OutBack(k), 1, 1));
                    if (ri >= 3)
                    {
                        glow.color = RarityColor(ri).A(0.6f);
                        MetaFx.Burst(root, pos, ri >= 4 ? 14 : 8, 200, Color.Lerp(RarityColor(ri), Color.white, 0.4f));
                        Tween.Run(glow, 999f, k => glow.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * 999f * 4f)));
                    }
                }, at);
            }
            float end = t0 + n * step + 0.3f;
            var btns = Ui.Rect(root, "btns");
            btns.anchoredPosition = new Vector2(0, n > 1 ? -460 : -330);
            var cgb = btns.gameObject.AddComponent<CanvasGroup>();
            cgb.alpha = 0;
            cgb.blocksRaycasts = false;
            Ui.Btn(btns, "确定", new Vector2(300, 112), new Vector2(-180, 0), Pal.Hex("F2B632"), () => MetaFx.Dismiss(root), 44);
            Ui.Btn(btns, n > 1 ? "再十连" : "再招一次", new Vector2(300, 112), new Vector2(180, 0), Pal.Hex("E8862A"), () =>
            {
                MetaFx.Dismiss(root);
                Game.I.Home?.DoRecruit(n, false);
            }, 40, "ic_token");
            Tween.Run(root, end, k => { }, () =>
            {
                MetaFx.RevealAll(layer, reveal, () =>
                {
                    if (!btns) return;
                    cgb.blocksRaycasts = true;
                    Tween.Fade(cgb, 0, 1, 0.2f);
                });
            });
        }

        // ---------------- 好汉详情 ----------------

        static string SkillIcon(HeroDef d, int i) => i == 0 ? d.AtkIcon : i == 1 ? d.UltIcon : "ic_star";

        /// <summary>职业小图标。</summary>
        public static string RoleArt(Role r) =>
            r == Role.Archer ? "ic_arrow" : r == Role.Bomber ? "ic_bomb" : r == Role.Mage ? "fx_zap" : r == Role.Fighter ? "ic_fist" : r == Role.Support ? "ic_heart" : "ic_hand";

        /// <summary>里程碑说明：达成的把关键字标橙。</summary>
        static string HighlightTalent(HeroId h, int i, bool on)
        {
            string t = Meta.Talent(h, i);
            if (!on) return t;
            t = System.Text.RegularExpressions.Regex.Replace(t, "「(.*?)」", m => HeroText.O("[" + m.Groups[1].Value + "]"));
            return System.Text.RegularExpressions.Regex.Replace(t, @"([+-]\d+%)", m => HeroText.O(m.Groups[1].Value));
        }

        static void StatBox(Transform p, Vector2 pos, string icon, string label, string value, string sub, Color? iconBg = null)
        {
            var r = Ui.Rect(p, "stat_" + label);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(430, 120);
            Ui.Skin(r, "row", new Vector2(430, 120), Vector2.zero, 0.6f, Pal.Hex("F4ECDA"));
            Slot(r, "slot_wood", 96, new Vector2(-152, 0));
            if (iconBg.HasValue) Ui.Box(r, Vector2.one * 74, new Vector2(-152, 0), iconBg.Value, Art.Circle, "roleBg");
            Ui.Icon(r, icon, 70, new Vector2(-152, 2));
            Ui.Label(r, label, 26, Pal.Hex("8A6A4A"), new Vector2(10, 28), new Vector2(200, 36), TextAnchor.MiddleLeft, Fonts.Heavy);
            Ui.Label(r, value, 38, Pal.Ink, new Vector2(10, -16), new Vector2(200, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
            if (sub != null)
                Ui.Label(r, sub, sub.StartsWith("+") ? 30 : 22, sub.StartsWith("+") ? Pal.GreenDeep : Pal.Hex("8A6A4A"), new Vector2(150, -16), new Vector2(150, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
        }

        static Image CostTile(Transform p, Vector2 pos, string skin, string icon, string text, bool ok, bool shard)
        {
            var r = Ui.Rect(p, "cost");
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(140, 170);
            var face = Slot(r, skin, 130, new Vector2(0, 16));
            Ui.Icon(r, icon, 104, new Vector2(0, 20));
            if (shard) Ui.Icon(r, "ic_shard", 48, new Vector2(-42, 58));
            Ui.Outlined(r, text, 28, ok ? Color.white : Pal.Hex("FF7A6A"), new Vector2(0, -66), new Vector2(200, 40), Pal.Ink, 2f);
            if (!ok)
            {
                var plus = Ui.Icon(r, "ui/dot", 40, new Vector2(50, 66), "plus");
                plus.color = Pal.Green;
                Ui.Outlined(plus.transform, "+", 30, Color.white, new Vector2(0, 2), new Vector2(40, 40), Pal.GreenDeep, 1.5f);
            }
            return face;
        }

        /// <summary>参考视频的好汉详情：头像压在框顶，伤害/职业两格，三个六边形技能（点选看说明），等级里程碑，底部碎片、铜钱和大号升级键。</summary>
        public static void HeroDetail(Transform layer, HeroId h, int skill = 0)
        {
            var d = Defs.Hero(h);
            int lv = S.HeroLv[(int)h];
            bool owned = lv > 0;
            int ulv = Mathf.Max(1, lv);
            var m = Modal(layer, "hero", null, new Vector2(980, 1440), out var p, null, -70);
            var rc = RarityColor(d.Rarity);

            // 抬头：稀有度光晕 + 头像压在框顶
            Ui.Box(p, new Vector2(940, 320), new Vector2(0, 620), rc.A(0.4f), Art.SoftDot, "headGlow");
            var medal = Ui.Icon(p, "ui/medal", 250, new Vector2(0, 716), "medal");
            medal.color = Color.Lerp(rc, Color.white, 0.3f);
            var art = Ui.Icon(p, d.Art, 300, new Vector2(0, 745), "art");
            if (!owned) art.color = new Color(0.3f, 0.3f, 0.34f);
            Tween.Run(art, 999f, k => art.rectTransform.anchoredPosition = new Vector2(0, 745 + 5 * Mathf.Sin(k * 999f * 2f)));

            var lvc = Ui.Icon(p, "ui/lv", 86, new Vector2(-150, 562), "lv");
            Ui.Outlined(lvc.transform, owned ? lv.ToString() : "-", 40, Pal.Hex("FFE7B0"), new Vector2(0, 2), new Vector2(86, 60), Pal.Ink, 2f);
            Ui.Outlined(p, d.Name, 54, Color.white, new Vector2(10, 564), new Vector2(240, 70), Pal.Ink, 3f);
            Badge(p, d.RarityName, rc, new Vector2(110, 50), new Vector2(176, 564), 26);
            Ui.Label(p, $"「{d.Nick}」{d.Desc}", 26, Pal.Hex("6E4A2A"), new Vector2(0, 506), new Vector2(880, 40), TextAnchor.MiddleCenter, Fonts.Heavy);

            int slot = Array.IndexOf(S.Lineup, (int)h);
            if (owned)
            {
                if (slot >= 0)
                {
                    var tag = Ui.Icon(p, "ui/tag", 150, new Vector2(-372, 610), "onTag");
                    Ui.Outlined(tag.transform, "出战中", 28, Color.white, new Vector2(-8, 3), new Vector2(130, 60), Pal.RedDeep, 1.5f);
                }
                else
                {
                    var b = Ui.Btn(p, "上阵", new Vector2(176, 84), new Vector2(-370, 610), Pal.Teal, () => { Close(m); BeginSwap(layer, h); }, 36);
                    Tween.Run(b, 999f, k => b.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(k * 999f * 5f)));
                }
            }

            int pw = Meta.HeroPower(S, h);
            int next = owned && lv < Meta.MaxHeroLv ? Meta.HeroPowerAt(h, lv + 1) - pw : 0;
            StatBox(p, new Vector2(-226, 400), "ic_sword", "伤害", pw.ToString(), next > 0 ? $"+{next}" : null);
            StatBox(p, new Vector2(226, 400), RoleArt(d.Kind), "职业", d.Role, HeroText.Tip(d).Split('：')[0], RoleColor(d.Kind));

            string[] tags = { "普攻", "大招", "被动" };
            string[] names = { d.AtkName, d.UltName, d.PassiveName };
            for (int i = 0; i < 3; i++)
            {
                bool locked = i == 2 && ulv < 5;
                float x = (i - 1) * 240;
                if (i == skill)
                {
                    var sel = Ui.Box(p, Vector2.one * 230, new Vector2(x, 240), Pal.Gold.A(0.6f), Art.SoftDot, "sel");
                    Tween.Run(sel, 999f, k => sel.color = Pal.Gold.A(0.45f + 0.2f * Mathf.Sin(k * 999f * 4f)));
                }
                var hx = MetaFx.Hex(p, SkillIcon(d, i), 128, new Vector2(x, 244), locked, d.Rarity);
                Ui.Outlined(p, $"{tags[i]}·{names[i]}", 26, i == skill ? Pal.Hex("FFE08A") : Color.white, new Vector2(x, 152), new Vector2(236, 40), Pal.Ink, 2f);
                int si = i;
                var hit = Ui.Box(hx, new Vector2(170, 180), Vector2.zero, new Color(0, 0, 0, 0.001f), Art.White, "hit");
                Ui.Tap(hit, () => { if (si != skill) HeroDetail(layer, h, si); }, hx);
            }
            string desc = skill == 0 ? $"每 {HeroText.O(d.Interval.ToString("0.##") + "秒")} 出手：" + HeroText.Atk(d)
                : skill == 1 ? $"冷却 {HeroText.O((d.UltCd * (ulv >= 15 ? 0.85f : 1f)).ToString("0.#") + "秒")}：" + HeroText.Ult(d, ulv >= 25 ? 1.5f : 1f)
                : HeroText.Passive(d) + (ulv >= 5 ? "" : $"（{HeroText.O("5级")}解锁）");
            Ui.Skin(p, "row", new Vector2(900, 128), new Vector2(0, 66), 0.6f, Pal.Hex("FFF6E2"));
            Ui.Label(p, desc.Replace("\n", " "), 25, Pal.Ink, new Vector2(0, 66), new Vector2(860, 120), TextAnchor.MiddleLeft);

            for (int i = 1; i < Meta.UnlockLv.Length; i++)
            {
                int need = Meta.UnlockLv[i];
                bool on = lv >= need;
                float y = -30 - (i - 1) * 64;
                Ui.Skin(p, "row", new Vector2(900, 58), new Vector2(0, y), 0.5f, on ? Pal.Hex("FFF1C0") : Pal.Hex("D6CFC3"));
                Badge(p, $"Lv.{need}", on ? Pal.Hex("E8862A") : Pal.Hex("8A8478"), new Vector2(110, 44), new Vector2(-380, y), 24);
                Ui.Label(p, HighlightTalent(h, i, on), 26, on ? Pal.Ink : Pal.Hex("7A6E60"), new Vector2(40, y), new Vector2(700, 54), TextAnchor.MiddleLeft, Fonts.Heavy);
                var mark = Ui.Icon(p, on ? "ic_thumb" : "ic_lock", 44, new Vector2(410, y));
                if (!on) mark.color = new Color(1, 1, 1, 0.75f);
            }

            float sy = -420;
            Ui.Box(p, new Vector2(900, 54), new Vector2(0, sy), Pal.Hex("8CD65A").A(0.35f), Art.Rounded, "bonus");
            Ui.Label(p, $"升级奖励：全军暴击伤害 {HeroText.O("+1%")}（当前 +{Meta.LevelCritDmg(S) * 100:0}%）", 26, Pal.GreenDeep, new Vector2(0, sy), new Vector2(880, 50), TextAnchor.MiddleCenter, Fonts.Heavy);

            float by = -566;
            if (!owned)
            {
                int at = Meta.UnlockLevelOf(h);
                string how = at > 0 ? $"通关第 {HeroText.O(at)} 关入伙，或擂鼓招募获得"
                    : "擂鼓招募获得" + (d.Rarity >= 4 && !Meta.OrangeOpened(S) ? $"（通关第 {Meta.OrangeOpen} 关后进入招募池）" : "");
                Ui.Label(p, how, 30, Pal.Ink, new Vector2(0, by + 56), new Vector2(880, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
                Ui.Btn(p, "获取途径", new Vector2(320, 110), new Vector2(0, by - 36), Pal.Hex("F2B632"), () => GetPaths(layer, h), 40);
                return;
            }
            if (lv >= Meta.MaxHeroLv)
            {
                Ui.Outlined(p, "已满级", 56, Pal.Hex("FFE08A"), new Vector2(0, by), new Vector2(400, 80), Pal.Ink, 3f);
                return;
            }
            int sc = Meta.ShardCost(lv), cc = Meta.CopperCost(lv);
            bool shardOk = S.Shards[(int)h] >= sc, copperOk = S.Copper >= cc;
            var st = CostTile(p, new Vector2(-370, by + 10), RaritySlot(d.Rarity), d.Art, $"{S.Shards[(int)h]}/{sc}", shardOk, true);
            Ui.Tap(st, () => GetPaths(layer, h), st.transform.parent);
            var ct = CostTile(p, new Vector2(-200, by + 10), "slot_wood", "ic_coins", $"{Ui.Num(S.Copper)}/{Ui.Num(cc)}", copperOk, false);
            Ui.Tap(ct, () => GetPaths(layer, null), ct.transform.parent);
            bool can = shardOk && copperOk;
            int sk = skill;
            var up = Ui.Btn(p, "升级", new Vector2(390, 144), new Vector2(200, by + 10), can ? Pal.Hex("F2B632") : Pal.Hex("B8AE98"), () => DoLevelUp(layer, h, sk), 58);
            var arrow = Ui.Icon(up, "ic_fast", 66, new Vector2(156, 42), "upArrow");
            arrow.transform.localRotation = Quaternion.Euler(0, 0, 90);
            if (can)
            {
                arrow.color = Pal.Hex("7CFF5A");
                Tween.Run(arrow, 999f, k => arrow.rectTransform.anchoredPosition = new Vector2(156, 42 + 6 * Mathf.Sin(k * 999f * 6f)));
                var glow = Ui.Box(up, new Vector2(540, 250), Vector2.zero, Pal.Gold.A(0.35f), Art.SoftDot, "glow");
                glow.transform.SetAsFirstSibling();
                Tween.Run(glow, 999f, k => glow.color = Pal.Gold.A(0.2f + 0.15f * Mathf.Sin(k * 999f * 4f)));
            }
            else arrow.color = new Color(0.62f, 0.62f, 0.62f);
        }

        static void DoLevelUp(Transform layer, HeroId h, int skill)
        {
            int lv = S.HeroLv[(int)h];
            float oldPw = Meta.HeroPower(S, h), oldCd = Meta.LevelCritDmg(S);
            if (!Meta.LevelUp(S, h))
            {
                Sfx.Play(Snd.Deny);
                bool shard = S.Shards[(int)h] < Meta.ShardCost(lv);
                Ui.Toast(shard ? "英雄碎片不足" : "铜钱不足", 200);
                GetPaths(layer, shard ? h : (HeroId?)null);
                return;
            }
            Save();
            HeroDetail(layer, h, skill);
            MetaFx.LevelUp(layer, new MetaFx.LevelUpInfo
            {
                Hero = h, NewLv = S.HeroLv[(int)h], OldPower = oldPw, NewPower = Meta.HeroPower(S, h),
                OldCritDmg = oldCd, NewCritDmg = Meta.LevelCritDmg(S),
            });
        }

        /// <summary>好汉页在前台就进页内换人模式（顶上四位头顶出箭头），否则弹选择框。</summary>
        static void BeginSwap(Transform layer, HeroId h)
        {
            var home = Game.I.Home;
            if (home != null && home.Tab == HomeScreen.TabHero) home.BeginSwap(h);
            else PickSwap(layer, h);
        }

        /// <summary>获取途径：hero 为空时是铜钱。</summary>
        public static void GetPaths(Transform layer, HeroId? hero)
        {
            var m = Modal(layer, "paths", "获取途径", new Vector2(860, hero.HasValue ? 860 : 900), out var p, Pal.Hex("3A6A8A"));
            float top = (hero.HasValue ? 860 : 900) * 0.5f;
            var head = Ui.Rect(p, "head");
            head.anchoredPosition = new Vector2(0, top - 150);
            head.sizeDelta = new Vector2(780, 170);
            Ui.Skin(head, "row", new Vector2(780, 170), Vector2.zero, 0.7f, Pal.Hex("F4ECDA"));
            if (hero.HasValue)
            {
                var d = Defs.Hero(hero.Value);
                Slot(head, RaritySlot(d.Rarity), 132, new Vector2(-300, 0));
                Ui.Icon(head, d.Art, 120, new Vector2(-300, 4));
                Ui.Icon(head, "ic_shard", 50, new Vector2(-344, 44));
                Ui.Label(head, d.Name + "碎片", 36, Pal.Ink, new Vector2(-50, 40), new Vector2(360, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
                Ui.Label(head, $"拥有数量：{HeroText.O(S.Shards[(int)hero.Value])}", 26, Pal.Hex("8A6A4A"), new Vector2(240, 40), new Vector2(260, 40), TextAnchor.MiddleRight, Fonts.Heavy);
                Ui.Label(head, $"用于升级{d.Name}，重复招募到会自动折成碎片", 24, Pal.Hex("6E4A2A"), new Vector2(60, -30), new Vector2(580, 70), TextAnchor.MiddleLeft);
            }
            else
            {
                Slot(head, "slot_wood", 132, new Vector2(-300, 0));
                Ui.Icon(head, "ic_coins", 112, new Vector2(-300, 4));
                Ui.Label(head, "铜钱", 36, Pal.Ink, new Vector2(-50, 40), new Vector2(360, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
                Ui.Label(head, $"拥有数量：{HeroText.O(Ui.Num(S.Copper))}", 26, Pal.Hex("8A6A4A"), new Vector2(240, 40), new Vector2(260, 40), TextAnchor.MiddleRight, Fonts.Heavy);
                Ui.Label(head, "好汉升级、装备强化、点亮天赋都要用到", 24, Pal.Hex("6E4A2A"), new Vector2(60, -30), new Vector2(580, 70), TextAnchor.MiddleLeft);
            }
            var rows = new List<(string icon, string name, string sub, Action go)>();
            var home = Game.I.Home;
            void Tab(int t) { Close(m); Close(layer.Find("modal_hero") as RectTransform); home?.SetTab(t); }
            if (hero.HasValue)
            {
                var d = Defs.Hero(hero.Value);
                int at = Meta.UnlockLevelOf(hero.Value);
                rows.Add(("prop_drum", "擂鼓招募", "招到重复好汉折成碎片", () => Tab(HomeScreen.TabShop)));
                rows.Add(("tab_battle", "主线出征", Meta.Owned(S, hero.Value) ? "胜利后随机给出战好汉碎片" : at > 0 ? $"首通第 {at} 关直接入伙" : "胜利奖励招募令", () => Tab(HomeScreen.TabBattle)));
                if (d.Rarity <= 3) rows.Add(("ic_chest_gold", "章节宝箱", "集星开宝箱得招募令", () => Tab(HomeScreen.TabBattle)));
            }
            else
            {
                rows.Add(("tab_idle", "巡山收益", "离线也在攒铜钱", () => { Close(m); Idle(layer); }));
                rows.Add(("tab_battle", "主线出征", "每次胜利都有铜钱", () => Tab(HomeScreen.TabBattle)));
                rows.Add(("tab_shop", "梁山集市", "元宝换一袋铜钱", () => Tab(HomeScreen.TabShop)));
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                Row(p, top - 340 - i * 150, row.icon, row.name, row.sub, "前往", Pal.Hex("4FB548"), row.go, 780);
            }
        }

        /// <summary>选一个已上阵的好汉换下（不在好汉页时用）。</summary>
        public static void PickSwap(Transform layer, HeroId h)
        {
            var m = Modal(layer, "swap", "替换谁？", new Vector2(960, 560), out var p);
            for (int i = 0; i < 4; i++)
            {
                var od = Defs.Hero((HeroId)S.Lineup[i]);
                var c = Ui.Rect(p, "s" + i);
                c.anchoredPosition = new Vector2((i - 1.5f) * 220, 20);
                c.sizeDelta = new Vector2(200, 260);
                var face = Card(c, RarityCard(od.Rarity), new Vector2(200, 260), Vector2.zero);
                Ui.Icon(c, od.Art, 190, new Vector2(0, 20));
                Ui.Outlined(c, od.Name, 32, Color.white, new Vector2(0, -100), new Vector2(200, 44), Pal.Ink, 2f);
                int idx = i;
                Ui.Tap(face, () =>
                {
                    Meta.SetLineup(S, idx, h);
                    Save();
                    Sfx.Play(Snd.Swap);
                    Close(m);
                    HeroDetail(layer, h);
                    Ui.Toast($"{Defs.Hero(h).Name} 上阵", 200);
                }, c);
            }
            Ui.Label(p, "上阵的四位好汉会在战斗中被召唤", 28, Pal.Hex("6E4A2A"), new Vector2(0, -200), new Vector2(800, 44));
        }

        // ---------------- 锻造 ----------------

        public static void ForgeSlot(Transform layer, int slot)
        {
            var e = Meta.Equips[slot];
            int lv = S.EquipLv[slot], max = Meta.EquipMax(S);
            Modal(layer, "forge", "强化 · " + e.Name, new Vector2(940, 1080), out var p, Pal.Hex("8A5A32"));
            Ui.Skin(p, "dark", new Vector2(892, 412), new Vector2(0, 280));
            var scene = Ui.Box(p, new Vector2(860, 380), new Vector2(0, 280), Color.white, Art.Img("forge_scene"));
            scene.type = Image.Type.Simple;
            scene.preserveAspect = false;
            Ui.Box(p, new Vector2(860, 380), new Vector2(0, 280), new Color(0, 0, 0, 0.1f), Art.White);
            Ui.Box(p, new Vector2(240, 240), new Vector2(0, 280), Pal.Gold.A(0.5f), Art.SoftDot);
            var ic = Ui.Icon(p, e.Icon, 220, new Vector2(0, 290));
            Ui.Outlined(p, $"Lv.{lv}/{max}", 44, Color.white, new Vector2(0, 130), new Vector2(400, 60), Pal.Ink, 3f);
            Ui.Label(p, "当前", 28, Pal.Hex("7A5432"), new Vector2(-220, 50), new Vector2(300, 40));
            Ui.Label(p, Meta.EquipText(slot, lv), 32, Pal.Ink, new Vector2(-220, 6), new Vector2(380, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Icon(p, "ic_fast", 60, new Vector2(0, 26));
            Ui.Label(p, "下一级", 28, Pal.Hex("7A5432"), new Vector2(220, 50), new Vector2(300, 40));
            Ui.Label(p, lv < max ? Meta.EquipText(slot, lv + 1) : "—", 32, Pal.GreenDeep, new Vector2(220, 6), new Vector2(380, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(p, $"每 10 级突破：{Defs.RoleName(e.Role)}伤害 +4%，之后每级加成提高一成（当前 {Meta.BreakText(slot, lv)}）", 24, Pal.Hex("8A6A4A"), new Vector2(0, -60), new Vector2(880, 40));
            if (lv >= max)
            {
                Ui.Label(p, lv >= 40 ? "已强化到顶" : "已到当前上限：继续通关提高强化上限", 32, Pal.Ink, new Vector2(0, -200), new Vector2(800, 60), TextAnchor.MiddleCenter, Fonts.Heavy);
                return;
            }
            int ic2 = Meta.IronCost(lv), cc = Meta.EquipCopper(lv);
            Ui.Icon(p, "ic_iron", 70, new Vector2(-360, -150));
            Ui.Outlined(p, $"{S.Iron}/{ic2}", 34, S.Iron >= ic2 ? Color.white : Pal.Hex("FF8070"), new Vector2(-220, -150), new Vector2(240, 50), Pal.Ink, 2f);
            Ui.Icon(p, "ic_coins", 70, new Vector2(-360, -230));
            Ui.Outlined(p, $"{Ui.Num(S.Copper)}/{Ui.Num(cc)}", 34, S.Copper >= cc ? Color.white : Pal.Hex("FF8070"), new Vector2(-220, -230), new Vector2(240, 50), Pal.Ink, 2f);
            bool can = Meta.CanForge(S, slot);
            Ui.Btn(p, "强化", new Vector2(300, 130), new Vector2(230, -190), can ? Pal.Hex("E8862A") : Pal.Slate, () =>
            {
                float before = Meta.TeamPower(S);
                if (!Meta.Forge(S, slot))
                {
                    Sfx.Play(Snd.Deny);
                    bool iron = S.Iron < Meta.IronCost(S.EquipLv[slot]);
                    Ui.Toast(iron ? "精铁不足：通关、巡山、生辰纲获得" : "铜钱不足", 200);
                    if (!iron) GetPaths(layer, null);
                    return;
                }
                Save();
                Sfx.Play(Snd.Forge, 0.9f);
                ForgeSlot(layer, slot);
                var fp = layer.Find("modal_forge/panel");
                if (fp)
                {
                    MetaFx.Burst(fp, new Vector2(0, 290), 12, 260);
                    if (Meta.Breaks(S.EquipLv[slot]) > Meta.Breaks(S.EquipLv[slot] - 1)) Sfx.Play(Snd.Unlock, 0.8f);
                }
                MetaFx.StatUp(layer, "战力", before, Meta.TeamPower(S));
            }, 50);
            Tween.Run(ic, 999f, k => ic.transform.localRotation = Quaternion.Euler(0, 0, 4f * Mathf.Sin(k * 999f * 2f)));
        }

        // ---------------- 寨主天赋 ----------------

        static string TalentIcon(int i)
        {
            if (i % 10 == 0) return "ic_crown";
            var k = Meta.TalentKind(i);
            return k == Meta.TalentStat.CritDmg ? "ic_rage" : k == Meta.TalentStat.Crit ? "ic_sword" : k == Meta.TalentStat.Wall ? "ic_wall" : k == Meta.TalentStat.Coins ? "ic_silver" : "ic_fast";
        }

        /// <summary>参考视频：竖向一串六边形节点，点亮的部分像水位一样往上涨；点下一个节点直接激活，每个节点加攻击力和一项副属性，上限跟主线通关走。</summary>
        public static void TalentTree(Transform layer, Action changed = null, int riseFrom = -1)
        {
            float ph = Mathf.Min(1640, Ui.Height - Ui.SafeTop - 260);
            var m = Modal(layer, "talent", "寨主天赋", new Vector2(1000, ph), out var p, Pal.Hex("3A6A8A"));
            int have = S.Talent, cap = Meta.TalentCap(S);
            Meta.TalentSum(have, out var atk, out var crit, out var cd, out var wall, out var coins, out var spd);
            float top = ph * 0.5f;
            Ui.Skin(p, "dark", new Vector2(920, 190), new Vector2(0, top - 150), 1f, Pal.Hex("6A8AA8"));
            Ui.Outlined(p, $"已点亮 {have}/{Meta.TalentMax}", 40, Pal.Hex("FFE08A"), new Vector2(0, top - 96), new Vector2(800, 56), Pal.Ink, 2.5f);
            Ui.Label(p, $"攻击力 {HeroText.O("+" + atk.ToString("0"))}   暴击率 {HeroText.O("+" + (crit * 100).ToString("0.#") + "%")}   暴击伤害 {HeroText.O("+" + (cd * 100).ToString("0") + "%")}\n寨墙生命 {HeroText.O("+" + (wall * 100).ToString("0.#") + "%")}   攻速 {HeroText.O("+" + (spd * 100).ToString("0.#") + "%")}   开局银两 {HeroText.O("+" + coins.ToString("0"))}",
                28, Pal.Cream, new Vector2(0, top - 176), new Vector2(880, 100), TextAnchor.MiddleCenter, Fonts.Heavy);

            float viewH = ph - 470;
            var view = Ui.Rect(p, "view");
            view.sizeDelta = new Vector2(920, viewH);
            view.anchoredPosition = new Vector2(0, top - 250 - viewH * 0.5f);
            view.gameObject.AddComponent<RectMask2D>();
            Ui.Box(view, new Vector2(920, viewH), Vector2.zero, Pal.Hex("16324A"), Art.Rounded, "deep");
            var content = Ui.Rect(view, "content");
            const float step = 150;
            int show = Math.Min(Meta.TalentMax, Math.Max(cap, have) + 4);
            float H = show * step + 120;
            content.sizeDelta = new Vector2(920, H);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var top0 = Ui.Rect(content, "top");
            top0.anchorMin = top0.anchorMax = new Vector2(0.5f, 1f);
            top0.sizeDelta = Vector2.zero;
            float NodeY(int i) => -(70 + (show - i) * step);

            // 水位：从底部涨到当前点亮的节点
            var water = Ui.Box(top0, new Vector2(920, 10), Vector2.zero, Pal.Hex("3FA8C8"), Art.White, "water");
            water.rectTransform.pivot = new Vector2(0.5f, 0f);
            water.rectTransform.anchoredPosition = new Vector2(0, -H);
            var surf = Ui.Box(top0, new Vector2(920, 10), Vector2.zero, Pal.Hex("BFF4FF").A(0.85f), Art.White, "surface");
            float Level(int n) => n <= 0 ? 0 : H + NodeY(n) + step * 0.5f;
            void SetWater(float h)
            {
                water.rectTransform.sizeDelta = new Vector2(920, h);
                surf.rectTransform.anchoredPosition = new Vector2(0, -H + h);
            }
            SetWater(Level(riseFrom >= 0 ? riseFrom : have));
            if (riseFrom >= 0 && riseFrom != have)
            {
                float wFrom = Level(riseFrom), wTo = Level(have);
                Tween.Run(water, 0.6f, k => SetWater(Mathf.Lerp(wFrom, wTo, Tween.OutCubic(k))), null, 0.1f);
            }
            for (int i = 0; i < 6; i++)
            {
                var bub = Ui.Box(water.transform, Vector2.one * (10 + i * 3), Vector2.zero, Color.white.A(0.25f), Art.Circle, "bubble");
                bub.rectTransform.anchorMin = bub.rectTransform.anchorMax = new Vector2(0.1f + i * 0.16f, 0);
                float off = i * 0.37f;
                Tween.Run(bub, 999f, k =>
                {
                    float t = (k * 999f * 0.25f + off) % 1f;
                    bub.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 12 + off * 9) * 12, t * Mathf.Max(0, water.rectTransform.sizeDelta.y - 10));
                    bub.color = Color.white.A(0.3f * (1 - t));
                });
            }

            const float cx = -230;
            for (int i = 1; i <= show; i++)
            {
                float y = NodeY(i);
                bool on = i <= have, next = i == have + 1 && i <= cap, locked = i > cap;
                if (i < show)
                {
                    var line = Ui.Box(top0, new Vector2(16, step), new Vector2(cx, y + step * 0.5f), i < have ? Pal.Green : Pal.Hex("3C4E62"), Art.White, "line");
                    line.transform.SetSiblingIndex(2);
                }
                bool big = i % 10 == 0;
                if (next)
                {
                    var glow = Ui.Box(top0, Vector2.one * 220, new Vector2(cx, y), Pal.Gold.A(0.55f), Art.SoftDot, "glow");
                    Tween.Run(glow, 999f, k => glow.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(k * 999f * 4f)));
                }
                var hx = MetaFx.Hex(top0, TalentIcon(i), big ? 130 : 104, new Vector2(cx, y), locked, big ? 4 : 2);
                if (!on && !locked)
                {
                    hx.Find("frame").GetComponent<Image>().color = next ? Color.white : new Color(0.62f, 0.66f, 0.74f);
                    hx.Find("icon").GetComponent<Image>().color = next ? Color.white : new Color(0.7f, 0.72f, 0.78f);
                }
                if (next)
                {
                    var up = Ui.Icon(hx, "ic_fast", 52, new Vector2(56, 52), "up");
                    up.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    up.color = Pal.Hex("7CFF5A");
                    Tween.Run(up, 999f, k => up.rectTransform.anchoredPosition = new Vector2(56, 52 + 6 * Mathf.Sin(k * 999f * 6f)));
                }
                var col = on ? Pal.Hex("FFE08A") : next ? Color.white : locked ? Pal.Hex("6A7A8C") : Pal.Hex("A8B8CC");
                Ui.Outlined(top0, $"{i}. {Meta.TalentText(i)}", big ? 30 : 27, col, new Vector2(cx + 100 + 280, y + (next ? 18 : 0)), new Vector2(560, 50), Pal.Ink, 1.5f, TextAnchor.MiddleLeft);
                if (next) Ui.Outlined(top0, $"点击激活 · 铜钱 {Ui.Num(Meta.TalentCost(i))}", 24, S.Copper >= Meta.TalentCost(i) ? Pal.Hex("B6FF6A") : Pal.Hex("FF8A7A"),
                    new Vector2(cx + 100 + 280, y - 22), new Vector2(560, 36), Pal.Ink, 1.5f, TextAnchor.MiddleLeft);
                int idx = i;
                var hit = Ui.Box(hx, new Vector2(130, 150), Vector2.zero, new Color(0, 0, 0, 0.001f), Art.White, "hit");
                Ui.Tap(hit, () =>
                {
                    if (idx == S.Talent + 1) Learn(layer, changed);
                    else if (idx <= S.Talent) Ui.Toast($"已点亮：{Meta.TalentText(idx)}", 200);
                    else if (idx > Meta.TalentCap(S)) Ui.Toast("继续通关主线解锁（每关 +3）", 200);
                    else Ui.Toast("先点亮下面的天赋", 200);
                }, hx);
            }
            float total = content.sizeDelta.y;
            float focus = -NodeY(Math.Max(1, have + 1));
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(focus - viewH * 0.6f, 0, Mathf.Max(0, total - viewH)));
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit").transform.SetAsFirstSibling();

            float by = -top + 110;
            if (have >= Meta.TalentMax)
            {
                Ui.Outlined(p, "天赋已全部点亮", 44, Pal.Hex("FFE08A"), new Vector2(0, by), new Vector2(600, 60), Pal.Ink, 3f);
                return;
            }
            if (have >= cap)
            {
                Ui.Label(p, "继续通关主线解锁更多天赋（每关 +3）", 32, Pal.Ink, new Vector2(0, by), new Vector2(880, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
                return;
            }
            int cost = Meta.TalentCost(have + 1);
            Ui.Icon(p, "ic_coins", 64, new Vector2(-330, by));
            Ui.Outlined(p, $"{Ui.Num(S.Copper)}/{Ui.Num(cost)}", 34, S.Copper >= cost ? Color.white : Pal.Hex("FF8070"), new Vector2(-160, by), new Vector2(300, 50), Pal.Ink, 2f);
            bool can = Meta.CanTalent(S);
            var b = Ui.Btn(p, "激活", new Vector2(300, 120), new Vector2(240, by), can ? Pal.Hex("F2B632") : Pal.Slate, () => Learn(layer, changed), 46);
            if (can) Dot(b, new Vector2(138, 50));
        }

        static void Learn(Transform layer, Action changed)
        {
            int before = S.Talent;
            if (!Meta.LearnTalent(S))
            {
                Sfx.Play(Snd.Deny);
                if (S.Talent >= Meta.TalentCap(S)) Ui.Toast("继续通关主线解锁更多天赋", 200);
                else { Ui.Toast("铜钱不足", 200); GetPaths(layer, null); }
                return;
            }
            Save();
            TalentTree(layer, changed, before);
            MetaFx.TalentUp(layer, TalentIcon(S.Talent), Meta.TalentText(S.Talent));
            changed?.Invoke();
        }
    }
}

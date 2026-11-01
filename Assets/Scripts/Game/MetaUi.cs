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
        public static RectTransform Modal(Transform layer, string key, string title, Vector2 size, out RectTransform panel, Color? ribbon = null)
        {
            var old = layer.Find("modal_" + key);
            bool again = old != null;
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = Ui.Rect(layer, "modal_" + key);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var dim = Ui.Fill(root, new Color(0, 0, 0, 0.62f), true, "dim");
            var b = dim.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Close(root));
            panel = Ui.Panel(root, size, new Vector2(0, -20));
            Ui.Ribbon(panel, title, new Vector2(0, size.y * 0.5f + 6), Mathf.Min(620, size.x - 80), ribbon);
            var r = root;
            var x = Ui.Icon(panel, "ui/close", 100, new Vector2(size.x * 0.5f - 4, size.y * 0.5f - 4), "close");
            Ui.Tap(x, () => Close(r));
            if (!again) Tween.Pop(panel, 0.25f);
            root.SetAsLastSibling();
            return root;
        }

        public static void Close(RectTransform modal)
        {
            if (modal) Object.Destroy(modal.gameObject);
        }

        public static bool IsOpen(Transform layer, string key) => layer.Find("modal_" + key) != null;

        static void Save() => Profile.Save();

        // ---------------- 奖励弹窗 ----------------

        public static void ShowRewards(Transform layer, string title, List<Item> items, HeroId? unlock = null)
        {
            bool hero = unlock.HasValue;
            Modal(layer, "rewards", title, new Vector2(920, hero ? 760 : 560), out var p, Pal.Hex("C8902A"));
            var shine = Ui.Box(p, new Vector2(700, 700), new Vector2(0, hero ? 80 : 20), Pal.Gold.A(0.25f), Art.Sparkle);
            Tween.Run(shine, 999f, k => shine.transform.localRotation = Quaternion.Euler(0, 0, k * 999f * 20f));
            if (hero)
            {
                var d = Defs.Hero(unlock.Value);
                var art = Ui.Icon(p, d.Art, 260, new Vector2(0, 150));
                Tween.Pop(art.transform, 0.4f, 0.1f);
                Ui.Outlined(p, $"{d.Name} 入伙！", 48, Pal.Hex("FFE08A"), new Vector2(0, -10), new Vector2(700, 64), Pal.Ink, 3f);
            }
            if (items.Count > 0) ItemRow(p, items, new Vector2(0, hero ? -150 : 30), items.Count > 4 ? 130 : 150);
            var m = layer.Find("modal_rewards") as RectTransform;
            Ui.Btn(p, "收下", new Vector2(320, 110), new Vector2(0, -(hero ? 760 : 560) * 0.5f + 90), Pal.Hex("F2B632"), () => Close(m), 46);
            Sfx.Play(Snd.Jackpot, 0.55f);
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
                    Ui.Icon(c, "hero_linchong", 190, new Vector2(-260, 0));
                    Ui.Outlined(c, "豹子头 林冲", 44, Pal.Hex("FFE08A"), new Vector2(80, 20), new Vector2(500, 60), Pal.Hex("5A2A8A"), 3f);
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

        public static void RecruitResult(Transform layer, List<Pull> pulls)
        {
            int n = pulls.Count;
            Modal(layer, "pulls", n > 1 ? "十连招募" : "招募结果", new Vector2(1000, n > 1 ? 1000 : 700), out var p, Pal.Hex("8A3A2C"));
            int best = 0;
            for (int i = 0; i < n; i++)
            {
                var pl = pulls[i];
                var d = Defs.Hero(pl.Hero);
                best = Mathf.Max(best, d.Rarity);
                var pos = n == 1 ? new Vector2(0, 40) : new Vector2(((i % 5) - 2) * 186, 200 - (i / 5) * 330);
                float w = n == 1 ? 300 : 172, h = n == 1 ? 400 : 300;
                var c = Ui.Rect(p, "pull" + i);
                c.anchoredPosition = pos;
                c.sizeDelta = new Vector2(w, h);
                var glow = Ui.Box(c, new Vector2(w * 1.5f, h * 1.3f), Vector2.zero, RarityColor(d.Rarity).A(d.Rarity >= 3 ? 0.55f : 0.2f), Art.SoftDot);
                Card(c, RarityCard(d.Rarity), new Vector2(w, h), Vector2.zero);
                Ui.Icon(c, d.Art, w * 0.9f, new Vector2(0, h * 0.1f));
                Ui.Outlined(c, d.Name, (int)(w * 0.17f), Color.white, new Vector2(0, -h * 0.3f), new Vector2(w, h * 0.16f), Pal.Ink, 2f);
                if (pl.New)
                {
                    Badge(c, "新入伙", Pal.Red, new Vector2(w * 0.66f, h * 0.13f), new Vector2(0, h * 0.47f), (int)(w * 0.13f));
                }
                Ui.Label(c, pl.New ? RarityName(d.Rarity) : $"碎片 +{pl.Shards}", (int)(w * 0.13f), Pal.Cream, new Vector2(0, -h * 0.42f), new Vector2(w, h * 0.12f), TextAnchor.MiddleCenter, Fonts.Heavy);
                Tween.Pop(c, 0.3f, 0.1f + i * 0.12f);
                if (d.Rarity >= 4) Tween.Run(glow, 999f, k => glow.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * 999f * 4f)));
            }
            var m = layer.Find("modal_pulls") as RectTransform;
            Ui.Btn(p, "确定", new Vector2(300, 110), new Vector2(0, n > 1 ? -420 : -270), Pal.Hex("F2B632"), () => Close(m), 44);
            Sfx.Play(best >= 4 ? Snd.Jackpot : Snd.Summon, 0.7f);
        }

        // ---------------- 好汉详情 ----------------

        public static void HeroDetail(Transform layer, HeroId h)
        {
            var d = Defs.Hero(h);
            int lv = S.HeroLv[(int)h];
            bool owned = lv > 0;
            var m = Modal(layer, "hero", d.Name, new Vector2(1000, 1560), out var p, RarityColor(d.Rarity));
            Ui.Skin(p, "dark", new Vector2(920, 420), new Vector2(0, 520));
            Ui.Box(p, new Vector2(600, 300), new Vector2(-180, 400), RarityColor(d.Rarity).A(0.55f), Art.SoftDot);
            var art = Ui.Icon(p, d.Art, 420, new Vector2(-190, 520));
            if (!owned) art.color = new Color(0.3f, 0.3f, 0.32f);
            Ui.Outlined(p, $"「{d.Nick}」", 40, Pal.Hex("FFE08A"), new Vector2(230, 660), new Vector2(400, 56), Pal.Ink, 2.5f);
            Badge(p, d.Role, RoleColor(d.Kind), new Vector2(136, 56), new Vector2(150, 596), 28);
            Badge(p, RarityName(d.Rarity), RarityColor(d.Rarity), new Vector2(136, 56), new Vector2(300, 596), 28);
            Ui.Outlined(p, owned ? $"Lv.{lv}" : "未获得", 56, Color.white, new Vector2(230, 520), new Vector2(400, 70), Pal.Ink, 3f);
            if (owned) Ui.Label(p, $"战力 {Meta.HeroPower(S, h)}", 32, Pal.Hex("FFE7B0"), new Vector2(230, 460), new Vector2(400, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(p, d.Desc, 26, Pal.Cream, new Vector2(230, 400), new Vector2(400, 110));

            // 普攻、大招、被动
            int ulv = Mathf.Max(1, lv);
            string[] names = { $"普攻「{d.AtkName}」· {d.Interval:0.##}秒", $"大招「{d.UltName}」· 冷却{d.UltCd * (ulv >= 15 ? 0.85f : 1f):0.#}秒", $"被动「{d.PassiveName}」" };
            string[] descs = { HeroText.Atk(d), HeroText.Ult(d, ulv >= 25 ? 1.5f : 1f), HeroText.Passive(d) + (ulv >= 5 ? "" : "（5级解锁）") };
            string[] icons = { d.AtkIcon, d.UltIcon, "ic_star" };
            for (int i = 0; i < 3; i++)
            {
                float y = 240 - i * 124;
                bool locked = i == 2 && ulv < 5;
                Ui.Skin(p, "row", new Vector2(920, 120), new Vector2(0, y), 0.6f, locked ? new Color(0.85f, 0.83f, 0.8f) : Color.white);
                Ui.Box(p, new Vector2(92, 92), new Vector2(-400, y), i == 1 ? Pal.Hex("D9A21E") : i == 2 ? Pal.Hex("8A5AD0") : Pal.Hex("3D6FB8"), Art.Hex6)
                    .rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                var ic = Ui.Icon(p, icons[i], 64, new Vector2(-400, y));
                if (locked) ic.color = new Color(0.5f, 0.5f, 0.55f);
                Ui.Label(p, names[i], 28, Pal.Ink, new Vector2(40, y + 34), new Vector2(800, 40), TextAnchor.MiddleLeft, Fonts.Heavy);
                Ui.Label(p, descs[i].Replace("\n", " "), 22, Pal.Hex("5A3A1A"), new Vector2(40, y - 18), new Vector2(800, 70), TextAnchor.UpperLeft);
            }

            // 等级解锁
            Ui.Label(p, $"等级解锁（{d.Coef * 100:0}%攻击力 · {HeroText.Tip(d)}）", 28, Pal.Ink, new Vector2(0, -120), new Vector2(900, 46), TextAnchor.MiddleCenter, Fonts.Heavy);
            for (int i = 0; i < Meta.UnlockLv.Length; i++)
            {
                int need = Meta.UnlockLv[i];
                bool on = lv >= need;
                var pos = i == 0 ? new Vector2(0, -172) : new Vector2((i % 2 == 1 ? -1 : 1) * 228, -172 - ((i + 1) / 2) * 62);
                var size = i == 0 ? new Vector2(900, 58) : new Vector2(444, 58);
                Ui.Skin(p, on ? "tab_on" : "row", size, pos, 0.6f, on ? Color.white : new Color(0.85f, 0.83f, 0.8f));
                Badge(p, $"{need}级", on ? Pal.Hex("D8452F") : Pal.Slate, new Vector2(92, 46), pos + new Vector2(-size.x / 2 + 56, 0), 22);
                Ui.Label(p, Meta.Talent(h, i), 22, on ? Pal.Ink : Pal.Hex("7A6A58"), pos + new Vector2(50, 0), new Vector2(size.x - 130, 54), TextAnchor.MiddleLeft, Fonts.Heavy);
            }

            // 升级
            float by = -510;
            if (owned && lv < Meta.MaxHeroLv)
            {
                int sc = Meta.ShardCost(lv), cc = Meta.CopperCost(lv);
                Ui.Icon(p, d.Art, 70, new Vector2(-380, by + 60));
                Ui.Icon(p, "ic_shard", 40, new Vector2(-404, by + 84));
                Bar(p, new Vector2(-150, by + 60), new Vector2(380, 34), S.Shards[(int)h] / (float)sc, Pal.Hex("9A5BD8"), $"碎片 {S.Shards[(int)h]}/{sc}");
                Ui.Icon(p, "ic_coins", 70, new Vector2(-380, by - 10));
                Ui.Outlined(p, $"{Ui.Num(S.Copper)}/{Ui.Num(cc)}", 32, S.Copper >= cc ? Color.white : Pal.Hex("FF8070"), new Vector2(-150, by - 10), new Vector2(380, 50), Pal.Ink, 2f);
                bool can = Meta.CanLevel(S, h);
                var up = Ui.Btn(p, "升级", new Vector2(300, 120), new Vector2(260, by + 26), can ? Pal.Hex("4FB548") : Pal.Slate, () =>
                {
                    if (!Meta.LevelUp(S, h))
                    {
                        Sfx.Play(Snd.Deny);
                        Ui.Toast(S.Shards[(int)h] < Meta.ShardCost(S.HeroLv[(int)h]) ? "碎片不足：招募或通关获得" : "铜钱不足：巡山、通关获得", 200);
                        return;
                    }
                    Save();
                    Sfx.Play(Snd.Merge, 0.8f);
                    HeroDetail(layer, h);
                    int nl = S.HeroLv[(int)h];
                    int ui = Array.IndexOf(Meta.UnlockLv, nl);
                    Ui.Toast(ui > 0 ? $"升到 {nl} 级，解锁：{Meta.Talent(h, ui)}" : $"{d.Name} 升到 {nl} 级 · 全军暴击伤害 +1%", 200);
                }, 46);
                if (can) Dot(up, new Vector2(138, 50));
                Ui.Label(p, "升级奖励：全军暴击伤害 +1%", 26, Pal.GreenDeep, new Vector2(260, by - 60), new Vector2(400, 40), TextAnchor.MiddleCenter, Fonts.Heavy);
            }
            else if (owned) Ui.Outlined(p, "已满级", 48, Pal.Hex("FFE08A"), new Vector2(0, by + 26), new Vector2(400, 70), Pal.Ink, 3f);
            else
            {
                var at = 0;
                for (int n = 1; n <= Defs.LevelCount && at == 0; n++) if (Meta.UnlockAt(n) == h) at = n;
                Ui.Label(p, at > 0 ? $"通关第 {at} 关入伙，或擂鼓招募获得" : "擂鼓招募获得", 34, Pal.Ink, new Vector2(0, by + 60), new Vector2(800, 56), TextAnchor.MiddleCenter, Fonts.Heavy);
                Bar(p, new Vector2(0, by - 10), new Vector2(500, 34), 0, Pal.Hex("9A5BD8"), $"碎片 {S.Shards[(int)h]}");
            }

            // 上阵
            int slot = Array.IndexOf(S.Lineup, (int)h);
            if (owned)
            {
                if (slot >= 0) Ui.Outlined(p, $"已上阵 · 第 {slot + 1} 位", 36, Pal.GreenDeep, new Vector2(0, -660), new Vector2(500, 56), Pal.Cream, 1f);
                else Ui.Btn(p, "上阵", new Vector2(300, 110), new Vector2(0, -660), Pal.Hex("F2B632"), () => PickSwap(layer, h), 44);
            }
        }

        /// <summary>选一个已上阵的好汉换下。</summary>
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
                    S.Lineup[idx] = (int)h;
                    Save();
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
                if (!Meta.Forge(S, slot))
                {
                    Sfx.Play(Snd.Deny);
                    Ui.Toast(S.Iron < Meta.IronCost(S.EquipLv[slot]) ? "精铁不足：通关、巡山、生辰纲获得" : "铜钱不足", 200);
                    return;
                }
                Save();
                Sfx.Play(Snd.WallHit, 0.7f, 1.3f);
                ForgeSlot(layer, slot);
                Ui.Toast($"{e.Name} 强化到 {S.EquipLv[slot]} 级", 200);
            }, 50);
            Tween.Run(ic, 999f, k => ic.transform.localRotation = Quaternion.Euler(0, 0, 4f * Mathf.Sin(k * 999f * 2f)));
        }

        // ---------------- 寨主天赋 ----------------

        /// <summary>参考视频：竖向一串节点，逐个点亮，每个节点加攻击力和一项副属性；进度跟主线通关走。</summary>
        public static void TalentTree(Transform layer, Action changed = null)
        {
            var m = Modal(layer, "talent", "寨主天赋", new Vector2(1000, 1560), out var p, Pal.Hex("8A5A32"));
            int have = S.Talent, cap = Meta.TalentCap(S);
            Meta.TalentSum(have, out var atk, out var crit, out var cd, out var wall, out var coins, out var spd);
            Ui.Skin(p, "dark", new Vector2(920, 200), new Vector2(0, 600));
            Ui.Outlined(p, $"已点亮 {have}/{Meta.TalentMax}", 40, Pal.Hex("FFE08A"), new Vector2(0, 660), new Vector2(800, 56), Pal.Ink, 2.5f);
            Ui.Label(p, $"攻击力 +{atk:0}   暴击率 +{crit * 100:0.#}%   暴击伤害 +{cd * 100:0}%\n寨墙生命 +{wall * 100:0.#}%   攻速 +{spd * 100:0.#}%   开局银两 +{coins:0}", 28, Pal.Cream, new Vector2(0, 580), new Vector2(880, 100), TextAnchor.MiddleCenter, Fonts.Heavy);

            var view = Ui.Rect(p, "view");
            view.sizeDelta = new Vector2(920, 920);
            view.anchoredPosition = new Vector2(0, 0);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect(view, "content");
            const float step = 130;
            int show = Math.Min(Meta.TalentMax, Math.Max(cap, have) + 4);
            content.sizeDelta = new Vector2(920, show * step + 100);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var top = Ui.Rect(content, "top");
            top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
            top.sizeDelta = Vector2.zero;
            const float amp = 70;
            for (int i = 1; i <= show; i++)
            {
                float y = -60 - (show - i) * step;
                float x = (i % 2 == 0 ? 1 : -1) * amp;
                bool on = i <= have, next = i == have + 1, locked = i > cap;
                if (i < show)
                {
                    float nx = -x;
                    var line = Ui.Box(top, new Vector2(10, Mathf.Sqrt(step * step + 4 * amp * amp)), new Vector2(0, y - step / 2), on ? Pal.Gold : Pal.Hex("B8A890"), Art.White);
                    line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(nx - x, step) * Mathf.Rad2Deg);
                    line.transform.SetAsFirstSibling();
                }
                var kind = Meta.TalentKind(i);
                bool big = i % 10 == 0;
                if (next) Ui.Box(top, Vector2.one * 170, new Vector2(x, y), Pal.Gold.A(0.6f), Art.SoftDot);
                var node = Ui.Box(top, Vector2.one * (big ? 112 : 92), new Vector2(x, y), on ? Pal.GoldDeep : next ? Pal.Hex("F2B632") : Pal.Hex("6A5A48"), Art.Hex6);
                node.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                Ui.Box(top, Vector2.one * (big ? 96 : 78), new Vector2(x, y), on ? Pal.Hex("FFE7A0") : locked ? Pal.Hex("4A4038") : Pal.Hex("2C241E"), Art.Hex6)
                    .rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                string icon = kind == Meta.TalentStat.Crit || kind == Meta.TalentStat.CritDmg ? "ic_sword" : kind == Meta.TalentStat.Wall ? "ic_wall" : kind == Meta.TalentStat.Coins ? "ic_silver" : "ic_fast";
                var ic = Ui.Icon(top, icon, big ? 64 : 52, new Vector2(x, y));
                if (!on) ic.color = locked ? new Color(0.4f, 0.4f, 0.42f) : new Color(0.8f, 0.8f, 0.8f);
                bool left = x < 0;
                var col = on ? Pal.Hex("A8650F") : next ? Pal.Ink : locked ? Pal.Hex("A89880") : Pal.Hex("6A5A48");
                Ui.Label(top, $"{i}. {Meta.TalentText(i)}", big ? 26 : 24, col, new Vector2(left ? x - 70 - 170 : x + 70 + 170, y), new Vector2(340, 70),
                    left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Fonts.Heavy);
            }
            float total = content.sizeDelta.y;
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(60 + (show - have - 1) * step - 460, 0, Mathf.Max(0, total - 920)));
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit").transform.SetAsFirstSibling();

            if (have >= Meta.TalentMax)
            {
                Ui.Outlined(p, "天赋已全部点亮", 44, Pal.Hex("FFE08A"), new Vector2(0, -620), new Vector2(600, 60), Pal.Ink, 3f);
                return;
            }
            if (have >= cap)
            {
                Ui.Label(p, "继续通关主线解锁更多天赋（每关 +3）", 32, Pal.Ink, new Vector2(0, -560), new Vector2(880, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
                return;
            }
            int cost = Meta.TalentCost(have + 1);
            Ui.Label(p, $"下一个：{Meta.TalentText(have + 1)}", 28, Pal.Ink, new Vector2(0, -540), new Vector2(880, 44), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Icon(p, "ic_coins", 64, new Vector2(-330, -640));
            Ui.Outlined(p, $"{Ui.Num(S.Copper)}/{Ui.Num(cost)}", 34, S.Copper >= cost ? Color.white : Pal.Hex("FF8070"), new Vector2(-160, -640), new Vector2(300, 50), Pal.Ink, 2f);
            bool can = Meta.CanTalent(S);
            var b = Ui.Btn(p, "点亮", new Vector2(300, 120), new Vector2(240, -640), can ? Pal.Hex("E8862A") : Pal.Slate, () =>
            {
                if (!Meta.LearnTalent(S))
                {
                    Sfx.Play(Snd.Deny);
                    Ui.Toast("铜钱不足：巡山、通关获得", 200);
                    return;
                }
                Save();
                Sfx.Play(Snd.Merge, 0.8f, 1.2f);
                Close(m);
                TalentTree(layer, changed);
                changed?.Invoke();
            }, 46);
            if (can) Dot(b, new Vector2(138, 50));
        }
    }
}

using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>每日任务（活跃宝箱）、七日目标、招募冲刺、每日福利。</summary>
    public static partial class MetaUi
    {
        static readonly Color Claim = Pal.Hex("F2B632");
        static readonly Color Grass = Pal.Hex("4FB548");

        // ---------------- 通用部件 ----------------

        /// <summary>可上下拖的列表：返回内容节点，子项按顶边居中摆（y 从 0 往下为负）。</summary>
        static RectTransform ScrollList(Transform p, float w, float top, float bottom, float contentH)
        {
            float h = top - bottom;
            var view = Ui.Rect(p, "view");
            view.sizeDelta = new Vector2(w, h);
            view.anchoredPosition = new Vector2(0, (top + bottom) * 0.5f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Ui.Rect(view, "content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(w, Mathf.Max(h, contentH));
            content.anchoredPosition = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            Ui.Fill(view, new Color(0, 0, 0, 0.001f), true, "hit").transform.SetAsFirstSibling();
            return content;
        }

        static RectTransform TopChild(Transform content, string name, float y, Vector2 size)
        {
            var r = Ui.Rect(content, name);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = size;
            return r;
        }

        enum RowState { Go, Claim, Taken, Locked }

        /// <summary>一行进度任务：左边奖励、中间标题和进度条、右边按钮。points &gt; 0 时奖励前加活跃度角标。</summary>
        static void ProgressRow(RectTransform r, float w, IList<Item> rewards, string title, int cur, int need, RowState st,
            Action claim, Action go, Transform tipLayer, int points = 0)
        {
            Ui.Skin(r, "row", new Vector2(w, 136), Vector2.zero, 1f, st == RowState.Taken ? new Color(0.82f, 0.8f, 0.78f) : Color.white);
            float x = -w * 0.5f + 70;
            if (points > 0)
            {
                Ui.Icon(r, "ic_active", 64, new Vector2(x - 6, 16));
                Ui.Outlined(r, "+" + points, 27, Color.white, new Vector2(x - 6, -38), new Vector2(90, 30), Pal.Hex("B0501A"), 2f);
                x += 96;
            }
            foreach (var it in rewards)
            {
                ItemTile(r, it, new Vector2(x, 0), 96, false, tipLayer);
                x += 106;
            }
            float tx = x - 50, bw = w * 0.5f - 230 - tx;
            Fit(Ui.Label(r, title, 33, Pal.Ink, new Vector2(tx + bw * 0.5f, 26), new Vector2(bw, 48), TextAnchor.MiddleLeft, Fonts.Heavy), 24);
            Bar(r, new Vector2(tx + bw * 0.5f, -30), new Vector2(bw, 32), cur / (float)Mathf.Max(1, need), Grass, $"{Mathf.Min(cur, need)}/{need}");
            var bpos = new Vector2(w * 0.5f - 115, 0);
            switch (st)
            {
                case RowState.Taken:
                    Ui.Outlined(r, "已领取", 32, Pal.Hex("8A7A60"), bpos, new Vector2(190, 60), Pal.Cream, 1f);
                    break;
                case RowState.Claim:
                    Dot(Ui.Btn(r, "领取", new Vector2(180, 86), bpos, Claim, claim, 34), new Vector2(84, 38));
                    break;
                case RowState.Locked:
                    Ui.Btn(r, "未开启", new Vector2(180, 86), bpos, Pal.Slate, () => Ui.Toast("还没到这一天", 200), 30);
                    break;
                default:
                    if (go != null) Ui.Btn(r, "前往", new Vector2(180, 86), bpos, Pal.Teal, go, 34);
                    else Ui.Outlined(r, "进行中", 30, Pal.Hex("7A5432"), bpos, new Vector2(190, 60), Pal.Cream, 1f);
                    break;
            }
        }

        /// <summary>积分条 + 一排宝箱：宝箱按门槛摆在条上，够分可点开，开过的换成空箱。</summary>
        static void BoxBar(Transform p, float y, float w, int points, int[] need, Func<int, bool> opened, Func<int, bool> can,
            Action<int> open, string[] icons, Func<int, IList<Item>> preview, Transform tipLayer)
        {
            float left = -w * 0.5f + 70, barL = left + 80, barW = w * 0.5f - 60 - barL;
            Ui.Icon(p, "ic_active", 120, new Vector2(left, y + 6));
            Ui.Outlined(p, points.ToString(), 34, Color.white, new Vector2(left, y - 2), new Vector2(120, 44), Pal.Hex("B0501A"), 2.4f);
            int max = need[need.Length - 1];
            Bar(p, new Vector2(barL + barW * 0.5f, y - 10), new Vector2(barW, 22), points / (float)max, Claim);
            for (int k = 0; k < need.Length; k++)
            {
                int kk = k;
                var pos = new Vector2(barL + barW * need[k] / max - (k == need.Length - 1 ? 20 : 0), y + 30);
                var c = Ui.Rect(p, "box" + k);
                c.anchoredPosition = pos;
                c.sizeDelta = new Vector2(110, 110);
                bool o = opened(k), ok = can(k);
                if (ok)
                {
                    var glow = Ui.Box(c, new Vector2(140, 140), Vector2.zero, Pal.Gold.A(0.45f), Art.SoftDot);
                    Tween.Run(glow, 999f, t => glow.color = Pal.Gold.A(0.3f + 0.25f * Mathf.Sin(t * 999f * 5f)));
                }
                var ic = Ui.Icon(c, o ? "ic_chest_open" : icons[Mathf.Min(k, icons.Length - 1)], 100, Vector2.zero);
                if (o) ic.color = new Color(1, 1, 1, 0.55f);
                Ui.Outlined(c, need[k].ToString(), 24, Color.white, new Vector2(0, -56), new Vector2(100, 30), Pal.Ink, 2f);
                if (ok)
                {
                    Dot(c, new Vector2(40, 40));
                    var t0 = c;
                    Tween.Run(t0, 999f, t => t0.localRotation = Quaternion.Euler(0, 0, 6f * Mathf.Sin(t * 999f * 9f) * (Mathf.Sin(t * 999f * 1.4f) > 0.6f ? 1 : 0)));
                }
                Ui.Tap(ic, () =>
                {
                    if (can(kk)) open(kk);
                    else if (opened(kk)) Ui.Toast("已经领过了", 200);
                    else BoxPreview(tipLayer, preview(kk), $"积分达到 {need[kk]} 可领取");
                }, c);
            }
        }

        /// <summary>宝箱没到门槛时看一眼里面有什么。</summary>
        static void BoxPreview(Transform layer, IList<Item> items, string hint)
        {
            Modal(layer, "boxPreview", "宝箱奖励", new Vector2(820, 520), out var p, Pal.Hex("8A5A2A"));
            ItemRow(p, new List<Item>(items), new Vector2(0, 40), 130);
            Ui.Label(p, hint, 30, Pal.Hex("6E4A2A"), new Vector2(0, -170), new Vector2(740, 44));
        }

        static readonly string[] ChestIcons = { "ic_chest_wood", "ic_chest_silver", "ic_chest_silver", "ic_chest_gold", "ic_chest" };

        // ---------------- 每日任务 ----------------

        public static void Tasks(Transform layer)
        {
            var m = Modal(layer, "tasks", "每日任务", new Vector2(960, 1380), out var p);
            Ui.Label(p, "做任务攒活跃度，开活跃宝箱；出征越多，奖励越多", 28, Pal.Hex("7A5432"), new Vector2(0, 600), new Vector2(880, 40));
            BoxBar(p, 470, 900, Meta.ActPoints(S), Meta.ActBoxNeed, k => (S.ActBox & (1 << k)) != 0, k => Meta.CanActBox(S, k), k =>
            {
                var got = Meta.OpenActBox(S, k);
                if (got == null) return;
                Save();
                Tasks(layer);
                ShowRewards(layer, "活跃宝箱", new List<Item>(got));
            }, ChestIcons, Meta.ActBoxReward, layer);

            var order = new List<int>();
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < Meta.Tasks.Length; i++)
                {
                    bool taken = Meta.TaskTaken(S, i), done = Meta.TaskDone(S, i);
                    if ((pass == 0 && done && !taken) || (pass == 1 && !done) || (pass == 2 && taken)) order.Add(i);
                }
            var content = ScrollList(p, 900, 360, -660, order.Count * 148 + 10);
            for (int n = 0; n < order.Count; n++)
            {
                int i = order[n];
                var t = Meta.Tasks[i];
                var r = TopChild(content, "task" + i, -76 - n * 148, new Vector2(880, 136));
                var st = Meta.TaskTaken(S, i) ? RowState.Taken : Meta.TaskDone(S, i) ? RowState.Claim : RowState.Go;
                Action go = t.Kind == Act.Login ? null : (Action)(() => { Close(m); Game.I.Home?.Goto(t.Kind); });
                ProgressRow(r, 880, new[] { t.Reward }, t.Name, Meta.TaskCount(S, i), t.Need, st, () =>
                {
                    if (!Meta.ClaimTask(S, i)) return;
                    Save();
                    Tasks(layer);
                    ShowRewards(layer, $"任务奖励 · 活跃度 +{t.Points}", new List<Item> { t.Reward });
                }, go, layer, t.Points);
            }
        }

        // ---------------- 七日目标 ----------------

        static int _sevenDay;

        public static void Seven(Transform layer, int day = 0)
        {
            int today = Profile.Today, now = Mathf.Clamp(Meta.DayNo(S, today), 1, Meta.SevenDays);
            if (day <= 0) day = _sevenDay > 0 ? _sevenDay : FirstSevenDay(today, now);
            _sevenDay = day;
            var m = Modal(layer, "seven", "七日目标", new Vector2(1000, 1420), out var p, Pal.Hex("C0602A"));
            var hd = Defs.Hero(Meta.SevenHero);
            Ui.Label(p, $"完成七日目标攒积分，集满领取传说好汉 <color=#D0602A>{hd.Nick}·{hd.Name}</color>", 26, Pal.Hex("7A5432"),
                new Vector2(0, 620), new Vector2(920, 40));
            BoxBar(p, 500, 940, Meta.SevenPointsOf(S), Meta.SevenBoxNeed, k => (S.SevenBox & (1 << k)) != 0, k => Meta.CanSevenBox(S, k), k =>
            {
                var got = Meta.OpenSevenBox(S, k, out var unlock);
                if (got == null) return;
                Save();
                Seven(layer);
                ShowRewards(layer, "七日宝箱", new List<Item>(got), unlock);
            }, ChestIcons, Meta.SevenBoxReward, layer);

            for (int d = 1; d <= Meta.SevenDays; d++)
            {
                int dd = d;
                bool open = d <= now, on = d == day;
                var tab = Ui.Rect(p, "day" + d);
                tab.anchoredPosition = new Vector2(-410, 340 - (d - 1) * 140);
                tab.sizeDelta = new Vector2(140, 124);
                var face = Ui.Skin(tab, on ? "tab_on" : "row", new Vector2(140, 124), Vector2.zero, 1f,
                    open ? (on ? Color.white : new Color(0.95f, 0.9f, 0.84f)) : new Color(0.7f, 0.7f, 0.72f));
                Ui.Outlined(tab, $"第{d}天", 30, Color.white, new Vector2(0, 8), new Vector2(140, 44), on ? Pal.Hex("C0602A") : Pal.Ink, 2f);
                if (!open) Ui.Icon(tab, "ic_lock", 40, new Vector2(0, -34));
                else if (d == now) Ui.Outlined(tab, "今天", 22, Pal.Hex("FFE08A"), new Vector2(0, -34), new Vector2(120, 30), Pal.Ink, 1.5f);
                int badge = open ? Meta.DayBadge(S, d, today) : 0;
                if (badge > 0) Dot(tab, new Vector2(56, 48), badge);
                Ui.Tap(face, () => Seven(layer, dd), tab);
            }

            bool dayOpen = day <= now;
            int row = 0;
            for (int i = 0; i < Meta.Goals.Length; i++)
            {
                var g = Meta.Goals[i];
                if (g.Day != day) continue;
                var r = Ui.Rect(p, "goal" + i);
                r.anchoredPosition = new Vector2(80, 340 - row * 160);
                r.sizeDelta = new Vector2(800, 136);
                row++;
                int idx = i;
                var st = !dayOpen ? RowState.Locked : Meta.GoalTaken(S, i) ? RowState.Taken : Meta.GoalDone(S, i) ? RowState.Claim : RowState.Go;
                ProgressRow(r, 800, g.Reward, g.Name, Meta.GoalValue(S, g), g.Need, st, () =>
                {
                    var got = Meta.ClaimGoal(S, idx, today);
                    if (got == null) return;
                    Save();
                    Seven(layer);
                    ShowRewards(layer, $"七日目标 · 积分 +{Meta.SevenPoints}", new List<Item>(got));
                }, () => { Close(m); Game.I.Home?.Goto(g.Act); }, layer);
            }
            string tip = dayOpen ? "每个目标 +10 积分，往后几天的目标提前完成也能领" : $"第 {day} 天开启，现在可以先做起来";
            Ui.Label(p, tip, 26, Pal.Hex("7A5432"), new Vector2(80, -470), new Vector2(800, 40));
        }

        /// <summary>打开时先跳到有奖可领的那天，没有就跳今天。</summary>
        static int FirstSevenDay(int today, int now)
        {
            for (int d = 1; d <= now; d++) if (Meta.DayBadge(S, d, today) > 0) return d;
            return now;
        }

        // ---------------- 招募冲刺 ----------------

        public static void Sprint(Transform layer, int tab = 0)
        {
            int today = Profile.Today;
            var m = Modal(layer, "sprint", "招募冲刺", new Vector2(1000, 1420), out var p, Pal.Hex("3A6AB0"));
            Ui.Icon(p, "tab_sprint", 150, new Vector2(-380, 560));
            Ui.Outlined(p, $"本轮已招募 {Meta.SprintPulls(S)} 次", 40, Pal.Hex("FFE08A"), new Vector2(-40, 590), new Vector2(560, 56), Pal.Ink, 2.5f);
            Ui.Label(p, $"每轮 {Meta.SprintDays} 天 · 本轮还剩 {Meta.SprintDaysLeft(S, today)} 天 · 招募越多返得越多", 26, Pal.Hex("6E4A2A"),
                new Vector2(-40, 538), new Vector2(620, 40));
            Ui.Btn(p, "前往招募", new Vector2(220, 90), new Vector2(360, 560), Pal.Hex("E8862A"), () => { Close(m); Game.I.Home?.Goto(Act.Recruit); }, 32);

            string[] tabs = { "冲刺目标", "冲刺任务" };
            int[] badges = { Meta.SprintTierBadge(S), Meta.SprintTaskBadge(S) };
            for (int i = 0; i < 2; i++)
            {
                int ti = i;
                var b = Ui.Btn(p, tabs[i], new Vector2(300, 86), new Vector2((i - 0.5f) * 330, 430), i == tab ? Claim : Pal.Hex("A08A70"), () => Sprint(layer, ti), 36);
                if (badges[i] > 0) Dot(b, new Vector2(136, 36), badges[i]);
            }
            int n = tab == 0 ? Meta.SprintTierNeed.Length : Meta.SprintTasks.Length;
            var order = new List<int>();
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < n; i++)
                {
                    bool taken = tab == 0 ? (S.SprintTier & (1 << i)) != 0 : Meta.SprintTaskTaken(S, i);
                    bool can = tab == 0 ? Meta.CanSprintTier(S, i) : Meta.CanSprintTask(S, i);
                    if ((pass == 0 && can) || (pass == 1 && !can && !taken) || (pass == 2 && taken)) order.Add(i);
                }
            var content = ScrollList(p, 920, 370, -670, order.Count * 148 + 10);
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                var r = TopChild(content, "row" + i, -76 - k * 148, new Vector2(900, 136));
                if (tab == 0)
                {
                    int need = Meta.SprintTierNeed[i];
                    var st = (S.SprintTier & (1 << i)) != 0 ? RowState.Taken : Meta.CanSprintTier(S, i) ? RowState.Claim : RowState.Go;
                    ProgressRow(r, 900, Meta.SprintTierReward(i), $"本轮招募 {need} 次", Meta.SprintPulls(S), need, st, () =>
                    {
                        var got = Meta.ClaimSprintTier(S, i);
                        if (got == null) return;
                        Save();
                        Sprint(layer, 0);
                        ShowRewards(layer, "冲刺奖励", new List<Item>(got));
                    }, () => { Close(m); Game.I.Home?.Goto(Act.Recruit); }, layer);
                }
                else
                {
                    var t = Meta.SprintTasks[i];
                    var st = Meta.SprintTaskTaken(S, i) ? RowState.Taken : Meta.CanSprintTask(S, i) ? RowState.Claim : RowState.Go;
                    ProgressRow(r, 900, new[] { t.Reward }, t.Name, Meta.SprintTaskCount(S, i), t.Need, st, () =>
                    {
                        var got = Meta.ClaimSprintTask(S, i);
                        if (got == null) return;
                        Save();
                        Sprint(layer, 1);
                        ShowRewards(layer, "冲刺任务", new List<Item> { got.Value });
                    }, () => { Close(m); Game.I.Home?.Goto(t.Kind); }, layer);
                }
            }
        }

        // ---------------- 每日福利 ----------------

        public static void Welfare(Transform layer)
        {
            int today = Profile.Today;
            Modal(layer, "welfare", "每日福利", new Vector2(880, 900), out var p, Pal.Hex("C0503A"));
            Ui.Icon(p, "tab_welfare", 230, new Vector2(0, 230));
            Ui.Label(p, "每天免费领一次，零点刷新", 30, Pal.Hex("6E4A2A"), new Vector2(0, 80), new Vector2(760, 44));
            ItemRow(p, new List<Item>(Meta.WelfareReward(S)), new Vector2(0, -60), 140);
            bool can = Meta.CanWelfare(S, today);
            Ui.Btn(p, can ? "免费领取" : "明日再来", new Vector2(340, 116), new Vector2(0, -300), can ? Claim : Pal.Slate, () =>
            {
                var got = Meta.ClaimWelfare(S, today);
                if (got == null) { Ui.Toast("今天已经领过了", 200); return; }
                Save();
                Welfare(layer);
                ShowRewards(layer, "每日福利", new List<Item>(got));
            }, 44);
        }
    }
}

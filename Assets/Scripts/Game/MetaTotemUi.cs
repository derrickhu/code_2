using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Liangshan
{
    public static partial class MetaUi
    {
        public static Color TotemRarityColor(int r) =>
            r >= 4 ? Pal.Hex("DF4E38") : r == 3 ? Pal.Hex("E5A428") : r == 2 ? Pal.Hex("8A62E0") : Pal.Hex("549CE0");

        public static Color TotemTileBg(int r) =>
            r >= 4 ? Pal.Hex("F5B89E") : r == 3 ? Pal.Hex("F4D688") : r == 2 ? Pal.Hex("B8A8EA") : Pal.Hex("A2C8EA");

        public static Color TotemTileRim(int r) =>
            r >= 4 ? Pal.Hex("C84A32") : r == 3 ? Pal.Hex("C4881C") : r == 2 ? Pal.Hex("6D4EC0") : Pal.Hex("3E7CB8");

        /// <summary>对标参考图4「熊王图腾·皇冠」双栏对照浮层：左卡洗炼/镶嵌，右列展示该部位 4 个镶嵌槽实时状态。</summary>
        public static void TotemDetail(Transform layer, int totemId, Action changed = null)
        {
            var t = Meta.FindTotem(S, totemId);
            if (t == null) return;
            var old = layer.Find("pop_totem");
            if (old) Object.DestroyImmediate(old.gameObject);

            var root = Ui.Rect(layer, "pop_totem");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var dim = Ui.Fill(root, new Color(0.02f, 0.05f, 0.1f, 0.56f), true, "dim");
            Ui.Tap(dim.GetComponent<Image>(), () => { if (root) Object.Destroy(root.gameObject); }, null, false);

            var wrap = Ui.Rect(root, "wrap");
            wrap.sizeDelta = new Vector2(1000, 560);
            wrap.anchoredPosition = new Vector2(0, 140);

            // ---------- 左侧：图腾主卡 ----------
            const float lw = 486f, lh = 532f;
            var left = Ui.Rect(wrap, "left");
            left.sizeDelta = new Vector2(lw, lh);
            left.anchoredPosition = new Vector2(-248, 0);
            var rc = TotemRarityColor(t.Rarity);
            Ui.Box(left, new Vector2(lw + 10, lh + 10), new Vector2(0, -8), new Color(0, 0, 0, 0.35f), Art.Rounded);
            var lface = Ui.Box(left, new Vector2(lw, lh), Vector2.zero, Pal.Hex("FAF8F2"), Art.Rounded);
            lface.raycastTarget = true;
            Ui.Box(left, new Vector2(lw, 152), new Vector2(0, lh * 0.5f - 76), rc, Art.Rounded);
            Ui.Box(left, new Vector2(lw, 32), new Vector2(0, lh * 0.5f - 136), rc, Art.White);
            Ui.Box(left, new Vector2(lw - 6, lh - 154), new Vector2(0, -76), Pal.Hex("F8F6F0"), Art.Rounded);
            Ui.Box(left, new Vector2(lw - 6, 24), new Vector2(0, lh * 0.5f - 162), Pal.Hex("F8F6F0"), Art.White);

            // 兽首大图标 + 品质角标
            Ui.Icon(left, Meta.TotemIcon(t), 132, new Vector2(-156, lh * 0.5f - 62));
            var rTag = Ui.Box(left, new Vector2(96, 36), new Vector2(-156, lh * 0.5f - 126), rc.Mul(0.75f), Art.RoundedSmall);
            Ui.Outlined(rTag.transform, Meta.TotemRarityNames[t.Rarity], 23, Color.white, new Vector2(0, 1), new Vector2(96, 36), Pal.Ink, 1.5f);

            Ui.Outlined(left, Meta.TotemFullName(t), 33, Color.white, new Vector2(42, lh * 0.5f - 48), new Vector2(280, 46), rc.Mul(0.38f), 2.2f, TextAnchor.MiddleLeft);

            // 锁头按钮
            var lockBg = Ui.Box(left, new Vector2(54, 54), new Vector2(lw * 0.5f - 42, lh * 0.5f - 48), new Color(0, 0, 0, 0.22f), Art.Circle, "lock");
            var lockIc = Ui.Icon(lockBg.transform, "ic_lock", 38, Vector2.zero);
            lockIc.color = t.Locked ? Pal.Gold : new Color(1, 1, 1, 0.45f);
            Ui.Tap(lockBg, () =>
            {
                t.Locked = !t.Locked;
                Save();
                TotemDetail(layer, totemId, changed);
                changed?.Invoke();
            });

            // 部位 + 基础攻击条
            var subBar = new Vector2(46, lh * 0.5f - 104);
            Ui.Box(left, new Vector2(276, 40), subBar, new Color(0, 0, 0, 0.22f), Art.Pill);
            Ui.Icon(left, Meta.Equips[t.Slot].Icon, 32, subBar + new Vector2(-112, 0));
            Ui.Outlined(left, $"{Meta.Equips[t.Slot].Name}    攻击 +{Meta.TotemAtk(t)}", 24, Color.white, subBar + new Vector2(14, 1), new Vector2(236, 38), Pal.Ink, 1.5f, TextAnchor.MiddleLeft);

            // 图腾效果内框
            Ui.Box(left, new Vector2(lw - 44, 212), new Vector2(0, -20), Pal.Hex("ECEAE2"), Art.RoundedSmall);
            Ui.Label(left, "图腾效果", 25, Pal.Hex("7C7870"), new Vector2(0, 56), new Vector2(200, 34), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(left, $"◆  <b>{Meta.TotemEffectText(t, true)}</b>", 30, Pal.Hex("2B343E"), new Vector2(0, -14), new Vector2(lw - 84, 100), TextAnchor.MiddleLeft, Fonts.Heavy);
            if (t.Rarity >= 2)
            {
                int rCost = Meta.RefineStoneCost(t.Rarity);
                Ui.Icon(left, "ic_refine", 32, new Vector2(-90, -96));
                Ui.Label(left, $"洗炼消耗：{S.RefineStones}/{rCost}", 22, S.RefineStones >= rCost ? Pal.Hex("6E6A60") : Pal.Hex("D04840"),
                    new Vector2(26, -96), new Vector2(220, 32), TextAnchor.MiddleLeft, Fonts.Heavy);
            }
            else
            {
                Ui.Label(left, "史诗及以上品质可洗炼词条", 22, Pal.Hex("8A867C"), new Vector2(0, -96), new Vector2(360, 32), TextAnchor.MiddleCenter, Fonts.Heavy);
            }

            // 底部双按钮：洗炼（暖金） + 镶嵌/卸下（青翠）
            bool canRef = Meta.CanRefineTotem(S, t);
            Ui.Btn(left, "洗炼", new Vector2(196, 88), new Vector2(-112, -lh * 0.5f + 66), canRef ? Pal.Gold : Pal.Slate, () =>
            {
                if (t.Rarity < 2) { Ui.Toast("史诗及以上品质图腾才可洗炼", 200); return; }
                if (t.Locked) { Ui.Toast("请先解锁右上角锁头再洗炼", 200); return; }
                float before = Meta.TeamPower(S);
                if (!Meta.RefineTotem(S, t.Id, new Rng(Environment.TickCount)))
                {
                    Sfx.Play(Snd.Deny);
                    Ui.Toast("洗炼石不足：可在集市、活跃宝箱获取", 200);
                    return;
                }
                Save();
                Sfx.Play(Snd.Merge, 0.85f);
                TotemDetail(layer, t.Id, changed);
                changed?.Invoke();
                float after = Meta.TeamPower(S);
                if (Mathf.Abs(after - before) >= 1f) MetaFx.StatUp(layer, "战力", before, after);
            }, 36);

            bool equipped = Meta.IsTotemEquipped(S, t.Id);
            Ui.Btn(left, equipped ? "卸下" : "镶嵌", new Vector2(196, 88), new Vector2(112, -lh * 0.5f + 66), equipped ? Pal.Orange : Pal.Teal, () =>
            {
                float before = Meta.TeamPower(S);
                Meta.ToggleInlayTotem(S, t.Id);
                Save();
                Sfx.Play(Snd.Unlock, 0.75f);
                TotemDetail(layer, t.Id, changed);
                changed?.Invoke();
                float after = Meta.TeamPower(S);
                if (after > before) MetaFx.StatUp(layer, "战力", before, after);
            }, 36);

            // ---------- 右侧：该部位 4 个镶嵌槽 ----------
            int baseIdx = t.Slot * Meta.TotemSocketsPerSlot;
            for (int k = 0; k < Meta.TotemSocketsPerSlot; k++)
            {
                var row = Ui.Rect(wrap, "sock" + k);
                const float rw = 476f, rh = 120f;
                row.sizeDelta = new Vector2(rw, rh);
                row.anchoredPosition = new Vector2(254, 198 - k * 132);
                bool open = Meta.TotemSocketOpen(S, k);
                int eqId = open ? S.TotemEquipped[baseIdx + k] : 0;
                var eqT = eqId > 0 ? Meta.FindTotem(S, eqId) : null;
                bool isSelf = eqT != null && eqT.Id == t.Id;

                Ui.Box(row, new Vector2(rw + 6, rh + 6), new Vector2(0, -4), new Color(0, 0, 0, 0.25f), Art.RoundedSmall);
                var rbg = Ui.Box(row, new Vector2(rw, rh), Vector2.zero,
                    !open ? Pal.Hex("D2CFC6") : isSelf ? Pal.Hex("FBE8A6") : Pal.Hex("FAF8F2"), Art.RoundedSmall);
                rbg.raycastTarget = true;
                if (isSelf)
                {
                    Ui.Box(row, new Vector2(rw - 8, rh - 8), Vector2.zero, Pal.Hex("E2A828"), Art.RoundedSmall);
                    Ui.Box(row, new Vector2(rw - 14, rh - 14), Vector2.zero, Pal.Hex("FFF9EA"), Art.RoundedSmall);
                }

                if (!open)
                {
                    Ui.Box(row, new Vector2(64, 64), new Vector2(-176, 0), Pal.Hex("BCB8AE"), Art.Hex6);
                    Ui.Label(row, $"通关主线第 <color=#C84032>{Meta.TotemSocketUnlockCleared[k]}</color> 关解锁", 26, Pal.Hex("6A665E"),
                        new Vector2(28, 0), new Vector2(340, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
                }
                else if (eqT != null)
                {
                    Ui.Box(row, new Vector2(82, 82), new Vector2(-176, 0), TotemTileBg(eqT.Rarity), Art.RoundedSmall);
                    Ui.Icon(row, Meta.TotemIcon(eqT), 74, new Vector2(-176, 2));
                    Ui.Label(row, Meta.TotemEffectText(eqT, true), 26, Pal.Hex("2B343E"),
                        new Vector2(36, 12), new Vector2(326, 64), TextAnchor.MiddleLeft, Fonts.Heavy);
                    Ui.Label(row, $"{Meta.TotemRarityNames[eqT.Rarity]} · 攻击 +{Meta.TotemAtk(eqT)}", 21, TotemRarityColor(eqT.Rarity),
                        new Vector2(36, -32), new Vector2(326, 30), TextAnchor.MiddleLeft, Fonts.Heavy);
                }
                else
                {
                    Ui.Box(row, new Vector2(74, 74), new Vector2(-176, 0), Pal.Hex("E5E2D8"), Art.RoundedSmall);
                    Ui.Label(row, "+", 44, Pal.Hex("9E9A90"), new Vector2(-176, 2), new Vector2(74, 74), TextAnchor.MiddleCenter, Fonts.Heavy);
                    Ui.Label(row, $"{Meta.Equips[t.Slot].Name} 空镶嵌槽 · 可镶嵌", 26, Pal.Hex("7C7870"),
                        new Vector2(32, 0), new Vector2(330, 50), TextAnchor.MiddleLeft, Fonts.Heavy);
                }
            }

            root.SetAsLastSibling();
            Tween.Pop(wrap, 0.2f);
        }

        // ---------------- 天罡战氅（寨主披风） ----------------

        public static void Cloak(Transform layer, Action changed = null)
        {
            Modal(layer, "cloak", "天罡战氅", new Vector2(880, 960), out var p, Pal.Hex("2C6E9E"));
            int lv = S.CloakLv;
            Ui.Skin(p, "dark", new Vector2(800, 340), new Vector2(0, 210), 1f);
            Ui.Box(p, new Vector2(300, 300), new Vector2(-210, 210), Pal.Azure.A(0.42f), Art.SoftDot);
            var ic = Ui.Icon(p, "eq_cloak", 240, new Vector2(-210, 214));
            Tween.Run(ic, 999f, k => ic.rectTransform.anchoredPosition = new Vector2(-210, 214 + 5f * Mathf.Sin(k * 999f * 2.2f)));

            Ui.Outlined(p, lv > 0 ? $"天罡白裘氅 · Lv.{lv}/{Meta.CloakMax}" : "天罡白裘氅 · 未织造", 36, Pal.Champagne,
                new Vector2(140, 290), new Vector2(440, 50), Pal.Ink, 2.2f, TextAnchor.MiddleLeft);
            Ui.Label(p, $"当前加成：\n寨墙生命 <color=#7CE898>+{Meta.CloakWallBonus(lv) * 100:0}%</color>\n全军最终伤害 <color=#7CE898>+{Meta.CloakDmgBonus(lv) * 100:0}%</color>",
                28, Pal.Cream, new Vector2(140, 175), new Vector2(430, 140), TextAnchor.MiddleLeft, Fonts.Heavy);

            if (lv < Meta.CloakMax)
            {
                Ui.Label(p, $"下一阶：寨墙生命 <color=#1E9438>+{Meta.CloakWallBonus(lv + 1) * 100:0}%</color> · 全军伤害 <color=#1E9438>+{Meta.CloakDmgBonus(lv + 1) * 100:0}%</color>",
                    28, Pal.Ink, new Vector2(0, -20), new Vector2(780, 50), TextAnchor.MiddleCenter, Fonts.Heavy);
                int sc = Meta.CloakSilkCost(lv), cc = Meta.CloakCopperCost(lv);
                Ui.Icon(p, "ic_silk", 64, new Vector2(-250, -130));
                Ui.Label(p, $"{Ui.Num(S.Silk)}/{sc}", 30, S.Silk >= sc ? Pal.GreenDeep : Pal.Hex("D04848"), new Vector2(-120, -130), new Vector2(200, 44), TextAnchor.MiddleLeft, Fonts.Heavy);
                Ui.Icon(p, "ic_coins", 64, new Vector2(90, -130));
                Ui.Label(p, $"{Ui.Num(S.Copper)}/{Ui.Num(cc)}", 30, S.Copper >= cc ? Pal.GreenDeep : Pal.Hex("D04848"), new Vector2(230, -130), new Vector2(220, 44), TextAnchor.MiddleLeft, Fonts.Heavy);
                bool can = Meta.CanCloakUp(S);
                Ui.Btn(p, lv == 0 ? "织造披风" : "升阶披风", new Vector2(360, 118), new Vector2(0, -290), can ? Pal.Gold : Pal.Slate, () =>
                {
                    float before = Meta.TeamPower(S);
                    if (!Meta.UpgradeCloak(S))
                    {
                        Sfx.Play(Snd.Deny);
                        Ui.Toast(S.Silk < sc ? "锦缎不足：关卡宝箱、巡山、集市获取" : "铜钱不足", 200);
                        return;
                    }
                    Save();
                    Sfx.Play(Snd.Unlock);
                    Cloak(layer, changed);
                    MetaFx.StatUp(layer, "战力", before, Meta.TeamPower(S));
                    changed?.Invoke();
                }, 42);
            }
            else Ui.Outlined(p, "战氅已臻满阶", 42, Pal.Champagne, new Vector2(0, -220), new Vector2(500, 64), Pal.Ink, 2.5f);
        }

        // ---------------- 寨主属性总览 ----------------

        public static void LordStats(Transform layer)
        {
            var lo = Meta.BuildLoadout(S);
            Modal(layer, "lordStats", "统帅属性总览", new Vector2(860, 920), out var p);
            string[] lines =
            {
                $"统帅基础攻击力：<color=#1874B0>{lo.Atk:0}</color>",
                $"全军最终增伤：<color=#1E9438>+{(lo.DmgMul - 1f) * 100:0.#}%</color>",
                $"全军攻击速度：<color=#1E9438>+{(lo.AllSpeed - 1f) * 100:0.#}%</color>",
                $"全军暴击率 / 暴伤：<color=#1874B0>{lo.Crit * 100:0.#}%</color> / <color=#1874B0>{lo.CritDmg * 100:0}%</color>",
                $"寨墙生命 / 减伤：<color=#1E9438>+{(lo.WallMul - 1f) * 100:0.#}%</color> / <color=#1E9438>{lo.WallResist * 100:0.#}%</color>",
                $"替天行道剑气：<color=#D67418>{lo.LordPct * 100:0}%</color> × {lo.LordHits}道（冷却 {lo.LordCd:0.#}s）",
                $"对精英 / 首领加成：<color=#1E9438>+{(lo.EliteDmg - 1f) * 100:0.#}%</color> / <color=#1E9438>+{(lo.BossDmg - 1f) * 100:0.#}%</color>",
                $"前3波 / 受控敌军加成：<color=#1E9438>+{lo.EarlyWaveDmg * 100:0.#}%</color> / <color=#1E9438>+{lo.ControlDmg * 100:0.#}%</color>",
                $"开局额外银两：<color=#D67418>+{lo.StartCoins}</color>",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                float y = 320 - i * 74;
                Ui.Skin(p, "row", new Vector2(760, 62), new Vector2(0, y), 0.55f);
                Ui.Label(p, lines[i], 28, Pal.Ink, new Vector2(0, y), new Vector2(700, 56), TextAnchor.MiddleLeft, Fonts.Heavy);
            }
            Ui.Btn(p, "确定", new Vector2(280, 100), new Vector2(0, -370), Pal.Azure, () => Close((RectTransform)layer.Find("modal_lordStats")), 38);
        }

        // ---------------- 心愿好汉选择（50% 定向 UP） ----------------

        public static void WishPicker(Transform layer, Action changed = null)
        {
            var m = Modal(layer, "wish", "心愿好汉 · 定向祈愿", new Vector2(980, 1340), out var p, Pal.Hex("C88A22"));
            Ui.Label(p, "选定 1 位史诗或传说好汉作为心愿目标：招募到对应品质时，<color=#D66816>50% 概率必中心愿好汉！</color>",
                26, Pal.SubInk, new Vector2(0, 568), new Vector2(880, 52), TextAnchor.MiddleCenter, Fonts.Heavy);

            var cands = new List<HeroId>();
            foreach (var d in Defs.Heroes)
            {
                if (d.Rarity == 4 && Meta.OrangeOpened(S)) cands.Add(d.Id);
                else if (d.Rarity == 3) cands.Add(d.Id);
            }
            cands.Sort((a, b) => Defs.Hero(b).Rarity != Defs.Hero(a).Rarity ? Defs.Hero(b).Rarity.CompareTo(Defs.Hero(a).Rarity) : a.CompareTo(b));

            const float cw = 206f, ch = 264f, rowH = 282f;
            var content = ScrollList(p, 900, 510, -520, ((cands.Count + 3) / 4) * rowH + 24);
            var curWish = Meta.GetWish(S);
            for (int i = 0; i < cands.Count; i++)
            {
                var h = cands[i];
                var d = Defs.Hero(h);
                bool sel = curWish.HasValue && curWish.Value == h;
                var cell = TopChild(content, "w" + i, -148 - (i / 4) * rowH, new Vector2(cw, ch));
                cell.anchoredPosition = new Vector2(((i % 4) - 1.5f) * 220, -148 - (i / 4) * rowH);
                if (sel) Ui.Box(cell, new Vector2(cw + 16, ch + 16), Vector2.zero, Pal.Gold, Art.Rounded);
                var face = Card(cell, RarityCard(d.Rarity), new Vector2(cw, ch), Vector2.zero);
                Ui.Icon(cell, d.Art, 180, new Vector2(0, 22));
                Ui.Outlined(cell, d.Name, 28, Color.white, new Vector2(0, -96), new Vector2(cw, 40), Pal.Ink, 2f);
                if (sel) Badge(cell, "心愿UP", Pal.Gold, new Vector2(104, 38), new Vector2(0, 108), 22);
                var pick = h;
                Ui.Tap(face, () =>
                {
                    Meta.SetWish(S, sel ? (HeroId?)null : pick);
                    Save();
                    Close(m);
                    changed?.Invoke();
                    Ui.Toast(sel ? "已取消心愿好汉" : $"已设定心愿：{d.Name}（同品质 50% UP）", 200);
                }, cell);
            }
            if (!Meta.OrangeOpened(S))
                Ui.Label(p, $"提示：通关主线第 {Meta.OrangeOpen} 关后，可选定传说好汉进入心愿池", 24, Pal.Muted, new Vector2(0, -585), new Vector2(860, 38), TextAnchor.MiddleCenter, Fonts.Heavy);
        }

        // ---------------- 兽魂寻宝结果揭晓 ----------------

        public static void TotemPullResult(Transform layer, List<TotemData> pulls, Action again)
        {
            int n = pulls.Count;
            var root = MetaFx.Overlay(layer, "totemPulls", 0.82f, null);
            MetaFx.Band(root, 40, n > 1 ? 820 : 540);
            MetaFx.GoldBanner(root, n > 1 ? "十连寻宝" : "兽魂寻宝", new Vector2(0, n > 1 ? 540 : 410), 660);
            Sfx.Play(Snd.Jackpot, 0.75f);

            for (int i = 0; i < n; i++)
            {
                var t = pulls[i];
                var pos = n == 1 ? new Vector2(0, 60) : new Vector2(((i % 5) - 2) * 196, 210 - (i / 5) * 330);
                float sz = n == 1 ? 240f : 168f;
                var cell = Ui.Rect(root, "tp" + i);
                cell.anchoredPosition = pos;
                cell.sizeDelta = new Vector2(sz, sz + 66);
                if (t.Rarity >= 2)
                    Ui.Box(cell, new Vector2(sz * 1.45f, sz * 1.45f), new Vector2(0, 20), TotemRarityColor(t.Rarity).A(0.45f), Art.SoftDot);
                string slotSkin = t.Rarity >= 3 ? "slot_orange" : t.Rarity == 2 ? "slot_purple" : "slot_blue";
                Slot(cell, slotSkin, sz, new Vector2(0, 20));
                Ui.Icon(cell, Meta.TotemIcon(t), sz * 0.78f, new Vector2(0, 22));
                Badge(cell, Meta.TotemRarityNames[t.Rarity], TotemRarityColor(t.Rarity), new Vector2(88, 34), new Vector2(0, 20 + sz * 0.5f - 10), 20);
                Ui.Outlined(cell, Meta.TotemFullName(t), n == 1 ? 30 : 22, Color.white, new Vector2(0, -sz * 0.5f - 6), new Vector2(sz + 30, 34), Pal.Ink, 1.8f);
                if (n == 1)
                    Ui.Outlined(cell, Meta.TotemEffectText(t), 26, Pal.Hex("FFE488"), new Vector2(0, -sz * 0.5f - 46), new Vector2(680, 40), Pal.Ink, 1.8f);
                Tween.Pop(cell, 0.28f, 0.08f + i * 0.05f);
            }

            var btns = Ui.Rect(root, "btns");
            btns.anchoredPosition = new Vector2(0, n > 1 ? -460 : -320);
            Ui.Btn(btns, "收入囊中", new Vector2(280, 110), new Vector2(-180, 0), Pal.Azure, () => MetaFx.Dismiss(root), 40);
            Ui.Btn(btns, n > 1 ? "再寻十次" : "再寻一次", new Vector2(300, 110), new Vector2(180, 0), Pal.Gold, () =>
            {
                MetaFx.Dismiss(root);
                again?.Invoke();
            }, 38, "ic_hammer");
        }
    }
}

using System;
using System.Collections.Generic;
using Liangshan.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Liangshan
{
    /// <summary>开发工具里的 GM 开关。真机不出现，跟 code_3 一样只认编辑器和微信开发者工具。</summary>
    public static class Gm
    {
        /// <summary>截图验收强制打开。正式包不要设。</summary>
        public static bool Force;

        public static bool Allowed
        {
            get
            {
                if (Force || Application.isEditor) return true;
                try
                {
                    var wx = Type.GetType("WeChatWASM.WX, Wx") ?? Type.GetType("WeChatWASM.WX");
                    var info = wx?.GetMethod("GetSystemInfoSync", Type.EmptyTypes)?.Invoke(null, null);
                    if (info == null) return false;
                    var t = info.GetType();
                    object raw = t.GetField("platform")?.GetValue(info) ?? t.GetProperty("platform")?.GetValue(info);
                    return raw is string name && name.Equals("devtools", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static float NudgeDifficulty(float delta)
        {
            var cfg = Balance.ExportCurrent();
            cfg.WaveStage.GlobalHpMul = Mathf.Clamp(Mathf.Round((cfg.WaveStage.GlobalHpMul + delta) * 100f) / 100f, 0.65f, 1.45f);
            cfg.WaveStage.GlobalAtkMul = Mathf.Clamp(Mathf.Round((cfg.WaveStage.GlobalAtkMul + delta * 0.8f) * 100f) / 100f, 0.60f, 1.50f);
            Balance.Apply(cfg);
            return Balance.Current.WaveStage.GlobalHpMul;
        }

        public static void ResetDifficulty()
        {
            var cfg = Balance.ExportCurrent();
            cfg.WaveStage.GlobalHpMul = 1.00f;
            cfg.WaveStage.GlobalAtkMul = 1.00f;
            Balance.Apply(cfg);
        }
    }

    /// <summary>主界面 GM：跳过还没打的关，发元宝等物资，实时微调测试难度或一键套用标准局外养成。</summary>
    public sealed partial class HomeScreen
    {
        Text _gmStatus;

        void BuildGm()
        {
            if (!Gm.Allowed) return;
            // 收在右上体力栏正下方小胶囊，绝不遮挡寨主背包格子、好汉列表或底栏
            var slot = Ui.Top(_page, 476, Ui.SafeTop + 122, new Vector2(92, 42), "gm");
            Ui.Btn(slot, "GM", new Vector2(92, 42), Vector2.zero, Pal.Slate, ShowGm, 22);
            slot.SetAsLastSibling();
        }

        public void ShowGm()
        {
            const float H = 1780f;
            MetaUi.Modal(Layer, "gm", "GM", new Vector2(860, H), out var card, Pal.Slate);
            float y = H * 0.5f - 110f;
            _gmStatus = Ui.Label(card, GmFrontier(), 34, Pal.Ink, new Vector2(0, y), new Vector2(760, 52), TextAnchor.MiddleCenter, Fonts.Heavy);
            y -= 54f;
            Ui.Label(card, "只在编辑器和微信开发者工具里出现。可实时微调难度或套用标准养成。", 24, Pal.SubInk, new Vector2(0, y), new Vector2(760, 40));
            y -= 74f;

            // 测试期难度快调与局外养成模拟档位
            Ui.Btn(card, "难度 -10%", new Vector2(236, 72), new Vector2(-252f, y), Pal.Green, () => GmDiff(-0.10f), 28);
            Ui.Btn(card, "标准 1.0x", new Vector2(236, 72), new Vector2(0f, y), Pal.Slate, () => GmDiff(0f), 28);
            Ui.Btn(card, "难度 +10%", new Vector2(236, 72), new Vector2(252f, y), Pal.Red, () => GmDiff(0.10f), 28);
            y -= 84f;
            Ui.Btn(card, "套用本关标准养成", new Vector2(364, 72), new Vector2(-190f, y), Pal.Azure, () => GmApplyExpected(0), 28);
            Ui.Btn(card, "套用落后3关养成", new Vector2(364, 72), new Vector2(190f, y), Pal.Teal, () => GmApplyExpected(-3), 28);
            y -= 92f;

            void Big(string name, Color c, Action act)
            {
                Ui.Btn(card, name, new Vector2(560, 86), new Vector2(0, y), c, act, 36);
                y -= 98f;
            }
            Big("跳过本关", Pal.Azure, () => GmSkip(1));
            Big("连跳五关", Pal.Teal, () => GmSkip(5));
            Big("关卡全开", Pal.Green, () => GmSkip(Defs.LevelCount));
            Big("解锁全部好汉", Pal.Gold, GmUnlockAll);
            Big("清档", Pal.Red, () => MetaUi.Confirm(Layer, "本地和云端都换成新档\n铜钱、进度、好汉都没了，不能恢复", WipeAll));
            y -= 4f;
            Ui.Label(card, "发物资，直接写进存档", 24, Pal.SubInk, new Vector2(0, y), new Vector2(740, 36));
            y -= 64f;
            Ui.Btn(card, "发一套常用", new Vector2(560, 80), new Vector2(0, y), Pal.Gold, GmPack, 34);
            y -= 86f;
            var grants = new (string name, Cur cur, int n)[]
            {
                ("元宝", Cur.Ingot, 1000),
                ("铜钱", Cur.Copper, 10000),
                ("精铁", Cur.Iron, 500),
                ("招募令", Cur.Token, 20),
                ("洗炼石", Cur.Refine, 20),
                ("寻宝锤", Cur.Hammer, 20),
                ("体力", Cur.Stamina, 60),
                ("锦缎", Cur.Silk, 100),
                ("玄铁", Cur.Meteor, 20),
                ("天书残页", Cur.Page, 50),
                ("玄女天书", Cur.Tome, 10),
                ("装备箱", Cur.GearBox, 5),
            };
            for (int i = 0; i < grants.Length; i++)
            {
                var g = grants[i];
                Cur cur = g.cur;
                int n = g.n;
                string name = g.name;
                Ui.Btn(card, $"{name} +{n}", new Vector2(360, 68), new Vector2(i % 2 == 0 ? -190f : 190f, y - (i / 2) * 76f), Pal.Slate,
                    () => GmGive(cur, n, name), 28);
            }
        }

        void GmDiff(float delta)
        {
            if (Mathf.Abs(delta) < 0.001f) Gm.ResetDifficulty();
            else Gm.NudgeDifficulty(delta);
            float hpM = Balance.Current.WaveStage.GlobalHpMul;
            float atkM = Balance.Current.WaveStage.GlobalAtkMul;
            if (_gmStatus) _gmStatus.text = GmFrontier();
            Ui.Toast($"测试难度：怪血 ×{hpM:0.00} · 拆墙 ×{atkM:0.00}", 900);
        }

        void GmApplyExpected(int offset)
        {
            int stage = Mathf.Clamp(S.Selected > 0 ? S.Selected : S.Cleared + 1, 1, Defs.LevelCount);
            int target = Mathf.Max(1, stage + offset);
            var exp = Meta.ExpectedSave(target);
            Array.Copy(exp.HeroLv, S.HeroLv, Mathf.Min(exp.HeroLv.Length, S.HeroLv.Length));
            Array.Copy(exp.Lineup, S.Lineup, S.Lineup.Length);
            if (exp.Presets != null && S.Presets != null)
                Array.Copy(exp.Presets, S.Presets, Mathf.Min(exp.Presets.Length, S.Presets.Length));
            Array.Copy(exp.EquipLv, S.EquipLv, S.EquipLv.Length);
            Array.Copy(exp.EquipQ, S.EquipQ, S.EquipQ.Length);
            S.Talent = exp.Talent;
            S.CloakLv = exp.CloakLv;
            S.PageLv = exp.PageLv;
            S.Totems.Clear();
            S.Totems.AddRange(exp.Totems);
            Array.Copy(exp.TotemEquipped, S.TotemEquipped, S.TotemEquipped.Length);
            S.NextTotemId = exp.NextTotemId;
            S.Cleared = Mathf.Max(S.Cleared, stage - 1);
            S.Selected = stage;
            Profile.Save();
            Rebuild(false);
            if (_gmStatus) _gmStatus.text = GmFrontier();
            string label = offset == 0 ? $"第 {stage} 关标准养成" : $"第 {target} 关养成(落后{-offset}关)";
            Ui.Toast($"已套用：{label}（战力 {Meta.TeamPower(S)}）", 920);
        }

        void GmGive(Cur c, int n, string name)
        {
            Meta.Give(S, new Item(c, n));
            Profile.Save();
            if (_gmStatus) _gmStatus.text = $"{name} +{n}　现有 {Meta.Have(S, c)}";
            Ui.Toast($"{name} +{n}", 880);
        }

        void GmPack()
        {
            (Cur c, int n)[] pack =
            {
                (Cur.Ingot, 5000), (Cur.Copper, 50000), (Cur.Iron, 2000), (Cur.Token, 50),
                (Cur.Refine, 30), (Cur.Hammer, 30), (Cur.Stamina, 120), (Cur.Silk, 200),
                (Cur.Meteor, 30), (Cur.Page, 50), (Cur.Tome, 10), (Cur.GearBox, 10),
            };
            foreach (var p in pack) Meta.Give(S, new Item(p.c, p.n));
            Profile.Save();
            if (_gmStatus) _gmStatus.text = "已发一套：元宝 铜钱 精铁 招募令等";
            Ui.Toast("已发一套常用物资", 880);
        }

        void GmUnlockAll()
        {
            int n = 0;
            for (int i = 0; i < Defs.HeroCount; i++)
            {
                if (S.HeroLv[i] > 0) continue;
                S.HeroLv[i] = 1;
                n++;
            }
            if (n == 0)
            {
                Ui.Toast("好汉都已入伙", 880);
                return;
            }
            Profile.Save();
            if (_gmStatus) _gmStatus.text = $"新入伙 {n} 位，都是 1 级";
            Ui.Toast($"解锁 {n} 位好汉", 880);
            Rebuild(false);
        }

        void WipeAll()
        {
            Profile.Reset();
            CloudSync.FlushNow("wipe");
            Game.I.ShowHome();
            Ui.Toast("已换成新档，云端一起盖掉", 880);
        }

        public void GmSkip(int count)
        {
            int from = S.Cleared + 1;
            if (from > Defs.LevelCount)
            {
                Ui.Toast("已经全部通关", 880);
                return;
            }
            int n = Mathf.Min(count, Defs.LevelCount - S.Cleared);
            var names = new List<string>();
            for (int i = 0; i < n; i++)
            {
                var st = Defs.Level(from + i);
                var r = Meta.BattleRewards(S, st, true, st.Waves, 1f, 0, new Rng(st.No * 97 + 11));
                if (r.Unlock.HasValue) names.Add(Defs.Hero(r.Unlock.Value).Name);
                Meta.Give(S, r);
                Meta.FinishMain(S, st, true, r);
            }
            Profile.Save();
            if (_gmStatus) _gmStatus.text = GmFrontier();
            string who = names.Count > 0 ? "，" + string.Join("、", names) + "入伙" : "";
            string where = S.Cleared >= Defs.LevelCount ? "已全部通关" : "现在第 " + (S.Cleared + 1) + " 关";
            Ui.Toast($"跳过 {n} 关，{where}{who}", 880);
        }

        static string GmFrontier()
        {
            float hpM = Balance.Current.WaveStage.GlobalHpMul;
            string diff = $"  |  难度 ×{hpM:0.00}";
            if (S.Cleared >= Defs.LevelCount) return "已全部通关" + diff;
            var st = Defs.Level(S.Cleared + 1);
            return "下一关 第 " + st.No + " 关·" + st.Name + diff;
        }
    }
}

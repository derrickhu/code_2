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
    }

    /// <summary>主界面 GM：跳过还没打的关，或把本地和云端一起换成新档。</summary>
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
            MetaUi.Modal(Layer, "gm", "GM", new Vector2(820, 980), out var card, Pal.Slate);
            _gmStatus = Ui.Label(card, GmFrontier(), 40, Pal.Ink, new Vector2(0, 340), new Vector2(720, 70), TextAnchor.MiddleCenter, Fonts.Heavy);
            Ui.Label(card, "只在编辑器和微信开发者工具里出现。\n跳过按三星通关结算，首通奖励和好汉照常给。", 30, Pal.SubInk, new Vector2(0, 230), new Vector2(700, 110));
            Ui.Btn(card, "跳过本关", new Vector2(560, 110), new Vector2(0, 80), Pal.Azure, () => GmSkip(1), 46);
            Ui.Btn(card, "连跳五关", new Vector2(560, 110), new Vector2(0, -50), Pal.Teal, () => GmSkip(5), 46);
            Ui.Btn(card, "关卡全开", new Vector2(560, 110), new Vector2(0, -180), Pal.Green, () => GmSkip(Defs.LevelCount), 46);
            Ui.Btn(card, "清档", new Vector2(560, 110), new Vector2(0, -310), Pal.Red, () =>
            {
                MetaUi.Confirm(Layer, "本地和云端都换成新档\n铜钱、进度、好汉都没了，不能恢复", WipeAll);
            }, 46);
        }

        void WipeAll()
        {
            Profile.Reset();
            CloudSync.FlushNow("wipe");
            Game.I.ShowHome();
            Ui.Toast("已换成新档，云端一起盖掉", 520);
        }

        public void GmSkip(int count)
        {
            int from = S.Cleared + 1;
            if (from > Defs.LevelCount)
            {
                Ui.Toast("已经全部通关", 520);
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
            Ui.Toast($"跳过 {n} 关，{where}{who}", 520);
        }

        static string GmFrontier()
        {
            if (S.Cleared >= Defs.LevelCount) return "已全部通关";
            var st = Defs.Level(S.Cleared + 1);
            return "下一关　第 " + st.No + " 关 · " + st.Name;
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Liangshan.Sim;
using UnityEngine;

namespace Liangshan
{
    /// <summary>敌人说明：局内点怪的信息卡和「本关敌情」面板共用。</summary>
    public static class EnemyText
    {
        public static string RankName(EnemyDef d) => d.IsBoss ? "首领" : d.Big ? "精英" : "小兵";

        public static Color RankColor(EnemyDef d) => d.IsBoss ? Pal.Hex("D8463A") : d.Big ? Pal.Hex("8E5CE0") : Pal.Hex("5B7C9E");

        /// <summary>一眼能看出打法的标签：高速、远程、飞行、重甲、自爆……</summary>
        public static List<(string text, Color color)> Tags(EnemyDef d)
        {
            var t = new List<(string, Color)>();
            if (d.Flying) t.Add(("飞行怪", Pal.Hex("3B8ED8")));
            else if (d.Speed >= 0.85f) t.Add(("高速怪", Pal.Hex("8652D6")));
            if (d.StopY > Layout.StopY + 1f) t.Add(("远程怪", Pal.Hex("2E9B68")));
            if (d.PhysResist > 0) t.Add(("重甲", Pal.Hex("5C7084")));
            if (d.Kind == EnemyKind.FireBomb || d.Kind == EnemyKind.Lantern) t.Add(("自爆", Pal.Hex("DF682C")));
            if (d.Kind == EnemyKind.Rattan || d.Kind == EnemyKind.Diver || d.Kind == EnemyKind.Scout) t.Add(("难缠", Pal.Hex("C48824")));
            if (d.Kind == EnemyKind.Taoist) t.Add(("治疗", Pal.Hex("2E9B68")));
            if (d.Kind == EnemyKind.Porter) t.Add(("宝箱", Pal.Hex("D49A1A")));
            return t;
        }

        /// <summary>深底面板用的克制提示。</summary>
        public static string Counter(EnemyDef d)
        {
            var parts = new List<string>();
            if (d.PhysResist > 0) parts.Add($"物理伤害 <color=#FF7868>-{d.PhysResist * 100:0}%</color>");
            if (d.MagicWeak > 0) parts.Add($"法术伤害 <color=#7DE35A>+{d.MagicWeak * 100:0}%</color>");
            if (d.ArcherWeak > 0) parts.Add($"射手伤害 <color=#7DE35A>+{d.ArcherWeak * 100:0}%</color>");
            if (d.Flying) parts.Add("<color=#FFB85C>近战无法命中</color>");
            return string.Join("，", parts);
        }

        static readonly Regex KeyRx = new Regex(@"(物理伤害减半|物理减伤|战士打不到|战士够不着|射手克制|怕火雷风|冲到墙下自爆|飘到墙上就炸|潜着时刀枪不入|挡下最先的两次攻击|给身边官军回血|受伤减半，狂奔冲墙|远远停下抛石砸墙|闪身躲开|藏着三个官兵|飞锤砸晕好汉|冲锋突进，残血狂暴|召来连环骑兵|踢火球砸墙，带护盾|召唤水鬼|金光护体|扑跃突进|猛撞寨墙|弓手齐射)");

        /// <summary>浅瓷底「本关敌情」用的机制高亮说明：关键特性用醒目琥珀橙标出。</summary>
        public static string SheetBody(EnemyDef d)
        {
            string desc = KeyRx.Replace(d.Desc, "<color=#D66816>$1</color>");
            var parts = new List<string>();
            if (d.PhysResist > 0 && !d.Desc.Contains("减")) parts.Add($"<color=#D66816>免疫{d.PhysResist * 100:0}%物理伤害</color>");
            if (d.MagicWeak > 0) parts.Add($"<color=#1E9438>受法术伤害+{d.MagicWeak * 100:0}%</color>");
            if (d.ArcherWeak > 0 && !d.Desc.Contains("射手")) parts.Add($"<color=#1E9438>受射手伤害+{d.ArcherWeak * 100:0}%</color>");
            return parts.Count > 0 ? desc + "，" + string.Join("、", parts) : desc;
        }
    }
}

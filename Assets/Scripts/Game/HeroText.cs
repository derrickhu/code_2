using Liangshan.Sim;

namespace Liangshan
{
    /// <summary>好汉技能说明：局内信息面板和局外好汉详情共用。传入 Battle 时带上本局强化后的数值。</summary>
    public static class HeroText
    {
        public static string O(object s) => $"<color=#FF9A3A>{s}</color>";
        static string P(float v) => $"{v * 100:0.#}%";
        static string Sec(float v) => O(v.ToString("0.#") + "秒");

        public static string Atk(HeroDef d, Battle b = null)
        {
            var h = d.Id;
            int count = b?.CountOf(h) ?? d.Count;
            int chain = b?.ChainOf(h) ?? d.Chain;
            float area = b?.AreaOf(h) ?? 1f;
            float stun, chance;
            if (b != null) (stun, chance) = b.StunOf(h);
            else { stun = d.Stun > 0 ? d.Stun : 0.8f; chance = d.Stun > 0 && d.StunChance <= 0 ? 1f : d.StunChance; }
            float slow = b?.SlowOf(h) ?? d.Slow;
            string dmg = $"{O(P(d.Coef))}攻击力的{d.Element}伤害";
            string t;
            switch (d.Shot)
            {
                case ShotKind.Arrow:
                    t = $"向前排射出{O(count)}支箭，每支造成{dmg}" + (chain > 0 ? $"，穿透{O(chain)}个敌人" : "");
                    break;
                case ShotKind.Bomb:
                    t = $"投掷{O(count)}枚炸弹，对半径{O((d.Radius * area).ToString("0.#"))}内的敌人造成{dmg}";
                    break;
                case ShotKind.Fireball:
                    t = $"掷出{O(count)}颗火球，落地爆炸对半径{O((d.Radius * area).ToString("0.#"))}内的敌人造成{dmg}";
                    break;
                case ShotKind.Lightning:
                    t = $"召雷劈中敌人，造成{dmg}" + (chain > 0 ? $"，再连锁{O(chain)}个敌人" : "");
                    break;
                case ShotKind.Punch:
                    t = $"只打墙前近身的敌人，出拳{O(count)}次，每次造成{dmg}并击退";
                    break;
                case ShotKind.Slam:
                    t = $"砸地震击周围敌人，造成{dmg}并震退";
                    break;
                case ShotKind.Spear:
                    t = $"长枪贯穿目标和身后一列敌人，造成{dmg}";
                    break;
                case ShotKind.Stone:
                    t = $"掷出飞石造成{dmg}" + (chain > 0 ? $"，再弹射{O(chain)}次（每次{O("80%")}）" : "");
                    break;
                case ShotKind.Lasso:
                    t = $"甩出套索缚住{O(count)}个敌人，造成{dmg}，定身{Sec(stun)}";
                    break;
                default:
                    t = $"风刃卷向敌群，对目标造成{dmg}，周围敌人受一半伤害";
                    break;
            }
            if (d.Shot != ShotKind.Lasso && chance > 0)
                t += chance >= 1f ? $"，击晕{Sec(stun)}" : $"，{O(P(chance))}几率击晕{Sec(stun)}";
            if (slow > 0) t += $"，减速{Sec(slow)}";
            if (d.Aura != AuraKind.None) t += "\n" + Aura(d, b);
            return t;
        }

        public static string Aura(HeroDef d, Battle b = null)
        {
            string v = O(P(d.AuraV));
            string t;
            switch (d.Aura)
            {
                case AuraKind.Speed: t = $"光环：在场每星为全军加{v}攻速" + (b != null ? $"（当前{O("+" + P(b.Aura))}）" : ""); break;
                case AuraKind.Dmg: t = $"光环：在场每星为全军加{v}伤害" + (b != null ? $"（当前{O("+" + P(b.DmgAura))}）" : ""); break;
                case AuraKind.Charge: t = $"光环：在场每星让全军大招充能加快{v}" + (b != null ? $"（当前{O("+" + P(b.ChargeAura))}）" : ""); break;
                case AuraKind.Heal: t = $"每次出手为寨墙回复最大生命的{v}×星级"; break;
                case AuraKind.Coin: t = $"光环：在场每星让击杀银两加{v}" + (b != null ? $"（当前{O("+" + P(b.CoinAura))}）" : ""); break;
                case AuraKind.Interest: t = $"每4秒获得当前银两{v}×星级的利息（单次最多25）"; break;
                default: return "";
            }
            return t;
        }

        /// <summary>pow：大招威力倍率（等级 25 的 +50%、强化加成）。</summary>
        public static string Ult(HeroDef d, float pow = 1f)
        {
            string dmg = $"{O(P(d.Coef * d.UltCoef * pow))}攻击力";
            string n = O(d.UltN);
            switch (d.Ult)
            {
                case UltKind.Rain: return $"火箭齐射{n}支，覆盖敌群最密处，每支造成{dmg}伤害";
                case UltKind.BigBomb: return $"砸下一枚巨型火炮，对大范围敌人造成{dmg}伤害并震退";
                case UltKind.Thunder: return $"{n}道天雷劈向敌群，每道造成{dmg}伤害并麻痹{Sec(d.UltStun)}";
                case UltKind.Barrage: return $"连环出拳{n}击，每击造成{dmg}伤害并击退";
                case UltKind.Volley: return $"{n}枚飞石连打前排，每枚造成{dmg}伤害并击晕{Sec(d.UltStun)}";
                case UltKind.Arrows: return $"连珠{n}箭射向前排，每箭造成{dmg}伤害";
                case UltKind.Frost: return $"风雪冰封一片敌人，造成{dmg}伤害，冻住{Sec(d.UltStun)}";
                case UltKind.Sweep: return $"横扫墙前一整排，{n}段各造成{dmg}伤害并击飞";
                case UltKind.Haste: return $"全军攻速{O("+60%")}持续{Sec(6f * pow)}，并获得银两";
                case UltKind.Heal: return $"为寨墙回复{O(P(d.UltCoef * pow))}最大生命，并解除所有好汉的晕眩";
                case UltKind.Bind: return $"同时套住{n}个敌人（优先精英、首领），每个造成{dmg}伤害并定身{Sec(d.UltStun)}";
                case UltKind.Snipe: return $"瞄准场上最强的敌人，一箭造成{dmg}伤害";
                case UltKind.Tornado: return $"召出龙卷风，范围内敌人受到{dmg}伤害并被卷起{Sec(d.UltStun)}";
                case UltKind.Meteor: return $"天降{n}颗火流星，每颗造成{dmg}范围伤害";
                case UltKind.FireLine: return $"沿墙前布下{n}团烈火，每团造成{dmg}范围伤害";
                case UltKind.Formation: return $"布下八门金锁阵：全场敌人受到{dmg}伤害，减速{Sec(4f * pow)}，{Sec(6f * pow)}内受到伤害{O("+25%")}";
                case UltKind.Net: return $"撒下大网，范围内敌人受到{dmg}伤害并定身{Sec(d.UltStun)}";
                case UltKind.Powder: return $"洒出蒙汗药，范围内敌人受到{dmg}伤害并麻倒{Sec(d.UltStun)}";
                case UltKind.Wave: return $"引水冲阵，两排水浪各造成{dmg}伤害并减速";
                case UltKind.Brick: return $"祭起金砖砸向最强的敌人，造成{dmg}伤害并击晕{Sec(d.UltStun)}";
                case UltKind.Hook: return $"钩镰枪横扫墙前{n}处，每处造成{dmg}伤害并绊倒{Sec(d.UltStun)}";
                case UltKind.Steal: return $"偷来银两：{O("20+10×星级+4×波次")}";
                case UltKind.Interest: return $"立即获得当前银两{O(P(d.UltCoef * pow))}的利息（{O("30~400")}）";
                case UltKind.Forge: return $"打造神兵：{Sec(6)}内全军伤害{O("+" + P(d.UltCoef * pow))}";
                case UltKind.Charge: return $"神行甲马：其余好汉大招冷却立即缩短{O(P(d.UltCoef))}，全军攻速{O("+30%")}持续{Sec(4f * pow)}";
            }
            return "";
        }

        public static string Passive(HeroDef d) => Defs.EffText(d.Id, d.PassiveEff, d.PassiveV);

        public static string Tip(HeroDef d) =>
            d.Melee ? "近战：打不到飞行敌人" : d.Physical ? "物理：被重甲削弱" : "法术：克制重甲";
    }
}

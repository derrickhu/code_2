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
            if (d.Buff.HasValue) return Buff(d, b);
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
            if (d.Chan > 0)
            {
                float chan = d.Chan + (b != null ? Defs.ChanPerCount * b.Stat(h, Eff.Count) : 0);
                string head = $"持续施法{Sec(chan)}，每{O(d.Tick.ToString("0.##"))}秒";
                switch (d.Shot)
                {
                    case ShotKind.Beam: t = $"{head}灼烧射线上的敌人，目标受到{dmg}，射线路过的敌人受{O("60%")}"; break;
                    case ShotKind.Lightning: t = $"{head}一道天雷随机劈向前排，造成{dmg}" + (chain > 0 ? $"并连锁{O(chain)}个敌人" : ""); break;
                    case ShotKind.Wind: t = $"{head}在目标处绞杀，对周围敌人造成{dmg}"; break;
                    default: t = $"{head}祭出金砖砸向敌人，造成{dmg}" + (chain > 0 ? $"，再弹射{O(chain)}次" : ""); break;
                }
            }
            else
            {
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
                        t = $"只打墙前近身的敌人，连击{O(count)}次，每次造成{dmg}并击退";
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
            }
            if (d.Shot != ShotKind.Lasso && chance > 0)
                t += chance >= 1f ? $"，击晕{Sec(stun)}" : $"，{O(P(chance))}几率击晕{Sec(stun)}";
            if (slow > 0) t += $"，减速{Sec(slow)}";
            if (d.Chan <= 0 && d.Count > 1) t += $"\n1星每轮只出{O(1)}发，每升1星多1发，最多{O(System.Math.Min(d.Count, 4))}发（强化另加）";
            string aura = Aura(d, b);
            if (aura.Length > 0) t += "\n" + aura;
            return t;
        }

        /// <summary>辅助的技能：给队友加持，不打伤害。</summary>
        static string Buff(HeroDef d, Battle b)
        {
            float boost = b?.BuffBoost(d.Id) ?? 1f;
            string t;
            switch (d.Buff.Value)
            {
                case UltKind.Bless:
                    t = $"祝福一位输出好汉（{O("3星")}起两位）：接下来{O("2+星级")}次技能冷却速度{O("+" + P(d.BuffV * boost))}";
                    break;
                case UltKind.Rush:
                    t = $"其余好汉的技能和大招冷却立即缩短{O(P(d.BuffV * boost))}（每星再{O("+10%")}）";
                    break;
                case UltKind.Mend:
                    t = $"为寨墙回复最大生命的{O(P(d.BuffV * boost))}×星级，并解除所有好汉的晕眩";
                    break;
                case UltKind.Forge:
                    t = $"打造神兵：{Sec(8)}内全军伤害{O("+" + P(d.BuffV * boost))}起（星级越高越多）";
                    break;
                case UltKind.Steal:
                    t = $"偷来银两：{O("20+10×星级+4×波次")}" + (boost > 1.01f ? $"，再{O("×" + boost.ToString("0.##"))}" : "");
                    break;
                case UltKind.Interest:
                    t = $"立即获得当前银两{O(P(d.BuffV * boost))}的利息（{O("30~400")}）";
                    break;
                default: t = ""; break;
            }
            string aura = Aura(d, b);
            if (aura.Length > 0) t += "\n" + aura;
            return t;
        }

        public static string Aura(HeroDef d, Battle b = null)
        {
            string v = O(P(d.AuraV));
            switch (d.Aura)
            {
                case AuraKind.Speed: return $"光环：在场每星为全军加{v}攻速" + (b != null ? $"（当前{O("+" + P(b.Aura))}）" : "");
                case AuraKind.Dmg: return $"光环：在场每星为全军加{v}伤害" + (b != null ? $"（当前{O("+" + P(b.DmgAura))}）" : "");
                case AuraKind.Charge: return $"光环：在场每星让全军冷却加快{v}" + (b != null ? $"（当前{O("+" + P(b.ChargeAura))}）" : "");
                case AuraKind.Coin: return $"光环：在场每星让击杀银两加{v}" + (b != null ? $"（当前{O("+" + P(b.CoinAura))}）" : "");
                case AuraKind.Interest: return $"每4秒获得当前银两{v}×星级的利息（单次最多25）";
                default: return "";
            }
        }

        /// <summary>pow：大招威力倍率（强化加成）。</summary>
        public static string Ult(HeroDef d, float pow = 1f)
        {
            if (!d.HasUlt) return "仅传说好汉拥有大招";
            string dmg = $"{O(P(d.UltCoef * pow))}攻击力";
            string n = O(d.UltN);
            switch (d.Ult)
            {
                case UltKind.Thunder: return $"{n}道天雷劈向敌群，每道造成{dmg}伤害并麻痹{Sec(d.UltStun)}";
                case UltKind.Frost: return $"风雪冰封一片敌人，造成{dmg}伤害，冻住{Sec(d.UltStun)}";
                case UltKind.Sweep: return $"横扫墙前一整排，{n}段各造成{dmg}伤害并击飞";
                case UltKind.Haste: return $"全军攻速{O("+60%")}持续{Sec(8f * pow)}";
                case UltKind.Heal: return $"为寨墙回复{O(P(d.UltCoef * pow))}最大生命，并解除所有好汉的晕眩";
                case UltKind.Bind: return $"同时套住{n}个敌人（优先精英、首领），每个造成{dmg}伤害并定身{Sec(d.UltStun)}";
                case UltKind.Snipe: return $"瞄准场上最强的敌人，一箭造成{dmg}伤害";
                case UltKind.Tornado: return $"召出龙卷风，范围内敌人受到{dmg}伤害并被卷起{Sec(d.UltStun)}";
                case UltKind.Meteor: return $"天降{n}颗火流星，每颗造成{dmg}范围伤害";
            }
            return "";
        }

        public static string Passive(HeroDef d) => Defs.EffText(d.Id, d.PassiveEff, d.PassiveV);

        public static string Tip(HeroDef d) =>
            d.Buff.HasValue ? "辅助：不打伤害，专给队友加持" :
            d.Melee ? "近战：打不到飞行敌人" : d.Physical ? "物理：被重甲削弱" : "法术：克制重甲";
    }
}

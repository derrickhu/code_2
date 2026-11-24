using System;

namespace Liangshan.Sim
{
    /// <summary>1 级常驻的个人特性类别（数值小巧克制，不污染局外 1 星白板首击断言，又让每位好汉从 1 级起就有独门脾气）。</summary>
    public enum TraitKind
    {
        LowHpDmg,       // 对血量 <40% 敌人增伤 V
        EliteBonus,     // 对精英、首领增伤 V
        FlyBonus,       // 对飞行敌人增伤 V
        CavBonus,       // 对骑兵、连环马增伤 V
        FirstPierceCrit,// 贯穿箭/长矛命中的首个目标暴击率 +V
        NthShotCrit,    // 每第 N 轮出手暴击率 +V
        KnockBonus,     // 击退距离 +V
        SplashSlow,     // 命中额外减速 V 秒
        DotBonus,       // 持续灼烧/毒伤 +V
        ZoneVuln,       // 命中附带 2.5 秒微易伤 +V
        AdjacentSpeed,  // 左右相邻好汉攻速 +V（吴用）
        WildMerge,      // 召唤与合成呈百搭态几率 V（时迁）
        CleanseWall,    // 回墙额外回复并净除异常（安道全）
        UltAuraCd,      // 冷却光环同步作用于大招（戴宗）
        CritDmgAura,    // 在场为全军暴击伤害 +V（汤隆）
        InterestCap,    // 利息结算上限提高 +V（蒋敬）
        PriorityElite,  // 套索与渔网优先锁定精英/飞禽（扈三娘/阮小七）
    }

    /// <summary>10 级自动拥有的本命绝技模板（12 大机制原型，彻底告别纯数值被动与抽卡看脸）。</summary>
    public enum SigKind
    {
        OnMerge,        // 参与合成时当场爆发（6 位辅助专属：抛祝福 / 回墙 / 减全队冷却 / 锻造 / 偷银 / 结息）
        CountGE,        // 场上同名好汉 ≥ N 位时激活常驻质变（燕青多1矢、花荣多1穿、孔亮多1雷）
        StarSum,        // 场上同名好汉总星级每 1 星提供 V 伤害增幅（公孙胜、樊瑞，上限 12 星）
        TopStar,        // 场上最高星那一位独享统帅质变（卢俊义枪罡扩幅、关胜斩精英狂暴）
        WallDrop,       // 寨墙每损失 10% 生命，全场同名好汉立刻齐射反击（鲁智深连砸、李逵连斧）
        NearRage,       // 墙根警戒线内有敌军时进入怒战（武松攻速爆发、石秀暴击爆发）
        HighHpBreak,    // 破势：对生命高于 50% 的敌军造成伤害 +V（林冲、徐宁）
        LowHpExec,      // 斩将：对生命低于 40% 的残血敌军造成伤害 +V
        OnDebuff,       // 追魂：对灼烧/中毒/减速/眩晕中的敌军伤害 +V（解宝、魏定国、孙二娘、单廷珪、乔道清）
        EveryNCast,     // 节拍极意：每第 N 轮出手触发强化巨击/双发/必晕（凌振、张清、马灵、解珍）
        OnStunBurst,    // 锁魂爆裂：控制命中时额外引爆追斩与破绽易伤（扈三娘、朱武、阮小七）
        DeployBurst,    // 天降神威：上阵或合成登场瞬间倾泻 3 枚火陨轰炸（秦明）
    }

    /// <summary>每位好汉完整的四技能与 5~30 级定制突破档案（数值统一接入 BalanceConfig 数值管理中心）。</summary>
    public sealed class HeroKitDef
    {
        public HeroId Hero;

        // 槽位 2：1 级天生特性
        public string TraitName, TraitIcon, TraitDesc, TraitTpl;
        public TraitKind Trait;
        public float TraitV;
        public int TraitN;

        // 槽位 3：10 级自动获得的绝技
        public string SigName, SigIcon, SigDesc, SigTpl;
        public SigKind Sig;
        public int SigN;
        public float SigV, SigV2;

        // 槽位 4：5 级进池的终结技（传说为大招，史诗/稀有为奥义）
        public string FinisherName, FinisherIcon, FinisherDesc, FinisherTpl;

        // 15 级解锁的专属双词条强化选项卡（Opt = 3，双属性全部真实生效，与单属性普通卡形成差异化互补）
        public string Opt15Name, Opt15Desc, Opt15Tpl;
        public Eff Opt15Eff;
        public float Opt15V;
        public Eff Opt15Eff2;
        public float Opt15V2;

        // 20 / 25 / 30 级专属机制改写与突破说明
        public string Lv20Desc;
        public string Lv25Desc;
        public string Lv30Desc;
    }

    public static class HeroKits
    {
        public static readonly HeroKitDef[] All = Build();

        public static HeroKitDef Of(HeroId h) => All[(int)h];

        static string Pct(float v) => (int)Math.Round(v * 100f) + "%";
        static string Num(float v) => v.ToString("0.##");

        /// <summary>当数值管理中心修改了特性/绝技/大招/奥义/15级专属卡数值时，同步刷新全部说明文本。</summary>
        public static void RefreshAll()
        {
            if (All == null) return;
            foreach (var k in All)
            {
                if (k != null) Refresh(k);
            }
        }

        public static void Refresh(HeroKitDef k)
        {
            var d = Defs.Hero(k.Hero);
            k.TraitDesc = FormatTpl(k, k.TraitTpl);
            k.SigDesc = FormatTpl(k, k.SigTpl);
            k.Opt15Desc = FormatTpl(k, k.Opt15Tpl);
            if (d.HasUlt)
                k.FinisherDesc = $"{d.UltTip.Replace("\n", "，")}（冷却 {d.UltCd:0} 秒）";
            else if (!string.IsNullOrEmpty(k.FinisherTpl))
                k.FinisherDesc = FormatTpl(k, k.FinisherTpl);
        }

        static string FormatTpl(HeroKitDef k, string tpl)
        {
            if (string.IsNullOrEmpty(tpl)) return "";
            var cb = Balance.Current != null && Balance.Current.Combat != null ? Balance.Current.Combat : new CombatBalance();
            return tpl
                .Replace("{tv%}", Pct(k.TraitV))
                .Replace("{tv}", Num(k.TraitV))
                .Replace("{tn}", k.TraitN.ToString())
                .Replace("{sn}", k.SigN.ToString())
                .Replace("{sv%}", Pct(k.SigV))
                .Replace("{sv}", Num(k.SigV))
                .Replace("{sv2%}", Pct(k.SigV2))
                .Replace("{sv2}", Num(k.SigV2))
                .Replace("{sn_sv%}", Pct(k.SigN * k.SigV))
                .Replace("{ov%}", Pct(k.Opt15V))
                .Replace("{ov}", Num(k.Opt15V))
                .Replace("{ov2%}", Pct(k.Opt15V2))
                .Replace("{ov2}", Num(k.Opt15V2))
                .Replace("{awkDmg%}", Pct(cb.AwakenDmgBonus))
                .Replace("{awkSpd%}", Pct(cb.AwakenSpeedBonus))
                .Replace("{awkAura%}", Pct(cb.AwakenAuraBonus));
        }

        static HeroKitDef K(
            HeroId h,
            string traitName, string traitIcon, TraitKind trait, float tv, int tn, string traitTpl,
            string sigName, string sigIcon, SigKind sig, int sn, float sv, float sv2, string sigTpl,
            string finIcon, string finTpl,
            string o15Name, Eff o15Eff, float o15V, Eff o15Eff2, float o15V2, string o15Tpl,
            string lv20, string lv25, string lv30 = null)
        {
            var d = Defs.Hero(h);
            bool sup = d.Kind == Role.Support;
            var kit = new HeroKitDef
            {
                Hero = h,
                TraitName = traitName, TraitIcon = traitIcon, Trait = trait, TraitV = tv, TraitN = tn, TraitTpl = traitTpl,
                SigName = sigName, SigIcon = sigIcon, Sig = sig, SigN = sn, SigV = sv, SigV2 = sv2, SigTpl = sigTpl,
                FinisherName = d.HasUlt ? d.UltName : Defs.AwakenName(h),
                FinisherIcon = d.HasUlt ? d.UltIcon : finIcon,
                FinisherTpl = finTpl,
                Opt15Name = o15Name, Opt15Eff = o15Eff, Opt15V = o15V, Opt15Eff2 = o15Eff2, Opt15V2 = o15V2, Opt15Tpl = o15Tpl,
                Lv20Desc = lv20,
                Lv25Desc = lv25,
                Lv30Desc = lv30 ?? (sup ? $"「{d.AtkName}」冷却 -20%"
                    : d.Kind == Role.Mage ? $"「{d.AtkName}」回气速率 +20%"
                    : d.Kind == Role.Bomber ? $"「{d.AtkName}」装填速度 +20%"
                    : d.Kind == Role.Control ? $"「{d.AtkName}」布阵速度 +20%"
                    : $"{d.Name}攻击速度 +20%"),
            };
            Refresh(kit);
            return kit;
        }

        static HeroKitDef[] Build()
        {
            var a = new HeroKitDef[Defs.HeroCount];
            void Put(HeroKitDef k) => a[(int)k.Hero] = k;

            // ==================== 传说（10 位，第 4 技能为大招） ====================
            Put(K(HeroId.GongSun,
                "天罡雷法", "sk_trait_elem", TraitKind.ZoneVuln, 0.12f, 0, "落雷劈中的敌人感电 2.5 秒，所受伤害 +{tv%}",
                "入云龙", "sk_sig_legion", SigKind.StarSum, 12, 0.04f, 0, "场上公孙胜总星级每 1 星，天雷与大招伤害 +{sv%}（上限 {sn_sv%}）",
                null, null,
                "掌心神雷", Eff.Count, 1f, Eff.Stun, 0.25f, "掌心再引一雷\n落雷数量 +{ov}，麻痹几率 +{ov2%}",
                "释放「五雷天罡」后 6 秒内，公孙胜回气速率 +35%",
                "「五雷天罡」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.LinChong,
                "寒星枪芒", "sk_trait_crit", TraitKind.FirstPierceCrit, 0.18f, 0, "丈八蛇矛刺中的首个敌人暴击率 +{tv%}",
                "禁军教头", "sk_sig_combo", SigKind.HighHpBreak, 0, 0.35f, 0, "破势：对生命高于 50% 的敌军，长枪与大招伤害 +{sv%}",
                null, null,
                "雪夜寒锋", Eff.Shatter, 0.60f, Eff.Crit, 0.22f, "寒锋专破受制之敌\n对减速眩晕伤害 +{ov%}，暴击率 +{ov2%}",
                "2 星及以上林冲合成登场时，立刻释放 1 道寒霜裂空枪",
                "「风雪山神庙」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.LuJunyi,
                "河北枪绝", "sk_trait_exec", TraitKind.KnockBonus, 0.35f, 0, "麒麟枪劲力雄浑，击退距离 +{tv%}",
                "麒麟镇岳", "sk_sig_crown", SigKind.TopStar, 0, 0.28f, 0.20f, "场上最高星的卢俊义统御战局：伤害 +{sv%}，暴击率 +{sv2%}",
                null, null,
                "枪挑三关", Eff.Reach, 2.2f, Eff.Dmg, 0.45f, "麒麟枪挑三关\n贯穿距离 +{ov}，长枪伤害 +{ov2%}",
                "释放「麒麟踏阵」时，额外向敌群斩出 2 道黄金裂阵枪罡",
                "「麒麟踏阵」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.GuanSheng,
                "青龙斩将", "sk_trait_exec", TraitKind.LowHpDmg, 0.35f, 0, "对生命低于 40% 的敌军，偃月刀斩杀伤害 +{tv%}",
                "武圣遗风", "sk_sig_crown", SigKind.TopStar, 0, 0.30f, 0.25f, "场上最高星的关胜威震敌胆：对全敌军伤害 +{sv%}，对首领额外 +{sv2%}",
                null, null,
                "单刀赴会", Eff.Elite, 0.90f, Eff.Crit, 0.25f, "威震华夏斩敌酋\n对精英首领伤害 +{ov%}，暴击率 +{ov2%}",
                "「拖刀计」横扫期间，生命低于 12% 的普通官军直接斩立决",
                "「拖刀计」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.YanQing,
                "巧手神机", "sk_trait_crit", TraitKind.NthShotCrit, 0.45f, 3, "机巧连弩每第 {tn} 轮齐射暴击率 +{tv%}",
                "浪子连弩", "sk_sig_legion", SigKind.CountGE, 4, 1f, 0, "场上同时存在 ≥{sn} 位燕青时，全体燕青每次齐射弩矢数量 +1",
                null, null,
                "追风连矢", Eff.Speed, 0.42f, Eff.Crit, 0.18f, "连弩装填提速\n攻速 +{ov%}，暴击率 +{ov2%}",
                "释放「一箭封喉」后 5 秒内，全场燕青攻速 +45%",
                "「一箭封喉」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.FanRui,
                "蚀骨妖风", "sk_trait_elem", TraitKind.SplashSlow, 0.8f, 0, "旋风咒与龙卷减速时间额外延长 {tv} 秒",
                "混世魔王", "sk_sig_legion", SigKind.StarSum, 12, 0.05f, 0, "场上樊瑞总星级每 1 星，旋风与大招伤害 +{sv%}（上限 {sn_sv%}）",
                null, null,
                "裂空风刃", Eff.Area, 0.45f, Eff.Dmg, 0.45f, "旋风外围激发切割风刃\n作用范围 +{ov%}，旋风伤害 +{ov2%}",
                "「飞沙走石」龙卷范围再 +25%，并把周围敌军向风眼聚拢",
                "「飞沙走石」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.HuSanniang,
                "红锦锁将", "sk_trait_tactic", TraitKind.PriorityElite, 0.25f, 0, "缚龙索优先锁定精英与首领，并对其伤害 +{tv%}",
                "日月双刀", "sk_sig_seal", SigKind.OnStunBurst, 0, 0.90f, 0.20f, "被套索缚住的敌军遭双刀追斩（{sv%} 攻击伤害），4 秒内受伤 +{sv2%}",
                null, null,
                "连环金索", Eff.Count, 1f, Eff.Stun, 0.45f, "红锦副索齐发\n同时多锁 {ov} 人，控制时间 +{ov2} 秒",
                "「阵前擒将」套住的敌军易伤提升至 +30%，定身延长 0.5 秒",
                "「阵前擒将」冷却 -15%，技能伤害 +15%"));

            Put(K(HeroId.AnDaoquan,
                "杏林春暖", "sk_trait_tactic", TraitKind.CleanseWall, 0.02f, 0, "妙手回春额外多回复 {tv%} 寨墙上限，并立即清零全体好汉晕眩",
                "神医圣手", "sk_sig_merge", SigKind.OnMerge, 0, 0.03f, 0, "安道全参与合成升星时，额外多回复 {sv%}×新星级 寨墙生命（总计 8%×星级）",
                null, null,
                "金针渡穴", Eff.Aura, 0.55f, Eff.UltCd, 0.18f, "回墙药力精纯\n回墙量 +{ov%}，技能冷却 -{ov2%}",
                "寨墙生命低于 40% 时，「妙手回春」回复量提升 40%",
                "「妙手回春」效果 +15%，「悬壶济世」冷却 -15%"));

            Put(K(HeroId.DaiZong,
                "甲马神行", "sk_trait_tactic", TraitKind.UltAuraCd, 0.04f, 0, "戴宗在场冷却光环同步加速全军大招回转",
                "日行八百", "sk_sig_merge", SigKind.OnMerge, 0, 0.18f, 0, "戴宗参与合成升星时，全军其余好汉当前技能与大招冷却立刻缩短 {sv%}",
                null, null,
                "八百里加急", Eff.UltCd, 0.24f, Eff.Coin, 32f, "甲马符催到极致\n技能冷却 -{ov%}，合成摸银 +{ov2}",
                "释放「日行千里」时，额外立刻缩短全军技能冷却 20%",
                "「神行甲马」效果 +15%，「日行千里」冷却 -15%"));

            Put(K(HeroId.QinMing,
                "烈火燎原", "sk_trait_elem", TraitKind.DotBonus, 0.12f, 0, "霹雳火陨留下的熔岩灼烧总伤害额外 +{tv%}",
                "统制神威", "sk_sig_crown", SigKind.DeployBurst, 3, 1.15f, 0, "秦明召唤入场或合成升星登场时，当场向敌群轰落 {sn} 颗霹雳火陨（{sv}×攻击）",
                null, null,
                "狼牙星陨", Eff.Split, 1f, Eff.Dmg, 0.40f, "火陨砸地分裂 3 枚小火球\n且火陨伤害 +{ov2%}",
                "「霹雳天火」流星数量 +2，灼烧敌军阵亡时引爆火环",
                "「霹雳天火」冷却 -15%，技能伤害 +15%"));

            // ==================== 史诗（12 位，第 4 技能为奥义） ====================
            Put(K(HeroId.HuaRong,
                "银镝穿云", "sk_trait_crit", TraitKind.FirstPierceCrit, 0.10f, 0, "穿云箭命中的首个目标暴击率 +{tv%}，每穿透 1 敌伤害递增 18%",
                "百步穿杨", "sk_sig_legion", SigKind.CountGE, 3, 1f, 0, "场上同时存在 ≥{sn} 位花荣时，全体花荣穿云箭穿透人数 +1",
                "sk_aoyi_arrow", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场齐射贯阵箭雨，之后每 3 轮普攻额外迸发 3 支穿云追魂箭",
                "雕翎急弦", Eff.Count, 1f, Eff.Crit, 0.22f, "雕翎急弦连发\n箭数量 +{ov}，暴击率 +{ov2%}",
                "场上满 4 位花荣时穿透再 +1，穿透递增伤害升至 +26%",
                "「穿云箭」伤害 +15%，奥义「雁落平沙」追射伤害 +35%"));

            Put(K(HeroId.LingZhen,
                "重炮轰雷", "sk_trait_exec", TraitKind.KnockBonus, 0.12f, 0, "虎蹲重炮气浪猛烈，震退距离 +{tv%}",
                "火药精通", "sk_sig_combo", SigKind.EveryNCast, 3, 1.45f, 0.35f, "每第 {sn} 轮发射升级为红衣巨炮：伤害 ×{sv}，爆炸范围 +{sv2%}",
                "sk_aoyi_fire", "重炮终伤 +{awkDmg%}，装填速度 +{awkSpd%}；当场倾泻重炮弹幕，之后每 3 轮普攻额外轰出 4 枚连环火炮",
                "神机火药", Eff.Count, 1f, Eff.Dmg, 0.40f, "神机火药连发\n重炮数量 +{ov}，重炮伤害 +{ov2%}",
                "「火药精通」巨炮触发节拍加快为每第 2 轮，必带震晕 0.5 秒",
                "「霹雳重炮」伤害 +15%，奥义「火烧连营」弹幕伤害 +35%"));

            Put(K(HeroId.WuSong,
                "行者虎威", "sk_trait_exec", TraitKind.EliteBonus, 0.12f, 0, "拳风刚猛无俦，对精英与首领伤害 +{tv%}",
                "景阳冈打虎", "sk_sig_bastion", SigKind.NearRage, 0, 0.32f, 0, "城墙前警戒线内有官军逼近时，武松战意沸腾，攻速 +{sv%}",
                "sk_aoyi_blade", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场轰出崩山双拳罡，之后每 3 轮普攻额外斩出双道裂地拳气",
                "景阳醉拳", Eff.Crit, 0.25f, Eff.Speed, 0.35f, "醉步连环崩拳\n暴击率 +{ov%}，攻速 +{ov2%}",
                "近身怒战期间，武松铁拳暴击率额外 +20%",
                "「打虎铁拳」伤害 +15%，奥义「连环十八跌」拳气伤害 +35%"));

            Put(K(HeroId.ZhangQing,
                "青玉巧劲", "sk_trait_crit", TraitKind.NthShotCrit, 0.40f, 3, "没羽飞石手法刁钻，每第 {tn} 轮掷石暴击率 +{tv%}",
                "百发百中", "sk_sig_combo", SigKind.EveryNCast, 3, 2f, 0.6f, "每第 {sn} 轮掷出流光奇石：多弹射 {sv} 次，命中必击晕 {sv2} 秒",
                "sk_aoyi_arrow", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场激射满天青玉石雨，之后每 3 轮普攻额外连掷双发弹射飞石",
                "漫天青玉", Eff.Chain, 2f, Eff.Dmg, 0.40f, "袖藏连珠青石\n飞石弹射 +{ov} 次，伤害 +{ov2%}",
                "飞石每次弹射不再衰减至 80%，改为维持 95% 高额伤害",
                "「飞石」伤害 +15%，奥义「流星赶月」追击伤害 +35%"));

            Put(K(HeroId.LuZhiShen,
                "金刚伏魔", "sk_trait_elem", TraitKind.SplashSlow, 1.0f, 0, "水磨禅杖震裂大地，被砸中的敌军额外减速 {tv} 秒",
                "大闹五台山", "sk_sig_bastion", SigKind.WallDrop, 10, 1.2f, 0, "寨墙每损失 10% 生命，场上全体鲁智深怒吼立刻腾空砸地反击（{sv}×伤害）",
                "sk_aoyi_blade", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场轰出佛金裂地罡气，之后每 3 轮普攻额外迸发双道破阵杖罡",
                "疯魔禅杖", Eff.Area, 0.48f, Eff.Slow, 1.2f, "疯魔杖风裂地\n砸地范围 +{ov%}，震荡减速 +{ov2} 秒",
                "寨墙低于 60% 生命时，鲁智深砸地范围 +30%",
                "「水磨禅杖」伤害 +15%，奥义「倒拔垂杨柳」杖罡伤害 +35%"));

            Put(K(HeroId.WuYong,
                "羽扇指阵", "sk_trait_tactic", TraitKind.AdjacentSpeed, 0.16f, 0, "羽扇指点战局：吴用左右相邻两格的好汉常驻攻速 +{tv%}",
                "锦囊妙计", "sk_sig_merge", SigKind.OnMerge, 0, 20f, 0, "吴用参与合成升星时，额外献计 +{sv} 银两，并立刻向 1 位输出好汉抛出祝福法球",
                "sk_aoyi_aid", "光环与祝福效力 +{awkAura%}，号令回转 +{awkSpd%}；觉醒瞬间立刻为至多 2 位输出好汉降下满层神速祝福",
                "奇门借风", Eff.Coin, 35f, Eff.UltPow, 0.35f, "奇门借风聚财\n每次合成 +{ov} 银两，祝福效力 +{ov2%}",
                "被祝福的好汉参与合成升星时，剩余祝福层数无损继承给新好汉",
                "「运筹帷幄」效果 +15%，奥义「八方风雨」增益再 +25%"));

            Put(K(HeroId.LiKui,
                "蛮牛破甲", "sk_trait_exec", TraitKind.LowHpDmg, 0.30f, 0, "凶悍嗜战：对生命低于 40% 的敌军板斧伤害 +{tv%}",
                "黑旋风", "sk_sig_bastion", SigKind.WallDrop, 10, 1.15f, 0, "寨墙每损失 10% 生命，场上全体李逵狂怒立刻旋斩飞斧清扫墙根（{sv}×伤害）",
                "sk_aoyi_blade", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场劈出苍青狂风刀浪，之后每 3 轮普攻额外斩出双道旋风斧罡",
                "嗜血双斧", Eff.Speed, 0.42f, Eff.Area, 0.30f, "黑旋风越战越狂\n攻速 +{ov%}，板斧范围 +{ov2%}",
                "墙根警戒线内有敌军时，李逵飞斧回旋范围 +30%",
                "「旋风飞斧」伤害 +15%，奥义「沂岭杀四虎」斧罡伤害 +35%"));

            Put(K(HeroId.QiaoDaoqing,
                "赤焰蚀甲", "sk_trait_elem", TraitKind.DotBonus, 0.30f, 0, "火焰射线引燃真火，命中附带持续灼烧（{tv%} 伤害）",
                "幻术", "sk_sig_seal", SigKind.OnDebuff, 0, 0.38f, 0, "幻火相生：对正处于灼烧、减速状态的敌军，火焰射线伤害 +{sv%}",
                "sk_aoyi_spell", "法术终伤 +{awkDmg%}，回气速率 +{awkSpd%}，持续施法延长 1 秒；觉醒当场激发真火冲击波灼烧前排",
                "九天玄火", Eff.Refract, 1f, Eff.Dmg, 0.45f, "三昧真火折跃\n折射 +{ov} 道副光束，射线伤害 +{ov2%}",
                "持续施法结束那一刻，在终点炸开一团烈焰爆破",
                "「火焰射线」伤害 +15%，奥义「幻魔真火」持续时间再 +0.6 秒"));

            Put(K(HeroId.ZhuWu,
                "奇门破绽", "sk_trait_tactic", TraitKind.ZoneVuln, 0.14f, 0, "踏入八阵图的敌军阵脚大乱，2.5 秒内所受伤害 +{tv%}",
                "八门金锁", "sk_sig_seal", SigKind.OnStunBurst, 0, 0.65f, 0.8f, "八阵图落下时阵心锁敌：对中心敌军定身 {sv2} 秒并追加 {sv%} 阵法伤害",
                "sk_aoyi_bind", "破阵终伤 +{awkDmg%}，布阵速度 +{awkSpd%}；当场展开天罡八卦大阵定身前排，之后每 3 轮普攻额外布下一座副阵",
                "太乙生门", Eff.Area, 0.45f, Eff.Slow, 1.0f, "阵盘扩演八方\n八阵图范围 +{ov%}，减速 +{ov2} 秒",
                "八阵图造成的破绽易伤提升至 +22%，持续延长 1.5 秒",
                "「八阵图」伤害 +15%，奥义「奇门八卦」副阵范围 +35%"));

            Put(K(HeroId.XuNing,
                "破甲金钩", "sk_trait_exec", TraitKind.CavBonus, 0.55f, 0, "祖传钩镰枪专破铁骑，对骑兵与连环马伤害 +{tv%}",
                "金枪班", "sk_sig_combo", SigKind.HighHpBreak, 0, 0.35f, 0, "迎头痛击：对生命高于 50% 的敌军，钩镰枪伤害 +{sv%}",
                "sk_aoyi_blade", "破阵终伤 +{awkDmg%}，布阵速度 +{awkSpd%}；当场刺出金钩裂阵枪芒，之后每 3 轮普攻额外挥出双道破马金罡",
                "雁翎倒钩", Eff.Stun, 0.28f, Eff.Cav, 0.65f, "倒钩锁拿铁骑\n击晕几率 +{ov%}，对骑兵伤害 +{ov2%}",
                "钩镰枪命中骑兵与精英时，将其向城门方向往回强拽 0.5 格",
                "「钩镰枪」伤害 +15%，奥义「金枪破连环」枪罡伤害 +35%"));

            Put(K(HeroId.ShanTinggui,
                "玄水寒潮", "sk_trait_elem", TraitKind.SplashSlow, 0.8f, 0, "圣水冰寒彻骨，减速持续时间额外 +{tv} 秒",
                "圣水灌顶", "sk_sig_seal", SigKind.OnDebuff, 0, 0.40f, 0, "寒潮叠涌：对处于减速、眩晕中的敌军，圣水射线伤害 +{sv%}",
                "sk_aoyi_spell", "法术终伤 +{awkDmg%}，回气速率 +{awkSpd%}，持续施法延长 1 秒；觉醒当场激起寒水狂澜冻结前排",
                "玄冥重水", Eff.Refract, 1f, Eff.Slow, 1.2f, "寒晶分流贯体\n折射 +{ov} 道副光束，减速 +{ov2} 秒",
                "被圣水持续冲刷的敌军，15% 几率凝结冰封 0.6 秒",
                "「圣水射线」伤害 +15%，奥义「弱水三千」持续时间再 +0.6 秒"));

            Put(K(HeroId.WeiDingguo,
                "猛火绝疗", "sk_trait_elem", TraitKind.DotBonus, 0.25f, 0, "猛火油黏附甲胄，灼烧伤害 +{tv%} 并降低妖道治疗 60%",
                "神火焚营", "sk_sig_seal", SigKind.OnDebuff, 0, 0.45f, 0, "火上浇油：对已处于灼烧状态的敌军，油罐直击伤害 +{sv%}",
                "sk_aoyi_fire", "重炮终伤 +{awkDmg%}，装填速度 +{awkSpd%}；灼烧敌军阵亡必连环引爆，且每 3 轮普攻额外抛投 4 枚猛火小油弹",
                "赤磷猛油", Eff.Burn, 0.85f, Eff.Area, 0.35f, "提纯赤磷火油\n持续灼烧 +{ov%}，火罐范围 +{ov2%}",
                "油罐爆炸范围 +25%，铺开的地面火海持续时间延长",
                "「猛火油罐」伤害 +15%，奥义「神火燎原」爆燃伤害 +35%"));

            // ==================== 稀有（10 位，第 4 技能为奥义） ====================
            Put(K(HeroId.ShiXiu,
                "狠辣快刃", "sk_trait_exec", TraitKind.LowHpDmg, 0.15f, 0, "刀刀致命：对生命低于 40% 的敌军快刀伤害 +{tv%}",
                "拼命三郎", "sk_sig_bastion", SigKind.NearRage, 0, 0.22f, 1f, "城墙前警戒线内有敌军逼近时，石秀舍命死战，暴击率 +{sv%}",
                "sk_aoyi_blade", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场斩出寒钢十字刀气，之后每 3 轮普攻额外劈出双道破风刀芒",
                "喋血急斩", Eff.Crit, 0.22f, Eff.Speed, 0.32f, "刀快如电\n暴击率 +{ov%}，攻速 +{ov2%}",
                "寨墙生命低于 50% 时，石秀最终伤害额外 +30%",
                "「拼命快刀」伤害 +15%，奥义「劫法场死斗」刀气伤害 +35%"));

            Put(K(HeroId.XieZhen,
                "穿林鹰眼", "sk_trait_exec", TraitKind.FlyBonus, 0.35f, 0, "山林老猎手专射飞禽，对鹰隼与孔明灯伤害 +{tv%}",
                "登州猎户", "sk_sig_combo", SigKind.EveryNCast, 3, 1f, 2f, "每第 {sn} 轮拉满强弓搭双矢：当轮额外多射 {sv} 支必暴追猎箭",
                "sk_aoyi_arrow", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场齐射穿林箭阵，之后每 3 轮普攻额外连发 3 支猎魂快箭",
                "追风毒镝", Eff.Dmg, 0.52f, Eff.Speed, 0.28f, "淬炼破甲猎簇\n猎箭伤害 +{ov%}，攻速 +{ov2%}",
                "每第 3 轮追猎双矢对精英与飞行敌军伤害再 +35%",
                "「穿林猎弓」伤害 +15%，奥义「两头蛇穿林」连射伤害 +35%"));

            Put(K(HeroId.XieBao,
                "倒钩淬毒", "sk_trait_elem", TraitKind.DotBonus, 0.25f, 0, "三股钢叉涂满蝎毒，穿透 1 人且毒伤额外 +{tv%}",
                "双尾剧毒", "sk_sig_seal", SigKind.OnDebuff, 0, 0.38f, 0, "以毒攻毒：对已中毒或减速的敌军，倒钩毒叉直击伤害 +{sv%}",
                "sk_aoyi_arrow", "终伤 +{awkDmg%}，攻速 +{awkSpd%}；当场激射淬毒飞叉雨，之后每 3 轮普攻额外甩出 3 柄贯阵毒叉",
                "万蝎蚀骨", Eff.Chain, 1f, Eff.Burn, 0.55f, "倒钩劲力穿透\n钢叉穿透 +{ov} 人，毒伤 +{ov2%}",
                "中毒敌军阵亡时，向周围溅开一团蝎毒绿雾传染周围官军",
                "「倒钩毒叉」伤害 +15%，奥义「双尾蝎破甲」飞叉伤害 +35%"));

            Put(K(HeroId.MaLing,
                "神驹疾影", "sk_trait_crit", TraitKind.NthShotCrit, 0.18f, 3, "脚踏风火轮法力流转，每第 {tn} 轮祭砖暴击率 +{tv%}",
                "风火金印", "sk_sig_combo", SigKind.EveryNCast, 3, 1.35f, 0.8f, "每第 {sn} 轮祭出镇山大金砖：伤害 ×{sv}，多弹射 1 次且必晕 {sv2} 秒",
                "sk_aoyi_spell", "法术终伤 +{awkDmg%}，回气速率 +{awkSpd%}；当场天降连环金砖镇压前排，之后每 3 轮施法额外砸落 2 块追魂金砖",
                "乾坤金光", Eff.Chain, 2f, Eff.Stun, 0.20f, "金光折跃连砸\n金砖弹射 +{ov} 次，击晕几率 +{ov2%}",
                "被金砖击晕的敌军 3 秒内所受伤害 +20%",
                "「金砖镇魔」伤害 +15%，奥义「五岳金砖」追砸伤害 +35%"));

            Put(K(HeroId.KongLiang,
                "烈性火药", "sk_trait_elem", TraitKind.DotBonus, 0.12f, 0, "独火雷内掺硫磺铁砂，爆炸后灼烧敌军（{tv%} 伤害）",
                "星火燎原", "sk_sig_legion", SigKind.CountGE, 3, 1f, 0, "场上同时存在 ≥{sn} 位孔亮时，全体孔亮每次投雷数量 +1",
                "sk_aoyi_fire", "重炮终伤 +{awkDmg%}，装填速度 +{awkSpd%}；当场倾泻漫天火雷雨，之后每 3 轮普攻额外抛投 4 枚流星小火雷",
                "轰天双响", Eff.Area, 0.42f, Eff.Dmg, 0.40f, "扩装生铁雷壳\n火雷范围 +{ov%}，火雷伤害 +{ov2%}",
                "场上满 4 位孔亮时，爆炸范围再 +25%",
                "「独火雷」伤害 +15%，奥义「独火燎天」流星火雷伤害 +35%"));

            Put(K(HeroId.TangLong,
                "百炼锋芒", "sk_trait_tactic", TraitKind.CritDmgAura, 0.12f, 0, "神匠坐镇：汤隆在场时，全军暴击伤害额外 +{tv%}",
                "铁匠世家", "sk_sig_merge", SigKind.OnMerge, 0, 0.42f, 6f, "汤隆参与合成升星时，当场开炉淬火：全军伤害 +{sv%} 持续 {sv2} 秒",
                "sk_aoyi_aid", "光环与神兵效力 +{awkAura%}，号令回转 +{awkSpd%}；觉醒瞬间全军神兵淬火 10 秒，暴击与终伤齐升",
                "赤炉精锻", Eff.Aura, 0.58f, Eff.Coin, 32f, "炉火纯青精锻\n伤害光环 +{ov%}，每次合成 +{ov2} 银两",
                "「打造神兵」持续时间从 8 秒延长至 11 秒",
                "「打造神兵」效果 +15%，奥义「欧冶传人」增幅再 +25%"));

            Put(K(HeroId.SunErniang,
                "迷魂软筋", "sk_trait_elem", TraitKind.ZoneVuln, 0.15f, 0, "蒙汗药粉侵入经脉，命中的敌军 2.5 秒内所受伤害 +{tv%}",
                "十字坡", "sk_sig_seal", SigKind.OnDebuff, 0, 0.42f, 0, "趁病要命：对已处于眩晕或减速的敌军，蒙汗药伤害 +{sv%}",
                "sk_aoyi_bind", "破阵终伤 +{awkDmg%}，布阵速度 +{awkSpd%}；当场爆开漫天紫雾迷倒前排，之后每 3 轮普攻额外加抛一团浓烈迷烟",
                "醉仙浓香", Eff.Stun, 0.28f, Eff.Area, 0.32f, "秘制陈年蒙汗药\n击晕几率 +{ov%}，范围 +{ov2%}",
                "被蒙汗药击晕的敌军，眩晕持续时间延长 0.4 秒",
                "「蒙汗药」伤害 +15%，奥义「十字坡夜叉」毒雾伤害 +35%"));

            Put(K(HeroId.RuanXiaoqi,
                "水泊渔歌", "sk_trait_tactic", TraitKind.PriorityElite, 0.30f, 0, "天罗地网连低空飞禽与精英一并罩住，对其伤害 +{tv%}",
                "石碣水寨", "sk_sig_seal", SigKind.OnStunBurst, 0, 0.75f, 0.18f, "渔网收紧勒伤猎物：网住瞬间爆开水浪造成 {sv%} 伤害，并挂 {sv2%} 易伤",
                "sk_aoyi_bind", "破阵终伤 +{awkDmg%}，布阵速度 +{awkSpd%}；当场撒开巨型铁索大网困住敌潮，之后每 3 轮普攻额外补撒一张大网",
                "铁索沉江", Eff.Stun, 0.45f, Eff.Area, 0.32f, "网绳坠满铁砣\n定身时间 +{ov} 秒，范围 +{ov2%}",
                "被渔网定身的敌军减速延长 1.5 秒",
                "「天罗地网」伤害 +15%，奥义「石碣村阎罗」追网伤害 +35%"));

            Put(K(HeroId.ShiQian,
                "易容百搭", "sk_trait_tactic", TraitKind.WildMerge, 0.28f, 0, "召唤与合成出时迁时，{tv%} 几率呈「易容百搭」态，可与任意同星好汉合成",
                "飞檐走壁", "sk_sig_merge", SigKind.OnMerge, 0, 28f, 0.30f, "时迁参与合成升星时，妙手顺走 {sv} 银两，并立刻给最强敌军挂 {sv2%} 破绽易伤",
                "sk_aoyi_aid", "赏银光环与顺手牵羊收益 +{awkAura%}，号令回转 +{awkSpd%}；觉醒当场盗取一笔赏银并标记全场精英破绽",
                "探囊取物", Eff.Coin, 32f, Eff.Aura, 0.45f, "神偷绝技大成\n每次合成 +{ov} 银两，赏银光环 +{ov2%}",
                "「易容百搭」触发几率从 28% 提升至 40%",
                "「顺手牵羊」收益 +15%，奥义「梁上君子」赏银再 +25%"));

            Put(K(HeroId.JiangJing,
                "铁算金盘", "sk_trait_tactic", TraitKind.InterestCap, 60f, 0, "神算账房坐镇：单次神机妙算利息上限提高 +{tv} 银两",
                "精打细算", "sk_sig_merge", SigKind.OnMerge, 0, 0.12f, 35f, "蒋敬参与合成升星时，当场按当前银两结算一次 {sv%} 活期红利（保底 {sv2} 银两）",
                "sk_aoyi_aid", "利息光环与红利效力 +{awkAura%}，号令回转 +{awkSpd%}；觉醒当场派发一笔丰厚银两并鼓舞全军攻速 6 秒",
                "日进斗金", Eff.Aura, 0.55f, Eff.Coin, 32f, "利滚利生财有道\n利息光环 +{ov%}，每次合成 +{ov2} 银两",
                "释放「神机妙算」鼓舞全军攻速的持续时间延长至 7 秒",
                "「神机妙算」收益 +15%，奥义「铁算通神」红利再 +25%"));

            return a;
        }
    }
}

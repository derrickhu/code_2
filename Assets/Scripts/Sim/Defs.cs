using System.Collections.Generic;

namespace Liangshan.Sim
{
    /// <summary>存档按下标存好汉，只能往后加。</summary>
    public enum HeroId
    {
        HuaRong, LingZhen, GongSun, WuSong, ZhangQing, LinChong, LuZhiShen, WuYong,
        LuJunyi, GuanSheng, YanQing, FanRui, HuSanniang, AnDaoquan, DaiZong, QinMing,
        LiKui, QiaoDaoqing, ZhuWu, XuNing, ShanTinggui, WeiDingguo,
        ShiXiu, XieZhen, XieBao, MaLing, KongLiang, TangLong, SunErniang, RuanXiaoqi, ShiQian, JiangJing,
    }

    public enum Role { Archer, Bomber, Mage, Fighter, Support, Control }

    /// <summary>强化、被动的效果种类。数值含义见 Defs.EffText。</summary>
    public enum Eff { Dmg, Speed, Count, Area, Chain, Crit, Stun, Slow, Elite, UltCd, UltPow, Range, Split, Aura, Coin, Fly, Cav }

    public enum UltKind
    {
        Rain, BigBomb, Thunder, Barrage, Volley, Frost, Sweep, Haste, Heal, Bind, Snipe, Tornado,
        Meteor, FireLine, Formation, Net, Powder, Wave, Brick, Hook, Steal, Interest, Forge, Charge, Arrows,
    }

    /// <summary>辅助在场时的光环，按场上该好汉星级累加。</summary>
    public enum AuraKind { None, Speed, Dmg, Charge, Heal, Coin, Interest }

    public sealed class HeroDef
    {
        public HeroId Id;
        public string Name, Nick, Art, Desc;
        public Role Kind;
        /// <summary>2 蓝（稀有）3 紫（史诗）4 橙（传说）。</summary>
        public int Rarity;
        public string Element = "物理";

        /// <summary>普攻：弹道种类、伤害系数（寨主攻击力的倍数）、间隔、射程（近战才有限制）。</summary>
        public ShotKind Shot;
        public float Coef, Interval, Range = 99, Radius = 0.4f, Knock, Stun, StunChance, Slow;
        public int Count = 1, Chain;
        public string AtkName, AtkIcon, Noun;
        /// <summary>弹道、命中特效的色调（空 = 弹道自带颜色）。</summary>
        public string Tint;

        public UltKind Ult;
        public string UltName, UltIcon;
        public float UltCoef, UltCd, UltRadius = 1.6f, UltStun;
        public int UltN = 1;

        /// <summary>5 级解锁的被动。</summary>
        public string PassiveName;
        public Eff PassiveEff;
        public float PassiveV;

        public AuraKind Aura;
        public float AuraV;

        public string Role => Defs.RoleName(Kind);
        public bool Physical => Element == "物理";
        public bool Melee => Range < 50;
        /// <summary>输出位：保底规则里辅助不算。</summary>
        public bool Carry => Kind != Sim.Role.Support;
        public string RarityName => Rarity >= 4 ? "传说" : Rarity == 3 ? "史诗" : "稀有";
    }

    public enum StageMode { Main, Daily, Escort }

    /// <summary>一波的兵种配比：累计阈值，Base + Grow × 波次，From 波起出现。</summary>
    public struct TroopMix
    {
        public EnemyKind Kind;
        public float Base, Grow;
        public int From;
        public TroopMix(EnemyKind k, float b, float g, int from) { Kind = k; Base = b; Grow = g; From = from; }
    }

    /// <summary>一关的配置。</summary>
    public sealed class LevelDef
    {
        public int No, Chapter, Index;
        public string Name, Intro, Bg = "battle_bg";
        public int Waves = 10, BossWave = 4, RageWave = 8;
        public int[] EliteWaves = { }, PorterWaves = { };
        public int PorterCount = 1;
        public EnemyKind[] EliteKinds = { EnemyKind.Elite };
        public EnemyKind BossKind = EnemyKind.Boss;
        public float BossMul = 1, HpMul = 1, AtkMul = 1, CountMul = 1;
        public TroopMix[] Troops = Defs.Mix1;
        /// <summary>关卡增益：某一职业伤害提高。</summary>
        public Role? BuffRole;
        public float BuffPct;
        public float WallHp = 6000;
        public int StartCoins = 100;
        public StageMode Mode = StageMode.Main;
        public string Title => Mode == StageMode.Main ? $"第{No}关 · {Name}" : Name;
        public string BossName => Defs.Enemy(BossKind).Name;
        public bool IsChapterBoss => Index == 5 || Index == 10;
        public string BuffText => BuffRole.HasValue ? $"{Defs.RoleName(BuffRole.Value)}伤害 +{(int)(BuffPct * 100 + 0.5f)}%" : "";
    }

    public enum EnemyKind
    {
        Soldier, Shield, Cavalry, Crossbow, Elite, Porter, Boss,
        Hawk, Armored, FireBomb, ChainHorse, Luan, ShiWen, HuYan, GaoQiu,
    }

    public enum EnemyRank { Minion, Elite, Boss }

    public sealed class EnemyDef
    {
        public EnemyKind Kind;
        public string Name, Art, Desc;
        public float Hp, Speed, WallDps, Size;
        public int Coin;
        /// <summary>远程兵停在这条线上放箭，不贴墙。</summary>
        public float StopY;
        public EnemyRank Rank;
        public bool Flying;
        /// <summary>受物理伤害减免；受法术（火、雷、风）伤害加成；被射手克制。</summary>
        public float PhysResist, MagicWeak, ArcherWeak;
        public bool Big => Rank != EnemyRank.Minion;
        public bool IsBoss => Rank == EnemyRank.Boss;
    }

    /// <summary>好汉专属强化。Star 为满星额外技能，只在合成满星时三选一。</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Desc;
        public HeroId Hero;
        public int Max = 1;
        public float Weight = 1;
        public Eff Eff;
        public float V;
        public bool Star;
    }

    /// <summary>战场坐标：1 单位 = 设计稿 100 像素，原点在 1080×1920 正中，y 朝上。</summary>
    public static class Layout
    {
        public const float HalfW = 5.4f;
        public const float WallRowY = -5.05f;
        public const float FrontRowY = -3.85f;
        public const float StopY = -3.05f;
        public const float SpawnY = 7.9f;
        /// <summary>出了寨门、走过这条线才会被锁定，让官军先涌进场地。</summary>
        public const float EngageY = 5.2f;
        public const float SlotGap = 1.36f;
        public const int BaseSlots = 7;
        public const int MaxSlots = 12;
        public static readonly float[] GateX = { -3.7f, 3.7f };

        public static V2 SlotPos(int i)
        {
            if (i < BaseSlots) return new V2((i - 3) * SlotGap, WallRowY);
            int[] cols = { 3, 1, 5, 2, 4 };
            return new V2((cols[i - BaseSlots] - 3) * SlotGap, FrontRowY);
        }
    }

    public static class Defs
    {
        /// <summary>
        /// 伤害系数 Coef 是寨主攻击力的倍数（寨主初始攻击 50）。同稀有度按职业定位平衡：
        /// 单体射手、近战战士单体最高；炮手、法师吃范围；控制、辅助伤害低但带控场或光环。
        /// 稀有度越高，同职业输出约高两成并带更强的大招。
        /// </summary>
        public static readonly HeroDef[] Heroes =
        {
            // ---------- 初始四人与早期入伙 ----------
            new HeroDef { Id = HeroId.HuaRong, Name = "花荣", Nick = "小李广", Kind = Sim.Role.Archer, Rarity = 3, Art = "hero_huarong",
                Shot = ShotKind.Arrow, Coef = 0.40f, Interval = 0.75f, AtkName = "穿云箭", AtkIcon = "ic_arrow", Noun = "箭",
                Ult = UltKind.Rain, UltName = "百步穿杨", UltIcon = "fx_goldarrow", UltCoef = 2.2f, UltCd = 11, UltN = 10,
                PassiveName = "神射", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "射速快的单体射手，克制飞行敌人" },
            new HeroDef { Id = HeroId.LingZhen, Name = "凌振", Nick = "轰天雷", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_lingzhen", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.60f, Interval = 1.55f, Radius = 1.0f, AtkName = "霹雳炮", AtkIcon = "ic_bomb", Noun = "炸弹",
                Ult = UltKind.BigBomb, UltName = "轰天雷", UltIcon = "fx_fireball", UltCoef = 7f, UltCd = 13, UltRadius = 2.3f,
                PassiveName = "火药精通", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "范围炮击，专清密集兵潮" },
            new HeroDef { Id = HeroId.GongSun, Name = "公孙胜", Nick = "入云龙", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_gongsun", Element = "雷系",
                Shot = ShotKind.Lightning, Coef = 0.48f, Interval = 1.25f, Radius = 0.75f, Chain = 1, AtkName = "引雷诀", AtkIcon = "fx_zap", Noun = "落雷",
                Ult = UltKind.Thunder, UltName = "五雷天罡", UltIcon = "fx_lightning", UltCoef = 4f, UltCd = 12, UltN = 5, UltStun = 1f, UltRadius = 1.15f,
                PassiveName = "道法自然", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "落雷连锁，克制重甲" },
            new HeroDef { Id = HeroId.WuSong, Name = "武松", Nick = "行者", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_wusong",
                Shot = ShotKind.Punch, Coef = 0.68f, Interval = 0.7f, Range = 4.6f, Radius = 0.8f, Knock = 0.35f, AtkName = "铁拳", AtkIcon = "ic_fist", Noun = "铁拳",
                Ult = UltKind.Barrage, UltName = "醉打", UltIcon = "fx_shock", UltCoef = 1.6f, UltCd = 12, UltN = 18,
                PassiveName = "景阳冈打虎", PassiveEff = Eff.Elite, PassiveV = 0.6f, Desc = "近身拳击，击退贴墙的敌人" },
            new HeroDef { Id = HeroId.ZhangQing, Name = "张清", Nick = "没羽箭", Kind = Sim.Role.Archer, Rarity = 3, Art = "hero_zhangqing",
                Shot = ShotKind.Stone, Coef = 0.40f, Interval = 0.9f, Radius = 0.3f, Chain = 1, AtkName = "飞石", AtkIcon = "fx_rock0", Noun = "飞石",
                Ult = UltKind.Volley, UltName = "飞石连珠", UltIcon = "fx_rock1", UltCoef = 2.5f, UltCd = 12, UltN = 12, UltStun = 1f,
                PassiveName = "百发百中", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "飞石在敌人之间弹射" },
            new HeroDef { Id = HeroId.LinChong, Name = "林冲", Nick = "豹子头", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_linchong",
                Shot = ShotKind.Spear, Coef = 0.80f, Interval = 0.9f, Range = 6.2f, Radius = 0.55f, Knock = 0.15f, AtkName = "丈八蛇矛", AtkIcon = "ic_sword", Noun = "长枪",
                Ult = UltKind.Frost, UltName = "风雪山神庙", UltIcon = "fx_ice", UltCoef = 5f, UltCd = 13, UltRadius = 1.9f, UltStun = 2.5f,
                PassiveName = "禁军教头", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "长枪贯穿一列，攻击距离远" },
            new HeroDef { Id = HeroId.LuZhiShen, Name = "鲁智深", Nick = "花和尚", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_luzhishen",
                Shot = ShotKind.Slam, Coef = 1.36f, Interval = 1.3f, Range = 5.2f, Radius = 1.15f, Knock = 0.3f, AtkName = "禅杖", AtkIcon = "fx_crack", Noun = "禅杖",
                Ult = UltKind.Sweep, UltName = "倒拔垂杨柳", UltIcon = "fx_wave", UltCoef = 4f, UltCd = 14, UltN = 5,
                PassiveName = "大闹五台山", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "禅杖砸地，大范围震退" },
            new HeroDef { Id = HeroId.WuYong, Name = "吴用", Nick = "智多星", Kind = Sim.Role.Support, Rarity = 3, Art = "hero_wuyong", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.26f, Interval = 1.0f, Radius = 0.6f, Slow = 1.5f, AtkName = "羽扇风", AtkIcon = "fx_smoke", Noun = "风刃",
                Aura = AuraKind.Speed, AuraV = 0.04f,
                Ult = UltKind.Haste, UltName = "运筹帷幄", UltIcon = "prop_drum", UltCoef = 3f, UltCd = 15, UltRadius = 2f,
                PassiveName = "锦囊妙计", PassiveEff = Eff.Coin, PassiveV = 15, Desc = "风刃减速，在场提升全军攻速" },

            // ---------- 传说 ----------
            new HeroDef { Id = HeroId.LuJunyi, Name = "卢俊义", Nick = "玉麒麟", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_lujunyi",
                Shot = ShotKind.Spear, Coef = 0.82f, Interval = 0.85f, Range = 6.0f, Radius = 0.8f, Knock = 0.2f, AtkName = "麒麟枪", AtkIcon = "ic_sword", Noun = "长枪",
                Ult = UltKind.Sweep, UltName = "麒麟踏阵", UltIcon = "fx_wave", UltCoef = 4.5f, UltCd = 13, UltN = 5,
                PassiveName = "河北三绝", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "宽幅枪扫，一次挑翻一列" },
            new HeroDef { Id = HeroId.GuanSheng, Name = "关胜", Nick = "大刀", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_guansheng",
                Shot = ShotKind.Slam, Coef = 1.55f, Interval = 1.25f, Range = 5.4f, Radius = 1.3f, Knock = 0.3f, AtkName = "青龙偃月刀", AtkIcon = "fx_crack", Noun = "大刀",
                Ult = UltKind.Sweep, UltName = "拖刀计", UltIcon = "fx_shock", UltCoef = 5f, UltCd = 14, UltN = 5,
                PassiveName = "武圣遗风", PassiveEff = Eff.Elite, PassiveV = 0.5f, Desc = "大刀劈砍，范围大伤害高" },
            new HeroDef { Id = HeroId.YanQing, Name = "燕青", Nick = "浪子", Kind = Sim.Role.Archer, Rarity = 4, Art = "hero_yanqing",
                Shot = ShotKind.Arrow, Coef = 0.29f, Interval = 0.8f, Count = 2, AtkName = "连弩", AtkIcon = "ic_arrow", Noun = "弩箭",
                Ult = UltKind.Snipe, UltName = "一箭封喉", UltIcon = "fx_goldarrow", UltCoef = 16f, UltCd = 11,
                PassiveName = "浪子身手", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "连弩双发，大招专点首领" },
            new HeroDef { Id = HeroId.FanRui, Name = "樊瑞", Nick = "混世魔王", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_fanrui", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.56f, Interval = 1.3f, Radius = 1.0f, Slow = 1.5f, AtkName = "旋风咒", AtkIcon = "fx_smoke", Noun = "旋风", Tint = "C9D9C2",
                Ult = UltKind.Tornado, UltName = "飞沙走石", UltIcon = "fx_rock1", UltCoef = 6f, UltCd = 13, UltRadius = 2.2f, UltStun = 1f,
                PassiveName = "妖法", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "旋风范围法伤并减速" },
            new HeroDef { Id = HeroId.HuSanniang, Name = "扈三娘", Nick = "一丈青", Kind = Sim.Role.Control, Rarity = 4, Art = "hero_husanniang",
                Shot = ShotKind.Lasso, Coef = 0.72f, Interval = 1.6f, Stun = 1.0f, AtkName = "红锦套索", AtkIcon = "ic_hand", Noun = "套索", Tint = "E8483A",
                Ult = UltKind.Bind, UltName = "阵前擒将", UltIcon = "ic_hand", UltCoef = 4f, UltCd = 12, UltN = 5, UltStun = 2f,
                PassiveName = "日月双刀", PassiveEff = Eff.Count, PassiveV = 1, Desc = "套索锁人，连精英首领也能拽住" },
            new HeroDef { Id = HeroId.AnDaoquan, Name = "安道全", Nick = "神医", Kind = Sim.Role.Support, Rarity = 4, Art = "hero_andaoquan", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.22f, Interval = 1.2f, Radius = 0.6f, AtkName = "药雾", AtkIcon = "ic_heart", Noun = "药雾", Tint = "8FF0A0",
                Aura = AuraKind.Heal, AuraV = 0.005f,
                Ult = UltKind.Heal, UltName = "妙手回春", UltIcon = "ic_heart", UltCoef = 0.2f, UltCd = 16,
                PassiveName = "神医圣手", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "出手就给寨墙回血" },
            new HeroDef { Id = HeroId.DaiZong, Name = "戴宗", Nick = "神行太保", Kind = Sim.Role.Support, Rarity = 4, Art = "hero_daizong",
                Shot = ShotKind.Stone, Coef = 0.24f, Interval = 1.0f, Radius = 0.3f, AtkName = "符箓", AtkIcon = "ic_scroll", Noun = "符箓", Tint = "FFD84A",
                Aura = AuraKind.Charge, AuraV = 0.06f,
                Ult = UltKind.Charge, UltName = "神行甲马", UltIcon = "ic_fast", UltCoef = 0.5f, UltCd = 15,
                PassiveName = "日行八百", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "在场加快全军大招冷却" },
            new HeroDef { Id = HeroId.QinMing, Name = "秦明", Nick = "霹雳火", Kind = Sim.Role.Bomber, Rarity = 4, Art = "hero_qinming", Element = "火系",
                Shot = ShotKind.Fireball, Coef = 0.78f, Interval = 1.6f, Radius = 1.1f, AtkName = "火流星", AtkIcon = "fx_fireball", Noun = "火球",
                Ult = UltKind.Meteor, UltName = "霹雳天火", UltIcon = "fx_explode", UltCoef = 3.5f, UltCd = 13, UltN = 6, UltRadius = 1.2f,
                PassiveName = "性如烈火", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "大火球砸进人堆" },

            // ---------- 史诗 ----------
            new HeroDef { Id = HeroId.LiKui, Name = "李逵", Nick = "黑旋风", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_likui",
                Shot = ShotKind.Punch, Coef = 0.36f, Interval = 0.75f, Count = 2, Range = 4.6f, Radius = 0.9f, Knock = 0.25f, AtkName = "板斧", AtkIcon = "ic_sword", Noun = "板斧",
                Ult = UltKind.Barrage, UltName = "旋风双斧", UltIcon = "fx_shock", UltCoef = 1.5f, UltCd = 12, UltN = 20,
                PassiveName = "黑旋风", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "双斧连砍，墙前清场" },
            new HeroDef { Id = HeroId.QiaoDaoqing, Name = "乔道清", Nick = "幻魔君", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_qiaodaoqing", Element = "火系",
                Shot = ShotKind.Fireball, Coef = 0.55f, Interval = 1.4f, Radius = 0.9f, AtkName = "三昧真火", AtkIcon = "fx_fireball", Noun = "火球",
                Ult = UltKind.Meteor, UltName = "天火焚营", UltIcon = "fx_explode", UltCoef = 3.8f, UltCd = 13, UltN = 5, UltRadius = 1.1f,
                PassiveName = "幻术", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "火球溅射，法伤克制重甲" },
            new HeroDef { Id = HeroId.ZhuWu, Name = "朱武", Nick = "神机军师", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_zhuwu", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.36f, Interval = 1.5f, Radius = 1.2f, Slow = 2f, AtkName = "八阵图", AtkIcon = "prop_book", Noun = "阵法", Tint = "7FB4FF",
                Ult = UltKind.Formation, UltName = "八门金锁", UltIcon = "prop_book", UltCoef = 2f, UltCd = 15,
                PassiveName = "神机妙算", PassiveEff = Eff.Slow, PassiveV = 1f, Desc = "大范围减速，大招让全场易伤" },
            new HeroDef { Id = HeroId.XuNing, Name = "徐宁", Nick = "金枪手", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_xuning",
                Shot = ShotKind.Spear, Coef = 0.56f, Interval = 1.0f, Range = 6.0f, Radius = 0.6f, Slow = 1.5f, AtkName = "钩镰枪", AtkIcon = "ic_sword", Noun = "钩镰", Tint = "FFD27A",
                Ult = UltKind.Hook, UltName = "钩镰枪阵", UltIcon = "ic_sword", UltCoef = 3.5f, UltCd = 12, UltN = 5, UltStun = 1.5f,
                PassiveName = "金枪班", PassiveEff = Eff.Cav, PassiveV = 0.5f, Desc = "钩镰绊马，克制骑兵与连环马" },
            new HeroDef { Id = HeroId.ShanTinggui, Name = "单廷珪", Nick = "圣水将", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_shantinggui", Element = "水系",
                Shot = ShotKind.Wind, Coef = 0.50f, Interval = 1.35f, Radius = 1.0f, Slow = 2f, AtkName = "圣水", AtkIcon = "fx_icecrystal", Noun = "水浪", Tint = "6FC8FF",
                Ult = UltKind.Wave, UltName = "黑水狂澜", UltIcon = "fx_wave", UltCoef = 3f, UltCd = 14,
                PassiveName = "圣水灌顶", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "水浪减速，大招横扫全场" },
            new HeroDef { Id = HeroId.WeiDingguo, Name = "魏定国", Nick = "神火将", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_weidingguo", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.62f, Interval = 1.6f, Radius = 1.1f, AtkName = "火罐", AtkIcon = "ic_bomb", Noun = "火罐", Tint = "FF7A3A",
                Ult = UltKind.FireLine, UltName = "火龙阵", UltIcon = "fx_explode", UltCoef = 4f, UltCd = 13, UltN = 6,
                PassiveName = "神火", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "火罐炸开一片，大招墙前一排火海" },

            // ---------- 稀有 ----------
            new HeroDef { Id = HeroId.ShiXiu, Name = "石秀", Nick = "拼命三郎", Kind = Sim.Role.Fighter, Rarity = 2, Art = "hero_shixiu",
                Shot = ShotKind.Punch, Coef = 0.40f, Interval = 0.6f, Range = 4.6f, Radius = 0.6f, Knock = 0.15f, AtkName = "朴刀", AtkIcon = "ic_sword", Noun = "朴刀",
                Ult = UltKind.Barrage, UltName = "拼命三刀", UltIcon = "fx_hit", UltCoef = 2f, UltCd = 12, UltN = 10,
                PassiveName = "拼命", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "出刀快，暴击高" },
            new HeroDef { Id = HeroId.XieZhen, Name = "解珍", Nick = "两头蛇", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiezhen",
                Shot = ShotKind.Arrow, Coef = 0.34f, Interval = 0.8f, AtkName = "猎弓", AtkIcon = "ic_arrow", Noun = "箭",
                Ult = UltKind.Arrows, UltName = "猎虎", UltIcon = "fx_goldarrow", UltCoef = 2.5f, UltCd = 12, UltN = 8,
                PassiveName = "登州猎户", PassiveEff = Eff.Fly, PassiveV = 0.5f, Desc = "猎户出身，专打飞禽" },
            new HeroDef { Id = HeroId.XieBao, Name = "解宝", Nick = "双尾蝎", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiebao",
                Shot = ShotKind.Arrow, Coef = 0.45f, Interval = 1.2f, Chain = 2, AtkName = "飞叉", AtkIcon = "ic_sword", Noun = "钢叉",
                Ult = UltKind.Arrows, UltName = "飞叉阵", UltIcon = "ic_sword", UltCoef = 3f, UltCd = 12, UltN = 6,
                PassiveName = "双尾蝎", PassiveEff = Eff.Chain, PassiveV = 1, Desc = "钢叉穿透一串敌人" },
            new HeroDef { Id = HeroId.MaLing, Name = "马灵", Nick = "神驹子", Kind = Sim.Role.Mage, Rarity = 2, Art = "hero_maling", Element = "金系",
                Shot = ShotKind.Stone, Coef = 0.55f, Interval = 1.3f, Radius = 0.35f, Stun = 0.8f, StunChance = 0.2f, AtkName = "金砖", AtkIcon = "ic_ingot", Noun = "金砖", Tint = "FFD24A",
                Ult = UltKind.Brick, UltName = "金砖镇魔", UltIcon = "ic_ingot", UltCoef = 12f, UltCd = 12, UltStun = 2f,
                PassiveName = "风火轮", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "金砖砸人有几率击晕" },
            new HeroDef { Id = HeroId.KongLiang, Name = "孔亮", Nick = "独火星", Kind = Sim.Role.Bomber, Rarity = 2, Art = "hero_kongliang", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.42f, Interval = 1.3f, Radius = 0.8f, AtkName = "火雷", AtkIcon = "ic_bomb", Noun = "火雷",
                Ult = UltKind.FireLine, UltName = "满天星火", UltIcon = "fx_explode", UltCoef = 3f, UltCd = 13, UltN = 5,
                PassiveName = "独火星", PassiveEff = Eff.Count, PassiveV = 1, Desc = "小炸弹扔得勤" },
            new HeroDef { Id = HeroId.TangLong, Name = "汤隆", Nick = "金钱豹子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_tanglong",
                Shot = ShotKind.Stone, Coef = 0.26f, Interval = 1.2f, Radius = 0.35f, AtkName = "铁锤", AtkIcon = "ic_gear", Noun = "铁锤", Tint = "A8AEB8",
                Aura = AuraKind.Dmg, AuraV = 0.03f,
                Ult = UltKind.Forge, UltName = "打造神兵", UltIcon = "ic_gear", UltCoef = 0.4f, UltCd = 15,
                PassiveName = "铁匠世家", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "在场提升全军伤害" },
            new HeroDef { Id = HeroId.SunErniang, Name = "孙二娘", Nick = "母夜叉", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_sunerniang", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.30f, Interval = 1.4f, Radius = 1.0f, Slow = 1.5f, Stun = 0.8f, StunChance = 0.3f, AtkName = "蒙汗药", AtkIcon = "fx_smoke", Noun = "药粉", Tint = "C690FF",
                Ult = UltKind.Powder, UltName = "十字坡", UltIcon = "fx_smoke", UltCoef = 3f, UltCd = 13, UltRadius = 2.2f, UltStun = 2.5f,
                PassiveName = "母夜叉", PassiveEff = Eff.Stun, PassiveV = 0.15f, Desc = "药粉迷倒一片" },
            new HeroDef { Id = HeroId.RuanXiaoqi, Name = "阮小七", Nick = "活阎罗", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_ruanxiaoqi",
                Shot = ShotKind.Wind, Coef = 0.30f, Interval = 1.8f, Radius = 0.9f, Stun = 1.0f, AtkName = "渔网", AtkIcon = "fx_wave", Noun = "渔网", Tint = "D8C08A",
                Ult = UltKind.Net, UltName = "天罗地网", UltIcon = "fx_wave", UltCoef = 2.5f, UltCd = 14, UltRadius = 2.5f, UltStun = 2.5f,
                PassiveName = "活阎罗", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "撒网定住小兵" },
            new HeroDef { Id = HeroId.ShiQian, Name = "时迁", Nick = "鼓上蚤", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_shiqian",
                Shot = ShotKind.Arrow, Coef = 0.16f, Interval = 0.9f, Count = 2, AtkName = "飞镖", AtkIcon = "ic_arrow", Noun = "飞镖",
                Aura = AuraKind.Coin, AuraV = 0.1f,
                Ult = UltKind.Steal, UltName = "顺手牵羊", UltIcon = "ic_coins", UltCoef = 1f, UltCd = 15,
                PassiveName = "鼓上蚤", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "在场击杀多掉银两" },
            new HeroDef { Id = HeroId.JiangJing, Name = "蒋敬", Nick = "神算子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_jiangjing",
                Shot = ShotKind.Stone, Coef = 0.22f, Interval = 1.1f, Radius = 0.3f, AtkName = "算珠", AtkIcon = "ic_coins", Noun = "算珠", Tint = "FFD24A",
                Aura = AuraKind.Interest, AuraV = 0.01f,
                Ult = UltKind.Interest, UltName = "神机妙算", UltIcon = "ic_coins", UltCoef = 0.15f, UltCd = 15,
                PassiveName = "精打细算", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "在场时银两生利息" },
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];
        public static int HeroCount => Heroes.Length;
        public const int EffCount = 17;
        public const int RoleCount = 6;

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Sim.Role.Archer: return "射手";
                case Sim.Role.Bomber: return "炮手";
                case Sim.Role.Mage: return "法师";
                case Sim.Role.Fighter: return "战士";
                case Sim.Role.Control: return "控制";
                default: return "辅助";
            }
        }

        /// <summary>职业图标与主色。</summary>
        public static string RoleIcon(Role r) => "role_" + ((int)r);
        public static readonly string[] RoleColors = { "4FA3E0", "E0753A", "9B6BE0", "D8463A", "4CB86A", "E0B23A" };

        public static readonly Role[] RoleOrder = { Sim.Role.Fighter, Sim.Role.Archer, Sim.Role.Mage, Sim.Role.Bomber, Sim.Role.Control, Sim.Role.Support };

        static EnemyDef E(EnemyKind k, string name, string art, float hp, float speed, float dps, float size, int coin, string desc,
            EnemyRank rank = EnemyRank.Minion, float stopY = Layout.StopY)
            => new EnemyDef { Kind = k, Name = name, Art = art, Hp = hp, Speed = speed, WallDps = dps, Size = size, Coin = coin, Desc = desc, Rank = rank, StopY = stopY };

        public static readonly EnemyDef[] Enemies =
        {
            E(EnemyKind.Soldier, "官兵", "en_soldier", 30, 0.62f, 11, 0.95f, 2, "最常见的官军步卒，人多势众"),
            E(EnemyKind.Shield, "盾兵", "en_shield", 110, 0.42f, 17, 1.15f, 4, "举着大盾，皮糙肉厚走得慢"),
            E(EnemyKind.Cavalry, "骑兵", "en_cavalry", 26, 1.0f, 9, 1.15f, 3, "跑得最快，冲到墙下才停"),
            E(EnemyKind.Crossbow, "弩手", "en_crossbow", 28, 0.58f, 8, 0.95f, 3, "停在半路放箭，近战够不着", stopY: 0.6f),
            E(EnemyKind.Elite, "教头", "en_elite", 1300, 0.36f, 70, 1.6f, 45, "官军教头，血厚攻高", EnemyRank.Elite),
            E(EnemyKind.Porter, "生辰纲", "en_porter", 420, 0.5f, 0, 1.25f, 20, "挑着财宝的脚夫，只在劫生辰纲时出现"),
            E(EnemyKind.Boss, "黄安", "en_boss", 9000, 0.17f, 200, 2.6f, 200, "济州团练使，首个来犯的官军首领", EnemyRank.Boss),
            new EnemyDef { Kind = EnemyKind.Hawk, Name = "鹰隼", Art = "en_hawk", Hp = 22, Speed = 0.85f, WallDps = 8, Size = 0.9f, Coin = 2,
                Desc = "飞行单位，战士打不到，射手克制", Flying = true, ArcherWeak = 0.3f, StopY = Layout.StopY + 0.5f },
            new EnemyDef { Kind = EnemyKind.Armored, Name = "铁甲兵", Art = "en_armored", Hp = 150, Speed = 0.4f, WallDps = 18, Size = 1.15f, Coin = 4,
                Desc = "重甲：物理伤害减半，怕火雷风", PhysResist = 0.5f, MagicWeak = 0.3f, StopY = Layout.StopY },
            E(EnemyKind.FireBomb, "火药兵", "en_firebomb", 40, 0.9f, 260, 0.95f, 3, "冲到墙下自爆；半路击杀会炸伤身边官军"),
            new EnemyDef { Kind = EnemyKind.ChainHorse, Name = "连环马", Art = "en_chainhorse", Hp = 950, Speed = 0.5f, WallDps = 60, Size = 1.7f, Coin = 40,
                Desc = "铁索相连的重骑，物理减伤", Rank = EnemyRank.Elite, PhysResist = 0.25f, StopY = Layout.StopY },
            E(EnemyKind.Luan, "栾廷玉", "en_luan", 11000, 0.17f, 170, 2.6f, 220, "祝家庄教师，飞锤砸晕好汉", EnemyRank.Boss),
            E(EnemyKind.ShiWen, "史文恭", "en_shiwen", 12000, 0.15f, 160, 2.6f, 240, "曾头市教师，冲锋突进，残血狂暴", EnemyRank.Boss),
            E(EnemyKind.HuYan, "呼延灼", "en_huyan", 14000, 0.16f, 170, 2.7f, 260, "双鞭将，不断召来连环骑兵", EnemyRank.Boss),
            E(EnemyKind.GaoQiu, "高俅", "en_gaoqiu", 16000, 0.15f, 180, 2.6f, 300, "殿帅府太尉，踢火球砸墙，带护盾", EnemyRank.Boss),
        };

        public static EnemyDef Enemy(EnemyKind k) => Enemies[(int)k];

        public static readonly PerkDef[] Perks = BuildPerks();

        /// <summary>
        /// 每位好汉 7 条：前 4 条进强化三选一（同一条可叠 Max 级），后 3 条是满星额外技能，合成满星时三选一、只给一次。
        /// </summary>
        static PerkDef[] BuildPerks()
        {
            var list = new List<PerkDef>();
            void Add(HeroId h, params (string name, Eff e, float v, int max)[] ps)
            {
                string key = Hero(h).Art.Substring(5);
                for (int i = 0; i < ps.Length; i++)
                {
                    var (name, e, v, max) = ps[i];
                    bool star = i >= 4;
                    list.Add(new PerkDef
                    {
                        Id = key + "_" + i, Hero = h, Name = name, Eff = e, V = v, Max = star ? 1 : max, Star = star,
                        Desc = EffText(h, e, v),
                    });
                }
            }
            Add(HeroId.HuaRong, ("连珠箭", Eff.Count, 1, 2), ("穿云箭", Eff.Chain, 1, 2), ("鹰眼", Eff.Crit, 0.2f, 1), ("强弓", Eff.Dmg, 0.35f, 2),
                ("箭神", Eff.Dmg, 1f, 1), ("疾风连射", Eff.Speed, 0.5f, 1), ("箭如雨下", Eff.UltPow, 1f, 1));
            Add(HeroId.LingZhen, ("双重炸弹", Eff.Count, 1, 2), ("火药加倍", Eff.Area, 0.35f, 2), ("分裂炸弹", Eff.Split, 1, 1), ("火炮改良", Eff.Dmg, 0.35f, 2),
                ("子母连环", Eff.Count, 2, 1), ("震天雷", Eff.Dmg, 1f, 1), ("炮火连天", Eff.UltCd, 0.3f, 1));
            Add(HeroId.GongSun, ("雷链", Eff.Chain, 2, 2), ("掌心雷", Eff.Stun, 0.3f, 1), ("呼风唤雪", Eff.Slow, 2f, 1), ("天雷", Eff.Dmg, 0.35f, 2),
                ("九天雷劫", Eff.Chain, 4, 1), ("雷霆万钧", Eff.Dmg, 1f, 1), ("五雷正法", Eff.UltPow, 1f, 1));
            Add(HeroId.WuSong, ("三碗不过冈", Eff.Speed, 0.4f, 2), ("打虎", Eff.Elite, 1f, 1), ("玉环步", Eff.Area, 0.5f, 1), ("铁臂", Eff.Dmg, 0.35f, 2),
                ("醉拳", Eff.Speed, 0.6f, 1), ("天伤星", Eff.Dmg, 1f, 1), ("醉打蒋门神", Eff.UltPow, 1f, 1));
            Add(HeroId.ZhangQing, ("连环飞石", Eff.Chain, 2, 2), ("石破天惊", Eff.Stun, 0.3f, 1), ("飞蝗石", Eff.Dmg, 0.3f, 2), ("快手", Eff.Speed, 0.3f, 1),
                ("漫天花雨", Eff.Chain, 4, 1), ("金石为开", Eff.Dmg, 1f, 1), ("连珠不绝", Eff.UltPow, 1f, 1));
            Add(HeroId.LinChong, ("丈八蛇矛", Eff.Range, 1.5f, 2), ("豹子头", Eff.Crit, 0.25f, 1), ("林教头", Eff.Slow, 1.6f, 1), ("枪出如龙", Eff.Dmg, 0.35f, 2),
                ("雪夜上梁山", Eff.Dmg, 1f, 1), ("风雪", Eff.UltPow, 1f, 1), ("枪挑", Eff.Speed, 0.5f, 1));
            Add(HeroId.LuZhiShen, ("禅杖横扫", Eff.Area, 0.4f, 2), ("大闹野猪林", Eff.Speed, 0.4f, 2), ("拳打镇关西", Eff.Elite, 1f, 1), ("金刚", Eff.Dmg, 0.35f, 1),
                ("倒拔杨柳", Eff.UltPow, 1f, 1), ("怒目金刚", Eff.Dmg, 1f, 1), ("坐化", Eff.Area, 0.6f, 1));
            Add(HeroId.WuYong, ("运筹", Eff.Aura, 0.5f, 2), ("羽扇纶巾", Eff.Dmg, 0.6f, 2), ("智多星", Eff.Coin, 15, 1), ("神机", Eff.UltCd, 0.2f, 1),
                ("智取", Eff.Aura, 1f, 1), ("帷幄", Eff.UltPow, 1f, 1), ("吴学究", Eff.Speed, 0.5f, 1));

            Add(HeroId.LuJunyi, ("横扫千军", Eff.Area, 0.4f, 2), ("棍棒无双", Eff.Dmg, 0.35f, 2), ("玉麒麟", Eff.Crit, 0.2f, 1), ("疾枪", Eff.Speed, 0.3f, 1),
                ("枪棒天下", Eff.Dmg, 1f, 1), ("踏阵", Eff.UltPow, 1f, 1), ("麒麟", Eff.Speed, 0.5f, 1));
            Add(HeroId.GuanSheng, ("偃月斩", Eff.Area, 0.35f, 2), ("斩将", Eff.Elite, 1f, 1), ("赤面", Eff.Dmg, 0.35f, 2), ("快刀", Eff.Speed, 0.3f, 1),
                ("青龙出水", Eff.Dmg, 1f, 1), ("拖刀斩", Eff.UltPow, 1f, 1), ("刀气纵横", Eff.Area, 0.6f, 1));
            Add(HeroId.YanQing, ("连弩", Eff.Count, 1, 2), ("破甲箭", Eff.Chain, 1, 1), ("百步", Eff.Crit, 0.2f, 1), ("劲弩", Eff.Dmg, 0.35f, 2),
                ("万箭", Eff.Count, 2, 1), ("封喉", Eff.UltPow, 1f, 1), ("燕子三抄水", Eff.Speed, 0.5f, 1));
            Add(HeroId.FanRui, ("狂风", Eff.Area, 0.35f, 2), ("走石", Eff.Stun, 0.3f, 1), ("魔焰", Eff.Dmg, 0.35f, 2), ("迷阵", Eff.Slow, 1f, 1),
                ("混世", Eff.Dmg, 1f, 1), ("飞沙", Eff.UltPow, 1f, 1), ("魔王", Eff.Area, 0.6f, 1));
            Add(HeroId.HuSanniang, ("双刀", Eff.Dmg, 0.35f, 2), ("长索", Eff.Count, 1, 2), ("缚", Eff.Stun, 0.5f, 1), ("擒将", Eff.Elite, 0.6f, 1),
                ("红锦", Eff.Count, 2, 1), ("擒王", Eff.UltPow, 1f, 1), ("胭脂马", Eff.Speed, 0.5f, 1));
            Add(HeroId.AnDaoquan, ("金针", Eff.Aura, 0.5f, 2), ("药王", Eff.Dmg, 0.5f, 1), ("回春", Eff.UltCd, 0.25f, 1), ("灵药", Eff.Speed, 0.3f, 1),
                ("华佗再世", Eff.Aura, 1f, 1), ("起死回生", Eff.UltPow, 1f, 1), ("济世", Eff.Speed, 0.5f, 1));
            Add(HeroId.DaiZong, ("甲马", Eff.Aura, 0.5f, 2), ("神行", Eff.Speed, 0.4f, 2), ("符咒", Eff.Dmg, 0.5f, 1), ("急递", Eff.UltCd, 0.25f, 1),
                ("千里", Eff.Aura, 1f, 1), ("神速", Eff.Speed, 0.6f, 1), ("太保", Eff.UltPow, 1f, 1));
            Add(HeroId.QinMing, ("烈焰", Eff.Area, 0.35f, 2), ("狼牙", Eff.Dmg, 0.35f, 2), ("连环火", Eff.Count, 1, 1), ("暴躁", Eff.Speed, 0.3f, 1),
                ("天火", Eff.Dmg, 1f, 1), ("火流星", Eff.UltPow, 1f, 1), ("焚城", Eff.Area, 0.6f, 1));

            Add(HeroId.LiKui, ("双斧", Eff.Count, 1, 1), ("旋风", Eff.Area, 0.4f, 2), ("莽撞", Eff.Speed, 0.35f, 2), ("砍杀", Eff.Dmg, 0.35f, 1),
                ("杀四虎", Eff.Elite, 1.5f, 1), ("黑旋风", Eff.UltPow, 1f, 1), ("铁牛", Eff.Dmg, 1f, 1));
            Add(HeroId.QiaoDaoqing, ("真火", Eff.Dmg, 0.35f, 2), ("火海", Eff.Area, 0.35f, 2), ("幻影", Eff.Count, 1, 1), ("法力", Eff.Speed, 0.3f, 1),
                ("炼狱", Eff.Dmg, 1f, 1), ("天火焚营", Eff.UltPow, 1f, 1), ("幻魔", Eff.Count, 2, 1));
            Add(HeroId.ZhuWu, ("阵眼", Eff.Area, 0.35f, 2), ("困阵", Eff.Slow, 1f, 2), ("锁阵", Eff.Stun, 0.25f, 1), ("军师", Eff.Dmg, 0.4f, 1),
                ("金锁", Eff.UltPow, 1f, 1), ("奇门", Eff.Area, 0.6f, 1), ("遁甲", Eff.Speed, 0.5f, 1));
            Add(HeroId.XuNing, ("钩镰", Eff.Cav, 0.5f, 2), ("金枪班", Eff.Dmg, 0.35f, 2), ("绊马", Eff.Stun, 0.25f, 1), ("长钩", Eff.Range, 1.5f, 1),
                ("破连环", Eff.Cav, 1.5f, 1), ("枪阵", Eff.UltPow, 1f, 1), ("金枪手", Eff.Speed, 0.5f, 1));
            Add(HeroId.ShanTinggui, ("浪涌", Eff.Area, 0.35f, 2), ("寒水", Eff.Slow, 1f, 1), ("水龙", Eff.Dmg, 0.35f, 2), ("潮汐", Eff.Speed, 0.3f, 1),
                ("狂澜", Eff.UltPow, 1f, 1), ("圣水", Eff.Dmg, 1f, 1), ("沧海", Eff.Area, 0.6f, 1));
            Add(HeroId.WeiDingguo, ("火罐", Eff.Count, 1, 2), ("爆燃", Eff.Area, 0.35f, 2), ("神火", Eff.Dmg, 0.35f, 1), ("火雨", Eff.Split, 1, 1),
                ("火龙", Eff.UltPow, 1f, 1), ("焚天", Eff.Dmg, 1f, 1), ("连环罐", Eff.Count, 2, 1));

            Add(HeroId.ShiXiu, ("快刀", Eff.Speed, 0.35f, 2), ("拼命", Eff.Crit, 0.2f, 1), ("劈砍", Eff.Dmg, 0.35f, 2), ("刀风", Eff.Area, 0.4f, 1),
                ("三郎", Eff.Dmg, 1f, 1), ("不要命", Eff.Speed, 0.6f, 1), ("劫法场", Eff.UltPow, 1f, 1));
            Add(HeroId.XieZhen, ("双箭", Eff.Count, 1, 2), ("猎鹰", Eff.Fly, 0.6f, 1), ("强弓", Eff.Dmg, 0.35f, 2), ("穿杨", Eff.Chain, 1, 1),
                ("打虎", Eff.Elite, 1.5f, 1), ("猎神", Eff.Dmg, 1f, 1), ("箭雨", Eff.UltPow, 1f, 1));
            Add(HeroId.XieBao, ("穿刺", Eff.Chain, 1, 2), ("猛叉", Eff.Dmg, 0.35f, 2), ("双叉", Eff.Count, 1, 1), ("蝎尾", Eff.Stun, 0.2f, 1),
                ("贯日", Eff.Chain, 3, 1), ("双尾", Eff.Dmg, 1f, 1), ("飞叉阵", Eff.UltPow, 1f, 1));
            Add(HeroId.MaLing, ("金砖", Eff.Dmg, 0.35f, 2), ("镇压", Eff.Stun, 0.2f, 1), ("风火轮", Eff.Speed, 0.3f, 2), ("金光", Eff.Chain, 1, 1),
                ("破阵", Eff.Dmg, 1f, 1), ("镇魔", Eff.UltPow, 1f, 1), ("神驹", Eff.Speed, 0.6f, 1));
            Add(HeroId.KongLiang, ("加料", Eff.Area, 0.35f, 2), ("连抛", Eff.Count, 1, 1), ("火星", Eff.Split, 1, 1), ("引线", Eff.Speed, 0.3f, 2),
                ("燎原", Eff.Dmg, 1f, 1), ("星火", Eff.UltPow, 1f, 1), ("火海", Eff.Area, 0.6f, 1));
            Add(HeroId.TangLong, ("淬火", Eff.Aura, 0.5f, 2), ("重锤", Eff.Dmg, 0.5f, 1), ("打钩镰", Eff.Elite, 0.5f, 1), ("锻打", Eff.Speed, 0.3f, 1),
                ("神兵", Eff.Aura, 1f, 1), ("百炼", Eff.UltPow, 1f, 1), ("豹子", Eff.Dmg, 1f, 1));
            Add(HeroId.SunErniang, ("加量", Eff.Area, 0.35f, 2), ("药力", Eff.Stun, 0.2f, 1), ("毒雾", Eff.Dmg, 0.4f, 2), ("迷香", Eff.Slow, 1f, 1),
                ("黑店", Eff.UltPow, 1f, 1), ("夜叉", Eff.Dmg, 1f, 1), ("迷魂", Eff.Area, 0.6f, 1));
            Add(HeroId.RuanXiaoqi, ("大网", Eff.Area, 0.35f, 2), ("缠", Eff.Stun, 0.5f, 1), ("水性", Eff.Speed, 0.3f, 2), ("阎罗", Eff.Dmg, 0.4f, 1),
                ("天罗地网", Eff.UltPow, 1f, 1), ("捞鱼", Eff.Area, 0.6f, 1), ("浪里", Eff.Speed, 0.6f, 1));
            Add(HeroId.ShiQian, ("妙手", Eff.Aura, 0.5f, 2), ("飞镖", Eff.Count, 1, 1), ("偷鸡", Eff.Coin, 10, 1), ("轻功", Eff.Speed, 0.4f, 1),
                ("盗甲", Eff.Aura, 1f, 1), ("神偷", Eff.UltPow, 1f, 1), ("鼓上蚤", Eff.Dmg, 1f, 1));
            Add(HeroId.JiangJing, ("复利", Eff.Aura, 0.5f, 2), ("算珠", Eff.Dmg, 0.5f, 1), ("精打细算", Eff.UltCd, 0.25f, 1), ("拨珠", Eff.Speed, 0.3f, 1),
                ("富甲", Eff.Aura, 1f, 1), ("神算", Eff.UltPow, 1f, 1), ("账房", Eff.Speed, 0.6f, 1));
            return list.ToArray();
        }

        static string Pct(float v) => (int)System.Math.Round(v * 100) + "%";

        /// <summary>效果说明：强化卡、被动、好汉详情共用。</summary>
        public static string EffText(HeroId h, Eff e, float v)
        {
            var d = Hero(h);
            string n = d.Noun ?? "攻击";
            switch (e)
            {
                case Eff.Dmg: return $"{n}伤害 +{Pct(v)}";
                case Eff.Speed: return $"攻速 +{Pct(v)}";
                case Eff.Count:
                    if (d.Shot == ShotKind.Lasso) return $"套索同时锁 +{v:0} 人";
                    if (d.Shot == ShotKind.Punch) return $"每次出手多砍 {v:0} 下";
                    return $"{n}数量 +{v:0}\n单发伤害 -{Pct(0.15f * v)}";
                case Eff.Area: return $"{n}范围 +{Pct(v)}";
                case Eff.Chain:
                    if (d.Shot == ShotKind.Stone) return $"{n}弹射 +{v:0} 次";
                    if (d.Shot == ShotKind.Lightning) return $"{n}连锁 +{v:0} 个敌人";
                    return $"{n}穿透 +{v:0} 个敌人";
                case Eff.Crit: return $"暴击率 +{Pct(v)}";
                case Eff.Stun:
                    if (d.Stun > 0 && d.StunChance <= 0) return $"控制时间 +{v:0.#} 秒";
                    return $"命中 {Pct(v)} 几率击晕";
                case Eff.Slow: return d.Slow > 0 ? $"减速时间 +{v:0.#} 秒" : $"命中减速 {v:0.#} 秒";
                case Eff.Elite: return $"对精英、首领\n伤害 +{Pct(v)}";
                case Eff.UltCd: return $"大招冷却 -{Pct(v)}";
                case Eff.UltPow: return $"大招「{d.UltName}」\n效果 +{Pct(v)}";
                case Eff.Range: return $"攻击距离 +{v:0.#}";
                case Eff.Split: return $"{n}落地后\n再分裂 3 枚小{n}";
                case Eff.Coin: return $"每次合成额外\n获得 {v:0} 银两";
                case Eff.Fly: return $"对飞行敌人\n伤害 +{Pct(v)}";
                case Eff.Cav: return $"对骑兵、连环马\n伤害 +{Pct(v)}";
                default:
                    switch (d.Aura)
                    {
                        case AuraKind.Speed: return $"攻速光环 +{Pct(v)}";
                        case AuraKind.Dmg: return $"伤害光环 +{Pct(v)}";
                        case AuraKind.Charge: return $"大招充能光环 +{Pct(v)}";
                        case AuraKind.Heal: return $"回墙量 +{Pct(v)}";
                        case AuraKind.Coin: return $"击杀银两加成 +{Pct(v)}";
                        case AuraKind.Interest: return $"银两利息 +{Pct(v)}";
                        default: return $"光环 +{Pct(v)}";
                    }
            }
        }

        public static List<PerkDef> PerksOf(HeroId h, bool star)
        {
            var r = new List<PerkDef>();
            foreach (var p in Perks) if (p.Hero == h && p.Star == star) r.Add(p);
            return r;
        }

        public static PerkDef Perk(string id)
        {
            foreach (var p in Perks) if (p.Id == id) return p;
            return null;
        }

        /// <summary>同格英雄的星级倍率：1 星到 4 星（满星）。</summary>
        public static readonly float[] StarMul = { 1f, 2.2f, 4.6f, 9.5f };
        public static readonly float[] StarSpeed = { 1f, 1.08f, 1.16f, 1.3f };

        public const float WaveTime = 25f;
        public const int SummonsPerSlot = 5;
        public const int LevelsPerChapter = 10;

        public static int SummonCost(int n) => 30 + n * 10 + (n * n) / 4;
        public static int EnhanceCost(int n) => (int)(60 * System.Math.Pow(1.7, n) / 5) * 5;
        public static float HpScale(int wave) => 1f + 0.13f * (wave - 1);

        // ---------------- 关卡 ----------------

        public static readonly TroopMix[] Mix1 =
        {
            new TroopMix(EnemyKind.Cavalry, 0.10f, 0.012f, 2),
            new TroopMix(EnemyKind.Shield, 0.20f, 0.022f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.27f, 0.028f, 4),
        };

        static readonly TroopMix[] Mix2 =
        {
            new TroopMix(EnemyKind.Hawk, 0.10f, 0.012f, 1),
            new TroopMix(EnemyKind.Cavalry, 0.18f, 0.015f, 2),
            new TroopMix(EnemyKind.Shield, 0.27f, 0.020f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.34f, 0.024f, 4),
        };

        static readonly TroopMix[] Mix3 =
        {
            new TroopMix(EnemyKind.Armored, 0.07f, 0.010f, 2),
            new TroopMix(EnemyKind.Hawk, 0.14f, 0.012f, 1),
            new TroopMix(EnemyKind.Cavalry, 0.21f, 0.014f, 2),
            new TroopMix(EnemyKind.Shield, 0.28f, 0.018f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.35f, 0.022f, 4),
        };

        static readonly TroopMix[] Mix4 =
        {
            new TroopMix(EnemyKind.FireBomb, 0.05f, 0.008f, 2),
            new TroopMix(EnemyKind.Cavalry, 0.16f, 0.018f, 1),
            new TroopMix(EnemyKind.Armored, 0.23f, 0.020f, 3),
            new TroopMix(EnemyKind.Hawk, 0.29f, 0.020f, 2),
            new TroopMix(EnemyKind.Shield, 0.35f, 0.020f, 4),
            new TroopMix(EnemyKind.Crossbow, 0.41f, 0.022f, 5),
        };

        static readonly TroopMix[] Mix5 =
        {
            new TroopMix(EnemyKind.FireBomb, 0.05f, 0.008f, 2),
            new TroopMix(EnemyKind.Armored, 0.12f, 0.014f, 2),
            new TroopMix(EnemyKind.Hawk, 0.19f, 0.014f, 1),
            new TroopMix(EnemyKind.Cavalry, 0.26f, 0.016f, 2),
            new TroopMix(EnemyKind.Shield, 0.33f, 0.018f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.39f, 0.022f, 4),
        };

        public sealed class ChapterDef
        {
            public string Name, Bg, Intro;
            public EnemyKind Boss;
            public TroopMix[] Mix;
            public EnemyKind[] Elites;
            public string[] Levels;
        }

        public static readonly ChapterDef[] Chapters =
        {
            new ChapterDef { Name = "金沙滩", Bg = "battle_bg", Boss = EnemyKind.Boss, Mix = Mix1, Elites = new[] { EnemyKind.Elite },
                Intro = "济州官军来犯水泊",
                Levels = new[] { "金沙滩", "鸭嘴滩", "李家道口", "朱贵酒店", "黄安水寨", "蓼儿洼", "断金亭", "石碣村", "宛子城", "梁山泊" } },
            new ChapterDef { Name = "三打祝家庄", Bg = "battle_bg_ch2", Boss = EnemyKind.Luan, Mix = Mix2, Elites = new[] { EnemyKind.Elite },
                Intro = "独龙冈上祝家庄，鹰隼盘旋",
                Levels = new[] { "独龙冈", "盘陀路", "白杨林", "扈家庄", "祝家店", "李家庄", "时迁偷鸡", "吊桥", "祝龙寨", "祝家庄" } },
            new ChapterDef { Name = "夜打曾头市", Bg = "battle_bg_ch3", Boss = EnemyKind.ShiWen, Mix = Mix3, Elites = new[] { EnemyKind.Elite },
                Intro = "曾家五虎铁甲列阵",
                Levels = new[] { "青州道", "照夜玉狮", "法华寺", "曾家五虎", "曾头北寨", "凌州", "东平府", "东昌府", "曾家大营", "曾头市" } },
            new ChapterDef { Name = "大破连环马", Bg = "battle_bg_ch4", Boss = EnemyKind.HuYan, Mix = Mix4, Elites = new[] { EnemyKind.ChainHorse, EnemyKind.Elite },
                Intro = "风雪中铁骑连环而来",
                Levels = new[] { "汝宁州", "高唐州", "雪夜", "钩镰枪", "韩滔营", "彭玘营", "金枪班", "徐宁", "连环阵", "呼延大营" } },
            new ChapterDef { Name = "三败高太尉", Bg = "battle_bg_ch5", Boss = EnemyKind.GaoQiu, Mix = Mix5, Elites = new[] { EnemyKind.ChainHorse, EnemyKind.Elite },
                Intro = "十节度兵马夜袭水泊",
                Levels = new[] { "济州城", "十节度", "王焕", "荆忠", "党世英", "水泊芦荡", "刘梦龙", "牛邦喜", "梁山水寨", "擒高俅" } },
        };

        static readonly int[][] EliteSets = { new[] { 2, 6, 9 }, new[] { 3, 7, 9 }, new[] { 3, 6, 9 }, new[] { 3, 7, 10 }, new[] { 2, 7, 9 } };
        static readonly Role[] BuffCycle = { Sim.Role.Mage, Sim.Role.Archer, Sim.Role.Fighter, Sim.Role.Bomber, Sim.Role.Control, Sim.Role.Archer, Sim.Role.Mage, Sim.Role.Fighter };

        /// <summary>第 n 关（1 起）的敌人血量倍率：跟着正常推进时的养成战力走，再逐关加压，落后养成就过不去。大招开局即有，整体血量 ×1.6 起。</summary>
        public static float StageHp(int n) => Meta.ExpectedPower(n) * (1.6f - 0.004f * (n - 1) - 0.02f * System.Math.Max(0, n - 40)) * (1f + 0.35f * System.Math.Min(1f, (n - 1) / 12f) + 0.009f * (n - 1) + 0.00015f * (n - 1) * (n - 1));
        public static float StageAtk(int n) => Meta.ExpectedWall(n) * (1f + 0.012f * (n - 1));

        public static readonly LevelDef[] Levels = BuildLevels();
        public static int LevelCount => Levels.Length;
        public static LevelDef Level(int no) => Levels[System.Math.Max(1, System.Math.Min(no, Levels.Length)) - 1];

        static LevelDef[] BuildLevels()
        {
            var list = new List<LevelDef>();
            for (int c = 0; c < Chapters.Length; c++)
            {
                var ch = Chapters[c];
                for (int i = 1; i <= LevelsPerChapter; i++)
                {
                    int n = c * LevelsPerChapter + i;
                    bool chBoss = i == 5 || i == 10;
                    var d = new LevelDef
                    {
                        No = n, Chapter = c + 1, Index = i, Name = ch.Levels[i - 1], Bg = ch.Bg,
                        Intro = chBoss ? $"首领 {Enemy(ch.Boss).Name} 亲至，守住寨墙" : n == 1 ? "击退十波官军，寨墙不能倒" : ch.Intro,
                        EliteWaves = EliteSets[(n - 1) % EliteSets.Length],
                        EliteKinds = ch.Elites, Troops = ch.Mix,
                        BossKind = chBoss || c == 0 ? ch.Boss : Chapters[c - 1].Boss,
                        BossMul = i == 10 ? 1.0f : i == 5 ? 0.8f : c == 0 ? 1f : 0.7f,
                        HpMul = StageHp(n), AtkMul = StageAtk(n),
                        CountMul = 1f + 0.05f * c + (i == 10 ? 0.08f : 0f),
                    };
                    if (n > 1)
                    {
                        d.BuffRole = BuffCycle[(n * 3 + c) % BuffCycle.Length];
                        d.BuffPct = chBoss ? 0.5f : 0.3f;
                    }
                    list.Add(d);
                }
            }
            return list.ToArray();
        }

        /// <summary>校场演武：按当前进度出一关加强版，每天换增益。</summary>
        public static LevelDef DailyStage(int cleared, int day)
        {
            int n = System.Math.Max(1, System.Math.Min(LevelCount, cleared + 2));
            var b = Level(n);
            var d = new LevelDef
            {
                No = n, Chapter = b.Chapter, Index = b.Index, Name = "校场演武", Bg = b.Bg, Intro = "守满十波，夺校场头名",
                EliteWaves = new[] { 2, 4, 6, 8, 9 }, EliteKinds = b.EliteKinds, Troops = b.Troops,
                BossKind = Chapters[(day % Chapters.Length + Chapters.Length) % Chapters.Length].Boss, BossMul = 1.2f,
                HpMul = b.HpMul * 1.1f, AtkMul = b.AtkMul, CountMul = b.CountMul + 0.1f,
                BuffRole = BuffCycle[(day % BuffCycle.Length + BuffCycle.Length) % BuffCycle.Length], BuffPct = 0.6f,
                Mode = StageMode.Daily,
            };
            return d;
        }

        /// <summary>劫生辰纲：六波，每波一串挑担的，打得越多铜钱越多。</summary>
        public static LevelDef EscortStage(int cleared)
        {
            int n = System.Math.Max(1, System.Math.Min(LevelCount, cleared));
            var b = Level(n);
            return new LevelDef
            {
                No = n, Chapter = b.Chapter, Index = b.Index, Name = "黄泥冈劫纲", Bg = "battle_bg", Intro = "截下生辰纲，挑担越多赏越多",
                Waves = 6, BossWave = 0, RageWave = 5, EliteWaves = new[] { 4 }, PorterWaves = new[] { 1, 2, 3, 4, 5, 6 }, PorterCount = 3,
                EliteKinds = b.EliteKinds, Troops = Mix1, HpMul = b.HpMul * 0.9f, AtkMul = b.AtkMul, CountMul = 0.6f,
                Mode = StageMode.Escort,
            };
        }

        /// <summary>每波的兵（不含精英、首领、生辰纲），按出场顺序。</summary>
        public static List<EnemyKind> WaveTroops(LevelDef st, int wave, Rng rng)
        {
            var list = new List<EnemyKind>();
            int total = (int)((18 + (int)((wave - 1) * 14.4f)) * st.CountMul);
            for (int i = 0; i < total; i++)
            {
                float r = rng.F();
                EnemyKind k = EnemyKind.Soldier;
                foreach (var m in st.Troops)
                    if (wave >= m.From && r < m.Base + wave * m.Grow) { k = m.Kind; break; }
                list.Add(k);
            }
            return list;
        }
    }
}

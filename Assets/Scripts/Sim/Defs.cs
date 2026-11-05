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
    public enum Eff { Dmg, Speed, Count, Area, Chain, Crit, Stun, Slow, Elite, UltCd, UltPow, Range, Split, Aura, Coin, Fly, Cav, Surge, Top }

    /// <summary>传说好汉的大招，和辅助的技能（Bless 之后几种只给辅助用）。</summary>
    public enum UltKind
    {
        Thunder, Frost, Sweep, Haste, Heal, Bind, Snipe, Tornado, Meteor,
        Bless, Rush, Mend, Forge, Steal, Interest,
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

        /// <summary>
        /// 技能：弹道种类、每发伤害系数（好汉攻击力的倍数）、冷却秒数、射程（近战才有限制）。
        /// 冷却按职业拉开：射手约 2 秒一轮齐射，战士 3~4 秒一套连击，炮手 5 秒，控制 6 秒，法师 12~15 秒一次持续施法，辅助 18~28 秒一次加持。
        /// </summary>
        public ShotKind Shot;
        public float Coef, Interval, Range = 99, Radius = 0.4f, Knock, Stun, StunChance, Slow;
        public int Count = 1, Chain;
        public string AtkName, AtkIcon, Noun;
        /// <summary>弹道、命中特效的色调（空 = 弹道自带颜色）。</summary>
        public string Tint;
        /// <summary>这位好汉自己的兵器弹道图（空 = 按弹道种类的通用图），图里兵器尖朝上；ProjSpin 为飞行中打旋。</summary>
        public string Proj;
        public bool ProjSpin;
        public float ProjSize = 1f;
        /// <summary>法师持续施法：持续秒数、每跳间隔；Coef 是每跳的伤害。</summary>
        public float Chan, Tick;
        /// <summary>辅助的技能：不打伤害，给友军加持。BuffV 是效果量。</summary>
        public UltKind? Buff;
        public float BuffV;

        /// <summary>大招：只有传说好汉有，局内强化抽到「大招」卡才解锁。UltCoef 是好汉攻击力的倍数。</summary>
        public UltKind Ult;
        public string UltName, UltIcon, UltTip;
        public float UltCoef, UltCd, UltRadius = 1.6f, UltStun;
        public int UltN = 1;

        /// <summary>10 级解锁的被动。</summary>
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
        public bool HasUlt => Rarity >= 4;
        /// <summary>每秒对单体的期望输出（× 攻击力），战力估算用。</summary>
        public float Dps => Buff.HasValue ? 0 : Coef * Count * (Chan > 0 ? Chan / Tick : 1f) / Interval;
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
        Diver, Farmer, Hound, Rattan, Taoist, Berserker, Catapult, Lantern, Scout, Brute,
        HeTao, ZhuChaofeng, ZengKui, HanTao, LiuMenglong,
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

    /// <summary>好汉专属强化。Star 为满星额外技能，只在合成满星时三选一；Ult 是传说好汉的「大招」卡，混在强化三选一里随机出。</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Desc;
        public HeroId Hero;
        public int Max = 1;
        public float Weight = 1;
        public Eff Eff;
        public float V;
        public bool Star, Ult;
        /// <summary>好汉等级解锁的选项：1 = 5 级解锁的「爆发」，2 = 20 级解锁的「最强」；没到等级不进卡池。</summary>
        public int Opt;
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
        /// <summary>替天行道的黄半圆：圆心在城墙中点，半径到战场左右边，只朝上。点选范围和实际命中用同一条。</summary>
        public static bool InLordRange(V2 p)
        {
            float dx = p.X;
            float dy = p.Y - WallRowY;
            if (dy < 0f) return false;
            return dx * dx + dy * dy <= HalfW * HalfW;
        }

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
        /// 伤害系数 Coef 是好汉攻击力的倍数（寨主初始攻击 50）。每位好汉一个带冷却的技能，冷却越长单次越重，
        /// 同稀有度各职业每秒输出相近：射手单体稳定，战士近身爆发，炮手、法师吃范围，控制伤害低但控场，辅助不打伤害只加持。
        /// 稀有度越高同职业输出约高两成；传说好汉另有大招，要在局内强化里抽到才解锁。
        /// </summary>
        public static readonly HeroDef[] Heroes =
        {
            // ---------- 初始四人与早期入伙 ----------
            new HeroDef { Id = HeroId.HuaRong, Name = "花荣", Nick = "小李广", Kind = Sim.Role.Archer, Rarity = 3, Art = "hero_huarong",
                Shot = ShotKind.Arrow, Coef = 0.46f, Interval = 2.0f, Count = 3, AtkName = "穿云箭", AtkIcon = "ic_arrow", Noun = "箭",
                PassiveName = "神射", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "一轮三箭的单体射手，克制飞行敌人" },
            new HeroDef { Id = HeroId.LingZhen, Name = "凌振", Nick = "轰天雷", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_lingzhen", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.84f, Interval = 5.0f, Count = 3, Radius = 1.0f, AtkName = "霹雳炮", AtkIcon = "ic_bomb", Noun = "炸弹",
                PassiveName = "火药精通", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "一轮三炮，专清密集兵潮" },
            new HeroDef { Id = HeroId.GongSun, Name = "公孙胜", Nick = "入云龙", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_gongsun", Element = "雷系",
                Shot = ShotKind.Lightning, Coef = 0.70f, Interval = 14f, Chan = 3f, Tick = 0.3f, Radius = 0.75f, Chain = 1, AtkName = "雷暴", AtkIcon = "fx_zap", Noun = "落雷",
                Ult = UltKind.Thunder, UltName = "五雷天罡", UltIcon = "fx_lightning", UltCoef = 2.8f, UltCd = 30, UltN = 8, UltStun = 1f, UltRadius = 1.15f,
                UltTip = "8道天雷劈向敌群\n每道麻痹1秒",
                PassiveName = "道法自然", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "引来一场雷暴，落雷连锁，克制重甲" },
            new HeroDef { Id = HeroId.WuSong, Name = "武松", Nick = "行者", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_wusong",
                Shot = ShotKind.Punch, Coef = 1.47f, Interval = 3.5f, Count = 3, Range = 4.6f, Radius = 0.8f, Knock = 0.35f, AtkName = "连环铁拳", AtkIcon = "ic_fist", Noun = "铁拳",
                PassiveName = "景阳冈打虎", PassiveEff = Eff.Elite, PassiveV = 0.6f, Desc = "近身三连拳，击退贴墙的敌人" },
            new HeroDef { Id = HeroId.ZhangQing, Name = "张清", Nick = "没羽箭", Kind = Sim.Role.Archer, Rarity = 3, Art = "hero_zhangqing",
                Shot = ShotKind.Stone, Coef = 1.1f, Interval = 2.0f, Radius = 0.3f, Chain = 2, AtkName = "飞石", AtkIcon = "fx_rock0", Noun = "飞石",
                PassiveName = "百发百中", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "飞石在敌人之间弹射" },
            new HeroDef { Id = HeroId.LinChong, Name = "林冲", Nick = "豹子头", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_linchong",
                Shot = ShotKind.Spear, Coef = 4.0f, Interval = 3.5f, Range = 6.2f, Radius = 0.55f, Knock = 0.15f, AtkName = "丈八蛇矛", AtkIcon = "wp_snakespear", Proj = "wp_snakespear", Noun = "长枪",
                Ult = UltKind.Frost, UltName = "风雪山神庙", UltIcon = "fx_ice", UltCoef = 9.2f, UltCd = 30, UltRadius = 1.9f, UltStun = 2.5f,
                UltTip = "风雪冰封一片敌人\n冻住2.5秒",
                PassiveName = "禁军教头", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "长枪贯穿一列，攻击距离远" },
            new HeroDef { Id = HeroId.LuZhiShen, Name = "鲁智深", Nick = "花和尚", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_luzhishen",
                Shot = ShotKind.Slam, Coef = 5.4f, Interval = 4.0f, Range = 5.2f, Radius = 1.15f, Knock = 0.3f, AtkName = "禅杖", AtkIcon = "fx_crack", Noun = "禅杖",
                PassiveName = "大闹五台山", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "禅杖砸地，大范围震退" },
            new HeroDef { Id = HeroId.WuYong, Name = "吴用", Nick = "智多星", Kind = Sim.Role.Support, Rarity = 3, Art = "hero_wuyong", Element = "风系",
                Shot = ShotKind.Wind, Interval = 28f, Buff = UltKind.Bless, BuffV = 1f, AtkName = "运筹帷幄", AtkIcon = "prop_drum", Noun = "锦囊",
                Aura = AuraKind.Speed, AuraV = 0.04f,
                PassiveName = "锦囊妙计", PassiveEff = Eff.Coin, PassiveV = 15, Desc = "祝福输出好汉加攻速，在场提升全军攻速" },

            // ---------- 传说 ----------
            new HeroDef { Id = HeroId.LuJunyi, Name = "卢俊义", Nick = "玉麒麟", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_lujunyi",
                Shot = ShotKind.Spear, Coef = 4.4f, Interval = 3.5f, Range = 6.0f, Radius = 0.8f, Knock = 0.2f, AtkName = "麒麟枪", AtkIcon = "wp_spear", Proj = "wp_spear", Noun = "长枪",
                Ult = UltKind.Sweep, UltName = "麒麟踏阵", UltIcon = "fx_wave", UltCoef = 8.5f, UltCd = 30, UltN = 5,
                UltTip = "枪扫墙前一整排\n五段击飞",
                PassiveName = "河北三绝", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "宽幅枪扫，一次挑翻一列" },
            new HeroDef { Id = HeroId.GuanSheng, Name = "关胜", Nick = "大刀", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_guansheng",
                Shot = ShotKind.Slam, Coef = 6.4f, Interval = 4.0f, Range = 5.4f, Radius = 1.3f, Knock = 0.3f, AtkName = "青龙偃月刀", AtkIcon = "wp_slash", Proj = "wp_slash", Tint = "5FE08A", Noun = "大刀",
                Ult = UltKind.Sweep, UltName = "拖刀计", UltIcon = "fx_shock", UltCoef = 18f, UltCd = 30, UltN = 5,
                UltTip = "大刀横扫墙前\n五段重劈",
                PassiveName = "武圣遗风", PassiveEff = Eff.Elite, PassiveV = 0.5f, Desc = "大刀劈砍，范围大伤害高" },
            new HeroDef { Id = HeroId.YanQing, Name = "燕青", Nick = "浪子", Kind = Sim.Role.Archer, Rarity = 4, Art = "hero_yanqing",
                Shot = ShotKind.Arrow, Coef = 0.34f, Interval = 1.8f, Count = 5, AtkName = "连弩", AtkIcon = "ic_arrow", Noun = "弩箭",
                Ult = UltKind.Snipe, UltName = "一箭封喉", UltIcon = "fx_goldarrow", UltCoef = 11f, UltCd = 30,
                UltTip = "瞄准场上最强敌人\n一箭重创",
                PassiveName = "浪子身手", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "连弩一轮五发，大招专点首领" },
            new HeroDef { Id = HeroId.FanRui, Name = "樊瑞", Nick = "混世魔王", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_fanrui", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.65f, Interval = 14f, Chan = 3f, Tick = 0.25f, Radius = 1.1f, Slow = 1.5f, AtkName = "旋风咒", AtkIcon = "fx_smoke", Noun = "旋风", Tint = "C9D9C2",
                Ult = UltKind.Tornado, UltName = "飞沙走石", UltIcon = "fx_rock1", UltCoef = 7.7f, UltCd = 30, UltRadius = 2.2f, UltStun = 1f,
                UltTip = "龙卷风卷起敌群\n眩晕1秒",
                PassiveName = "妖法", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "旋风缠住目标持续绞杀，范围减速" },
            new HeroDef { Id = HeroId.HuSanniang, Name = "扈三娘", Nick = "一丈青", Kind = Sim.Role.Control, Rarity = 4, Art = "hero_husanniang",
                Shot = ShotKind.Lasso, Coef = 1.17f, Interval = 6.0f, Count = 3, Stun = 1.2f, AtkName = "红锦套索", AtkIcon = "wp_lasso", Proj = "wp_lasso", ProjSpin = true, Noun = "套索", Tint = "E8483A",
                Ult = UltKind.Bind, UltName = "阵前擒将", UltIcon = "ic_hand", UltCoef = 6.6f, UltCd = 30, UltN = 5, UltStun = 2.5f,
                UltTip = "同时套住5个敌人\n优先精英首领",
                PassiveName = "日月双刀", PassiveEff = Eff.Count, PassiveV = 1, Desc = "一次套三人，连精英首领也能拽住" },
            new HeroDef { Id = HeroId.AnDaoquan, Name = "安道全", Nick = "神医", Kind = Sim.Role.Support, Rarity = 4, Art = "hero_andaoquan", Element = "风系",
                Shot = ShotKind.Wind, Interval = 20f, Buff = UltKind.Mend, BuffV = 0.08f, AtkName = "妙手回春", AtkIcon = "ic_heart", Noun = "药", Tint = "8FF0A0",
                Aura = AuraKind.Heal,
                Ult = UltKind.Heal, UltName = "悬壶济世", UltIcon = "ic_heart", UltCoef = 0.3f, UltCd = 40,
                UltTip = "寨墙回复30%\n解除全部晕眩",
                PassiveName = "神医圣手", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时给寨墙回血，解除好汉晕眩" },
            new HeroDef { Id = HeroId.DaiZong, Name = "戴宗", Nick = "神行太保", Kind = Sim.Role.Support, Rarity = 4, Art = "hero_daizong",
                Shot = ShotKind.Stone, Interval = 20f, Buff = UltKind.Rush, BuffV = 0.3f, AtkName = "神行甲马", AtkIcon = "ic_fast", Noun = "甲马", Tint = "FFD84A",
                Aura = AuraKind.Charge, AuraV = 0.04f,
                Ult = UltKind.Haste, UltName = "日行千里", UltIcon = "ic_fast", UltCd = 35,
                UltTip = "全军攻速+60%\n持续8秒",
                PassiveName = "日行八百", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "给全队缩短冷却，在场冷却转得更快" },
            new HeroDef { Id = HeroId.QinMing, Name = "秦明", Nick = "霹雳火", Kind = Sim.Role.Bomber, Rarity = 4, Art = "hero_qinming", Element = "火系",
                Shot = ShotKind.Fireball, Coef = 1.06f, Interval = 5.0f, Count = 3, Radius = 1.1f, AtkName = "火流星", AtkIcon = "fx_fireball", Noun = "火球",
                Ult = UltKind.Meteor, UltName = "霹雳天火", UltIcon = "fx_explode", UltCoef = 3.8f, UltCd = 30, UltN = 10, UltRadius = 1.2f,
                UltTip = "天降10颗火流星\n砸进敌群",
                PassiveName = "性如烈火", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "一轮三颗大火球砸进人堆" },

            // ---------- 史诗 ----------
            new HeroDef { Id = HeroId.LiKui, Name = "李逵", Nick = "黑旋风", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_likui",
                Shot = ShotKind.Punch, Coef = 1.09f, Interval = 3.5f, Count = 4, Range = 4.6f, Radius = 0.9f, Knock = 0.25f, AtkName = "旋风双斧", AtkIcon = "wp_axe", Proj = "wp_axe", ProjSpin = true, Noun = "板斧",
                PassiveName = "黑旋风", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "双斧连砍四下，墙前清场" },
            new HeroDef { Id = HeroId.QiaoDaoqing, Name = "乔道清", Nick = "幻魔君", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_qiaodaoqing", Element = "火系",
                Shot = ShotKind.Beam, Coef = 0.45f, Interval = 15f, Chan = 3f, Tick = 0.2f, Radius = 0.45f, AtkName = "火焰射线", AtkIcon = "fx_fireball", Noun = "射线", Tint = "FF8A2A",
                PassiveName = "幻术", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "喷出火焰射线持续灼烧，法伤克制重甲" },
            new HeroDef { Id = HeroId.ZhuWu, Name = "朱武", Nick = "神机军师", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_zhuwu", Element = "风系",
                Shot = ShotKind.Wind, Coef = 1.9f, Interval = 6.0f, Radius = 1.6f, Slow = 3f, AtkName = "八阵图", AtkIcon = "prop_book", Noun = "阵法", Tint = "7FB4FF",
                PassiveName = "神机妙算", PassiveEff = Eff.Slow, PassiveV = 1f, Desc = "布阵困住一大片敌人，长时间减速" },
            new HeroDef { Id = HeroId.XuNing, Name = "徐宁", Nick = "金枪手", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_xuning",
                Shot = ShotKind.Spear, Coef = 3.6f, Interval = 5.0f, Range = 6.0f, Radius = 0.6f, Slow = 2f, AtkName = "钩镰枪", AtkIcon = "wp_hookspear", Proj = "wp_hookspear", Noun = "钩镰", Tint = "FFD27A",
                PassiveName = "金枪班", PassiveEff = Eff.Cav, PassiveV = 0.5f, Desc = "钩镰绊马，克制骑兵与连环马" },
            new HeroDef { Id = HeroId.ShanTinggui, Name = "单廷珪", Nick = "圣水将", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_shantinggui", Element = "水系",
                Shot = ShotKind.Beam, Coef = 0.42f, Interval = 15f, Chan = 3f, Tick = 0.2f, Radius = 0.5f, Slow = 1.5f, AtkName = "圣水射线", AtkIcon = "fx_icecrystal", Noun = "射线", Tint = "6FC8FF",
                PassiveName = "圣水灌顶", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "水柱持续冲刷一条线，沿途减速" },
            new HeroDef { Id = HeroId.WeiDingguo, Name = "魏定国", Nick = "神火将", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_weidingguo", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.84f, Interval = 5.0f, Count = 3, Radius = 1.1f, AtkName = "火罐", AtkIcon = "ic_bomb", Noun = "火罐", Tint = "FF7A3A",
                PassiveName = "神火", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "一轮三个火罐，炸开一片" },

            // ---------- 稀有 ----------
            new HeroDef { Id = HeroId.ShiXiu, Name = "石秀", Nick = "拼命三郎", Kind = Sim.Role.Fighter, Rarity = 2, Art = "hero_shixiu",
                Shot = ShotKind.Punch, Coef = 0.87f, Interval = 3.0f, Count = 3, Range = 4.6f, Radius = 0.6f, Knock = 0.15f, AtkName = "拼命三刀", AtkIcon = "wp_slash", Proj = "wp_slash", ProjSize = 0.9f, Noun = "朴刀",
                PassiveName = "拼命", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "一套三刀，暴击高" },
            new HeroDef { Id = HeroId.XieZhen, Name = "解珍", Nick = "两头蛇", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiezhen",
                Shot = ShotKind.Arrow, Coef = 0.40f, Interval = 2.2f, Count = 3, AtkName = "猎弓", AtkIcon = "ic_arrow", Noun = "箭",
                PassiveName = "登州猎户", PassiveEff = Eff.Fly, PassiveV = 0.5f, Desc = "一轮三箭，专打飞禽" },
            new HeroDef { Id = HeroId.XieBao, Name = "解宝", Nick = "双尾蝎", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiebao",
                Shot = ShotKind.Arrow, Coef = 0.58f, Interval = 2.4f, Count = 2, Chain = 2, AtkName = "飞叉", AtkIcon = "wp_fork", Proj = "wp_fork", Noun = "钢叉",
                PassiveName = "双尾蝎", PassiveEff = Eff.Chain, PassiveV = 1, Desc = "双叉穿透一串敌人" },
            new HeroDef { Id = HeroId.MaLing, Name = "马灵", Nick = "神驹子", Kind = Sim.Role.Mage, Rarity = 2, Art = "hero_maling", Element = "金系",
                Shot = ShotKind.Stone, Coef = 1.3f, Interval = 12f, Chan = 2.5f, Tick = 0.5f, Radius = 0.35f, Stun = 0.8f, StunChance = 0.3f, AtkName = "金砖镇魔", AtkIcon = "wp_goldbrick", Proj = "wp_goldbrick", ProjSpin = true, Noun = "金砖", Tint = "FFD24A",
                PassiveName = "风火轮", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "连砸五块金砖，有几率击晕" },
            new HeroDef { Id = HeroId.KongLiang, Name = "孔亮", Nick = "独火星", Kind = Sim.Role.Bomber, Rarity = 2, Art = "hero_kongliang", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 0.63f, Interval = 4.5f, Count = 3, Radius = 0.8f, AtkName = "火雷", AtkIcon = "ic_bomb", Noun = "火雷",
                PassiveName = "独火星", PassiveEff = Eff.Count, PassiveV = 1, Desc = "一轮三颗小火雷" },
            new HeroDef { Id = HeroId.TangLong, Name = "汤隆", Nick = "金钱豹子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_tanglong",
                Shot = ShotKind.Stone, Interval = 24f, Buff = UltKind.Forge, BuffV = 0.4f, AtkName = "打造神兵", AtkIcon = "ic_gear", Noun = "神兵", Tint = "A8AEB8",
                Aura = AuraKind.Dmg, AuraV = 0.03f,
                PassiveName = "铁匠世家", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时打造神兵加伤害，在场提升全军伤害" },
            new HeroDef { Id = HeroId.SunErniang, Name = "孙二娘", Nick = "母夜叉", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_sunerniang", Element = "风系",
                Shot = ShotKind.Wind, Coef = 1.67f, Interval = 6.0f, Radius = 1.3f, Slow = 2f, Stun = 1.2f, StunChance = 0.5f, AtkName = "蒙汗药", AtkIcon = "fx_smoke", Noun = "药粉", Tint = "C690FF",
                PassiveName = "母夜叉", PassiveEff = Eff.Stun, PassiveV = 0.15f, Desc = "药粉迷倒一片" },
            new HeroDef { Id = HeroId.RuanXiaoqi, Name = "阮小七", Nick = "活阎罗", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_ruanxiaoqi",
                Shot = ShotKind.Wind, Coef = 1.3f, Interval = 6.0f, Radius = 1.3f, Stun = 1.2f, AtkName = "天罗地网", AtkIcon = "wp_net", Proj = "wp_net", ProjSpin = true, Noun = "渔网", Tint = "D8C08A",
                PassiveName = "活阎罗", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "撒网定住一片小兵" },
            new HeroDef { Id = HeroId.ShiQian, Name = "时迁", Nick = "鼓上蚤", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_shiqian",
                Shot = ShotKind.Arrow, Interval = 18f, Buff = UltKind.Steal, BuffV = 1f, AtkName = "顺手牵羊", AtkIcon = "ic_coins", Noun = "银两",
                Aura = AuraKind.Coin, AuraV = 0.1f,
                PassiveName = "鼓上蚤", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时偷来银两，在场击杀多掉银两" },
            new HeroDef { Id = HeroId.JiangJing, Name = "蒋敬", Nick = "神算子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_jiangjing",
                Shot = ShotKind.Stone, Interval = 22f, Buff = UltKind.Interest, BuffV = 0.15f, AtkName = "神机妙算", AtkIcon = "ic_coins", Noun = "利息", Tint = "FFD24A",
                Aura = AuraKind.Interest, AuraV = 0.01f,
                PassiveName = "精打细算", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时结算一大笔利息，在场银两生利息" },
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];
        public static int HeroCount => Heroes.Length;
        public static readonly int EffCount = System.Enum.GetValues(typeof(Eff)).Length;
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
            E(EnemyKind.Boss, "黄安", "en_boss", 8200, 0.17f, 200, 2.6f, 200, "济州团练使，首个来犯的官军首领", EnemyRank.Boss),
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
            E(EnemyKind.Diver, "水鬼", "en_diver", 34, 0.7f, 10, 0.95f, 3, "时不时潜进水里，潜着时刀枪不入"),
            E(EnemyKind.Farmer, "庄客", "en_farmer", 18, 0.75f, 7, 0.9f, 1, "祝家庄的庄客，扛着钉耙成群涌来"),
            E(EnemyKind.Hound, "恶犬", "en_hound", 9, 1.25f, 4, 0.75f, 1, "两条一群，跑得飞快"),
            E(EnemyKind.Rattan, "藤牌手", "en_rattan", 45, 0.48f, 14, 1.05f, 4, "藤牌能挡下最先的两次攻击"),
            E(EnemyKind.Taoist, "妖道", "en_taoist", 45, 0.5f, 8, 1.0f, 5, "站在后面作法，给身边官军回血", stopY: 1.6f),
            E(EnemyKind.Berserker, "敢死刀手", "en_berserker", 70, 0.6f, 16, 1.0f, 4, "残血时拼命：两秒内受伤减半，狂奔冲墙"),
            new EnemyDef { Kind = EnemyKind.Catapult, Name = "投石车", Art = "en_catapult", Hp = 220, Speed = 0.3f, WallDps = 30, Size = 1.4f, Coin = 8,
                Desc = "远远停下抛石砸墙，打不退", PhysResist = 0.15f, StopY = 3.6f },
            new EnemyDef { Kind = EnemyKind.Lantern, Name = "孔明灯", Art = "en_lantern", Hp = 18, Speed = 0.55f, WallDps = 200, Size = 0.9f, Coin = 3,
                Desc = "飘到墙上就炸，战士够不着，射手克制", Flying = true, ArcherWeak = 0.3f, StopY = Layout.StopY + 0.5f },
            E(EnemyKind.Scout, "斥候", "en_scout", 26, 0.9f, 9, 0.9f, 3, "挨打时常常闪身躲开，往前一窜"),
            E(EnemyKind.Brute, "力士", "en_brute", 260, 0.38f, 24, 1.35f, 6, "背篓里藏着三个官兵，打倒就跳出来"),
            E(EnemyKind.HeTao, "何涛", "en_hetao", 9000, 0.17f, 200, 2.6f, 200, "济州缉捕使臣，驾车船而来，召唤水鬼", EnemyRank.Boss),
            E(EnemyKind.ZhuChaofeng, "祝朝奉", "en_zhucf", 11000, 0.16f, 170, 2.6f, 220, "祝家庄庄主，坐轿撒钱，买来金光护体", EnemyRank.Boss),
            E(EnemyKind.ZengKui, "曾魁", "en_zengkui", 12000, 0.16f, 160, 2.6f, 240, "曾家四虎，骑猛虎扑跃突进", EnemyRank.Boss),
            new EnemyDef { Kind = EnemyKind.HanTao, Name = "韩滔", Art = "en_hantao", Hp = 14000, Speed = 0.16f, WallDps = 170, Size = 2.7f, Coin = 260,
                Desc = "百胜将，推铁头撞车猛撞寨墙，物理减伤", Rank = EnemyRank.Boss, PhysResist = 0.3f, StopY = Layout.StopY },
            E(EnemyKind.LiuMenglong, "刘梦龙", "en_liuml", 16000, 0.15f, 180, 2.7f, 300, "水军节度使，推云梯楼车，楼上弓手齐射", EnemyRank.Boss, stopY: 0.4f),
        };

        public static EnemyDef Enemy(EnemyKind k) => Enemies[(int)k];

        public static readonly PerkDef[] Perks = BuildPerks();

        /// <summary>
        /// 每位好汉 7 条：前 4 条进强化三选一（同一条可叠 Max 级），后 3 条是满星额外技能，合成满星时三选一、只给一次。
        /// 传说好汉再多一张「大招」卡，和普通强化一起随机出。
        /// </summary>
        static PerkDef[] BuildPerks()
        {
            var list = new List<PerkDef>();
            void Add(HeroId h, params (string name, Eff e, float v, int max)[] ps)
            {
                var d = Hero(h);
                string key = d.Art.Substring(5);
                for (int i = 0; i < ps.Length; i++)
                {
                    var (name, e, v, max) = ps[i];
                    bool star = i >= 4;
                    (e, v) = Adapt(d, e, v);
                    if (!star) v *= Boost(e);
                    list.Add(new PerkDef
                    {
                        Id = key + "_" + i, Hero = h, Name = name, Eff = e, V = v, Max = star ? 1 : max, Star = star,
                        Desc = EffText(h, e, v),
                    });
                }
                bool sup = d.Kind == Sim.Role.Support;
                string surge = OptNames.Surge[(int)h];
                list.Add(new PerkDef
                {
                    Id = key + "_surge", Hero = h, Name = surge, Opt = 1, Weight = 1.3f,
                    Eff = sup ? Eff.UltPow : Eff.Surge, V = sup ? 0.6f : SurgeChance,
                    Desc = sup ? $"「{d.AtkName}」\n效果 +60%" : $"{Pct(SurgeChance)}几率爆发成巨型{d.Noun ?? "攻击"}\n伤害 +{Pct(SurgeDmg - 1f)}",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_top", Hero = h, Name = "最强" + (d.Noun ?? "一击"), Opt = 2, Weight = 1.3f,
                    Eff = sup ? Eff.UltCd : Eff.Top, V = sup ? 0.3f : TopDmg,
                    Desc = sup ? $"「{d.AtkName}」\n冷却 -30%" : $"场上最高星的{d.Name}\n伤害 +{Pct(TopDmg)}，体积 +25%",
                });
                if (d.HasUlt)
                    list.Add(new PerkDef
                    {
                        Id = key + "_ult", Hero = h, Name = d.UltName, Eff = Eff.UltPow, Ult = true, Weight = 1.5f,
                        Desc = $"{d.UltTip}\n冷却{d.UltCd:0}秒",
                    });
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

        /// <summary>局内普通强化要一眼看得出变强：伤害、范围、攻速类在表里的基础值上放大。</summary>
        static float Boost(Eff e) => e == Eff.Dmg ? 1.6f : e == Eff.Area ? 1.45f : e == Eff.Speed ? 1.3f : e == Eff.Crit ? 1.25f : 1f;

        /// <summary>5 级解锁的「爆发」：每次出手有几率打出巨型一击；15 级起触发率再加、必定暴击。20 级解锁的「最强」：场上星级最高的那一位加伤害。</summary>
        public const float SurgeChance = 0.4f, SurgeDmg = 3.4f, SurgePlusChance = 0.1f, TopDmg = 1.4f;

        static class OptNames
        {
            public static readonly string[] Surge =
        {
            "穿云神箭", "轰天巨炮", "九霄神雷", "打虎神拳", "巨石天降", "蛇矛贯日", "千斤禅杖", "锦囊妙计",
            "麒麟贯阵", "青龙偃月", "劲弩连珠", "混世狂风", "天罗红锦", "妙手神方", "神行万里", "烈焰狼牙",
            "开山巨斧", "三昧真火", "八门金锁", "钩镰破阵", "圣水滔天", "神火巨罐",
            "拼命一刀", "猎虎神箭", "蝎尾巨叉", "天降金砖", "火雷连爆", "神兵天成", "十里迷香", "天罗巨网", "飞檐走壁", "利滚利",
        };
        }

        /// <summary>没有大招的好汉，大招类强化改成加伤害、攻速；辅助不打伤害，伤害类强化改成加技能效果、减冷却。</summary>
        static (Eff, float) Adapt(HeroDef d, Eff e, float v)
        {
            if (d.Kind == Sim.Role.Support)
            {
                if (e == Eff.Dmg || e == Eff.Elite) return (Eff.UltPow, System.Math.Min(v, 0.5f));
                if (e == Eff.Count) return (Eff.UltCd, 0.2f);
                return (e, v);
            }
            if (d.HasUlt) return (e, v);
            if (e == Eff.UltPow) return (Eff.Dmg, 0.8f);
            if (e == Eff.UltCd) return (Eff.Speed, 0.3f);
            return (e, v);
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
                    if (d.Chan > 0) return $"施法时间 +{v * ChanPerCount:0.#} 秒";
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
                case Eff.UltCd: return d.Kind == Sim.Role.Support ? $"技能冷却 -{Pct(v)}" : $"大招冷却 -{Pct(v)}";
                case Eff.UltPow:
                    if (d.Kind != Sim.Role.Support) return $"大招「{d.UltName}」\n伤害 +{Pct(v)}";
                    return d.HasUlt ? $"技能与大招\n效果 +{Pct(v)}" : $"「{d.AtkName}」\n效果 +{Pct(v)}";
                case Eff.Range: return $"攻击距离 +{v:0.#}";
                case Eff.Split: return $"{n}落地后\n再分裂 3 枚小{n}";
                case Eff.Coin: return $"每次合成额外\n获得 {v:0} 银两";
                case Eff.Fly: return $"对飞行敌人\n伤害 +{Pct(v)}";
                case Eff.Cav: return $"对骑兵、连环马\n伤害 +{Pct(v)}";
                case Eff.Surge: return $"{Pct(v)}几率爆发成巨型{n}\n伤害 +{Pct(SurgeDmg - 1f)}";
                case Eff.Top: return $"场上最高星的{d.Name}\n伤害 +{Pct(v)}，体积 +25%";
                default:
                    switch (d.Aura)
                    {
                        case AuraKind.Speed: return $"攻速光环 +{Pct(v)}";
                        case AuraKind.Dmg: return $"伤害光环 +{Pct(v)}";
                        case AuraKind.Charge: return $"冷却光环 +{Pct(v)}";
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
            foreach (var p in Perks) if (p.Hero == h && p.Star == star && !p.Ult) r.Add(p);
            return r;
        }

        public static PerkDef UltPerk(HeroId h)
        {
            foreach (var p in Perks) if (p.Hero == h && p.Ult) return p;
            return null;
        }

        public static PerkDef Perk(string id)
        {
            foreach (var p in Perks) if (p.Id == id) return p;
            return null;
        }

        /// <summary>同格英雄的星级倍率：1 星到 4 星（满星）。</summary>
        public static readonly float[] StarMul = { 1f, 2.2f, 4.6f, 9.5f };
        public static readonly float[] StarSpeed = { 1f, 1.08f, 1.16f, 1.3f };
        /// <summary>法师的「数量」强化：每级持续施法多这么多秒。</summary>
        public const float ChanPerCount = 0.75f;

        /// <summary>一轮几发：1 星只打一发，每升一星多一发，到 Count 封顶；少打的那几发折一部分进单发伤害。</summary>
        public static int ShotsAt(HeroDef d, int star) => System.Math.Max(1, System.Math.Min(d.Count, star));

        public const float WaveTime = 25f;

        /// <summary>寨主宋江的战斗技能：自动释放，剑气从寨墙扫到城门。</summary>
        public const string LordSkillName = "替天行道";
        public const float LordPctBase = 2f, LordCdBase = 40f, LordFirstCast = 12f, LordWaveSpeed = 16f;
        /// <summary>冷却转满时半圆里没人，最多再等这么久；再没人就空过这一轮，圈重新转，不能停在满圈上。</summary>
        public const float LordHold = 2.5f;
        public const int SummonsPerSlot = 5;
        public const int LevelsPerChapter = 10;

        static readonly int[] SummonTable = { 10, 20, 30, 45, 60, 80, 90, 110, 130, 150, 160, 175, 190, 200, 215, 235, 260, 290 };

        /// <summary>第 n+1 次召唤的价格：前 18 次查表，之后每次涨幅 +30、+35、+40……</summary>
        public static int SummonCost(int n)
        {
            if (n < SummonTable.Length) return SummonTable[n];
            int k = n - SummonTable.Length + 1;
            return SummonTable[SummonTable.Length - 1] + 30 * k + 5 * k * (k - 1) / 2;
        }

        /// <summary>强化价格：100、200、350、550、800……前几次接近参考的线性涨价，后面慢慢变贵。</summary>
        public static int EnhanceCost(int n) => 100 + 100 * n + 25 * n * (n - 1);
        public static float HpScale(int wave) => 1f + 0.13f * (wave - 1);
        /// <summary>小兵「厚度」：参考视频里小兵要挨几下才倒，兵少一些、每个更硬、打墙更疼、赏银更多。前两波再厚一点。</summary>
        public static float Tough(int wave) => Bulk * (wave <= 1 ? 1.25f : wave == 2 ? 1.12f : 1f);
        public const float Bulk = 3.2f;
        /// <summary>赏银随厚度涨，再多给两成半，让局内经济跟得上召唤和强化。</summary>
        public static float ToughCoin(float tough) => tough * 1.25f;
        /// <summary>每波小兵数：第 1 波 4 个，逐波加，第 10 波 46 个。</summary>
        static readonly int[] WaveCounts = { 4, 7, 10, 14, 18, 23, 28, 34, 40, 46 };
        public static int WaveCount(int wave) => wave <= WaveCounts.Length ? WaveCounts[System.Math.Max(1, wave) - 1] : WaveCounts[WaveCounts.Length - 1] + 6 * (wave - WaveCounts.Length);

        // ---------------- 关卡 ----------------

        public static readonly TroopMix[] Mix1 =
        {
            new TroopMix(EnemyKind.Hound, 0.02f, 0.002f, 2),
            new TroopMix(EnemyKind.Cavalry, 0.10f, 0.012f, 2),
            new TroopMix(EnemyKind.Diver, 0.15f, 0.016f, 3),
            new TroopMix(EnemyKind.Shield, 0.21f, 0.022f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.27f, 0.028f, 4),
        };

        static readonly TroopMix[] Mix2 =
        {
            new TroopMix(EnemyKind.Hawk, 0.08f, 0.010f, 1),
            new TroopMix(EnemyKind.Hound, 0.10f, 0.0105f, 2),
            new TroopMix(EnemyKind.Cavalry, 0.17f, 0.014f, 2),
            new TroopMix(EnemyKind.Rattan, 0.21f, 0.017f, 3),
            new TroopMix(EnemyKind.Shield, 0.28f, 0.020f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.34f, 0.024f, 4),
            new TroopMix(EnemyKind.Farmer, 0.42f, 0.024f, 1),
        };

        static readonly TroopMix[] Mix3 =
        {
            new TroopMix(EnemyKind.Armored, 0.06f, 0.009f, 2),
            new TroopMix(EnemyKind.Taoist, 0.09f, 0.005f, 3),
            new TroopMix(EnemyKind.Hawk, 0.14f, 0.010f, 1),
            new TroopMix(EnemyKind.Scout, 0.19f, 0.012f, 1),
            new TroopMix(EnemyKind.Cavalry, 0.23f, 0.013f, 2),
            new TroopMix(EnemyKind.Berserker, 0.28f, 0.015f, 3),
            new TroopMix(EnemyKind.Shield, 0.32f, 0.017f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.37f, 0.021f, 4),
        };

        static readonly TroopMix[] Mix4 =
        {
            new TroopMix(EnemyKind.FireBomb, 0.04f, 0.007f, 2),
            new TroopMix(EnemyKind.Catapult, 0.06f, 0.004f, 3),
            new TroopMix(EnemyKind.Cavalry, 0.14f, 0.016f, 1),
            new TroopMix(EnemyKind.Brute, 0.17f, 0.006f, 3),
            new TroopMix(EnemyKind.Armored, 0.22f, 0.016f, 3),
            new TroopMix(EnemyKind.Berserker, 0.26f, 0.016f, 2),
            new TroopMix(EnemyKind.Hawk, 0.31f, 0.018f, 2),
            new TroopMix(EnemyKind.Shield, 0.36f, 0.019f, 4),
            new TroopMix(EnemyKind.Crossbow, 0.42f, 0.022f, 5),
        };

        static readonly TroopMix[] Mix5 =
        {
            new TroopMix(EnemyKind.FireBomb, 0.03f, 0.005f, 2),
            new TroopMix(EnemyKind.Lantern, 0.05f, 0.005f, 2),
            new TroopMix(EnemyKind.Catapult, 0.07f, 0.004f, 3),
            new TroopMix(EnemyKind.Armored, 0.11f, 0.010f, 2),
            new TroopMix(EnemyKind.Brute, 0.13f, 0.004f, 3),
            new TroopMix(EnemyKind.Taoist, 0.15f, 0.004f, 3),
            new TroopMix(EnemyKind.Hawk, 0.19f, 0.010f, 1),
            new TroopMix(EnemyKind.Scout, 0.22f, 0.010f, 1),
            new TroopMix(EnemyKind.Diver, 0.25f, 0.010f, 2),
            new TroopMix(EnemyKind.Cavalry, 0.29f, 0.012f, 2),
            new TroopMix(EnemyKind.Shield, 0.34f, 0.016f, 3),
            new TroopMix(EnemyKind.Crossbow, 0.39f, 0.022f, 4),
        };

        /// <summary>每关都会来的首领，按章节顺序：每章先章中首领，再章末首领。</summary>
        public static readonly EnemyKind[] BossOrder =
        {
            EnemyKind.HeTao, EnemyKind.Boss, EnemyKind.ZhuChaofeng, EnemyKind.Luan, EnemyKind.ZengKui,
            EnemyKind.ShiWen, EnemyKind.HanTao, EnemyKind.HuYan, EnemyKind.LiuMenglong, EnemyKind.GaoQiu,
        };

        public sealed class ChapterDef
        {
            public string Name, Bg, Intro;
            /// <summary>Boss 守第 10 关，MidBoss 守第 5 关，第 6~9 关也由他压阵。</summary>
            public EnemyKind Boss, MidBoss;
            public TroopMix[] Mix;
            public EnemyKind[] Elites;
            public string[] Levels;
        }

        public static readonly ChapterDef[] Chapters =
        {
            new ChapterDef { Name = "金沙滩", Bg = "battle_bg", Boss = EnemyKind.Boss, MidBoss = EnemyKind.HeTao, Mix = Mix1, Elites = new[] { EnemyKind.Elite },
                Intro = "济州官军来犯水泊",
                Levels = new[] { "金沙滩", "鸭嘴滩", "李家道口", "朱贵酒店", "黄安水寨", "蓼儿洼", "断金亭", "石碣村", "宛子城", "梁山泊" } },
            new ChapterDef { Name = "三打祝家庄", Bg = "battle_bg_ch2", Boss = EnemyKind.Luan, MidBoss = EnemyKind.ZhuChaofeng, Mix = Mix2, Elites = new[] { EnemyKind.Elite },
                Intro = "独龙冈上祝家庄，鹰隼盘旋",
                Levels = new[] { "独龙冈", "盘陀路", "白杨林", "扈家庄", "祝家店", "李家庄", "时迁偷鸡", "吊桥", "祝龙寨", "祝家庄" } },
            new ChapterDef { Name = "夜打曾头市", Bg = "battle_bg_ch3", Boss = EnemyKind.ShiWen, MidBoss = EnemyKind.ZengKui, Mix = Mix3, Elites = new[] { EnemyKind.Elite },
                Intro = "曾家五虎铁甲列阵",
                Levels = new[] { "青州道", "照夜玉狮", "法华寺", "曾家五虎", "曾头北寨", "凌州", "东平府", "东昌府", "曾家大营", "曾头市" } },
            new ChapterDef { Name = "大破连环马", Bg = "battle_bg_ch4", Boss = EnemyKind.HuYan, MidBoss = EnemyKind.HanTao, Mix = Mix4, Elites = new[] { EnemyKind.ChainHorse, EnemyKind.Elite },
                Intro = "风雪中铁骑连环而来",
                Levels = new[] { "汝宁州", "高唐州", "雪夜", "钩镰枪", "韩滔营", "彭玘营", "金枪班", "徐宁", "连环阵", "呼延大营" } },
            new ChapterDef { Name = "三败高太尉", Bg = "battle_bg_ch5", Boss = EnemyKind.GaoQiu, MidBoss = EnemyKind.LiuMenglong, Mix = Mix5, Elites = new[] { EnemyKind.ChainHorse, EnemyKind.Elite },
                Intro = "十节度兵马夜袭水泊",
                Levels = new[] { "济州城", "十节度", "王焕", "荆忠", "党世英", "水泊芦荡", "刘梦龙", "牛邦喜", "梁山水寨", "擒高俅" } },
        };

        /// <summary>主线每关只来一个首领（第 4 波压上来，慢慢逼近，第 7、8 波前后才倒）和第 9 波一个押纲精英，生辰纲一共掉两次。</summary>
        static readonly int[] MainEliteWaves = { 9 };
        public const float BossPace = 0.4f;
        static readonly Role[] BuffCycle = { Sim.Role.Mage, Sim.Role.Archer, Sim.Role.Fighter, Sim.Role.Bomber, Sim.Role.Control, Sim.Role.Archer, Sim.Role.Mage, Sim.Role.Fighter };

        /// <summary>第 n 关（1 起）的敌人血量倍率：跟着正常推进时的养成战力走，再逐关加压，落后养成就过不去。大招要局内抽卡才有，后期血量压得更低。</summary>
        public static float StageHp(int n) => HpTune(n) * Meta.ExpectedPower(n) * Meta.ExpectedRoster(n) * Pressure(n) * LordPocket(n) * (1.06f - 0.0085f * (n - 1) - 0.002f * System.Math.Max(0, n - 40)) * (1f + 0.35f * System.Math.Min(1f, (n - 1) / 12f) + 0.009f * (n - 1) + 0.00015f * (n - 1) * (n - 1)) * (1f + 0.005f * System.Math.Max(0, n - 30));
        /// <summary>前面的关卡别一碰就倒：低关小兵也得挨两三下，越往后养成越吃紧、这层加成慢慢收掉。</summary>
        static float HpTune(int n) => Curve(n, new[] { 1f, 10f, 25f, 40f }, new[] { 2.3f, 1.4f, 1.25f, 1f });
        static float Curve(int n, float[] xs, float[] ys)
        {
            if (n <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
                if (n <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (n - xs[i - 1]) / (xs[i] - xs[i - 1]);
            return ys[ys.Length - 1];
        }
        /// <summary>替天行道只打城墙前的黄半圆，不再横扫全场。低关少一截血，高关少收回一些，落后养成仍过不去。</summary>
        static float LordPocket(int n)
        {
            float[] xs = { 1f, 10f, 25f, 40f, 50f };
            float[] ys = { 0.32f, 0.50f, 0.60f, 0.72f, 0.92f };
            if (n <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
            {
                if (n <= xs[i])
                {
                    float t = (n - xs[i - 1]) / (xs[i] - xs[i - 1]);
                    return ys[i - 1] + (ys[i] - ys[i - 1]) * t;
                }
            }
            return ys[ys.Length - 1];
        }
        /// <summary>召唤便宜、寨主技能补的那部分输出：第 1 关不加，前 7 关逐步压上来。</summary>
        static float Pressure(int n) => 1f + 0.35f * System.Math.Min(1f, (n - 1) / 6f);

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
                    var boss = i == 10 ? ch.Boss : i >= 5 || c == 0 ? ch.MidBoss : Chapters[c - 1].Boss;
                    var d = new LevelDef
                    {
                        No = n, Chapter = c + 1, Index = i, Name = ch.Levels[i - 1], Bg = ch.Bg,
                        Intro = chBoss ? $"首领 {Enemy(boss).Name} 亲至，守住寨墙" : n == 1 ? "击退十波官军，寨墙不能倒" : ch.Intro,
                        EliteWaves = MainEliteWaves,
                        EliteKinds = ch.Elites, Troops = ch.Mix,
                        BossKind = boss,
                        BossMul = i == 10 ? (c >= 4 ? 0.78f : 1.0f) : i == 5 ? 0.8f : c == 0 ? 1f : 0.7f,
                        HpMul = StageHp(n), AtkMul = StageAtk(n),
                        CountMul = 1f + 0.008f * (n - 1) + (i == 10 ? 0.08f : 0f),
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
                EliteWaves = MainEliteWaves, EliteKinds = b.EliteKinds, Troops = b.Troops,
                BossKind = BossOrder[(day % BossOrder.Length + BossOrder.Length) % BossOrder.Length], BossMul = 1.2f,
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
            int total = (int)(WaveCount(wave) * st.CountMul);
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

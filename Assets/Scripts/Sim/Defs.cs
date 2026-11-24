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
    public enum Eff { Dmg, Speed, Count, Area, Chain, Crit, Stun, Slow, Elite, UltCd, UltPow, Range, Split, Aura, Coin, Fly, Cav, Surge, Top, Blast, Burn, Vuln, Reach, Shatter, Exec, Resonance, Conduct, Refract }

    /// <summary>强化卡的类型：数值（纯加成）、取舍（有代价，同组互斥）、质变（改变攻击方式）、进阶（要先有对应质变）。</summary>
    public enum PerkKind { Value, Trade, Morph, Adv }

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
        /// <summary>穿透递增伤害（如花荣每穿 1 怪增伤 18%）、天生持续灼烧/毒素倍率、是否附带 60% 减疗。</summary>
        public float PierceGrow, Dot;
        public bool AntiHeal;
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
        /// <summary>每秒对单体的期望输出（× 攻击力），战力估算与数值表核验用（持续施法计入完整施法周期 Interval + Chan）。</summary>
        public float Dps => Buff.HasValue ? 0 : Coef * Count * (Chan > 0 ? Chan / Tick : 1f) / (Interval + Chan);
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
        /// <summary>本关主统领（逢5/10关为守关大首领，普通关为第4波领军精英统领，用于城门前展示与敌情）。</summary>
        public EnemyKind LeaderKind => BossWave > 0 ? BossKind : (EliteKinds != null && EliteKinds.Length > 0 ? EliteKinds[0] : EnemyKind.Elite);
        public string BuffText => BuffRole.HasValue ? $"{Defs.RoleName(BuffRole.Value)}伤害 +{(int)(BuffPct * 100 + 0.5f)}%" : "";
    }

    public enum EnemyKind
    {
        Soldier, Shield, Cavalry, Crossbow, Elite, Porter, Boss,
        Hawk, Armored, FireBomb, ChainHorse, Luan, ShiWen, HuYan, GaoQiu,
        Diver, Farmer, Hound, Rattan, Taoist, Berserker, Catapult, Lantern, Scout, Brute,
        HeTao, ZhuChaofeng, ZengKui, HanTao, LiuMenglong,
        WaterChief, CrossbowCapt, GoldenEagle, IronPagoda, HighPriest, GiantBrute, FireCart,
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

    /// <summary>好汉专属强化。Star 为满星额外技能，只在合成满星时三选一；Ult 是传说「大招」卡；Finisher 是非传说「奥义」卡。</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Desc;
        public HeroId Hero;
        public int Max = 1;
        public float Weight = 1;
        public Eff Eff;
        public float V;
        public bool Star, Ult, Finisher;
        /// <summary>好汉等级解锁的选项：1 = 5 级解锁选项，2 = 20 级解锁选项，3 = 15 级解锁专属选项；没到等级不进卡池。</summary>
        public int Opt;
        public PerkKind Kind;
        /// <summary>取舍卡的代价（V2 为负）。</summary>
        public bool Has2;
        public Eff Eff2;
        public float V2;
        /// <summary>进阶卡的前置强化；同一好汉 Group 相同的卡只能拿一张。</summary>
        public string Req, Group;
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

        /// <summary>剑气从寨墙中点出手，刃口一路飞到半圆边上。</summary>
        public static readonly V2 LordOrigin = new V2(0f, WallRowY);

        /// <summary>刃口从寨主扫到 p 的秒数。伤害和画面都按这个时刻落下。</summary>
        public static float LordHitDelay(V2 p) => (p - LordOrigin).Len / Defs.LordWaveSpeed;

        /// <summary>剑气整段飞行时间：刃口到半圆边。</summary>
        public static float LordFlightTime => HalfW / Defs.LordWaveSpeed;

        public const float SlotGap = 1.36f;
        public const int BaseSlots = 7;
        public const int MaxSlots = 12;
        public const float RoadHalfW = 2.85f;
        public static readonly float[] GateX = { -3.05f, 3.05f };

        /// <summary>按纵坐标收束石板路半宽：刚出城门时对齐门洞，踏上大路后严格留在两侧路肩以内。</summary>
        public static float RoadMaxX(float y, float size = 1f)
        {
            float road = System.Math.Max(0.8f, RoadHalfW - size * 0.22f);
            if (y <= SpawnY - 1.35f) return road;
            float gate = GateX[1] + 0.16f;
            float t = M.Clamp01((y - (SpawnY - 1.35f)) / 0.85f);
            return road + (gate - road) * t;
        }

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
                Shot = ShotKind.Arrow, Coef = 1.42f, Interval = 2.0f, Chain = 1, PierceGrow = 0.18f, AtkName = "穿云箭", AtkIcon = "wp_arrow_cloud", Proj = "wp_arrow_cloud", ProjSize = 1.12f, Tint = "58F2C8", Noun = "箭",
                PassiveName = "百步穿杨", PassiveEff = Eff.Crit, PassiveV = 0.18f, Desc = "一箭穿云，每穿透一敌伤害递增" },
            new HeroDef { Id = HeroId.LingZhen, Name = "凌振", Nick = "轰天雷", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_lingzhen", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 2.85f, Interval = 4.6f, Radius = 1.25f, Knock = 0.32f, AtkName = "霹雳重炮", AtkIcon = "wp_cannon_shell", Proj = "wp_cannon_shell", ProjSize = 1.08f, Tint = "FF7828", Noun = "重炮",
                PassiveName = "火药精通", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "重型轰天巨炮，大口径震退敌潮" },
            new HeroDef { Id = HeroId.GongSun, Name = "公孙胜", Nick = "入云龙", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_gongsun", Element = "雷系",
                Shot = ShotKind.Lightning, Coef = 2.60f, Interval = 8.5f, Radius = 0.8f, Chain = 1, AtkName = "天罡雷链", AtkIcon = "fx_zap", Tint = "9AB8FF", Noun = "落雷",
                Ult = UltKind.Thunder, UltName = "五雷天罡", UltIcon = "fx_lightning", UltCoef = 2.8f, UltCd = 30, UltN = 8, UltStun = 1f, UltRadius = 1.15f,
                UltTip = "8道天雷劈向敌群\n每道麻痹1秒",
                PassiveName = "入云龙", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "引一道天雷劈敌并跃迁传导，随强化进化连环雷网" },
            new HeroDef { Id = HeroId.WuSong, Name = "武松", Nick = "行者", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_wusong",
                Shot = ShotKind.Punch, Coef = 2.95f, Interval = 2.7f, Range = 4.8f, Radius = 0.92f, Knock = 0.65f, AtkName = "打虎铁拳", AtkIcon = "wp_ironfist", Proj = "wp_ironfist", Tint = "FFB438", Noun = "铁拳",
                PassiveName = "景阳冈打虎", PassiveEff = Eff.Elite, PassiveV = 0.6f, Desc = "势大力沉一记重拳，震退贴墙敌军" },
            new HeroDef { Id = HeroId.ZhangQing, Name = "张清", Nick = "没羽箭", Kind = Sim.Role.Archer, Rarity = 3, Art = "hero_zhangqing",
                Shot = ShotKind.Stone, Coef = 1.15f, Interval = 2.0f, Radius = 0.32f, Chain = 2, AtkName = "飞石", AtkIcon = "wp_jade_stone", Proj = "wp_jade_stone", ProjSize = 1.04f, Tint = "5CE8FF", Noun = "飞石",
                PassiveName = "百发百中", PassiveEff = Eff.Chain, PassiveV = 2, Desc = "飞石在敌人之间弹射" },
            new HeroDef { Id = HeroId.LinChong, Name = "林冲", Nick = "豹子头", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_linchong",
                Shot = ShotKind.Spear, Coef = 3.40f, Interval = 3.2f, Range = 6.2f, Radius = 0.55f, Knock = 0.30f, AtkName = "丈八蛇矛", AtkIcon = "wp_snakespear", Proj = "wp_snakespear", ProjSize = 1.32f, Tint = "54E2FF", Noun = "长枪",
                Ult = UltKind.Frost, UltName = "风雪山神庙", UltIcon = "fx_ice", UltCoef = 13.5f, UltCd = 30, UltRadius = 1.9f, UltStun = 2.5f,
                UltTip = "风雪冰封一片敌人\n冻住2.5秒",
                PassiveName = "禁军教头", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "长枪贯穿一列，攻击距离远" },
            new HeroDef { Id = HeroId.LuZhiShen, Name = "鲁智深", Nick = "花和尚", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_luzhishen",
                Shot = ShotKind.Slam, Coef = 3.80f, Interval = 3.5f, Range = 5.2f, Radius = 1.2f, Knock = 0.55f, AtkName = "水磨禅杖", AtkIcon = "wp_staff", Proj = "wp_staff", ProjSize = 1.18f, Tint = "FFB038", Noun = "禅杖",
                PassiveName = "大闹五台山", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "浑铁禅杖腾空砸地，大范围震退" },
            new HeroDef { Id = HeroId.WuYong, Name = "吴用", Nick = "智多星", Kind = Sim.Role.Support, Rarity = 3, Art = "hero_wuyong", Element = "风系",
                Shot = ShotKind.Wind, Interval = 28f, Buff = UltKind.Bless, BuffV = 1f, AtkName = "运筹帷幄", AtkIcon = "prop_drum", Noun = "锦囊",
                Aura = AuraKind.Speed, AuraV = 0.04f,
                PassiveName = "锦囊妙计", PassiveEff = Eff.Coin, PassiveV = 15, Desc = "祝福输出好汉加攻速，在场提升全军攻速" },

            // ---------- 传说 ----------
            new HeroDef { Id = HeroId.LuJunyi, Name = "卢俊义", Nick = "玉麒麟", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_lujunyi",
                Shot = ShotKind.Spear, Coef = 3.70f, Interval = 3.2f, Range = 6.0f, Radius = 0.8f, Knock = 0.32f, AtkName = "麒麟枪", AtkIcon = "wp_spear", Proj = "wp_spear", ProjSize = 1.34f, Tint = "FFC83A", Noun = "长枪",
                Ult = UltKind.Sweep, UltName = "麒麟踏阵", UltIcon = "fx_wave", UltCoef = 10.0f, UltCd = 30, UltN = 5,
                UltTip = "枪扫墙前一整排\n五段击飞",
                PassiveName = "麒麟镇岳", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "宽幅枪扫，一次挑翻一列" },
            new HeroDef { Id = HeroId.GuanSheng, Name = "关胜", Nick = "大刀", Kind = Sim.Role.Fighter, Rarity = 4, Art = "hero_guansheng",
                Shot = ShotKind.Slam, Coef = 4.35f, Interval = 3.5f, Range = 5.4f, Radius = 1.3f, Knock = 0.55f, AtkName = "青龙偃月刀", AtkIcon = "wp_broadsword", Proj = "wp_broadsword", ProjSize = 1.28f, Tint = "38F0A8", Noun = "大刀",
                Ult = UltKind.Sweep, UltName = "拖刀计", UltIcon = "fx_shock", UltCoef = 11.5f, UltCd = 30, UltN = 5,
                UltTip = "大刀横扫墙前\n五段重劈",
                PassiveName = "武圣遗风", PassiveEff = Eff.Elite, PassiveV = 0.5f, Desc = "偃月重刀腾空劈斩，范围大伤害高" },
            new HeroDef { Id = HeroId.YanQing, Name = "燕青", Nick = "浪子", Kind = Sim.Role.Archer, Rarity = 4, Art = "hero_yanqing",
                Shot = ShotKind.Arrow, Coef = 0.58f, Interval = 1.9f, Count = 3, AtkName = "连弩", AtkIcon = "wp_bolt_yanqing", Proj = "wp_bolt_yanqing", ProjSize = 1.04f, Tint = "FFD448", Noun = "弩箭",
                Ult = UltKind.Snipe, UltName = "一箭封喉", UltIcon = "fx_goldarrow", UltCoef = 11f, UltCd = 30,
                UltTip = "瞄准场上最强敌人\n一箭重创",
                PassiveName = "浪子连弩", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "唯一三连轻弩射手，场上满四位再添一矢" },
            new HeroDef { Id = HeroId.FanRui, Name = "樊瑞", Nick = "混世魔王", Kind = Sim.Role.Mage, Rarity = 4, Art = "hero_fanrui", Element = "风系",
                Shot = ShotKind.Wind, Coef = 0.88f, Interval = 12.0f, Chan = 3f, Tick = 0.25f, Radius = 1.1f, Slow = 1.5f, AtkName = "旋风咒", AtkIcon = "fx_smoke", Noun = "旋风", Tint = "5CE8B4",
                Ult = UltKind.Tornado, UltName = "飞沙走石", UltIcon = "fx_rock1", UltCoef = 9.0f, UltCd = 30, UltRadius = 2.2f, UltStun = 1f,
                UltTip = "龙卷风卷起敌群\n眩晕1秒",
                PassiveName = "混世魔王", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "旋风缠住目标持续绞杀，范围减速" },
            new HeroDef { Id = HeroId.HuSanniang, Name = "扈三娘", Nick = "一丈青", Kind = Sim.Role.Control, Rarity = 4, Art = "hero_husanniang",
                Shot = ShotKind.Lasso, Coef = 2.90f, Interval = 5.2f, Stun = 1.6f, AtkName = "红锦缚龙索", AtkIcon = "wp_lasso", Proj = "wp_lasso", ProjSpin = true, Noun = "套索", Tint = "F03C38",
                Ult = UltKind.Bind, UltName = "阵前擒将", UltIcon = "ic_hand", UltCoef = 6.6f, UltCd = 30, UltN = 5, UltStun = 2.5f,
                UltTip = "同时套住5个敌人\n优先精英首领",
                PassiveName = "日月双刀", PassiveEff = Eff.Stun, PassiveV = 0.4f, Desc = "红锦长索精准锁敌，连精英首领也能缚住" },
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
                Shot = ShotKind.Fireball, Coef = 3.40f, Interval = 4.5f, Radius = 1.3f, Dot = 0.45f, AtkName = "霹雳火陨", AtkIcon = "wp_flame_mace", Proj = "wp_flame_mace", ProjSize = 1.14f, Tint = "FF5E1E", Noun = "火陨",
                Ult = UltKind.Meteor, UltName = "霹雳天火", UltIcon = "fx_explode", UltCoef = 3.8f, UltCd = 30, UltN = 10, UltRadius = 1.2f,
                UltTip = "天降10颗火流星\n砸进敌群",
                PassiveName = "统制神威", PassiveEff = Eff.Area, PassiveV = 0.25f, Desc = "天降重型霹雳火陨，大招十颗天火洗地" },

            // ---------- 史诗 ----------
            new HeroDef { Id = HeroId.LiKui, Name = "李逵", Nick = "黑旋风", Kind = Sim.Role.Fighter, Rarity = 3, Art = "hero_likui",
                Shot = ShotKind.Punch, Coef = 2.95f, Interval = 2.9f, Range = 4.8f, Radius = 1.05f, Knock = 0.52f, AtkName = "旋风飞斧", AtkIcon = "wp_axe_spin", Proj = "wp_axe", ProjSpin = true, Tint = "38C8F0", Noun = "板斧",
                PassiveName = "黑旋风", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "甩出沉重回旋板斧，击退近墙官军" },
            new HeroDef { Id = HeroId.QiaoDaoqing, Name = "乔道清", Nick = "幻魔君", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_qiaodaoqing", Element = "火系",
                Shot = ShotKind.Beam, Coef = 0.58f, Interval = 11.5f, Chan = 3f, Tick = 0.2f, Radius = 0.45f, AtkName = "火焰射线", AtkIcon = "fx_fireball", Noun = "射线", Tint = "FF8A2A",
                PassiveName = "幻术", PassiveEff = Eff.Crit, PassiveV = 0.15f, Desc = "喷出火焰射线持续灼烧，法伤克制重甲" },
            new HeroDef { Id = HeroId.ZhuWu, Name = "朱武", Nick = "神机军师", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_zhuwu", Element = "风系",
                Shot = ShotKind.Wind, Coef = 1.9f, Interval = 6.0f, Radius = 1.6f, Slow = 3f, AtkName = "八阵图", AtkIcon = "wp_array_flag", Proj = "wp_array_flag", ProjSize = 1.06f, Noun = "阵法", Tint = "6CAEFF",
                PassiveName = "八门金锁", PassiveEff = Eff.Slow, PassiveV = 1f, Desc = "布阵困住一大片敌人，长时间减速" },
            new HeroDef { Id = HeroId.XuNing, Name = "徐宁", Nick = "金枪手", Kind = Sim.Role.Control, Rarity = 3, Art = "hero_xuning",
                Shot = ShotKind.Spear, Coef = 2.95f, Interval = 4.6f, Range = 6.0f, Radius = 0.6f, Slow = 2f, AtkName = "钩镰枪", AtkIcon = "wp_hookspear", Proj = "wp_hookspear", ProjSize = 1.20f, Noun = "钩镰", Tint = "FFD268",
                PassiveName = "金枪班", PassiveEff = Eff.Cav, PassiveV = 0.5f, Desc = "钩镰绊马，克制骑兵与连环马" },
            new HeroDef { Id = HeroId.ShanTinggui, Name = "单廷珪", Nick = "圣水将", Kind = Sim.Role.Mage, Rarity = 3, Art = "hero_shantinggui", Element = "水系",
                Shot = ShotKind.Beam, Coef = 0.54f, Interval = 11.5f, Chan = 3f, Tick = 0.2f, Radius = 0.5f, Slow = 1.5f, AtkName = "圣水射线", AtkIcon = "fx_icecrystal", Noun = "射线", Tint = "6FC8FF",
                PassiveName = "圣水灌顶", PassiveEff = Eff.Dmg, PassiveV = 0.2f, Desc = "水柱持续冲刷一条线，沿途减速" },
            new HeroDef { Id = HeroId.WeiDingguo, Name = "魏定国", Nick = "神火将", Kind = Sim.Role.Bomber, Rarity = 3, Art = "hero_weidingguo", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 1.85f, Interval = 4.5f, Radius = 1.15f, Dot = 0.70f, AntiHeal = true, AtkName = "猛火油罐", AtkIcon = "wp_oil_jar", Proj = "wp_oil_jar", ProjSize = 1.08f, Noun = "火罐", Tint = "FF6422",
                PassiveName = "神火焚营", PassiveEff = Eff.Burn, PassiveV = 0.45f, Desc = "投出猛火油罐铺开火海，持续灼烧并减疗" },

            // ---------- 稀有 ----------
            new HeroDef { Id = HeroId.ShiXiu, Name = "石秀", Nick = "拼命三郎", Kind = Sim.Role.Fighter, Rarity = 2, Art = "hero_shixiu",
                Shot = ShotKind.Punch, Coef = 1.85f, Interval = 2.2f, Range = 4.8f, Radius = 0.72f, Knock = 0.34f, AtkName = "拼命快刀", AtkIcon = "wp_fast_sabre", Proj = "wp_fast_sabre", ProjSize = 1.08f, Tint = "D8EEFF", Noun = "利刃",
                PassiveName = "拼命三郎", PassiveEff = Eff.Crit, PassiveV = 0.2f, Desc = "寒钢快刀凌厉一斩，暴击极高" },
            new HeroDef { Id = HeroId.XieZhen, Name = "解珍", Nick = "两头蛇", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiezhen",
                Shot = ShotKind.Arrow, Coef = 1.18f, Interval = 1.9f, AtkName = "穿林猎弓", AtkIcon = "wp_arrow_hunt", Proj = "wp_arrow_hunt", ProjSize = 0.96f, Tint = "DCE8F2", Noun = "箭",
                PassiveName = "登州猎户", PassiveEff = Eff.Fly, PassiveV = 0.5f, Desc = "精准穿林单箭点杀，专克飞禽" },
            new HeroDef { Id = HeroId.XieBao, Name = "解宝", Nick = "双尾蝎", Kind = Sim.Role.Archer, Rarity = 2, Art = "hero_xiebao",
                Shot = ShotKind.Arrow, Coef = 1.15f, Interval = 2.1f, Chain = 1, Dot = 0.35f, AtkName = "倒钩毒叉", AtkIcon = "wp_fork", Proj = "wp_fork", ProjSize = 1.00f, Tint = "5AE868", Noun = "钢叉",
                PassiveName = "双尾剧毒", PassiveEff = Eff.Chain, PassiveV = 1, Desc = "毒叉穿透敌军并附带持续毒伤" },
            new HeroDef { Id = HeroId.MaLing, Name = "马灵", Nick = "神驹子", Kind = Sim.Role.Mage, Rarity = 2, Art = "hero_maling", Element = "金系",
                Shot = ShotKind.Stone, Coef = 2.00f, Interval = 3.8f, Radius = 0.45f, Stun = 0.8f, StunChance = 0.35f, AtkName = "金砖镇魔", AtkIcon = "wp_goldbrick", Proj = "wp_goldbrick", ProjSpin = true, Noun = "金砖", Tint = "FFD24A",
                PassiveName = "风火金印", PassiveEff = Eff.Speed, PassiveV = 0.2f, Desc = "祭出一块镇魔金砖从天砸落，高几率击晕" },
            new HeroDef { Id = HeroId.KongLiang, Name = "孔亮", Nick = "独火星", Kind = Sim.Role.Bomber, Rarity = 2, Art = "hero_kongliang", Element = "火系",
                Shot = ShotKind.Bomb, Coef = 1.82f, Interval = 3.7f, Radius = 0.95f, AtkName = "独火雷", AtkIcon = "ic_bomb", Noun = "火雷",
                PassiveName = "星火燎原", PassiveEff = Eff.Area, PassiveV = 0.22f, Desc = "掷出一枚高爆独火雷，炸翻扎堆步卒" },
            new HeroDef { Id = HeroId.TangLong, Name = "汤隆", Nick = "金钱豹子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_tanglong",
                Shot = ShotKind.Stone, Interval = 24f, Buff = UltKind.Forge, BuffV = 0.4f, AtkName = "打造神兵", AtkIcon = "ic_gear", Noun = "神兵", Tint = "A8AEB8",
                Aura = AuraKind.Dmg, AuraV = 0.03f,
                PassiveName = "铁匠世家", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时打造神兵加伤害，在场提升全军伤害" },
            new HeroDef { Id = HeroId.SunErniang, Name = "孙二娘", Nick = "母夜叉", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_sunerniang", Element = "风系",
                Shot = ShotKind.Wind, Coef = 1.67f, Interval = 6.0f, Radius = 1.3f, Slow = 2f, Stun = 1.2f, StunChance = 0.5f, AtkName = "蒙汗药", AtkIcon = "wp_drug_pouch", Proj = "wp_drug_pouch", ProjSpin = true, ProjSize = 0.88f, Noun = "药粉", Tint = "D27EFF",
                PassiveName = "十字坡", PassiveEff = Eff.Stun, PassiveV = 0.15f, Desc = "药粉迷倒一片" },
            new HeroDef { Id = HeroId.RuanXiaoqi, Name = "阮小七", Nick = "活阎罗", Kind = Sim.Role.Control, Rarity = 2, Art = "hero_ruanxiaoqi",
                Shot = ShotKind.Wind, Coef = 1.3f, Interval = 6.0f, Radius = 1.3f, Stun = 1.2f, AtkName = "天罗地网", AtkIcon = "wp_net", Proj = "wp_net", ProjSpin = true, Noun = "渔网", Tint = "D8C08A",
                PassiveName = "石碣水寨", PassiveEff = Eff.Area, PassiveV = 0.3f, Desc = "撒网定住一片小兵" },
            new HeroDef { Id = HeroId.ShiQian, Name = "时迁", Nick = "鼓上蚤", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_shiqian",
                Shot = ShotKind.Arrow, Interval = 15f, Buff = UltKind.Steal, BuffV = 1f, AtkName = "顺手牵羊", AtkIcon = "ic_coins", Noun = "银两",
                Aura = AuraKind.Coin, AuraV = 0.1f,
                PassiveName = "飞檐走壁", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "定时偷银并破绽强敌，在场击杀多掉银两" },
            new HeroDef { Id = HeroId.JiangJing, Name = "蒋敬", Nick = "神算子", Kind = Sim.Role.Support, Rarity = 2, Art = "hero_jiangjing",
                Shot = ShotKind.Stone, Interval = 18f, Buff = UltKind.Interest, BuffV = 0.18f, AtkName = "神机妙算", AtkIcon = "ic_coins", Noun = "利息", Tint = "FFD24A",
                Aura = AuraKind.Interest, AuraV = 0.01f,
                PassiveName = "精打细算", PassiveEff = Eff.Aura, PassiveV = 0.5f, Desc = "结算利息并鼓舞攻速，在场银两生利息" },
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
            E(EnemyKind.Soldier, "官兵", "en_soldier", 72, 0.424f, 12, 0.95f, 16, "最常见的官军步卒，需多番射击方能击退"),
            new EnemyDef { Kind = EnemyKind.Shield, Name = "盾兵", Art = "en_shield", Hp = 164, Speed = 0.292f, WallDps = 18, Size = 1.15f, Coin = 32,
                Desc = "举着大盾，减免物理伤害，怕法术轰击", PhysResist = 0.25f, MagicWeak = 0.20f, StopY = Layout.StopY },
            E(EnemyKind.Cavalry, "骑兵", "en_cavalry", 64, 0.672f, 11, 1.15f, 24, "跑得最快，冲到墙下才停"),
            E(EnemyKind.Crossbow, "弩手", "en_crossbow", 56, 0.409f, 11, 0.95f, 22, "停在半路放箭，近战够不着", stopY: 0.6f),
            E(EnemyKind.Elite, "教头", "en_elite", 1020, 0.310f, 70, 1.6f, 200, "官军教头，血厚攻高", EnemyRank.Elite),
            E(EnemyKind.Porter, "生辰纲", "en_porter", 480, 0.365f, 0, 1.25f, 120, "挑着财宝的脚夫，只在劫生辰纲时出现"),
            E(EnemyKind.Boss, "黄安", "en_boss", 5800, 0.295f, 185, 2.6f, 320, "济州团练使，首个来犯的官军首领", EnemyRank.Boss),
            new EnemyDef { Kind = EnemyKind.Hawk, Name = "鹰隼", Art = "en_hawk", Hp = 46, Speed = 0.585f, WallDps = 11, Size = 0.9f, Coin = 20,
                Desc = "飞行单位，战士打不到，射手克制", Flying = true, ArcherWeak = 0.45f, StopY = Layout.StopY + 0.5f },
            new EnemyDef { Kind = EnemyKind.Armored, Name = "铁甲兵", Art = "en_armored", Hp = 180, Speed = 0.292f, WallDps = 20, Size = 1.15f, Coin = 40,
                Desc = "重甲：大幅减免物理伤害，怕火雷风法术", PhysResist = 0.45f, MagicWeak = 0.40f, StopY = Layout.StopY },
            E(EnemyKind.FireBomb, "火药兵", "en_firebomb", 68, 0.585f, 195, 0.95f, 28, "冲到墙下自爆；半路击杀会炸伤身边官军"),
            new EnemyDef { Kind = EnemyKind.ChainHorse, Name = "连环马", Art = "en_chainhorse", Hp = 860, Speed = 0.351f, WallDps = 64, Size = 1.7f, Coin = 180,
                Desc = "铁索相连的重骑，物理减伤，钩镰与法术克制", Rank = EnemyRank.Elite, PhysResist = 0.35f, MagicWeak = 0.20f, StopY = Layout.StopY },
            E(EnemyKind.Luan, "栾廷玉", "en_luan", 6800, 0.295f, 180, 2.6f, 360, "祝家庄教师，飞锤砸晕好汉", EnemyRank.Boss),
            E(EnemyKind.ShiWen, "史文恭", "en_shiwen", 7600, 0.255f, 175, 2.6f, 400, "曾头市教师，冲锋突进，残血狂暴", EnemyRank.Boss),
            E(EnemyKind.HuYan, "呼延灼", "en_huyan", 8400, 0.285f, 180, 2.7f, 440, "双鞭将，不断召来连环骑兵", EnemyRank.Boss),
            E(EnemyKind.GaoQiu, "高俅", "en_gaoqiu", 10200, 0.280f, 190, 2.6f, 480, "殿帅府太尉，踢火球砸墙，带护盾", EnemyRank.Boss),
            E(EnemyKind.Diver, "水鬼", "en_diver", 64, 0.482f, 12, 0.95f, 24, "时不时潜进水里，潜着时刀枪不入"),
            E(EnemyKind.Farmer, "庄客", "en_farmer", 48, 0.526f, 9, 0.9f, 14, "祝家庄的庄客，扛着钉耙成群涌来"),
            E(EnemyKind.Hound, "恶犬", "en_hound", 26, 0.833f, 6, 0.75f, 8, "两条一群，跑得飞快"),
            new EnemyDef { Kind = EnemyKind.Rattan, Name = "藤牌手", Art = "en_rattan", Hp = 80, Speed = 0.351f, WallDps = 15, Size = 1.05f, Coin = 30,
                Desc = "藤牌能挡下最先的两次攻击，并减免物理伤害", PhysResist = 0.20f, StopY = Layout.StopY },
            E(EnemyKind.Taoist, "妖道", "en_taoist", 84, 0.365f, 10, 1.0f, 36, "站在后面作法，给身边官军回血", stopY: 1.6f),
            E(EnemyKind.Berserker, "敢死刀手", "en_berserker", 112, 0.438f, 18, 1.0f, 32, "残血时拼命：两秒内受伤减半，狂奔冲墙"),
            new EnemyDef { Kind = EnemyKind.Catapult, Name = "投石车", Art = "en_catapult", Hp = 220, Speed = 0.234f, WallDps = 28, Size = 1.4f, Coin = 52,
                Desc = "远远停下抛石砸墙，打不退", PhysResist = 0.25f, StopY = 2.8f },
            new EnemyDef { Kind = EnemyKind.Lantern, Name = "孔明灯", Art = "en_lantern", Hp = 42, Speed = 0.380f, WallDps = 165, Size = 0.9f, Coin = 20,
                Desc = "飘到墙上就炸，战士够不着，射手克制", Flying = true, ArcherWeak = 0.45f, StopY = Layout.StopY + 0.5f },
            E(EnemyKind.Scout, "斥候", "en_scout", 54, 0.628f, 11, 0.9f, 22, "挨打时常常闪身躲开，往前一窜"),
            E(EnemyKind.Brute, "力士", "en_brute", 250, 0.278f, 24, 1.35f, 44, "背篓里藏着三个官兵，打倒就跳出来"),
            E(EnemyKind.HeTao, "何涛", "en_hetao", 5200, 0.295f, 180, 2.6f, 300, "济州缉捕使臣，驾车船而来，召唤水鬼", EnemyRank.Boss),
            E(EnemyKind.ZhuChaofeng, "祝朝奉", "en_zhucf", 6800, 0.285f, 175, 2.6f, 340, "祝家庄庄主，坐轿撒钱，买来金光护体", EnemyRank.Boss),
            E(EnemyKind.ZengKui, "曾魁", "en_zengkui", 7600, 0.235f, 175, 2.6f, 380, "曾家四虎，骑猛虎扑跃突进", EnemyRank.Boss),
            new EnemyDef { Kind = EnemyKind.HanTao, Name = "韩滔", Art = "en_hantao", Hp = 8600, Speed = 0.285f, WallDps = 180, Size = 2.7f, Coin = 420,
                Desc = "百胜将，推铁头撞车猛撞寨墙，物理减伤", Rank = EnemyRank.Boss, PhysResist = 0.35f, MagicWeak = 0.25f, StopY = Layout.StopY },
            E(EnemyKind.LiuMenglong, "刘梦龙", "en_liuml", 10000, 0.265f, 190, 2.7f, 460, "水军节度使，推云梯楼车，楼上弓手齐射", EnemyRank.Boss, stopY: 0.4f),

            // ---------- 9大水浒特色精英矩阵（补充7种全新章节精英，与教头/连环马构成3×3满屏图鉴） ----------
            E(EnemyKind.WaterChief, "水军统制", "en_waterchief", 920, 0.365f, 72, 1.6f, 200, "水泊官军头目，周期潜入水下刀枪不入并突进破墙", EnemyRank.Elite),
            new EnemyDef { Kind = EnemyKind.CrossbowCapt, Name = "神臂弩统领", Art = "en_crossbowcapt", Hp = 880, Speed = 0.310f, WallDps = 62, Size = 1.6f, Coin = 200,
                Desc = "半路立盾架起神臂重弩连射寨墙，近战够不着，怕法术轰击", Rank = EnemyRank.Elite, MagicWeak = 0.25f, StopY = 0.8f },
            new EnemyDef { Kind = EnemyKind.GoldenEagle, Name = "披甲金雕王", Art = "en_goldeneagle", Hp = 760, Speed = 0.510f, WallDps = 66, Size = 1.65f, Coin = 200,
                Desc = "披挂铁羽的巨型战雕高速扑城，战士打不到，射手克制", Rank = EnemyRank.Elite, Flying = true, ArcherWeak = 0.45f, StopY = Layout.StopY + 0.5f },
            new EnemyDef { Kind = EnemyKind.IronPagoda, Name = "铁浮屠重卫", Art = "en_ironpagoda", Hp = 1120, Speed = 0.295f, WallDps = 74, Size = 1.75f, Coin = 210,
                Desc = "重铁铠挡下最先的两次攻击，大幅减免物理伤害，怕火雷风法术", Rank = EnemyRank.Elite, PhysResist = 0.45f, MagicWeak = 0.40f, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.HighPriest, Name = "护法妖师", Art = "en_highpriest", Hp = 860, Speed = 0.295f, WallDps = 52, Size = 1.6f, Coin = 200,
                Desc = "站在后面作法，给身边官军回血并加速冲锋", Rank = EnemyRank.Elite, MagicWeak = 0.20f, StopY = 1.6f },
            new EnemyDef { Kind = EnemyKind.GiantBrute, Name = "背嵬巨力士", Art = "en_giantbrute", Hp = 1080, Speed = 0.300f, WallDps = 76, Size = 1.75f, Coin = 210,
                Desc = "铁篓里藏着敢死刀手，减免物理伤害，打倒就跳出来狂奔冲墙", Rank = EnemyRank.Elite, PhysResist = 0.25f, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.FireCart, Name = "神火轰天车", Art = "en_firecart", Hp = 920, Speed = 0.220f, WallDps = 60, Size = 1.8f, Coin = 220,
                Desc = "远远停下抛石砸墙，自带护盾并减免物理伤害，怕法术轰击", Rank = EnemyRank.Elite, PhysResist = 0.30f, MagicWeak = 0.25f, StopY = 2.4f },
        };

        public static EnemyDef Enemy(EnemyKind k) => Enemies[(int)k];

        public static PerkDef[] Perks { get; private set; } = BuildPerks();

        struct PerkSpec
        {
            public string Name;
            public Eff Eff, Eff2;
            public float V, V2;
            public int Max;
            public bool Has2, Raw;
        }

        static PerkSpec S(string name, Eff e, float v, int max = 1) =>
            new PerkSpec { Name = name, Eff = e, V = v, Max = max };

        static PerkSpec C(string name, Eff e, float v, Eff e2, float v2, int max = 1) =>
            new PerkSpec { Name = name, Eff = e, V = v, Has2 = true, Eff2 = e2, V2 = v2, Max = max, Raw = true };

        /// <summary>
        /// 每位好汉 7 条核心专属（前 4 条进局内强化三选一，后 3 条为 4 星合成免费大奖）+ 等级解锁专属 + 职业定制取舍/流派质变链。
        /// 全职业严格遵循「同档等值预算（常规卡 ~+62%、取舍税后 ~+76%、满星 ~+105%）+ 职业特性本味（法师修法术连锁/折射/回气，辅助修扩格/加持/破绽）」。
        /// </summary>
        static PerkDef[] BuildPerks()
        {
            var list = new List<PerkDef>();
            void Add(HeroId h, params PerkSpec[] ps)
            {
                var d = Hero(h);
                string key = d.Art.Substring(5);
                for (int i = 0; i < ps.Length; i++)
                {
                    var sp = ps[i];
                    bool star = i >= 4;
                    var (e, v) = sp.Raw ? (sp.Eff, sp.V) : Adapt(d, sp.Eff, sp.V);
                    if (!star && !sp.Raw) v *= Boost(e);
                    Eff e2 = sp.Eff2;
                    float v2 = sp.V2;
                    string desc = sp.Has2
                        ? $"{SingleLineEff(h, e, v)}\n{SingleLineEff(h, e2, v2)}"
                        : EffText(h, e, v);
                    list.Add(new PerkDef
                    {
                        Id = key + "_" + i, Hero = h, Name = sp.Name, Eff = e, V = v,
                        Has2 = sp.Has2, Eff2 = e2, V2 = v2,
                        Max = star ? 1 : sp.Max, Star = star,
                        Group = !star && e == Eff.Count && !sp.Has2 && d.Kind != Sim.Role.Support && d.Chan <= 0 ? "volley" : null,
                        Weight = !star && e == Eff.Count && !sp.Has2 ? 0.80f : 1f,
                        Desc = desc,
                    });
                }
                bool sup = d.Kind == Sim.Role.Support;
                var kit = HeroKits.Of(h);
                string surge = OptNames.Surge[(int)h];
                list.Add(new PerkDef
                {
                    Id = key + "_surge", Hero = h, Name = surge, Opt = 1, Weight = 1.3f,
                    Eff = sup ? Eff.UltPow : Eff.Surge, V = sup ? 0.50f : SurgeChance,
                    Has2 = sup, Eff2 = Eff.UltCd, V2 = sup ? 0.22f : 0f,
                    Desc = sup ? $"「{d.AtkName}」效力 +50%\n技能冷却 -22%" : $"{Pct(SurgeChance)}几率爆发成巨型{d.Noun ?? "攻击"}\n伤害 +{Pct(SurgeDmg - 1f)}",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_top", Hero = h, Name = sup ? "首席军师" : "最强" + (d.Noun ?? "一击"), Opt = 2, Weight = 1.3f,
                    Eff = sup ? Eff.Aura : Eff.Top, V = sup ? 0.55f : TopDmg,
                    Has2 = sup, Eff2 = Eff.UltPow, V2 = sup ? 0.35f : 0f,
                    Desc = sup ? $"在场光环 +55%\n「{d.AtkName}」效力 +35%" : $"场上最高星的{d.Name}\n伤害 +{Pct(TopDmg)}，体积 +20%",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_opt15", Hero = h, Name = kit.Opt15Name, Opt = 3, Weight = 1.45f,
                    Eff = kit.Opt15Eff, V = kit.Opt15V,
                    Has2 = kit.Opt15V2 > 0f, Eff2 = kit.Opt15Eff2, V2 = kit.Opt15V2,
                    Desc = kit.Opt15Desc,
                });
                if (d.HasUlt)
                    list.Add(new PerkDef
                    {
                        Id = key + "_ult", Hero = h, Name = d.UltName, Eff = Eff.UltPow, Ult = true, Weight = 2.4f,
                        Desc = $"{d.UltTip}\n冷却{d.UltCd:0}秒",
                    });
                else
                    list.Add(new PerkDef
                    {
                        Id = key + "_aoyi", Hero = h, Name = AwakenName(h), Eff = sup ? Eff.Aura : Eff.Dmg, V = 0f,
                        Finisher = true, Weight = 2.4f,
                        Desc = sup
                            ? $"当场觉醒本命奥义\n光环与加持 +{Pct(AwakenAuraBonus)} · 号令回转 +{Pct(AwakenSpeedBonus)}"
                            : d.Kind == Sim.Role.Mage
                                ? $"当场觉醒本命奥义\n法术终伤 +{Pct(AwakenDmgBonus)} · 回气速率 +{Pct(AwakenSpeedBonus)} · 法象追击"
                                : $"当场觉醒本命奥义\n终伤 +{Pct(AwakenDmgBonus)} · {SpeedLabel(d.Kind)} +{Pct(AwakenSpeedBonus)} · 每3轮弹幕",
                    });
                AddPlays(list, d, key);
            }

            // ---------- 射手（5位：拉弓攻速 · 箭矢齐射 · 贯穿弹射 · 弱点暴击） ----------
            Add(HeroId.HuaRong,
                S("连珠箭", Eff.Count, 1, 1), C("一箭双穿", Eff.Chain, 1, Eff.Dmg, 0.22f, 2), C("鹰眼", Eff.Crit, 0.22f, Eff.Dmg, 0.26f, 1), S("强弓", Eff.Dmg, 0.25f, 2),
                S("箭神", Eff.Dmg, 0.8f, 1), S("疾风连射", Eff.Speed, 0.5f, 1), S("箭如雨下", Eff.Count, 1, 1));
            Add(HeroId.ZhangQing,
                C("连环飞石", Eff.Chain, 2, Eff.Dmg, 0.20f, 2), C("石破天惊", Eff.Stun, 0.24f, Eff.Dmg, 0.26f, 1), S("飞蝗石", Eff.Dmg, 0.25f, 2), S("快手", Eff.Speed, 0.30f, 1),
                S("漫天花雨", Eff.Chain, 3, 1), S("金石为开", Eff.Dmg, 0.8f, 1), S("连珠不绝", Eff.Count, 1, 1));
            Add(HeroId.YanQing,
                S("扩充箭匣", Eff.Count, 2, 1), C("破甲箭", Eff.Chain, 1, Eff.Dmg, 0.22f, 2), C("百步", Eff.Crit, 0.22f, Eff.Dmg, 0.26f, 1), S("劲弩", Eff.Dmg, 0.25f, 2),
                S("万箭", Eff.Count, 2, 1), S("封喉", Eff.UltPow, 1f, 1), S("燕子三抄水", Eff.Speed, 0.5f, 1));
            Add(HeroId.XieZhen,
                S("双弦猎箭", Eff.Count, 1, 1), C("猎鹰", Eff.Fly, 0.70f, Eff.Dmg, 0.24f, 1), S("强弓", Eff.Dmg, 0.25f, 2), C("穿杨", Eff.Chain, 1, Eff.Dmg, 0.22f, 1),
                S("打虎", Eff.Elite, 1.5f, 1), S("猎神", Eff.Dmg, 0.8f, 1), S("箭雨", Eff.Count, 1, 1));
            Add(HeroId.XieBao,
                S("双叉齐投", Eff.Count, 1, 1), C("穿刺", Eff.Chain, 1, Eff.Dmg, 0.22f, 2), S("猛叉", Eff.Dmg, 0.25f, 2), C("蝎尾", Eff.Stun, 0.22f, Eff.Dmg, 0.26f, 1),
                S("贯日", Eff.Chain, 3, 1), S("双尾", Eff.Dmg, 0.8f, 1), S("飞叉阵", Eff.Count, 1, 1));

            // ---------- 炮手（4位：重炮口径 · 子母分裂 · 装填提速 · 焦土火海） ----------
            Add(HeroId.LingZhen,
                S("双联重炮", Eff.Count, 1, 1), C("火药加倍", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("子母巨弹", Eff.Split, 1, Eff.Dmg, 0.18f, 1), S("火炮改良", Eff.Dmg, 0.25f, 2),
                S("子母连环", Eff.Count, 1, 1), S("震天雷", Eff.Dmg, 0.8f, 1), S("炮火连天", Eff.Speed, 0.5f, 1));
            Add(HeroId.QinMing,
                S("双星火陨", Eff.Count, 1, 1), C("烈焰", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), S("狼牙", Eff.Dmg, 0.25f, 2), S("暴躁", Eff.Speed, 0.30f, 1),
                S("天火", Eff.Dmg, 0.8f, 1), S("火流星", Eff.UltPow, 1f, 1), S("焚城", Eff.Area, 0.6f, 1));
            Add(HeroId.WeiDingguo,
                S("双罐猛火", Eff.Count, 1, 1), C("爆燃", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), S("神火", Eff.Dmg, 0.25f, 2), C("子母火罐", Eff.Split, 1, Eff.Dmg, 0.18f, 1),
                S("火龙", Eff.Burn, 1.2f, 1), S("焚天", Eff.Dmg, 0.8f, 1), S("连环罐", Eff.Count, 1, 1));
            Add(HeroId.KongLiang,
                S("双响火雷", Eff.Count, 1, 1), C("猛火加料", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("子母火雷", Eff.Split, 1, Eff.Dmg, 0.18f, 1), S("急促引线", Eff.Speed, 0.30f, 2),
                S("燎原", Eff.Dmg, 0.8f, 1), S("星火", Eff.Count, 1, 1), S("火海", Eff.Area, 0.6f, 1));

            // ---------- 法师（5位：连锁跃迁 · 射线折射 · 法阵规模 · 凝神回气 —— 零物理攻速词条！） ----------
            Add(HeroId.GongSun,
                S("双道天雷", Eff.Count, 1, 1), C("雷鸣增幅", Eff.Chain, 2, Eff.Dmg, 0.20f, 2), C("掌心麻痹", Eff.Stun, 0.24f, Eff.Dmg, 0.26f, 1), S("天雷", Eff.Dmg, 0.25f, 2),
                S("九天雷劫", Eff.Chain, 4, 1), S("雷霆万钧", Eff.Dmg, 0.8f, 1), S("五雷正法", Eff.UltPow, 1f, 1));
            Add(HeroId.FanRui,
                C("狂风", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("走石", Eff.Stun, 0.22f, Eff.Dmg, 0.26f, 1), S("魔焰", Eff.Dmg, 0.25f, 2), C("迷阵", Eff.Slow, 1.2f, Eff.Count, 1f, 1),
                S("混世", Eff.Dmg, 0.8f, 1), S("飞沙", Eff.UltPow, 1f, 1), S("魔王", Eff.Area, 0.6f, 1));
            Add(HeroId.QiaoDaoqing,
                S("真火", Eff.Dmg, 0.25f, 2), C("火海", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("幻影", Eff.Count, 1.5f, Eff.Dmg, 0.20f, 1), S("凝神回气", Eff.Speed, 0.30f, 1),
                S("炼狱", Eff.Dmg, 0.8f, 1), S("天火焚营", Eff.Area, 0.6f, 1), S("幻魔", Eff.Count, 2f, 1));
            Add(HeroId.ShanTinggui,
                C("浪涌", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("寒水", Eff.Slow, 1.2f, Eff.Dmg, 0.26f, 1), S("水龙", Eff.Dmg, 0.25f, 2), S("玄水回气", Eff.Speed, 0.30f, 1),
                S("狂澜", Eff.Count, 2f, 1), S("圣水", Eff.Dmg, 0.8f, 1), S("沧海", Eff.Area, 0.6f, 1));
            Add(HeroId.MaLing,
                S("双印金砖", Eff.Count, 1, 1), S("金砖", Eff.Dmg, 0.25f, 2), C("镇压", Eff.Stun, 0.22f, Eff.Dmg, 0.26f, 1), C("金光弹射", Eff.Chain, 2, Eff.Dmg, 0.18f, 2),
                S("破阵", Eff.Dmg, 0.8f, 1), S("镇魔", Eff.Chain, 3, 1), S("法相金身", Eff.Speed, 0.5f, 1));

            // ---------- 战士（7位：多段连斩 · 刀气枪芒 · 破甲重创 · 近战死斗 —— 与射手数量梯度完全拉平！） ----------
            Add(HeroId.WuSong,
                S("连环铁拳", Eff.Count, 1, 1), S("三碗不过冈", Eff.Speed, 0.30f, 2), S("打虎", Eff.Elite, 0.85f, 1), S("铁臂", Eff.Dmg, 0.25f, 2),
                S("醉拳", Eff.Speed, 0.5f, 1), S("天伤星", Eff.Dmg, 0.8f, 1), S("醉打蒋门神", Eff.Count, 1, 1));
            Add(HeroId.LinChong,
                C("枪及百步", Eff.Range, 1.6f, Eff.Dmg, 0.28f, 2), C("豹子头", Eff.Crit, 0.22f, Eff.Dmg, 0.26f, 1), C("林教头", Eff.Slow, 1.5f, Eff.Dmg, 0.26f, 1), S("枪出如龙", Eff.Dmg, 0.25f, 2),
                S("雪夜上梁山", Eff.Dmg, 0.8f, 1), S("风雪", Eff.UltPow, 1f, 1), S("枪挑", Eff.Speed, 0.5f, 1));
            Add(HeroId.LuZhiShen,
                C("禅杖横扫", Eff.Area, 0.38f, Eff.Dmg, 0.18f, 2), S("大闹野猪林", Eff.Speed, 0.30f, 2), S("拳打镇关西", Eff.Elite, 0.85f, 1), S("金刚", Eff.Dmg, 0.25f, 2),
                S("倒拔杨柳", Eff.Speed, 0.5f, 1), S("怒目金刚", Eff.Dmg, 0.8f, 1), S("坐化", Eff.Area, 0.6f, 1));
            Add(HeroId.LuJunyi,
                C("横扫千军", Eff.Area, 0.38f, Eff.Dmg, 0.18f, 2), S("棍棒无双", Eff.Dmg, 0.25f, 2), C("玉麒麟", Eff.Crit, 0.22f, Eff.Dmg, 0.26f, 1), S("疾枪", Eff.Speed, 0.30f, 1),
                S("枪棒天下", Eff.Dmg, 0.8f, 1), S("踏阵", Eff.UltPow, 1f, 1), S("麒麟", Eff.Speed, 0.5f, 1));
            Add(HeroId.GuanSheng,
                C("偃月斩", Eff.Area, 0.38f, Eff.Dmg, 0.18f, 2), S("斩将", Eff.Elite, 0.85f, 1), S("赤面", Eff.Dmg, 0.25f, 2), S("快刀", Eff.Speed, 0.30f, 1),
                S("青龙出水", Eff.Dmg, 0.8f, 1), S("拖刀斩", Eff.UltPow, 1f, 1), S("刀气纵横", Eff.Area, 0.6f, 1));
            Add(HeroId.LiKui,
                S("双斧同掷", Eff.Count, 1, 1), C("旋风", Eff.Area, 0.38f, Eff.Dmg, 0.18f, 2), S("莽撞", Eff.Speed, 0.30f, 2), S("砍杀", Eff.Dmg, 0.25f, 2),
                S("杀四虎", Eff.Elite, 1.5f, 1), S("卷地黑风", Eff.Count, 1, 1), S("铁牛", Eff.Dmg, 0.8f, 1));
            Add(HeroId.ShiXiu,
                S("拼命连斩", Eff.Count, 1, 1), S("快刀", Eff.Speed, 0.30f, 2), C("死战到底", Eff.Crit, 0.22f, Eff.Dmg, 0.26f, 1), S("劈砍", Eff.Dmg, 0.25f, 2),
                S("三郎", Eff.Dmg, 0.8f, 1), S("不要命", Eff.Speed, 0.5f, 1), S("劫法场", Eff.Count, 1, 1));

            // ---------- 控制（5位：封路扩阵 · 硬控延长 · 破阵易伤 · 克制锁拿 —— 零纯控0伤废卡！） ----------
            Add(HeroId.HuSanniang,
                S("双索齐飞", Eff.Count, 1, 1), S("双刀", Eff.Dmg, 0.25f, 2), C("缚", Eff.Stun, 0.45f, Eff.Dmg, 0.26f, 1), C("擒将", Eff.Elite, 0.75f, Eff.Dmg, 0.22f, 1),
                S("红锦", Eff.Count, 1, 1), S("擒王", Eff.UltPow, 1f, 1), S("胭脂马", Eff.Speed, 0.5f, 1));
            Add(HeroId.ZhuWu,
                C("阵眼", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("困阵", Eff.Slow, 1.4f, Eff.Dmg, 0.24f, 2), C("锁阵", Eff.Stun, 0.22f, Eff.Dmg, 0.26f, 1), S("军师", Eff.Dmg, 0.25f, 1),
                S("金锁", Eff.Dmg, 0.8f, 1), S("奇门", Eff.Area, 0.6f, 1), S("遁甲", Eff.Speed, 0.5f, 1));
            Add(HeroId.XuNing,
                C("钩镰", Eff.Cav, 0.75f, Eff.Dmg, 0.24f, 2), S("枪法纯熟", Eff.Dmg, 0.25f, 2), C("绊马", Eff.Stun, 0.24f, Eff.Dmg, 0.26f, 1), C("长钩", Eff.Range, 1.6f, Eff.Dmg, 0.28f, 1),
                C("破连环", Eff.Cav, 1.2f, Eff.Dmg, 0.40f, 1), S("枪阵", Eff.Dmg, 0.8f, 1), S("金枪手", Eff.Speed, 0.5f, 1));
            Add(HeroId.SunErniang,
                C("加量", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("药力", Eff.Stun, 0.24f, Eff.Dmg, 0.26f, 1), S("毒雾", Eff.Dmg, 0.25f, 2), C("迷香", Eff.Slow, 1.4f, Eff.Dmg, 0.24f, 1),
                S("黑店", Eff.Stun, 0.45f, 1), S("夜叉", Eff.Dmg, 0.8f, 1), S("迷魂", Eff.Area, 0.6f, 1));
            Add(HeroId.RuanXiaoqi,
                C("大网", Eff.Area, 0.36f, Eff.Dmg, 0.18f, 2), C("缠", Eff.Stun, 0.45f, Eff.Dmg, 0.26f, 1), S("水性", Eff.Speed, 0.30f, 2), S("阎罗", Eff.Dmg, 0.25f, 1),
                S("网罗四方", Eff.Dmg, 0.8f, 1), S("捞鱼", Eff.Area, 0.6f, 1), S("浪里", Eff.Speed, 0.5f, 1));

            // ---------- 辅助（6位：广域传令 · 光环倍增 · 号令回转 · 聚财破绽 —— 零攻速/零平A伤害词条！） ----------
            Add(HeroId.WuYong,
                S("运筹", Eff.Aura, 0.50f, 2), S("羽扇纶巾", Eff.UltPow, 0.50f, 2), C("智多星", Eff.Aura, 0.32f, Eff.Coin, 28f, 1), C("广发锦囊", Eff.Count, 1f, Eff.UltCd, 0.14f, 1),
                S("智取", Eff.Aura, 0.8f, 1), S("帷幄", Eff.UltPow, 0.8f, 1), S("吴学究", Eff.Count, 1f, 1));
            Add(HeroId.AnDaoquan,
                S("金针", Eff.Aura, 0.50f, 2), S("药王", Eff.UltPow, 0.50f, 2), S("回春", Eff.UltCd, 0.25f, 1), C("悬壶广济", Eff.Count, 1f, Eff.Aura, 0.28f, 1),
                S("华佗再世", Eff.Aura, 0.8f, 1), S("起死回生", Eff.UltPow, 0.8f, 1), S("济世", Eff.UltCd, 0.35f, 1));
            Add(HeroId.DaiZong,
                S("甲马", Eff.Aura, 0.50f, 2), S("神行", Eff.Speed, 0.30f, 2), S("符咒", Eff.UltPow, 0.50f, 1), C("急递八方", Eff.Count, 1f, Eff.UltCd, 0.15f, 1),
                S("千里", Eff.Aura, 0.8f, 1), S("神速", Eff.UltCd, 0.35f, 1), S("太保", Eff.UltPow, 0.8f, 1));
            Add(HeroId.TangLong,
                S("淬火", Eff.Aura, 0.50f, 2), S("重锤", Eff.UltPow, 0.50f, 2), S("急锻", Eff.UltCd, 0.25f, 1), C("名匠延火", Eff.Count, 1f, Eff.Aura, 0.28f, 1),
                S("神兵", Eff.Aura, 0.8f, 1), S("百炼", Eff.UltPow, 0.8f, 1), S("千锤", Eff.UltCd, 0.35f, 1));
            Add(HeroId.ShiQian,
                S("妙手", Eff.Aura, 0.50f, 2), S("急探", Eff.UltCd, 0.25f, 1), C("偷鸡", Eff.Aura, 0.34f, Eff.Coin, 36f, 1), C("暗影连探", Eff.Count, 1f, Eff.UltPow, 0.28f, 1),
                S("盗甲", Eff.Aura, 0.8f, 1), S("神偷", Eff.UltPow, 0.8f, 1), S("梁上飞盗", Eff.Coin, 55f, 1));
            Add(HeroId.JiangJing,
                S("复利", Eff.Aura, 0.50f, 2), S("算珠", Eff.UltPow, 0.50f, 2), S("心算如飞", Eff.UltCd, 0.25f, 1), C("广开财源", Eff.Count, 1f, Eff.Aura, 0.28f, 1),
                S("富甲", Eff.Aura, 0.8f, 1), S("神算", Eff.UltPow, 0.8f, 1), S("账房", Eff.UltCd, 0.35f, 1));
            return list.ToArray();
        }

        /// <summary>
        /// 按六大职业特性定制取舍卡、流派质变→进阶链与阵容战术卡：
        /// 1) 所有取舍卡税后乘算净收益严格达到 +70%~+82%（显著高于普通卡），20级免除负面代价；
        /// 2) 射手与战士/炮手/法术单发英雄的多重取舍卡 100% 拉平为「数量+2，单次伤-25%」，彻底消除职业双标与同池完爆；
        /// 3) 法师与辅助全池零「攻速」字样，各自拥有超导雷链、光束折射、风眼千刃、军师破阵等纯正职业进化线。
        /// </summary>
        static void AddPlays(List<PerkDef> list, HeroDef d, string key)
        {
            string n = d.Noun ?? "攻击";
            string spdName = SpeedLabel(d.Kind);
            bool sup = d.Kind == Sim.Role.Support;
            PerkDef Trade(string id, string name, Eff e, float v, Eff e2, float v2, string group, string desc) => new PerkDef
            {
                Id = key + "_" + id, Hero = d.Id, Name = name, Kind = PerkKind.Trade, Eff = e, V = v, Has2 = v2 != 0, Eff2 = e2, V2 = v2,
                Group = group, Weight = 0.92f, Desc = desc,
            };

            if (sup)
            {
                list.Add(Trade("heavy", "宏图大略", Eff.UltPow, 1.15f, Eff.Speed, -0.18f, "pace", $"「{d.AtkName}」效力 +115%\n号令回转 -18%"));
                list.Add(Trade("quick", "羽檄飞驰", Eff.Speed, 0.90f, Eff.UltPow, -0.10f, "pace", $"号令回转 +90%\n「{d.AtkName}」效力 -10%"));
                list.Add(Trade("wide", "传令三军", Eff.Count, 2f, Eff.Speed, -0.18f, null, $"{SupportCountLine(d.Id, 2f)}\n号令回转 -18%"));
                string sMorph = key + "_morph";
                list.Add(new PerkDef
                {
                    Id = sMorph, Hero = d.Id, Name = "军师破阵", Kind = PerkKind.Morph, Eff = Eff.Vuln, V = 0.28f,
                    Weight = 1.15f, Desc = $"施放「{d.AtkName}」时标记强敌\n4 秒内受全队伤害 +28%",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_adv", Hero = d.Id, Name = "决胜庙算", Kind = PerkKind.Adv, Eff = Eff.Vuln, V = 0.22f, Req = sMorph,
                    Has2 = true, Eff2 = Eff.Aura, V2 = 0.35f,
                    Weight = 1.65f, Desc = "破阵易伤升至 +50% 且多锁 1 敌\n在场光环 +35%",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_sync", Hero = d.Id, Name = "众志成城", Kind = PerkKind.Value,
                    Eff = Eff.Aura, V = 0.42f, Has2 = true, Eff2 = Eff.UltPow, V2 = 0.42f,
                    Weight = 1.15f, Desc = "在场光环 +42%\n主动加持效力 +42%",
                });
                list.Add(new PerkDef
                {
                    Id = key + "_aid", Hero = d.Id, Name = "同袍共济", Kind = PerkKind.Value,
                    Eff = Eff.UltCd, V = 0.20f, Has2 = true, Eff2 = Eff.Aura, V2 = 0.35f,
                    Weight = 1.10f, Desc = "技能冷却 -20%\n在场光环 +35%",
                });
                return;
            }

            // 1) 职业本味节奏取舍（二选一互斥组 pace）：税后净收益 +56% ~ +64%（高于常规卡 ~+46%，20级免负面）
            if (d.Kind == Sim.Role.Mage)
            {
                list.Add(Trade("heavy", "禁咒蓄力", Eff.Dmg, 1.00f, Eff.Speed, -0.18f, "pace", $"法术伤害 +100%\n{spdName} -18%"));
                list.Add(Trade("quick", "瞬发连吟", Eff.Speed, 0.80f, Eff.Dmg, -0.12f, "pace", $"{spdName} +80%\n法术伤害 -12%"));
            }
            else if (d.Kind == Sim.Role.Bomber)
            {
                list.Add(Trade("heavy", "巨型臼炮", Eff.Dmg, 1.00f, Eff.Speed, -0.18f, "pace", $"{n}伤害 +100%\n{spdName} -18%"));
                list.Add(Trade("quick", "速装火药", Eff.Speed, 0.80f, Eff.Dmg, -0.12f, "pace", $"{spdName} +80%\n{n}伤害 -12%"));
            }
            else if (d.Kind == Sim.Role.Control)
            {
                list.Add(Trade("heavy", "镇岳封阵", Eff.Dmg, 0.95f, Eff.Speed, -0.16f, "pace", $"{n}伤害 +95%\n{spdName} -16%"));
                list.Add(Trade("quick", "疾风布阵", Eff.Speed, 0.78f, Eff.Dmg, -0.12f, "pace", $"{spdName} +78%\n{n}伤害 -12%"));
            }
            else
            {
                list.Add(Trade("heavy", "千钧之力", Eff.Dmg, 1.00f, Eff.Speed, -0.18f, "pace", $"{n}伤害 +100%\n攻速 -18%"));
                list.Add(Trade("quick", "快如闪电", Eff.Speed, 0.80f, Eff.Dmg, -0.12f, "pace", $"攻速 +80%\n{n}伤害 -12%"));
            }

            // 2) 规模扩展取舍（wide）：全单发职业统一 +2 / -28% 标尺（与 +1 无惩罚卡归入 volley 互斥组，彻底根除解珍+2 vs 石秀+1双标！）
            var cb = Balance.Current.Combat;
            float multiPen = cb.WideMultiDmgPenalty; // -0.28f
            if (d.Id == HeroId.YanQing)
                list.Add(Trade("wide", "暴雨连弩", Eff.Count, 3f, Eff.Dmg, -0.18f, "volley", $"弩箭数量 +3\n单发伤害 -18%"));
            else if (d.Shot == ShotKind.Beam)
                list.Add(Trade("wide", "万象折射", Eff.Refract, 2f, Eff.Dmg, -0.15f, null, $"命中主目标折射 +2 道副光束\n主束法术伤害 -15%"));
            else if (d.Chan <= 0 && (d.Shot == ShotKind.Arrow || d.Shot == ShotKind.Bomb || d.Shot == ShotKind.Fireball
                || d.Shot == ShotKind.Punch || d.Shot == ShotKind.Lasso || d.Shot == ShotKind.Lightning || d.Shot == ShotKind.Stone))
            {
                string wName = d.Kind == Sim.Role.Fighter ? "疾风连斩"
                    : d.Kind == Sim.Role.Bomber ? "连环齐轰"
                    : d.Kind == Sim.Role.Mage ? "万法齐发"
                    : d.Shot == ShotKind.Lasso ? "天罗齐发" : "万矢齐射";
                string wTop = d.Shot == ShotKind.Punch ? "每次出手多斩 2 击"
                    : d.Shot == ShotKind.Lasso ? "套索同时多锁 2 人"
                    : $"{n}数量 +2";
                list.Add(Trade("wide", wName, Eff.Count, 2f, Eff.Dmg, multiPen, "volley", $"{wTop}\n单次伤害 {Pct(multiPen)}"));
            }
            else
            {
                string aName = d.Kind == Sim.Role.Mage ? "吞天风域" : d.Kind == Sim.Role.Control ? "奇门扩阵" : "大开大合";
                list.Add(Trade("wide", aName, Eff.Area, cb.WideAreaBonus, Eff.Speed, cb.WideAreaSpeedPenalty, null, $"{n}范围 +{Pct(cb.WideAreaBonus)}\n{spdName} {Pct(cb.WideAreaSpeedPenalty)}"));
            }

            // 3) 职业专属流派质变 → 进阶二连卡（改变弹道/连锁/折射/火海形态，凑齐可觉醒本命奥义）
            bool fire = d.Element == "火系";
            Eff me = Eff.Vuln, ae = Eff.Vuln;
            string mname, mdesc, aname, adesc;
            float mv, av;
            switch (d.Shot)
            {
                case ShotKind.Lightning:
                    me = Eff.Conduct; ae = Eff.Conduct; mv = 1f; av = 1f;
                    mname = "高压超导"; mdesc = "闪电连锁 +2 人，跳跃衰减减半\n进化亮青超导电弧";
                    aname = "紫电雷池"; adesc = "闪电跳跃无损 100% 传导\n末端炸开紫电雷池麻痹敌群";
                    break;
                case ShotKind.Stone when d.Kind == Sim.Role.Mage:
                    me = Eff.Conduct; ae = Eff.Conduct; mv = 1f; av = 1f;
                    mname = "乾坤重印"; mdesc = "金砖弹射 +1 次且弹跳不衰减\n落地激起金光震荡环";
                    aname = "镇魂法印"; adesc = "震荡环范围扩大\n被砸敌军 4 秒内受伤害 +35%";
                    break;
                case ShotKind.Stone:
                    me = Eff.Conduct; ae = Eff.Conduct; mv = 1f; av = 1f;
                    mname = "金石贯通"; mdesc = "飞石弹射不再衰减伤害\n每次弹跳迸发青玉碎星溅射";
                    aname = "碎玉星河"; adesc = "碎星溅射范围扩大\n命中的敌军 4 秒内受伤害 +35%";
                    break;
                case ShotKind.Beam when !fire:
                    me = Eff.Refract; ae = Eff.Refract; mv = 1f; av = 1f;
                    mname = "寒晶折射"; mdesc = "圣水命中主目标后\n额外折射 1 道寒流副光束";
                    aname = "冰魄贯流"; adesc = "折射副光束再 +1 道\n被冲刷敌军 4 秒内受伤害 +30%";
                    break;
                case ShotKind.Wind when d.Kind == Sim.Role.Mage:
                    me = Eff.Burn; ae = Eff.Burn; mv = 0.65f; av = 0.95f;
                    mname = "风眼引力"; mdesc = "旋风向中心聚拢周围敌军\n并附加 3 秒 65% 风蚀割裂";
                    aname = "千刃风暴"; adesc = "旋风外围激发高速旋转风刃\n风蚀升至 160% 且阵亡卷起风爆";
                    break;
                case ShotKind.Arrow:
                    me = Eff.Blast; ae = Eff.Blast; mv = 0.55f; av = 1.05f;
                    mname = "爆裂" + n; mdesc = $"{n}命中后爆炸\n周围敌人受 55% 伤害";
                    aname = "火药加量"; adesc = "爆炸范围 +35%\n爆炸伤害 55% → 160%";
                    break;
                case ShotKind.Spear:
                    me = Eff.Reach; ae = Eff.Reach; mv = 2.5f; av = 2f;
                    mname = "一枪穿阵"; mdesc = "贯穿距离 +2.5\n一枪捅穿整列";
                    aname = "枪裂重甲"; adesc = "贯穿距离再 +2\n身后敌人也受全额伤害";
                    break;
                case ShotKind.Bomb:
                case ShotKind.Fireball:
                case ShotKind.Beam when fire:
                    me = Eff.Burn; ae = Eff.Burn; mv = 0.65f; av = 0.95f;
                    mname = "烈火引燃"; mdesc = "命中的敌人燃烧 3 秒\n共受 65% 伤害";
                    aname = "连环爆燃"; adesc = "灼烧伤害 65% → 160%\n燃烧敌人阵亡引爆周围";
                    break;
                case ShotKind.Wind:
                    me = Eff.Burn; ae = Eff.Burn; mv = 0.65f; av = 0.95f;
                    mname = "蚀骨毒雾"; mdesc = "命中的敌人中毒 3 秒\n共受 65% 伤害";
                    aname = "万毒攻心"; adesc = "中毒伤害 65% → 160%\n中毒敌人阵亡传染毒爆";
                    break;
                case ShotKind.Lasso:
                    me = Eff.Burn; ae = Eff.Burn; mv = 0.65f; av = 0.95f;
                    mname = "倒刺锁链"; mdesc = "被套索捆住的敌军流血 3 秒\n共受 65% 伤害";
                    aname = "绞杀铁索"; adesc = "流血伤害 65% → 160%\n阵亡时刀花飞溅伤敌";
                    break;
                default:
                    me = Eff.Vuln; ae = Eff.Vuln; mv = 0.30f; av = 0.22f;
                    mname = "破甲重创"; mdesc = "命中的敌人 4 秒内\n受到所有伤害 +30%";
                    aname = "摧枯拉朽"; adesc = "破甲易伤\n30% → 52%";
                    break;
            }
            string morph = key + "_morph";
            list.Add(new PerkDef { Id = morph, Hero = d.Id, Name = mname, Kind = PerkKind.Morph, Eff = me, V = mv, Weight = 1.15f, Desc = mdesc });
            list.Add(new PerkDef { Id = key + "_adv", Hero = d.Id, Name = aname, Kind = PerkKind.Adv, Eff = ae, V = av, Req = morph, Weight = 1.65f, Desc = adesc });

            // 4) 跨职业战术联动 & 逆风斩杀词条（破冰追击仅在阵容编入控场好汉时智能入池！）
            if (d.Kind == Sim.Role.Control)
            {
                list.Add(new PerkDef { Id = key + "_shatter", Hero = d.Id, Name = "落井下石", Kind = PerkKind.Value, Eff = Eff.Vuln, V = 0.32f, Weight = 1.15f, Desc = "命中的敌人 4 秒内\n所受全队伤害 +32%" });
                bool hardStun = d.Stun > 0 && d.StunChance <= 0;
                bool slowOnly = d.Stun <= 0 && d.Slow > 0;
                list.Add(new PerkDef
                {
                    Id = key + "_exec", Hero = d.Id, Name = "困兽犹斗", Kind = PerkKind.Value,
                    Eff = slowOnly ? Eff.Slow : Eff.Stun, V = slowOnly ? 1.0f : (hardStun ? 0.40f : 0.20f),
                    Has2 = true, Eff2 = Eff.Dmg, V2 = 0.35f,
                    Max = 2, Weight = 1.0f,
                    Desc = (slowOnly ? "减速时间 +1.0 秒（近墙再 +1.2 秒）"
                        : hardStun ? "定身时间 +0.4 秒（近墙再 +0.45 秒）"
                        : "击晕几率 +20%（近墙触发率翻倍）") + $"\n{n}伤害 +35%",
                });
            }
            else
            {
                list.Add(new PerkDef { Id = key + "_shatter", Hero = d.Id, Name = "破冰追击", Kind = PerkKind.Value, Eff = Eff.Shatter, V = 0.65f, Weight = 1.15f, Desc = "对减速、眩晕敌人\n伤害 +65%，暴击 +20%" });
                bool carry = d.Kind == Sim.Role.Fighter || d.Kind == Sim.Role.Archer || d.Kind == Sim.Role.Mage || d.Kind == Sim.Role.Bomber;
                if (carry && d.Rarity >= 3)
                    list.Add(new PerkDef { Id = key + "_res", Hero = d.Id, Name = "众星捧月", Kind = PerkKind.Value, Eff = Eff.Resonance, V = 0.24f, Weight = 1.15f, Desc = $"场上每有 1 个{d.Role}\n最高星{d.Name}\n伤害 +24%" });
                else
                    list.Add(new PerkDef { Id = key + "_exec", Hero = d.Id, Name = "绝地反击", Kind = PerkKind.Value, Eff = Eff.Exec, V = 0.75f, Weight = 1.1f, Desc = "对半血以下敌人伤害 +75%\n寨墙危急时全伤再 +35%" });
            }
        }

        /// <summary>局内普通强化要一眼看得出变强：伤害、范围、攻速类在表里的基础值上放大，确保拿钱买词条远优于无脑盲目召唤。</summary>
        static float Boost(Eff e)
        {
            var c = Balance.Current.Combat;
            return e == Eff.Dmg ? c.PerkDmgBoost : e == Eff.Area ? c.PerkAreaBoost : e == Eff.Speed ? c.PerkSpeedBoost : e == Eff.Crit ? c.PerkCritBoost : 1f;
        }

        /// <summary>5 级解锁的「爆发」：每次出手有几率打出巨型一击；15 级起触发率再加、必定暴击。20 级解锁的「最强」：场上星级最高的那一位加伤害。</summary>
        public static float SurgeChance => Balance.Current.Combat.SurgeChance;
        public static float SurgeDmg => Balance.Current.Combat.SurgeDmg;
        public static float SurgePlusChance => Balance.Current.Combat.SurgePlusChance;
        public static float TopDmg => Balance.Current.Combat.TopDmg;

        public static class OptNames
        {
            public static readonly string[] Surge =
        {
            "箭破长空", "轰天巨炮", "九霄神雷", "拳碎山石", "巨石天降", "蛇矛贯日", "千斤禅杖", "决胜千里",
            "麒麟贯阵", "怒斩千军", "劲弩连珠", "混世狂风", "天罗红锦", "妙手神方", "神行万里", "烈焰狼牙",
            "开山巨斧", "三昧真火", "奇门生克", "钩镰破阵", "圣水滔天", "神火巨罐",
            "背水一战", "猎虎神箭", "蝎尾巨叉", "天降金砖", "火雷连爆", "神兵天成", "十里迷香", "撒网擒敌", "顺风摸月", "利滚利",
        };
            public static readonly string[] Awaken =
        {
            "雁落平沙", "火烧连营", "紫微天罡", "连环十八跌", "流星赶月", "枪魂破阵", "倒拔垂杨柳", "八方风雨",
            "玉麒麟踏天", "武圣显圣", "百步封喉", "魔王蚀天", "红锦擒王", "医圣悬壶", "神行追风", "霹雳焚天",
            "沂岭杀四虎", "幻魔真火", "奇门八卦", "金枪破连环", "弱水三千", "神火燎原",
            "劫法场死斗", "两头蛇穿林", "双尾蝎破甲", "五岳金砖", "独火燎天", "欧冶传人", "十字坡夜叉", "石碣村阎罗", "梁上君子", "铁算通神",
        };
        }

        public static string AwakenName(HeroId h) => OptNames.Awaken[(int)h];
        public static float AwakenDmgBonus => Balance.Current.Combat.AwakenDmgBonus;
        public static float AwakenSpeedBonus => Balance.Current.Combat.AwakenSpeedBonus;
        public static float AwakenAuraBonus => Balance.Current.Combat.AwakenAuraBonus;

        /// <summary>上阵异职业羁绊：2/3/4 种不同职业上阵时，全军最终伤害 +3% / +7% / +12%。</summary>
        public static int DistinctRoles(HeroId[] lineup, out int mask)
        {
            mask = 0;
            int n = 0;
            if (lineup == null) return 0;
            for (int i = 0; i < lineup.Length; i++)
            {
                int bit = 1 << (int)Hero(lineup[i]).Kind;
                if ((mask & bit) != 0) continue;
                mask |= bit;
                n++;
            }
            return n;
        }

        public static float RoleSynergyBonus(HeroId[] lineup)
        {
            int n = DistinctRoles(lineup, out _);
            return n >= 4 ? 0.12f : n == 3 ? 0.07f : n == 2 ? 0.03f : 0f;
        }

        public sealed class BondDef
        {
            public HeroId A, B;
            public string Name, Desc;
            public Role? RoleTarget;
            public float Dmg, Elite, Crit, FlyCav;
        }

        /// <summary>水浒原著兄弟同场羁绊：两人同时在出战阵容中即永久生效。</summary>
        public static readonly BondDef[] Bonds =
        {
            new BondDef { A = HeroId.LuZhiShen, B = HeroId.LinChong, Name = "野猪林结义", Desc = "战士伤害 +12%", RoleTarget = Sim.Role.Fighter, Dmg = 0.12f },
            new BondDef { A = HeroId.WuSong, B = HeroId.LuZhiShen, Name = "二龙山双杰", Desc = "对精英与首领伤害 +15%", Elite = 0.15f },
            new BondDef { A = HeroId.HuaRong, B = HeroId.YanQing, Name = "神臂连珠", Desc = "全军暴击率 +6%", Crit = 0.06f },
            new BondDef { A = HeroId.HuaRong, B = HeroId.ZhangQing, Name = "箭石齐飞", Desc = "射手伤害 +12%", RoleTarget = Sim.Role.Archer, Dmg = 0.12f },
            new BondDef { A = HeroId.XieZhen, B = HeroId.XieBao, Name = "登州双猎", Desc = "对飞行与骑兵伤害 +20%", FlyCav = 0.20f },
            new BondDef { A = HeroId.GongSun, B = HeroId.FanRui, Name = "五雷玄风", Desc = "法师伤害 +12%", RoleTarget = Sim.Role.Mage, Dmg = 0.12f },
        };

        public static List<BondDef> ActiveBonds(HeroId[] lineup)
        {
            var r = new List<BondDef>();
            if (lineup == null) return r;
            foreach (var b in Bonds)
                if (System.Array.IndexOf(lineup, b.A) >= 0 && System.Array.IndexOf(lineup, b.B) >= 0) r.Add(b);
            return r;
        }

        /// <summary>按职业特性命名出手/施法速率：射手战士称攻速，炮手称装填速度，法师称回气速率，控制称布阵速度，辅助称号令回转。</summary>
        public static string SpeedLabel(Role r)
        {
            switch (r)
            {
                case Sim.Role.Mage: return "回气速率";
                case Sim.Role.Bomber: return "装填速度";
                case Sim.Role.Control: return "布阵速度";
                case Sim.Role.Support: return "号令回转";
                default: return "攻速";
            }
        }

        public static string SupportCountLine(HeroId h, float v)
        {
            int k = System.Math.Max(1, (int)System.Math.Round(v));
            switch (h)
            {
                case HeroId.WuYong: return $"「运筹帷幄」多祝福 +{k} 名友军";
                case HeroId.AnDaoquan: return $"「妙手回春」额外鼓舞 +{k} 名主将";
                case HeroId.DaiZong: return $"「神行甲马」对主将额外催速 +{k * 18}%";
                case HeroId.TangLong: return $"「打造神兵」淬火延长 +{k * 2.5f:0.#} 秒";
                case HeroId.ShiQian: return $"「顺手牵羊」多标记 +{k} 名强敌破绽";
                case HeroId.JiangJing: return $"「神机妙算」鼓舞延长 +{k * 2.5f:0.#} 秒";
                default: return $"主动加持目标 +{k}";
            }
        }

        static string SingleLineEff(HeroId h, Eff e, float v) => EffText(h, e, v).Replace("\n", "");

        /// <summary>根据好汉当前等级返回实时卡面说明：满 20 级解锁免除取舍负面后，动态隐去红色负面代价行。</summary>
        public static string PerkDesc(PerkDef p, int heroLv)
        {
            if (p == null) return "";
            if (p.Kind == PerkKind.Trade && p.Has2 && p.V2 < 0f && heroLv >= Meta.NoPenaltyLv && !string.IsNullOrEmpty(p.Desc))
            {
                int nl = p.Desc.IndexOf('\n');
                if (nl > 0) return p.Desc.Substring(0, nl);
            }
            return p.Desc;
        }

        /// <summary>没有大招的好汉，大招类强化改成加伤害、回气/攻速；辅助不打伤害，伤害类强化改成加加持效力。</summary>
        static (Eff, float) Adapt(HeroDef d, Eff e, float v)
        {
            if (d.Kind == Sim.Role.Support)
            {
                if (e == Eff.Dmg || e == Eff.Elite) return (Eff.UltPow, System.Math.Min(v, 0.55f));
                return (e, v);
            }
            if (d.HasUlt) return (e, v);
            if (e == Eff.UltPow) return (Eff.Dmg, 0.8f);
            if (e == Eff.UltCd) return (Eff.Speed, 0.3f);
            return (e, v);
        }

        static string Pct(float v) => (int)System.Math.Round(v * 100) + "%";

        /// <summary>效果说明：强化卡、被动、好汉详情共用（严格贴合六大职业本味术语）。</summary>
        public static string EffText(HeroId h, Eff e, float v)
        {
            var d = Hero(h);
            string n = d.Noun ?? "攻击";
            switch (e)
            {
                case Eff.Dmg: return d.Kind == Sim.Role.Mage ? $"法术伤害 +{Pct(v)}" : $"{n}伤害 +{Pct(v)}";
                case Eff.Speed: return $"{SpeedLabel(d.Kind)} +{Pct(v)}";
                case Eff.Count:
                    if (d.Kind == Sim.Role.Support) return SupportCountLine(h, v);
                    if (d.Chan > 0) return $"施法时间 +{v * ChanPerCount:0.#} 秒";
                    if (d.Shot == ShotKind.Lasso) return $"套索同时锁 +{v:0} 人";
                    if (d.Shot == ShotKind.Punch) return $"每次出手多斩 {v:0} 击";
                    return $"{n}数量 +{v:0}";
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
                    return d.HasUlt ? $"技能与大招\n效力 +{Pct(v)}" : $"「{d.AtkName}」\n效力 +{Pct(v)}";
                case Eff.Range: return $"攻击距离 +{v:0.#}";
                case Eff.Split: return $"{n}落地后\n分裂 3 枚小{n}";
                case Eff.Coin: return $"每次合成额外\n获得 {v:0} 银两";
                case Eff.Fly: return $"对飞行敌人\n伤害 +{Pct(v)}";
                case Eff.Cav: return $"对骑兵、连环马\n伤害 +{Pct(v)}";
                case Eff.Surge: return $"{Pct(v)}几率爆发成巨型{n}\n伤害 +{Pct(SurgeDmg - 1f)}";
                case Eff.Top: return $"场上最高星的{d.Name}\n伤害 +{Pct(v)}，体积 +20%";
                case Eff.Blast: return $"{n}命中后爆炸\n周围敌人受 {Pct(v)} 伤害";
                case Eff.Burn: return $"命中的敌人持续 {BurnDur:0} 秒\n共受 {Pct(v)} 伤害";
                case Eff.Vuln: return $"命中的敌人 {VulnDur:0} 秒内\n受到伤害 +{Pct(v)}";
                case Eff.Reach: return $"贯穿距离 +{v:0.#}";
                case Eff.Shatter: return $"对减速、眩晕敌人\n伤害 +{Pct(v)}，暴击 +20%";
                case Eff.Exec: return $"对半血以下敌人伤害 +{Pct(v)}\n寨墙危急时再 +35%";
                case Eff.Resonance: return $"每有 1 个同职业队友\n最高星{d.Name}伤害 +{Pct(v)}";
                case Eff.Conduct: return d.Shot == ShotKind.Lightning ? "闪电传导衰减大幅降低" : "弹跳不衰减并激起震波";
                case Eff.Refract: return $"命中主目标折射 +{v:0} 道副光束";
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
            foreach (var p in Perks) if (p.Hero == h && p.Star == star && !p.Ult && !p.Finisher) r.Add(p);
            return r;
        }

        public static PerkDef UltPerk(HeroId h)
        {
            foreach (var p in Perks) if (p.Hero == h && p.Ult) return p;
            return null;
        }

        public static PerkDef FinisherPerk(HeroId h)
        {
            foreach (var p in Perks) if (p.Hero == h && (p.Ult || p.Finisher)) return p;
            return null;
        }

        public static PerkDef Perk(string id)
        {
            foreach (var p in Perks) if (p.Id == id) return p;
            return null;
        }

        /// <summary>
        /// 合成星级曲线（对标《永远的蔚蓝星球》实测黄金衰减律）：
        /// 1→2 星伤害整整翻倍（2.0x，零损耗腾格子，开局双 2 星性价比最高）；
        /// 3 星基础 3.30x（约理论值 82.5%，靠解锁 3 星核心形态质变与放大强化词条拉开上限）；
        /// 4 星满星觉醒 4.20x（合体巨型猛将 + 免费触发一次满星专属三选一 + 吃满「众星捧月 / 最强一击 / 大招与三合一奥义」乘区）。
        /// </summary>
        public static float[] StarMul => Balance.Current.Combat.StarMul;
        public static float[] StarSpeed => Balance.Current.Combat.StarSpeed;
        /// <summary>法师的「数量」强化：每级持续施法多这么多秒。</summary>
        public const float ChanPerCount = 0.75f;

        /// <summary>1 星即具备完整职业出招节奏，不再拆成按星级递增的冗长排队连击；多发弹幕由局内强化词条自由构筑。</summary>
        public static int ShotsAt(HeroDef d, int star) => System.Math.Max(1, d.Count);

        /// <summary>灼烧/中毒持续秒数、每跳间隔；破绽持续秒数。</summary>
        public const float BurnDur = 3f, BurnEvery = 0.5f, VulnDur = 4f;

        public static float WaveTime => Balance.Current.WaveStage.WaveTime;
        /// <summary>清场后自然衔接下一波，无需漫长空等或倒计时弹框。</summary>
        public static float WaveMinTime => Balance.Current.WaveStage.WaveMinTime;
        public static float ClearBreath => Balance.Current.WaveStage.ClearBreath;
        /// <summary>一股兵最多在这么几秒内全部出门。</summary>
        public const float PackSpreadMax = 4.2f;

        /// <summary>一波分多股逐步涌出，行军连贯自然，不扎堆成团。</summary>
        public static (float at, float share)[] WavePacks(int wave) => wave <= 2
            ? new[] { (0.3f, 0.55f), (6.5f, 0.45f) }
            : new[] { (0.3f, 0.38f), (5.8f, 0.34f), (11.5f, 0.28f) };

        /// <summary>寨主宋江的战斗技能：自动释放，剑气朝面前的人推过去（第1波尾声首放救场，避免开场替好汉清屏）。</summary>
        public const string LordSkillName = "替天行道";
        public static float LordPctBase => Balance.Current.Combat.LordPctBase;
        public static float LordCdBase => Balance.Current.Combat.LordCdBase;
        public static float LordFirstCast => Balance.Current.Combat.LordFirstCast;
        /// <summary>剑气刃口每秒走的格数（由数值管理中心 Combat.LordWaveSpeed 统一调控）。</summary>
        public static float LordWaveSpeed => Balance.Current.Combat.LordWaveSpeed;
        /// <summary>全局武器弹道与刀剑枪斧特效节奏倍率（由数值管理中心统一调控）。</summary>
        public static float ProjPace => Balance.Current.Combat.ProjPace;
        public static float VfxPace => Balance.Current.Combat.VfxPace;
        public static float BeamFlowSpeed => Balance.Current.Combat.BeamFlowSpeed;
        /// <summary>冷却转满时剑气够不着人，最多再等这么久；再没人就空过这一轮，圈重新转，不能停在满圈上。</summary>
        public const float LordHold = 2.5f;
        public static int SummonsPerSlot => Balance.Current.Economy.SummonsPerSlot;
        public static int EnhanceUnlockSummons => Balance.Current.Economy.EnhanceUnlockSummons;
        public const int LevelsPerChapter = 10;

        /// <summary>第 n+1 次召唤的价格：严格按召唤价格表取值，不走线性外推（超出表长时维持最后一格价格）。</summary>
        public static int SummonCost(int n)
        {
            var tbl = Balance.Current.Economy.SummonTable;
            if (tbl == null || tbl.Length == 0) return 10;
            return tbl[System.Math.Max(0, System.Math.Min(n, tbl.Length - 1))];
        }

        /// <summary>第 n+1 次局内强化的价格：严格按强化价格表取值，不走线性递增（超出表长时维持最后一格价格）。</summary>
        public static int EnhanceCost(int n)
        {
            var tbl = Balance.Current.Economy.EnhanceTable;
            if (tbl == null || tbl.Length == 0) return 100;
            return tbl[System.Math.Max(0, System.Math.Min(n, tbl.Length - 1))];
        }

        /// <summary>波次同种怪血量斜率：10波怪量暴涨近10倍、单怪血量涨约1.04倍，不买词条中后期崩盘，买齐词条爽快割草。</summary>
        public static float HpScale(int wave) => 1f + Balance.Current.WaveStage.HpScaleSlope * (wave - 1);

        /// <summary>小兵「厚度」：前4波平滑收敛过渡（杜绝第3波比第1波脆的倒挂，开场1星需多击点杀）。</summary>
        public static float Tough(int wave)
        {
            var ws = Balance.Current.WaveStage;
            var et = ws.EarlyWaveTough;
            float mul = et != null && wave >= 1 && wave <= et.Length ? et[wave - 1] : 1f;
            return ws.Bulk * mul;
        }

        public static float Bulk => Balance.Current.WaveStage.Bulk;

        /// <summary>击杀赏银：全波次按面板赏银恒定直掉（默认倍率 1.0、波次涨幅 0，不做局内波次兵种加成，所见即所得）。</summary>
        public static int KillCoin(EnemyDef def, int wave, float coinAura)
        {
            var eco = Balance.Current.Economy;
            float raw = def.Big
                ? def.Coin * eco.BigKillCoinMul
                : def.Coin * eco.KillCoinMul * (1f + eco.KillCoinWaveSlope * System.Math.Max(0, wave - 1));
            return System.Math.Max(0, (int)System.Math.Round(raw * (1f + coinAura)));
        }

        /// <summary>每撑过/击退一波发放的守城军饷（w 为刚刚完成的波次 1..Waves-1）。</summary>
        public static int WaveClearBonus(int completedWave)
        {
            var eco = Balance.Current.Economy;
            return System.Math.Max(0, eco.WaveBonusBase + eco.WaveBonusPerWave * completedWave);
        }

        /// <summary>首领血量压过阶段线时爆出的钱袋银两。</summary>
        public static int BossPhaseDropCoin(int wave)
        {
            var eco = Balance.Current.Economy;
            return System.Math.Max(0, eco.BossDropCoinBase + eco.BossDropCoinPerWave * wave);
        }

        /// <summary>每波小兵数：对标《永远的蔚蓝星球》割草曲线（第 1 波 10 只 → 第 10 波 95 只，共 482 只）。</summary>
        public static int WaveCount(int wave)
        {
            var ws = Balance.Current.WaveStage;
            var wc = ws.WaveCounts;
            return wave <= wc.Length ? wc[System.Math.Max(1, wave) - 1] : wc[wc.Length - 1] + ws.WaveCountOverflowStep * (wave - wc.Length);
        }

        // ---------------- 关卡渐进兵种编排（每关仅出 2~3 种小兵，新兵种逐关解锁、波内逐步登场） ----------------

        static TroopMix T(EnemyKind k, int from)
        {
            switch (k)
            {
                case EnemyKind.Catapult:
                case EnemyKind.Brute:
                case EnemyKind.Taoist:
                    return new TroopMix(k, 0.12f, 0.008f, from);
                case EnemyKind.FireBomb:
                case EnemyKind.Lantern:
                case EnemyKind.Armored:
                    return new TroopMix(k, 0.13f, 0.008f, from);
                case EnemyKind.Farmer:
                case EnemyKind.Hound:
                case EnemyKind.Cavalry:
                    return new TroopMix(k, 0.24f, 0.014f, from);
                default:
                    return new TroopMix(k, 0.20f, 0.012f, from);
            }
        }

        public static readonly TroopMix[] Mix1 = { T(EnemyKind.Shield, 2), T(EnemyKind.Cavalry, 4) };

        /// <summary>
        /// 按关卡精细编排本关兵种（不含基础官兵）：
        /// 第1~2关仅 1 种特色兵（共2种小兵），后续每关精选 2 种互补特色兵（共3种小兵）；
        /// 新怪物随关卡逐个首次亮相，波次内第1波纯官兵、第2波出副兵A、第4波出副兵B，绝不一窝蜂涌出六七种怪。
        /// </summary>
        static TroopMix[] StageTroops(int n)
        {
            switch (n)
            {
                // 第一章 金沙滩：逐关解锁 盾兵(1) → 骑兵(2) → 弩手(3) → 恶犬(4) → 水鬼(5)
                case 1: return new[] { T(EnemyKind.Shield, 3) };
                case 2: return new[] { T(EnemyKind.Cavalry, 2) };
                case 3: return new[] { T(EnemyKind.Shield, 2), T(EnemyKind.Crossbow, 4) };
                case 4: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Hound, 4) };
                case 5: return new[] { T(EnemyKind.Shield, 2), T(EnemyKind.Diver, 3) };
                case 6: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Crossbow, 4) };
                case 7: return new[] { T(EnemyKind.Hound, 2), T(EnemyKind.Shield, 4) };
                case 8: return new[] { T(EnemyKind.Diver, 2), T(EnemyKind.Crossbow, 4) };
                case 9: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Diver, 4) };
                case 10: return new[] { T(EnemyKind.Shield, 2), T(EnemyKind.Crossbow, 4) };

                // 第二章 三打祝家庄：逐关解锁 庄客(11) → 鹰隼(12) → 藤牌手(14)
                case 11: return new[] { T(EnemyKind.Farmer, 2), T(EnemyKind.Shield, 4) };
                case 12: return new[] { T(EnemyKind.Hawk, 2), T(EnemyKind.Farmer, 4) };
                case 13: return new[] { T(EnemyKind.Hawk, 2), T(EnemyKind.Cavalry, 4) };
                case 14: return new[] { T(EnemyKind.Rattan, 2), T(EnemyKind.Crossbow, 4) };
                case 15: return new[] { T(EnemyKind.Rattan, 2), T(EnemyKind.Hawk, 4) };
                case 16: return new[] { T(EnemyKind.Farmer, 2), T(EnemyKind.Hound, 4) };
                case 17: return new[] { T(EnemyKind.Hawk, 2), T(EnemyKind.Crossbow, 4) };
                case 18: return new[] { T(EnemyKind.Rattan, 2), T(EnemyKind.Cavalry, 4) };
                case 19: return new[] { T(EnemyKind.Hawk, 2), T(EnemyKind.Shield, 4) };
                case 20: return new[] { T(EnemyKind.Rattan, 2), T(EnemyKind.Crossbow, 4) };

                // 第三章 夜打曾头市：逐关解锁 铁甲兵(21) → 斥候(23) → 敢死刀手(25) → 妖道(27)
                case 21: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Crossbow, 4) };
                case 22: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Hawk, 4) };
                case 23: return new[] { T(EnemyKind.Scout, 2), T(EnemyKind.Cavalry, 4) };
                case 24: return new[] { T(EnemyKind.Scout, 2), T(EnemyKind.Rattan, 4) };
                case 25: return new[] { T(EnemyKind.Berserker, 2), T(EnemyKind.Armored, 4) };
                case 26: return new[] { T(EnemyKind.Berserker, 2), T(EnemyKind.Hawk, 4) };
                case 27: return new[] { T(EnemyKind.Shield, 2), T(EnemyKind.Taoist, 3) };
                case 28: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Taoist, 4) };
                case 29: return new[] { T(EnemyKind.Scout, 2), T(EnemyKind.Berserker, 4) };
                case 30: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Berserker, 4) };

                // 第四章 大破连环马：逐关解锁 火药兵(31) → 力士(33) → 连环马精英(34) → 投石车(36)
                case 31: return new[] { T(EnemyKind.FireBomb, 2), T(EnemyKind.Cavalry, 4) };
                case 32: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.FireBomb, 4) };
                case 33: return new[] { T(EnemyKind.Brute, 2), T(EnemyKind.Crossbow, 4) };
                case 34: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Brute, 4) };
                case 35: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.FireBomb, 4) };
                case 36: return new[] { T(EnemyKind.Shield, 2), T(EnemyKind.Catapult, 3) };
                case 37: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Catapult, 4) };
                case 38: return new[] { T(EnemyKind.Berserker, 2), T(EnemyKind.FireBomb, 4) };
                case 39: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Brute, 4) };
                case 40: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Catapult, 4) };

                // 第五章 三败高太尉：逐关解锁 孔明灯(42)，精选双兵种高阶战术组合
                case 41: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Diver, 4) };
                case 42: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Lantern, 3) };
                case 43: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Hawk, 4) };
                case 44: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.FireBomb, 4) };
                case 45: return new[] { T(EnemyKind.Armored, 2), T(EnemyKind.Taoist, 4) };
                case 46: return new[] { T(EnemyKind.Diver, 2), T(EnemyKind.Catapult, 4) };
                case 47: return new[] { T(EnemyKind.Berserker, 2), T(EnemyKind.Lantern, 4) };
                case 48: return new[] { T(EnemyKind.Scout, 2), T(EnemyKind.FireBomb, 4) };
                case 49: return new[] { T(EnemyKind.Brute, 2), T(EnemyKind.Taoist, 4) };
                default: return new[] { T(EnemyKind.Cavalry, 2), T(EnemyKind.Armored, 4) };
            }
        }

        /// <summary>
        /// 按 50 关章节与兵种特色精细编排精英统领（对标《永远的蔚蓝星球》）：
        /// - 普通关（非 5 倍数）：返回 [第4波守关主精英, 第9波压轴副精英]，相邻关卡主统领不重复；
        /// - 首领关（逢 5 / 10）：第4波由大首领亲自坐镇，此处返回 [第9波压轴副将精英]。
        /// 9 种精英在 5 大章节逐章亮相，与本关小怪克制属性呼应！
        /// </summary>
        static EnemyKind[] StageElites(int n)
        {
            switch (n)
            {
                // 第一章 金沙滩：禁军教头(1) + 水军统制(4亮相) + 神臂弩统领(8预告)
                case 1:  return new[] { EnemyKind.Elite, EnemyKind.Elite };
                case 2:  return new[] { EnemyKind.CrossbowCapt, EnemyKind.Elite };
                case 3:  return new[] { EnemyKind.Elite, EnemyKind.CrossbowCapt };
                case 4:  return new[] { EnemyKind.WaterChief, EnemyKind.Elite };
                case 5:  return new[] { EnemyKind.WaterChief };
                case 6:  return new[] { EnemyKind.WaterChief, EnemyKind.CrossbowCapt };
                case 7:  return new[] { EnemyKind.Elite, EnemyKind.WaterChief };
                case 8:  return new[] { EnemyKind.CrossbowCapt, EnemyKind.WaterChief };
                case 9:  return new[] { EnemyKind.WaterChief, EnemyKind.Elite };
                case 10: return new[] { EnemyKind.Elite };

                // 第二章 三打祝家庄：神臂弩统领 + 披甲金雕王(12随鹰隼亮相)
                case 11: return new[] { EnemyKind.CrossbowCapt, EnemyKind.Elite };
                case 12: return new[] { EnemyKind.GoldenEagle, EnemyKind.CrossbowCapt };
                case 13: return new[] { EnemyKind.CrossbowCapt, EnemyKind.GoldenEagle };
                case 14: return new[] { EnemyKind.Elite, EnemyKind.CrossbowCapt };
                case 15: return new[] { EnemyKind.GoldenEagle };
                case 16: return new[] { EnemyKind.WaterChief, EnemyKind.GoldenEagle };
                case 17: return new[] { EnemyKind.GoldenEagle, EnemyKind.CrossbowCapt };
                case 18: return new[] { EnemyKind.CrossbowCapt, EnemyKind.Elite };
                case 19: return new[] { EnemyKind.GoldenEagle, EnemyKind.WaterChief };
                case 20: return new[] { EnemyKind.CrossbowCapt };

                // 第三章 夜打曾头市：铁浮屠重卫(21随铁甲亮相) + 护法妖师(26/27随妖道亮相)
                case 21: return new[] { EnemyKind.IronPagoda, EnemyKind.CrossbowCapt };
                case 22: return new[] { EnemyKind.GoldenEagle, EnemyKind.IronPagoda };
                case 23: return new[] { EnemyKind.IronPagoda, EnemyKind.WaterChief };
                case 24: return new[] { EnemyKind.CrossbowCapt, EnemyKind.IronPagoda };
                case 25: return new[] { EnemyKind.IronPagoda };
                case 26: return new[] { EnemyKind.HighPriest, EnemyKind.GoldenEagle };
                case 27: return new[] { EnemyKind.IronPagoda, EnemyKind.HighPriest };
                case 28: return new[] { EnemyKind.HighPriest, EnemyKind.IronPagoda };
                case 29: return new[] { EnemyKind.IronPagoda, EnemyKind.HighPriest };
                case 30: return new[] { EnemyKind.HighPriest };

                // 第四章 大破连环马：连环铁骑(31) + 背嵬巨力士(33随力士亮相)
                case 31: return new[] { EnemyKind.ChainHorse, EnemyKind.IronPagoda };
                case 32: return new[] { EnemyKind.IronPagoda, EnemyKind.ChainHorse };
                case 33: return new[] { EnemyKind.GiantBrute, EnemyKind.ChainHorse };
                case 34: return new[] { EnemyKind.ChainHorse, EnemyKind.GiantBrute };
                case 35: return new[] { EnemyKind.ChainHorse };
                case 36: return new[] { EnemyKind.GiantBrute, EnemyKind.CrossbowCapt };
                case 37: return new[] { EnemyKind.ChainHorse, EnemyKind.GiantBrute };
                case 38: return new[] { EnemyKind.GiantBrute, EnemyKind.HighPriest };
                case 39: return new[] { EnemyKind.ChainHorse, EnemyKind.IronPagoda };
                case 40: return new[] { EnemyKind.GiantBrute };

                // 第五章 三败高太尉：神火轰天车(41压轴登场) + 全精英轮番会战
                case 41: return new[] { EnemyKind.FireCart, EnemyKind.WaterChief };
                case 42: return new[] { EnemyKind.GoldenEagle, EnemyKind.FireCart };
                case 43: return new[] { EnemyKind.FireCart, EnemyKind.GoldenEagle };
                case 44: return new[] { EnemyKind.ChainHorse, EnemyKind.FireCart };
                case 45: return new[] { EnemyKind.FireCart };
                case 46: return new[] { EnemyKind.FireCart, EnemyKind.WaterChief };
                case 47: return new[] { EnemyKind.GiantBrute, EnemyKind.GoldenEagle };
                case 48: return new[] { EnemyKind.FireCart, EnemyKind.HighPriest };
                case 49: return new[] { EnemyKind.IronPagoda, EnemyKind.GiantBrute };
                default: return new[] { EnemyKind.FireCart };
            }
        }

        /// <summary>每关都会来的首领，按章节顺序：每章先章中首领，再章末首领。</summary>
        public static readonly EnemyKind[] BossOrder =
        {
            EnemyKind.HeTao, EnemyKind.Boss, EnemyKind.ZhuChaofeng, EnemyKind.Luan, EnemyKind.ZengKui,
            EnemyKind.ShiWen, EnemyKind.HanTao, EnemyKind.HuYan, EnemyKind.LiuMenglong, EnemyKind.GaoQiu,
        };

        public sealed class ChapterDef
        {
            public string Name, Bg, Intro;
            /// <summary>Boss 守第 10 关，MidBoss 守第 5 关。其余关按 BossOrder 轮换，血量仍沿用原来的章节奏。</summary>
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
            new ChapterDef { Name = "三打祝家庄", Bg = "battle_bg_ch2", Boss = EnemyKind.Luan, MidBoss = EnemyKind.ZhuChaofeng, Mix = Mix1, Elites = new[] { EnemyKind.Elite },
                Intro = "独龙冈上祝家庄，鹰隼盘旋",
                Levels = new[] { "独龙冈", "盘陀路", "白杨林", "扈家庄", "祝家店", "李家庄", "时迁偷鸡", "吊桥", "祝龙寨", "祝家庄" } },
            new ChapterDef { Name = "夜打曾头市", Bg = "battle_bg_ch3", Boss = EnemyKind.ShiWen, MidBoss = EnemyKind.ZengKui, Mix = Mix1, Elites = new[] { EnemyKind.Elite },
                Intro = "曾家五虎铁甲列阵",
                Levels = new[] { "青州道", "照夜玉狮", "法华寺", "曾家五虎", "曾头北寨", "凌州", "东平府", "东昌府", "曾家大营", "曾头市" } },
            new ChapterDef { Name = "大破连环马", Bg = "battle_bg_ch4", Boss = EnemyKind.HuYan, MidBoss = EnemyKind.HanTao, Mix = Mix1, Elites = new[] { EnemyKind.ChainHorse },
                Intro = "风雪中铁骑连环而来",
                Levels = new[] { "汝宁州", "高唐州", "雪夜", "钩镰枪", "韩滔营", "彭玘营", "金枪班", "徐宁", "连环阵", "呼延大营" } },
            new ChapterDef { Name = "三败高太尉", Bg = "battle_bg_ch5", Boss = EnemyKind.GaoQiu, MidBoss = EnemyKind.LiuMenglong, Mix = Mix1, Elites = new[] { EnemyKind.ChainHorse },
                Intro = "十节度兵马夜袭水泊",
                Levels = new[] { "济州城", "十节度", "王焕", "荆忠", "党世英", "水泊芦荡", "刘梦龙", "牛邦喜", "梁山水寨", "擒高俅" } },
        };

        /// <summary>首领关（逢5/10）第 4 波出首领、第 9 波出精英；普通关（非5倍数）不出大首领，第 4 波守关主精英 + 第 9 波压轴精英双节点挑大梁！</summary>
        static readonly int[] BossStageEliteWaves = { 9 };
        static readonly int[] NormalStageEliteWaves = { 4, 9 };
        public static float BossPace => Balance.Current.WaveStage.BossPace;
        static readonly Role[] BuffCycle = { Sim.Role.Mage, Sim.Role.Archer, Sim.Role.Fighter, Sim.Role.Bomber, Sim.Role.Control, Sim.Role.Archer, Sim.Role.Mage, Sim.Role.Fighter };

        /// <summary>
        /// 第 n 关（1 起）的敌人血量倍率（对标《永远的蔚蓝星球》关卡推进与卡点曲线）：
        /// 1. 开局第 1 关普通官兵约 175 血（1星射手需3箭、战士/炮手/法师需2击），彻底告别开场一刀秒；
        /// 2. 紧跟完整局外养成曲线 ExpectedPower(n) 并叠加关间压力斜率，每关扎实递进，逢 5/10 首领关设明确策略与养成试金石；
        /// 3. 不做局外养成在 3~4 关即明显受阻，落后 4 关养成在任意章节均无法平推。
        /// </summary>
        static float SmoothStagePower(int n)
        {
            float p0 = Meta.ExpectedPower(System.Math.Max(1, n - 1)) * Meta.ExpectedRoster(System.Math.Max(1, n - 1));
            float p1 = Meta.ExpectedPower(n) * Meta.ExpectedRoster(n);
            float p2 = Meta.ExpectedPower(System.Math.Min(50, n + 1)) * Meta.ExpectedRoster(System.Math.Min(50, n + 1));
            return n == 1 ? p1 : (0.25f * p0 + 0.50f * p1 + 0.25f * p2);
        }

        static float StageHpBase(int n)
        {
            var ws = Balance.Current.WaveStage;
            float raw = HpTune(n) * SmoothStagePower(n) * StageEscalation(n);
            int idx = (n - 1) % LevelsPerChapter + 1;
            if (idx == 5) raw *= 1.07f;
            else if (idx == 10) raw *= 1.12f;
            if (n <= 1) return raw;
            // 保证每关严格单调上升且前期斜率饱满（第1~5关每关至少+18%，第6~15关至少+12%，后续至少+7.5%紧咬养成）
            float prev = StageHpBase(n - 1);
            float minStep = n <= 5 ? ws.StageMinStep1To5 : n <= 15 ? ws.StageMinStep6To15 : ws.StageMinStep16Plus;
            return System.Math.Max(raw, prev * minStep);
        }

        public static float StageHp(int n) => StageHpBase(n) * Balance.Current.WaveStage.GlobalHpMul;

        static float HpTune(int n)
        {
            var ws = Balance.Current.WaveStage;
            return Curve(n, ws.HpTuneStages, ws.HpTuneValues);
        }

        static float StageEscalation(int n) => 1f + 0.26f * System.Math.Min(1f, (n - 1) / 5f) + 0.005f * (n - 1);

        static float Curve(int n, float[] xs, float[] ys)
        {
            if (n <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
                if (n <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (n - xs[i - 1]) / (xs[i] - xs[i - 1]);
            return ys[ys.Length - 1];
        }

        public static float StageAtk(int n)
        {
            var ws = Balance.Current.WaveStage;
            float a = Meta.ExpectedWall(n) * (1.08f + 0.020f * (n - 1)) * ws.GlobalAtkMul;
            // 第4关移速放慢后，白板号会靠多出来的走位时间滚起阵容。拆墙伤害略抬，让摸到寨墙的官军能把这关压回去。
            if (n == 4) a *= 1.22f;
            return a;
        }

        public static LevelDef[] Levels { get; private set; } = BuildLevels();
        public static int LevelCount => Levels.Length;
        public static LevelDef Level(int no) => Levels[System.Math.Max(1, System.Math.Min(no, Levels.Length)) - 1];

        static Defs()
        {
            Balance.InitWithCodeDefaults();
        }

        internal static void EnsureInitialized() { _ = Heroes.Length; }

        /// <summary>当 BalanceConfig 参数变更时重算四技能说明、派生词条表与 50 关关卡表。</summary>
        public static void RebuildDerived()
        {
            HeroKits.RefreshAll();
            Perks = BuildPerks();
            Levels = BuildLevels();
        }

        static LevelDef[] BuildLevels()
        {
            var ws = Balance.Current.WaveStage;
            var eco = Balance.Current.Economy;
            var list = new List<LevelDef>();
            for (int c = 0; c < Chapters.Length; c++)
            {
                var ch = Chapters[c];
                for (int i = 1; i <= LevelsPerChapter; i++)
                {
                    int n = c * LevelsPerChapter + i;
                    bool chBoss = i == 5 || i == 10;
                    // 对标《永远的蔚蓝星球》双轨节奏：
                    // - 逢 5 / 10 首领关：第 4 波守关大首领亲自压阵（BossWave = 4），第 9 波出精英副将；
                    // - 普通关（非 5 倍数）：不出大首领（BossWave = 0），第 4 波由守关主精英领军（血量与赏银对齐原第 4 波统领节奏，掉唯一生辰纲），第 9 波再出压轴精英！
                    var elites = StageElites(n);
                    // 首领移速提升至对标《蔚蓝星球》（第4波出→第5波到路中→第7波压墙）后，按真实交战窗口校准第4波首领/统领血量倍率
                    var oldKind = i == 10 ? ch.Boss : i >= 5 || c == 0 ? ch.MidBoss : Chapters[c - 1].Boss;
                    float oldMul = i == 10 ? (c == 0 ? 0.54f : c >= 4 ? 0.40f : c >= 3 ? 0.50f : 0.70f)
                        : i == 5 ? (c == 0 ? 0.38f : 0.58f)
                        : c == 0 ? 0.76f : 0.58f;
                    var boss = i == 10 ? ch.Boss : ch.MidBoss;
                    var wave4Unit = chBoss ? boss : elites[0];
                    float bossMul = wave4Unit == oldKind ? oldMul : Enemy(oldKind).Hp * oldMul / Enemy(wave4Unit).Hp;
                    var d = new LevelDef
                    {
                        No = n, Chapter = c + 1, Index = i, Name = ch.Levels[i - 1], Bg = ch.Bg,
                        Intro = chBoss ? $"首领 {Enemy(boss).Name} 亲至，守住寨墙" : n == 1 ? "击退十波官军，寨墙不能倒" : ch.Intro,
                        BossWave = chBoss ? ws.MainBossWave : 0,
                        RageWave = ws.MainRageWave,
                        EliteWaves = chBoss ? BossStageEliteWaves : NormalStageEliteWaves,
                        EliteKinds = elites, Troops = StageTroops(n),
                        BossKind = boss,
                        BossMul = bossMul,
                        HpMul = StageHp(n), AtkMul = StageAtk(n),
                        WallHp = ws.WallBaseHp,
                        StartCoins = eco.StartCoins,
                        CountMul = 1f + 0.006f * (n - 1) + (i == 10 ? 0.06f : 0f),
                    };
                    if (n > 1)
                    {
                        d.BuffRole = BuffCycle[(n * 3 + c) % BuffCycle.Length];
                        d.BuffPct = chBoss ? ws.StageBuffBoss : ws.StageBuffNormal;
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
            var ws = Balance.Current.WaveStage;
            var eco = Balance.Current.Economy;
            var d = new LevelDef
            {
                No = n, Chapter = b.Chapter, Index = b.Index, Name = "校场演武", Bg = b.Bg, Intro = "守满十波，夺校场头名",
                BossWave = ws.MainBossWave, RageWave = ws.MainRageWave,
                EliteWaves = BossStageEliteWaves, EliteKinds = new[] { b.EliteKinds[b.EliteKinds.Length - 1] }, Troops = b.Troops,
                BossKind = BossOrder[(day % BossOrder.Length + BossOrder.Length) % BossOrder.Length], BossMul = 1.15f,
                HpMul = b.HpMul * 1.08f, AtkMul = b.AtkMul, CountMul = b.CountMul + 0.08f,
                WallHp = ws.WallBaseHp, StartCoins = eco.StartCoins,
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
            var ws = Balance.Current.WaveStage;
            var eco = Balance.Current.Economy;
            return new LevelDef
            {
                No = n, Chapter = b.Chapter, Index = b.Index, Name = "黄泥冈劫纲", Bg = "battle_bg", Intro = "截下生辰纲，挑担越多赏越多",
                Waves = 6, BossWave = 0, RageWave = 5, EliteWaves = new[] { 4 }, PorterWaves = new[] { 1, 2, 3, 4, 5, 6 }, PorterCount = 3,
                EliteKinds = b.EliteKinds, Troops = Mix1, HpMul = b.HpMul * 0.9f, AtkMul = b.AtkMul, CountMul = 0.6f,
                WallHp = ws.WallBaseHp, StartCoins = eco.StartCoins,
                Mode = StageMode.Escort,
            };
        }

        /// <summary>每波的兵（不含精英、首领、生辰纲），按独立权重逐步混入，第1波纯基础步卒。</summary>
        public static List<EnemyKind> WaveTroops(LevelDef st, int wave, Rng rng)
        {
            var list = new List<EnemyKind>();
            int total = System.Math.Max(1, (int)System.Math.Round(WaveCount(wave) * st.CountMul));
            for (int i = 0; i < total; i++)
            {
                float r = rng.F(), acc = 0f;
                EnemyKind k = EnemyKind.Soldier;
                foreach (var m in st.Troops)
                {
                    if (wave < m.From) continue;
                    acc += m.Base + (wave - m.From) * m.Grow;
                    if (r < acc) { k = m.Kind; break; }
                }
                list.Add(k);
            }
            return list;
        }
    }
}

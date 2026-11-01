using System.Collections.Generic;

namespace Liangshan.Sim
{
    public enum HeroId { HuaRong, LingZhen, GongSun, WuSong, ZhangQing, LinChong, LuZhiShen, WuYong }
    public enum Role { Archer, Bomber, Mage, Fighter, Support }

    public sealed class HeroDef
    {
        public HeroId Id;
        public string Name, Nick, Role, Art;
        public Role Kind;
        public float Damage, Interval, Range;
        public string UltName, UltDesc, Ult;
        public float UltCd;
        /// <summary>普攻名、伤害属性、技能图标，点开好汉信息面板时展示。</summary>
        public string AtkName, Element, AtkIcon, UltIcon;
        /// <summary>2 蓝 3 紫 4 橙。</summary>
        public int Rarity;
        public string Desc;
        public bool Physical => Element == "物理";
        /// <summary>输出位：保底规则里只有辅助不算。</summary>
        public bool Carry => Kind != Sim.Role.Support;
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

    /// <summary>强化词条。Hero 为空表示全军通用。</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Desc;
        public HeroId? Hero;
        public int Max = 1;
        public float Weight = 1;
        public bool Ult;
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
        public static readonly HeroDef[] Heroes =
        {
            new HeroDef { Id = HeroId.HuaRong, Name = "花荣", Nick = "小李广", Role = "射手", Kind = Sim.Role.Archer, Art = "hero_huarong", Rarity = 3,
                Damage = 20, Interval = 0.75f, Range = 99, UltName = "百步穿杨", UltDesc = "火箭齐射，覆盖敌群最密处", UltCd = 11, Ult = "hr_ult",
                AtkName = "穿云箭", Element = "物理", AtkIcon = "ic_arrow", UltIcon = "fx_goldarrow", Desc = "射速快的单体射手，克制飞行敌人" },
            new HeroDef { Id = HeroId.LingZhen, Name = "凌振", Nick = "轰天雷", Role = "炮手", Kind = Sim.Role.Bomber, Art = "hero_lingzhen", Rarity = 3,
                Damage = 30, Interval = 1.55f, Range = 99, UltName = "轰天雷", UltDesc = "砸下一枚巨型火炮，大范围爆炸", UltCd = 13, Ult = "lz_ult",
                AtkName = "霹雳炮", Element = "火系", AtkIcon = "ic_bomb", UltIcon = "fx_fireball", Desc = "范围炮击，专清密集兵潮" },
            new HeroDef { Id = HeroId.GongSun, Name = "公孙胜", Nick = "入云龙", Role = "法师", Kind = Sim.Role.Mage, Art = "hero_gongsun", Rarity = 4,
                Damage = 24, Interval = 1.25f, Range = 99, UltName = "五雷天罡", UltDesc = "五道天雷劈向敌群", UltCd = 12, Ult = "gs_ult",
                AtkName = "引雷诀", Element = "雷系", AtkIcon = "fx_zap", UltIcon = "fx_lightning", Desc = "落雷连锁，克制重甲" },
            new HeroDef { Id = HeroId.WuSong, Name = "武松", Nick = "行者", Role = "战士", Kind = Sim.Role.Fighter, Art = "hero_wusong", Rarity = 3,
                Damage = 34, Interval = 0.7f, Range = 4.6f, UltName = "醉打", UltDesc = "三秒连环醉拳，击退墙前敌人", UltCd = 12, Ult = "ws_ult",
                AtkName = "铁拳", Element = "物理", AtkIcon = "ic_fist", UltIcon = "fx_shock", Desc = "近身拳击，击退贴墙的敌人" },
            new HeroDef { Id = HeroId.ZhangQing, Name = "张清", Nick = "没羽箭", Role = "射手", Kind = Sim.Role.Archer, Art = "hero_zhangqing", Rarity = 2,
                Damage = 18, Interval = 0.9f, Range = 99, UltName = "飞石连珠", UltDesc = "十二枚飞石连打，击晕敌人", UltCd = 12, Ult = "zq_ult",
                AtkName = "飞石", Element = "物理", AtkIcon = "fx_rock0", UltIcon = "fx_rock1", Desc = "飞石弹射多个敌人" },
            new HeroDef { Id = HeroId.LinChong, Name = "林冲", Nick = "豹子头", Role = "战士", Kind = Sim.Role.Fighter, Art = "hero_linchong", Rarity = 4,
                Damage = 38, Interval = 0.9f, Range = 6.2f, UltName = "风雪山神庙", UltDesc = "风雪冻住一片敌人并重创", UltCd = 13, Ult = "lc_ult",
                AtkName = "丈八蛇矛", Element = "物理", AtkIcon = "ic_sword", UltIcon = "fx_ice", Desc = "长枪贯穿一列，攻击距离远" },
            new HeroDef { Id = HeroId.LuZhiShen, Name = "鲁智深", Nick = "花和尚", Role = "战士", Kind = Sim.Role.Fighter, Art = "hero_luzhishen", Rarity = 3,
                Damage = 68, Interval = 1.3f, Range = 5.2f, UltName = "倒拔垂杨柳", UltDesc = "横扫墙前一整排敌人并击飞", UltCd = 14, Ult = "lzs_ult",
                AtkName = "禅杖", Element = "物理", AtkIcon = "fx_crack", UltIcon = "fx_wave", Desc = "禅杖砸地，大范围震退" },
            new HeroDef { Id = HeroId.WuYong, Name = "吴用", Nick = "智多星", Role = "军师", Kind = Sim.Role.Support, Art = "hero_wuyong", Rarity = 2,
                Damage = 12, Interval = 1.0f, Range = 99, UltName = "智取生辰纲", UltDesc = "全军攻速大增，并凭空得银两", UltCd = 15, Ult = "wy_ult",
                AtkName = "羽扇风", Element = "风系", AtkIcon = "fx_smoke", UltIcon = "ic_silver", Desc = "风刃减速，在场提升全军攻速" },
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];
        public static int HeroCount => Heroes.Length;

        public static string RoleName(Role r) => r == Sim.Role.Archer ? "射手" : r == Sim.Role.Bomber ? "炮手" : r == Sim.Role.Mage ? "法师" : r == Sim.Role.Fighter ? "战士" : "军师";

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
            E(EnemyKind.Porter, "生辰纲", "en_porter", 420, 0.5f, 0, 1.25f, 20, "挑着财宝，击杀可转老虎机"),
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

        public static readonly PerkDef[] Perks =
        {
            new PerkDef { Id = "hr_ult", Hero = HeroId.HuaRong, Name = "百步穿杨", Desc = "解锁大招：每 11 秒火箭齐射", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "hr_multi", Hero = HeroId.HuaRong, Name = "连珠箭", Desc = "每次多射 1 支箭\n单支伤害 -15%", Max = 2 },
            new PerkDef { Id = "hr_pierce", Hero = HeroId.HuaRong, Name = "穿云箭", Desc = "箭矢穿透 +1\n伤害 +10%", Max = 2 },
            new PerkDef { Id = "hr_crit", Hero = HeroId.HuaRong, Name = "神射", Desc = "25% 几率暴击\n暴击伤害 ×2.5" },
            new PerkDef { Id = "hr_rain", Hero = HeroId.HuaRong, Name = "箭如雨下", Desc = "大招火箭数 +6", Max = 2, Weight = 0.8f },

            new PerkDef { Id = "lz_ult", Hero = HeroId.LingZhen, Name = "轰天雷", Desc = "解锁大招：每 13 秒巨型火炮", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "lz_double", Hero = HeroId.LingZhen, Name = "双重炸弹", Desc = "扔出炸弹数 +1\n炸弹伤害 -20%", Max = 2 },
            new PerkDef { Id = "lz_split", Hero = HeroId.LingZhen, Name = "分裂炸弹", Desc = "爆炸后分裂 3 枚\n小炸弹" },
            new PerkDef { Id = "lz_big", Hero = HeroId.LingZhen, Name = "火药加倍", Desc = "爆炸范围 +35%\n伤害 +15%", Max = 2 },

            new PerkDef { Id = "gs_ult", Hero = HeroId.GongSun, Name = "五雷天罡", Desc = "解锁大招：每 12 秒五道天雷", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "gs_chain", Hero = HeroId.GongSun, Name = "雷链", Desc = "落雷连锁 +2 个敌人", Max = 2 },
            new PerkDef { Id = "gs_stun", Hero = HeroId.GongSun, Name = "掌心雷", Desc = "落雷麻痹敌人 0.8 秒" },
            new PerkDef { Id = "gs_ice", Hero = HeroId.GongSun, Name = "呼风唤雪", Desc = "攻击附带冰冻\n敌人减速 45%" },

            new PerkDef { Id = "ws_ult", Hero = HeroId.WuSong, Name = "醉打", Desc = "解锁大招：每 12 秒连环醉拳", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "ws_fast", Hero = HeroId.WuSong, Name = "三碗不过冈", Desc = "攻速 +45%", Max = 2 },
            new PerkDef { Id = "ws_tiger", Hero = HeroId.WuSong, Name = "打虎", Desc = "对精英、首领\n伤害 ×2.5" },
            new PerkDef { Id = "ws_quake", Hero = HeroId.WuSong, Name = "玉环步", Desc = "拳风范围 +50%\n击退加强" },

            new PerkDef { Id = "zq_ult", Hero = HeroId.ZhangQing, Name = "飞石连珠", Desc = "解锁大招：每 12 秒十二连石", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "zq_bounce", Hero = HeroId.ZhangQing, Name = "连环飞石", Desc = "飞石弹射 +2 次", Max = 2 },
            new PerkDef { Id = "zq_stun", Hero = HeroId.ZhangQing, Name = "石破天惊", Desc = "30% 几率击晕 1 秒" },
            new PerkDef { Id = "zq_big", Hero = HeroId.ZhangQing, Name = "飞蝗石", Desc = "飞石伤害 +25%", Max = 2 },

            new PerkDef { Id = "lc_ult", Hero = HeroId.LinChong, Name = "风雪山神庙", Desc = "解锁大招：每 13 秒风雪冰封", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "lc_reach", Hero = HeroId.LinChong, Name = "丈八蛇矛", Desc = "攻击距离 +1.5\n伤害 +10%", Max = 2 },
            new PerkDef { Id = "lc_crit", Hero = HeroId.LinChong, Name = "豹子头", Desc = "30% 几率暴击\n暴击伤害 ×2.5" },
            new PerkDef { Id = "lc_frost", Hero = HeroId.LinChong, Name = "林教头", Desc = "枪尖带寒气\n命中减速" },

            new PerkDef { Id = "lzs_ult", Hero = HeroId.LuZhiShen, Name = "倒拔垂杨柳", Desc = "解锁大招：每 14 秒横扫墙前", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "lzs_quake", Hero = HeroId.LuZhiShen, Name = "禅杖横扫", Desc = "震地范围 +40%", Max = 2 },
            new PerkDef { Id = "lzs_rage", Hero = HeroId.LuZhiShen, Name = "大闹五台山", Desc = "攻速 +40%", Max = 2 },
            new PerkDef { Id = "lzs_boss", Hero = HeroId.LuZhiShen, Name = "拳打镇关西", Desc = "对精英、首领\n伤害 ×2.5" },

            new PerkDef { Id = "wy_ult", Hero = HeroId.WuYong, Name = "智取生辰纲", Desc = "解锁大招：每 15 秒全军急攻", Ult = true, Weight = 2.2f },
            new PerkDef { Id = "wy_aura", Hero = HeroId.WuYong, Name = "运筹帷幄", Desc = "在场攻速光环翻倍" },
            new PerkDef { Id = "wy_coin", Hero = HeroId.WuYong, Name = "智多星", Desc = "每次合成额外\n获得 15 银两" },
            new PerkDef { Id = "wy_wind", Hero = HeroId.WuYong, Name = "羽扇纶巾", Desc = "风刃伤害 +60%\n减速范围扩大", Max = 2 },

            new PerkDef { Id = "all_speed", Name = "擂鼓助威", Desc = "全军攻速 +12%", Max = 3, Weight = 0.7f },
            new PerkDef { Id = "all_dmg", Name = "替天行道", Desc = "全军伤害 +15%", Max = 3, Weight = 0.7f },
            new PerkDef { Id = "coin", Name = "劫富济贫", Desc = "击杀掉落银两 +30%", Max = 2, Weight = 0.6f },
            new PerkDef { Id = "wall", Name = "加固寨墙", Desc = "寨墙回复 1500\n上限 +1000", Max = 2, Weight = 0.6f },
        };

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
        static readonly int[][] PorterSets = { new[] { 3, 5, 7 }, new[] { 2, 5, 8 }, new[] { 4, 6, 9 }, new[] { 3, 6, 8 }, new[] { 2, 6, 9 } };
        static readonly Role[] BuffCycle = { Sim.Role.Mage, Sim.Role.Archer, Sim.Role.Fighter, Sim.Role.Bomber, Sim.Role.Archer, Sim.Role.Mage, Sim.Role.Fighter };

        /// <summary>第 n 关（1 起）的敌人血量倍率：跟着正常推进时的养成战力走，再逐关加压，落后养成就过不去。</summary>
        public static float StageHp(int n) => Meta.ExpectedPower(n) * (1f + 0.35f * System.Math.Min(1f, (n - 1) / 12f) + 0.009f * (n - 1) + 0.00015f * (n - 1) * (n - 1));
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
                        PorterWaves = PorterSets[(n - 1) % PorterSets.Length],
                        EliteKinds = ch.Elites, Troops = ch.Mix,
                        BossKind = chBoss || c == 0 ? ch.Boss : Chapters[c - 1].Boss,
                        BossMul = i == 10 ? 1.0f : i == 5 ? 0.9f : c == 0 ? 1f : 0.7f,
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
                EliteWaves = new[] { 2, 4, 6, 8, 9 }, PorterWaves = new[] { 3, 7 }, EliteKinds = b.EliteKinds, Troops = b.Troops,
                BossKind = Chapters[(day % Chapters.Length + Chapters.Length) % Chapters.Length].Boss, BossMul = 1.2f,
                HpMul = b.HpMul * 1.1f, AtkMul = b.AtkMul, CountMul = b.CountMul + 0.1f,
                BuffRole = BuffCycle[(day % BuffCycle.Length + BuffCycle.Length) % BuffCycle.Length], BuffPct = 0.6f,
                Mode = StageMode.Daily,
            };
            return d;
        }

        /// <summary>智取生辰纲：六波，每波一串挑担的，打得越多铜钱越多。</summary>
        public static LevelDef EscortStage(int cleared)
        {
            int n = System.Math.Max(1, System.Math.Min(LevelCount, cleared));
            var b = Level(n);
            return new LevelDef
            {
                No = n, Chapter = b.Chapter, Index = b.Index, Name = "智取生辰纲", Bg = "battle_bg", Intro = "截下生辰纲，挑担越多赏越多",
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

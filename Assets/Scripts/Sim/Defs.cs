using System.Collections.Generic;

namespace Liangshan.Sim
{
    public enum HeroId { HuaRong, LingZhen, GongSun, WuSong }
    public enum Role { Archer, Bomber, Mage, Fighter }

    public sealed class HeroDef
    {
        public HeroId Id;
        public string Name, Nick, Role, Art;
        public Role Kind;
        public float Damage, Interval, Range;
        public string UltName, UltDesc;
        public float UltCd;
        /// <summary>普攻名、伤害属性、技能图标，点开好汉信息面板时展示。</summary>
        public string AtkName, Element, AtkIcon, UltIcon;
    }

    /// <summary>一关的配置。加新关只需要往 Defs.Levels 里追加。</summary>
    public sealed class LevelDef
    {
        public int No;
        public string Name, Intro, Bg = "battle_bg";
        public int Waves = 10, BossWave = 4, RageWave = 8;
        public int[] EliteWaves = { }, PorterWaves = { };
        public float WallHp = 6000;
        public int StartCoins = 100;
        public string Title => $"第{No}关 · {Name}";
        public string BossName => Defs.Enemy(EnemyKind.Boss).Name;
    }

    public enum EnemyKind { Soldier, Shield, Cavalry, Crossbow, Elite, Porter, Boss }

    public sealed class EnemyDef
    {
        public EnemyKind Kind;
        public string Name, Art;
        public float Hp, Speed, WallDps, Size;
        public int Coin;
        /// <summary>远程兵停在这条线上放箭，不贴墙。</summary>
        public float StopY;
        public bool Big => Kind == EnemyKind.Elite || Kind == EnemyKind.Boss;
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
            new HeroDef { Id = HeroId.HuaRong, Name = "花荣", Nick = "小李广", Role = "射手", Kind = Sim.Role.Archer, Art = "hero_huarong",
                Damage = 20, Interval = 0.75f, Range = 99, UltName = "百步穿杨", UltDesc = "火箭齐射，覆盖敌群最密处", UltCd = 11,
                AtkName = "穿云箭", Element = "物理", AtkIcon = "ic_arrow", UltIcon = "fx_goldarrow" },
            new HeroDef { Id = HeroId.LingZhen, Name = "凌振", Nick = "轰天雷", Role = "炮手", Kind = Sim.Role.Bomber, Art = "hero_lingzhen",
                Damage = 30, Interval = 1.55f, Range = 99, UltName = "轰天雷", UltDesc = "砸下一枚巨型火炮，大范围爆炸", UltCd = 13,
                AtkName = "霹雳炮", Element = "火系", AtkIcon = "ic_bomb", UltIcon = "fx_fireball" },
            new HeroDef { Id = HeroId.GongSun, Name = "公孙胜", Nick = "入云龙", Role = "法师", Kind = Sim.Role.Mage, Art = "hero_gongsun",
                Damage = 24, Interval = 1.25f, Range = 99, UltName = "五雷天罡", UltDesc = "五道天雷劈向敌群", UltCd = 12,
                AtkName = "引雷诀", Element = "雷系", AtkIcon = "fx_zap", UltIcon = "fx_lightning" },
            new HeroDef { Id = HeroId.WuSong, Name = "武松", Nick = "行者", Role = "战士", Kind = Sim.Role.Fighter, Art = "hero_wusong",
                Damage = 34, Interval = 0.7f, Range = 4.6f, UltName = "醉打", UltDesc = "三秒连环醉拳，击退墙前敌人", UltCd = 12,
                AtkName = "铁拳", Element = "物理", AtkIcon = "ic_fist", UltIcon = "fx_shock" },
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];

        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Kind = EnemyKind.Soldier, Name = "官兵", Art = "en_soldier", Hp = 30, Speed = 0.62f, WallDps = 11, Size = 0.95f, Coin = 2, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.Shield, Name = "盾兵", Art = "en_shield", Hp = 110, Speed = 0.42f, WallDps = 17, Size = 1.15f, Coin = 4, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.Cavalry, Name = "骑兵", Art = "en_cavalry", Hp = 26, Speed = 1.0f, WallDps = 9, Size = 1.15f, Coin = 3, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.Crossbow, Name = "弩手", Art = "en_crossbow", Hp = 28, Speed = 0.58f, WallDps = 8, Size = 0.95f, Coin = 3, StopY = 0.6f },
            new EnemyDef { Kind = EnemyKind.Elite, Name = "教头", Art = "en_elite", Hp = 1300, Speed = 0.36f, WallDps = 70, Size = 1.6f, Coin = 45, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.Porter, Name = "生辰纲", Art = "en_porter", Hp = 420, Speed = 0.5f, WallDps = 0, Size = 1.25f, Coin = 20, StopY = Layout.StopY },
            new EnemyDef { Kind = EnemyKind.Boss, Name = "黄安", Art = "en_boss", Hp = 9000, Speed = 0.17f, WallDps = 200, Size = 2.6f, Coin = 200, StopY = Layout.StopY },
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

        public static readonly LevelDef[] Levels =
        {
            new LevelDef { No = 1, Name = "金沙滩", Intro = "击退十波官军，寨墙不能倒",
                Waves = 10, BossWave = 4, RageWave = 8, EliteWaves = new[] { 2, 6, 9 }, PorterWaves = new[] { 3, 5, 7 } },
        };

        public static int SummonCost(int n) => 30 + n * 10 + (n * n) / 4;
        public static int EnhanceCost(int n) => (int)(60 * System.Math.Pow(1.7, n) / 5) * 5;
        public static float HpScale(int wave) => 1f + 0.13f * (wave - 1);

        /// <summary>每波的兵（不含精英、首领、生辰纲），按出场顺序。</summary>
        public static List<EnemyKind> WaveTroops(int wave, Rng rng)
        {
            var list = new List<EnemyKind>();
            int total = 18 + (int)((wave - 1) * 14.4f);
            for (int i = 0; i < total; i++)
            {
                float r = rng.F();
                EnemyKind k = EnemyKind.Soldier;
                if (wave >= 2 && r < 0.10f + wave * 0.012f) k = EnemyKind.Cavalry;
                else if (wave >= 3 && r < 0.20f + wave * 0.022f) k = EnemyKind.Shield;
                else if (wave >= 4 && r < 0.27f + wave * 0.028f) k = EnemyKind.Crossbow;
                list.Add(k);
            }
            return list;
        }
    }
}

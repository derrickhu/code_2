using System;
using System.Collections.Generic;

namespace Liangshan.Sim
{
    /// <summary>局外养成折算成一局战斗里的加成。</summary>
    public sealed class Loadout
    {
        public HeroId[] Lineup;
        public float[] HeroDmg, HeroSpeed, HeroUltCd;
        public float AllDmg = 1, AllSpeed = 1, WallMul = 1, WallResist, UltCd = 1;
        public int StartCoins;
        public readonly List<string> StartPerks = new List<string>();

        public static Loadout Plain(HeroId[] lineup)
        {
            var lo = new Loadout { Lineup = lineup };
            int n = Defs.HeroCount;
            lo.HeroDmg = new float[n];
            lo.HeroSpeed = new float[n];
            lo.HeroUltCd = new float[n];
            for (int i = 0; i < n; i++) lo.HeroDmg[i] = lo.HeroSpeed[i] = lo.HeroUltCd[i] = 1f;
            return lo;
        }
    }

    /// <summary>存档。字段全公开，方便 JsonUtility 读写。</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 1;
        public int Ingots, Copper, Iron, Tokens;
        public int Stamina = Meta.StaminaMax;
        public long StaminaAt;
        public int[] HeroLv = new int[0], Shards = new int[0];
        public int[] Lineup = { 0, 1, 2, 3 };
        public int Cleared, Selected = 1;
        public int[] Stars = new int[0];
        public int[] EquipLv = new int[0];
        public int RecruitLv = 1, RecruitExp, Pity, Pulls;
        /// <summary>图鉴：0 未遇到 1 已遇到 2 已领奖。</summary>
        public int[] Seen = new int[0];
        public int[] ChestClaimed = new int[0];
        public int Day;
        public int[] Task = new int[0];
        public int TaskClaimed, AdIngots, AdStamina, AdRecruit, AdIdle, DailyTries, EscortTries, ShopStamina;
        public int SignDays, SignDay;
        public long IdleAt;
        public int Speed15, TutorialDone, Wins, Battles;
        public int Sound = 1, Music = 1;
    }

    public enum Cur { Ingot, Copper, Iron, Token, Stamina, Shard }

    public struct Item
    {
        public Cur Cur;
        public int N;
        public HeroId Hero;
        public Item(Cur c, int n, HeroId h = HeroId.HuaRong) { Cur = c; N = n; Hero = h; }
    }

    public sealed class Rewards
    {
        public readonly List<Item> Items = new List<Item>();
        public HeroId? Unlock;
        public int Stars;
        public bool First;

        public void Add(Cur c, int n, HeroId h = HeroId.HuaRong)
        {
            if (n <= 0) return;
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Cur == c && (c != Cur.Shard || Items[i].Hero == h)) { Items[i] = new Item(c, Items[i].N + n, h); return; }
            Items.Add(new Item(c, n, h));
        }

        public int Get(Cur c)
        {
            int n = 0;
            foreach (var it in Items) if (it.Cur == c) n += it.N;
            return n;
        }
    }

    public struct Pull
    {
        public HeroId Hero;
        public int Shards;
        public bool New;
    }

    public sealed class EquipDef
    {
        public string Name, Icon, Stat;
        public float Per, Cap;
        public bool Pct = true;
    }

    public sealed class TaskDef
    {
        public string Name;
        public int Need;
        public Item Reward;
    }

    public static class Meta
    {
        public const int MaxHeroLv = 30;
        public const int StaminaMax = 60;
        public const int StaminaSec = 300;
        public const int BattleStamina = 5;
        public const int AdIngotsMax = 5, AdStaminaMax = 3, AdRecruitMax = 3, AdIdleMax = 3, DailyTriesMax = 2, EscortTriesMax = 2, ShopStaminaMax = 2;
        public const int TalentStep = 5;
        public const float GlobalPerLv = 0.003f;
        public const int IdleCapHours = 8;

        // ---------------- 存档 ----------------

        public static SaveData NewSave(long now)
        {
            var s = new SaveData { Ingots = 300, Copper = 1500, Tokens = 5, StaminaAt = now, IdleAt = now };
            Normalize(s);
            for (int h = 0; h < 4; h++) s.HeroLv[h] = 1;
            return s;
        }

        static int[] Fit(int[] a, int n)
        {
            if (a != null && a.Length == n) return a;
            var r = new int[n];
            if (a != null) Array.Copy(a, r, Math.Min(n, a.Length));
            return r;
        }

        /// <summary>老存档补齐数组长度，加了新好汉、新关卡也能读。</summary>
        public static void Normalize(SaveData s)
        {
            s.HeroLv = Fit(s.HeroLv, Defs.HeroCount);
            s.Shards = Fit(s.Shards, Defs.HeroCount);
            s.Stars = Fit(s.Stars, Defs.LevelCount);
            s.EquipLv = Fit(s.EquipLv, Equips.Length);
            s.Seen = Fit(s.Seen, Defs.Enemies.Length);
            s.ChestClaimed = Fit(s.ChestClaimed, Defs.Chapters.Length);
            s.Task = Fit(s.Task, Tasks.Length);
            if (s.Lineup == null || s.Lineup.Length != 4) s.Lineup = new[] { 0, 1, 2, 3 };
            s.Selected = Math.Max(1, Math.Min(s.Selected, Math.Min(Defs.LevelCount, s.Cleared + 1)));
            if (s.RecruitLv < 1) s.RecruitLv = 1;
        }

        public static void RollDay(SaveData s, int day)
        {
            if (s.Day == day) return;
            s.Day = day;
            for (int i = 0; i < s.Task.Length; i++) s.Task[i] = 0;
            s.TaskClaimed = 0;
            s.AdIngots = s.AdStamina = s.AdRecruit = s.AdIdle = s.DailyTries = s.EscortTries = s.ShopStamina = 0;
        }

        public static void RegenStamina(SaveData s, long now)
        {
            if (s.Stamina >= StaminaMax) { s.StaminaAt = now; return; }
            long ticks = (now - s.StaminaAt) / StaminaSec;
            if (ticks <= 0) return;
            s.Stamina = (int)Math.Min(StaminaMax, s.Stamina + ticks);
            s.StaminaAt = s.Stamina >= StaminaMax ? now : s.StaminaAt + ticks * StaminaSec;
        }

        public static int StaminaWait(SaveData s, long now) => s.Stamina >= StaminaMax ? 0 : (int)(StaminaSec - (now - s.StaminaAt) % StaminaSec);

        public static int Have(SaveData s, Cur c) => c == Cur.Ingot ? s.Ingots : c == Cur.Copper ? s.Copper : c == Cur.Iron ? s.Iron : c == Cur.Token ? s.Tokens : c == Cur.Stamina ? s.Stamina : 0;

        public static void Give(SaveData s, Item it)
        {
            switch (it.Cur)
            {
                case Cur.Ingot: s.Ingots += it.N; break;
                case Cur.Copper: s.Copper += it.N; break;
                case Cur.Iron: s.Iron += it.N; break;
                case Cur.Token: s.Tokens += it.N; break;
                case Cur.Stamina: s.Stamina += it.N; break;
                case Cur.Shard: s.Shards[(int)it.Hero] += it.N; break;
            }
        }

        public static void Give(SaveData s, Rewards r, int mul = 1)
        {
            foreach (var it in r.Items) Give(s, new Item(it.Cur, it.N * mul, it.Hero));
            if (r.Unlock.HasValue && s.HeroLv[(int)r.Unlock.Value] == 0) s.HeroLv[(int)r.Unlock.Value] = 1;
        }

        public static bool Spend(SaveData s, Cur c, int n)
        {
            if (Have(s, c) < n) return false;
            Give(s, new Item(c, -n));
            return true;
        }

        public static HeroId[] Lineup(SaveData s)
        {
            var r = new HeroId[s.Lineup.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (HeroId)s.Lineup[i];
            return r;
        }

        // ---------------- 好汉 ----------------

        public static bool Owned(SaveData s, HeroId h) => s.HeroLv[(int)h] > 0;
        public static int ShardCost(int lv) => 2 + lv;
        public static int CopperCost(int lv) => 100 + lv * 60 + lv * lv * 22;
        public static int DupShards(int rarity) => rarity >= 4 ? 10 : rarity == 3 ? 6 : 4;

        public static bool CanLevel(SaveData s, HeroId h)
        {
            int lv = s.HeroLv[(int)h];
            return lv > 0 && lv < MaxHeroLv && s.Shards[(int)h] >= ShardCost(lv) && s.Copper >= CopperCost(lv);
        }

        public static bool LevelUp(SaveData s, HeroId h)
        {
            if (!CanLevel(s, h)) return false;
            int lv = s.HeroLv[(int)h];
            s.Shards[(int)h] -= ShardCost(lv);
            s.Copper -= CopperCost(lv);
            s.HeroLv[(int)h] = lv + 1;
            Progress(s, 2);
            return true;
        }

        public static float LevelDmg(int lv) => 1f + 0.12f * (Math.Max(1, lv) - 1);

        public static float HeroDmg(int lv)
        {
            float m = LevelDmg(lv);
            if (lv >= 10) m *= 1.2f;
            if (lv >= 25) m *= 1.3f;
            return m;
        }

        /// <summary>每升一级给全军加的伤害，所有好汉一起算。</summary>
        public static float GlobalDmg(SaveData s)
        {
            int sum = 0;
            foreach (var lv in s.HeroLv) if (lv > 1) sum += lv - 1;
            return sum * GlobalPerLv;
        }

        public static string FirstPerk(HeroId h)
        {
            foreach (var p in Defs.Perks)
                if (p.Hero == h && !p.Ult) return p.Id;
            return null;
        }

        /// <summary>第 i 个天赋（0..5），在 5、10…30 级解锁。</summary>
        public static string Talent(HeroId h, int i)
        {
            switch (i)
            {
                case 0: return $"开局自带「{Defs.Perk(FirstPerk(h)).Name}」";
                case 1: return "伤害 +20%";
                case 2: return "大招冷却 -15%";
                case 3: return $"开局自带大招「{Defs.Hero(h).UltName}」";
                case 4: return "伤害 +30%";
                default: return "攻速 +20%";
            }
        }

        public static int HeroPower(SaveData s, HeroId h)
        {
            var d = Defs.Hero(h);
            int lv = Math.Max(1, s.HeroLv[(int)h]);
            return (int)(d.Damage / d.Interval * HeroDmg(lv) * (lv >= 30 ? 1.2f : 1f) * 10);
        }

        public static int TeamPower(SaveData s)
        {
            var lo = BuildLoadout(s);
            float p = 0;
            foreach (var h in lo.Lineup)
            {
                var d = Defs.Hero(h);
                p += d.Damage / d.Interval * lo.HeroDmg[(int)h] * lo.HeroSpeed[(int)h];
            }
            return (int)(p * lo.AllDmg * lo.AllSpeed * 10 + lo.WallMul * 6000 / 20);
        }

        // ---------------- 装备（寨主宋江） ----------------

        public static readonly EquipDef[] Equips =
        {
            new EquipDef { Name = "佩剑", Icon = "eq_weapon", Stat = "全军伤害", Per = 0.04f, Cap = 9 },
            new EquipDef { Name = "头盔", Icon = "eq_helmet", Stat = "寨墙生命", Per = 0.05f, Cap = 9 },
            new EquipDef { Name = "铠甲", Icon = "eq_armor", Stat = "寨墙减伤", Per = 0.015f, Cap = 0.45f },
            new EquipDef { Name = "战靴", Icon = "eq_boots", Stat = "全军攻速", Per = 0.025f, Cap = 9 },
            new EquipDef { Name = "玉佩", Icon = "eq_amulet", Stat = "开局银两", Per = 8, Cap = 999, Pct = false },
            new EquipDef { Name = "号角", Icon = "eq_horn", Stat = "大招冷却", Per = 0.015f, Cap = 0.4f },
        };

        public static int EquipMax(SaveData s) => Math.Min(40, 5 + s.Cleared * 7 / 10);
        public static int IronCost(int lv) => 3 + lv * 2;
        public static int EquipCopper(int lv) => 150 + lv * 80 + lv * lv * 10;

        /// <summary>每 10 级一次突破，突破后每级加成提高一成。</summary>
        public static float EquipValue(int slot, int lv)
        {
            var d = Equips[slot];
            float v = 0;
            for (int i = 1; i <= lv; i++) v += d.Per * (1f + 0.1f * ((i - 1) / 10));
            return Math.Min(d.Cap, v);
        }

        public static string EquipText(int slot, int lv)
        {
            var d = Equips[slot];
            float v = EquipValue(slot, lv);
            string sign = slot == 2 || slot == 5 ? "-" : "+";
            return d.Pct ? $"{d.Stat} {sign}{v * 100:0.#}%" : $"{d.Stat} +{v:0}";
        }

        public static bool CanForge(SaveData s, int slot)
        {
            int lv = s.EquipLv[slot];
            return lv < EquipMax(s) && s.Iron >= IronCost(lv) && s.Copper >= EquipCopper(lv);
        }

        public static bool Forge(SaveData s, int slot)
        {
            if (!CanForge(s, slot)) return false;
            int lv = s.EquipLv[slot];
            s.Iron -= IronCost(lv);
            s.Copper -= EquipCopper(lv);
            s.EquipLv[slot] = lv + 1;
            Progress(s, 3);
            return true;
        }

        public static Loadout BuildLoadout(SaveData s, HeroId[] lineup = null)
        {
            var lo = Loadout.Plain(lineup ?? Lineup(s));
            for (int h = 0; h < Defs.HeroCount; h++)
            {
                int lv = Math.Max(1, s.HeroLv[h]);
                lo.HeroDmg[h] = HeroDmg(lv);
                lo.HeroSpeed[h] = lv >= 30 ? 1.2f : 1f;
                lo.HeroUltCd[h] = lv >= 15 ? 0.85f : 1f;
            }
            lo.AllDmg = (1f + GlobalDmg(s)) * (1f + EquipValue(0, s.EquipLv[0]));
            lo.WallMul = 1f + EquipValue(1, s.EquipLv[1]);
            lo.WallResist = EquipValue(2, s.EquipLv[2]);
            lo.AllSpeed = 1f + EquipValue(3, s.EquipLv[3]);
            lo.StartCoins = (int)EquipValue(4, s.EquipLv[4]);
            lo.UltCd = 1f - EquipValue(5, s.EquipLv[5]);
            foreach (var h in lo.Lineup)
            {
                int lv = s.HeroLv[(int)h];
                if (lv >= 5) lo.StartPerks.Add(FirstPerk(h));
                if (lv >= 20) lo.StartPerks.Add(Defs.Hero(h).Ult);
            }
            return lo;
        }

        // ---------------- 关卡 ----------------

        public static int StarsFor(float wallPct) => wallPct >= 0.7f ? 3 : wallPct >= 0.35f ? 2 : 1;

        /// <summary>首通送好汉：前几章各解锁一位新人。</summary>
        public static HeroId? UnlockAt(int level) =>
            level == 4 ? HeroId.ZhangQing : level == 8 ? HeroId.WuYong : level == 15 ? HeroId.LuZhiShen : level == 25 ? HeroId.LinChong : (HeroId?)null;

        public static Rewards BattleRewards(SaveData s, LevelDef st, bool win, int wave, float wallPct, int porters, Rng rng)
        {
            var r = new Rewards();
            int n = st.No;
            if (st.Mode == StageMode.Escort)
            {
                r.Add(Cur.Copper, porters * (60 + 15 * n));
                r.Add(Cur.Iron, porters / 3);
                return r;
            }
            if (st.Mode == StageMode.Daily)
            {
                float k = win ? 1f : Math.Max(0, wave - 1) / (float)st.Waves;
                r.Add(Cur.Token, win ? 3 : (int)(3 * k));
                r.Add(Cur.Iron, (int)((12 + n / 2) * k));
                r.Add(Cur.Ingot, win ? 60 : 0);
                return r;
            }
            if (!win)
            {
                float k = Math.Max(0, wave - 1) / (float)st.Waves * 0.5f;
                r.Add(Cur.Copper, (int)((300 + 70 * n) * k));
                return r;
            }
            r.Stars = StarsFor(wallPct);
            r.First = n > s.Cleared;
            r.Add(Cur.Copper, 300 + 70 * n);
            r.Add(Cur.Iron, 2 + n / 3);
            var lineup = Lineup(s);
            var h = lineup[rng.Int(lineup.Length)];
            r.Add(Cur.Shard, 2 + st.Chapter, h);
            if (r.First)
            {
                r.Add(Cur.Ingot, st.IsChapterBoss ? 150 : 50);
                r.Add(Cur.Token, st.IsChapterBoss ? 3 : 1);
                r.Unlock = UnlockAt(n);
            }
            return r;
        }

        /// <summary>结算一局主线：记星、推进度、解锁倍速。</summary>
        public static void FinishMain(SaveData s, LevelDef st, bool win, Rewards r)
        {
            s.Battles++;
            Progress(s, 0);
            if (!win) return;
            s.Wins++;
            Progress(s, 1);
            if (st.Mode != StageMode.Main) return;
            int i = st.No - 1;
            s.Stars[i] = Math.Max(s.Stars[i], r.Stars);
            if (st.No > s.Cleared)
            {
                s.Cleared = st.No;
                s.Selected = Math.Min(Defs.LevelCount, st.No + 1);
            }
            s.Speed15 = 1;
        }

        public static int ChapterStars(SaveData s, int chapter)
        {
            int sum = 0;
            for (int i = 0; i < Defs.LevelsPerChapter; i++) sum += s.Stars[(chapter - 1) * Defs.LevelsPerChapter + i];
            return sum;
        }

        public static readonly int[] ChestStars = { 10, 20, 30 };

        public static Rewards ChestReward(int chapter, int k)
        {
            var r = new Rewards();
            r.Add(Cur.Ingot, 80 + 60 * k + 20 * chapter);
            r.Add(Cur.Token, 1 + k);
            r.Add(Cur.Iron, 10 * (k + 1) + 5 * chapter);
            return r;
        }

        public static bool CanOpenChest(SaveData s, int chapter, int k) =>
            (s.ChestClaimed[chapter - 1] & (1 << k)) == 0 && ChapterStars(s, chapter) >= ChestStars[k];

        public static Rewards OpenChest(SaveData s, int chapter, int k)
        {
            if (!CanOpenChest(s, chapter, k)) return null;
            s.ChestClaimed[chapter - 1] |= 1 << k;
            var r = ChestReward(chapter, k);
            Give(s, r);
            return r;
        }

        // ---------------- 擂鼓招募 ----------------

        public static int RecruitNeed(int lv) => 10 + lv * 10;
        public const int RecruitMaxLv = 10;

        public static float OrangeRate(SaveData s) => 0.05f + 0.01f * (s.RecruitLv - 1);
        public static float PurpleRate(SaveData s) => 0.30f + 0.01f * (s.RecruitLv - 1);
        public const int OrangePity = 40;

        static HeroId PickOf(int rarity, Rng rng)
        {
            var pool = new List<HeroId>();
            foreach (var d in Defs.Heroes) if (d.Rarity == rarity) pool.Add(d.Id);
            return pool[rng.Int(pool.Count)];
        }

        /// <summary>抽 n 次：十连保底一个紫，四十抽保底橙。没拥有的好汉直接入伙，重复的折成碎片。</summary>
        public static List<Pull> Recruit(SaveData s, int n, Rng rng)
        {
            var res = new List<Pull>();
            bool purple = false;
            for (int i = 0; i < n; i++)
            {
                s.Pity++;
                s.Pulls++;
                float r = rng.F();
                int rar = r < OrangeRate(s) ? 4 : r < OrangeRate(s) + PurpleRate(s) ? 3 : 2;
                if (s.Pity >= OrangePity) rar = 4;
                if (n >= 10 && i == n - 1 && !purple && rar < 3) rar = 3;
                if (rar >= 3) purple = true;
                if (rar == 4) s.Pity = 0;
                var h = PickOf(rar, rng);
                var p = new Pull { Hero = h };
                if (s.HeroLv[(int)h] == 0)
                {
                    s.HeroLv[(int)h] = 1;
                    p.New = true;
                }
                else
                {
                    p.Shards = DupShards(rar);
                    s.Shards[(int)h] += p.Shards;
                }
                res.Add(p);
                s.RecruitExp++;
                while (s.RecruitLv < RecruitMaxLv && s.RecruitExp >= RecruitNeed(s.RecruitLv))
                {
                    s.RecruitExp -= RecruitNeed(s.RecruitLv);
                    s.RecruitLv++;
                }
            }
            Progress(s, 5, n);
            return res;
        }

        // ---------------- 每日任务、签到、巡山 ----------------

        public static readonly TaskDef[] Tasks =
        {
            new TaskDef { Name = "出征 3 次", Need = 3, Reward = new Item(Cur.Ingot, 50) },
            new TaskDef { Name = "获胜 2 次", Need = 2, Reward = new Item(Cur.Token, 2) },
            new TaskDef { Name = "升级好汉 1 次", Need = 1, Reward = new Item(Cur.Copper, 1500) },
            new TaskDef { Name = "强化装备 2 次", Need = 2, Reward = new Item(Cur.Iron, 15) },
            new TaskDef { Name = "观看广告 3 次", Need = 3, Reward = new Item(Cur.Ingot, 80) },
            new TaskDef { Name = "擂鼓招募 3 次", Need = 3, Reward = new Item(Cur.Token, 1) },
        };

        public static readonly Item[] TaskBonus = { new Item(Cur.Ingot, 120), new Item(Cur.Token, 2) };

        public static void Progress(SaveData s, int task, int n = 1)
        {
            if (s.Task == null || task >= s.Task.Length) return;
            s.Task[task] = Math.Min(Tasks[task].Need, s.Task[task] + n);
        }

        public static bool TaskDone(SaveData s, int i) => s.Task[i] >= Tasks[i].Need;
        public static bool TaskTaken(SaveData s, int i) => (s.TaskClaimed & (1 << i)) != 0;

        public static bool ClaimTask(SaveData s, int i)
        {
            if (!TaskDone(s, i) || TaskTaken(s, i)) return false;
            s.TaskClaimed |= 1 << i;
            Give(s, Tasks[i].Reward);
            return true;
        }

        public static bool AllTasksTaken(SaveData s)
        {
            for (int i = 0; i < Tasks.Length; i++) if (!TaskTaken(s, i)) return false;
            return true;
        }

        public const int BonusBit = 1 << 20;

        public static bool ClaimBonus(SaveData s)
        {
            if (!AllTasksTaken(s) || (s.TaskClaimed & BonusBit) != 0) return false;
            s.TaskClaimed |= BonusBit;
            foreach (var it in TaskBonus) Give(s, it);
            return true;
        }

        public static int TaskBadge(SaveData s)
        {
            int n = 0;
            for (int i = 0; i < Tasks.Length; i++) if (TaskDone(s, i) && !TaskTaken(s, i)) n++;
            if (AllTasksTaken(s) && (s.TaskClaimed & BonusBit) == 0) n++;
            return n;
        }

        public static readonly Item[] SignRewards =
        {
            new Item(Cur.Ingot, 100), new Item(Cur.Token, 2), new Item(Cur.Copper, 3000), new Item(Cur.Iron, 30),
            new Item(Cur.Token, 3), new Item(Cur.Ingot, 300), new Item(Cur.Shard, 20, HeroId.LinChong),
        };

        public static bool CanSign(SaveData s, int day) => s.SignDay != day && s.SignDays < SignRewards.Length;

        public static Item? Sign(SaveData s, int day)
        {
            if (!CanSign(s, day)) return null;
            var it = SignRewards[s.SignDays];
            s.SignDays++;
            s.SignDay = day;
            if (it.Cur == Cur.Shard && s.HeroLv[(int)it.Hero] == 0)
            {
                s.HeroLv[(int)it.Hero] = 1;
                return it;
            }
            Give(s, it);
            return it;
        }

        public static int IdleCopperPerHour(SaveData s) => 400 + 90 * s.Cleared;
        public static float IdleIronPerHour(SaveData s) => 3 + s.Cleared / 4f;

        public static Rewards IdleRewards(SaveData s, double hours)
        {
            hours = Math.Max(0, Math.Min(IdleCapHours, hours));
            var r = new Rewards();
            r.Add(Cur.Copper, (int)(IdleCopperPerHour(s) * hours));
            r.Add(Cur.Iron, (int)(IdleIronPerHour(s) * hours));
            r.Add(Cur.Token, (int)(hours / 4));
            return r;
        }

        public static double IdleHours(SaveData s, long now) => Math.Min(IdleCapHours, Math.Max(0, now - s.IdleAt) / 3600.0);

        public static Rewards CollectIdle(SaveData s, long now)
        {
            var r = IdleRewards(s, IdleHours(s, now));
            Give(s, r);
            s.IdleAt = now;
            return r;
        }

        // ---------------- 图鉴 ----------------

        public static int CodexReward(EnemyDef d) => d.IsBoss ? 100 : d.Big ? 50 : 20;

        public static bool ClaimCodex(SaveData s, EnemyKind k)
        {
            if (s.Seen[(int)k] != 1) return false;
            s.Seen[(int)k] = 2;
            s.Ingots += CodexReward(Defs.Enemy(k));
            return true;
        }

        public static int CodexBadge(SaveData s)
        {
            int n = 0;
            foreach (var v in s.Seen) if (v == 1) n++;
            return n;
        }

        public static void MarkSeen(SaveData s, IEnumerable<EnemyKind> kinds)
        {
            foreach (var k in kinds)
                if (s.Seen[(int)k] == 0) s.Seen[(int)k] = 1;
        }

        // ---------------- 商城 ----------------

        public sealed class ShopDef
        {
            public string Name, Icon;
            public Item Get;
            public int Price;
            /// <summary>0 元宝购买，1 看广告。</summary>
            public bool Ad;
        }

        public static ShopDef[] Shop(SaveData s) => new[]
        {
            new ShopDef { Name = "免费元宝", Icon = "ic_ingot", Get = new Item(Cur.Ingot, 60), Ad = true },
            new ShopDef { Name = "免费体力", Icon = "ic_wine", Get = new Item(Cur.Stamina, 30), Ad = true },
            new ShopDef { Name = "招募令", Icon = "ic_token", Get = new Item(Cur.Token, 1), Price = 150 },
            new ShopDef { Name = "招募令×10", Icon = "ic_token", Get = new Item(Cur.Token, 10), Price = 1350 },
            new ShopDef { Name = "一袋铜钱", Icon = "ic_coins", Get = new Item(Cur.Copper, 3000 + 300 * s.Cleared), Price = 100 },
            new ShopDef { Name = "一箱精铁", Icon = "ic_iron", Get = new Item(Cur.Iron, 30 + s.Cleared), Price = 120 },
            new ShopDef { Name = "酒坛体力", Icon = "ic_wine", Get = new Item(Cur.Stamina, 60), Price = 80 },
        };

        // ---------------- 数值参考 ----------------

        static int ExpectedLv(int n) => Math.Min(MaxHeroLv, 1 + (int)((n - 1) * 0.5f));
        static int ExpectedEquip(int n) => Math.Min(Math.Min(40, 5 + (n - 1) * 7 / 10), (int)((n - 1) * 0.6f));

        /// <summary>正常推进到第 n 关时大致的养成：调难度曲线、写单测都用它。</summary>
        public static SaveData ExpectedSave(int n)
        {
            var s = NewSave(0);
            int lv = ExpectedLv(n);
            for (int h = 0; h < 4; h++) s.HeroLv[h] = lv;
            for (int h = 4; h < Defs.HeroCount; h++) s.HeroLv[h] = Math.Max(0, lv - 4);
            s.Cleared = n - 1;
            int eq = ExpectedEquip(n);
            for (int i = 0; i < s.EquipLv.Length; i++) s.EquipLv[i] = eq;
            return s;
        }

        /// <summary>ExpectedSave(n) 的单个好汉输出倍率（伤害 × 攻速），不依赖关卡表，关卡表初始化时可以调。</summary>
        public static float ExpectedPower(int n)
        {
            int lv = ExpectedLv(n);
            int lv2 = Math.Max(0, lv - 4);
            int eq = ExpectedEquip(n);
            float global = (4 * (lv - 1) + 4 * Math.Max(0, lv2 - 1)) * GlobalPerLv;
            float talents = (lv >= 5 ? 1.08f : 1f) * (lv >= 15 ? 1.05f : 1f) * (lv >= 20 ? 1.15f : 1f);
            return HeroDmg(lv) * (lv >= 30 ? 1.2f : 1f) * (1f + global) * (1f + EquipValue(0, eq)) * (1f + EquipValue(3, eq)) * talents;
        }

        /// <summary>ExpectedSave(n) 的寨墙等效血量倍率（头盔加血、铠甲减伤）。</summary>
        public static float ExpectedWall(int n)
        {
            int eq = ExpectedEquip(n);
            return (1f + EquipValue(1, eq)) / (1f - EquipValue(2, eq));
        }
    }
}

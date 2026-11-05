using System;
using System.Collections.Generic;

namespace Liangshan.Sim
{
    public sealed class Unit
    {
        public int Id;
        public HeroId Hero;
        public int Star = 1;
        public float Cd;
        public bool Burst;
        /// <summary>被首领飞锤砸晕，期间不出手。</summary>
        public float Stun;
        /// <summary>吴用的祝福：接下来 BlessN 次出手冷却转速 × BlessMul。</summary>
        public int BlessN;
        public float BlessMul = 1f;
        public HeroDef Def => Defs.Hero(Hero);
    }

    /// <summary>法师的持续施法：每 Every 秒跳一次，直到 Left 用完或施法者离场、被晕。</summary>
    public sealed class Channel
    {
        public Unit Unit;
        public int Slot, Target, Ticks;
        public float Next, Every, Damage;
        /// <summary>「爆发」「最强」带来的弹道放大；Sure 为这一轮必定暴击。</summary>
        public float Scale = 1;
        public bool Sure;
    }

    public sealed class Slot
    {
        public int Index;
        public V2 Pos;
        public Unit Unit;
    }

    public sealed class Enemy
    {
        public int Id;
        public EnemyDef Def;
        public float Hp, MaxHp;
        public float Tough = 1;
        public V2 Pos;
        public float LaneX;
        public float SlowT, Stun, AtkCd;
        public bool Attacking, Dead, Escaped;
        public float HitT;
        /// <summary>首领：技能计时、冲锋、护盾。</summary>
        public float SkillT, ChargeT, Shield;
        public bool ShieldUsed;
        /// <summary>小兵特技：水鬼潜水、妖道作法、斥候闪身的计时；藤牌剩余格挡；敢死刀手无敌狂奔；黄安战鼓加速。</summary>
        public float AbilT, DiveT, RageT, HasteT;
        public int Guard;
        public bool Raged;
        public bool Diving => DiveT > 0;
        public bool Big => Def.Big;
        public bool Mounted => Def.Kind == EnemyKind.Cavalry || Def.Kind == EnemyKind.ChainHorse || Def.Kind == EnemyKind.HuYan || Def.Kind == EnemyKind.ZengKui;
    }

    public enum ShotKind { Arrow, Bomb, MiniBomb, Bolt, Lightning, Punch, Stone, Spear, Slam, Wind, Frost, Hammer, Ball, Fireball, Lasso, Slash, Beam, Boulder }

    public sealed class Shot
    {
        public int Id;
        public ShotKind Kind;
        public HeroId Hero;
        public V2 From, To;
        public float T, Dur;
        public float Damage, Radius;
        public int TargetId = -1;
        public int Pierce;
        public bool Crit, Ult, Split;
        /// <summary>弹道显示放大倍数：「爆发」打出的巨型一击、「最强」那一位的攻击。</summary>
        public float Scale = 1;
        public float Stun, StunChance, Knock, Slow;
        /// <summary>飞石已经打过的敌人。</summary>
        public List<int> Hit;
    }

    public enum Ev
    {
        Summon, Merge, Move, SlotAdded, Fire, Hit, Damage, Kill, WallHit, Ult, WaveStart, BossComing, EliteComing,
        Rage, PorterKilled, PorterEscaped, Enhance, Victory, Defeat, Revive, Coin, Reels, BossSkill, Explode, UnitStun,
        /// <summary>精英、首领倒下掉出生辰纲。</summary>
        Treasure,
        /// <summary>合成出满星，弹出额外技能三选一。</summary>
        StarPick,
        Heal,
        /// <summary>寨主放技能：剑气从寨墙出发往城门扫。</summary>
        LordSkill,
        /// <summary>辅助放技能。Slot 是施法者；祝福时 EnemyId 是被祝福的格子。</summary>
        Buff,
        /// <summary>寨墙首次掉到半血，杏黄旗震慑全场。Radius 是眩晕秒数。</summary>
        Rally,
        /// <summary>「爆发」：这一下打出巨型一击。Slot 是出手的格子。</summary>
        Surge,
        /// <summary>妖道作法给身边官军回血，Radius 是范围。</summary>
        EnemyHeal,
        /// <summary>藤牌挡下一击，Value 是剩余格挡次数。</summary>
        Guard,
        /// <summary>斥候闪身躲开，Pos 是闪到的位置。</summary>
        Blink,
        /// <summary>敢死刀手残血狂暴。</summary>
        Berserk,
    }

    public enum BossSkill { Hammer = 1, Charge, Summon, Ball, Shield, Drum, Leap, Ram, Volley }

    public struct BattleEvent
    {
        public Ev Type;
        public V2 Pos;
        public int Slot, Value, EnemyId;
        public HeroId Hero;
        public ShotKind Shot;
        public float Radius;
        public bool Crit;
    }

    public enum Phase { Running, Choosing, Reels, Defeat, Victory }

    /// <summary>生辰纲老虎机只出银两和好汉强化：不送好汉（会占满格子卡住合成）、不送免费强化次数、不修墙。</summary>
    public enum Reel { Silver, Perk }

    public sealed class Battle
    {
        public readonly HeroId[] Lineup;
        public readonly Loadout Lo;
        public readonly List<Slot> Slots = new List<Slot>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<Channel> Channels = new List<Channel>();
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly Dictionary<string, int> Perks = new Dictionary<string, int>();
        public readonly float[] DamageBy = new float[Defs.HeroCount];
        public readonly float[] UltTimer = new float[Defs.HeroCount];
        /// <summary>寨主技能：距下次释放的秒数、累计伤害、释放次数。</summary>
        public float LordT = Defs.LordFirstCast, LordDur = Defs.LordFirstCast, LordDamage;
        public int LordCasts;
        public readonly HashSet<EnemyKind> Seen = new HashSet<EnemyKind>();
        readonly float[,] _stat = new float[Defs.HeroCount, Defs.EffCount];
        readonly bool[] _starDone = new bool[Defs.HeroCount];
        readonly bool[] _ultOn = new bool[Defs.HeroCount];

        public Phase Phase = Phase.Running;
        public float Time;
        public readonly LevelDef Stage;
        public int Coins;
        public float Wall, WallMax;
        /// <summary>Summons 只算花钱召唤（定价用）；Recruited 含老虎机送的（扩格用）。</summary>
        public int Summons, Recruited, Enhances, Kills;
        public int Wave;
        public bool Rage;
        public bool RevivedOnce, RerolledOnce;
        public PerkDef[] Choices;
        /// <summary>不为空时 Choices 是这位好汉的满星额外技能。</summary>
        public HeroId? StarPick;
        public int PendingReels, JackpotReels;
        public int PortersKilled, ElitesKilled, BossesKilled;
        /// <summary>急攻（日行千里）、神兵（打造神兵）剩余时间与倍率。</summary>
        public float HasteT, HasteMul = 1f, ForgeT, ForgeV;
        /// <summary>辅助光环：攻速、伤害、冷却转速、击杀银两加成、每 4 秒利息比例。</summary>
        public float Aura, DmgAura, ChargeAura, CoinAura, Interest;
        float _interestT;
        int _highStars;
        bool _owePity;

        readonly Rng _rng;
        int _nextId = 1;
        readonly List<(float at, EnemyKind kind, int gate)> _queue = new List<(float, EnemyKind, int)>();

        public Battle(int seed, HeroId[] lineup, LevelDef stage = null, Loadout lo = null)
        {
            _rng = new Rng(seed);
            Lineup = lineup;
            Lo = lo ?? Loadout.Plain(lineup);
            Stage = stage ?? Defs.Levels[0];
            Coins = Stage.StartCoins + Lo.StartCoins;
            Wall = WallMax = Stage.WallHp * Lo.WallMul;
            for (int i = 0; i < Layout.BaseSlots; i++) AddSlot();
            foreach (var id in Lo.StartPerks)
            {
                var p = Defs.Perk(id);
                if (p == null || Level(id) >= p.Max) continue;
                Perks[id] = Level(id) + 1;
            }
            RebuildStats();
        }

        public Rng Rng => _rng;
        public int SummonCost => Defs.SummonCost(Summons);
        public int EnhanceCost => Defs.EnhanceCost(Enhances);
        public int SummonsToNextSlot => Slots.Count >= Layout.MaxSlots ? 0 : Defs.SummonsPerSlot - Recruited % Defs.SummonsPerSlot;
        public float WaveProgress => Wave == 0 ? 0 : M.Clamp01((Time - (Wave - 1) * Defs.WaveTime) / Defs.WaveTime);
        public bool AllSpawned => Wave >= Stage.Waves && _queue.Count == 0;
        public int Level(string perk) => Perks.TryGetValue(perk, out var n) ? n : 0;
        /// <summary>大招只有传说好汉有，要在强化三选一里抽到「大招」卡才解锁。</summary>
        public bool HasUlt(HeroId h) => _ultOn[(int)h];
        public bool InLineup(HeroId h) => Array.IndexOf(Lineup, h) >= 0;
        public bool StarTaken(HeroId h) => _starDone[(int)h];

        /// <summary>这位好汉身上某种效果的总量：已选强化 × 等级 + 10 级被动 + 15 级「爆发」加成。</summary>
        public float Stat(HeroId h, Eff e) => _stat[(int)h, (int)e];

        void RebuildStats()
        {
            Array.Clear(_stat, 0, _stat.Length);
            foreach (var kv in Perks)
            {
                var p = Defs.Perk(kv.Key);
                if (p != null) _stat[(int)p.Hero, (int)p.Eff] += p.V * kv.Value;
            }
            for (int h = 0; h < Defs.HeroCount; h++)
            {
                var opt = Meta.OptPerk((HeroId)h, 1);
                if (opt != null && Level(opt.Id) > 0 && Lo.Lv[h] >= Meta.SurgePlusLv)
                    _stat[h, (int)opt.Eff] += opt.Eff == Eff.Surge ? Defs.SurgePlusChance : opt.V;
                if (!Lo.Passive[h]) continue;
                var d = Defs.Heroes[h];
                _stat[h, (int)d.PassiveEff] += d.PassiveV;
            }
        }

        void Emit(Ev t, V2 pos = default, int slot = -1, int value = 0, HeroId hero = HeroId.HuaRong)
            => Events.Add(new BattleEvent { Type = t, Pos = pos, Slot = slot, Value = value, Hero = hero });

        Slot AddSlot()
        {
            var s = new Slot { Index = Slots.Count, Pos = Layout.SlotPos(Slots.Count) };
            Slots.Add(s);
            return s;
        }

        Slot SlotOf(HeroId h)
        {
            Slot best = null;
            foreach (var s in Slots)
                if (s.Unit != null && s.Unit.Hero == h && (best == null || s.Unit.Star > best.Unit.Star)) best = s;
            return best;
        }

        // ---------------- 玩家操作 ----------------

        public bool HasEmptySlot()
        {
            foreach (var s in Slots) if (s.Unit == null) return true;
            return false;
        }

        public bool CanSummon => Phase == Phase.Running && Coins >= SummonCost && HasEmptySlot();

        public Slot Summon(bool free = false, int star = 1)
        {
            if (Phase != Phase.Running && !free) return null;
            if (!HasEmptySlot()) return null;
            if (!free)
            {
                if (Coins < SummonCost) return null;
                Coins -= SummonCost;
            }
            var empty = new List<Slot>();
            foreach (var s in Slots) if (s.Unit == null) empty.Add(s);
            var slot = empty[_rng.Int(empty.Count)];
            slot.Unit = new Unit { Id = _nextId++, Hero = Lineup[_rng.Int(Lineup.Length)], Star = star, Cd = 0.8f };
            if (!free) Summons++;
            Recruited++;
            Emit(Ev.Summon, slot.Pos, slot.Index, star, slot.Unit.Hero);
            if (Recruited % Defs.SummonsPerSlot == 0 && Slots.Count < Layout.MaxSlots)
            {
                var ns = AddSlot();
                Emit(Ev.SlotAdded, ns.Pos, ns.Index);
            }
            return slot;
        }

        public bool CanMerge(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Slots.Count || b >= Slots.Count) return false;
            var ua = Slots[a].Unit;
            var ub = Slots[b].Unit;
            return ua != null && ub != null && ua.Hero == ub.Hero && ua.Star == ub.Star && ua.Star < 4;
        }

        /// <summary>
        /// 合成结果随机。保底：第一个三星以上若是辅助，下一个三星以上必出输出位。
        /// </summary>
        HeroId MergeResult(int star)
        {
            var h = Lineup[_rng.Int(Lineup.Length)];
            if (star < 3) return h;
            if (_owePity && !Defs.Hero(h).Carry)
            {
                var carries = new List<HeroId>();
                foreach (var x in Lineup) if (Defs.Hero(x).Carry) carries.Add(x);
                if (carries.Count > 0) h = carries[_rng.Int(carries.Count)];
            }
            _highStars++;
            if (_highStars == 1 && !Defs.Hero(h).Carry) _owePity = true;
            else if (Defs.Hero(h).Carry) _owePity = false;
            return h;
        }

        /// <summary>拖 a 到 b：同名同星合成，b 变成随机一位上阵好汉的高一星；空格就挪过去；不同的两格互换。</summary>
        public bool Drop(int a, int b)
        {
            if (Phase != Phase.Running || a == b || a < 0 || b < 0 || a >= Slots.Count || b >= Slots.Count) return false;
            var sa = Slots[a];
            var sb = Slots[b];
            if (sa.Unit == null) return false;
            if (CanMerge(a, b))
            {
                int star = sb.Unit.Star + 1;
                sa.Unit = null;
                sb.Unit = new Unit { Id = _nextId++, Hero = MergeResult(star), Star = star, Cd = 0.4f, Burst = true };
                Emit(Ev.Merge, sb.Pos, b, star, sb.Unit.Hero);
                foreach (var h in Lineup)
                {
                    int coin = (int)Stat(h, Eff.Coin);
                    if (coin <= 0 || SlotOf(h) == null) continue;
                    Coins += coin;
                    Emit(Ev.Coin, sb.Pos, b, coin, h);
                }
                if (star == 4 && !_starDone[(int)sb.Unit.Hero]) OpenStar(sb.Unit.Hero, sb);
                return true;
            }
            var t = sb.Unit;
            sb.Unit = sa.Unit;
            sa.Unit = t;
            Emit(Ev.Move, sb.Pos, b);
            return true;
        }

        void OpenStar(HeroId h, Slot at)
        {
            _starDone[(int)h] = true;
            var pool = Defs.PerksOf(h, true);
            pool.RemoveAll(p => Level(p.Id) >= p.Max || UltLocked(p));
            if (pool.Count == 0) return;
            Choices = pool.ToArray();
            StarPick = h;
            Phase = Phase.Choosing;
            Emit(Ev.StarPick, at.Pos, at.Index, 4, h);
        }

        public bool CanEnhance => Phase == Phase.Running && Coins >= EnhanceCost && RollChoices(false) != null;

        public bool Enhance()
        {
            if (Phase != Phase.Running || Coins < EnhanceCost) return false;
            var c = RollChoices(true);
            if (c == null) return false;
            Coins -= EnhanceCost;
            Enhances++;
            Choices = c;
            StarPick = null;
            Phase = Phase.Choosing;
            return true;
        }

        public void Reroll()
        {
            if (Phase != Phase.Choosing || StarPick.HasValue) return;
            RerolledOnce = true;
            Choices = RollChoices(true) ?? Choices;
        }

        public void Pick(int i)
        {
            if (Phase != Phase.Choosing || Choices == null || i < 0 || i >= Choices.Length) return;
            Grant(Choices[i]);
            Choices = null;
            StarPick = null;
            Phase = Phase.Running;
        }

        void Grant(PerkDef p)
        {
            Perks[p.Id] = Level(p.Id) + 1;
            if (p.Ult)
            {
                _ultOn[(int)p.Hero] = true;
                UltTimer[(int)p.Hero] = 1.5f;
            }
            RebuildStats();
            Emit(Ev.Enhance, default, -1, p.Ult ? 2 : p.Star ? 1 : 0, p.Hero);
        }

        /// <summary>只强化大招的卡，在大招卡抽到之前不出（辅助的「效果」强化同时管技能，不受限）。</summary>
        bool UltLocked(PerkDef p) =>
            !p.Ult && (p.Eff == Eff.UltPow || p.Eff == Eff.UltCd) && Defs.Hero(p.Hero).Kind != Role.Support && !HasUlt(p.Hero);

        PerkDef[] RollChoices(bool consume)
        {
            var pool = new List<PerkDef>();
            foreach (var p in Defs.Perks)
            {
                if (p.Star || Level(p.Id) >= p.Max || !InLineup(p.Hero) || UltLocked(p)) continue;
                if (p.Ult && SlotOf(p.Hero) == null) continue;
                if (p.Opt == 1 && Lo.Lv[(int)p.Hero] < Meta.SurgeLv || p.Opt == 2 && Lo.Lv[(int)p.Hero] < Meta.TopLv) continue;
                pool.Add(p);
            }
            if (pool.Count == 0) return null;
            if (!consume) return new PerkDef[0];
            var pick = new List<PerkDef>();
            while (pick.Count < 3 && pool.Count > 0)
            {
                float sum = 0;
                foreach (var p in pool) sum += Weight(p);
                float r = _rng.F() * sum;
                int k = 0;
                for (; k < pool.Count - 1; k++)
                {
                    r -= Weight(pool[k]);
                    if (r <= 0) break;
                }
                pick.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return pick.ToArray();
        }

        /// <summary>场上星级越高的好汉，他的强化越容易出；不在场的也能出，但概率低。</summary>
        float Weight(PerkDef p)
        {
            var s = SlotOf(p.Hero);
            int best = s?.Unit.Star ?? 0;
            return p.Weight * (best == 0 ? 0.35f : 1f + best * 0.25f);
        }

        /// <summary>
        /// 老虎机：四个格子，每格银两或强化（35%）。强化格当场随机给上阵好汉一个强化，一次最多 2 个；
        /// 四格相同：银两翻倍，或强化给 3 个。首领掉的那次必中四格相同。
        /// 强化在开转时就定好（ReelPerks），界面据此报出给了谁什么，收下时生效。
        /// </summary>
        public Reel[] SpinReels()
        {
            if (Phase != Phase.Reels) return null;
            var r = new Reel[4];
            bool jackpot = JackpotReels > 0 || _rng.Chance(0.16f);
            Reel Roll() => _rng.Chance(PerkCellChance) ? Reel.Perk : Reel.Silver;
            var first = Roll();
            for (int i = 0; i < 4; i++) r[i] = jackpot ? first : Roll();
            int want = Array.FindAll(r, x => x == Reel.Perk).Length;
            want = jackpot && first == Reel.Perk ? ReelPerkJackpot : Math.Min(want, ReelPerkMax);
            var pool = want > 0 ? RollChoices(true) : null;
            int got = pool == null ? 0 : Math.Min(want, pool.Length);
            ReelPerks = new PerkDef[got];
            for (int i = 0; i < got; i++) ReelPerks[i] = pool[i];
            // 强化抽不出来（都满级了）的格子折成银两。
            ReelExtraSilver = (want - got) * ReelSilver;
            return r;
        }

        public const float PerkCellChance = 0.35f;
        public const int ReelPerkMax = 2, ReelPerkJackpot = 3;
        public bool NextIsJackpot => JackpotReels > 0;
        /// <summary>每格银两。不再送援兵和修墙，这份银两要够玩家自己多召唤一两次。</summary>
        public int ReelSilver => 100 + Wave * 40;
        /// <summary>这一转定下的强化。</summary>
        public PerkDef[] ReelPerks { get; private set; } = new PerkDef[0];
        public int ReelExtraSilver { get; private set; }

        /// <summary>这一转一共给多少银两。</summary>
        public int ReelSilverOf(Reel[] r)
        {
            bool all = r[0] == r[1] && r[1] == r[2] && r[2] == r[3];
            int n = Array.FindAll(r, x => x == Reel.Silver).Length;
            return n * ReelSilver * (all ? 2 : 1) + ReelExtraSilver;
        }

        public void ApplyReels(Reel[] r)
        {
            Coins += ReelSilverOf(r);
            foreach (var p in ReelPerks) Grant(p);
            ReelPerks = new PerkDef[0];
            ReelExtraSilver = 0;
            PendingReels = Math.Max(0, PendingReels - 1);
            if (JackpotReels > 0) JackpotReels--;
            Phase = Phase.Running;
        }

        public void Revive()
        {
            if (Phase != Phase.Defeat || RevivedOnce) return;
            RevivedOnce = true;
            Wall = WallMax * 0.5f;
            foreach (var e in Enemies)
            {
                if (e.Pos.Y < Layout.StopY + 2.5f)
                {
                    e.Pos.Y = Math.Min(Layout.SpawnY, e.Pos.Y + (e.Big ? 1.2f : 3f));
                    e.Attacking = false;
                    e.Stun = 1.2f;
                }
            }
            Phase = Phase.Running;
            Emit(Ev.Revive);
        }

        // ---------------- 推进 ----------------

        public void Update(float dt)
        {
            if (Phase != Phase.Running) return;
            Time += dt;
            if (HasteT > 0) { HasteT -= dt; if (HasteT <= 0) HasteMul = 1f; }
            if (ForgeT > 0) ForgeT -= dt;
            UpdateAuras(dt);
            UpdateWaves();
            UpdateEnemies(dt);
            UpdateUnits(dt);
            UpdateChannels(dt);
            UpdateUlts(dt);
            UpdateLord(dt);
            UpdateShots(dt);
            Enemies.RemoveAll(e => e.Dead || e.Escaped);

            if (Wall <= 0)
            {
                Wall = 0;
                Phase = Phase.Defeat;
                Emit(Ev.Defeat);
                return;
            }
            if (PendingReels > 0)
            {
                Phase = Phase.Reels;
                Emit(Ev.Reels);
                return;
            }
            if (AllSpawned && Enemies.Count == 0 && Shots.Count == 0 && Channels.Count == 0)
            {
                Phase = Phase.Victory;
                Emit(Ev.Victory);
            }
        }

        void UpdateAuras(float dt)
        {
            float spd = 0, dmg = 0, charge = 0, coin = 0, interest = 0;
            Slot banker = null;
            foreach (var s in Slots)
            {
                var u = s.Unit;
                if (u == null || u.Stun > 0) continue;
                var d = u.Def;
                if (d.Aura == AuraKind.None) continue;
                float v = d.AuraV * u.Star * (1f + Stat(u.Hero, Eff.Aura));
                switch (d.Aura)
                {
                    case AuraKind.Speed: spd += v; break;
                    case AuraKind.Dmg: dmg += v; break;
                    case AuraKind.Charge: charge += v; break;
                    case AuraKind.Coin: coin += v; break;
                    case AuraKind.Interest: interest += v; banker = s; break;
                }
            }
            Aura = Math.Min(0.5f, spd);
            DmgAura = Math.Min(0.4f, dmg);
            ChargeAura = Math.Min(0.3f, charge);
            CoinAura = Math.Min(1f, coin);
            Interest = Math.Min(0.08f, interest);
            if (Interest <= 0 || Wave == 0) { _interestT = 0; return; }
            _interestT += dt;
            if (_interestT < 4f) return;
            _interestT -= 4f;
            int gain = M.Clamp((int)Math.Round(Coins * Interest), 1, 25);
            Coins += gain;
            Emit(Ev.Coin, banker.Pos, banker.Index, gain, banker.Unit.Hero);
        }

        void UpdateWaves()
        {
            int want = Math.Min(Stage.Waves, (int)(Time / Defs.WaveTime) + 1);
            while (Wave < want)
            {
                Wave++;
                StartWave(Wave);
            }
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].at > Time) continue;
                Spawn(_queue[i].kind, _queue[i].gate);
                _queue.RemoveAt(i);
            }
            foreach (var p in _bruteDrops)
                for (int k = 0; k < 3; k++)
                {
                    var s = Spawn(EnemyKind.Soldier, -1, new V2(p.X + (k - 1) * 0.6f, p.Y + 0.3f));
                    s.LaneX = M.Clamp(p.X + (k - 1) * 0.9f, -3.7f, 3.7f);
                }
            _bruteDrops.Clear();
        }

        /// <summary>力士倒下的位置，下一帧从背篓里跳出官兵（不能在伤害结算中途改敌人列表）。</summary>
        readonly List<V2> _bruteDrops = new List<V2>();

        public EnemyKind EliteKind(int w)
        {
            int k = Array.IndexOf(Stage.EliteWaves, w);
            var kinds = Stage.EliteKinds;
            return kinds[(k < 0 ? 0 : k) % kinds.Length];
        }

        void StartWave(int w)
        {
            float t0 = (w - 1) * Defs.WaveTime;
            var troops = Defs.WaveTroops(Stage, w, _rng);
            // 参考视频：兵是一股一股接连不断涌上来的，整波铺满，波与波之间不留空档。
            float gap = Defs.WaveTime / Math.Max(1, troops.Count);
            for (int i = 0; i < troops.Count; i++)
            {
                float at = t0 + 0.4f + (i + _rng.Range(-0.35f, 0.35f)) * gap;
                int gate = _rng.Int(2);
                int pack = troops[i] == EnemyKind.Hound ? 2 : 1;
                for (int k = 0; k < pack; k++) _queue.Add((at + k * 0.25f, troops[i], gate));
            }
            if (Array.IndexOf(Stage.EliteWaves, w) >= 0)
            {
                var ek = EliteKind(w);
                _queue.Add((t0 + 6f, ek, _rng.Int(2)));
                Events.Add(new BattleEvent { Type = Ev.EliteComing, Slot = (int)ek, Value = w });
            }
            if (Array.IndexOf(Stage.PorterWaves, w) >= 0)
                for (int i = 0; i < Stage.PorterCount; i++)
                    _queue.Add((t0 + 3f + i * 2.2f, EnemyKind.Porter, _rng.Int(2)));
            if (w == Stage.BossWave)
            {
                _queue.Add((t0 + 2.5f, Stage.BossKind, -1));
                Events.Add(new BattleEvent { Type = Ev.BossComing, Slot = (int)Stage.BossKind, Value = w });
            }
            if (w == Stage.RageWave)
            {
                Rage = true;
                Emit(Ev.Rage, default, -1, w);
            }
            _queue.Sort((x, y) => y.at.CompareTo(x.at));
            Emit(Ev.WaveStart, default, -1, w);
        }

        Enemy Spawn(EnemyKind kind, int gate, V2? at = null)
        {
            var def = Defs.Enemy(kind);
            float tough = def.Big ? 1f : Defs.Tough(Wave);
            float hp = def.Hp * Defs.HpScale(Wave) * tough * Stage.HpMul * (def.IsBoss ? Stage.BossMul : 1f);
            float gx = gate < 0 ? 0 : Layout.GateX[gate];
            var e = new Enemy
            {
                Id = _nextId++, Def = def, Hp = hp, MaxHp = hp, Tough = tough,
                Pos = at ?? new V2(gx + _rng.Range(-0.5f, 0.5f), Layout.SpawnY + _rng.Range(0f, 0.4f)),
                LaneX = def.IsBoss ? 0 : _rng.Range(-3.7f, 3.7f),
                AtkCd = _rng.Range(0.2f, 1f),
                SkillT = 4f,
            };
            if (kind == EnemyKind.GaoQiu) e.Shield = hp * 0.2f;
            if (kind == EnemyKind.Rattan) e.Guard = 2;
            if (kind == EnemyKind.Diver || kind == EnemyKind.Taoist) e.AbilT = _rng.Range(1f, 3f);
            Seen.Add(kind);
            Enemies.Add(e);
            return e;
        }

        float EnemySpeed(Enemy e)
        {
            float s = e.Def.Speed * (Rage ? 1.3f : 1f) * (e.Def.IsBoss ? Defs.BossPace : 1f);
            if (e.SlowT > 0) s *= 0.55f;
            if (e.ChargeT > 0) s *= 5f;
            if (e.Def.Kind == EnemyKind.ShiWen && e.Hp < e.MaxHp * 0.5f) s *= 1.3f;
            if (e.RageT > 0) s *= 2.5f;
            if (e.HasteT > 0) s *= 1.2f;
            return s;
        }

        void UpdateEnemies(float dt)
        {
            float atkMul = (Rage ? 1.5f : 1f) * Stage.AtkMul;
            int n = Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = Enemies[i];
                if (e.Dead) continue;
                if (e.HitT > 0) e.HitT -= dt;
                if (e.SlowT > 0) e.SlowT -= dt;
                if (e.ChargeT > 0) e.ChargeT -= dt;
                if (e.AbilT > 0) e.AbilT -= dt;
                if (e.DiveT > 0) e.DiveT -= dt;
                if (e.RageT > 0) e.RageT -= dt;
                if (e.HasteT > 0) e.HasteT -= dt;
                if (e.Def.IsBoss && e.Pos.Y < Layout.EngageY) BossSkills(e, dt);
                if (e.Stun > 0) { e.Stun -= dt; continue; }
                float stop = e.Def.StopY + (e.Big ? e.Def.Size * 0.18f : 0f);
                if (e.Def.Kind == EnemyKind.Taoist && e.AbilT <= 0 && e.Pos.Y < Layout.EngageY) TaoistHeal(e);
                if (e.Pos.Y > stop)
                {
                    if (e.Def.Kind == EnemyKind.Diver && e.AbilT <= 0 && e.Pos.Y < Layout.EngageY)
                    {
                        e.AbilT = 5f;
                        e.DiveT = 1.2f;
                    }
                    e.Attacking = false;
                    e.Pos.Y = Math.Max(stop, e.Pos.Y - EnemySpeed(e) * dt);
                    float k = M.Clamp01(dt * 0.9f);
                    e.Pos.X += (e.LaneX - e.Pos.X) * k;
                    continue;
                }
                if (e.Def.Kind == EnemyKind.Porter)
                {
                    e.Escaped = true;
                    Events.Add(new BattleEvent { Type = Ev.PorterEscaped, Pos = e.Pos, EnemyId = e.Id });
                    continue;
                }
                if (e.Def.Kind == EnemyKind.FireBomb || e.Def.Kind == EnemyKind.Lantern)
                {
                    e.Escaped = true;
                    HitWall(e.Def.WallDps * atkMul * e.Tough, e.Pos);
                    Events.Add(new BattleEvent { Type = Ev.Explode, Pos = e.Pos, EnemyId = e.Id, Radius = 1.2f });
                    continue;
                }
                e.Attacking = true;
                e.AtkCd -= dt;
                if (e.AtkCd > 0) continue;
                var kind = e.Def.Kind;
                float period = kind == EnemyKind.Crossbow || kind == EnemyKind.LiuMenglong ? 1.6f : kind == EnemyKind.Catapult ? 3f : 1f;
                e.AtkCd = period;
                float dmg = e.Def.WallDps * period * atkMul * e.Tough;
                if (kind == EnemyKind.ShiWen && e.Hp < e.MaxHp * 0.5f) dmg *= 1.25f;
                if (kind == EnemyKind.Crossbow || kind == EnemyKind.LiuMenglong || kind == EnemyKind.Catapult)
                {
                    bool rock = kind == EnemyKind.Catapult;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = rock ? ShotKind.Boulder : ShotKind.Bolt, From = e.Pos + new V2(0, rock ? 0.5f : 0f),
                        To = new V2(e.Pos.X + (rock ? _rng.Range(-1f, 1f) : 0f), Layout.WallRowY + 0.3f), Dur = rock ? 1.1f : 0.55f, Damage = dmg,
                    });
                    continue;
                }
                HitWall(dmg, e.Pos);
            }
            Separate(dt);
        }

        void BossSkills(Enemy e, float dt)
        {
            if (e.Def.Kind == EnemyKind.GaoQiu && !e.ShieldUsed && e.Hp < e.MaxHp * 0.5f)
            {
                e.ShieldUsed = true;
                e.Shield = e.MaxHp * 0.15f;
                Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Shield });
            }
            if (e.Stun > 0) return;
            e.SkillT -= dt;
            if (e.SkillT > 0) return;
            switch (e.Def.Kind)
            {
                case EnemyKind.Luan:
                {
                    var pool = new List<Slot>();
                    foreach (var s in Slots) if (s.Unit != null && s.Unit.Stun <= 0) pool.Add(s);
                    e.SkillT = 7f;
                    if (pool.Count == 0) return;
                    var t = pool[_rng.Int(pool.Count)];
                    Shots.Add(new Shot { Id = _nextId++, Kind = ShotKind.Hammer, From = e.Pos, To = t.Pos, TargetId = t.Index, Dur = 0.75f, Stun = 3f });
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Slot = t.Index, Value = (int)BossSkill.Hammer });
                    return;
                }
                case EnemyKind.ShiWen:
                    e.SkillT = e.Hp < e.MaxHp * 0.5f ? 6f : 9f;
                    if (e.Attacking) return;
                    e.ChargeT = 0.35f;
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Charge });
                    return;
                case EnemyKind.HuYan:
                    e.SkillT = 10f;
                    for (int i = 0; i < 3; i++)
                    {
                        var c = Spawn(EnemyKind.Cavalry, -1, new V2(e.Pos.X + (i - 1) * 1.1f, Math.Min(Layout.SpawnY, e.Pos.Y + 0.6f)));
                        c.LaneX = M.Clamp(e.Pos.X + (i - 1) * 2.2f, -3.7f, 3.7f);
                    }
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Summon });
                    return;
                case EnemyKind.GaoQiu:
                    e.SkillT = 10f;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Ball, From = e.Pos, To = new V2(e.Pos.X + _rng.Range(-1.5f, 1.5f), Layout.WallRowY + 0.3f),
                        Dur = 0.9f, Damage = e.Def.WallDps * 1.5f * Stage.AtkMul,
                    });
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Ball });
                    return;
                case EnemyKind.HeTao:
                    e.SkillT = 9f;
                    for (int i = 0; i < 3; i++)
                    {
                        var d = Spawn(EnemyKind.Diver, -1, new V2(e.Pos.X + (i - 1) * 1.1f, Math.Min(Layout.SpawnY, e.Pos.Y + 0.4f)));
                        d.LaneX = M.Clamp(e.Pos.X + (i - 1) * 2.2f, -3.7f, 3.7f);
                    }
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Summon });
                    return;
                case EnemyKind.Boss:
                    e.SkillT = 10f;
                    foreach (var o in Enemies)
                        if (!o.Dead && o != e && (o.Pos - e.Pos).Len2 < DrumRadius * DrumRadius) o.HasteT = 2f;
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Drum, Radius = DrumRadius });
                    return;
                case EnemyKind.ZhuChaofeng:
                    e.SkillT = 15f;
                    e.Shield = Math.Max(e.Shield, e.MaxHp * 0.08f);
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Shield });
                    return;
                case EnemyKind.ZengKui:
                {
                    e.SkillT = 9f;
                    float stop = e.Def.StopY + e.Def.Size * 0.18f;
                    if (e.Attacking || e.Pos.Y <= stop) return;
                    e.Pos.Y = Math.Max(stop, e.Pos.Y - 1.3f);
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Leap });
                    return;
                }
                case EnemyKind.HanTao:
                    if (!e.Attacking) { e.SkillT = 1f; return; }
                    e.SkillT = 6f;
                    HitWall(e.Def.WallDps * 2.5f * Stage.AtkMul, e.Pos);
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Ram });
                    return;
                case EnemyKind.LiuMenglong:
                    e.SkillT = 8f;
                    for (int i = 0; i < 5; i++)
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Bolt, From = e.Pos + new V2((i - 2) * 0.3f, 1.2f), T = -i * 0.08f,
                            To = new V2(M.Clamp(e.Pos.X + (i - 2) * 1.3f, -4f, 4f), Layout.WallRowY + 0.3f), Dur = 0.6f, Damage = e.Def.WallDps * 0.5f * Stage.AtkMul,
                        });
                    Events.Add(new BattleEvent { Type = Ev.BossSkill, Pos = e.Pos, EnemyId = e.Id, Value = (int)BossSkill.Volley });
                    return;
                default:
                    e.SkillT = 99f;
                    return;
            }
        }

        public const float DrumRadius = 3f, TaoistRadius = 2.5f;

        void TaoistHeal(Enemy e)
        {
            e.AbilT = 5f;
            bool any = false;
            foreach (var o in Enemies)
            {
                if (o.Dead || o.Hp >= o.MaxHp || (o.Pos - e.Pos).Len2 > TaoistRadius * TaoistRadius) continue;
                o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * (o.Def.IsBoss ? 0.03f : 0.12f));
                any = true;
            }
            if (any) Events.Add(new BattleEvent { Type = Ev.EnemyHeal, Pos = e.Pos, EnemyId = e.Id, Radius = TaoistRadius });
        }

        void HitWall(float dmg, V2 pos)
        {
            dmg *= 1f - Lo.WallResist;
            Wall -= dmg;
            Events.Add(new BattleEvent { Type = Ev.WallHit, Pos = pos, Value = (int)dmg });
            if (!Rallied && Lo.FlagStun > 0 && Wall > 0 && Wall < WallMax * 0.5f) Rally();
        }

        public bool Rallied;

        void Rally()
        {
            Rallied = true;
            foreach (var e in Enemies)
                if (!e.Dead && !e.Escaped) e.Stun = Math.Max(e.Stun, e.Def.IsBoss ? Lo.FlagStun * 0.3f : Lo.FlagStun);
            Events.Add(new BattleEvent { Type = Ev.Rally, Pos = new V2(0, Layout.WallRowY), Radius = Lo.FlagStun });
        }

        void HealWall(float amount, V2 pos, HeroId h)
        {
            if (amount <= 0 || Wall >= WallMax) return;
            float add = Math.Min(WallMax - Wall, amount);
            Wall += add;
            Emit(Ev.Heal, pos, -1, (int)Math.Ceiling(add), h);
        }

        /// <summary>兵挤兵：轻推开，免得几十个叠成一坨。飞的不跟地上的挤。</summary>
        void Separate(float dt)
        {
            int n = Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Enemies[i];
                for (int j = i + 1; j < n; j++)
                {
                    var b = Enemies[j];
                    if (a.Def.Flying != b.Def.Flying) continue;
                    float dx = b.Pos.X - a.Pos.X, dy = b.Pos.Y - a.Pos.Y;
                    float r = (a.Def.Size + b.Def.Size) * 0.27f;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= r * r || d2 < 1e-6f) continue;
                    float d = (float)Math.Sqrt(d2);
                    float push = (r - d) * 0.5f * Math.Min(1f, dt * 8f);
                    float nx = dx / d, ny = dy / d;
                    float wa = a.Big ? 0.15f : 1f, wb = b.Big ? 0.15f : 1f;
                    a.Pos.X -= nx * push * wa;
                    b.Pos.X += nx * push * wb;
                    if (!a.Attacking) a.Pos.Y -= ny * push * wa * 0.5f;
                    if (!b.Attacking) b.Pos.Y += ny * push * wb * 0.5f;
                }
                a.Pos.X = M.Clamp(a.Pos.X, -Layout.HalfW + 1.1f, Layout.HalfW - 1.1f);
            }
        }

        // ---------------- 好汉数值 ----------------

        /// <summary>寨主攻击力 × 好汉等级 × 职业突破 × 强化 × 关卡增益 × 光环。乘上系数和星级就是一次普攻的伤害。</summary>
        public float HeroDmgMul(HeroId h)
        {
            var d = Defs.Hero(h);
            float m = Lo.Atk * Lo.HeroDmg[(int)h] * Lo.RoleDmg[(int)d.Kind] * (1f + Stat(h, Eff.Dmg)) * (1f + DmgAura);
            if (Stage.BuffRole.HasValue && d.Kind == Stage.BuffRole.Value) m *= 1f + Stage.BuffPct;
            if (ForgeT > 0) m *= 1f + ForgeV;
            return m;
        }

        /// <summary>好汉攻击：伤害倍率 × 星级 × 等级带来的技能伤害。技能一下（或法师一跳）的伤害再乘系数。</summary>
        public float HeroAtk(HeroId h, int star) => HeroDmgMul(h) * Defs.StarMul[star - 1] * Lo.SkillPow[(int)h];

        public float AtkPower(HeroId h, int star) => Defs.Hero(h).Coef * HeroAtk(h, star);

        /// <summary>大招威力 = 好汉攻击 × 大招强化；CastUlt 里再乘 UltCoef。</summary>
        public float UltPower(HeroId h, int star) => HeroAtk(h, star) * (1f + Stat(h, Eff.UltPow));

        /// <summary>技能冷却：职业决定基数（射手两秒、法师十几秒），等级和辅助的冷却强化再减。</summary>
        public float SkillCd(HeroId h)
        {
            var d = Defs.Hero(h);
            float cd = d.Interval * Lo.SkillCd[(int)h];
            if (d.Kind == Role.Support) cd *= 1f - Math.Min(0.6f, Stat(h, Eff.UltCd));
            return cd;
        }

        /// <summary>辅助技能的效果倍率。</summary>
        public float BuffBoost(HeroId h) => Lo.SkillPow[(int)h] * (1f + Stat(h, Eff.UltPow));

        public float HeroSpeedMul(HeroId h)
            => Lo.AllSpeed * Lo.HeroSpeed[(int)h] * (1f + Stat(h, Eff.Speed)) * (1f + Aura) * (HasteT > 0 ? HasteMul : 1f);

        public float HeroRange(HeroId h) => Defs.Hero(h).Range + Stat(h, Eff.Range);

        public float CritRate(HeroId h) => Math.Min(0.8f, Lo.Crit + Stat(h, Eff.Crit));

        public float UltCd(HeroId h)
            => Defs.Hero(h).UltCd * Lo.UltCd * (1f - Math.Min(0.6f, Stat(h, Eff.UltCd)));

        /// <summary>控制效果：(时长, 几率)。自带必控（套索、渔网）的强化加时长，其余加几率。</summary>
        public (float dur, float chance) StunOf(HeroId h)
        {
            var d = Defs.Hero(h);
            float add = Stat(h, Eff.Stun);
            if (d.Stun > 0 && d.StunChance <= 0) return (d.Stun + add, 1f);
            float chance = d.StunChance + add;
            return (d.Stun > 0 ? d.Stun : 0.8f, Math.Min(1f, chance));
        }

        public float SlowOf(HeroId h) => Defs.Hero(h).Slow + Stat(h, Eff.Slow);
        public float AreaOf(HeroId h) => 1f + Stat(h, Eff.Area);
        public int CountOf(HeroId h) => Defs.Hero(h).Count + (int)Stat(h, Eff.Count);
        public int ChainOf(HeroId h) => Defs.Hero(h).Chain + (int)Stat(h, Eff.Chain);

        Enemy PickTarget(HeroDef def, V2 from, int skip = -1)
        {
            bool melee = def.Melee;
            float maxY = melee ? Layout.StopY + HeroRange(def.Id) : 99f;
            Enemy best = null;
            float bestScore = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Dead || e.Diving || e.Id == skip || e.Pos.Y > maxY || e.Pos.Y > Layout.EngageY) continue;
                if (melee && e.Def.Flying) continue;
                float score = e.Pos.Y + Math.Abs(e.Pos.X - from.X) * 0.25f;
                if (e.Big || e.Def.Kind == EnemyKind.Porter) score -= 6f;
                if (def.Shot == ShotKind.Lasso && e.Big) score -= 4f;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        void UpdateUnits(float dt)
        {
            foreach (var s in Slots)
            {
                var u = s.Unit;
                if (u == null) continue;
                if (u.Stun > 0) { u.Stun -= dt; continue; }
                var def = u.Def;
                float spd = HeroSpeedMul(u.Hero) * Defs.StarSpeed[u.Star - 1] * (1f + ChargeAura);
                if (u.BlessN > 0) spd *= u.BlessMul;
                u.Cd -= dt * spd;
                if (u.Cd > 0) continue;
                if (def.Buff.HasValue)
                {
                    u.Cd = Enemies.Count > 0 && CastBuff(s, u) ? SkillCd(u.Hero) : 0.5f;
                    continue;
                }
                if (Channeling(u)) { u.Cd = 0.1f; continue; }
                var target = PickTarget(def, s.Pos);
                if (target == null) { u.Cd = 0.1f; continue; }
                u.Cd = SkillCd(u.Hero);
                float dmg = AtkPower(u.Hero, u.Star);
                if (u.Burst) { dmg *= 2f; u.Burst = false; }
                if (u.BlessN > 0 && --u.BlessN == 0) u.BlessMul = 1f;
                float scale = 1f;
                float top = Stat(u.Hero, Eff.Top);
                if (top > 0 && SlotOf(u.Hero) == s) { dmg *= 1f + top; scale = 1.25f; }
                float surge = Stat(u.Hero, Eff.Surge);
                _sureCrit = false;
                if (surge > 0 && _rng.Chance(surge))
                {
                    dmg *= Defs.SurgeDmg;
                    scale *= 1.6f;
                    _sureCrit = Lo.Lv[(int)u.Hero] >= Meta.SurgePlusLv;
                    Emit(Ev.Surge, s.Pos, s.Index, 0, u.Hero);
                }
                int n0 = Shots.Count, c0 = Channels.Count;
                Fire(s, u, target, dmg);
                for (int i = n0; i < Shots.Count; i++) Grow(Shots[i], scale);
                for (int i = c0; i < Channels.Count; i++) { Channels[i].Scale = scale; Channels[i].Sure = _sureCrit; }
                _sureCrit = false;
            }
        }

        static void Grow(Shot sh, float scale)
        {
            if (scale <= 1f) return;
            sh.Scale = scale;
            sh.Radius *= 1f + (scale - 1f) * 0.6f;
        }

        bool Channeling(Unit u)
        {
            foreach (var c in Channels) if (c.Unit == u) return true;
            return false;
        }

        void UpdateChannels(float dt)
        {
            for (int i = Channels.Count - 1; i >= 0; i--)
            {
                var c = Channels[i];
                var s = Slots[c.Slot];
                if (s.Unit != c.Unit || c.Unit.Stun > 0 || c.Ticks <= 0) { Channels.RemoveAt(i); continue; }
                c.Next -= dt;
                if (c.Next > 0) continue;
                c.Next += c.Every;
                c.Ticks--;
                int n0 = Shots.Count;
                _sureCrit = c.Sure;
                ChannelTick(s, c);
                _sureCrit = false;
                for (int k = n0; k < Shots.Count; k++) Grow(Shots[k], c.Scale);
            }
        }

        /// <summary>法师持续施法的一跳：射线贯穿一条线、天雷随机劈前排、旋风原地绞、金砖砸目标。</summary>
        void ChannelTick(Slot s, Channel c)
        {
            var d = c.Unit.Def;
            var h = c.Unit.Hero;
            var t = Find(c.Target);
            if (t == null || t.Pos.Y > Layout.EngageY)
            {
                t = PickTarget(d, s.Pos);
                if (t == null) { c.Ticks = 0; return; }
                c.Target = t.Id;
            }
            float v = c.Damage;
            bool crit = RollCrit(h, ref v);
            var (stun, chance) = StunOf(h);
            float slow = SlowOf(h);
            float r = d.Radius * AreaOf(h);
            switch (d.Shot)
            {
                case ShotKind.Beam:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Beam, Hero = h, From = s.Pos, To = t.Pos, TargetId = t.Id, Dur = c.Every + 0.04f,
                        Damage = v, Radius = r, Crit = crit, Slow = slow, Stun = chance > 0 ? stun : 0, StunChance = chance,
                    });
                    break;
                case ShotKind.Lightning:
                {
                    var front = FrontTargets();
                    var p = front.Count > 0 ? front[_rng.Int(Math.Min(front.Count, 10))] : t;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Lightning, Hero = h, From = p.Pos + new V2(0, 6), To = p.Pos, TargetId = p.Id,
                        Dur = 0.08f, Damage = v, Radius = r, Pierce = 1 + ChainOf(h), Crit = crit, Slow = slow,
                        Stun = chance > 0 ? stun : 0, StunChance = chance,
                    });
                    break;
                }
                case ShotKind.Wind:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Wind, Hero = h, From = t.Pos + new V2(0, 0.3f), To = t.Pos, TargetId = t.Id,
                        Dur = c.Every, Damage = v, Radius = r, Slow = slow, Crit = crit, Stun = chance > 0 ? stun : 0, StunChance = chance,
                    });
                    break;
                default:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Stone, Hero = h, From = s.Pos, To = t.Pos, TargetId = t.Id,
                        Dur = 0.16f + V2.Dist(s.Pos, t.Pos) / 30f, Damage = v, Radius = d.Radius, Pierce = ChainOf(h),
                        Stun = chance > 0 ? stun : 0, StunChance = chance, Crit = crit, Hit = new List<int>(),
                    });
                    break;
            }
        }

        /// <summary>辅助技能：不打伤害，给队友加成。时机不对返回 false，半秒后再试。</summary>
        bool CastBuff(Slot from, Unit u)
        {
            var d = u.Def;
            var h = u.Hero;
            float boost = BuffBoost(h);
            switch (d.Buff.Value)
            {
                case UltKind.Bless:
                {
                    var pool = new List<Slot>();
                    foreach (var s in Slots)
                        if (s.Unit != null && s.Unit.Def.Carry && s.Unit.BlessN == 0) pool.Add(s);
                    if (pool.Count == 0) return false;
                    int n = u.Star >= 3 ? 2 : 1;
                    for (int i = 0; i < n && pool.Count > 0; i++)
                    {
                        int k = _rng.Int(pool.Count);
                        var s = pool[k];
                        pool.RemoveAt(k);
                        s.Unit.BlessN = 2 + u.Star;
                        s.Unit.BlessMul = 1f + d.BuffV * boost;
                        Events.Add(new BattleEvent { Type = Ev.Buff, Slot = from.Index, Hero = h, Pos = s.Pos, EnemyId = s.Index, Value = s.Unit.BlessN });
                    }
                    return true;
                }
                case UltKind.Rush:
                {
                    float k = Math.Min(0.8f, (d.BuffV + 0.1f * (u.Star - 1)) * boost);
                    foreach (var s in Slots)
                        if (s.Unit != null && s.Unit != u && s.Unit.Def.Carry) s.Unit.Cd *= 1f - k;
                    foreach (var x in Lineup)
                        if (x != h) UltTimer[(int)x] *= 1f - k;
                    break;
                }
                case UltKind.Mend:
                    if (Wall > WallMax * 0.85f && !AnyStunned()) return false;
                    HealWall(WallMax * d.BuffV * u.Star * (1f + Stat(h, Eff.Aura)) * boost, from.Pos, h);
                    foreach (var s in Slots) if (s.Unit != null) s.Unit.Stun = 0;
                    break;
                case UltKind.Forge:
                    ForgeT = 8f;
                    ForgeV = d.BuffV * (0.8f + 0.1f * u.Star) * boost;
                    break;
                case UltKind.Steal:
                {
                    int coin = (int)((20 + 10 * u.Star + Wave * 4) * boost);
                    Coins += coin;
                    Emit(Ev.Coin, from.Pos, from.Index, coin, h);
                    break;
                }
                case UltKind.Interest:
                {
                    int coin = M.Clamp((int)(Coins * d.BuffV * boost), 30, 400);
                    Coins += coin;
                    Emit(Ev.Coin, from.Pos, from.Index, coin, h);
                    break;
                }
            }
            Events.Add(new BattleEvent { Type = Ev.Buff, Slot = from.Index, Hero = h, Pos = from.Pos, EnemyId = -1 });
            return true;
        }

        bool _sureCrit;

        bool RollCrit(HeroId h, ref float dmg)
        {
            if (!_sureCrit && !_rng.Chance(CritRate(h))) return false;
            dmg *= Lo.CritDmg;
            return true;
        }

        void Fire(Slot s, Unit u, Enemy target, float dmg)
        {
            var d = u.Def;
            var h = u.Hero;
            Events.Add(new BattleEvent { Type = Ev.Fire, Slot = s.Index, Hero = h, Pos = target.Pos });
            int extra = (int)Stat(h, Eff.Count);
            if (d.Chan > 0)
            {
                float chan = d.Chan + Defs.ChanPerCount * extra;
                Channels.Add(new Channel
                {
                    Unit = u, Slot = s.Index, Target = target.Id, Every = d.Tick, Damage = dmg,
                    Ticks = Math.Max(1, (int)Math.Round(chan / d.Tick)),
                });
                return;
            }
            int shots = Defs.ShotsAt(d, u.Star);
            if (shots < d.Count) dmg *= (float)Math.Pow((float)d.Count / shots, 0.75f);
            int count = shots + extra;
            float each = extra > 0 && d.Shot != ShotKind.Lasso && d.Shot != ShotKind.Punch ? dmg * Math.Max(0.4f, 1f - 0.15f * extra) : dmg;
            float area = AreaOf(h);
            int chain = ChainOf(h);
            var (stun, chance) = StunOf(h);
            float slow = SlowOf(h);
            var used = new List<int>();
            switch (d.Shot)
            {
                case ShotKind.Arrow:
                    for (int i = 0; i < count; i++)
                    {
                        var t = i == 0 ? target : PickOther(target, used) ?? target;
                        used.Add(t.Id);
                        float v = each;
                        bool crit = RollCrit(h, ref v);
                        var shot = AddShot(ShotKind.Arrow, h, s.Pos, t, v, 0.35f, 0.18f + V2.Dist(s.Pos, t.Pos) / 26f, crit);
                        shot.Pierce = chain;
                        shot.T = -i * 0.05f;
                    }
                    break;
                case ShotKind.Bomb:
                case ShotKind.Fireball:
                    for (int i = 0; i < count; i++)
                    {
                        var t = i == 0 ? target : PickOther(target, used) ?? target;
                        used.Add(t.Id);
                        float v = each;
                        bool crit = RollCrit(h, ref v);
                        float dur = d.Shot == ShotKind.Bomb ? 0.6f : 0.35f + V2.Dist(s.Pos, t.Pos) / 24f;
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = d.Shot, Hero = h, From = s.Pos, To = Lead(t, dur), Dur = dur, T = -i * 0.08f,
                            Damage = v, Radius = d.Radius * area, Crit = crit, Split = Stat(h, Eff.Split) > 0, Knock = 0.12f,
                        });
                    }
                    break;
                case ShotKind.Lightning:
                {
                    float v = each;
                    bool crit = RollCrit(h, ref v);
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Lightning, Hero = h, From = s.Pos, To = target.Pos, TargetId = target.Id,
                        Dur = 0.1f, Damage = v, Radius = d.Radius * area, Pierce = 1 + chain, Crit = crit,
                        Stun = chance > 0 ? stun : 0, StunChance = chance, Slow = slow,
                    });
                    break;
                }
                case ShotKind.Punch:
                case ShotKind.Slam:
                    for (int i = 0; i < count; i++)
                    {
                        float v = each;
                        bool crit = RollCrit(h, ref v);
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = d.Shot, Hero = h, From = s.Pos, To = target.Pos, TargetId = target.Id, T = -i * 0.14f,
                            Dur = d.Shot == ShotKind.Punch ? 0.12f : 0.16f, Damage = v, Radius = d.Radius * area, Knock = d.Knock * (1f + 0.5f * Stat(h, Eff.Area)), Crit = crit,
                        });
                    }
                    break;
                case ShotKind.Spear:
                {
                    float v = each;
                    bool crit = RollCrit(h, ref v);
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Spear, Hero = h, From = s.Pos, To = target.Pos, TargetId = target.Id,
                        Dur = 0.1f, Damage = v, Radius = d.Radius * area, Crit = crit, Knock = d.Knock, Slow = slow,
                        Stun = chance > 0 ? stun : 0, StunChance = chance,
                    });
                    break;
                }
                case ShotKind.Stone:
                {
                    float v = each;
                    bool crit = RollCrit(h, ref v);
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Stone, Hero = h, From = s.Pos, To = target.Pos, TargetId = target.Id,
                        Dur = 0.16f + V2.Dist(s.Pos, target.Pos) / 30f, Damage = v, Radius = d.Radius, Pierce = chain,
                        Stun = chance > 0 ? stun : 0, StunChance = chance, Crit = crit, Hit = new List<int>(),
                    });
                    break;
                }
                case ShotKind.Wind:
                {
                    float v = each;
                    bool crit = RollCrit(h, ref v);
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Wind, Hero = h, From = s.Pos, To = target.Pos, TargetId = target.Id,
                        Dur = 0.2f + V2.Dist(s.Pos, target.Pos) / 22f, Damage = v, Radius = d.Radius * area, Slow = slow, Crit = crit,
                        Stun = chance > 0 ? stun : 0, StunChance = chance,
                    });
                    break;
                }
                case ShotKind.Lasso:
                    for (int i = 0; i < count; i++)
                    {
                        var t = i == 0 ? target : PickOther(target, used);
                        if (t == null) break;
                        used.Add(t.Id);
                        float v = each;
                        bool crit = RollCrit(h, ref v);
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Lasso, Hero = h, From = s.Pos, To = t.Pos, TargetId = t.Id, T = -i * 0.08f,
                            Dur = 0.22f, Damage = v, Radius = 0.4f, Stun = stun, StunChance = 1f, Crit = crit,
                        });
                    }
                    break;
            }
        }

        Enemy PickOther(Enemy first, List<int> used)
        {
            Enemy best = null;
            float bestY = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Dead || used.Contains(e.Id) || e == first || e.Pos.Y > Layout.EngageY) continue;
                if (e.Pos.Y < bestY) { bestY = e.Pos.Y; best = e; }
            }
            return best;
        }

        V2 Lead(Enemy e, float t)
        {
            if (e.Stun > 0 || e.Attacking) return e.Pos;
            float y = Math.Max(e.Def.StopY, e.Pos.Y - EnemySpeed(e) * t);
            return new V2(e.Pos.X, y);
        }

        Shot AddShot(ShotKind k, HeroId h, V2 from, Enemy t, float dmg, float r, float dur, bool crit = false, bool ult = false)
        {
            var s = new Shot
            {
                Id = _nextId++, Kind = k, Hero = h, From = from, To = t.Pos, TargetId = t.Id, Dur = dur, Damage = dmg, Radius = r,
                Crit = crit, Ult = ult,
            };
            Shots.Add(s);
            return s;
        }

        Enemy Find(int id)
        {
            foreach (var e in Enemies) if (e.Id == id && !e.Dead) return e;
            return null;
        }

        void UpdateShots(float dt)
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                if (s.T < 0)
                {
                    s.T += dt;
                    continue;
                }
                s.T += dt;
                if (s.TargetId >= 0 && (s.Kind == ShotKind.Arrow || s.Kind == ShotKind.Stone || s.Kind == ShotKind.Wind || s.Kind == ShotKind.Lasso || s.Kind == ShotKind.Beam))
                {
                    var t = Find(s.TargetId);
                    if (t != null) s.To = t.Pos;
                }
                if (s.T < s.Dur) continue;
                Shots.RemoveAt(i);
                Land(s);
            }
        }

        bool TryStun(Enemy e, Shot s, float bigMul = 0f)
        {
            if (s.Stun <= 0 || s.StunChance <= 0) return false;
            if (e.Big && bigMul <= 0) return false;
            if (s.StunChance < 1f && !_rng.Chance(s.StunChance)) return false;
            e.Stun = Math.Max(e.Stun, e.Big ? s.Stun * bigMul : s.Stun);
            return true;
        }

        void Land(Shot s)
        {
            switch (s.Kind)
            {
                case ShotKind.Slash:
                {
                    var t = Find(s.TargetId);
                    if (t == null) return;
                    DamageLord(t, s.Damage, s.Crit);
                    if (!t.Dead && !t.Def.IsBoss) TryStun(t, s);
                    Events.Add(new BattleEvent { Type = Ev.Hit, Pos = t.Pos, Shot = s.Kind, Radius = 0.6f, Crit = s.Crit, EnemyId = t.Id });
                    return;
                }
                case ShotKind.Bolt:
                case ShotKind.Ball:
                case ShotKind.Boulder:
                    HitWall(s.Damage, s.To);
                    if (s.Kind != ShotKind.Bolt) Events.Add(new BattleEvent { Type = Ev.Explode, Pos = s.To, Radius = s.Kind == ShotKind.Ball ? 1.4f : 0.9f });
                    return;
                case ShotKind.Hammer:
                {
                    var slot = s.TargetId >= 0 && s.TargetId < Slots.Count ? Slots[s.TargetId] : null;
                    if (slot?.Unit != null)
                    {
                        slot.Unit.Stun = Math.Max(slot.Unit.Stun, s.Stun);
                        Events.Add(new BattleEvent { Type = Ev.UnitStun, Pos = slot.Pos, Slot = slot.Index, Radius = s.Stun });
                    }
                    Events.Add(new BattleEvent { Type = Ev.Hit, Pos = s.To, Shot = s.Kind, Radius = 0.8f });
                    return;
                }
                case ShotKind.Arrow:
                {
                    var t = Find(s.TargetId);
                    if (t == null) t = Nearest(s.To, 0.6f);
                    if (t != null) Damage(t, s.Damage, s.Hero, s.Crit);
                    if (s.Pierce > 0)
                    {
                        var hit = Around(s.To, 1.2f, t?.Id ?? -1, s.Pierce);
                        foreach (var e in hit) Damage(e, s.Damage * 0.8f, s.Hero, false);
                    }
                    EmitHit(s, s.Radius);
                    return;
                }
                case ShotKind.Beam:
                {
                    var a = s.From;
                    var ab = s.To - a;
                    float len2 = Math.Max(0.01f, ab.Len2);
                    foreach (var e in Enemies)
                    {
                        if (e.Dead || e.Pos.Y > Layout.EngageY) continue;
                        var ap = e.Pos - a;
                        float k = Math.Max(0f, Math.Min(1f, (ap.X * ab.X + ap.Y * ab.Y) / len2));
                        float rr = s.Radius + e.Def.Size * 0.2f;
                        if ((e.Pos - (a + ab * k)).Len2 > rr * rr) continue;
                        bool main = e.Id == s.TargetId;
                        Damage(e, main ? s.Damage : s.Damage * 0.6f, s.Hero, s.Crit && main);
                        if (s.Slow > 0) e.SlowT = Math.Max(e.SlowT, s.Slow);
                        TryStun(e, s);
                    }
                    Events.Add(new BattleEvent { Type = Ev.Hit, Pos = s.To, Hero = s.Hero, Shot = s.Kind, Radius = 0.35f, Crit = s.Crit });
                    return;
                }
                case ShotKind.Bomb:
                case ShotKind.MiniBomb:
                case ShotKind.Fireball:
                    Splash(s, s.To, s.Radius, s.Damage, s.Knock);
                    EmitHit(s, s.Radius);
                    if (s.Split)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            float ang = (float)(k * Math.PI * 2 / 3 + _rng.F());
                            var to = s.To + new V2((float)Math.Cos(ang), (float)Math.Sin(ang)) * _rng.Range(0.9f, 1.5f);
                            Shots.Add(new Shot { Id = _nextId++, Kind = ShotKind.MiniBomb, Hero = s.Hero, From = s.To, To = to, Dur = 0.35f, Damage = s.Damage * 0.4f, Radius = 0.6f });
                        }
                    }
                    return;
                case ShotKind.Lightning:
                {
                    Splash(s, s.To, s.Radius, s.Damage, 0);
                    EmitHit(s, s.Radius);
                    if (s.Pierce > 1)
                    {
                        var chain = Around(s.To, 2.4f, s.TargetId, s.Pierce - 1);
                        V2 from = s.To;
                        float delay = 0;
                        foreach (var e in chain)
                        {
                            delay += 0.06f;
                            Shots.Add(new Shot
                            {
                                Id = _nextId++, Kind = ShotKind.Lightning, Hero = s.Hero, From = from, To = e.Pos, TargetId = e.Id,
                                T = -delay, Dur = 0.05f, Damage = s.Damage * 0.6f, Radius = 0.45f, Pierce = 0, Stun = s.Stun, StunChance = s.StunChance, Slow = s.Slow,
                            });
                            from = e.Pos;
                        }
                    }
                    return;
                }
                case ShotKind.Punch:
                case ShotKind.Slam:
                    Splash(s, s.To, s.Radius, s.Damage, s.Knock, true);
                    EmitHit(s, s.Radius);
                    return;
                case ShotKind.Spear:
                {
                    // 一枪贯穿目标和它身后同一列的敌人。
                    foreach (var e in Enemies)
                    {
                        if (e.Dead || e.Def.Flying) continue;
                        float half = s.Radius + e.Def.Size * 0.2f;
                        if (Math.Abs(e.Pos.X - s.To.X) > half || e.Pos.Y < s.To.Y - 0.5f || e.Pos.Y > s.To.Y + 2.2f) continue;
                        Damage(e, e.Id == s.TargetId || s.Ult ? s.Damage : s.Damage * 0.7f, s.Hero, s.Crit && e.Id == s.TargetId);
                        if (s.Slow > 0) e.SlowT = Math.Max(e.SlowT, s.Slow);
                        TryStun(e, s, s.Ult ? 0.4f : 0f);
                        if (s.Knock > 0 && !e.Big && e.Def.Kind != EnemyKind.Porter) { e.Pos.Y = Math.Min(Layout.SpawnY - 0.5f, e.Pos.Y + s.Knock); e.Attacking = false; }
                    }
                    EmitHit(s, s.Radius);
                    return;
                }
                case ShotKind.Stone:
                {
                    var t = Find(s.TargetId) ?? Nearest(s.To, 0.6f);
                    if (t != null)
                    {
                        Damage(t, s.Damage, s.Hero, s.Crit);
                        TryStun(t, s, s.Ult ? 0.5f : 0f);
                        s.Hit.Add(t.Id);
                    }
                    EmitHit(s, s.Radius);
                    if (s.Pierce > 0)
                    {
                        Enemy next = null;
                        float bd = 2.8f * 2.8f;
                        foreach (var e in Enemies)
                        {
                            if (e.Dead || s.Hit.Contains(e.Id) || e.Pos.Y > Layout.EngageY) continue;
                            float d = (e.Pos - s.To).Len2;
                            if (d < bd) { bd = d; next = e; }
                        }
                        if (next != null)
                            Shots.Add(new Shot
                            {
                                Id = _nextId++, Kind = ShotKind.Stone, Hero = s.Hero, From = s.To, To = next.Pos, TargetId = next.Id,
                                Dur = 0.14f, Damage = s.Damage * 0.8f, Radius = s.Radius, Pierce = s.Pierce - 1, Stun = s.Stun, StunChance = s.StunChance,
                                Hit = s.Hit, Ult = s.Ult,
                            });
                    }
                    return;
                }
                case ShotKind.Wind:
                {
                    foreach (var e in Enemies)
                    {
                        if (e.Dead) continue;
                        float rr = s.Radius + e.Def.Size * 0.25f;
                        if ((e.Pos - s.To).Len2 > rr * rr) continue;
                        Damage(e, e.Id == s.TargetId || s.Ult ? s.Damage : s.Damage * 0.5f, s.Hero, s.Crit && e.Id == s.TargetId);
                        if (s.Slow > 0) e.SlowT = Math.Max(e.SlowT, s.Slow);
                        TryStun(e, s);
                    }
                    EmitHit(s, s.Radius);
                    return;
                }
                case ShotKind.Lasso:
                {
                    var t = Find(s.TargetId) ?? Nearest(s.To, 0.8f);
                    if (t != null)
                    {
                        Damage(t, s.Damage, s.Hero, s.Crit);
                        TryStun(t, s, 0.4f);
                    }
                    EmitHit(s, s.Radius);
                    return;
                }
                case ShotKind.Frost:
                    foreach (var e in Enemies)
                    {
                        if (e.Dead) continue;
                        float rr = s.Radius + e.Def.Size * 0.25f;
                        if ((e.Pos - s.To).Len2 > rr * rr) continue;
                        Damage(e, s.Damage, s.Hero, false);
                        if (!e.Big) e.Stun = Math.Max(e.Stun, s.Stun);
                        e.SlowT = Math.Max(e.SlowT, 3f);
                    }
                    EmitHit(s, s.Radius);
                    return;
            }
        }

        void EmitHit(Shot s, float r) => Events.Add(new BattleEvent { Type = Ev.Hit, Pos = s.To, Hero = s.Hero, Shot = s.Kind, Radius = r, Crit = s.Crit || s.Ult });

        Enemy Nearest(V2 p, float r)
        {
            Enemy best = null;
            float bd = r * r;
            foreach (var e in Enemies)
            {
                if (e.Dead) continue;
                float d = (e.Pos - p).Len2;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        List<Enemy> Around(V2 p, float r, int skip, int max)
        {
            var list = new List<Enemy>();
            foreach (var e in Enemies)
                if (!e.Dead && e.Id != skip && (e.Pos - p).Len2 < r * r) list.Add(e);
            list.Sort((a, b) => (a.Pos - p).Len2.CompareTo((b.Pos - p).Len2));
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        void Splash(Shot s, V2 p, float r, float dmg, float knock, bool ground = false)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead || (ground && e.Def.Flying)) continue;
                float rr = r + e.Def.Size * 0.25f;
                if ((e.Pos - p).Len2 > rr * rr) continue;
                Damage(e, dmg, s.Hero, false);
                TryStun(e, s, s.Ult ? 0.4f : 0f);
                if (s.Slow > 0) e.SlowT = Math.Max(e.SlowT, s.Slow);
                if (knock > 0 && !e.Big && e.Def.Kind != EnemyKind.Porter && e.Def.Kind != EnemyKind.Catapult && e.Def.Kind != EnemyKind.Brute && !e.Def.Flying)
                {
                    e.Pos.Y = Math.Min(Layout.SpawnY - 0.5f, e.Pos.Y + knock);
                    e.Attacking = false;
                }
            }
        }

        /// <summary>克制关系：重甲减物理、怕法术；飞行被射手克制。</summary>
        public static float Affinity(EnemyDef e, HeroDef h)
        {
            float m = 1f;
            if (h.Physical) m *= 1f - e.PhysResist;
            else m *= 1f + e.MagicWeak;
            if (h.Kind == Role.Archer) m *= 1f + e.ArcherWeak;
            return m;
        }

        void Damage(Enemy e, float dmg, HeroId h, bool crit)
        {
            if (e.Dead) return;
            dmg *= Affinity(e.Def, Defs.Hero(h));
            if (e.Big) dmg *= 1f + Stat(h, Eff.Elite);
            if (e.Def.Flying) dmg *= 1f + Stat(h, Eff.Fly);
            if (e.Mounted) dmg *= 1f + Stat(h, Eff.Cav);
            Deal(e, dmg * MetaMul(e), h, crit, false);
        }

        /// <summary>局外的全军伤害、对精英/首领加成（杏黄旗词条、天书）。</summary>
        float MetaMul(Enemy e) => Lo.DmgMul * (e.Def.IsBoss ? Lo.BossDmg : e.Def.Rank == EnemyRank.Elite ? Lo.EliteDmg : 1f);

        /// <summary>寨主剑气：物理伤害，不吃好汉强化。</summary>
        void DamageLord(Enemy e, float dmg, bool crit)
        {
            if (e.Dead) return;
            if (e.Big) dmg *= Lo.LordBig;
            Deal(e, dmg * (1f - e.Def.PhysResist) * MetaMul(e), Lineup[0], crit, true);
        }

        void Deal(Enemy e, float dmg, HeroId h, bool crit, bool lord)
        {
            if (e.Diving) return;
            if (e.RageT > 0) dmg *= 0.5f;
            if (e.Guard > 0)
            {
                e.Guard--;
                e.HitT = 0.12f;
                Events.Add(new BattleEvent { Type = Ev.Guard, Pos = e.Pos, EnemyId = e.Id, Value = e.Guard });
                return;
            }
            if (e.Def.Kind == EnemyKind.Scout && e.AbilT <= 0 && !e.Attacking && _rng.Chance(0.25f))
            {
                e.AbilT = 1.5f;
                e.Pos.Y = Math.Max(e.Def.StopY, e.Pos.Y - 1.2f);
                e.Pos.X = M.Clamp(e.Pos.X + _rng.Range(-0.8f, 0.8f), -Layout.HalfW + 1.1f, Layout.HalfW - 1.1f);
                Events.Add(new BattleEvent { Type = Ev.Blink, Pos = e.Pos, EnemyId = e.Id });
                return;
            }
            if (e.Def.Kind == EnemyKind.Berserker && !e.Raged && e.Hp - dmg < e.MaxHp * 0.3f)
            {
                e.Raged = true;
                e.RageT = 2f;
                dmg = Math.Min(dmg, e.Hp - 1f);
                Events.Add(new BattleEvent { Type = Ev.Berserk, Pos = e.Pos, EnemyId = e.Id });
            }
            if (e.Shield > 0)
            {
                float a = Math.Min(e.Shield, dmg);
                e.Shield -= a;
                dmg -= a;
                if (dmg <= 0) { e.HitT = 0.12f; return; }
            }
            float dealt = Math.Min(dmg, e.Hp);
            e.Hp -= dmg;
            e.HitT = 0.12f;
            if (lord) LordDamage += dealt;
            else DamageBy[(int)h] += dealt;
            Events.Add(new BattleEvent { Type = Ev.Damage, Pos = e.Pos, Value = (int)Math.Ceiling(dmg), Hero = h, Crit = crit, EnemyId = e.Id, Shot = lord ? ShotKind.Slash : default });
            if (e.Hp > 0) return;
            e.Dead = true;
            Kills++;
            int coin = (int)Math.Round(e.Def.Coin * Defs.ToughCoin(e.Tough) * (1f + CoinAura));
            Coins += coin;
            Events.Add(new BattleEvent { Type = Ev.Kill, Pos = e.Pos, Value = coin, EnemyId = e.Id, Hero = h });
            switch (e.Def.Kind)
            {
                case EnemyKind.Porter:
                    PortersKilled++;
                    Events.Add(new BattleEvent { Type = Ev.PorterKilled, Pos = e.Pos, EnemyId = e.Id });
                    break;
                case EnemyKind.FireBomb:
                    Events.Add(new BattleEvent { Type = Ev.Explode, Pos = e.Pos, EnemyId = e.Id, Radius = 1.3f });
                    Splash(new Shot { Hero = h }, e.Pos, 1.3f, e.MaxHp * 3f, 0.3f);
                    break;
                case EnemyKind.Brute:
                    _bruteDrops.Add(e.Pos);
                    break;
            }
            if (e.Def.Rank == EnemyRank.Elite) ElitesKilled++;
            if (e.Def.IsBoss) BossesKilled++;
            // 生辰纲：精英、首领倒下必掉，首领那一次四格必中。
            if (e.Big && Stage.Mode != StageMode.Escort)
            {
                PendingReels++;
                if (e.Def.IsBoss) JackpotReels++;
                Events.Add(new BattleEvent { Type = Ev.Treasure, Pos = e.Pos, EnemyId = e.Id, Value = e.Def.IsBoss ? 1 : 0 });
            }
        }

        // ---------------- 寨主技能 ----------------

        public float LordPower => Lo.Atk * Lo.LordPct * Lo.LordMul;
        /// <summary>天书连斩时每道剑气间隔秒数。</summary>
        public const float LordHitGap = 0.45f;

        void UpdateLord(float dt)
        {
            LordT -= dt;
            if (LordT > 0) return;
            bool any = false;
            foreach (var e in Enemies)
                if (!e.Dead && !e.Escaped && Layout.InLordRange(e.Pos)) { any = true; break; }
            // 一直等的话，冷却圈会停在满圈上，看起来像卡住。过了宽限就空过这一轮，圈按下一轮冷却重新转。
            if (!any && LordT > -Defs.LordHold) return;
            LordDur = Math.Max(0.5f, Lo.LordCd);
            LordT = LordDur;
            if (!any) return;
            LordCasts++;
            float y0 = Layout.WallRowY;
            int hits = Math.Max(1, Lo.LordHits);
            for (int k = 0; k < hits; k++)
                foreach (var e in Enemies)
                {
                    if (e.Dead || e.Escaped || !Layout.InLordRange(e.Pos)) continue;
                    float dmg = LordPower;
                    bool crit = _rng.Chance(Math.Min(0.8f, Lo.Crit));
                    if (crit) dmg *= Lo.CritDmg;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Slash, From = new V2(0, y0), To = e.Pos, TargetId = e.Id,
                        T = -Math.Max(0, e.Pos.Y - y0) / Defs.LordWaveSpeed - k * LordHitGap, Dur = 0.01f, Damage = dmg, Crit = crit, Ult = true,
                        Stun = Lo.LordStun, StunChance = Lo.LordStun > 0 ? 1f : 0f,
                    });
                }
            Events.Add(new BattleEvent { Type = Ev.LordSkill, Pos = new V2(0, y0), Slot = hits, Value = LordCasts });
        }

        // ---------------- 大招 ----------------

        void UpdateUlts(float dt)
        {
            foreach (var hero in Lineup)
            {
                int h = (int)hero;
                if (!HasUlt(hero)) continue;
                var best = SlotOf(hero);
                if (best == null || best.Unit.Stun > 0) continue;
                UltTimer[h] -= dt * HeroSpeedMul(hero) * (1f + ChargeAura);
                if (UltTimer[h] > 0 || Enemies.Count == 0) continue;
                var def = Defs.Hero(hero);
                if (def.Ult == UltKind.Heal && Wall > WallMax * 0.85f && !AnyStunned()) { UltTimer[h] = 0.5f; continue; }
                var c = Densest(def.Melee ? Layout.StopY + HeroRange(hero) : 99f, def.Melee);
                if (c == null) { UltTimer[h] = 0.5f; continue; }
                UltTimer[h] = UltCd(hero);
                CastUlt(hero, best, c);
            }
        }

        bool AnyStunned()
        {
            foreach (var s in Slots) if (s.Unit != null && s.Unit.Stun > 0) return true;
            return false;
        }

        Enemy Densest(float maxY, bool ground)
        {
            Enemy best = null;
            int bestN = -1;
            foreach (var a in Enemies)
            {
                if (a.Dead || a.Pos.Y > maxY || a.Pos.Y > Layout.EngageY - 0.3f || (ground && a.Def.Flying)) continue;
                int n = 0;
                foreach (var b in Enemies)
                    if (!b.Dead && (a.Pos - b.Pos).Len2 < 2.25f) n++;
                if (a.Big) n += 4;
                if (n > bestN) { bestN = n; best = a; }
            }
            return best;
        }

        List<Enemy> FrontTargets()
        {
            var targets = new List<Enemy>(Enemies);
            targets.RemoveAll(e => e.Dead || e.Pos.Y > Layout.EngageY - 0.3f);
            targets.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));
            return targets;
        }

        /// <summary>首领 &gt; 精英 &gt; 血最多。</summary>
        Enemy Strongest()
        {
            Enemy best = null;
            float bs = -1;
            foreach (var e in Enemies)
            {
                if (e.Dead || e.Pos.Y > Layout.EngageY - 0.3f) continue;
                float s = e.Hp + (e.Def.IsBoss ? 1e7f : e.Big ? 1e6f : 0);
                if (s > bs) { bs = s; best = e; }
            }
            return best;
        }

        void CastUlt(HeroId hero, Slot from, Enemy focus)
        {
            var u = from.Unit;
            var def = u.Def;
            float power = UltPower(hero, u.Star) * def.UltCoef;
            float boost = 1f + Stat(hero, Eff.UltPow);
            var at = focus.Pos;
            Events.Add(new BattleEvent { Type = Ev.Ult, Slot = from.Index, Hero = hero, Pos = at, Value = u.Star });
            switch (def.Ult)
            {
                case UltKind.Thunder:
                {
                    var targets = FrontTargets();
                    for (int i = 0; i < def.UltN; i++)
                    {
                        V2 p = i == 0 ? at : targets.Count > 0 ? targets[_rng.Int(Math.Min(targets.Count, 14))].Pos : at;
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Lightning, Hero = hero, From = p + new V2(0, 6), To = p, T = -i * 0.16f,
                            Dur = 0.08f, Damage = power, Radius = def.UltRadius, Stun = def.UltStun, StunChance = 1f, Ult = true,
                        });
                    }
                    break;
                }
                case UltKind.Frost:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Frost, Hero = hero, From = from.Pos, To = at, Dur = 0.35f,
                        Damage = power, Radius = def.UltRadius, Stun = def.UltStun, Ult = true,
                    });
                    break;
                case UltKind.Sweep:
                    for (int i = 0; i < def.UltN; i++)
                    {
                        var p = new V2(-3.6f + i * 7.2f / Math.Max(1, def.UltN - 1), Layout.StopY + 1.1f);
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Slam, Hero = hero, From = from.Pos, To = p, T = -i * 0.1f, Dur = 0.1f,
                            Damage = power, Radius = 1.5f, Knock = 1.2f, Ult = true,
                        });
                    }
                    break;
                case UltKind.Haste:
                    HasteT = Math.Max(HasteT, 8f * boost);
                    HasteMul = Math.Max(HasteMul, 1.6f);
                    break;
                case UltKind.Heal:
                    HealWall(WallMax * def.UltCoef * boost * (0.7f + 0.1f * u.Star), from.Pos, hero);
                    foreach (var s in Slots) if (s.Unit != null) s.Unit.Stun = 0;
                    break;
                case UltKind.Bind:
                {
                    var targets = new List<Enemy>(Enemies);
                    targets.RemoveAll(e => e.Dead || e.Pos.Y > Layout.EngageY - 0.3f);
                    targets.Sort((a, b) => (b.Big ? 1 : 0).CompareTo(a.Big ? 1 : 0) != 0 ? (b.Big ? 1 : 0).CompareTo(a.Big ? 1 : 0) : a.Pos.Y.CompareTo(b.Pos.Y));
                    int n = def.UltN + (int)Stat(hero, Eff.Count);
                    for (int i = 0; i < n && i < targets.Count; i++)
                    {
                        var t = targets[i];
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Lasso, Hero = hero, From = from.Pos, To = t.Pos, TargetId = t.Id, T = -i * 0.07f,
                            Dur = 0.25f, Damage = power, Radius = 0.5f, Stun = def.UltStun + Stat(hero, Eff.Stun), StunChance = 1f, Ult = true,
                        });
                    }
                    break;
                }
                case UltKind.Snipe:
                {
                    var t = Strongest() ?? focus;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Arrow, Hero = hero, From = from.Pos, To = t.Pos, TargetId = t.Id,
                        Dur = 0.3f, Damage = power, Radius = 0.5f, Crit = true, Ult = true,
                    });
                    break;
                }
                case UltKind.Tornado:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Wind, Hero = hero, From = from.Pos, To = at, Dur = 0.45f,
                        Damage = power, Radius = def.UltRadius * (1f + Stat(hero, Eff.Area) * 0.5f), Slow = 3f,
                        Stun = def.UltStun, StunChance = 1f, Ult = true,
                    });
                    break;
                case UltKind.Meteor:
                {
                    var targets = FrontTargets();
                    for (int i = 0; i < def.UltN; i++)
                    {
                        V2 p = i == 0 ? at : targets.Count > 0 ? targets[_rng.Int(Math.Min(targets.Count, 12))].Pos : at;
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Fireball, Hero = hero, From = p + new V2(-1.2f, 6f), To = p, T = -i * 0.18f,
                            Dur = 0.5f, Damage = power, Radius = def.UltRadius, Knock = 0.4f, Ult = true,
                        });
                    }
                    break;
                }
            }
        }
    }
}

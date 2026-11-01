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
        public HeroDef Def => Defs.Hero(Hero);
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
        public V2 Pos;
        public float LaneX;
        public float SlowT, Stun, AtkCd;
        /// <summary>八门金锁：易伤剩余时间。</summary>
        public float VulnT;
        public bool Attacking, Dead, Escaped;
        public float HitT;
        /// <summary>首领：技能计时、冲锋、护盾。</summary>
        public float SkillT, ChargeT, Shield;
        public bool ShieldUsed;
        public bool Big => Def.Big;
        public bool Mounted => Def.Kind == EnemyKind.Cavalry || Def.Kind == EnemyKind.ChainHorse || Def.Kind == EnemyKind.HuYan;
    }

    public enum ShotKind { Arrow, FireArrow, Bomb, BigBomb, MiniBomb, Bolt, Lightning, Punch, Stone, Spear, Slam, Wind, Frost, Hammer, Ball, Fireball, Lasso }

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
    }

    public enum BossSkill { Hammer = 1, Charge, Summon, Ball, Shield }

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

    public enum Reel { Silver, Book, Banner, Wall }

    public sealed class Battle
    {
        public readonly HeroId[] Lineup;
        public readonly Loadout Lo;
        public readonly List<Slot> Slots = new List<Slot>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly Dictionary<string, int> Perks = new Dictionary<string, int>();
        public readonly float[] DamageBy = new float[Defs.HeroCount];
        public readonly float[] UltTimer = new float[Defs.HeroCount];
        public readonly HashSet<EnemyKind> Seen = new HashSet<EnemyKind>();
        readonly float[,] _stat = new float[Defs.HeroCount, Defs.EffCount];
        readonly bool[] _starDone = new bool[Defs.HeroCount];

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
        public int FreeEnhances;
        public int PendingReels, JackpotReels;
        public int PortersKilled, ElitesKilled, BossesKilled;
        /// <summary>急攻（运筹帷幄、神行甲马）、神兵（打造神兵）剩余时间与倍率。</summary>
        public float HasteT, HasteMul = 1f, ForgeT, ForgeV;
        /// <summary>辅助光环：攻速、伤害、大招充能、击杀银两加成、每 4 秒利息比例。</summary>
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
            foreach (var h in Lineup) UltTimer[(int)h] = 4f;
            RebuildStats();
        }

        public Rng Rng => _rng;
        public int SummonCost => Defs.SummonCost(Summons);
        public int EnhanceCost => Defs.EnhanceCost(Enhances);
        public int SummonsToNextSlot => Slots.Count >= Layout.MaxSlots ? 0 : Defs.SummonsPerSlot - Recruited % Defs.SummonsPerSlot;
        public float WaveProgress => Wave == 0 ? 0 : M.Clamp01((Time - (Wave - 1) * Defs.WaveTime) / Defs.WaveTime);
        public bool AllSpawned => Wave >= Stage.Waves && _queue.Count == 0;
        public int Level(string perk) => Perks.TryGetValue(perk, out var n) ? n : 0;
        public bool HasUlt(HeroId h) => Lo.Ult[(int)h];
        public bool InLineup(HeroId h) => Array.IndexOf(Lineup, h) >= 0;
        public bool StarTaken(HeroId h) => _starDone[(int)h];

        /// <summary>这位好汉身上某种效果的总量：已选强化 × 等级 + 5 级被动。</summary>
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
            slot.Unit = new Unit { Id = _nextId++, Hero = Lineup[_rng.Int(Lineup.Length)], Star = star, Cd = 0.25f };
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
                sb.Unit = new Unit { Id = _nextId++, Hero = MergeResult(star), Star = star, Cd = 0, Burst = true };
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
            pool.RemoveAll(p => Level(p.Id) >= p.Max);
            if (pool.Count == 0) return;
            Choices = pool.ToArray();
            StarPick = h;
            Phase = Phase.Choosing;
            Emit(Ev.StarPick, at.Pos, at.Index, 4, h);
        }

        public bool CanEnhance => Phase == Phase.Running && (FreeEnhances > 0 || Coins >= EnhanceCost) && RollChoices(false) != null;

        public bool Enhance()
        {
            if (Phase != Phase.Running) return false;
            bool free = FreeEnhances > 0;
            if (!free && Coins < EnhanceCost) return false;
            var c = RollChoices(true);
            if (c == null) return false;
            if (free) FreeEnhances--;
            else
            {
                Coins -= EnhanceCost;
                Enhances++;
            }
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
            var p = Choices[i];
            Perks[p.Id] = Level(p.Id) + 1;
            RebuildStats();
            Choices = null;
            StarPick = null;
            Phase = Phase.Running;
            Emit(Ev.Enhance, default, -1, p.Star ? 1 : 0, p.Hero);
        }

        PerkDef[] RollChoices(bool consume)
        {
            var pool = new List<PerkDef>();
            foreach (var p in Defs.Perks)
            {
                if (p.Star || Level(p.Id) >= p.Max || !InLineup(p.Hero)) continue;
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

        /// <summary>老虎机：四个格子，每格一种奖励。四格相同翻倍；首领掉的那次必中四格相同。</summary>
        public Reel[] SpinReels()
        {
            if (Phase != Phase.Reels) return null;
            var r = new Reel[4];
            bool jackpot = JackpotReels > 0 || _rng.Chance(0.16f);
            var first = (Reel)_rng.Int(4);
            for (int i = 0; i < 4; i++) r[i] = jackpot ? first : (Reel)_rng.Int(4);
            return r;
        }

        public bool NextIsJackpot => JackpotReels > 0;
        public int ReelSilver => 40 + Wave * 18;

        public void ApplyReels(Reel[] r)
        {
            bool all = r[0] == r[1] && r[1] == r[2] && r[2] == r[3];
            int mul = all ? 2 : 1;
            int banners = 0;
            foreach (var x in r)
            {
                switch (x)
                {
                    case Reel.Silver: Coins += ReelSilver * mul; break;
                    case Reel.Book: FreeEnhances += 1; break;
                    case Reel.Banner: banners += mul; break;
                    case Reel.Wall: Wall = Math.Min(WallMax, Wall + 500 * mul); break;
                }
            }
            PendingReels = Math.Max(0, PendingReels - 1);
            if (JackpotReels > 0) JackpotReels--;
            Phase = Phase.Running;
            for (int i = 0; i < banners; i++)
                if (Summon(true, all ? 2 : 1) == null) Coins += ReelSilver;
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
            UpdateUlts(dt);
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
            if (AllSpawned && Enemies.Count == 0 && Shots.Count == 0)
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
            ChargeAura = Math.Min(0.6f, charge);
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
        }

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
            // 一波分两到三拨刷，中间留口气。
            int bursts = w <= 2 ? 2 : 3;
            for (int i = 0; i < troops.Count; i++)
            {
                int b = i * bursts / troops.Count;
                float start = t0 + 0.6f + b * (Defs.WaveTime * 0.72f / bursts);
                float spread = Defs.WaveTime * 0.72f / bursts * 0.7f;
                _queue.Add((start + _rng.F() * spread, troops[i], _rng.Int(2)));
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
            float hp = def.Hp * Defs.HpScale(Wave) * Stage.HpMul * (def.IsBoss ? Stage.BossMul : 1f);
            float gx = gate < 0 ? 0 : Layout.GateX[gate];
            var e = new Enemy
            {
                Id = _nextId++, Def = def, Hp = hp, MaxHp = hp,
                Pos = at ?? new V2(gx + _rng.Range(-0.5f, 0.5f), Layout.SpawnY + _rng.Range(0f, 0.4f)),
                LaneX = def.IsBoss ? 0 : _rng.Range(-3.7f, 3.7f),
                AtkCd = _rng.Range(0.2f, 1f),
                SkillT = 4f,
            };
            if (kind == EnemyKind.GaoQiu) e.Shield = hp * 0.2f;
            Seen.Add(kind);
            Enemies.Add(e);
            return e;
        }

        float EnemySpeed(Enemy e)
        {
            float s = e.Def.Speed * (Rage ? 1.3f : 1f);
            if (e.SlowT > 0) s *= 0.55f;
            if (e.ChargeT > 0) s *= 5f;
            if (e.Def.Kind == EnemyKind.ShiWen && e.Hp < e.MaxHp * 0.5f) s *= 1.3f;
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
                if (e.VulnT > 0) e.VulnT -= dt;
                if (e.ChargeT > 0) e.ChargeT -= dt;
                if (e.Def.IsBoss && e.Pos.Y < Layout.EngageY) BossSkills(e, dt);
                if (e.Stun > 0) { e.Stun -= dt; continue; }
                float stop = e.Def.StopY + (e.Big ? e.Def.Size * 0.18f : 0f);
                if (e.Pos.Y > stop)
                {
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
                if (e.Def.Kind == EnemyKind.FireBomb)
                {
                    e.Escaped = true;
                    HitWall(e.Def.WallDps * atkMul, e.Pos);
                    Events.Add(new BattleEvent { Type = Ev.Explode, Pos = e.Pos, EnemyId = e.Id, Radius = 1.2f });
                    continue;
                }
                e.Attacking = true;
                e.AtkCd -= dt;
                if (e.AtkCd > 0) continue;
                float period = e.Def.Kind == EnemyKind.Crossbow ? 1.6f : 1f;
                e.AtkCd = period;
                float dmg = e.Def.WallDps * period * atkMul;
                if (e.Def.Kind == EnemyKind.ShiWen && e.Hp < e.MaxHp * 0.5f) dmg *= 1.25f;
                if (e.Def.Kind == EnemyKind.Crossbow)
                {
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Bolt, From = e.Pos,
                        To = new V2(e.Pos.X, Layout.WallRowY + 0.3f), Dur = 0.55f, Damage = dmg,
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
                default:
                    e.SkillT = 99f;
                    return;
            }
        }

        void HitWall(float dmg, V2 pos)
        {
            dmg *= 1f - Lo.WallResist;
            Wall -= dmg;
            Events.Add(new BattleEvent { Type = Ev.WallHit, Pos = pos, Value = (int)dmg });
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

        public float AtkPower(HeroId h, int star) => Defs.Hero(h).Coef * Defs.StarMul[star - 1] * HeroDmgMul(h);

        public float UltPower(HeroId h, int star) => AtkPower(h, star) * Lo.UltPow[(int)h] * (1f + Stat(h, Eff.UltPow));

        public float HeroSpeedMul(HeroId h)
            => Lo.AllSpeed * Lo.HeroSpeed[(int)h] * (1f + Stat(h, Eff.Speed)) * (1f + Aura) * (HasteT > 0 ? HasteMul : 1f);

        public float HeroRange(HeroId h) => Defs.Hero(h).Range + Stat(h, Eff.Range);

        public float CritRate(HeroId h) => Math.Min(0.8f, Lo.Crit + Stat(h, Eff.Crit));

        public float UltCd(HeroId h)
            => Defs.Hero(h).UltCd * Lo.UltCd * Lo.HeroUltCd[(int)h] * (1f - Math.Min(0.6f, Stat(h, Eff.UltCd)));

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
                if (e.Dead || e.Id == skip || e.Pos.Y > maxY || e.Pos.Y > Layout.EngageY) continue;
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
                u.Cd -= dt * HeroSpeedMul(u.Hero) * Defs.StarSpeed[u.Star - 1];
                if (u.Cd > 0) continue;
                var target = PickTarget(def, s.Pos);
                if (target == null) { u.Cd = 0.1f; continue; }
                u.Cd = def.Interval;
                float dmg = AtkPower(u.Hero, u.Star);
                if (u.Burst) { dmg *= 2.5f; u.Burst = false; }
                Fire(s, u, target, dmg);
            }
        }

        bool RollCrit(HeroId h, ref float dmg)
        {
            if (!_rng.Chance(CritRate(h))) return false;
            dmg *= Lo.CritDmg;
            return true;
        }

        void Fire(Slot s, Unit u, Enemy target, float dmg)
        {
            var d = u.Def;
            var h = u.Hero;
            Events.Add(new BattleEvent { Type = Ev.Fire, Slot = s.Index, Hero = h, Pos = target.Pos });
            int extra = (int)Stat(h, Eff.Count);
            int count = d.Count + extra;
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
            if (d.Aura == AuraKind.Heal)
                HealWall(WallMax * d.AuraV * u.Star * (1f + Stat(h, Eff.Aura)), s.Pos, h);
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
                if (s.TargetId >= 0 && (s.Kind == ShotKind.Arrow || s.Kind == ShotKind.Stone || s.Kind == ShotKind.Wind || s.Kind == ShotKind.Lasso))
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
                case ShotKind.Bolt:
                case ShotKind.Ball:
                    HitWall(s.Damage, s.To);
                    if (s.Kind == ShotKind.Ball) Events.Add(new BattleEvent { Type = Ev.Explode, Pos = s.To, Radius = 1.4f });
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
                case ShotKind.FireArrow:
                    Splash(s, s.To, s.Radius, s.Damage, 0);
                    EmitHit(s, s.Radius);
                    return;
                case ShotKind.Bomb:
                case ShotKind.MiniBomb:
                case ShotKind.BigBomb:
                case ShotKind.Fireball:
                    Splash(s, s.To, s.Radius, s.Damage, s.Kind == ShotKind.BigBomb ? 0.5f : s.Knock);
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
                if (knock > 0 && !e.Big && e.Def.Kind != EnemyKind.Porter && !e.Def.Flying)
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
            if (e.VulnT > 0) dmg *= 1.25f;
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
            DamageBy[(int)h] += dealt;
            Events.Add(new BattleEvent { Type = Ev.Damage, Pos = e.Pos, Value = (int)Math.Ceiling(dmg), Hero = h, Crit = crit, EnemyId = e.Id });
            if (e.Hp > 0) return;
            e.Dead = true;
            Kills++;
            int coin = (int)Math.Round(e.Def.Coin * (1f + CoinAura));
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
                case UltKind.Rain:
                    for (int i = 0; i < def.UltN; i++)
                    {
                        var to = at + new V2(_rng.Range(-1.6f, 1.6f), _rng.Range(-1.2f, 1.6f));
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.FireArrow, Hero = hero, From = new V2(from.Pos.X + _rng.Range(-0.4f, 0.4f), from.Pos.Y),
                            To = to, T = -i * 0.06f, Dur = 0.45f, Damage = power, Radius = 0.6f, Ult = true,
                        });
                    }
                    break;
                case UltKind.BigBomb:
                    Shots.Add(new Shot { Id = _nextId++, Kind = ShotKind.BigBomb, Hero = hero, From = from.Pos, To = at, Dur = 0.95f, Damage = power, Radius = def.UltRadius * (1f + Stat(hero, Eff.Area) * 0.5f), Ult = true });
                    break;
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
                case UltKind.Barrage:
                    for (int i = 0; i < def.UltN; i++)
                    {
                        var p = at + new V2(_rng.Range(-1.8f, 1.8f), _rng.Range(-0.6f, 1.4f));
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Punch, Hero = hero, From = from.Pos, To = p, T = -i * 3f / def.UltN, Dur = 0.05f,
                            Damage = power, Radius = 1.0f, Knock = 0.5f, Ult = true,
                        });
                    }
                    break;
                case UltKind.Volley:
                case UltKind.Arrows:
                {
                    var targets = FrontTargets();
                    bool stone = def.Ult == UltKind.Volley;
                    for (int i = 0; i < def.UltN && targets.Count > 0; i++)
                    {
                        var t = targets[_rng.Int(Math.Min(targets.Count, 16))];
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = stone ? ShotKind.Stone : ShotKind.Arrow, Hero = hero, From = from.Pos, To = t.Pos, TargetId = t.Id, T = -i * 0.08f,
                            Dur = 0.22f + (stone ? 0 : V2.Dist(from.Pos, t.Pos) / 30f), Damage = power, Radius = 0.45f, Stun = def.UltStun, StunChance = def.UltStun > 0 ? 1f : 0f,
                            Ult = true, Hit = new List<int>(), Pierce = stone ? 0 : ChainOf(hero), Crit = true,
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
                {
                    HasteT = 6f * boost;
                    HasteMul = Math.Max(HasteMul, 1.6f);
                    int coin = (int)((10 + 10 * u.Star) * boost);
                    Coins += coin;
                    Emit(Ev.Coin, from.Pos, from.Index, coin, hero);
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Wind, Hero = hero, From = from.Pos, To = at, Dur = 0.4f,
                        Damage = power, Radius = def.UltRadius, Slow = 2f, Ult = true,
                    });
                    break;
                }
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
                case UltKind.Brick:
                {
                    var t = Strongest() ?? focus;
                    bool brick = def.Ult == UltKind.Brick;
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = brick ? ShotKind.Stone : ShotKind.Arrow, Hero = hero, From = from.Pos, To = t.Pos, TargetId = t.Id,
                        Dur = brick ? 0.45f : 0.3f, Damage = power, Radius = brick ? 0.9f : 0.5f, Crit = true, Ult = true, Hit = new List<int>(),
                        Stun = def.UltStun, StunChance = def.UltStun > 0 ? 1f : 0f,
                    });
                    break;
                }
                case UltKind.Tornado:
                case UltKind.Net:
                case UltKind.Powder:
                    Shots.Add(new Shot
                    {
                        Id = _nextId++, Kind = ShotKind.Wind, Hero = hero, From = from.Pos, To = at, Dur = 0.45f,
                        Damage = power, Radius = def.UltRadius * (1f + Stat(hero, Eff.Area) * 0.5f), Slow = 3f,
                        Stun = def.UltStun + (def.Ult == UltKind.Tornado ? 0 : Stat(hero, Eff.Stun)), StunChance = 1f, Ult = true,
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
                case UltKind.FireLine:
                    for (int i = 0; i < def.UltN; i++)
                    {
                        var p = new V2(-3.6f + i * 7.2f / Math.Max(1, def.UltN - 1), Layout.StopY + 1.2f + _rng.Range(-0.3f, 0.6f));
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Bomb, Hero = hero, From = from.Pos, To = p, T = -i * 0.12f, Dur = 0.6f,
                            Damage = power, Radius = 1.3f, Knock = 0.3f, Ult = true,
                        });
                    }
                    break;
                case UltKind.Formation:
                    foreach (var e in Enemies)
                    {
                        if (e.Dead || e.Pos.Y > Layout.EngageY) continue;
                        e.SlowT = Math.Max(e.SlowT, 4f * boost);
                        e.VulnT = Math.Max(e.VulnT, 6f * boost);
                        Damage(e, power, hero, false);
                    }
                    break;
                case UltKind.Wave:
                    for (int row = 0; row < 2; row++)
                        for (int i = 0; i < 4; i++)
                        {
                            var p = new V2(-3.3f + i * 2.2f, Layout.StopY + 1.3f + row * 2.4f);
                            Shots.Add(new Shot
                            {
                                Id = _nextId++, Kind = ShotKind.Wind, Hero = hero, From = new V2(p.X, Layout.WallRowY), To = p, T = -row * 0.25f - i * 0.05f,
                                Dur = 0.35f, Damage = power, Radius = 1.5f, Slow = 3f, Knock = 0.6f, Ult = true,
                            });
                        }
                    break;
                case UltKind.Hook:
                    for (int i = 0; i < def.UltN; i++)
                    {
                        var p = new V2(-3.6f + i * 7.2f / Math.Max(1, def.UltN - 1), Layout.StopY + 0.6f);
                        Shots.Add(new Shot
                        {
                            Id = _nextId++, Kind = ShotKind.Spear, Hero = hero, From = new V2(p.X, Layout.WallRowY), To = p, T = -i * 0.08f, Dur = 0.12f,
                            Damage = power, Radius = 0.9f, Stun = def.UltStun, StunChance = 1f, Slow = 2f, Ult = true,
                        });
                    }
                    break;
                case UltKind.Steal:
                {
                    int coin = (int)((20 + 10 * u.Star + Wave * 4) * boost);
                    Coins += coin;
                    Emit(Ev.Coin, from.Pos, from.Index, coin, hero);
                    break;
                }
                case UltKind.Interest:
                {
                    int coin = M.Clamp((int)(Coins * def.UltCoef * boost), 30, 400);
                    Coins += coin;
                    Emit(Ev.Coin, from.Pos, from.Index, coin, hero);
                    break;
                }
                case UltKind.Forge:
                    ForgeT = 6f;
                    ForgeV = def.UltCoef * boost * (0.8f + 0.1f * u.Star);
                    break;
                case UltKind.Charge:
                    foreach (var h in Lineup)
                        if (h != hero) UltTimer[(int)h] *= 1f - def.UltCoef;
                    HasteT = Math.Max(HasteT, 4f * boost);
                    HasteMul = Math.Max(HasteMul, 1.3f);
                    break;
            }
        }
    }
}

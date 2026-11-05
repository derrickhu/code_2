using System.Linq;
using Liangshan.Sim;
using NUnit.Framework;

namespace Liangshan.Tests
{
    public class BattleTests
    {
        static readonly HeroId[] Lineup = { HeroId.HuaRong, HeroId.LingZhen, HeroId.GongSun, HeroId.WuSong };

        [Test]
        public void SummonCostsRiseAndEveryFifthAddsSlot()
        {
            var b = new Battle(1, Lineup) { Coins = 100000 };
            Assert.AreEqual(7, b.Slots.Count);
            int last = 0;
            for (int i = 0; i < 5; i++)
            {
                int cost = b.SummonCost;
                Assert.Greater(cost, last);
                last = cost;
                Assert.IsNotNull(b.Summon());
            }
            Assert.AreEqual(8, b.Slots.Count);
            Assert.AreEqual(Layout.FrontRowY, b.Slots[7].Pos.Y);
        }

        [Test]
        public void OpeningWavesAreFewButToughThenSwell()
        {
            Assert.LessOrEqual(Defs.WaveCount(1), 8);
            for (int w = 2; w <= 10; w++) Assert.Greater(Defs.WaveCount(w), Defs.WaveCount(w - 1));
            Assert.GreaterOrEqual(Defs.WaveCount(10), Defs.WaveCount(1) * 10);
            Assert.Greater(Defs.Tough(1), Defs.Tough(6));
            Assert.GreaterOrEqual(Defs.Tough(6), 2.5f);
        }

        [Test]
        public void OneStarFiresOnceAndEachStarAddsAShot()
        {
            var d = Defs.Hero(HeroId.HuaRong);
            Assert.AreEqual(1, Defs.ShotsAt(d, 1));
            Assert.AreEqual(2, Defs.ShotsAt(d, 2));
            Assert.AreEqual(d.Count, Defs.ShotsAt(d, 3));
            Assert.AreEqual(1, Defs.ShotsAt(Defs.Hero(HeroId.LinChong), 4));
        }

        [Test]
        public void SummonAndEnhancePricesFollowTable()
        {
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 45, 60, 80, 90, 110, 130, 150, 160, 175, 190, 200, 215, 235, 260, 290, 320, 355 },
                Enumerable.Range(0, 20).Select(Defs.SummonCost).ToArray());
            CollectionAssert.AreEqual(new[] { 100, 200, 350, 550, 800 }, Enumerable.Range(0, 5).Select(Defs.EnhanceCost).ToArray());
            Assert.AreEqual(100, new Battle(1, Lineup).Coins);
        }

        [Test]
        public void LordSkillHitsOnlyInsideTheShownRange()
        {
            var b = new Battle(3, Lineup);
            var near = new Enemy { Id = 900, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(-3, Layout.StopY), Attacking = true };
            var far = new Enemy { Id = 901, Def = Defs.Enemy(EnemyKind.Shield), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(3, 4.5f) };
            Assert.IsTrue(Layout.InLordRange(near.Pos));
            Assert.IsFalse(Layout.InLordRange(far.Pos));
            b.Enemies.Add(near);
            b.Enemies.Add(far);
            b.LordT = 0.01f;
            for (int i = 0; i < 60; i++) { b.Update(1f / 30f); b.Events.Clear(); }
            Assert.AreEqual(1, b.LordCasts);
            Assert.Less(near.Hp, near.MaxHp);
            Assert.AreEqual(far.MaxHp, far.Hp);
            Assert.Greater(b.LordDamage, b.LordPower * 0.9f);
            Assert.AreEqual(Defs.LordCdBase, b.LordT, 2.1f);
        }

        [Test]
        public void LordCooldownRestartsWhenTheSemicircleStaysEmpty()
        {
            var b = new Battle(1, Lineup);
            bool restarted = false;
            for (int i = 0; i < 180; i++)
            {
                b.Update(0.1f);
                Assert.AreEqual(Phase.Running, b.Phase);
                foreach (var e in b.Enemies)
                {
                    e.Pos = new V2(0, 6f);
                    e.Hp = e.MaxHp;
                    e.Dead = false;
                }
                if (b.LordCasts == 0 && b.LordT > Defs.LordCdBase * 0.5f) restarted = true;
            }
            Assert.AreEqual(0, b.LordCasts);
            Assert.IsTrue(restarted);
        }

        [Test]
        public void MergeNeedsSameHeroAndStarAndRaisesStar()
        {
            var b = new Battle(2, Lineup);
            b.Slots[0].Unit = new Unit { Hero = HeroId.HuaRong, Star = 1 };
            b.Slots[1].Unit = new Unit { Hero = HeroId.HuaRong, Star = 1 };
            b.Slots[2].Unit = new Unit { Hero = HeroId.WuSong, Star = 1 };
            b.Slots[3].Unit = new Unit { Hero = HeroId.HuaRong, Star = 2 };
            Assert.IsFalse(b.CanMerge(0, 2));
            Assert.IsFalse(b.CanMerge(0, 3));
            Assert.IsTrue(b.CanMerge(0, 1));
            b.Drop(0, 1);
            Assert.IsNull(b.Slots[0].Unit);
            Assert.AreEqual(2, b.Slots[1].Unit.Star);
            Assert.IsTrue(b.Slots[1].Unit.Burst);
        }

        [Test]
        public void FullStarCannotMerge()
        {
            var b = new Battle(3, Lineup);
            b.Slots[0].Unit = new Unit { Hero = HeroId.GongSun, Star = 4 };
            b.Slots[1].Unit = new Unit { Hero = HeroId.GongSun, Star = 4 };
            Assert.IsFalse(b.CanMerge(0, 1));
            b.Drop(0, 1);
            Assert.AreEqual(4, b.Slots[0].Unit.Star);
        }

        [Test]
        public void EnhanceOffersThreeChoicesAndPauses()
        {
            var b = new Battle(4, Lineup) { Coins = 1000 };
            Assert.IsTrue(b.Enhance());
            Assert.AreEqual(Phase.Choosing, b.Phase);
            Assert.AreEqual(3, b.Choices.Length);
            float t = b.Time;
            b.Update(1f);
            Assert.AreEqual(t, b.Time);
            var picked = b.Choices[0];
            b.Pick(0);
            Assert.AreEqual(Phase.Running, b.Phase);
            Assert.AreEqual(1, b.Level(picked.Id));
        }

        [Test]
        public void EnhanceOnlyOffersLineupHeroesRegularPerks()
        {
            var b = new Battle(6, Lineup) { Coins = 100000 };
            for (int k = 0; k < 6; k++)
            {
                Assert.IsTrue(b.Enhance());
                foreach (var p in b.Choices)
                {
                    Assert.IsFalse(p.Star);
                    Assert.IsTrue(b.InLineup(p.Hero));
                }
                b.Pick(0);
            }
        }

        [Test]
        public void LevelOptionsOnlyEnterThePoolOnceUnlocked()
        {
            bool Offers(int lv, int opt)
            {
                var lo = Loadout.Plain(Lineup);
                for (int h = 0; h < lo.Lv.Length; h++) lo.Lv[h] = lv;
                for (int seed = 1; seed <= 30; seed++)
                {
                    var b = new Battle(seed, Lineup, null, lo) { Coins = 100000 };
                    for (int k = 0; k < 6 && b.Enhance(); k++)
                    {
                        if (b.Choices.Any(p => p.Opt == opt)) return true;
                        b.Pick(0);
                    }
                }
                return false;
            }
            Assert.IsFalse(Offers(Meta.SurgeLv - 1, 1));
            Assert.IsTrue(Offers(Meta.SurgeLv, 1));
            Assert.IsFalse(Offers(Meta.TopLv - 1, 2));
            Assert.IsTrue(Offers(Meta.TopLv, 2));
        }

        [Test]
        public void FirstFullStarMergeOffersStarSkillsOnce()
        {
            var b = new Battle(7, new[] { HeroId.HuaRong, HeroId.HuaRong, HeroId.HuaRong, HeroId.HuaRong });
            b.Slots[0].Unit = new Unit { Hero = HeroId.HuaRong, Star = 3 };
            b.Slots[1].Unit = new Unit { Hero = HeroId.HuaRong, Star = 3 };
            Assert.IsTrue(b.Drop(0, 1));
            Assert.AreEqual(4, b.Slots[1].Unit.Star);
            Assert.AreEqual(Phase.Choosing, b.Phase);
            Assert.AreEqual(HeroId.HuaRong, b.StarPick);
            Assert.AreEqual(3, b.Choices.Length);
            foreach (var p in b.Choices) Assert.IsTrue(p.Star);
            b.Reroll();
            Assert.IsTrue(b.Choices[0].Star);
            b.Pick(1);
            Assert.AreEqual(Phase.Running, b.Phase);
            Assert.IsTrue(b.StarTaken(HeroId.HuaRong));

            b.Slots[2].Unit = new Unit { Hero = HeroId.HuaRong, Star = 3 };
            b.Slots[3].Unit = new Unit { Hero = HeroId.HuaRong, Star = 3 };
            b.Drop(2, 3);
            Assert.AreEqual(Phase.Running, b.Phase);
        }

        [Test]
        public void ElitesAndBossesAlwaysDropTreasureBossIsJackpot()
        {
            var b = new Battle(8, Lineup, Defs.Level(5));
            Assert.AreEqual(0, b.Stage.PorterWaves.Length);
            var boss = Defs.Enemy(Defs.Level(5).BossKind);
            b.Enemies.Add(new Enemy { Id = 999, Def = boss, Hp = 1, MaxHp = 1, Pos = new V2(0, 3) });
            b.Slots[0].Unit = new Unit { Hero = HeroId.GongSun, Star = 4 };
            for (int i = 0; i < 300 && b.Phase == Phase.Running; i++) b.Update(1f / 30f);
            Assert.AreEqual(Phase.Reels, b.Phase);
            Assert.IsTrue(b.NextIsJackpot);
            var r = b.SpinReels();
            Assert.IsTrue(r[0] == r[1] && r[1] == r[2] && r[2] == r[3]);
            b.ApplyReels(r);
            Assert.IsFalse(b.NextIsJackpot);
        }

        [Test]
        public void MainLevelsHaveOneBossAndDropTreasureTwice()
        {
            foreach (var st in Defs.Levels)
            {
                Assert.AreEqual(4, st.BossWave, st.Name);
                CollectionAssert.AreEqual(new[] { 9 }, st.EliteWaves, st.Name);
            }
            var b = new Battle(3, Lineup, Defs.Level(12));
            var boss = new Enemy { Id = 999, Def = Defs.Enemy(Defs.Level(12).BossKind), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0, 7) };
            b.Enemies.Add(boss);
            for (int i = 0; i < 30; i++) b.Update(1f / 30f);
            Assert.AreEqual(boss.Def.Speed * Defs.BossPace, 7 - boss.Pos.Y, 0.01f, "首领压阵要慢慢逼近");
        }

        [Test]
        public void ReelsJackpotDoublesSilver()
        {
            var b = new Battle(5, Lineup) { Phase = Phase.Reels, PendingReels = 1 };
            int before = b.Coins;
            b.ApplyReels(new[] { Reel.Silver, Reel.Silver, Reel.Silver, Reel.Silver });
            Assert.AreEqual(before + b.ReelSilver * 8, b.Coins);
            Assert.AreEqual(Phase.Running, b.Phase);
        }

        [Test]
        public void ReelsOnlyGiveSilverAndPerksNeverHeroes()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var b = new Battle(seed, Lineup);
                Put(b, 0, Lineup[0]);
                b.Phase = Phase.Reels;
                b.PendingReels = 1;
                if (seed % 3 == 0) b.JackpotReels = 1;
                int units = b.Slots.Count(s => s.Unit != null), slots = b.Slots.Count, perks = b.Perks.Values.Sum();
                float wall = b.Wall;
                int coins = b.Coins;
                var r = b.SpinReels();
                Assert.IsTrue(r.All(x => x == Reel.Silver || x == Reel.Perk));
                int given = b.ReelPerks.Length;
                bool all = r.All(x => x == r[0]);
                Assert.LessOrEqual(given, all && r[0] == Reel.Perk ? Battle.ReelPerkJackpot : Battle.ReelPerkMax);
                b.ApplyReels(r);
                Assert.AreEqual(units, b.Slots.Count(s => s.Unit != null), "生辰纲不送好汉，不能把格子占满");
                Assert.AreEqual(slots, b.Slots.Count);
                Assert.AreEqual(wall, b.Wall);
                Assert.AreEqual(perks + given, b.Perks.Values.Sum());
                Assert.GreaterOrEqual(b.Coins, coins);
                Assert.AreEqual(Phase.Running, b.Phase);
            }
        }

        [Test]
        public void GoodBotClearsFirstStageMostOfTheTime()
        {
            int wins = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var b = new Battle(seed, Meta.Starters);
                var bot = new Bot(2);
                while (b.Phase != Phase.Victory && b.Phase != Phase.Defeat && b.Time < 400)
                {
                    bot.Step(b, 1f / 30f);
                    b.Update(1f / 30f);
                    b.Events.Clear();
                }
                if (b.Phase == Phase.Victory) wins++;
            }
            Assert.GreaterOrEqual(wins, 7);
        }

        [Test]
        public void IdleSummonerLoses()
        {
            var b = new Battle(9, Meta.Starters);
            var bot = new Bot(0);
            while (b.Phase != Phase.Victory && b.Phase != Phase.Defeat && b.Time < 400)
            {
                bot.Step(b, 1f / 30f);
                b.Update(1f / 30f);
                b.Events.Clear();
            }
            Assert.AreEqual(Phase.Defeat, b.Phase);
        }

        [Test]
        public void CooldownsDifferByRoleAndOnlyLegendsHaveUlts()
        {
            Assert.LessOrEqual(Defs.Hero(HeroId.HuaRong).Interval, 2.5f);
            Assert.GreaterOrEqual(Defs.Hero(HeroId.GongSun).Interval, 12f);
            Assert.Greater(Defs.Hero(HeroId.GongSun).Chan, 0);
            foreach (var d in Defs.Heroes)
            {
                if (d.Kind == Role.Mage) Assert.GreaterOrEqual(d.Interval, 10f, d.Name);
                if (d.Kind == Role.Support)
                {
                    Assert.IsTrue(d.Buff.HasValue, d.Name);
                    Assert.AreEqual(0f, d.Coef, d.Name);
                }
                Assert.AreEqual(d.Rarity >= 4, Defs.UltPerk(d.Id) != null, d.Name);
            }
        }

        static Unit Put(Battle b, int slot, HeroId h, int star = 1)
        {
            var u = new Unit { Hero = h, Star = star, Cd = 0 };
            b.Slots[slot].Unit = u;
            return u;
        }

        static Enemy Dummy(Battle b, float x = 0, float y = 3f)
        {
            var e = new Enemy { Id = 900 + b.Enemies.Count, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e9f, MaxHp = 1e9f, Pos = new V2(x, y), Attacking = true };
            b.Enemies.Add(e);
            return e;
        }

        [Test]
        public void LegendUltUnlocksOnlyFromEnhanceCard()
        {
            var b = new Battle(4, Lineup);
            Put(b, 0, HeroId.GongSun);
            Assert.IsFalse(b.HasUlt(HeroId.GongSun));
            bool seen = false;
            for (int i = 0; i < 60 && !b.HasUlt(HeroId.GongSun); i++)
            {
                b.Coins = 1000000;
                Assert.IsTrue(b.Enhance());
                int k = System.Array.FindIndex(b.Choices, p => p.Ult);
                Assert.IsFalse(b.Choices.Any(p => p.Eff == Eff.UltCd && !p.Ult), "大招没解锁前不出大招冷却卡");
                if (k >= 0) seen = true;
                b.Pick(k >= 0 ? k : 0);
            }
            Assert.IsTrue(seen);
            Assert.IsTrue(b.HasUlt(HeroId.GongSun));
            Assert.IsFalse(b.HasUlt(HeroId.HuaRong));
        }

        [Test]
        public void MageChannelsSeveralTicksPerCast()
        {
            var b = new Battle(5, Lineup);
            Put(b, 0, HeroId.GongSun);
            Dummy(b);
            int fires = 0, ticks = 0;
            for (int i = 0; i < 30 * 5; i++)
            {
                b.Update(1f / 30f);
                foreach (var e in b.Events)
                {
                    if (e.Type == Ev.Fire && e.Slot == 0) fires++;
                    if (e.Type == Ev.Hit && e.Hero == HeroId.GongSun) ticks++;
                }
                b.Events.Clear();
            }
            Assert.AreEqual(1, fires, "法师 5 秒内只出手一次");
            Assert.GreaterOrEqual(ticks, 8, "一次施法持续跳多下");
        }

        [Test]
        public void WuYongBlessesACarryAndSpeedsItsCooldown()
        {
            var b = new Battle(6, Lineup);
            Put(b, 0, HeroId.WuYong);
            var archer = Put(b, 1, HeroId.HuaRong);
            Dummy(b);
            bool buffed = false;
            for (int i = 0; i < 30 && !buffed; i++)
            {
                b.Update(1f / 30f);
                buffed = b.Events.Any(e => e.Type == Ev.Buff && e.EnemyId == 1);
                b.Events.Clear();
            }
            Assert.IsTrue(buffed);
            Assert.Greater(archer.BlessN, 0);
            Assert.Greater(archer.BlessMul, 1.5f);
        }

        [Test]
        public void DaiZongCutsTeammateCooldowns()
        {
            var b = new Battle(7, Lineup);
            var mage = Put(b, 1, HeroId.GongSun);
            Dummy(b);
            b.Update(1f / 30f);
            b.Events.Clear();
            float before = mage.Cd;
            Assert.Greater(before, 10f);
            Put(b, 0, HeroId.DaiZong);
            b.Update(1f / 30f);
            Assert.Less(mage.Cd, before * 0.75f);
        }
    }
}

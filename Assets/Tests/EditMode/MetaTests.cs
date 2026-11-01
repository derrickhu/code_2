using System;
using System.Linq;
using Liangshan.Sim;
using NUnit.Framework;

namespace Liangshan.Tests
{
    public class MetaTests
    {
        [Test]
        public void NewSaveOwnsFourCommonStartersAndAllArraysSized()
        {
            var s = Meta.NewSave(1000);
            Assert.AreEqual(Defs.HeroCount, s.HeroLv.Length);
            Assert.AreEqual(Defs.LevelCount, s.Stars.Length);
            Assert.AreEqual(Defs.Enemies.Length, s.Seen.Length);
            for (int h = 0; h < Defs.HeroCount; h++)
                Assert.AreEqual(Meta.Starters.Contains((HeroId)h) ? 1 : 0, s.HeroLv[h]);
            CollectionAssert.AreEqual(Meta.Starters, Meta.Lineup(s));
            foreach (var h in Meta.Starters) Assert.AreEqual(2, Defs.Hero(h).Rarity, Defs.Hero(h).Name);
            Assert.AreEqual(4, Meta.Starters.Select(h => Defs.Hero(h).Kind).Distinct().Count());
            Assert.AreEqual(50, Defs.LevelCount);
            Assert.AreEqual(Meta.StaminaMax, s.Stamina);
        }

        [Test]
        public void FirstClearUnlocksRampFromCommonToLegendary()
        {
            int last = 2, firstLegend = 0;
            for (int n = 1; n <= Defs.LevelCount; n++)
            {
                var h = Meta.UnlockAt(n);
                if (!h.HasValue) continue;
                int r = Defs.Hero(h.Value).Rarity;
                Assert.GreaterOrEqual(r, last, "第" + n + "关");
                last = r;
                if (r == 4 && firstLegend == 0) firstLegend = n;
            }
            Assert.GreaterOrEqual(firstLegend, 21);

            var s = Meta.NewSave(0);
            for (int seed = 1; seed <= 20; seed++)
                Assert.IsFalse(Meta.Recruit(s, 10, new Rng(seed)).Any(p => Defs.Hero(p.Hero).Rarity == 4));
            Assert.AreEqual(0, s.Pity);
        }

        [Test]
        public void LevelUpSpendsShardsAndCopperAndUnlocksTalent()
        {
            var s = Meta.NewSave(0);
            var a = Meta.Starters[0];
            int ai = (int)a, bi = (int)Meta.Starters[1];
            Assert.IsFalse(Meta.LevelUp(s, a));
            Assert.IsFalse(Meta.LevelUp(s, HeroId.HuaRong));
            s.Shards[ai] = 100;
            s.Copper = 100000;
            float before = Meta.BuildLoadout(s).HeroDmg[ai];
            for (int i = 0; i < 4; i++) Assert.IsTrue(Meta.LevelUp(s, a));
            Assert.AreEqual(5, s.HeroLv[ai]);
            Assert.AreEqual(100 - (3 + 4 + 5 + 6), s.Shards[ai]);
            var lo = Meta.BuildLoadout(s);
            Assert.Greater(lo.HeroDmg[ai], before);
            Assert.IsTrue(lo.Passive[ai]);
            Assert.IsFalse(lo.Passive[bi]);
            Assert.Greater(lo.CritDmg, Meta.BaseCritDmg);
            Assert.AreEqual(1, s.Task[2]);
        }

        [Test]
        public void ForgeCappedByProgress()
        {
            var s = Meta.NewSave(0);
            s.Iron = 10000;
            s.Copper = 10000000;
            while (Meta.Forge(s, 0)) { }
            Assert.AreEqual(Meta.EquipMax(s), s.EquipLv[0]);
            s.Cleared = 20;
            Assert.IsTrue(Meta.Forge(s, 0));
            var lo = Meta.BuildLoadout(s);
            Assert.Greater(lo.Atk, Meta.BaseAtk + 10);
            Assert.AreEqual(1f, lo.RoleDmg[(int)Role.Fighter], 1e-4f);
            s.EquipLv[0] = 10;
            Assert.AreEqual(1f + Meta.BreakDmg, Meta.BuildLoadout(s).RoleDmg[(int)Role.Fighter], 1e-4f);
        }

        [Test]
        public void FirstClearRewardsAndUnlockOnce()
        {
            var s = Meta.NewSave(0);
            s.Cleared = 2;
            var st = Defs.Level(3);
            var r = Meta.BattleRewards(s, st, true, 10, 0.8f, 0, new Rng(1));
            Assert.IsTrue(r.First);
            Assert.AreEqual(3, r.Stars);
            Assert.AreEqual(HeroId.RuanXiaoqi, r.Unlock);
            Assert.Greater(r.Get(Cur.Ingot), 0);
            Meta.Give(s, r);
            Meta.FinishMain(s, st, true, r);
            Assert.IsTrue(Meta.Owned(s, HeroId.RuanXiaoqi));
            Assert.AreEqual(3, s.Cleared);
            Assert.AreEqual(4, s.Selected);
            Assert.AreEqual(1, s.Speed15);

            var again = Meta.BattleRewards(s, st, true, 10, 0.2f, 0, new Rng(2));
            Assert.IsFalse(again.First);
            Assert.IsNull(again.Unlock);
            Assert.AreEqual(0, again.Get(Cur.Ingot));
            Meta.FinishMain(s, st, true, again);
            Assert.AreEqual(3, s.Stars[2]);
        }

        [Test]
        public void LossGivesPartialCopperOnly()
        {
            var s = Meta.NewSave(0);
            var r = Meta.BattleRewards(s, Defs.Level(1), false, 6, 0, 0, new Rng(1));
            Assert.Greater(r.Get(Cur.Copper), 0);
            Assert.AreEqual(1, r.Items.Count);
            Assert.IsFalse(r.First);
        }

        [Test]
        public void RecruitPityAndTenPullGuarantee()
        {
            var s = Meta.NewSave(0);
            s.Cleared = Meta.OrangeOpen;
            bool orange = false;
            for (int i = 0; i < Meta.OrangePity && !orange; i++)
                orange = Meta.Recruit(s, 1, new Rng(1000 + i)).Any(p => Defs.Hero(p.Hero).Rarity == 4);
            Assert.IsTrue(orange);

            for (int seed = 1; seed <= 30; seed++)
            {
                var t = Meta.NewSave(0);
                var res = Meta.Recruit(t, 10, new Rng(seed));
                Assert.AreEqual(10, res.Count);
                Assert.IsTrue(res.Any(p => Defs.Hero(p.Hero).Rarity >= 3));
                foreach (var p in res) Assert.IsTrue(p.New || p.Shards > 0);
            }
        }

        [Test]
        public void StaminaRegensAndDayRollResetsDailies()
        {
            var s = Meta.NewSave(0);
            Assert.IsTrue(Meta.Spend(s, Cur.Stamina, 20));
            s.StaminaAt = 0;
            Meta.RegenStamina(s, Meta.StaminaSec * 7 + 10);
            Assert.AreEqual(Meta.StaminaMax - 13, s.Stamina);
            Meta.RegenStamina(s, Meta.StaminaSec * 1000);
            Assert.AreEqual(Meta.StaminaMax, s.Stamina);

            s.Day = 5;
            Meta.Progress(s, 0, 3);
            Assert.IsTrue(Meta.ClaimTask(s, 0));
            s.DailyTries = 2;
            Meta.RollDay(s, 6);
            Assert.AreEqual(0, s.Task[0]);
            Assert.AreEqual(0, s.TaskClaimed);
            Assert.AreEqual(0, s.DailyTries);
        }

        [Test]
        public void IdleCapsAtEightHours()
        {
            var s = Meta.NewSave(0);
            s.IdleAt = 0;
            var r = Meta.CollectIdle(s, 3600 * 30);
            Assert.AreEqual((int)(Meta.IdleCopperPerHour(s) * Meta.IdleCapHours), r.Get(Cur.Copper));
            Assert.AreEqual(3600 * 30, s.IdleAt);
        }

        [Test]
        public void ChapterChestNeedsStarsAndOpensOnce()
        {
            var s = Meta.NewSave(0);
            Assert.IsFalse(Meta.CanOpenChest(s, 1, 0));
            for (int i = 0; i < 4; i++) s.Stars[i] = 3;
            Assert.IsTrue(Meta.CanOpenChest(s, 1, 0));
            Assert.IsFalse(Meta.CanOpenChest(s, 1, 1));
            int ingots = s.Ingots;
            Assert.IsNotNull(Meta.OpenChest(s, 1, 0));
            Assert.Greater(s.Ingots, ingots);
            Assert.IsNull(Meta.OpenChest(s, 1, 0));
        }

        [Test]
        public void LevelsHaveChapterBossesAndRisingDifficulty()
        {
            for (int c = 0; c < Defs.Chapters.Length; c++)
            {
                var boss = Defs.Level(c * 10 + 10);
                Assert.IsTrue(boss.IsChapterBoss);
                Assert.AreEqual(Defs.Chapters[c].Boss, boss.BossKind);
                Assert.IsTrue(Defs.Enemy(boss.BossKind).IsBoss);
            }
            for (int n = 2; n <= Defs.LevelCount; n++)
                Assert.GreaterOrEqual(Defs.Level(n).HpMul, Defs.Level(n - 1).HpMul * 0.98f, "第" + n + "关");
        }

        [Test]
        public void HeroRosterHasRaritiesRolesAndPerks()
        {
            Assert.AreEqual(32, Defs.HeroCount);
            for (int r = 2; r <= 4; r++) Assert.Greater(Defs.Heroes.Count(d => d.Rarity == r), 5);
            for (int r = 0; r < Defs.RoleCount; r++) Assert.GreaterOrEqual(Defs.Heroes.Count(d => (int)d.Kind == r), 4);
            foreach (var d in Defs.Heroes)
            {
                Assert.AreEqual(4, Defs.PerksOf(d.Id, false).Count, d.Name);
                Assert.AreEqual(3, Defs.PerksOf(d.Id, true).Count, d.Name);
                Assert.IsNotNull(Meta.FirstPerk(d.Id));
            }
            Assert.AreEqual(Defs.Perks.Length, Defs.Perks.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        public void TalentsCostCopperAndAddKingStats()
        {
            var s = Meta.NewSave(0);
            s.Copper = 1000000;
            var before = Meta.BuildLoadout(s);
            while (Meta.LearnTalent(s)) { }
            Assert.AreEqual(Meta.TalentCap(s), s.Talent);
            var lo = Meta.BuildLoadout(s);
            Assert.AreEqual(before.Atk + 6, lo.Atk, 0.01f);
            Assert.Greater(lo.Crit, before.Crit);
            Assert.Greater(lo.StartCoins, 0);
            s.Cleared = 10;
            Assert.IsTrue(Meta.LearnTalent(s));
        }

        [Test]
        public void ExpectedPowerMatchesExpectedSaveLoadout()
        {
            foreach (var n in new[] { 1, 5, 10, 20, 30, 40, 50 })
            {
                var s = Meta.ExpectedSave(n);
                var lo = Meta.BuildLoadout(s);
                var h = Meta.Starters[0];
                int lv = s.HeroLv[(int)h];
                float actual = Meta.PowerOf(lo, h) * Meta.UnlockFactor(lv);
                Assert.AreEqual(Meta.ExpectedPower(n), actual, Meta.ExpectedPower(n) * 0.05f, "第" + n + "关");
                float wall = lo.WallMul / (1f - lo.WallResist);
                Assert.AreEqual(Meta.ExpectedWall(n), wall, wall * 0.02f, "第" + n + "关寨墙");
            }
        }

        static int Wins(int level, SaveData s)
        {
            int w = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                var lo = Meta.BuildLoadout(s);
                var b = new Battle(seed, lo.Lineup, Defs.Level(level), lo);
                var bot = new Bot(2);
                while (b.Phase != Phase.Victory && b.Phase != Phase.Defeat && b.Time < 600)
                {
                    bot.Step(b, 1f / 30f);
                    b.Update(1f / 30f);
                    b.Events.Clear();
                }
                if (b.Phase == Phase.Victory) w++;
            }
            return w;
        }

        [Test]
        public void ExpectedGrowthClearsKeyLevelsButSkippingGrowthFails()
        {
            foreach (var n in new[] { 10, 25, 40, 50 })
            {
                Assert.GreaterOrEqual(Wins(n, Meta.ExpectedSave(n)), 5, $"第{n}关按标准养成应能过");
                Assert.LessOrEqual(Wins(n, Meta.ExpectedSave(n - 8)), 2, $"第{n}关落后8关的养成不该轻松过");
            }
        }
    }
}

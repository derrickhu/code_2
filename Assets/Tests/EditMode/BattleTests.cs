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
        public void StarMultipliersFollowDiminishingCurveAndMergeShocksNearWall()
        {
            Assert.AreEqual(1.0f, Defs.StarMul[0], 1e-4f);
            Assert.AreEqual(2.0f, Defs.StarMul[1], 1e-4f, "1→2星整整翻倍，性价比最高");
            Assert.Less(Defs.StarMul[2], Defs.StarMul[1] * 2f, "2→3星边际微敛，靠质变和词条放大");
            Assert.Greater(Defs.StarMul[3], Defs.StarMul[2] * 1.5f);
            var d = Defs.Hero(HeroId.HuaRong);
            Assert.AreEqual(d.Count, Defs.ShotsAt(d, 1), "1星就有完整职业连射节奏");

            var b = new Battle(2, Lineup);
            b.Slots[0].Unit = new Unit { Hero = HeroId.ShiXiu, Star = 1 };
            b.Slots[1].Unit = new Unit { Hero = HeroId.ShiXiu, Star = 1 };
            var near = new Enemy { Id = 910, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 500, MaxHp = 500, Pos = new V2(b.Slots[1].Pos.X, Layout.StopY), Attacking = true };
            b.Enemies.Add(near);
            Assert.IsTrue(b.Drop(0, 1));
            Assert.LessOrEqual(b.Slots[1].Unit.Cd, 0.3f, "合成落地立即重置冷却准备爆发");
            Assert.Greater(near.Pos.Y, Layout.StopY + 0.3f, "合成冲击震退贴墙敌军");
            Assert.Greater(near.Stun, 0.2f);
        }

        [Test]
        public void ShatterAndResonanceAmplifySynergyDamage()
        {
            var b = new Battle(5, new[] { HeroId.HuaRong, HeroId.RuanXiaoqi, HeroId.XieZhen, HeroId.ShiXiu });
            b.Slots[0].Unit = new Unit { Hero = HeroId.HuaRong, Star = 3 };
            b.Slots[1].Unit = new Unit { Hero = HeroId.XieZhen, Star = 1 };
            b.Perks["huarong_res"] = 1;
            b.Perks["huarong_shatter"] = 1;
            // 通过 Pick 刷新内部 _stat 表
            b.Choices = new[] { Defs.Perk("huarong_0") };
            b.Phase = Phase.Choosing;
            b.Pick(0);
            Assert.Greater(b.SlotHeroAtk(b.Slots[0]), b.HeroAtk(HeroId.HuaRong, 3) * 1.35f, "众星捧月按场上同职业人数放大主C攻击");

            var plain = new Enemy { Id = 920, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0, 1f), Stun = 0f };
            var chilled = new Enemy { Id = 921, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0, 1f), SlowT = 3f };
            b.Enemies.Add(plain);
            b.Enemies.Add(chilled);
            b.Shots.Add(new Shot { Id = 1, Kind = ShotKind.Arrow, Hero = HeroId.HuaRong, From = b.Slots[0].Pos, To = plain.Pos, TargetId = plain.Id, Dur = 0.01f, Damage = 100f });
            b.Shots.Add(new Shot { Id = 2, Kind = ShotKind.Arrow, Hero = HeroId.HuaRong, From = b.Slots[0].Pos, To = chilled.Pos, TargetId = chilled.Id, Dur = 0.01f, Damage = 100f });
            b.Update(0.02f);
            float plainLost = plain.MaxHp - plain.Hp;
            float chillLost = chilled.MaxHp - chilled.Hp;
            Assert.Greater(chillLost, plainLost * 1.7f, "破冰追击对受控敌人造成大幅额外伤害");
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
        public void StagesIntroduceMonstersGraduallyAndCapPerStageTypes()
        {
            var allMinions = new System.Collections.Generic.HashSet<EnemyKind>();
            foreach (var st in Defs.Levels)
            {
                Assert.LessOrEqual(st.Troops.Length, 2, $"第{st.No}关特色小兵不超过2种（加基础官兵最多3种）");
                var b = new Battle(1, Lineup, st);
                var roster = b.StageRoster();
                Assert.LessOrEqual(roster.Count, 5, $"第{st.No}关本关怪物总数不扎堆");

                var w1 = Defs.WaveTroops(st, 1, new Rng(11));
                Assert.IsTrue(w1.All(k => k == EnemyKind.Soldier), $"第{st.No}关第1波只出基础官兵，后续波次再逐步混入新怪");
                foreach (var m in st.Troops) allMinions.Add(m.Kind);
            }
            Assert.AreEqual(1, Defs.Level(1).Troops.Length, "第1关只解锁盾兵1种特色怪");
            Assert.AreEqual(1, Defs.Level(2).Troops.Length, "第2关只解锁骑兵1种特色怪");
            Assert.GreaterOrEqual(allMinions.Count, 14, "全5章逐步覆盖全部特色兵种");
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

        [Test]
        public void MeleeSwingTravelsToTheTargetAndHitsOnArrival()
        {
            Assert.That(Battle.MeleeDur, Is.InRange(0.15f, 0.4f), "太快看不见刀在走，太慢不像砍");

            var b = new Battle(1, Lineup);
            var slot = b.Slots[0];
            slot.Unit = new Unit { Hero = HeroId.ShiXiu, Star = 1 };
            var foe = new Enemy { Id = 900, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(slot.Pos.X, Layout.StopY), Attacking = true };
            b.Enemies.Add(foe);
            Shot cut = null;
            for (int i = 0; i < 120 && cut == null; i++)
            {
                b.Update(1f / 60f);
                cut = b.Shots.FirstOrDefault(s => s.Kind == ShotKind.Punch && s.Hero == HeroId.ShiXiu);
            }
            Assert.IsNotNull(cut);
            Assert.AreEqual(Battle.MeleeDur, cut.Dur, 1e-4f);
            Assert.AreEqual(foe.MaxHp, foe.Hp, "刀还在路上就不该掉血");
            float flown = 0f;
            while (b.Shots.Contains(cut) && flown < 1f)
            {
                Assert.AreEqual(foe.MaxHp, foe.Hp);
                b.Update(1f / 60f);
                flown += 1f / 60f;
            }
            Assert.Less(foe.Hp, foe.MaxHp);
            Assert.AreEqual(cut.Dur, flown, 2f / 60f);
        }

        [Test]
        public void TradeCardsExcludeEachOtherAndAdvancedNeedsTheMorph()
        {
            var b = new Battle(1, Lineup);
            b.Slots[0].Unit = new Unit { Hero = HeroId.HuaRong, Star = 1 };
            var heavy = Defs.Perk("huarong_heavy");
            var quick = Defs.Perk("huarong_quick");
            var morph = Defs.Perk("huarong_morph");
            var adv = Defs.Perk("huarong_adv");
            Assert.AreEqual(Eff.Blast, morph.Eff);
            bool Offered(PerkDef p)
            {
                for (int k = 0; k < 400; k++)
                {
                    b.Coins = 99999;
                    b.Enhances = 0;
                    if (!b.Enhance()) return false;
                    bool hit = b.Choices.Contains(p);
                    b.Choices = null;
                    b.Phase = Phase.Running;
                    if (hit) return true;
                }
                return false;
            }
            void Take(PerkDef p)
            {
                b.Choices = new[] { p };
                b.Phase = Phase.Choosing;
                b.Pick(0);
            }
            Assert.IsFalse(Offered(adv), "没有质变时不出进阶");
            Assert.IsTrue(Offered(quick));
            float dmg0 = b.HeroDmgMul(HeroId.HuaRong), spd0 = b.HeroSpeedMul(HeroId.HuaRong);
            Take(heavy);
            Assert.Greater(b.HeroDmgMul(HeroId.HuaRong), dmg0, "重手加伤害");
            Assert.Less(b.HeroSpeedMul(HeroId.HuaRong), spd0, "重手的代价是攻速");
            Assert.IsFalse(Offered(quick), "重手和快手二选一");
            Take(morph);
            Assert.IsTrue(Offered(adv), "拿了质变后出进阶");
        }

        [Test]
        public void BurnTicksOverItsDuration()
        {
            var b = new Battle(1, Lineup);
            var foe = new Enemy { Id = 900, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0f, 2f), Stun = 99f };
            b.Enemies.Add(foe);
            foe.BurnT = Defs.BurnDur;
            foe.BurnDps = 100f;
            foe.BurnTick = Defs.BurnEvery;
            for (int i = 0; i < 240; i++) b.Update(1f / 60f);
            Assert.AreEqual(1e6f - 100f * Defs.BurnDur, foe.Hp, 100f * Defs.BurnEvery + 1f, "灼烧 3 秒共掉约 BurnDps × 3");
            Assert.LessOrEqual(foe.BurnT, 0f);
        }

        [Test]
        public void EveryHeroUnlocksATraitAtEachMergedStar()
        {
            foreach (var d in Defs.Heroes)
                for (int star = 2; star <= 4; star++)
                {
                    var t = StarTraits.At(d, star);
                    Assert.IsNotNull(t, $"{d.Name} {star} 星");
                    Assert.IsNotEmpty(t.Name);
                    Assert.IsNotEmpty(t.Desc);
                }
            Assert.IsNull(StarTraits.At(Defs.Hero(HeroId.ShiXiu), 1));
        }

        [Test]
        public void ThreeStarBladeSendsQiDownTheColumn()
        {
            Shot Swing(int star, out Battle b)
            {
                b = new Battle(1, Lineup);
                var slot = b.Slots[0];
                slot.Unit = new Unit { Hero = HeroId.ShiXiu, Star = star };
                b.Enemies.Add(new Enemy { Id = 900, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0f, Layout.StopY), Attacking = true });
                for (int i = 0; i < 120; i++)
                {
                    b.Update(1f / 60f);
                    if (b.Shots.Any(s => s.Kind == ShotKind.Punch)) break;
                }
                return b.Shots.FirstOrDefault(s => s.Kind == ShotKind.Qi);
            }
            Assert.IsNull(Swing(2, out _), "2 星还没有刀气");
            var qi = Swing(3, out var b3);
            Assert.IsNotNull(qi, "3 星挥刀带一道刀气");
            Assert.Greater(qi.To.Y, qi.From.Y, "刀气往敌阵方向飞");
            var behind = new Enemy { Id = 901, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(qi.From.X, qi.From.Y + 2f) };
            b3.Enemies.Add(behind);
            for (int i = 0; i < 60 && b3.Shots.Contains(qi); i++) b3.Update(1f / 60f);
            Assert.Less(behind.Hp, behind.MaxHp, "刀气打到身后同一列的敌人");
        }

        [Test]
        public void ForceWinEndsAtFullWall()
        {
            var b = new Battle(1, Lineup);
            b.Wall = 10;
            b.Enemies.Add(new Enemy());
            b.Shots.Add(new Shot());
            b.Phase = Phase.Choosing;
            b.PendingReels = 1;
            b.ForceWin();
            Assert.AreEqual(Phase.Victory, b.Phase);
            Assert.AreEqual(b.WallMax, b.Wall);
            Assert.AreEqual(0, b.Enemies.Count);
            Assert.AreEqual(0, b.Shots.Count);
            Assert.AreEqual(0, b.PendingReels);
            Assert.IsTrue(b.Events.Any(e => e.Type == Ev.Victory));
            b.ForceWin();
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void ForceLoseBreaksTheWallOnTheCurrentWave()
        {
            var b = new Battle(1, Lineup);
            var foe = new Enemy();
            b.Enemies.Add(foe);
            b.Shots.Add(new Shot());
            b.Wave = 4;
            b.Phase = Phase.Running;
            b.ForceLose();
            Assert.AreEqual(Phase.Defeat, b.Phase);
            Assert.AreEqual(0, b.Wall);
            Assert.AreEqual(4, b.Wave);
            Assert.AreSame(foe, b.Enemies[0]);
            Assert.AreEqual(0, b.Shots.Count);
            Assert.IsTrue(b.Events.Any(e => e.Type == Ev.Defeat));
            b.ForceWin();
            Assert.AreEqual(Phase.Defeat, b.Phase);

            var early = new Battle(2, Lineup);
            early.ForceLose();
            Assert.AreEqual(1, early.Wave);
            Assert.AreEqual(Phase.Defeat, early.Phase);
        }
    }
}

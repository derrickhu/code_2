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
            for (int i = 0; i < Defs.SummonsPerSlot; i++)
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
        public void OpeningWavesRampToMowingDensityWithConservedPool()
        {
            Assert.AreEqual(10, Defs.WaveCount(1));
            for (int w = 2; w <= 10; w++) Assert.Greater(Defs.WaveCount(w), Defs.WaveCount(w - 1));
            Assert.GreaterOrEqual(Defs.WaveCount(10), 90, "第10波接近百怪割草密度");
            Assert.Greater(Defs.Tough(1), Defs.Tough(6));
            Assert.That(Defs.Tough(6), Is.InRange(1.1f, 2.0f), "单怪厚度扎实，总血池与银两守恒");

            // 验证第1波小怪散着持续刷出（直到 ~21s+ 仍有新怪出门，杜绝 8s 出完后中间空一大截断档）
            var b = new Battle(1, Lineup, Defs.Level(1));
            int spawnedEarly = 0, spawnedMid = 0, spawnedLate = 0;
            for (int s = 1; s <= 23; s++)
            {
                int prev = b.Enemies.Count;
                for (int k = 0; k < 20; k++) b.Update(0.05f);
                int delta = b.Enemies.Count - prev;
                if (s <= 8) spawnedEarly += delta;
                else if (s <= 16) spawnedMid += delta;
                else spawnedLate += delta;
            }
            Assert.GreaterOrEqual(spawnedEarly, 2);
            Assert.GreaterOrEqual(spawnedMid, 2, "波中段必须持续有怪散着走下来，不得断档留空");
            Assert.GreaterOrEqual(spawnedLate, 2, "波尾段必须衔接下一波，不得空等十余秒");
        }

        [Test]
        public void StarMultipliersFollowDiminishingCurveAndMergeShocksNearWall()
        {
            Assert.AreEqual(1.0f, Defs.StarMul[0], 1e-4f);
            Assert.AreEqual(2.0f, Defs.StarMul[1], 1e-4f, "1→2星整整翻倍，性价比最高");
            Assert.Less(Defs.StarMul[2], Defs.StarMul[1] * 2f, "2→3星边际微敛，靠质变和词条放大");
            Assert.That(Defs.StarMul[3], Is.InRange(Defs.StarMul[2] * 1.15f, Defs.StarMul[2] * 1.45f), "4星遵循蔚蓝黄金衰减律，靠免费觉醒词条与大核载体拉开上限");
            var d = Defs.Hero(HeroId.HuaRong);
            Assert.AreEqual(1, d.Count, "1星标准单发起步，弹幕靠词条成长");
            Assert.AreEqual(d.Count, Defs.ShotsAt(d, 1));

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
            CollectionAssert.AreEqual(
                new[] { 10, 20, 30, 45, 60, 80, 90, 110, 130, 150, 160, 175, 190, 200, 215, 235, 260, 290, 320, 360, 410, 470, 540, 630, 750, 1100, 1800 },
                Enumerable.Range(0, 27).Select(Defs.SummonCost).ToArray());
            // 纯查表不走线性加价：超出表长固定取最后一格
            Assert.AreEqual(1800, Defs.SummonCost(27));
            Assert.AreEqual(1800, Defs.SummonCost(35));
            CollectionAssert.AreEqual(new[] { 100, 200, 400, 600, 1000, 1500, 1800, 2500, 3000 }, Enumerable.Range(0, 9).Select(Defs.EnhanceCost).ToArray());
            Assert.AreEqual(3000, Defs.EnhanceCost(9));
            Assert.AreEqual(3000, Defs.EnhanceCost(15));
            // 开局 100 银两固定召唤 3 人（10+20+30=60，余 40 < 第 4 抽 45）
            var open = new Battle(1, Lineup);
            Assert.AreEqual(100, open.Coins);
            for (int i = 0; i < 3; i++) Assert.IsNotNull(open.Summon());
            Assert.IsFalse(open.CanSummon, "开局保持固定召唤 3 人不变");
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
            for (int i = 0; i < 80; i++) { b.Update(1f / 30f); b.Events.Clear(); }
            Assert.AreEqual(1, b.LordCasts);
            Assert.Less(near.Hp, near.MaxHp);
            Assert.AreEqual(far.MaxHp, far.Hp);
            Assert.Greater(b.LordDamage, b.LordPower * 0.9f);
            Assert.AreEqual(Defs.LordCdBase, b.LordT, 2.8f);
        }

        [Test]
        public void LordSwordFliesAtTheCrowdNotTheNearestLoner()
        {
            var b = new Battle(3, Lineup);
            var loner = new Enemy { Id = 1, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(-0.4f, Layout.WallRowY + 1.1f), Attacking = true };
            var c1 = new Enemy { Id = 2, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0.8f, Layout.WallRowY + 2.4f), Attacking = true };
            var c2 = new Enemy { Id = 3, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(1.5f, Layout.WallRowY + 2.6f), Attacking = true };
            var c3 = new Enemy { Id = 4, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(1.1f, Layout.WallRowY + 3.1f), Attacking = true };
            foreach (var e in new[] { loner, c1, c2, c3 })
            {
                Assert.IsTrue(Layout.InLordRange(e.Pos));
                b.Enemies.Add(e);
            }
            b.LordT = 0.01f;
            b.Update(0.05f);
            Assert.AreEqual(1, b.LordCasts);
            var crowd = new V2((0.8f + 1.5f + 1.1f) / 3f, Layout.WallRowY + (2.4f + 2.6f + 3.1f) / 3f);
            int slashes = 0;
            foreach (var s in b.Shots)
            {
                if (s.Kind != ShotKind.Slash) continue;
                slashes++;
                Assert.Less((s.To - crowd).Len, (s.To - loner.Pos).Len, "剑气落点靠近人群，不追更近的落单者");
            }
            Assert.AreEqual(4, slashes, "半圆里的人都会被打到");
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

            // 精英怪阵亡只给赏银，不再触发第二个生辰纲（全关仅首领掉唯一一次生辰纲）
            var bElite = new Battle(4, Lineup, Defs.Level(5));
            var elite = new Enemy { Id = 998, Def = Defs.Enemy(EnemyKind.Elite), Hp = 1, MaxHp = 1, Pos = new V2(0, 3) };
            bElite.Enemies.Add(elite);
            bElite.Slots[0].Unit = new Unit { Hero = HeroId.GongSun, Star = 4 };
            for (int i = 0; i < 120; i++) bElite.Update(1f / 30f);
            Assert.IsTrue(elite.Dead);
            Assert.AreEqual(0, bElite.PendingReels, "精英阵亡不得再掉落第二个生辰纲");
            Assert.AreEqual(Phase.Running, bElite.Phase);
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
            Assert.GreaterOrEqual(Defs.Hero(HeroId.FanRui).Interval, 12f);
            Assert.Greater(Defs.Hero(HeroId.FanRui).Chan, 0);
            foreach (var d in Defs.Heroes)
            {
                if (d.Kind == Role.Mage && d.Chan > 0) Assert.GreaterOrEqual(d.Interval, 10f, d.Name);
                if (d.Kind == Role.Support)
                {
                    Assert.IsTrue(d.Buff.HasValue, d.Name);
                    Assert.AreEqual(0f, d.Coef, d.Name);
                }
                else if (d.Id != HeroId.YanQing)
                {
                    Assert.AreEqual(1, d.Count, $"{d.Name} 1星基础应为极简1发，把弹幕增长留给强化词条");
                }
                Assert.AreEqual(d.Rarity >= 4, Defs.UltPerk(d.Id) != null, d.Name);
            }
            Assert.AreEqual(3, Defs.Hero(HeroId.YanQing).Count, "仅燕青保留轻弩三连发特色");
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
        public void LegendUltUnlocksAtLevelFiveFromEnhanceCard()
        {
            // 1~4 级传说尚未解锁大招卡进池
            var loLow = Loadout.Plain(Lineup);
            loLow.Lv[(int)HeroId.GongSun] = Meta.UltLv - 1;
            var bLow = new Battle(4, Lineup, null, loLow);
            Put(bLow, 0, HeroId.GongSun);
            for (int i = 0; i < 20; i++)
            {
                bLow.Coins = 1000000;
                if (!bLow.Enhance()) break;
                Assert.IsFalse(bLow.Choices.Any(p => p.Ult), "未满5级的传说好汉不出大招卡");
                bLow.Pick(0);
            }

            // 升至 5 级后大招卡进入强化池
            var lo = Loadout.Plain(Lineup);
            lo.Lv[(int)HeroId.GongSun] = Meta.UltLv;
            var b = new Battle(4, Lineup, null, lo);
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
            var lu = new[] { HeroId.HuaRong, HeroId.LingZhen, HeroId.FanRui, HeroId.WuSong };
            var b = new Battle(5, lu);
            Put(b, 0, HeroId.FanRui);
            Dummy(b);
            int fires = 0, ticks = 0;
            for (int i = 0; i < 30 * 5; i++)
            {
                b.Update(1f / 30f);
                foreach (var e in b.Events)
                {
                    if (e.Type == Ev.Fire && e.Slot == 0) fires++;
                    if (e.Type == Ev.Hit && e.Hero == HeroId.FanRui) ticks++;
                }
                b.Events.Clear();
            }
            Assert.AreEqual(1, fires, "持续型法师 5 秒内只出手一次");
            Assert.GreaterOrEqual(ticks, 8, "一次施法持续跳多下");
        }

        [Test]
        public void SingleProjectileBaseDoublesCleanlyWithCountPerkAndAwakenVolley()
        {
            // 1. 验证孔亮1星仅投掷1枚火雷，选「双响火雷(Count+1)」后变为2枚且单发零暗扣（总伤害实打实翻倍）
            var lu = new[] { HeroId.KongLiang, HeroId.XieZhen, HeroId.ShiXiu, HeroId.MaLing };
            var b0 = new Battle(21, lu);
            Put(b0, 0, HeroId.KongLiang, 1);
            var foe0 = Dummy(b0, 0, 2.5f);
            b0.Update(0.05f);
            Assert.AreEqual(1, b0.Shots.Count(s => s.Hero == HeroId.KongLiang), "孔亮1星0词条每次只投1枚独火雷");
            float baseShotDmg = b0.Shots.First(s => s.Hero == HeroId.KongLiang).Damage;

            var b1 = new Battle(21, lu);
            Put(b1, 0, HeroId.KongLiang, 1);
            Dummy(b1, 0, 2.5f);
            b1.Choices = new[] { Defs.Perk("kongliang_0") }; // 双响火雷 Count+1
            b1.Phase = Phase.Choosing;
            b1.Pick(0);
            b1.Update(0.05f);
            var shots1 = b1.Shots.Where(s => s.Hero == HeroId.KongLiang).ToList();
            Assert.AreEqual(2, shots1.Count, "拿1张双响火雷后投出2枚");
            Assert.GreaterOrEqual(shots1.Sum(s => s.Damage), baseShotDmg * 1.99f, "废除15%暗扣后，1→2枚真实总伤翻倍");

            // 2. 验证三合一奥义觉醒后第3轮普攻额外迸发流星火雨弹幕
            b1.Choices = new[] { Defs.Perk("kongliang_morph") };
            b1.Phase = Phase.Choosing;
            b1.Pick(0);
            b1.Choices = new[] { Defs.Perk("kongliang_adv") };
            b1.Phase = Phase.Choosing;
            b1.Pick(0);
            Assert.IsTrue(b1.IsAwakened(HeroId.KongLiang));
            b1.Shots.Clear();
            b1.Slots[0].Unit.Casts = 2;
            b1.Slots[0].Unit.Cd = 0f;
            b1.Update(0.05f);
            Assert.GreaterOrEqual(b1.Shots.Count(s => s.Kind == ShotKind.MiniBomb && s.Hero == HeroId.KongLiang), 4, "觉醒后第3轮额外倾泻流星小火雷");
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
            var lu = new[] { HeroId.HuaRong, HeroId.LingZhen, HeroId.FanRui, HeroId.WuSong };
            var b = new Battle(7, lu);
            var mage = Put(b, 1, HeroId.FanRui);
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

        [Test]
        public void WildcardSupportMergeBonusAndSwapOrderSolveSupportBrick()
        {
            var lu = new[] { HeroId.HuaRong, HeroId.WuYong, HeroId.ShiQian, HeroId.WuSong };
            var b = new Battle(11, lu);
            // 1. 百搭时迁可与任意同星不同名好汉合成，且辅助参与合成当场爆银两并赋加速
            b.Slots[0].Unit = new Unit { Id = 1, Hero = HeroId.ShiQian, Star = 1, Wild = true };
            b.Slots[1].Unit = new Unit { Id = 2, Hero = HeroId.HuaRong, Star = 1 };
            Assert.IsTrue(b.CanMerge(0, 1));
            Assert.IsTrue(b.HasMergePartner(0));
            int coins0 = b.Coins;
            Assert.IsTrue(b.Drop(0, 1));
            Assert.AreEqual(2, b.Slots[1].Unit.Star);
            Assert.Greater(b.Coins, coins0 + 20, "辅助参与合成当场额外爆银两");
            Assert.AreEqual(4, b.Slots[1].Unit.BlessN, "辅助参与合成赋予新单位加速祝福");

            // 2. 梁山换将令：将场上 3 星辅助当场重随为同星输出好汉，每局仅限 1 次
            b.Slots[2].Unit = new Unit { Id = 3, Hero = HeroId.WuYong, Star = 3 };
            Assert.IsTrue(b.CanSwapUnit(2));
            Assert.IsTrue(b.SwapUnit(2));
            Assert.AreEqual(3, b.Slots[2].Unit.Star);
            Assert.IsTrue(b.Slots[2].Unit.Def.Carry, "换将令必出输出位好汉");
            Assert.IsFalse(b.CanSwapUnit(2), "每局仅可使用 1 次换将令");
        }

        [Test]
        public void NearWallEmergencyAndFocusFirePreventBossTunnelVision()
        {
            var b = new Battle(12, Lineup);
            Put(b, 0, HeroId.HuaRong, 1);
            var farBoss = new Enemy { Id = 101, Def = Defs.Enemy(EnemyKind.Boss), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0, 4.2f) };
            var wallChewer = new Enemy { Id = 102, Def = Defs.Enemy(EnemyKind.Hound), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0, Layout.StopY), Attacking = true };
            var manualTarget = new Enemy { Id = 103, Def = Defs.Enemy(EnemyKind.Taoist), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(1.5f, 3.5f) };
            b.Enemies.AddRange(new[] { farBoss, wallChewer, manualTarget });

            b.Update(0.05f);
            Assert.IsTrue(b.Shots.Any(s => s.TargetId == wallChewer.Id), "墙根拆家快怪优先级高于远端刚出门的首领");

            b.Shots.Clear();
            b.Slots[0].Unit.Cd = 0f;
            b.FocusEnemyId = manualTarget.Id;
            b.Update(0.05f);
            Assert.IsTrue(b.Shots.Any(s => s.TargetId == manualTarget.Id), "玩家点击集火目标拥有最高优先级");
        }

        [Test]
        public void ThreeInOneAwakeningAndLv20PenaltyRemovalAndAdjacencyWork()
        {
            var lo = Loadout.Plain(new[] { HeroId.WuSong, HeroId.LuZhiShen, HeroId.LinChong, HeroId.WuYong });
            lo.Lv[(int)HeroId.WuSong] = Meta.NoPenaltyLv;
            var b = new Battle(15, lo.Lineup, Defs.Level(5), lo);
            Assert.AreEqual(5, b.ReelCount, "逢5首领关升级为5格黄金老虎机");
            Assert.AreEqual(4, new Battle(15, lo.Lineup, Defs.Level(2), lo).ReelCount);

            Put(b, 0, HeroId.WuSong, 2);
            Put(b, 1, HeroId.WuYong, 1);
            Assert.Greater(b.AdjSpeedMul(b.Slots[0]), 1.1f, "吴用相邻格获得攻速加成");

            float spd0 = b.HeroSpeedMul(HeroId.WuSong);
            // 20级武松拿「千钧之力」不再扣攻速
            b.Choices = new[] { Defs.Perk("wusong_heavy") };
            b.Phase = Phase.Choosing;
            b.Pick(0);
            Assert.GreaterOrEqual(b.HeroSpeedMul(HeroId.WuSong), spd0 - 1e-4f, "20级抹除取舍卡减速代价");

            // 再拿质变+进阶凑齐 3 张词条，自动免费触发名场面奥义觉醒
            b.Choices = new[] { Defs.Perk("wusong_morph") };
            b.Phase = Phase.Choosing;
            b.Pick(0);
            Assert.IsFalse(b.IsAwakened(HeroId.WuSong));
            float dmgBefore = b.HeroDmgMul(HeroId.WuSong);
            b.Choices = new[] { Defs.Perk("wusong_adv") };
            b.Phase = Phase.Choosing;
            b.Pick(0);
            Assert.IsTrue(b.IsAwakened(HeroId.WuSong), "凑齐质变+进阶+专属共3张词条自动觉醒本命奥义");
            Assert.IsTrue(b.Events.Any(e => e.Type == Ev.Awaken && e.Hero == HeroId.WuSong));
            Assert.Greater(b.HeroDmgMul(HeroId.WuSong), dmgBefore * 1.25f);
        }

        [Test]
        public void OutOfBattleAndInBattleAttackAndHitDamageMatchExactly()
        {
            // 验证局外好汉详情页显示的攻击力/单发伤害 与 局内1星0词条面板及真实命中伤害 100% 同源一致（彻底根除 294 vs 56 割裂）
            foreach (var n in new[] { 1, 8, 20 })
            {
                var s = Meta.ExpectedSave(n);
                var lo = Meta.BuildLoadout(s);
                // 使用第1关（无任何职业环境Buff）纯粹核对面板与首击伤害
                var b = new Battle(42, lo.Lineup, Defs.Level(1), lo);
                for (int i = 0; i < lo.Lineup.Length; i++)
                {
                    var h = lo.Lineup[i];
                    var d = Defs.Hero(h);
                    Put(b, i, h, 1);
                    int outAtk = Meta.HeroDisplayAtk(s, h);
                    int inAtk = (int)System.Math.Round(b.SlotHeroAtk(b.Slots[i]));
                    Assert.AreEqual(outAtk, inAtk, $"存档阶段{n} {d.Name} 局外面板攻击({outAtk}) 必须等于局内1星面板攻击({inAtk})");

                    if (d.Carry && d.Chan <= 0)
                    {
                        b.Enemies.Clear();
                        b.Shots.Clear();
                        var dummy = new Enemy { Id = 800 + i, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(b.Slots[i].Pos.X, Layout.StopY), Attacking = true };
                        b.Enemies.Add(dummy);
                        b.Slots[i].Unit.Cd = 0f;
                        // 暂时清零暴击率以精确比对不暴击单发白字伤害
                        float savedCrit = lo.Crit;
                        lo.Crit = 0f;
                        b.Update(0.02f);
                        lo.Crit = savedCrit;
                        var sh = b.Shots.FirstOrDefault(x => x.Hero == h);
                        Assert.IsNotNull(sh, d.Name);
                        float rawShotDmg = sh.Crit ? sh.Damage / lo.CritDmg : sh.Damage;
                        Assert.AreEqual(b.SlotHeroAtk(b.Slots[i]) * d.Coef, rawShotDmg, 0.5f, $"{d.Name} 真实发射伤害必须等于 面板攻击 × 技能系数");
                    }
                }
            }
        }

        [Test]
        public void OpeningMinionsRequireMultipleHitsAndEnhancesAreMandatoryToClear()
        {
            // 1. 杜绝开场一刀秒：在各章节标准养成下，第1波普通官兵面对1星0词条射手需 2.4~4.5 击，面对1星战士/炮手/法师也绝不能满血一击秒杀
            foreach (var stageNo in new[] { 1, 5, 10, 20, 35 })
            {
                var s = Meta.ExpectedSave(stageNo);
                var lo = Meta.BuildLoadout(s);
                var st = Defs.Level(stageNo);
                var b = new Battle(1, lo.Lineup, st, lo);
                b.Update(0.5f); // 触发第1波刷怪
                float soldierHp = Defs.Enemy(EnemyKind.Soldier).Hp * Defs.HpScale(1) * Defs.Tough(1) * st.HpMul;
                foreach (var h in lo.Lineup)
                {
                    var d = Defs.Hero(h);
                    if (!d.Carry || d.Chan > 0) continue;
                    // 1) 纯白字不暴击单发：任何关卡任何职业1星0词条绝不能满血1下秒杀官兵
                    float rawHit = Meta.HeroBaseAtk(lo, h) * lo.SkillPow[(int)h] * d.Coef * d.Count;
                    Assert.GreaterOrEqual(soldierHp / rawHit, d.Kind == Role.Archer ? 2.2f : 1.35f,
                        $"第{stageNo}关 1星 {d.Name} 绝不能1下秒满血官兵，实际={soldierHp / rawHit:F2}");
                    // 2) 计入攻速与暴击期望后的单轮等效输出：射手在全50关始终维持 2.2~4.8 轮点杀节奏，不刮痧也不秒杀
                    if (d.Kind == Role.Archer)
                    {
                        float critExp = 1f + System.Math.Min(0.8f, lo.Crit + (lo.Passive[(int)h] && d.PassiveEff == Eff.Crit ? d.PassiveV : 0f)) * (lo.CritDmg - 1f);
                        float effVolley = rawHit * critExp * lo.AllSpeed * lo.HeroSpeed[(int)h];
                        Assert.That(soldierHp / effVolley, Is.InRange(2.8f, 6.4f), $"第{stageNo}关 1星射手 {d.Name} 等效轮次应在 2.8~6.4 之间，实际={soldierHp / effVolley:F2}");
                    }
                }
            }

            // 2. 局内策略必要性：如果只召唤+无脑合成（Bot 1，完全不买强化词条），在首领关大概率崩盘失败！
            int noEnhanceWins = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var s = Meta.ExpectedSave(10);
                var lo = Meta.BuildLoadout(s);
                var b = new Battle(seed, lo.Lineup, Defs.Level(10), lo);
                var mergeOnlyBot = new Bot(1);
                while (b.Phase != Phase.Victory && b.Phase != Phase.Defeat && b.Time < 500)
                {
                    mergeOnlyBot.Step(b, 1f / 30f);
                    b.Update(1f / 30f);
                    b.Events.Clear();
                }
                if (b.Phase == Phase.Victory) noEnhanceWins++;
            }
            Assert.LessOrEqual(noEnhanceWins, 2, $"不买局内强化词条光靠铺人合成不该混过首领关，实际胜场={noEnhanceWins}/10");
        }

        [Test]
        public void HeroSkillBehaviorMatchesDescriptions()
        {
            // 1. 溅射/近战暴击保留 Crit 标记；击退是持续平滑滑退而非单帧瞬移
            var lo = Loadout.Plain(new[] { HeroId.ShiXiu, HeroId.XieZhen, HeroId.HuSanniang, HeroId.TangLong });
            lo.Crit = 1f; // 100% 暴击核验 Splash Crit 透传
            var b = new Battle(7, lo.Lineup, Defs.Level(1), lo);
            Put(b, 0, HeroId.ShiXiu, 3);
            var foe1 = new Enemy { Id = 910, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(b.Slots[0].Pos.X, Layout.StopY), Attacking = true };
            var foe2 = new Enemy { Id = 911, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(b.Slots[0].Pos.X + 0.5f, Layout.StopY), Attacking = true };
            var hawk = new Enemy { Id = 912, Def = Defs.Enemy(EnemyKind.Hawk), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(b.Slots[0].Pos.X, Layout.StopY + 0.5f) };
            b.Enemies.AddRange(new[] { foe1, foe2, hawk });
            b.Slots[0].Unit.Cd = 0f;
            b.Update(0.02f);
            var cut = b.Shots.First(s => s.Kind == ShotKind.Punch);
            var qi = b.Shots.First(s => s.Kind == ShotKind.Qi);
            Assert.AreEqual(cut.Damage / lo.CritDmg * 0.65f, qi.Damage, 0.05f, "3星刀气伤害严格等于单刀基础伤害的 65%");

            b.Events.Clear();
            while (b.Shots.Contains(cut)) b.Update(1f / 60f);
            Assert.IsTrue(b.Events.Any(e => e.Type == Ev.Damage && e.EnemyId == foe1.Id && e.Crit), "石秀暴击必须打出暴击飘字");
            Assert.IsTrue(b.Events.Any(e => e.Type == Ev.Damage && e.EnemyId == foe2.Id && e.Crit), "小范围溅射目标同样获得暴击标记");
            Assert.Greater(foe1.Pos.Y - Layout.StopY, 0.30f, "命中后产生肉眼清晰可见的击退距离");
            Assert.IsFalse(foe1.Attacking, "击退打断啃墙状态");

            // 2. 3星近战绝不对飞行怪空挥
            b.Enemies.Clear();
            b.Shots.Clear();
            b.Enemies.Add(hawk);
            b.Slots[0].Unit.Cd = 0f;
            b.Update(0.05f);
            Assert.IsEmpty(b.Shots, "近战打不到飞行敌人，不应朝飞行怪空挥");

            // 3. 20级即免除取舍强化负面代价（与里程碑说明一致）
            Assert.AreEqual(20, Meta.NoPenaltyLv);

            // 4. 扈三娘 4星落点震慑周围敌人；汤隆 1星打造神兵为 +40% 起
            var b2 = new Battle(9, lo.Lineup, Defs.Level(1), Loadout.Plain(lo.Lineup));
            Put(b2, 0, HeroId.HuSanniang, 4);
            Put(b2, 1, HeroId.TangLong, 1);
            var mainT = new Enemy { Id = 920, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0f, 0f) };
            var nearT = new Enemy { Id = 921, Def = Defs.Enemy(EnemyKind.Soldier), Hp = 1e6f, MaxHp = 1e6f, Pos = new V2(0.8f, 0f) };
            b2.Enemies.AddRange(new[] { mainT, nearT });
            b2.Slots[0].Unit.Cd = 0f;
            b2.Slots[1].Unit.Cd = 0f;
            b2.Update(0.02f);
            Assert.AreEqual(0.4f, b2.ForgeV, 0.01f, "汤隆1星神兵加成严格从 40% 起步");
            for (int f = 0; f < 60 && b2.Shots.Count > 0; f++) b2.Update(1f / 60f);
            Assert.Greater(nearT.Stun, 0f, "扈三娘4星天罗地网落点震慑周围敌人");
        }

        [Test]
        public void BalanceManagementSystemAndSmoothedEconomyWork()
        {
            // 1. 小兵击杀赏银随波次温和上升，彻底消除第3波跌回5银两的倒挂；小兵有效血量前5波单调上升
            var soldier = Defs.Enemy(EnemyKind.Soldier);
            Assert.GreaterOrEqual(Defs.KillCoin(soldier, 3, 0f), Defs.KillCoin(soldier, 1, 0f));
            Assert.Greater(Defs.KillCoin(soldier, 10, 0f), Defs.KillCoin(soldier, 1, 0f));
            for (int w = 2; w <= 10; w++)
                Assert.GreaterOrEqual(Defs.Tough(w) * Defs.HpScale(w), Defs.Tough(w - 1) * Defs.HpScale(w - 1) * 0.99f);

            // 2. 生辰纲去泡沫核验：第7波5格首领全银两大奖收敛至 ~1060 银两（不再爆 3800+），转盘占整局经济比重 < 30%
            var bBoss = new Battle(3, Lineup, Defs.Level(5)) { Wave = 7, Phase = Phase.Reels, PendingReels = 1 };
            int bossAllSilver = bBoss.ReelSilverOf(new[] { Reel.Silver, Reel.Silver, Reel.Silver, Reel.Silver, Reel.Silver });
            Assert.That(bossAllSilver, Is.InRange(700, 1300), $"首领全银两生辰纲应适度惊喜无泡沫，实际={bossAllSilver}");
            var est = Balance.EstimateTenWaveEconomy(true, 7);
            Assert.Less(est.TreasureShare, 0.30f, $"生辰纲+首领期望占比应低于30%，实际={est.TreasureShare:P1}");

            // 3. 首领压血分段爆钱袋 + 波次推进发放守城军饷
            var b = new Battle(5, Lineup, Defs.Level(1));
            b.Update(0.1f);
            // 推进过第 1 波时长触发进第 2 波发放军饷（清空场上官兵避免拆坏寨墙）
            for (int i = 0; i < 600 && b.Wave == 1; i++)
            {
                b.Enemies.Clear();
                b.Update(0.05f);
            }
            Assert.AreEqual(2, b.Wave);
            Assert.Greater(b.CoinsFromWaves, 0, "每撑过一波发放守城军饷");

            var boss = new Enemy { Id = 777, Def = Defs.Enemy(EnemyKind.Boss), Hp = 1000f, MaxHp = 1000f, Pos = new V2(0, 2f) };
            b.Enemies.Add(boss);
            b.Shots.Add(new Shot { Id = 1, Kind = ShotKind.Arrow, Hero = HeroId.HuaRong, From = default, To = boss.Pos, TargetId = boss.Id, Dur = 0.01f, Damage = 700f });
            b.Update(0.02f);
            Assert.AreEqual(2, boss.BossDrops, "首领压过 70% 与 35% 血线各爆一次阶段钱袋");
            Assert.Greater(b.CoinsFromBossPhase, 100);

            // 4. Resources/BalanceConfig.json 完整性与动态热改数值回退验证
            var txt = UnityEngine.Resources.Load<UnityEngine.TextAsset>("BalanceConfig");
            Assert.IsNotNull(txt, "缺少 Resources/BalanceConfig.json");
            var parsed = UnityEngine.JsonUtility.FromJson<BalanceConfig>(txt.text);
            Assert.IsNotNull(parsed);
            Assert.AreEqual(Defs.HeroCount, parsed.Heroes.Length);
            Assert.AreEqual(Defs.Enemies.Length, parsed.Enemies.Length);

            try
            {
                var custom = Balance.CreateDefault();
                custom.Economy.StartCoins = 155;
                custom.Economy.EnhanceTable[0] = 66;
                custom.Heroes[0].Coef = 2.22f;
                Balance.Apply(custom);
                Assert.AreEqual(155, new Battle(1, Lineup).Coins);
                Assert.AreEqual(66, Defs.EnhanceCost(0));
                Assert.AreEqual(2.22f, Defs.Heroes[0].Coef, 1e-4f);
            }
            finally
            {
                Balance.ResetToDefault();
            }
            Assert.AreEqual(100, new Battle(1, Lineup).Coins);
            Assert.AreEqual(100, Defs.EnhanceCost(0));
        }
    }
}

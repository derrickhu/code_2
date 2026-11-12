namespace Liangshan.Sim
{
    /// <summary>自动玩家：数值验证和自动截图用。Skill 0 只会召唤，1 会合成，2 按《永远的蔚蓝星球》策略运营（先铺双2星、抢低费核心词条、联动破冰/共鸣升星）。</summary>
    public sealed class Bot
    {
        readonly int _skill;
        float _think;

        public Bot(int skill) { _skill = skill; }

        public void Step(Battle b, float dt)
        {
            if (b.Phase == Phase.Choosing)
            {
                int pick = 0;
                if (_skill >= 2)
                {
                    float best = -999f;
                    for (int i = 0; i < b.Choices.Length; i++)
                    {
                        var c = b.Choices[i];
                        var hd = Defs.Hero(c.Hero);
                        float score = BestStar(b, c.Hero) * 4f + (hd.Carry ? 2f : 0f);
                        if (c.Ult) score += 45f;
                        if (c.Kind == PerkKind.Adv) score += 16f;
                        else if (c.Kind == PerkKind.Morph) score += 10f;
                        if (c.Eff == Eff.Surge || c.Eff == Eff.Top || c.Eff == Eff.Resonance) score += 8f;
                        if (c.Eff == Eff.Shatter) score += 7f;
                        if (c.Eff == Eff.Exec && b.Wall < b.WallMax * 0.7f) score += 9f;
                        if (c.Eff == Eff.Dmg || c.Eff == Eff.Area || c.Eff == Eff.Count) score += 3f;
                        if (score > best) { best = score; pick = i; }
                    }
                }
                b.Pick(pick);
                return;
            }
            if (b.Phase == Phase.Reels)
            {
                b.ApplyReels(b.SpinReels());
                return;
            }
            if (b.Phase != Phase.Running) return;
            _think -= dt;
            if (_think > 0) return;
            _think = 0.32f;

            int units = Occupied(b);
            // 100/200 低费强化性价比极高，场上站稳 4 人后优先拿前两手词条
            if (_skill >= 2 && units >= 4 && b.Enhances < 2 && b.Coins >= b.EnhanceCost && b.Enhance()) return;
            if (_skill >= 1 && TryMerge(b)) return;
            if (_skill >= 2 && b.Coins >= b.EnhanceCost && b.EnhanceCost <= b.SummonCost * 1.85f && b.Enhance()) return;
            if (b.CanSummon) { b.Summon(); return; }
            if (_skill >= 2 && !b.HasEmptySlot() && b.Coins >= b.EnhanceCost) b.Enhance();
        }

        static int Occupied(Battle b)
        {
            int n = 0;
            foreach (var s in b.Slots) if (s.Unit != null) n++;
            return n;
        }

        static int BestStar(Battle b, HeroId h)
        {
            int best = 0;
            foreach (var s in b.Slots)
                if (s.Unit != null && s.Unit.Hero == h && s.Unit.Star > best) best = s.Unit.Star;
            return best;
        }

        static bool TryMerge(Battle b)
        {
            bool full = !b.HasEmptySlot();
            bool danger = b.Wall < b.WallMax * 0.55f;
            // 1→2 星 200% 翻倍零损耗，只要场上满 4 人就优先合出 2 星腾格子；
            // 2→3 星有边际衰减，仅当满格、危急需合击震退、或者是战士/已有强化词条时才合高星。
            for (int star = 3; star >= 1; star--)
            {
                if (!full && !danger)
                {
                    if (star == 1 && Occupied(b) < 4 && b.Coins >= b.SummonCost) continue;
                    if (star >= 2 && b.Enhances < 2) continue;
                }
                for (int i = 0; i < b.Slots.Count; i++)
                {
                    var u = b.Slots[i].Unit;
                    if (u == null || u.Star != star) continue;
                    if (!full && !danger && star >= 2 && u.Def.Kind != Role.Fighter && b.Enhances < 3) continue;
                    for (int j = i + 1; j < b.Slots.Count; j++)
                        if (b.CanMerge(i, j)) return b.Drop(i, j);
                }
            }
            return false;
        }
    }
}

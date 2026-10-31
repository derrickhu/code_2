namespace Liangshan.Sim
{
    /// <summary>自动玩家：数值验证和自动截图用。Skill 0 只会召唤，1 会合成，2 还会买强化、优先拿大招。</summary>
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
                    for (int i = 0; i < b.Choices.Length; i++)
                        if (b.Choices[i].Ult) { pick = i; break; }
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
            _think = 0.35f;

            if (_skill >= 1 && TryMerge(b)) return;
            if (_skill >= 2 && b.FreeEnhances > 0 && b.Enhance()) return;
            if (_skill >= 2 && b.Coins >= b.EnhanceCost && b.EnhanceCost < b.SummonCost * 1.6f && b.Enhance()) return;
            if (b.CanSummon) { b.Summon(); return; }
            if (_skill >= 2 && !b.HasEmptySlot() && b.Coins >= b.EnhanceCost) b.Enhance();
        }

        static bool TryMerge(Battle b)
        {
            bool full = !b.HasEmptySlot();
            for (int star = 3; star >= 1; star--)
            {
                // 不满格时只合二星以上，留着一星占位输出。
                if (!full && star == 1 && b.Coins >= b.SummonCost) continue;
                for (int i = 0; i < b.Slots.Count; i++)
                {
                    var u = b.Slots[i].Unit;
                    if (u == null || u.Star != star) continue;
                    for (int j = i + 1; j < b.Slots.Count; j++)
                        if (b.CanMerge(i, j)) return b.Drop(i, j);
                }
            }
            return false;
        }
    }
}

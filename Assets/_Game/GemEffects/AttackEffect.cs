namespace Match3
{
    [System.Serializable]
    public class AttackEffect : IGemEffect
    {
        public int DamagePerGem = 1;

        public void Apply(BattleState state, int matchedCount)
        {
            int totalDamage = DamagePerGem * matchedCount;

            foreach (var enemy in state.Enemies)
            {
                if (!enemy.Health.IsAlive) continue;
                totalDamage = enemy.Health.Hit(totalDamage);
                if (totalDamage <= 0) break;
            }
        }
    }
}

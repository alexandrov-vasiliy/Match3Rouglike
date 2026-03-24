namespace Match3
{
    [System.Serializable]
    public class HealEffect : IGemEffect
    {
        public int HealPerGem = 1;

        public void Apply(BattleState state, int matchedCount)
        {
            state.PlayerHealth.Heal(HealPerGem * matchedCount);
        }
    }
}

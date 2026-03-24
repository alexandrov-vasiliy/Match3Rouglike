namespace Match3
{
    [System.Serializable]
    public class DefenseEffect : IGemEffect
    {
        public int ArmorPerGem = 1;

        public void Apply(BattleState state, int matchedCount)
        {
            state.PlayerHealth.AddArmor(ArmorPerGem * matchedCount);
        }
    }
}

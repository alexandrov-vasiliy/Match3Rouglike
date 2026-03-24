namespace Match3
{
    [System.Serializable]
    public class GoldEffect : IGemEffect
    {
        public int GoldPerGem = 1;

        public void Apply(BattleState state, int matchedCount)
        {
            state.Coins += GoldPerGem * matchedCount;
        }
    }
}

namespace Match3
{
    [System.Serializable]
    public class BloodEffect : IGemEffect
    {
        public int BloodPerGem = 1;

        public void Apply(BattleState state, int matchedCount)
        {
            state.BloodResource += BloodPerGem * matchedCount;
        }
    }
}

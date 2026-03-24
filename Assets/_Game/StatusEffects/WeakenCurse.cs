namespace Match3
{
    [System.Serializable]
    public class WeakenCurse : IStatusEffect
    {
        public float DamageReductionPercent = 0.5f;
        public int Duration = 3;

        private int remainingTurns;

        public string EffectName => "Ослабление";
        public int RemainingTurns => remainingTurns;
        public bool IsExpired => remainingTurns <= 0;

        public void Init()
        {
            remainingTurns = Duration;
        }

        public void OnTurnStart(BattleState state)
        {
        }

        public void OnTurnEnd(BattleState state)
        {
            remainingTurns--;
        }
    }
}

namespace Match3
{
    public interface IStatusEffect
    {
        string EffectName { get; }
        int RemainingTurns { get; }
        bool IsExpired { get; }
        void OnTurnStart(BattleState state);
        void OnTurnEnd(BattleState state);
    }
}

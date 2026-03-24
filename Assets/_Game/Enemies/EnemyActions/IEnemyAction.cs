namespace Match3
{
    public interface IEnemyAction
    {
        void OnPlayerMoved(BattleState state);
        bool TryExecute(BattleState state);
    }
}

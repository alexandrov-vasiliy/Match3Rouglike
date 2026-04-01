using System.Collections;

namespace Match3
{
    public interface IEnemyAction
    {
        void OnPlayerMoved(BattleState state);
        IEnumerator TryExecute(BattleState state);
    }

    public interface IEnemyActionDisplayString
    {
        string GetActionString();
    }
}

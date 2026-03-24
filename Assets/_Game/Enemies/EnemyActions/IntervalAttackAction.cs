using UnityEngine;

namespace Match3
{
    [System.Serializable]
    public class IntervalAttackAction : IEnemyAction
    {
        public int AttackInterval = 3;
        public int Damage = 5;

        [SerializeField]
        private int moveCounter;

        public void OnPlayerMoved(BattleState state)
        {
            moveCounter++;
        }

        public bool TryExecute(BattleState state)
        {
            if (moveCounter < AttackInterval)
                return false;

            moveCounter = 0;
            state.PlayerHealth.Hit(Damage);
            return true;
        }
    }
}

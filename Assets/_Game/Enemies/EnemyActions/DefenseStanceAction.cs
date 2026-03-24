using UnityEngine;

namespace Match3
{
    [System.Serializable]
    public class DefenseStanceAction : IEnemyAction
    {
        public int Interval = 4;
        public int ArmorAmount = 3;

        [SerializeField]
        private int moveCounter;

        private EnemyDefinition owner;

        public void SetOwner(EnemyDefinition enemyDefinition)
        {
            owner = enemyDefinition;
        }

        public void OnPlayerMoved(BattleState state)
        {
            moveCounter++;
        }

        public bool TryExecute(BattleState state)
        {
            if (moveCounter < Interval)
                return false;

            moveCounter = 0;

            if (owner != null && owner.Health != null)
                owner.Health.AddArmor(ArmorAmount);

            return true;
        }
    }
}

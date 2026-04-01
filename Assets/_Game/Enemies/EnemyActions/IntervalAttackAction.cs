using System.Collections;
using UnityEngine;

namespace Match3
{
    [System.Serializable]
    public class IntervalAttackAction : IEnemyAction, IEnemyActionDisplayString
    {
        public int Damage = 5;


        public IntervalAttackAction(int damage)
        {
            Damage = damage;
        }

        public void OnPlayerMoved(BattleState state)
        {
        }

        public IEnumerator TryExecute(BattleState state)
        {
            state.PlayerHealth.Hit(Damage);
            Debug.Log("Attack Action");
            yield return new WaitForSeconds(1);
        }

        public string GetActionString()
        {
            return $"Attack {Damage}";
        }
    }
}

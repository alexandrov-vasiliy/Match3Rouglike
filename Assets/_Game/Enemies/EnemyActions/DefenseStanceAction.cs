using System.Collections;
using UnityEngine;

namespace Match3
{
    public class DefenseStanceAction :IEnemyAction, IEnemyActionDisplayString
    {
        public int ArmorAmount = 3;

        private EnemyDefinition owner;

        public void SetOwner(EnemyDefinition enemyDefinition)
        {
            owner = enemyDefinition;
        }

        public void OnPlayerMoved(BattleState state)
        {
        }

        public IEnumerator TryExecute(BattleState state)
        {

            if (owner != null && owner.Health != null)
                owner.Health.AddArmor(ArmorAmount);

            
            Debug.Log("Armor action");
            yield return new WaitForSeconds(1);
        }

        public string GetActionString()
        {
            return "Armor";
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class EnemyDefinition : MonoBehaviour
    {
        public string EnemyName;
        public int MaxHealth = 50;
        
        public Health Health { get; private set; }

        private int _currentAction = 0;

        public virtual List<IEnemyAction> GetActions()
        {
            return new();
        }

        public IEnemyAction GetCurrentAction()
        {
            var enemyActions = GetActions();
            
            if (enemyActions.Count == 0) throw new AggregateException($"{name} No have actions");

            if (_currentAction >= enemyActions.Count) _currentAction = 0;

            return enemyActions[_currentAction];
        }

        public IEnumerator ExecuteAction(BattleState state)
        {
            yield return GetCurrentAction().TryExecute(state);
            _currentAction++;
        }
        
        public void InitBattle()
        {
            Health = new Health(MaxHealth);

            foreach (var action in GetActions())
            {
                if (action is DefenseStanceAction defenseStance)
                    defenseStance.SetOwner(this);
            }
        }
    }
}

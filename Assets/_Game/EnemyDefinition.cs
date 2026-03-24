using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class EnemyDefinition : MonoBehaviour
    {
        public string EnemyName;
        public int MaxHealth = 50;

        [SerializeReference]
        public List<IEnemyAction> Actions = new();

        public Health Health { get; private set; }

        public void InitBattle()
        {
            Health = new Health(MaxHealth);

            foreach (var action in Actions)
            {
                if (action is DefenseStanceAction defenseStance)
                    defenseStance.SetOwner(this);
            }
        }
    }
}

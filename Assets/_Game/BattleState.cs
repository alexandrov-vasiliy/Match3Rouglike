using System.Collections.Generic;
using System.Linq;

namespace Match3
{
    public class BattleState
    {
        public Health PlayerHealth;
        public List<EnemyDefinition> Enemies = new();
        public int BloodResource;
        public int Coins;
        public int TotalMoves;
        public List<IStatusEffect> StatusEffects = new();

        public EnemyDefinition CurrentTarget =>
            Enemies.FirstOrDefault(enemy => enemy.Health.IsAlive);

        public bool AllEnemiesDead =>
            Enemies.All(enemy => !enemy.Health.IsAlive);
    }
}

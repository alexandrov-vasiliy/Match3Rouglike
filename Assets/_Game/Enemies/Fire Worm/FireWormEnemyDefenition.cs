using System.Collections.Generic;

namespace Match3.Fire_Worm
{
    public class FireWormEnemyDefenition : EnemyDefinition
    {
        public override List<IEnemyAction> GetActions()
        {
            return new()
            {
                new IntervalAttackAction(10),
                new DefenseStanceAction()
            };
        }
    }
}
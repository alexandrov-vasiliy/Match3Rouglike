namespace Match3
{
    public enum BattleResult
    {
        None,
        Win,
        Lose
    }

    public class BattleProcessor
    {
        private readonly BattleState state;

        public BattleProcessor(BattleState battleState)
        {
            state = battleState;
        }

        public void ProcessGemEffect(IGemEffect effect, int matchedCount)
        {
            if (effect == null) return;
            effect.Apply(state, matchedCount);
        }

        public void OnPlayerMoved()
        {
            state.TotalMoves++;

            foreach (var enemy in state.Enemies)
            {
                if (!enemy.Health.IsAlive) continue;

                foreach (var action in enemy.Actions)
                {
                    action.OnPlayerMoved(state);
                }
            }
        }

        public void ProcessEnemyTurn()
        {
            foreach (var enemy in state.Enemies)
            {
                if (!enemy.Health.IsAlive) continue;

                foreach (var action in enemy.Actions)
                {
                    action.TryExecute(state);
                }
            }

            ProcessStatusEffects();
        }

        private void ProcessStatusEffects()
        {
            for (int i = state.StatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = state.StatusEffects[i];
                effect.OnTurnEnd(state);

                if (effect.IsExpired)
                    state.StatusEffects.RemoveAt(i);
            }
        }

        public BattleResult CheckBattleEnd()
        {
            if (state.AllEnemiesDead)
                return BattleResult.Win;

            if (!state.PlayerHealth.IsAlive)
                return BattleResult.Lose;

            return BattleResult.None;
        }
    }
}

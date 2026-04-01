using System.Collections;
using UnityEngine;

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

        public IEnumerator OnPlayerMoved()
        {
            state.TotalMoves++;
            state.Energy -= 1;

            if (state.Energy == 0)
            {
                GameManager.Instance.Board.ToggleInput(false);
                yield return ProcessEnemyTurn();
                GameManager.Instance.Board.ToggleInput(true);
                state.Energy = state.MaxEnergy;
            }

            yield return null;
        }

        public IEnumerator ProcessEnemyTurn()
        {
            foreach (var enemy in state.Enemies)
            {
                if (!enemy.Health.IsAlive) continue;

                yield return enemy.ExecuteAction(state);
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
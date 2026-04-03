using TMPro;
using UnityEngine;

namespace Match3
{
    public class PlayerEnergyView : MonoBehaviour
    {
        public TMP_Text energy;

        private BattleFlowCoordinator subscribedBattleFlow;

        private void OnEnable()
        {
            subscribedBattleFlow = BattleFlowCoordinator.Instance;
            if (subscribedBattleFlow != null)
            {
                subscribedBattleFlow.OnBattleStateChanged += OnBattleStateChanged;
                RefreshText(subscribedBattleFlow.BattleState);
            }
        }

        private void OnDisable()
        {
            if (subscribedBattleFlow != null)
                subscribedBattleFlow.OnBattleStateChanged -= OnBattleStateChanged;

            subscribedBattleFlow = null;
        }

        private void OnBattleStateChanged(BattleState state)
        {
            RefreshText(state);
        }

        private void RefreshText(BattleState state)
        {
            if (energy == null || state == null)
                return;

            energy.text = $"{state.Energy} / {state.MaxEnergy}";
        }
    }
}

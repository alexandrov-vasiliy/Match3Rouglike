using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Presenter для UI персонажа. Подписывается на Health и BattleState,
    /// вызывает Set-методы Passive View при изменении данных.
    /// </summary>
    public class PlayerStatsView : MonoBehaviour
    {
        [SerializeField] private PlayerHpBarView hpBarView;
        [SerializeField] private PlayerArmorView armorView;
        [SerializeField] private PlayerBloodView bloodView;
        [SerializeField] private PlayerCoinsView coinsView;

        private Health playerHealth;
        private BattleState battleState;
        private BattleFlowCoordinator subscribedBattleFlow;

        private void Awake()
        {
            if (hpBarView == null) hpBarView = GetComponentInChildren<PlayerHpBarView>();
            if (armorView == null) armorView = GetComponentInChildren<PlayerArmorView>();
            if (bloodView == null) bloodView = GetComponentInChildren<PlayerBloodView>();
            if (coinsView == null) coinsView = GetComponentInChildren<PlayerCoinsView>();
        }

        public void Init(Health health, BattleState state)
        {
            UnsubscribeFromBattleFlow();

            playerHealth = health;
            battleState = state;

            if (playerHealth != null)
            {
                playerHealth.OnDamaged += RefreshHealth;
                playerHealth.OnHealed += RefreshHealth;
                playerHealth.OnArmorChanged += RefreshArmor;
                playerHealth.OnDied += HandleDied;
                RefreshHealth(0);
                RefreshArmor(playerHealth.Armor);
            }

            if (battleState != null)
            {
                RefreshBlood();
                RefreshCoins();
            }

            subscribedBattleFlow = BattleFlowCoordinator.Instance;
            if (subscribedBattleFlow != null)
                subscribedBattleFlow.OnBattleStateChanged += OnBattleStateChanged;
        }

        private void UnsubscribeFromBattleFlow()
        {
            if (subscribedBattleFlow != null)
                subscribedBattleFlow.OnBattleStateChanged -= OnBattleStateChanged;

            subscribedBattleFlow = null;
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= RefreshHealth;
                playerHealth.OnHealed -= RefreshHealth;
                playerHealth.OnArmorChanged -= RefreshArmor;
                playerHealth.OnDied -= HandleDied;
            }

            UnsubscribeFromBattleFlow();
        }

        private void RefreshHealth(int unused)
        {
            if (hpBarView != null && playerHealth != null)
                hpBarView.SetHp(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void HandleDied()
        {
        }

        private void RefreshArmor(int armor)
        {
            if (armorView != null)
                armorView.SetArmor(armor);
        }

        private void RefreshBlood()
        {
            if (bloodView != null && battleState != null)
                bloodView.SetBlood(battleState.BloodResource);
        }

        private void RefreshCoins()
        {
            if (coinsView != null && battleState != null)
                coinsView.SetCoins(battleState.Coins);
        }

        private void OnBattleStateChanged(BattleState state)
        {
            battleState = state;
            RefreshBlood();
            RefreshCoins();
        }
    }
}

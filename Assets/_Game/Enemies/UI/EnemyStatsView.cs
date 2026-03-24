using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Presenter для UI врага. Подписывается на enemy.Health,
    /// вызывает Set-методы Passive View при изменении данных.
    /// </summary>
    public class EnemyStatsView : MonoBehaviour
    {
        [SerializeField] private EnemyHpBarView hpBarView;
        [SerializeField] private EnemyIntentView intentView;

        private EnemyDefinition enemyDefinition;

        private void EnsureViewReferences()
        {
            if (hpBarView == null) hpBarView = GetComponentInChildren<EnemyHpBarView>();
            if (intentView == null) intentView = GetComponentInChildren<EnemyIntentView>();
        }

        private void Start()
        {
            EnsureViewReferences();
            enemyDefinition = GetComponentInParent<EnemyDefinition>();
            if (enemyDefinition == null)
                return;

            var health = enemyDefinition.Health;
            if (health != null)
            {
                health.OnDamaged += RefreshHealth;
                health.OnHealed += RefreshHealth;
                health.OnDied += HandleDied;
                RefreshHealth(0);
            }

            if (intentView != null)
                intentView.SetIntent("?");
        }

        private void HandleDied()
        {
            
        }

        private void OnDestroy()
        {
            if (enemyDefinition?.Health != null)
            {
                enemyDefinition.Health.OnDamaged -= RefreshHealth;
                enemyDefinition.Health.OnHealed -= RefreshHealth;
                enemyDefinition.Health.OnDied -= HandleDied;
            }
        }

        private void RefreshHealth(int unused)
        {
            if (hpBarView != null && enemyDefinition?.Health != null)
                hpBarView.SetHp(enemyDefinition.Health.CurrentHealth, enemyDefinition.Health.MaxHealth);
        }
    }
}

using System;

namespace Match3
{
    public interface IDamageable
    {
        int Hit(int damage);
    }
    
    public class Health : IDamageable
    {
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int Armor { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        public event Action<int> OnDamaged;
        public event Action<int> OnHealed;
        public event Action<int> OnArmorChanged;
        public event Action OnDied;

        public Health(int maxHealth, int? currentHealthOverride = null)
        {
            MaxHealth = maxHealth;
            CurrentHealth = currentHealthOverride.HasValue
                ? Math.Max(0, Math.Min(maxHealth, currentHealthOverride.Value))
                : maxHealth;
            Armor = 0;
        }

        public int Hit(int damage)
        {
            if (damage <= 0) return 0;

            int armorAbsorbed = Math.Min(Armor, damage);
            Armor -= armorAbsorbed;
            int damageAfterArmor = damage - armorAbsorbed;
            int overflow = Math.Max(0, damageAfterArmor - CurrentHealth);
            CurrentHealth = Math.Max(0, CurrentHealth - damageAfterArmor);

            if (armorAbsorbed > 0)
                OnArmorChanged?.Invoke(Armor);

            if (damageAfterArmor > 0)
                OnDamaged?.Invoke(damageAfterArmor);

            if (!IsAlive)
                OnDied?.Invoke();

            return overflow;
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;

            int actualHeal = Math.Min(amount, MaxHealth - CurrentHealth);
            CurrentHealth += actualHeal;

            if (actualHeal > 0)
                OnHealed?.Invoke(actualHeal);
        }

        public void AddArmor(int amount)
        {
            if (amount <= 0) return;

            Armor += amount;
            OnArmorChanged?.Invoke(Armor);
        }
    }
}

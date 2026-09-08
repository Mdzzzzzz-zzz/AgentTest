using System;
using UnityEngine;

namespace RuneDice.Game
{
    public sealed class EnemyTarget : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 30;
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;
        public event Action<int> HealthChanged;
        public event Action Died;

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth);
            if (IsDead) Died?.Invoke();
        }
    }
}

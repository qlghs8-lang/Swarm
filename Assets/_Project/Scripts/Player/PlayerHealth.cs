using System;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float invincibleDuration = 0.5f;

        private int _currentHealth;
        private float _invincibleTimer;
        private PlayerStats _stats;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (_invincibleTimer > 0f)
            {
                _invincibleTimer -= Time.deltaTime;
            }
        }

        public void SetInvincible(float duration)
        {
            _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
        }

        public void Heal(int amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        public void IncreaseMaxHealth(int amount)
        {
            maxHealth += amount;
            _currentHealth += amount;
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        public void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower,
                               float penetration = 0f, bool isCritical = false)
        {
            if (IsDead || _invincibleTimer > 0f) return;

            var defenseMultiplier = 1f - Mathf.Clamp(_stats != null ? _stats.DefenseBonus : 0f, -0.5f, 0.9f);
            var reducedAmount = Mathf.RoundToInt(amount * defenseMultiplier);
            _currentHealth = Mathf.Max(_currentHealth - reducedAmount, 0);
            _invincibleTimer = invincibleDuration;
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            IsDead = true;

            if (TryGetComponent<PlayerController>(out var controller))
            {
                controller.enabled = false;
            }

            foreach (var weapon in GetComponents<ILevelableWeapon>())
            {
                if (weapon is MonoBehaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }

            OnDied?.Invoke();
        }
    }
}

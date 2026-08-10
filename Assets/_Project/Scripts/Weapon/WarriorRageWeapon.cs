using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorRageWeapon : LevelableWeapon
    {
        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float lifestealPercent = 0.1f;
        [SerializeField] private float attackPowerPerKill = 0.1f;
        [SerializeField] private int maxHealthPerKill = 2;

        private float _timer;
        private PlayerStats _stats;
        private PlayerHealth _health;

        public int StackCount { get; private set; }

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (data == null) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            if (Attack())
            {
                _timer = 0f;
            }
        }

        private bool Attack()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var target = EnemyTargeting.FindNearest(origin, radius);
            if (target == null) return false;

            if (!target.TryGetComponent<IDamageable>(out var damageable)) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            target.TryGetComponent<EnemyHealth>(out var enemyHealth);
            var killed = false;
            void HandleDied() => killed = true;
            if (enemyHealth != null) enemyHealth.OnDied += HandleDied;

            damageable.TakeDamage(damage, DamageStatType.AttackPower, penetration);

            if (enemyHealth != null) enemyHealth.OnDied -= HandleDied;

            if (_health != null)
            {
                _health.Heal(Mathf.RoundToInt(damage * lifestealPercent));
            }

            if (killed)
            {
                StackCount++;
                if (_stats != null) _stats.IncreaseAttackPower(attackPowerPerKill);
                if (_health != null) _health.IncreaseMaxHealth(maxHealthPerKill);
            }

            return true;
        }
    }
}

using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorLifestealWeapon : LevelableWeapon
    {
        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float lifestealPercent = 0.1f;

        private float _timer;
        private PlayerStats _stats;
        private PlayerHealth _health;

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

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            if (target.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage, DamageStatType.AttackPower, penetration);

                if (_health != null)
                {
                    _health.Heal(Mathf.RoundToInt(damage * lifestealPercent));
                }
            }

            return true;
        }
    }
}

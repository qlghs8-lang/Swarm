using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ArcherArrowWeapon : LevelableWeapon
    {
        [SerializeField] private ProjectileWeaponData data;

        private float _timer;
        private ObjectPool _pool;
        private PlayerStats _stats;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        private void Start()
        {
            if (data != null && data.ProjectilePrefab != null)
            {
                _pool = new ObjectPool(data.ProjectilePrefab);
            }
        }

        private void Update()
        {
            if (_pool == null) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            _timer = 0f;
            TryFire();
        }

        private void TryFire()
        {
            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var targets = EnemyTargeting.FindMultiple(transform.position, data.Range, count);
            if (targets.Count == 0) return;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            for (var i = 0; i < targets.Count; i++)
            {
                var direction = ((Vector2)targets[i].position - (Vector2)transform.position).normalized;

                var instance = _pool.Get(transform.position, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, 0, DamageStatType.AttackPower, penetration);
                }
            }
        }
    }
}

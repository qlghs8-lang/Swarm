using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageFireballWeapon : LevelableWeapon
    {
        private const int PierceCount = 99;

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

            if (TryFire())
            {
                _timer = 0f;
            }
        }

        private bool TryFire()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var targets = EnemyTargeting.FindMultiple(origin, data.Range, count);
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            for (var i = 0; i < count; i++)
            {
                var target = targets[i % targets.Count];
                var direction = ((Vector2)target.position - origin).normalized;

                var instance = _pool.Get(origin, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, PierceCount, damageType, penetration);
                }
            }

            return true;
        }
    }
}

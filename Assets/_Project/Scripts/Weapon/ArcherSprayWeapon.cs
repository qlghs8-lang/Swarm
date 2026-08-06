using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ArcherSprayWeapon : LevelableWeapon
    {
        private const int PierceCount = 99;

        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private float rotationSpeed = 90f;

        private float _timer;
        private float _currentAngleDegrees;
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

            _currentAngleDegrees += rotationSpeed * Time.deltaTime;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.05f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            _timer = 0f;
            TryFire();
        }

        private void TryFire()
        {
            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            var radians = _currentAngleDegrees * Mathf.Deg2Rad;
            var baseDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            for (var i = 0; i < count; i++)
            {
                var direction = ProjectileSpread.GetDirection(baseDirection, i, count, data.SpreadAngleDegrees);

                var instance = _pool.Get(transform.position, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, PierceCount, DamageStatType.AttackPower, penetration);
                }
            }
        }
    }
}

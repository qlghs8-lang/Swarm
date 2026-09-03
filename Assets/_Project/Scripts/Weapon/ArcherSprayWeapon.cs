using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ArcherSprayWeapon : LevelableWeapon
    {
        private const int PierceCount = 99;

        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private float rotationSpeed = 90f;

        // Spray pierces everything and fires constantly, so every enemy in the fan takes a hit
        // several times a second. At full knockback that reads as a wind tunnel; this scales the
        // per-hit push down so the weapon shreds crowds without shoving them off screen.
        [SerializeField, Range(0f, 1f)] private float knockbackScale = 0.3f;

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
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            var radians = _currentAngleDegrees * Mathf.Deg2Rad;
            var baseDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            for (var i = 0; i < count; i++)
            {
                var direction = ProjectileSpread.GetDirection(baseDirection, i, count, data.SpreadAngleDegrees);

                var instance = _pool.Get(origin, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, PierceCount, DamageStatType.AttackPower, penetration, isCritical: isCritical, knockbackScale: knockbackScale);
                }
            }
        }
    }
}

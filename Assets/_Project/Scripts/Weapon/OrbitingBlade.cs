using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class OrbitingBlade : MonoBehaviour
    {
        private Transform _pivot;
        private float _orbitRadius;
        private float _angleDegrees;
        private float _hitRadius;
        private float _hitCooldown;
        private int _damage;
        private float _penetration;

        private readonly Dictionary<Collider2D, float> _lastHitTime = new();

        public void Configure(Transform pivot, float orbitRadius, float angleDegrees, float hitRadius)
        {
            _pivot = pivot;
            _orbitRadius = orbitRadius;
            _angleDegrees = angleDegrees;
            _hitRadius = hitRadius;
        }

        public void SetCombatStats(int damage, float hitCooldown, float penetration = 0f)
        {
            _damage = damage;
            _hitCooldown = hitCooldown;
            _penetration = penetration;
        }

        private void Update()
        {
            if (_pivot == null) return;

            var radians = _angleDegrees * Mathf.Deg2Rad;
            transform.position = (Vector2)_pivot.position + _orbitRadius * new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var hits = Physics2D.OverlapCircleAll(transform.position, _hitRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var lastHit = _lastHitTime.TryGetValue(hit, out var time) ? time : -Mathf.Infinity;
                if (Time.time - lastHit < _hitCooldown) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_damage, DamageStatType.AttackPower, _penetration);
                    _lastHitTime[hit] = Time.time;
                }
            }
        }
    }
}

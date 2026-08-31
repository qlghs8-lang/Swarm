using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class OrbitingBlade : MonoBehaviour
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private Transform _pivot;
        private float _orbitRadius;
        private float _angleDegrees;
        private float _hitRadius;
        private float _hitCooldown;
        private int _damage;
        private float _penetration;

        private readonly Dictionary<Collider2D, float> _lastHitTime = new();

        // The hit-cooldown table is keyed by Collider2D and enemies are recycled through an
        // object pool, so without pruning it grew for the entire run: thousands of entries
        // pinning destroyed objects and slowing every lookup. An entry older than the cooldown
        // can never suppress a hit again, so dropping it is behaviour-neutral.
        private const float HitHistoryPruneInterval = 2f;
        private float _nextHitHistoryPrune;
        private readonly List<Collider2D> _staleHitKeys = new();

        private void PruneHitHistory()
        {
            if (Time.time < _nextHitHistoryPrune) return;
            _nextHitHistoryPrune = Time.time + HitHistoryPruneInterval;

            _staleHitKeys.Clear();
            foreach (var pair in _lastHitTime)
            {
                if (pair.Key == null || Time.time - pair.Value >= _hitCooldown)
                {
                    _staleHitKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < _staleHitKeys.Count; i++)
            {
                _lastHitTime.Remove(_staleHitKeys[i]);
            }
        }

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

            PruneHitHistory();

            var radians = _angleDegrees * Mathf.Deg2Rad;
            transform.position = (Vector2)_pivot.position + _orbitRadius * new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            EnemyTargeting.OverlapEnemies(transform.position, _hitRadius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var lastHit = _lastHitTime.TryGetValue(hit, out var time) ? time : -Mathf.Infinity;
                if (Time.time - lastHit < _hitCooldown) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_damage, DamageStatType.AttackPower, _penetration);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                    _lastHitTime[hit] = Time.time;
                }
            }
        }
    }
}

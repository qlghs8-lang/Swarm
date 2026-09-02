using System.Collections.Generic;
using Swarm.Enemy;
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
        private bool _isCritical;

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

        public void SetCombatStats(int damage, float hitCooldown, float penetration = 0f, bool isCritical = false)
        {
            _damage = damage;
            _hitCooldown = hitCooldown;
            _penetration = penetration;
            _isCritical = isCritical;
        }

        private void Update()
        {
            if (_pivot == null) return;

            PruneHitHistory();

            var radians = _angleDegrees * Mathf.Deg2Rad;
            transform.position = (Vector2)_pivot.position + _orbitRadius * new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            // The way the blade is sweeping: perpendicular to the arm, towards increasing angle.
            var tangent = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));

            EnemyTargeting.OverlapEnemies(transform.position, _hitRadius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var lastHit = _lastHitTime.TryGetValue(hit, out var time) ? time : -Mathf.Infinity;
                if (Time.time - lastHit < _hitCooldown) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_damage, DamageStatType.AttackPower, _penetration, _isCritical);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                    _lastHitTime[hit] = Time.time;

                    // EnemyHealth knocks anything it damages straight away from the player, which
                    // for an orbiting blade is straight out of the ring. The knockback (0.66 units)
                    // is most of the blade's hit band (1.0 wide), so one hit resets an approach --
                    // and past four blades the ring re-hits before an enemy can cross, walling the
                    // player in while they stand still. Sweeping them along the ring instead leaves
                    // their approach intact, and reads as being swatted aside by the blade rather
                    // than repelled by a force field. ApplyKnockback assigns linearVelocity
                    // outright, so this replaces the radial knockback applied a moment ago; the
                    // active check skips an enemy the hit killed and returned to the pool.
                    if (hit.gameObject.activeInHierarchy && hit.TryGetComponent<EnemyChaser>(out var chaser))
                    {
                        chaser.ApplyKnockback(tangent);
                    }
                }
            }
        }
    }
}

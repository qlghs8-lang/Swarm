using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private float _maxRange;
        private int _damage;
        private ObjectPool _pool;
        private Vector2 _startPosition;
        private int _pierceRemaining;
        private DamageStatType _damageType;
        private float _penetration;
        private System.Action<Vector2> _onHit;

        // A pooled projectile is activated before Launch() runs, and some callers activate one
        // without ever launching it (the fireball shader warm-up pulls an instance from the pool
        // for a single frame). Until Launch() fills in the pool and range, Update() must not run:
        // with _maxRange still 0 the travel check passes immediately and releases through a null
        // pool reference.
        private bool _isLaunched;
        private readonly HashSet<Collider2D> _hitColliders = new();

        public void Launch(Vector2 direction, float speed, float maxRange, int damage, ObjectPool pool, int pierceCount = 0, DamageStatType damageType = DamageStatType.AttackPower, float penetration = 0f, System.Action<Vector2> onHit = null)
        {
            _direction = direction;
            _speed = speed;
            _maxRange = maxRange;
            _damage = damage;
            _pool = pool;
            _startPosition = transform.position;
            _pierceRemaining = pierceCount;
            _damageType = damageType;
            _penetration = penetration;
            _onHit = onHit;
            _hitColliders.Clear();
            _isLaunched = true;

            // Sprite artwork faces right (+X), matching this formula's zero-angle direction.
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnEnable()
        {
            _isLaunched = false;
        }

        private void Update()
        {
            if (!_isLaunched) return;

            transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));

            if (((Vector2)transform.position - _startPosition).sqrMagnitude >= _maxRange * _maxRange)
            {
                _pool.Release(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isLaunched) return;
            if (!other.CompareTag("Enemy")) return;
            if (!_hitColliders.Add(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(_damage, _damageType, _penetration);
                PlayerDamageEvents.RaiseDamageDealt(other.gameObject);
            }

            _onHit?.Invoke(transform.position);

            if (_pierceRemaining > 0)
            {
                _pierceRemaining--;
                return;
            }

            _pool.Release(gameObject);
        }
    }
}

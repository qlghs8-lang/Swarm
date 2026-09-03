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
        private bool _isCritical;
        private float _knockbackScale;

        // A pooled projectile is activated before Launch() runs, and some callers activate one
        // without ever launching it (the fireball shader warm-up pulls an instance from the pool
        // for a single frame). Until Launch() fills in the pool and range, Update() must not run:
        // with _maxRange still 0 the travel check passes immediately and releases through a null
        // pool reference.
        private bool _isLaunched;
        private readonly HashSet<Collider2D> _hitColliders = new();

        // 0 at the muzzle, 1 at the edge of the weapon's range. FireballAnimator reads this to fade
        // the flame out by distance instead of by a timer, so the burnout lands with the despawn
        // no matter how speed or range are tuned.
        public float TravelProgress
        {
            get
            {
                if (!_isLaunched || _maxRange <= 0f) return 0f;
                return Mathf.Clamp01(((Vector2)transform.position - _startPosition).magnitude / _maxRange);
            }
        }

        public void Launch(Vector2 direction, float speed, float maxRange, int damage, ObjectPool pool, int pierceCount = 0, DamageStatType damageType = DamageStatType.AttackPower, float penetration = 0f, System.Action<Vector2> onHit = null, bool isCritical = false, float knockbackScale = 0f)
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
            _isCritical = isCritical;
            _knockbackScale = knockbackScale;
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
                damageable.TakeDamage(_damage, _damageType, _penetration, _isCritical, _knockbackScale);
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

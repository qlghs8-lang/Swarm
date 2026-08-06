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
        private readonly HashSet<Collider2D> _hitColliders = new();

        public void Launch(Vector2 direction, float speed, float maxRange, int damage, ObjectPool pool, int pierceCount = 0, DamageStatType damageType = DamageStatType.AttackPower, float penetration = 0f)
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
            _hitColliders.Clear();
        }

        private void Update()
        {
            transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));

            if (((Vector2)transform.position - _startPosition).sqrMagnitude >= _maxRange * _maxRange)
            {
                _pool.Release(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            if (!_hitColliders.Add(other)) return;

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(_damage, _damageType, _penetration);
            }

            if (_pierceRemaining > 0)
            {
                _pierceRemaining--;
                return;
            }

            _pool.Release(gameObject);
        }
    }
}

using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ChainLightningBolt : MonoBehaviour
    {
        private const int CandidateCount = 8;
        private const float HitDistance = 0.2f;

        private Transform _target;
        private int _damage;
        private float _speed;
        private int _remainingJumps;
        private float _chainRange;
        private float _freezeChance;
        private float _freezeDuration;
        private float _penetration;
        private DamageStatType _damageType;

        private readonly HashSet<Transform> _hitTargets = new();

        public void Launch(Transform target, int damage, float speed, int maxJumps, float chainRange,
            float freezeChance, float freezeDuration, float penetration, DamageStatType damageType)
        {
            _target = target;
            _damage = damage;
            _speed = speed;
            _remainingJumps = maxJumps;
            _chainRange = chainRange;
            _freezeChance = freezeChance;
            _freezeDuration = freezeDuration;
            _penetration = penetration;
            _damageType = damageType;
        }

        private void Update()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            var toTarget = (Vector2)_target.position - (Vector2)transform.position;
            if (toTarget.magnitude <= HitDistance)
            {
                HitCurrentTarget();
                return;
            }

            transform.position += (Vector3)(toTarget.normalized * (_speed * Time.deltaTime));
        }

        private void HitCurrentTarget()
        {
            _hitTargets.Add(_target);

            if (_target.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(_damage, _damageType, _penetration);
            }

            if (Random.value < _freezeChance && _target.TryGetComponent<EnemyChaser>(out var chaser))
            {
                chaser.ApplyFreeze(_freezeDuration);
            }

            if (_remainingJumps <= 0)
            {
                Destroy(gameObject);
                return;
            }

            var next = FindNextTarget();
            if (next == null)
            {
                Destroy(gameObject);
                return;
            }

            _remainingJumps--;
            _target = next;
        }

        private Transform FindNextTarget()
        {
            var candidates = EnemyTargeting.FindMultiple(transform.position, _chainRange, CandidateCount);
            foreach (var candidate in candidates)
            {
                if (_hitTargets.Contains(candidate)) continue;
                return candidate;
            }

            return null;
        }
    }
}

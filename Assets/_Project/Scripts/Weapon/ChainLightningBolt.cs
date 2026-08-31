using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ChainLightningBolt : MonoBehaviour
    {
        // Reused across calls so target selection allocates nothing per shot.
        private readonly List<Transform> _targetBuffer = new();

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
        private System.Action<Vector2> _onHit;

        private SpriteRenderer _spriteRenderer;
        private Sprite[] _travelFrames;
        private float _travelFrameDuration;
        private float _animTimer;

        private readonly HashSet<Transform> _hitTargets = new();

        public void Launch(Transform target, int damage, float speed, int maxJumps, float chainRange,
            float freezeChance, float freezeDuration, float penetration, DamageStatType damageType,
            System.Action<Vector2> onHit = null)
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
            _onHit = onHit;
        }

        public void SetTravelAnimation(SpriteRenderer spriteRenderer, Sprite[] travelFrames, float travelFrameDuration)
        {
            _spriteRenderer = spriteRenderer;
            _travelFrames = travelFrames;
            _travelFrameDuration = travelFrameDuration;
        }

        private void Update()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            UpdateTravelAnimation();

            var toTarget = EnemyTargeting.GetHitPoint(_target) - (Vector2)transform.position;
            if (toTarget.magnitude <= HitDistance)
            {
                HitCurrentTarget();
                return;
            }

            // Sprite artwork faces right (+X).
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg);
            transform.position += (Vector3)(toTarget.normalized * (_speed * Time.deltaTime));
        }

        private void UpdateTravelAnimation()
        {
            if (_travelFrames == null || _travelFrames.Length == 0 || _spriteRenderer == null) return;

            _animTimer += Time.deltaTime;
            var frameIndex = Mathf.FloorToInt(_animTimer / _travelFrameDuration) % _travelFrames.Length;
            _spriteRenderer.sprite = _travelFrames[frameIndex];
        }

        private void HitCurrentTarget()
        {
            _hitTargets.Add(_target);
            _onHit?.Invoke(EnemyTargeting.GetHitPoint(_target));

            if (_target.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(_damage, _damageType, _penetration);
                PlayerDamageEvents.RaiseDamageDealt(_target.gameObject);
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
            EnemyTargeting.FindMultiple(transform.position, _chainRange, CandidateCount, _targetBuffer);
            var candidates = _targetBuffer;
            foreach (var candidate in candidates)
            {
                if (_hitTargets.Contains(candidate)) continue;
                return candidate;
            }

            return null;
        }
    }
}

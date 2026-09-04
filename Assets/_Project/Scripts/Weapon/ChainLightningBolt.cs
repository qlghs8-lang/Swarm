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
        private float _slowMultiplier = 1f;
        private float _slowDuration;
        private float _penetration;
        private bool _isCritical;
        private DamageStatType _damageType;
        private System.Action<Vector2> _onHit;

        private RuntimeObjectPool _pool;

        private SpriteRenderer _spriteRenderer;
        private Sprite[] _travelFrames;
        private float _travelFrameDuration;
        private float _animTimer;

        private readonly HashSet<Transform> _hitTargets = new();

        public void Launch(Transform target, int damage, float speed, int maxJumps, float chainRange,
            float slowMultiplier, float slowDuration, float penetration, DamageStatType damageType,
            System.Action<Vector2> onHit = null, bool isCritical = false)
        {
            _target = target;
            _damage = damage;
            _speed = speed;
            _remainingJumps = maxJumps;
            _chainRange = chainRange;
            _slowMultiplier = slowMultiplier;
            _slowDuration = slowDuration;
            _penetration = penetration;
            _damageType = damageType;
            _onHit = onHit;
            _isCritical = isCritical;

            // Pooled: the previous flight's jump history and animation clock are still here, and
            // _hitTargets holds Transforms of enemies that have themselves been recycled, so a
            // stale entry would silently make the bolt refuse to jump to a live target.
            _hitTargets.Clear();
            _animTimer = 0f;
        }

        /// <summary>Set by the spawning weapon so the bolt is recycled instead of destroyed.</summary>
        public void SetPool(RuntimeObjectPool pool)
        {
            _pool = pool;
        }

        private void Retire()
        {
            if (_pool != null) _pool.Release(gameObject);
            else Destroy(gameObject);
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
                Retire();
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
                damageable.TakeDamage(_damage, _damageType, _penetration, _isCritical);
                PlayerDamageEvents.RaiseDamageDealt(_target.gameObject);
            }

            // Every link slows. The chain is the weapon's identity, so the control it leaves
            // behind is a trail of dragging enemies rather than a lucky lockdown on one of them.
            if (_target.TryGetComponent<EnemyChaser>(out var chaser))
            {
                chaser.ApplySlow(_slowMultiplier, _slowDuration);
            }

            if (_remainingJumps <= 0)
            {
                Retire();
                return;
            }

            var next = FindNextTarget();
            if (next == null)
            {
                Retire();
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

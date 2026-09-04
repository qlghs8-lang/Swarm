using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class Meteor : MonoBehaviour
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private const float ImpactDistance = 0.15f;

        private Vector2 _targetPosition;
        private float _speed;
        private float _impactRadius;
        private int _impactDamage;
        private DamageStatType _damageType;
        private bool _isCritical;
        private float _penetration;
        private System.Action<Vector2> _onImpact;

        private RuntimeObjectPool _pool;

        private SpriteRenderer _spriteRenderer;
        private Sprite[] _travelFrames;
        private float _travelFrameDuration;
        private float _animTimer;

        public void Launch(Vector2 targetPosition, float fallOffset, float speed, float impactRadius,
            int impactDamage, DamageStatType damageType, float penetration, System.Action<Vector2> onImpact,
            bool isCritical = false)
        {
            _targetPosition = targetPosition;
            _speed = speed;
            _impactRadius = impactRadius;
            _impactDamage = impactDamage;
            _damageType = damageType;
            _penetration = penetration;
            _onImpact = onImpact;
            _isCritical = isCritical;
            // Pooled: the previous fall left the animation clock at the end of the strip, which
            // would show the reused meteor's last frame for its whole descent.
            _animTimer = 0f;

            var offsetDirection = new Vector2(-1f, 1f).normalized;
            transform.position = targetPosition + offsetDirection * fallOffset;
        }

        /// <summary>Set by the spawning weapon so the meteor is recycled instead of destroyed.</summary>
        public void SetPool(RuntimeObjectPool pool)
        {
            _pool = pool;
        }

        public void SetTravelAnimation(SpriteRenderer spriteRenderer, Sprite[] travelFrames, float travelFrameDuration)
        {
            _spriteRenderer = spriteRenderer;
            _travelFrames = travelFrames;
            _travelFrameDuration = travelFrameDuration;
        }

        private void Update()
        {
            UpdateTravelAnimation();

            var toTarget = _targetPosition - (Vector2)transform.position;
            if (toTarget.magnitude <= ImpactDistance)
            {
                Impact();
                return;
            }

            transform.position += (Vector3)(toTarget.normalized * (_speed * Time.deltaTime));
        }

        private void UpdateTravelAnimation()
        {
            if (_travelFrames == null || _travelFrames.Length == 0 || _spriteRenderer == null) return;

            _animTimer += Time.deltaTime;
            var frameIndex = Mathf.Min(_travelFrames.Length - 1, Mathf.FloorToInt(_animTimer / _travelFrameDuration));
            _spriteRenderer.sprite = _travelFrames[frameIndex];
        }

        private void Impact()
        {
            EnemyTargeting.OverlapEnemies(_targetPosition, _impactRadius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_impactDamage, _damageType, _penetration, _isCritical);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                }
            }

            _onImpact?.Invoke(_targetPosition);

            if (_pool != null) _pool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}

using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class Meteor : MonoBehaviour
    {
        private const float ImpactDistance = 0.15f;

        private Vector2 _targetPosition;
        private float _speed;
        private float _impactRadius;
        private int _impactDamage;
        private DamageStatType _damageType;
        private float _penetration;
        private System.Action<Vector2> _onImpact;

        public void Launch(Vector2 targetPosition, float fallOffset, float speed, float impactRadius,
            int impactDamage, DamageStatType damageType, float penetration, System.Action<Vector2> onImpact)
        {
            _targetPosition = targetPosition;
            _speed = speed;
            _impactRadius = impactRadius;
            _impactDamage = impactDamage;
            _damageType = damageType;
            _penetration = penetration;
            _onImpact = onImpact;

            var offsetDirection = new Vector2(-1f, 1f).normalized;
            transform.position = targetPosition + offsetDirection * fallOffset;
        }

        private void Update()
        {
            var toTarget = _targetPosition - (Vector2)transform.position;
            if (toTarget.magnitude <= ImpactDistance)
            {
                Impact();
                return;
            }

            transform.position += (Vector3)(toTarget.normalized * (_speed * Time.deltaTime));
        }

        private void Impact()
        {
            var hits = Physics2D.OverlapCircleAll(_targetPosition, _impactRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_impactDamage, _damageType, _penetration);
                }
            }

            _onImpact?.Invoke(_targetPosition);
            Destroy(gameObject);
        }
    }
}

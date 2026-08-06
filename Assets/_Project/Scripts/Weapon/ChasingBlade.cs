using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ChasingBlade : MonoBehaviour
    {
        private const int MaxCandidates = 8;

        private enum State
        {
            Orbiting,
            Chasing,
            Returning
        }

        private Transform _pivot;
        private float _orbitRadius;
        private float _orbitAngleDegrees;
        private float _hitRadius;
        private float _chaseRange;
        private float _chaseSpeed;
        private float _swoopDistance;
        private int _damage;
        private float _hitCooldown;
        private float _penetration;
        private List<ChasingBlade> _siblings;

        private State _state = State.Orbiting;
        private Transform _chaseTarget;
        private Vector2 _approachPoint;
        private bool _hasApproachPoint;
        private float _attackTimer;

        private readonly Dictionary<Collider2D, float> _lastHitTime = new();

        public Transform CurrentTarget => _state == State.Chasing ? _chaseTarget : null;

        public void Configure(Transform pivot, float orbitRadius, float orbitAngleDegrees, float hitRadius,
            float chaseRange, float chaseSpeed, float swoopDistance, List<ChasingBlade> siblings)
        {
            _pivot = pivot;
            _orbitRadius = orbitRadius;
            _orbitAngleDegrees = orbitAngleDegrees;
            _hitRadius = hitRadius;
            _chaseRange = chaseRange;
            _chaseSpeed = chaseSpeed;
            _swoopDistance = swoopDistance;
            _siblings = siblings;
        }

        public void SetCombatStats(int damage, float hitCooldown, float penetration = 0f)
        {
            _damage = damage;
            _hitCooldown = hitCooldown;
            _penetration = penetration;
        }

        private Vector2 OrbitPosition
        {
            get
            {
                var radians = _orbitAngleDegrees * Mathf.Deg2Rad;
                return (Vector2)_pivot.position + _orbitRadius * new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        private void Update()
        {
            if (_pivot == null) return;

            if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

            if (_state == State.Chasing && IsOffScreen(transform.position))
            {
                transform.position = OrbitPosition;
                _state = State.Orbiting;
            }

            switch (_state)
            {
                case State.Orbiting:
                    UpdateOrbiting();
                    break;
                case State.Chasing:
                    UpdateChasing();
                    break;
                case State.Returning:
                    UpdateReturning();
                    break;
            }
        }

        private static bool IsOffScreen(Vector2 position)
        {
            var camera = Camera.main;
            if (camera == null) return false;

            var viewportPoint = camera.WorldToViewportPoint(position);
            return viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f;
        }

        private void UpdateOrbiting()
        {
            transform.position = OrbitPosition;
            DealProximityDamage();

            var target = FindTarget(null);
            if (target != null)
            {
                _chaseTarget = target;
                _hasApproachPoint = false;
                _state = State.Chasing;
            }
        }

        private void UpdateChasing()
        {
            if (_chaseTarget == null || !_chaseTarget.gameObject.activeInHierarchy)
            {
                _chaseTarget = FindTarget(null);
                _hasApproachPoint = false;
                if (_chaseTarget == null)
                {
                    _state = State.Returning;
                    return;
                }
            }

            if (!_hasApproachPoint)
            {
                var offset = (Vector2)_chaseTarget.position - (Vector2)transform.position;
                if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right;
                _approachPoint = (Vector2)_chaseTarget.position + offset.normalized * _swoopDistance;
                _hasApproachPoint = true;
            }

            var toTargetActual = (Vector2)_chaseTarget.position - (Vector2)transform.position;
            if (toTargetActual.magnitude <= _hitRadius && _attackTimer <= 0f)
            {
                if (_chaseTarget.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_damage, DamageStatType.AttackPower, _penetration);
                }

                _attackTimer = _hitCooldown;
            }

            var toPoint = _approachPoint - (Vector2)transform.position;
            if (toPoint.magnitude <= 0.15f)
            {
                var previous = _chaseTarget;
                _chaseTarget = FindTarget(previous);
                _hasApproachPoint = false;

                if (_chaseTarget == null)
                {
                    _state = State.Returning;
                }

                return;
            }

            transform.position += (Vector3)(toPoint.normalized * (_chaseSpeed * Time.deltaTime));
        }

        private void UpdateReturning()
        {
            var target = OrbitPosition;
            var toTarget = target - (Vector2)transform.position;

            if (toTarget.magnitude <= 0.1f)
            {
                _state = State.Orbiting;
                return;
            }

            transform.position += (Vector3)(toTarget.normalized * (_chaseSpeed * Time.deltaTime));

            var newTarget = FindTarget(null);
            if (newTarget != null)
            {
                _chaseTarget = newTarget;
                _hasApproachPoint = false;
                _state = State.Chasing;
            }
        }

        private Transform FindTarget(Transform previous)
        {
            var candidates = EnemyTargeting.FindMultiple(transform.position, _chaseRange, MaxCandidates);

            foreach (var candidate in candidates)
            {
                if (candidate == previous) continue;
                if (IsClaimedBySibling(candidate)) continue;
                return candidate;
            }

            foreach (var candidate in candidates)
            {
                if (candidate != previous) return candidate;
            }

            return candidates.Count > 0 ? candidates[0] : null;
        }

        private bool IsClaimedBySibling(Transform target)
        {
            if (_siblings == null) return false;

            foreach (var sibling in _siblings)
            {
                if (sibling == null || sibling == this) continue;
                if (sibling.CurrentTarget == target) return true;
            }

            return false;
        }

        private void DealProximityDamage()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, _hitRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var lastHit = _lastHitTime.TryGetValue(hit, out var time) ? time : -Mathf.Infinity;
                if (Time.time - lastHit < _hitCooldown) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(_damage, DamageStatType.AttackPower, _penetration);
                    _lastHitTime[hit] = Time.time;
                }
            }
        }
    }
}

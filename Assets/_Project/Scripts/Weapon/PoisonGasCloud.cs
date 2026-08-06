using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class PoisonGasCloud : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private float _tickInterval;
        private int _damagePerTick;
        private float _penetration;
        private float _defensePenalty;
        private float _speedMultiplier;

        private float _elapsed;

        public void Configure(float radius, float duration, float tickInterval, int damagePerTick, float defensePenalty, float speedMultiplier, float penetration = 0f)
        {
            _radius = radius;
            _duration = duration;
            _tickInterval = tickInterval;
            _damagePerTick = damagePerTick;
            _defensePenalty = defensePenalty;
            _speedMultiplier = speedMultiplier;
            _penetration = penetration;

            transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            var hits = Physics2D.OverlapCircleAll(transform.position, _radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.TryGetComponent<EnemyHealth>(out var enemyHealth)) continue;

                enemyHealth.ApplyPoison(_damagePerTick, _tickInterval, _defensePenalty, _speedMultiplier,
                    DamageStatType.AttackPower, _penetration);
            }
        }
    }
}

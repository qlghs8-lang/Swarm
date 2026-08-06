using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class FirePatch : MonoBehaviour
    {
        private float _radius;
        private float _duration;
        private int _burnDamagePerTick;
        private float _burnTickInterval;
        private float _burnDuration;
        private DamageStatType _damageType;
        private float _penetration;

        private float _elapsed;
        private readonly HashSet<EnemyHealth> _ignited = new();

        public void Configure(float radius, float duration, int burnDamagePerTick, float burnTickInterval,
            float burnDuration, DamageStatType damageType, float penetration)
        {
            _radius = radius;
            _duration = duration;
            _burnDamagePerTick = burnDamagePerTick;
            _burnTickInterval = burnTickInterval;
            _burnDuration = burnDuration;
            _damageType = damageType;
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
                if (!_ignited.Add(enemyHealth)) continue;

                enemyHealth.ApplyBurn(_burnDamagePerTick, _burnTickInterval, _burnDuration, _damageType, _penetration);
            }
        }
    }
}

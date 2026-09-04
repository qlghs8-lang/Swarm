using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class FirePatch : MonoBehaviour
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private const float IntroFrameDuration = 0.15f;
        private const float PeakFrameDuration = 0.12f;
        private const int IntroFrameCount = 1;
        private const int OutroFrameCount = 1;
        private const float VisualScaleMultiplier = 3f;

        private float _radius;
        private float _duration;
        private int _burnDamagePerTick;
        private float _burnTickInterval;
        private float _burnDuration;
        private DamageStatType _damageType;
        private float _penetration;
        private Sprite[] _frames;

        private float _elapsed;
        private SpriteRenderer _spriteRenderer;
        private RuntimeObjectPool _pool;
        private readonly HashSet<EnemyHealth> _ignited = new();

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(float radius, float duration, int burnDamagePerTick, float burnTickInterval,
            float burnDuration, DamageStatType damageType, float penetration, Sprite[] frames = null)
        {
            _radius = radius;
            _duration = duration;
            _burnDamagePerTick = burnDamagePerTick;
            _burnTickInterval = burnTickInterval;
            _burnDuration = burnDuration;
            _damageType = damageType;
            _penetration = penetration;
            _frames = frames;

            // Pooled: without this the reused patch would start at the previous life's elapsed
            // time (expiring instantly), and _ignited still holds EnemyHealth references — which
            // are themselves pooled, so a recycled enemy standing in a new patch would never
            // catch fire.
            _elapsed = 0f;
            _ignited.Clear();

            transform.localScale = new Vector3(radius * VisualScaleMultiplier, radius * VisualScaleMultiplier, 1f);

            if (_frames != null && _frames.Length > 0 && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = _frames[0];
            }
        }

        /// <summary>Set by the spawning weapon so the patch is recycled instead of destroyed.</summary>
        public void SetPool(RuntimeObjectPool pool)
        {
            _pool = pool;
        }

        private bool HasFullAnimation => _frames != null && _frames.Length > IntroFrameCount + OutroFrameCount;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                if (_pool != null) _pool.Release(gameObject);
                else Destroy(gameObject);
                return;
            }

            UpdateAnimation();

            EnemyTargeting.OverlapEnemies(transform.position, _radius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.TryGetComponent<EnemyHealth>(out var enemyHealth)) continue;
                if (!_ignited.Add(enemyHealth)) continue;

                enemyHealth.ApplyBurn(_burnDamagePerTick, _burnTickInterval, _burnDuration, _damageType, _penetration);
            }
        }

        // Intro plays once, the middle (peak) frames loop for the whole active duration to give a
        // flickering-fire look, and the last frame plays right before the patch is destroyed -
        // same looping-ground-effect structure as PoisonGasCloud.
        private void UpdateAnimation()
        {
            if (!HasFullAnimation || _spriteRenderer == null) return;

            var outroStartTime = Mathf.Max(0f, _duration - IntroFrameDuration);
            if (_elapsed >= outroStartTime)
            {
                _spriteRenderer.sprite = _frames[^1];
                return;
            }

            var introEnd = IntroFrameDuration * IntroFrameCount;
            if (_elapsed < introEnd)
            {
                var introIndex = Mathf.Clamp(Mathf.FloorToInt(_elapsed / IntroFrameDuration), 0, IntroFrameCount - 1);
                _spriteRenderer.sprite = _frames[introIndex];
                return;
            }

            var peakFrameCount = _frames.Length - IntroFrameCount - OutroFrameCount;
            var peakElapsed = _elapsed - introEnd;
            var peakIndex = Mathf.FloorToInt(peakElapsed / PeakFrameDuration) % peakFrameCount;
            _spriteRenderer.sprite = _frames[IntroFrameCount + peakIndex];
        }
    }
}

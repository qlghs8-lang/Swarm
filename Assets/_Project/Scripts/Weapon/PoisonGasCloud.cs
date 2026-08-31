using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class PoisonGasCloud : MonoBehaviour
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private const float IntroFrameDuration = 0.15f;
        private const float OutroFrameDuration = 0.15f;
        private const int IntroFrameCount = 3;
        private const int OutroFrameCount = 2;
        // Raw multiplier on the AoE radius. poisondash1.aseprite is a 64px canvas at PPU 100 with a
        // centre pivot; the puddle reaches the canvas edge, i.e. 32px = 0.32 world units per side at
        // scale 1. 1 / 0.32 = 3.125 makes the drawn puddle's half-width equal the damage radius.
        private const float VisualScaleMultiplier = 3.125f;

        private float _radius;
        private float _duration;
        private float _tickInterval;
        private int _damagePerTick;
        private float _penetration;
        private float _defensePenalty;
        private float _speedMultiplier;
        private Sprite[] _frames;
        private float _outroStartTime;

        private float _elapsed;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(float radius, float duration, float tickInterval, int damagePerTick, float defensePenalty, float speedMultiplier, float penetration = 0f, Sprite[] frames = null)
        {
            _radius = radius;
            _duration = duration;
            _tickInterval = tickInterval;
            _damagePerTick = damagePerTick;
            _defensePenalty = defensePenalty;
            _speedMultiplier = speedMultiplier;
            _penetration = penetration;
            _frames = frames;

            transform.localScale = new Vector3(radius * VisualScaleMultiplier, radius * VisualScaleMultiplier, 1f);

            var outroDuration = HasFullAnimation ? OutroFrameDuration * OutroFrameCount : 0f;
            _outroStartTime = Mathf.Max(0f, _duration - outroDuration);

            if (_frames != null && _frames.Length > 0 && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = _frames[0];
            }
        }

        private bool HasFullAnimation => _frames != null && _frames.Length >= IntroFrameCount + OutroFrameCount;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
                return;
            }

            UpdateAnimation();

            EnemyTargeting.OverlapEnemies(transform.position, _radius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                if (!hit.TryGetComponent<EnemyHealth>(out var enemyHealth)) continue;

                enemyHealth.ApplyPoison(_damagePerTick, _tickInterval, _defensePenalty, _speedMultiplier,
                    DamageStatType.AttackPower, _penetration);
            }
        }

        // Emerge/Grow/Peak play once, Peak then holds for the whole active duration, and
        // Fade/Disappear play once right before the cloud is destroyed - a looping ground
        // effect instead of the one-shot animations every other weapon effect uses.
        private void UpdateAnimation()
        {
            if (!HasFullAnimation || _spriteRenderer == null) return;

            if (_elapsed >= _outroStartTime)
            {
                var outroElapsed = _elapsed - _outroStartTime;
                var outroIndex = Mathf.Clamp(Mathf.FloorToInt(outroElapsed / OutroFrameDuration), 0, OutroFrameCount - 1);
                _spriteRenderer.sprite = _frames[IntroFrameCount + outroIndex];
                return;
            }

            var introEnd = IntroFrameDuration * IntroFrameCount;
            if (_elapsed < introEnd)
            {
                var introIndex = Mathf.Clamp(Mathf.FloorToInt(_elapsed / IntroFrameDuration), 0, IntroFrameCount - 1);
                _spriteRenderer.sprite = _frames[introIndex];
            }
            else
            {
                _spriteRenderer.sprite = _frames[IntroFrameCount - 1];
            }
        }
    }
}

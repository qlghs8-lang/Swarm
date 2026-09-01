using System.Collections;
using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageFireballWeapon : LevelableWeapon
    {
        // Reused across calls so target selection allocates nothing per shot.
        private readonly List<Transform> _targetBuffer = new();

        private const int PierceCount = 99;

        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private Sprite[] hitEffectFrames;
        [SerializeField] private float hitEffectFrameDuration = 0.05f;
        [SerializeField] private float hitEffectScale = 1f;
        [SerializeField] private Material effectMaterial;

        private float _timer;
        private ObjectPool _pool;
        private PlayerStats _stats;
        private SpriteEffectPlayer _hitEffect;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _hitEffect = SpriteEffectPlayer.Create(
                this, "FireballHitEffect (Temp)", effectMaterial, hitEffectFrames, hitEffectFrameDuration,
                initialScale: hitEffectScale);
        }

        private void OnDisable()
        {
            _hitEffect?.Hide();
        }

        private void Start()
        {
            if (data != null && data.ProjectilePrefab != null)
            {
                _pool = new ObjectPool(data.ProjectilePrefab);
                StartCoroutine(WarmUpFireballShader());
            }
        }

        private IEnumerator WarmUpFireballShader()
        {
            var instance = _pool.Get(transform.position, Quaternion.identity);
            if (instance.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                var originalColor = renderer.color;
                renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

                yield return null;

                renderer.color = originalColor;
            }
            else
            {
                yield return null;
            }

            _pool.Release(instance);
        }

        private void Update()
        {
            if (_pool == null) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            if (TryFire())
            {
                _timer = 0f;
            }
        }

        private bool TryFire()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            EnemyTargeting.FindMultiple(origin, data.Range, count, _targetBuffer);
            var targets = _targetBuffer;
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            for (var i = 0; i < count; i++)
            {
                var target = targets[i % targets.Count];
                var direction = (EnemyTargeting.GetHitPoint(target) - origin).normalized;

                var instance = _pool.Get(origin, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, PierceCount, damageType, penetration, PlayHitEffect, isCritical);
                }
            }

            return true;
        }

        private void PlayHitEffect(Vector2 position)
        {
            _hitEffect?.Play(position, 0f, hitEffectScale);
        }
    }
}

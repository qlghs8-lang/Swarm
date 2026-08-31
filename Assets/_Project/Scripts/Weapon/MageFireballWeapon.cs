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
        private SpriteRenderer _hitEffectRenderer;
        private Coroutine _hitEffectCoroutine;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            CreateHitEffectRenderer();
        }

        private void CreateHitEffectRenderer()
        {
            var effectObject = new GameObject("FireballHitEffect (Temp)");
            effectObject.transform.localScale = Vector3.one * hitEffectScale;
            _hitEffectRenderer = effectObject.AddComponent<SpriteRenderer>();
            _hitEffectRenderer.sortingOrder = 2;
            if (effectMaterial != null) _hitEffectRenderer.material = effectMaterial;
            effectObject.SetActive(false);

            if (effectMaterial != null && hitEffectFrames != null && hitEffectFrames.Length > 0)
            {
                StartCoroutine(WarmUpEffectShader(_hitEffectRenderer, hitEffectFrames[0]));
            }
        }

        // Forces the additive shader variant to compile on scene load (one invisible on-screen
        // frame) instead of during the player's first real hit, where a compile stutter would
        // otherwise show up as a flash of the wrong (uncompiled fallback) color.
        private IEnumerator WarmUpEffectShader(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite;
            renderer.transform.position = transform.position;
            var originalColor = renderer.color;
            renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            renderer.gameObject.SetActive(true);

            yield return null;

            renderer.gameObject.SetActive(false);
            renderer.color = originalColor;
        }

        private void OnDisable()
        {
            if (_hitEffectRenderer != null)
            {
                _hitEffectRenderer.gameObject.SetActive(false);
            }
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

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
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
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, PierceCount, damageType, penetration, PlayHitEffect);
                }
            }

            return true;
        }

        private void PlayHitEffect(Vector2 position)
        {
            if (hitEffectFrames == null || hitEffectFrames.Length == 0) return;

            _hitEffectRenderer.transform.position = position;

            if (_hitEffectCoroutine != null)
            {
                StopCoroutine(_hitEffectCoroutine);
            }

            _hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
        }

        private IEnumerator HitEffectRoutine()
        {
            _hitEffectRenderer.gameObject.SetActive(true);
            foreach (var frameSprite in hitEffectFrames)
            {
                _hitEffectRenderer.sprite = frameSprite;
                yield return new WaitForSeconds(hitEffectFrameDuration);
            }

            _hitEffectRenderer.gameObject.SetActive(false);
            _hitEffectCoroutine = null;
        }
    }
}

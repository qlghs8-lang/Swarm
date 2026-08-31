using System.Collections;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ArcherVolleyWeapon : LevelableWeapon
    {
        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private Sprite[] hitEffectFrames;
        [SerializeField] private float hitEffectFrameDuration = 0.045f;
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
            var effectObject = new GameObject("VolleyHitEffect (Temp)");
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
            }
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
            var target = EnemyTargeting.FindNearest(origin, data.Range);
            if (target == null) return false;

            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var baseDirection = (EnemyTargeting.GetHitPoint(target) - origin).normalized;

            for (var i = 0; i < count; i++)
            {
                var direction = ProjectileSpread.GetDirection(baseDirection, i, count, data.SpreadAngleDegrees);

                var instance = _pool.Get(origin, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, 0, DamageStatType.AttackPower, penetration, PlayHitEffect);
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

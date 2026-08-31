using System.Collections;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorLifestealWeapon : LevelableWeapon
    {
        private const float SmashEffectReferenceRadius = 1.2f;

        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float procChance = 0.1f;
        [SerializeField] private int healAmount = 15;
        [SerializeField] private Sprite[] smashEffectFrames;
        [SerializeField] private float smashFrameDuration = 0.07f;
        [SerializeField] private Material effectMaterial;

        private PlayerStats _stats;
        private PlayerHealth _health;
        private SpriteRenderer _smashEffectRenderer;
        private Coroutine _smashEffectCoroutine;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            CreateSmashEffectRenderer();
        }

        private void CreateSmashEffectRenderer()
        {
            var effectObject = new GameObject("LifestealSmashEffect (Temp)");
            _smashEffectRenderer = effectObject.AddComponent<SpriteRenderer>();
            _smashEffectRenderer.sortingOrder = 2;
            if (effectMaterial != null) _smashEffectRenderer.material = effectMaterial;
            effectObject.SetActive(false);

            if (effectMaterial != null && smashEffectFrames != null && smashEffectFrames.Length > 0)
            {
                StartCoroutine(WarmUpEffectShader(_smashEffectRenderer, smashEffectFrames[0]));
            }
        }

        // Forces the additive shader variant to compile on scene load (one invisible on-screen
        // frame) instead of during the player's first real proc, where a compile stutter would
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

        private void OnEnable()
        {
            PlayerDamageEvents.OnDamageDealt += HandleDamageDealt;
        }

        private void OnDisable()
        {
            PlayerDamageEvents.OnDamageDealt -= HandleDamageDealt;

            if (_smashEffectRenderer != null)
            {
                _smashEffectRenderer.gameObject.SetActive(false);
            }
        }

        private void HandleDamageDealt(GameObject target)
        {
            if (data == null || target == null) return;
            if (Random.value > procChance) return;
            if (!target.TryGetComponent<IDamageable>(out var damageable)) return;

            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var bonusDamage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            damageable.TakeDamage(bonusDamage, DamageStatType.AttackPower, penetration);

            if (_health != null)
            {
                _health.Heal(healAmount);
            }

            var hitPoint = (Vector2)target.transform.position;
            var facing = (hitPoint - origin).sqrMagnitude > 0.0001f ? (hitPoint - origin).normalized : Vector2.right;
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            PlaySmashEffect(hitPoint, facing, radius);
        }

        private void PlaySmashEffect(Vector2 hitPoint, Vector2 facing, float radius)
        {
            if (smashEffectFrames == null || smashEffectFrames.Length == 0) return;

            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var scale = radius / SmashEffectReferenceRadius;
            var transformComponent = _smashEffectRenderer.transform;
            transformComponent.position = hitPoint;
            transformComponent.rotation = Quaternion.Euler(0f, 0f, angle);
            transformComponent.localScale = Vector3.one * scale;

            if (_smashEffectCoroutine != null)
            {
                StopCoroutine(_smashEffectCoroutine);
            }

            _smashEffectCoroutine = StartCoroutine(SmashEffectRoutine());
        }

        private IEnumerator SmashEffectRoutine()
        {
            _smashEffectRenderer.gameObject.SetActive(true);
            foreach (var frameSprite in smashEffectFrames)
            {
                _smashEffectRenderer.sprite = frameSprite;
                yield return new WaitForSeconds(smashFrameDuration);
            }

            _smashEffectRenderer.gameObject.SetActive(false);
            _smashEffectCoroutine = null;
        }
    }
}

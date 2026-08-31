using System.Collections;
using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorForwardWeapon : LevelableWeapon
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private const float IndicatorDuration = 0.15f;
        private const int FanSegments = 16;
        private const float SlashOriginOffset = 0.32f;
        // Calibrated so the slash art's outer edge lands exactly on the AoE radius.
        // SlashEffect.aseprite: 64px canvas, PPU 50, centre-pivot, art reaches 31px right of centre
        // => 0.62 world units at scale 1. Effect is placed at origin + facing * (SlashOriginOffset * scale),
        // so reach = scale * (0.32 + 0.62). Setting the reference to that sum makes scale = radius / reach.
        private const float SlashEffectReferenceRadius = 0.94f;

        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float forwardAngleDegrees = 150f;
        [SerializeField] private Color indicatorColor = new(1f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Sprite[] slashEffectFrames;
        [SerializeField] private float slashFrameDuration = 0.05f;
        [SerializeField] private Material effectMaterial;

        private float _timer;
        private float _indicatorTimer;
        private PlayerStats _stats;
        private Transform _indicator;
        private Mesh _indicatorMesh;
        private SpriteRenderer _slashEffectRenderer;
        private Coroutine _slashEffectCoroutine;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            CreateIndicator();
            CreateSlashEffectRenderer();
        }

        private void CreateSlashEffectRenderer()
        {
            var effectObject = new GameObject("SlashEffect (Temp)");
            _slashEffectRenderer = effectObject.AddComponent<SpriteRenderer>();
            _slashEffectRenderer.sortingOrder = 2;
            if (effectMaterial != null) _slashEffectRenderer.material = effectMaterial;
            effectObject.SetActive(false);

            if (effectMaterial != null && slashEffectFrames != null && slashEffectFrames.Length > 0)
            {
                StartCoroutine(WarmUpEffectShader(_slashEffectRenderer, slashEffectFrames[0]));
            }
        }

        // Forces the additive shader variant to compile on scene load (one invisible on-screen
        // frame) instead of during the player's first real attack, where a compile stutter would
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

        private void CreateIndicator()
        {
            var indicatorObject = new GameObject("ForwardAttackIndicator (Temp)");
            var meshFilter = indicatorObject.AddComponent<MeshFilter>();
            var meshRenderer = indicatorObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = indicatorColor };
            meshRenderer.sortingOrder = 1;

            _indicatorMesh = new Mesh();
            meshFilter.mesh = _indicatorMesh;
            _indicator = indicatorObject.transform;
            indicatorObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_indicator != null)
            {
                _indicator.gameObject.SetActive(false);
            }

            if (_slashEffectRenderer != null)
            {
                _slashEffectRenderer.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (data == null) return;

            if (_indicatorTimer > 0f)
            {
                _indicatorTimer -= Time.deltaTime;
                if (_indicatorTimer <= 0f)
                {
                    _indicator.gameObject.SetActive(false);
                }
            }

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            if (Attack())
            {
                _timer = 0f;
            }
        }

        private bool Attack()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var target = EnemyTargeting.FindNearest(origin, radius);
            if (target == null) return false;

            var facing = (EnemyTargeting.GetHitPoint(target) - origin).normalized;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var forwardDotThreshold = Mathf.Cos(forwardAngleDegrees * 0.5f * Mathf.Deg2Rad);

            EnemyTargeting.OverlapEnemies(origin, radius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var toEnemy = ((Vector2)hit.transform.position - origin).normalized;
                if (Vector2.Dot(facing, toEnemy) < forwardDotThreshold) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(Mathf.RoundToInt(data.Damage * damageMultiplier), DamageStatType.AttackPower, penetration);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                }
            }

            PlaySlashEffect(origin, facing, radius);
            return true;
        }

        private void PlaySlashEffect(Vector2 origin, Vector2 facing, float radius)
        {
            if (slashEffectFrames == null || slashEffectFrames.Length == 0) return;

            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var scale = radius / SlashEffectReferenceRadius;
            var transformComponent = _slashEffectRenderer.transform;
            transformComponent.position = origin + facing * (SlashOriginOffset * scale);
            transformComponent.rotation = Quaternion.Euler(0f, 0f, angle);
            transformComponent.localScale = Vector3.one * scale;

            if (_slashEffectCoroutine != null)
            {
                StopCoroutine(_slashEffectCoroutine);
            }

            _slashEffectCoroutine = StartCoroutine(SlashEffectRoutine());
        }

        private IEnumerator SlashEffectRoutine()
        {
            _slashEffectRenderer.gameObject.SetActive(true);
            foreach (var frameSprite in slashEffectFrames)
            {
                _slashEffectRenderer.sprite = frameSprite;
                yield return new WaitForSeconds(slashFrameDuration);
            }

            _slashEffectRenderer.gameObject.SetActive(false);
            _slashEffectCoroutine = null;
        }

        private void ShowIndicator(Vector2 origin, Vector2 facing, float radius)
        {
            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            BuildFanMesh(radius);
            _indicator.position = origin;
            _indicator.rotation = Quaternion.Euler(0f, 0f, angle);
            _indicator.gameObject.SetActive(true);
            _indicatorTimer = IndicatorDuration;
        }

        private void BuildFanMesh(float radius)
        {
            var halfAngleRad = forwardAngleDegrees * 0.5f * Mathf.Deg2Rad;
            var vertices = new Vector3[FanSegments + 2];
            var triangles = new int[FanSegments * 3];

            vertices[0] = Vector3.zero;
            for (var i = 0; i <= FanSegments; i++)
            {
                var t = (float)i / FanSegments;
                var segmentAngle = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
                vertices[i + 1] = new Vector3(Mathf.Cos(segmentAngle), Mathf.Sin(segmentAngle), 0f) * radius;
            }

            for (var i = 0; i < FanSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _indicatorMesh.Clear();
            _indicatorMesh.vertices = vertices;
            _indicatorMesh.triangles = triangles;
            _indicatorMesh.RecalculateBounds();
        }
    }
}

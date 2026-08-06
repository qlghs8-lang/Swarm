using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorComboAttackWeapon : LevelableWeapon
    {
        private const float IndicatorDuration = 0.15f;
        private const int FanSegments = 16;

        [SerializeField] private AoeWeaponData[] steps;
        [SerializeField] private float forwardAngleDegrees = 150f;
        [SerializeField] private Color indicatorColor = new(1f, 0.3f, 0.3f, 0.6f);

        private int _stepIndex;
        private float _timer;
        private float _indicatorTimer;
        private PlayerStats _stats;
        private Transform _indicator;
        private Mesh _indicatorMesh;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            CreateIndicator();
        }

        private void CreateIndicator()
        {
            var indicatorObject = new GameObject("ComboAttackIndicator (Temp)");
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
        }

        private void Update()
        {
            if (steps == null || steps.Length == 0) return;

            if (_indicatorTimer > 0f)
            {
                _indicatorTimer -= Time.deltaTime;
                if (_indicatorTimer <= 0f)
                {
                    _indicator.gameObject.SetActive(false);
                }
            }

            var data = steps[_stepIndex];
            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            _timer = 0f;
            Attack(data);
            _stepIndex = (_stepIndex + 1) % steps.Length;
        }

        private void Attack(AoeWeaponData data)
        {
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var target = EnemyTargeting.FindNearest(transform.position, radius);
            if (target == null) return;

            var facing = ((Vector2)target.position - (Vector2)transform.position).normalized;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var forwardDotThreshold = Mathf.Cos(forwardAngleDegrees * 0.5f * Mathf.Deg2Rad);

            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var toEnemy = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
                if (Vector2.Dot(facing, toEnemy) < forwardDotThreshold) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(Mathf.RoundToInt(data.Damage * damageMultiplier), DamageStatType.AttackPower, penetration);
                }
            }

            ShowIndicator(facing, radius);
        }

        private void ShowIndicator(Vector2 facing, float radius)
        {
            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            BuildFanMesh(radius);
            _indicator.position = transform.position;
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

using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorSwordChasingWeapon : LevelableWeapon
    {
        [SerializeField] private OrbitWeaponData data;
        [SerializeField] private float chaseRange = 4f;
        [SerializeField] private float chaseSpeed = 10f;
        [SerializeField] private float swoopDistance = 1.5f;
        [SerializeField] private Sprite bladeSprite;
        [SerializeField] private Color bladeColor = new(1f, 0.4f, 0.2f, 0.9f);

        private PlayerStats _stats;
        private readonly List<ChasingBlade> _blades = new();
        private float _sharedAngleDegrees;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            foreach (var blade in _blades)
            {
                if (blade != null) blade.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            foreach (var blade in _blades)
            {
                if (blade != null) blade.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (data == null) return;

            var desiredCount = Mathf.Max(1, 1 + (_stats != null ? _stats.ProjectileCountBonus : 0));
            SyncBladeCount(desiredCount);

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var rotationSpeedMultiplier = 1f / Mathf.Max(0.1f, cooldownMultiplier);
            _sharedAngleDegrees += data.AngularSpeed * rotationSpeedMultiplier * Time.deltaTime;

            var areaMultiplier = 1f + (_stats != null ? _stats.AreaSizeBonus : 0f);
            var orbitRadius = data.OrbitRadius * areaMultiplier;
            var hitRadius = data.HitRadius * areaMultiplier;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var hitCooldown = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            for (var i = 0; i < _blades.Count; i++)
            {
                var angle = _sharedAngleDegrees + 360f / _blades.Count * i;
                _blades[i].Configure(transform, orbitRadius, angle, hitRadius, chaseRange, chaseSpeed, swoopDistance, _blades);
                _blades[i].SetCombatStats(damage, hitCooldown, penetration);
            }
        }

        private void SyncBladeCount(int desiredCount)
        {
            while (_blades.Count < desiredCount)
            {
                var bladeObject = new GameObject("ChasingBlade (Temp)");
                var spriteRenderer = bladeObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = bladeSprite;
                spriteRenderer.color = bladeColor;
                spriteRenderer.sortingOrder = 1;
                _blades.Add(bladeObject.AddComponent<ChasingBlade>());
            }

            while (_blades.Count > desiredCount)
            {
                var last = _blades[^1];
                _blades.RemoveAt(_blades.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }
        }
    }
}

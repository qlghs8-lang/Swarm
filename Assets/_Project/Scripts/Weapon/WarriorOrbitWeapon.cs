using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorOrbitWeapon : LevelableWeapon
    {
        // Blade art (sword2/sword3.aseprite) is a 64px canvas at PPU 100 with a centre pivot; the
        // sword spans 62px vertically, i.e. 0.31 world units from the pivot to the tip at scale 1.
        // Scaling by hitRadius / 0.31 makes the drawn sword span the blade's hit circle diameter.
        private const float BladeSpriteHalfLengthUnits = 0.31f;

        [SerializeField] private OrbitWeaponData data;
        [SerializeField] private Sprite bladeSprite;
        [SerializeField] private Color bladeColor = new(0.3f, 0.6f, 1f, 0.9f);

        private PlayerStats _stats;
        private readonly List<OrbitingBlade> _blades = new();
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
            var areaMultiplier = 1f + (_stats != null ? _stats.AreaSizeBonus : 0f);
            // Divide by areaMultiplier so tangential speed (angularSpeed * radius) stays constant
            // as range-increase passives grow the orbit radius, instead of the blades visibly
            // speeding up.
            _sharedAngleDegrees += data.AngularSpeed * rotationSpeedMultiplier / areaMultiplier * Time.deltaTime;

            var orbitRadius = data.OrbitRadius * areaMultiplier;
            var hitRadius = data.HitRadius * areaMultiplier;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var hitCooldown = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            for (var i = 0; i < _blades.Count; i++)
            {
                var angle = _sharedAngleDegrees + 360f / _blades.Count * i;
                _blades[i].Configure(transform, orbitRadius, angle, hitRadius);
                _blades[i].transform.localScale = Vector3.one * (hitRadius / BladeSpriteHalfLengthUnits);
                _blades[i].SetCombatStats(damage, hitCooldown, penetration, isCritical);
            }
        }

        private void SyncBladeCount(int desiredCount)
        {
            while (_blades.Count < desiredCount)
            {
                var bladeObject = new GameObject("OrbitingBlade (Temp)");
                var spriteRenderer = bladeObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = bladeSprite;
                spriteRenderer.color = bladeColor;
                spriteRenderer.sortingLayerName = SortingLayers.EFFECT;
                spriteRenderer.sortingOrder = 0;
                _blades.Add(bladeObject.AddComponent<OrbitingBlade>());
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

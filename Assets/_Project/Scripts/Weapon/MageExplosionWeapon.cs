using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageExplosionWeapon : LevelableWeapon
    {
        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float detectRange = 8f;
        [SerializeField] private float meteorFallSpeed = 9.8f;
        [SerializeField] private float meteorFallOffset = 5f;
        [SerializeField] private float firePatchRadius = 1.8f;
        [SerializeField] private float firePatchDuration = 3f;
        [SerializeField] private float burnDuration = 3f;
        [SerializeField] private float burnTickInterval = 0.5f;
        [SerializeField] private int burnDamagePerTick = 4;
        [SerializeField] private Sprite meteorSprite;
        [SerializeField] private Color meteorColor = new(1f, 0.5f, 0.15f, 1f);
        [SerializeField] private Sprite firePatchSprite;
        [SerializeField] private Color firePatchColor = new(1f, 0.35f, 0.1f, 0.5f);

        private float _timer;
        private PlayerStats _stats;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (data == null) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            _timer = 0f;
            TryFire();
        }

        private void TryFire()
        {
            var areaMultiplier = 1f + (_stats != null ? _stats.AreaSizeBonus : 0f);

            var meteorCount = Mathf.Max(1, 1 + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var targets = EnemyTargeting.FindMultiple(transform.position, detectRange, meteorCount);
            if (targets.Count == 0) return;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var impactDamage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var burnTick = Mathf.RoundToInt(burnDamagePerTick * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var impactRadius = data.Radius * areaMultiplier;
            var patchRadius = firePatchRadius * areaMultiplier;

            foreach (var target in targets)
            {
                SpawnMeteor(target.position, impactRadius, impactDamage, damageType, penetration, patchRadius, burnTick);
            }
        }

        private void SpawnMeteor(Vector2 targetPosition, float impactRadius, int impactDamage, DamageStatType damageType,
            float penetration, float patchRadius, int burnTick)
        {
            var meteorObject = new GameObject("Meteor (Temp)");
            var spriteRenderer = meteorObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = meteorSprite;
            spriteRenderer.color = meteorColor;
            spriteRenderer.sortingOrder = 2;

            var meteor = meteorObject.AddComponent<Meteor>();
            meteor.Launch(targetPosition, meteorFallOffset, meteorFallSpeed, impactRadius, impactDamage, damageType, penetration,
                impactPosition => SpawnFirePatch(impactPosition, patchRadius, burnTick, damageType, penetration));
        }

        private void SpawnFirePatch(Vector2 position, float radius, int burnTick, DamageStatType damageType, float penetration)
        {
            var patchObject = new GameObject("FirePatch (Temp)");
            patchObject.transform.position = position;

            var spriteRenderer = patchObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = firePatchSprite;
            spriteRenderer.color = firePatchColor;
            spriteRenderer.sortingOrder = -1;

            var patch = patchObject.AddComponent<FirePatch>();
            patch.Configure(radius, firePatchDuration, burnTick, burnTickInterval, burnDuration, damageType, penetration);
        }
    }
}

using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageExplosionWeapon : LevelableWeapon
    {
        // Reused across calls so target selection allocates nothing per shot.
        private readonly List<Transform> _targetBuffer = new();

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
        [SerializeField] private Sprite[] meteorTravelFrames;
        [SerializeField] private float meteorTravelFrameDuration = 0.08f;
        [SerializeField] private float meteorVisualScale = 1f;
        [SerializeField] private Sprite[] meteorImpactEffectFrames;
        [SerializeField] private float meteorImpactEffectFrameDuration = 0.06f;
        [SerializeField] private float meteorImpactEffectScale = 1f;
        [SerializeField] private Sprite firePatchSprite;
        [SerializeField] private Color firePatchColor = new(1f, 0.35f, 0.1f, 0.5f);
        [SerializeField] private Sprite[] firePatchFrames;
        [SerializeField] private Material effectMaterial;

        private float _timer;
        private PlayerStats _stats;
        private SpriteEffectPlayer _impactEffect;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _impactEffect = SpriteEffectPlayer.Create(
                this, "MeteorImpactEffect (Temp)", effectMaterial, meteorImpactEffectFrames, meteorImpactEffectFrameDuration,
                initialScale: meteorImpactEffectScale);
        }

        private void OnDisable()
        {
            _impactEffect?.Hide();
        }

        private void Update()
        {
            if (data == null) return;

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
            var areaMultiplier = 1f + (_stats != null ? _stats.AreaSizeBonus : 0f);

            var meteorCount = Mathf.Max(1, 1 + (_stats != null ? _stats.ProjectileCountBonus : 0));
            EnemyTargeting.FindMultiple(origin, detectRange, meteorCount, _targetBuffer);
            var targets = _targetBuffer;
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var impactDamage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            // The impact can crit; the fire patch it leaves is damage over time, which cannot,
            // so the burn tick is sized off the un-critted multiplier.
            var burnMultiplier = DamageMultiplier * (_stats != null ? _stats.BaseDamageMultiplier : 1f);
            var burnTick = Mathf.RoundToInt(burnDamagePerTick * burnMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var impactRadius = data.Radius * areaMultiplier;
            var patchRadius = firePatchRadius * areaMultiplier;

            foreach (var target in targets)
            {
                SpawnMeteor(EnemyTargeting.GetHitPoint(target), impactRadius, impactDamage, damageType, penetration, patchRadius, burnTick, isCritical);
            }

            return true;
        }

        private void SpawnMeteor(Vector2 targetPosition, float impactRadius, int impactDamage, DamageStatType damageType,
            float penetration, float patchRadius, int burnTick, bool isCritical)
        {
            var meteorObject = new GameObject("Meteor (Temp)");
            meteorObject.transform.localScale = Vector3.one * meteorVisualScale;

            var spriteRenderer = meteorObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 2;
            if (effectMaterial != null) spriteRenderer.material = effectMaterial;

            var meteor = meteorObject.AddComponent<Meteor>();
            meteor.Launch(targetPosition, meteorFallOffset, meteorFallSpeed, impactRadius, impactDamage, damageType, penetration,
                impactPosition =>
                {
                    PlayImpactEffect(impactPosition);
                    SpawnFirePatch(impactPosition, patchRadius, burnTick, damageType, penetration);
                }, isCritical);

            if (meteorTravelFrames != null && meteorTravelFrames.Length > 0)
            {
                spriteRenderer.sprite = meteorTravelFrames[0];
                meteor.SetTravelAnimation(spriteRenderer, meteorTravelFrames, meteorTravelFrameDuration);
            }
            else
            {
                spriteRenderer.sprite = meteorSprite;
                spriteRenderer.color = meteorColor;
            }
        }

        private void PlayImpactEffect(Vector2 position)
        {
            _impactEffect?.Play(position, 0f, meteorImpactEffectScale);
        }

        private void SpawnFirePatch(Vector2 position, float radius, int burnTick, DamageStatType damageType, float penetration)
        {
            var patchObject = new GameObject("FirePatch (Temp)");
            patchObject.transform.position = position;

            var spriteRenderer = patchObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = -1;

            var patch = patchObject.AddComponent<FirePatch>();

            if (firePatchFrames != null && firePatchFrames.Length > 0)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.55f);
                patch.Configure(radius, firePatchDuration, burnTick, burnTickInterval, burnDuration, damageType, penetration, firePatchFrames);
            }
            else
            {
                spriteRenderer.sprite = firePatchSprite;
                spriteRenderer.color = firePatchColor;
                patch.Configure(radius, firePatchDuration, burnTick, burnTickInterval, burnDuration, damageType, penetration);
            }
        }
    }
}

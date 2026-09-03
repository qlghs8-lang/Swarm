using System.Collections;
using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageBoltWeapon : LevelableWeapon
    {
        // Reused across calls so target selection allocates nothing per shot.
        private readonly List<Transform> _targetBuffer = new();

        [SerializeField] private AoeWeaponData data;
        [SerializeField] private int baseTargetCount = 2;
        [SerializeField] private int levelsPerExtraTarget = 2;
        [SerializeField] private GameObject strikeFlashPrefab;

        // A brief hitstop on everything the strike touches. The mage has no knockback, so this is
        // what buys the space a melee shove would: at a 1.1s cast it is roughly a quarter of the
        // time, enough to interrupt an approach without reading as a lockdown.
        [SerializeField] private float stunDuration = 0.3f;

        private float _timer;
        private PlayerStats _stats;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();

            if (strikeFlashPrefab != null)
            {
                StartCoroutine(WarmUpStrikeFlashShader());
            }
        }

        // Forces StrikeFlash's (additive) shader variant to compile on scene load instead of
        // during the player's first real strike, where a compile stutter would otherwise show up
        // as a flash of the wrong (uncompiled fallback) color.
        private IEnumerator WarmUpStrikeFlashShader()
        {
            var instance = SharedObjectPool.Get(strikeFlashPrefab, transform.position, Quaternion.identity);
            if (instance.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                var originalColor = renderer.color;
                renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            }

            yield return null;

            SharedObjectPool.Release(strikeFlashPrefab, instance);
        }

        private void Update()
        {
            if (data == null) return;

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
            // Projectile Count is the mage's synergy passive here, the same way Area Size is the
            // warrior's: without it the strike hit a fixed handful no matter how thick the crowd
            // got, so damage scattered across a re-picked nearest-N every cast and nothing ever
            // reached its health total.
            var targetCount = baseTargetCount
                              + (Level - 1) / levelsPerExtraTarget
                              + (_stats != null ? _stats.ProjectileCountBonus : 0);
            EnemyTargeting.FindMultiple(origin, radius, targetCount, _targetBuffer);
            var targets = _targetBuffer;
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            foreach (var target in targets)
            {
                if (target.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage, damageType, penetration, isCritical);
                    PlayerDamageEvents.RaiseDamageDealt(target.gameObject);

                    if (target.gameObject.activeInHierarchy && target.TryGetComponent<EnemyChaser>(out var chaser))
                    {
                        chaser.ApplyStun(stunDuration);
                    }
                }

                SpawnStrikeFlash(EnemyTargeting.GetHitPoint(target));
            }

            return true;
        }

        private void SpawnStrikeFlash(Vector3 position)
        {
            if (strikeFlashPrefab == null) return;

            var instance = SharedObjectPool.Get(strikeFlashPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
            if (instance.TryGetComponent<StrikeFlash>(out var flash))
            {
                flash.SetSourcePrefab(strikeFlashPrefab);
            }
        }
    }
}

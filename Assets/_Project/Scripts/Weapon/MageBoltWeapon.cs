using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageBoltWeapon : LevelableWeapon
    {
        [SerializeField] private AoeWeaponData data;
        [SerializeField] private int baseTargetCount = 2;
        [SerializeField] private int levelsPerExtraTarget = 2;
        [SerializeField] private GameObject strikeFlashPrefab;

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
            Attack();
        }

        private void Attack()
        {
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var targetCount = baseTargetCount + (Level - 1) / levelsPerExtraTarget;
            var targets = EnemyTargeting.FindMultiple(transform.position, radius, targetCount);
            if (targets.Count == 0) return;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            foreach (var target in targets)
            {
                if (target.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage, damageType, penetration);
                }

                SpawnStrikeFlash(target.position);
            }
        }

        private void SpawnStrikeFlash(Vector3 position)
        {
            if (strikeFlashPrefab == null) return;

            Instantiate(strikeFlashPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
        }
    }
}

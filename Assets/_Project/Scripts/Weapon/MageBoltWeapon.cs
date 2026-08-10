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

            if (Attack())
            {
                _timer = 0f;
            }
        }

        private bool Attack()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var targetCount = baseTargetCount + (Level - 1) / levelsPerExtraTarget;
            var targets = EnemyTargeting.FindMultiple(origin, radius, targetCount);
            if (targets.Count == 0) return false;

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

            return true;
        }

        private void SpawnStrikeFlash(Vector3 position)
        {
            if (strikeFlashPrefab == null) return;

            Instantiate(strikeFlashPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
        }
    }
}

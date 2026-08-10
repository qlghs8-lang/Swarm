using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageIceBoltWeapon : LevelableWeapon
    {
        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private int baseJumps = 2;
        [SerializeField] private int levelsPerExtraJump = 2;
        [SerializeField] private float chainRange = 4f;
        [SerializeField] private float freezeChance = 0.1f;
        [SerializeField] private float freezeDuration = 1f;
        [SerializeField] private Sprite boltSprite;
        [SerializeField] private Color boltColor = new(0.5f, 0.85f, 1f, 0.95f);

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

            if (TryFire())
            {
                _timer = 0f;
            }
        }

        private bool TryFire()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var boltCount = Mathf.Max(1, 1 + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var targets = EnemyTargeting.FindMultiple(origin, data.Range, boltCount);
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var maxJumps = baseJumps + (Level - 1) / levelsPerExtraJump;

            for (var i = 0; i < targets.Count; i++)
            {
                SpawnBolt(origin, targets[i], damage, damageType, penetration, maxJumps);
            }

            return true;
        }

        private void SpawnBolt(Vector2 origin, Transform target, int damage, DamageStatType damageType, float penetration, int maxJumps)
        {
            var boltObject = new GameObject("IceBolt (Temp)");
            boltObject.transform.position = origin;

            var spriteRenderer = boltObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = boltSprite;
            spriteRenderer.color = boltColor;
            spriteRenderer.sortingOrder = 1;

            var bolt = boltObject.AddComponent<ChainLightningBolt>();
            bolt.Launch(target, damage, data.ProjectileSpeed, maxJumps, chainRange, freezeChance, freezeDuration, penetration, damageType);
        }
    }
}

using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorForwardWeapon : LevelableWeapon
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        private const float SlashOriginOffset = 0.32f;
        // Calibrated so the slash art's outer edge lands exactly on the AoE radius.
        // SlashEffect.aseprite: 64px canvas, PPU 50, centre-pivot, art reaches 31px right of centre
        // => 0.62 world units at scale 1. Effect is placed at origin + facing * (SlashOriginOffset * scale),
        // so reach = scale * (0.32 + 0.62). Setting the reference to that sum makes scale = radius / reach.
        private const float SlashEffectReferenceRadius = 0.94f;

        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float forwardAngleDegrees = 150f;
        [SerializeField] private Sprite[] slashEffectFrames;
        [SerializeField] private float slashFrameDuration = 0.05f;
        [SerializeField] private Material effectMaterial;
        // Knockback is opt-in per weapon (see IDamageable.TakeDamage).
        [SerializeField, Range(0f, 2f)] private float knockbackScale = 1f;

        private float _timer;
        private PlayerStats _stats;
        private SpriteEffectPlayer _slashEffect;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _slashEffect = SpriteEffectPlayer.Create(
                this, "SlashEffect (Temp)", effectMaterial, slashEffectFrames, slashFrameDuration);
        }

        private void OnDisable()
        {
            _slashEffect?.Hide();
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
            var target = EnemyTargeting.FindNearest(origin, radius);
            if (target == null) return false;

            var facing = (EnemyTargeting.GetHitPoint(target) - origin).normalized;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var halfAngleDegrees = forwardAngleDegrees * 0.5f;

            EnemyTargeting.OverlapEnemies(origin, radius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                if (!EnemyTargeting.IsInsideCone(hit, origin, facing, halfAngleDegrees)) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(Mathf.RoundToInt(data.Damage * damageMultiplier), DamageStatType.AttackPower, penetration, isCritical, knockbackScale);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                }
            }

            PlaySlashEffect(origin, facing, radius);
            return true;
        }

        private void PlaySlashEffect(Vector2 origin, Vector2 facing, float radius)
        {
            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var scale = radius / SlashEffectReferenceRadius;
            _slashEffect?.Play(origin + facing * (SlashOriginOffset * scale), angle, scale);
        }
    }
}

using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorComboAttackWeapon : LevelableWeapon
    {
        // Reused across calls: the old OverlapCircleAll allocated a new array every hit tick.
        private readonly List<Collider2D> _hitBuffer = new();

        [SerializeField] private AoeWeaponData[] steps;
        [SerializeField] private EffectFrameSet[] stepEffects;
        [SerializeField] private float effectFrameDuration = 0.05f;
        [SerializeField] private Material effectMaterial;
        // Knockback is opt-in per weapon (see IDamageable.TakeDamage).
        [SerializeField, Range(0f, 2f)] private float knockbackScale = 0.3f;

        private static readonly float[] StepEffectOriginOffset = { 0.3f, 0.52f, 0.15f };
        // Per step: StepEffectOriginOffset + (art's forward reach in px / PPU 100), so the artwork's
        // leading edge lands on the step's AoE radius. Slash/Thrust reach 31px right of the 64px canvas
        // centre; Slam is drawn facing -Y and reaches 32px down. Steps are { Slash, Thrust, Slam }.
        private static readonly float[] StepEffectReferenceRadius = { 0.61f, 0.83f, 0.47f };
        // Slam artwork is drawn facing down (-Y) instead of right (+X) like Slash/Thrust, so its rotation needs a +90° correction.
        private static readonly float[] StepEffectRotationOffsetDegrees = { 0f, 0f, 90f };

        private int _stepIndex;
        private float _timer;
        private PlayerStats _stats;
        private SpriteEffectPlayer _effect;

        [System.Serializable]
        private class EffectFrameSet
        {
            public Sprite[] frames;
        }

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _effect = SpriteEffectPlayer.Create(
                this, "ComboAttackEffect (Temp)", effectMaterial, null, effectFrameDuration,
                warmUpSprite: FindFirstEffectFrame());
        }

        private Sprite FindFirstEffectFrame()
        {
            if (stepEffects == null) return null;
            foreach (var step in stepEffects)
            {
                if (step?.frames != null && step.frames.Length > 0) return step.frames[0];
            }

            return null;
        }

        private void OnDisable()
        {
            _effect?.Hide();
        }

        private void Update()
        {
            if (steps == null || steps.Length == 0) return;

            var data = steps[_stepIndex];
            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            if (Attack(data))
            {
                _timer = 0f;
                _stepIndex = (_stepIndex + 1) % steps.Length;
            }
        }

        private bool Attack(AoeWeaponData data)
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var areaMultiplier = 1f + (_stats != null ? _stats.AreaSizeBonus : 0f);
            var radius = data.Radius * areaMultiplier;
            var target = EnemyTargeting.FindNearest(origin, radius);
            if (target == null) return false;

            var facing = (EnemyTargeting.GetHitPoint(target) - origin).normalized;
            var hitCenter = origin + facing * (data.ForwardOffset * areaMultiplier);
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var halfAngleDegrees = data.AttackAngleDegrees * 0.5f;

            EnemyTargeting.OverlapEnemies(hitCenter, radius, _hitBuffer);
            var hits = _hitBuffer;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                if (!EnemyTargeting.IsInsideCone(hit, hitCenter, facing, halfAngleDegrees)) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(Mathf.RoundToInt(data.Damage * damageMultiplier), DamageStatType.AttackPower, penetration, isCritical, knockbackScale);
                    PlayerDamageEvents.RaiseDamageDealt(hit.gameObject);
                }
            }

            PlayStepEffect(_stepIndex, hitCenter, facing, radius);
            return true;
        }

        private void PlayStepEffect(int stepIndex, Vector2 hitCenter, Vector2 facing, float radius)
        {
            if (stepEffects == null || stepIndex >= stepEffects.Length) return;
            var frames = stepEffects[stepIndex]?.frames;
            if (frames == null || frames.Length == 0) return;

            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + StepEffectRotationOffsetDegrees[stepIndex];
            var scale = radius / StepEffectReferenceRadius[stepIndex];
            _effect?.Play(frames, hitCenter + facing * (StepEffectOriginOffset[stepIndex] * scale), angle, scale);
        }
    }
}

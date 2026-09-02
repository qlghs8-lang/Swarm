using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class WarriorLifestealWeapon : LevelableWeapon
    {
        private const float SmashEffectReferenceRadius = 1.2f;

        [SerializeField] private AoeWeaponData data;
        [SerializeField] private float procChance = 0.1f;
        [SerializeField] private int healAmount = 3;
        [SerializeField] private Sprite[] smashEffectFrames;
        [SerializeField] private float smashFrameDuration = 0.07f;
        [SerializeField] private Material effectMaterial;

        private PlayerStats _stats;
        private PlayerHealth _health;
        private SpriteEffectPlayer _smashEffect;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _smashEffect = SpriteEffectPlayer.Create(
                this, "LifestealSmashEffect (Temp)", effectMaterial, smashEffectFrames, smashFrameDuration);
        }

        private void OnEnable()
        {
            PlayerDamageEvents.OnDamageDealt += HandleDamageDealt;
        }

        private void OnDisable()
        {
            PlayerDamageEvents.OnDamageDealt -= HandleDamageDealt;

            _smashEffect?.Hide();
        }

        private void HandleDamageDealt(GameObject target)
        {
            if (data == null || target == null) return;
            if (Random.value > procChance) return;
            if (!target.TryGetComponent<IDamageable>(out var damageable)) return;

            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            // Fixed-damage proc: excluded from crits, and rolling here would also clobber the
            // crit flag of the attack that triggered this proc.
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.BaseDamageMultiplier : 1f);
            var bonusDamage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            damageable.TakeDamage(bonusDamage, DamageStatType.AttackPower, penetration);

            if (_health != null)
            {
                _health.Heal(healAmount);
            }

            var hitPoint = (Vector2)target.transform.position;
            var facing = (hitPoint - origin).sqrMagnitude > 0.0001f ? (hitPoint - origin).normalized : Vector2.right;
            var radius = data.Radius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            PlaySmashEffect(hitPoint, facing, radius);
        }

        private void PlaySmashEffect(Vector2 hitPoint, Vector2 facing, float radius)
        {
            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var scale = radius / SmashEffectReferenceRadius;
            _smashEffect?.Play(hitPoint, angle, scale);
        }
    }
}

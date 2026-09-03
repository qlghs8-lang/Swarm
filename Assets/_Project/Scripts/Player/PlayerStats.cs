using UnityEngine;

namespace Swarm.Player
{
    public enum DamageStatType
    {
        AttackPower,
        MagicPower
    }

    public class PlayerStats : MonoBehaviour
    {
        private Collider2D _collider;

        // Weapon origin for targeting/AOE/indicators. Uses the actual hit collider's
        // center rather than transform.position, since the collider can be offset from
        // it (e.g. raised to chest height) and the two are not interchangeable.
        public Vector2 AttackOrigin => _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;

        public float AttackPower { get; private set; }
        public float MoveSpeedBonus { get; private set; }
        /// <summary>Critical chance, 0-1. Raised by the shop's permanent Luck upgrade and by the
        /// Luck level-up card; the stat had no consumer at all before this.</summary>
        public float Luck { get; private set; }

        public float CritChance => Mathf.Clamp01(Luck);

        /// <summary>True when the most recent <see cref="RollDamageMultiplier"/> critted.</summary>
        public bool LastAttackWasCritical { get; private set; }
        /// <summary>Pickup reach in world units. The base is not cosmetic: the hit collider sits
        /// at chest height (offset y 0.7, r 0.25) while an orb lies on the ground (r 0.3), so the
        /// two circles are 0.7 apart and 0.55 wide combined — standing directly on an orb did not
        /// touch it. With the base at zero the magnet was switched off entirely, which left the
        /// player unable to collect reliably at all until they bought the shop upgrade.</summary>
        public float MagnetRadius => baseMagnetRadius + _magnetRadiusBonus;
        public int ProjectileCountBonus { get; private set; }
        public float AreaSizeBonus { get; private set; }
        public float ArmorPenetration { get; private set; }
        public DamageStatType DamageStatType { get; private set; } = DamageStatType.AttackPower;

        private float _magicPower;
        private float _defenseBonus;
        private float _cooldownReduction;
        private float _magicPenetration;

        [SerializeField] private float critDamageBonus = 1f;

        // Deliberately just past the 0.7 collider gap and no further, so it only buys a working
        // pickup — the shop's magnet upgrade (+1.0 per level, 10 levels) keeps all of its value.
        [SerializeField] private float baseMagnetRadius = 1f;

        private float _magnetRadiusBonus;

        private float _tempMagicPower;
        private float _tempDefenseBonus;
        private float _tempCooldownReduction;
        private float _tempMagicPenetration;
        private float _buffTimer;

        public float MagicPower => _magicPower + _tempMagicPower;
        public float DefenseBonus => _defenseBonus + _tempDefenseBonus;
        public float CooldownReduction => _cooldownReduction + _tempCooldownReduction;
        public float MagicPenetration => _magicPenetration + _tempMagicPenetration;
        public float PermanentCooldownReduction => _cooldownReduction;
        public bool IsTemporaryBuffActive => _buffTimer > 0f;
        public float TemporaryBuffRemaining => Mathf.Max(0f, _buffTimer);

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (_buffTimer <= 0f) return;

            _buffTimer -= Time.deltaTime;
            if (_buffTimer <= 0f)
            {
                _tempMagicPower = 0f;
                _tempDefenseBonus = 0f;
                _tempCooldownReduction = 0f;
                _tempMagicPenetration = 0f;
            }
        }

        public void ApplyTemporaryBuff(float magicPowerBonus, float defenseBonus, float magicPenetrationBonus,
            float cooldownReductionBonus, float duration)
        {
            _tempMagicPower = magicPowerBonus;
            _tempDefenseBonus = defenseBonus;
            _tempMagicPenetration = magicPenetrationBonus;
            _tempCooldownReduction = cooldownReductionBonus;
            _buffTimer = duration;
        }

        public void IncreaseAttackPower(float percent)
        {
            AttackPower += percent;
        }

        public void IncreaseMagicPower(float percent)
        {
            _magicPower += percent;
        }

        public void IncreasePrimaryStat(float percent)
        {
            if (DamageStatType == DamageStatType.AttackPower)
            {
                IncreaseAttackPower(percent);
            }
            else
            {
                IncreaseMagicPower(percent);
            }
        }

        public void IncreaseMoveSpeed(float percent)
        {
            MoveSpeedBonus += percent;
        }

        public void IncreaseDefense(float percent)
        {
            _defenseBonus += percent;
        }

        public void IncreaseCooldownReduction(float percent)
        {
            _cooldownReduction += percent;
        }

        public void IncreaseLuck(float percent)
        {
            Luck += percent;
        }

        public void IncreaseMagnetRadius(float amount)
        {
            _magnetRadiusBonus += amount;
        }

        public void IncreaseProjectileCount(int amount)
        {
            ProjectileCountBonus += amount;
        }

        public void IncreaseAreaSize(float percent)
        {
            AreaSizeBonus += percent;
        }

        public void IncreaseArmorPenetration(float percent)
        {
            ArmorPenetration += percent;
        }

        public void IncreaseMagicPenetration(float percent)
        {
            _magicPenetration += percent;
        }

        public void SetDamageStatType(DamageStatType type)
        {
            DamageStatType = type;
        }

        /// <summary>
        /// The damage multiplier with no critical roll, for damage that is deliberately excluded
        /// from crits: damage-over-time ticks (poison gas, fire patch) and the fixed-damage warrior
        /// procs. Reading this leaves <see cref="LastAttackWasCritical"/> untouched, which also
        /// matters because the procs fire *inside* another weapon's attack and must not overwrite
        /// the flag that attack is about to read.
        /// </summary>
        public float BaseDamageMultiplier =>
            1f + (DamageStatType == DamageStatType.AttackPower ? AttackPower : MagicPower);

        /// <summary>
        /// The damage multiplier for one attack, rolling the critical once. Deliberately per
        /// attack rather than per target: an area weapon that crits should crit on everything it
        /// caught, which reads as a single big hit instead of a scatter of mixed numbers.
        /// </summary>
        public float RollDamageMultiplier()
        {
            LastAttackWasCritical = Random.value < CritChance;

            var multiplier = BaseDamageMultiplier;
            return LastAttackWasCritical ? multiplier * (1f + critDamageBonus) : multiplier;
        }

        public float GetPenetration()
        {
            return DamageStatType == DamageStatType.AttackPower ? ArmorPenetration : MagicPenetration;
        }
    }
}

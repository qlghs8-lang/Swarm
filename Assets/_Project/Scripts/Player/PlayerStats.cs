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
        public float Luck { get; private set; }
        public float MagnetRadius { get; private set; }
        public int ProjectileCountBonus { get; private set; }
        public float AreaSizeBonus { get; private set; }
        public float ArmorPenetration { get; private set; }
        public DamageStatType DamageStatType { get; private set; } = DamageStatType.AttackPower;

        private float _magicPower;
        private float _defenseBonus;
        private float _cooldownReduction;
        private float _magicPenetration;

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
            MagnetRadius += amount;
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

        public float GetDamageMultiplier()
        {
            return 1f + (DamageStatType == DamageStatType.AttackPower ? AttackPower : MagicPower);
        }

        public float GetPenetration()
        {
            return DamageStatType == DamageStatType.AttackPower ? ArmorPenetration : MagicPenetration;
        }
    }
}

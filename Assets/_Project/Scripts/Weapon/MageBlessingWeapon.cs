using Swarm.Player;
using Swarm.UI;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageBlessingWeapon : LevelableWeapon
    {
        [SerializeField] private int healAmount = 20;
        [SerializeField] private float baseCooldown = 30f;
        [SerializeField] private GameObject healNumberPrefab;
        [SerializeField] private Color healNumberColor = new(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private float magicPowerBonus = 0.2f;
        [SerializeField] private float magicPowerBonusPerLevel = 0.1f;
        [SerializeField] private float defenseBonus = 0.15f;
        [SerializeField] private float magicPenetrationBonus = 0.15f;
        [SerializeField] private float magicPenetrationBonusPerLevel = 0.05f;
        [SerializeField] private float cooldownReductionBonus = 0.25f;
        [SerializeField] private float buffDuration = 3f;

        private float _timer;
        private PlayerStats _stats;
        private PlayerHealth _health;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.PermanentCooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveCooldown) return;

            _timer = 0f;
            TriggerBlessing();
        }

        private void TriggerBlessing()
        {
            if (_health == null) return;

            var amount = Mathf.RoundToInt(healAmount * DamageMultiplier);
            _health.Heal(amount);
            SpawnHealNumber(amount);

            if (_stats != null)
            {
                var levelBonus = Mathf.Max(0, Level - 1);
                var totalMagicPowerBonus = magicPowerBonus + magicPowerBonusPerLevel * levelBonus;
                var totalMagicPenetrationBonus = magicPenetrationBonus + magicPenetrationBonusPerLevel * levelBonus;
                _stats.ApplyTemporaryBuff(totalMagicPowerBonus, defenseBonus, totalMagicPenetrationBonus, cooldownReductionBonus, buffDuration);
            }
        }

        private void SpawnHealNumber(int amount)
        {
            if (healNumberPrefab == null) return;

            var instance = Instantiate(healNumberPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            if (instance.TryGetComponent<DamageNumber>(out var damageNumber))
            {
                damageNumber.Setup($"+{amount}", healNumberColor);
            }
        }
    }
}

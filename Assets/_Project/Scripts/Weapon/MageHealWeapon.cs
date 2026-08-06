using Swarm.Player;
using Swarm.UI;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageHealWeapon : LevelableWeapon
    {
        [SerializeField] private int healAmount = 15;
        [SerializeField] private float baseCooldown = 30f;
        [SerializeField] private GameObject healNumberPrefab;
        [SerializeField] private Color healNumberColor = new(0.3f, 1f, 0.4f, 1f);

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
            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveCooldown) return;

            _timer = 0f;
            TriggerHeal();
        }

        private void TriggerHeal()
        {
            if (_health == null) return;

            var amount = Mathf.RoundToInt(healAmount * DamageMultiplier);
            _health.Heal(amount);
            SpawnHealNumber(amount);
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

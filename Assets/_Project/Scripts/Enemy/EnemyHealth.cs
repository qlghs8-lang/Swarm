using Swarm.Game;
using Swarm.Player;
using Swarm.UI;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Enemy
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 30;
        [SerializeField] private GameObject experiencePickupPrefab;
        [SerializeField] private int experienceReward = 5;
        [SerializeField] private GameObject goldPickupPrefab;
        [SerializeField] private float goldDropChance = 0.1f;
        [SerializeField] private float dropScatterRadius = 0.5f;
        [SerializeField] private GameObject damageNumberPrefab;

        private int _currentHealth;
        private int _currentMaxHealth;
        private float _defenseBonus;
        private float _magicDefenseBonus;
        private float _temporaryDefensePenalty;
        private bool _isDead;
        private ObjectPool _pool;

        private float _burnTimer;
        private float _burnTickTimer;
        private int _burnDamagePerTick;
        private float _burnTickInterval;
        private DamageStatType _burnDamageType;
        private float _burnPenetration;

        private bool _isPoisoned;
        private bool _poisonedThisFrame;
        private float _poisonTickTimer;
        private int _poisonDamagePerTick;
        private float _poisonTickInterval;
        private float _poisonDefensePenalty;
        private float _poisonSpeedMultiplier;
        private DamageStatType _poisonDamageType;
        private float _poisonPenetration;

        public event System.Action<int, int> OnHealthChanged;
        public event System.Action OnDied;

        private void Awake()
        {
            _currentMaxHealth = maxHealth;
            _currentHealth = maxHealth;
        }

        public void SetPool(ObjectPool pool)
        {
            _pool = pool;
        }

        public void ApplyDifficulty(float healthMultiplier, float defenseBonus, float magicDefenseBonus)
        {
            _currentMaxHealth = Mathf.RoundToInt(maxHealth * healthMultiplier);
            _defenseBonus = defenseBonus;
            _magicDefenseBonus = magicDefenseBonus;
        }

        public void ResetHealth()
        {
            _currentHealth = _currentMaxHealth;
            _isDead = false;
            _temporaryDefensePenalty = 0f;
            _burnTimer = 0f;
            _isPoisoned = false;
            _poisonedThisFrame = false;
            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);
        }

        public void SetTemporaryDefensePenalty(float penalty)
        {
            _temporaryDefensePenalty = penalty;
        }

        public void ApplyBurn(int damagePerTick, float tickInterval, float duration, DamageStatType damageType, float penetration)
        {
            _burnDamagePerTick = damagePerTick;
            _burnTickInterval = tickInterval;
            _burnDamageType = damageType;
            _burnPenetration = penetration;
            _burnTickTimer = tickInterval;
            _burnTimer = Mathf.Max(_burnTimer, duration);
        }

        public void ApplyPoison(int damagePerTick, float tickInterval, float defensePenalty, float speedMultiplier,
            DamageStatType damageType, float penetration)
        {
            _poisonedThisFrame = true;
            _poisonDamagePerTick = damagePerTick;
            _poisonTickInterval = tickInterval;
            _poisonDefensePenalty = defensePenalty;
            _poisonSpeedMultiplier = speedMultiplier;
            _poisonDamageType = damageType;
            _poisonPenetration = penetration;

            if (_isPoisoned) return;

            _isPoisoned = true;
            _poisonTickTimer = tickInterval;
            SetTemporaryDefensePenalty(defensePenalty);
            if (TryGetComponent<EnemyChaser>(out var chaser))
            {
                chaser.SetSpeedMultiplier(speedMultiplier);
            }
        }

        private void Update()
        {
            if (_burnTimer > 0f)
            {
                _burnTimer -= Time.deltaTime;
                _burnTickTimer -= Time.deltaTime;

                if (_burnTickTimer <= 0f)
                {
                    _burnTickTimer = _burnTickInterval;
                    TakeDamage(_burnDamagePerTick, _burnDamageType, _burnPenetration);
                }
            }

            if (_poisonedThisFrame)
            {
                _poisonedThisFrame = false;
                _poisonTickTimer -= Time.deltaTime;

                if (_poisonTickTimer <= 0f)
                {
                    _poisonTickTimer = _poisonTickInterval;
                    TakeDamage(_poisonDamagePerTick, _poisonDamageType, _poisonPenetration);
                }
            }
            else if (_isPoisoned)
            {
                _isPoisoned = false;
                SetTemporaryDefensePenalty(0f);
                if (TryGetComponent<EnemyChaser>(out var chaser))
                {
                    chaser.SetSpeedMultiplier(1f);
                }
            }
        }

        public void Configure(int maxHealthOverride, float defenseBonus, float magicDefenseBonus)
        {
            _currentMaxHealth = maxHealthOverride;
            _defenseBonus = defenseBonus;
            _magicDefenseBonus = magicDefenseBonus;
            ResetHealth();
        }

        public void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower, float penetration = 0f)
        {
            if (_isDead) return;

            var baseDefense = damageType == DamageStatType.MagicPower ? _magicDefenseBonus : _defenseBonus;
            var defense = baseDefense - _temporaryDefensePenalty - penetration;
            var reducedAmount = Mathf.RoundToInt(amount * (1f - Mathf.Clamp(defense, 0f, 0.9f)));
            _currentHealth = Mathf.Max(_currentHealth - reducedAmount, 0);
            SpawnDamageNumber(reducedAmount);
            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        private void SpawnDamageNumber(int amount)
        {
            if (damageNumberPrefab == null) return;

            var jitter = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f), 0f);
            var instance = SharedObjectPool.Get(damageNumberPrefab, transform.position + Vector3.up * 0.5f + jitter, Quaternion.identity);
            if (instance.TryGetComponent<DamageNumber>(out var damageNumber))
            {
                damageNumber.SetSourcePrefab(damageNumberPrefab);
                damageNumber.Setup(amount);
            }
        }

        private void Die()
        {
            _isDead = true;
            OnDied?.Invoke();

            if (experiencePickupPrefab != null)
            {
                var pickup = SharedObjectPool.Get(experiencePickupPrefab, GetDropPosition(), Quaternion.identity);
                if (pickup.TryGetComponent<ExperiencePickup>(out var experiencePickup))
                {
                    experiencePickup.SetSourcePrefab(experiencePickupPrefab);
                    experiencePickup.SetAmount(experienceReward);
                }
            }

            if (goldPickupPrefab != null && Random.value < goldDropChance)
            {
                var goldInstance = SharedObjectPool.Get(goldPickupPrefab, GetDropPosition(), Quaternion.identity);
                if (goldInstance.TryGetComponent<GoldPickup>(out var goldPickup))
                {
                    goldPickup.SetSourcePrefab(goldPickupPrefab);
                }
            }

            if (_pool != null)
            {
                _pool.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private Vector3 GetDropPosition()
        {
            var offset = Random.insideUnitCircle * dropScatterRadius;
            return transform.position + new Vector3(offset.x, offset.y, 0f);
        }
    }
}

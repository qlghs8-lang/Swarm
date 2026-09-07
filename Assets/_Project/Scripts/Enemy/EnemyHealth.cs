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

        // The spawner only calls ApplyDifficulty on pooled enemies, never on the boss, so the boss
        // used to fight with zero defence — softer than a late-game tank. These give a prefab its
        // own baseline, which the time-based difficulty ramp then adds to.
        [SerializeField] private float baseDefense;
        [SerializeField] private float baseMagicDefense;

        // Paid out once on death, on top of the usual drop chance. The boss is the only thing that
        // uses it: clearing a ten-minute run needs to be worth more than the single gold pickup it
        // was granting before.
        [SerializeField] private int goldReward;

        // Handed in by the spawner at spawn time, alongside the difficulty ramp. Enemies killed
        // later in a run drop more, which is what lets the level requirement stay linear: the
        // income curve is shaped here rather than by bending the requirement formula.
        private float _experienceMultiplier = 1f;

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
        // What is actually pushed to the defence/movement systems right now, so a frame with
        // several overlapping clouds only writes through when the value really changed.
        private float _appliedDefensePenalty;
        private float _appliedSpeedMultiplier = 1f;

        private EnemyChaser _chaser;

        public int GoldReward => goldReward;

        public event System.Action<int, int> OnHealthChanged;
        public event System.Action OnDied;

        private void Awake()
        {
            _currentMaxHealth = maxHealth;
            _currentHealth = maxHealth;
            _defenseBonus = baseDefense;
            _magicDefenseBonus = baseMagicDefense;
            TryGetComponent(out _chaser);
        }

        public void SetPool(ObjectPool pool)
        {
            _pool = pool;
        }

        public void SetExperienceMultiplier(float multiplier)
        {
            _experienceMultiplier = multiplier;
        }

        public void ApplyDifficulty(float healthMultiplier, float defenseBonus, float magicDefenseBonus)
        {
            _currentMaxHealth = Mathf.RoundToInt(maxHealth * healthMultiplier);
            _defenseBonus = baseDefense + defenseBonus;
            _magicDefenseBonus = baseMagicDefense + magicDefenseBonus;
        }

        public void ResetHealth()
        {
            _currentHealth = _currentMaxHealth;
            _isDead = false;
            _temporaryDefensePenalty = 0f;
            _burnTimer = 0f;
            _isPoisoned = false;
            _poisonedThisFrame = false;
            _appliedDefensePenalty = 0f;
            _appliedSpeedMultiplier = 1f;
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

            // Damage values and debuff values follow the same rule: the last cloud to report this
            // frame wins. Refreshing here rather than only on first entry is what keeps them
            // consistent -- a second, stronger cloud used to overwrite the damage fields while the
            // defence penalty and the slow stayed at the first cloud's weaker values.
            RefreshPoisonDebuffs();

            if (_isPoisoned) return;

            _isPoisoned = true;
            _poisonTickTimer = tickInterval;
        }

        private void RefreshPoisonDebuffs()
        {
            if (!Mathf.Approximately(_appliedDefensePenalty, _poisonDefensePenalty))
            {
                _appliedDefensePenalty = _poisonDefensePenalty;
                SetTemporaryDefensePenalty(_poisonDefensePenalty);
            }

            if (!Mathf.Approximately(_appliedSpeedMultiplier, _poisonSpeedMultiplier))
            {
                _appliedSpeedMultiplier = _poisonSpeedMultiplier;
                if (_chaser != null) _chaser.SetSpeedMultiplier(_poisonSpeedMultiplier);
            }
        }

        private void ClearPoisonDebuffs()
        {
            _appliedDefensePenalty = 0f;
            _appliedSpeedMultiplier = 1f;
            SetTemporaryDefensePenalty(0f);
            if (_chaser != null) _chaser.SetSpeedMultiplier(1f);
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
                    ApplyDamage(_burnDamagePerTick, _burnDamageType, _burnPenetration,
                                isCritical: false, knockback: false);
                }
            }

            if (_poisonedThisFrame)
            {
                _poisonedThisFrame = false;
                _poisonTickTimer -= Time.deltaTime;

                if (_poisonTickTimer <= 0f)
                {
                    _poisonTickTimer = _poisonTickInterval;
                    ApplyDamage(_poisonDamagePerTick, _poisonDamageType, _poisonPenetration,
                                isCritical: false, knockback: false);
                }
            }
            else if (_isPoisoned)
            {
                _isPoisoned = false;
                ClearPoisonDebuffs();
            }
        }

        public void Configure(int maxHealthOverride, float defenseBonus, float magicDefenseBonus)
        {
            _currentMaxHealth = maxHealthOverride;
            _defenseBonus = baseDefense + defenseBonus;
            _magicDefenseBonus = baseMagicDefense + magicDefenseBonus;
            ResetHealth();
        }

        public void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower,
                               float penetration = 0f, bool isCritical = false, float knockbackScale = 0f)
        {
            ApplyDamage(amount, damageType, penetration, isCritical, knockback: true, knockbackScale);
        }

        // Burn and poison ticks route here with knockback off. A damage-over-time effect firing
        // every tick would keep the enemy permanently airborne and permanently unable to steer,
        // which reads as a stun, not a hit.
        private void ApplyDamage(int amount, DamageStatType damageType, float penetration,
                                 bool isCritical, bool knockback, float knockbackScale = 1f)
        {
            if (_isDead) return;

            var baseDefense = damageType == DamageStatType.MagicPower ? _magicDefenseBonus : _defenseBonus;
            var defense = baseDefense - _temporaryDefensePenalty - penetration;
            var reducedAmount = Mathf.RoundToInt(amount * (1f - Mathf.Clamp(defense, 0f, 0.9f)));
            _currentHealth = Mathf.Max(_currentHealth - reducedAmount, 0);
            SpawnDamageNumber(reducedAmount, isCritical);
            OnHealthChanged?.Invoke(_currentHealth, _currentMaxHealth);

            if (_currentHealth <= 0)
            {
                Die();
                return;
            }

            if (knockback && knockbackScale > 0f && TryGetComponent<EnemyChaser>(out var chaser))
            {
                chaser.ApplyKnockbackFromTarget(knockbackScale);
            }
        }

        private void SpawnDamageNumber(int amount, bool isCritical)
        {
            if (damageNumberPrefab == null) return;
            // 설정에서 끈 경우. 화면에 적이 수십 마리 깔릴 때 숫자를 지우면 상황이 훨씬 잘 보인다.
            if (!Swarm.Settings.GameSettings.ShowDamageNumbers) return;

            var jitter = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f), 0f);
            var instance = SharedObjectPool.Get(damageNumberPrefab, transform.position + Vector3.up * 0.5f + jitter, Quaternion.identity);
            if (instance.TryGetComponent<DamageNumber>(out var damageNumber))
            {
                damageNumber.SetSourcePrefab(damageNumberPrefab);
                damageNumber.Setup(amount, isCritical);
            }
        }

        private void Die()
        {
            _isDead = true;

            Swarm.Game.RunStats.AddKill();

            if (goldReward > 0) Swarm.Game.GoldWallet.Add(goldReward);

            OnDied?.Invoke();

            if (experiencePickupPrefab != null)
            {
                var pickup = SharedObjectPool.Get(experiencePickupPrefab, GetDropPosition(), Quaternion.identity);
                if (pickup.TryGetComponent<ExperiencePickup>(out var experiencePickup))
                {
                    experiencePickup.SetSourcePrefab(experiencePickupPrefab);
                    experiencePickup.SetAmount(Mathf.RoundToInt(experienceReward * _experienceMultiplier));
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

using Swarm.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ArcherToxicRollWeapon : LevelableWeapon, IActiveRoll
    {
        [SerializeField] private float rollDistance = 3f;
        [SerializeField] private float rollDuration = 0.2f;
        [SerializeField] private float baseCooldown = 4f;

        [SerializeField] private Sprite[] gasCloudFrames;
        [SerializeField] private Color gasCloudColor = new(1f, 1f, 1f, 0.55f);
        [SerializeField] private float gasCloudRadius = 1.5f;
        [SerializeField] private float gasCloudDuration = 3f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private int damagePerTick = 5;
        [SerializeField] private float defensePenalty = 0.2f;
        [SerializeField] private float speedMultiplier = 0.6f;
        [SerializeField] private float debuffPerLevel = 0.03f;

        private float _timer;
        private bool _isRolling;
        private float _rollElapsed;
        private Vector2 _rollStart;
        private Vector2 _rollTarget;
        private PlayerStats _stats;
        private PlayerController _controller;
        private PlayerHealth _health;
        private Rigidbody2D _rigidbody;

        public float CooldownProgress01 { get; private set; } = 1f;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (_isRolling) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);

            if (_timer < effectiveCooldown) _timer += Time.deltaTime;
            CooldownProgress01 = Mathf.Clamp01(_timer / effectiveCooldown);

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryRoll();
            }
        }

        public void TryRoll()
        {
            if (_isRolling || Level <= 0) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);
            if (_timer < effectiveCooldown) return;

            _timer = 0f;
            StartRoll();
        }

        private void FixedUpdate()
        {
            if (!_isRolling) return;

            _rollElapsed += Time.fixedDeltaTime;
            var t = Mathf.Clamp01(_rollElapsed / rollDuration);
            _rigidbody.MovePosition(Vector2.Lerp(_rollStart, _rollTarget, t));

            if (t >= 1f)
            {
                _isRolling = false;

                if (_controller != null && (_health == null || !_health.IsDead))
                {
                    _controller.enabled = true;
                }
            }
        }

        private void StartRoll()
        {
            var direction = _controller != null ? _controller.FacingDirection : Vector2.right;

            _rollStart = _rigidbody.position;
            _rollTarget = _rollStart + direction * rollDistance;
            _rollElapsed = 0f;
            _isRolling = true;

            if (_controller != null) _controller.enabled = false;
            if (_health != null) _health.SetInvincible(rollDuration);

            SpawnGasCloud(_rollStart);
        }

        private void SpawnGasCloud(Vector2 position)
        {
            var levelBonus = Mathf.Max(0, Level - 1) * debuffPerLevel;
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.GetDamageMultiplier() : 1f);
            var damage = Mathf.RoundToInt(damagePerTick * damageMultiplier);
            var radius = gasCloudRadius * (1f + (_stats != null ? _stats.AreaSizeBonus : 0f));
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            var cloudObject = new GameObject("PoisonGasCloud (Temp)");
            cloudObject.transform.position = position;

            var spriteRenderer = cloudObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = gasCloudColor;
            spriteRenderer.sortingOrder = 1; // above the enemy sprite (sortingOrder 0) so the monster reads as standing "inside" the semi-transparent gas

            var cloud = cloudObject.AddComponent<PoisonGasCloud>();
            cloud.Configure(radius, gasCloudDuration, tickInterval, damage,
                defensePenalty + levelBonus, Mathf.Max(0.1f, speedMultiplier - levelBonus), penetration, gasCloudFrames);
        }
    }
}

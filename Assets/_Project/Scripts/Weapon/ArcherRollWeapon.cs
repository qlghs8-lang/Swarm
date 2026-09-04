using Swarm.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ArcherRollWeapon : LevelableWeapon, IActiveRoll
    {
        [SerializeField] private float rollDistance = 3f;
        [SerializeField] private float rollDuration = 0.2f;
        [SerializeField] private float baseCooldown = 4f;

        private float _timer;
        private bool _isRolling;
        private float _rollElapsed;
        private Vector2 _rollStart;
        private Vector2 _rollTarget;
        private PlayerStats _stats;
        private PlayerController _controller;
        private PlayerHealth _health;
        private Rigidbody2D _rigidbody;
        private Animator _animator;

        public float CooldownProgress01 { get; private set; } = 1f;
        public float CooldownRemaining { get; private set; }

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();
            _rigidbody = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (_isRolling) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);

            if (_timer < effectiveCooldown) _timer += Time.deltaTime;
            CooldownProgress01 = Mathf.Clamp01(_timer / effectiveCooldown);
            CooldownRemaining = Mathf.Max(0f, effectiveCooldown - _timer);

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
            if (_animator != null) _animator.SetTrigger("Roll");
        }
    }
}

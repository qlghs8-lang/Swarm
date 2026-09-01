using Swarm.Arena;
using Swarm.UI;
using UnityEngine;

namespace Swarm.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private VirtualJoystick joystick;

        // Shoulder force applied to enemies the player is pressed against, but only while actually
        // moving — standing still should not part the crowd. Paired with EnemyChaser's
        // acceleration, this is what turns "walled in until you kill your way out" into "spend
        // health, shove through, escape".
        [SerializeField] private float crowdPushForce = 40f;

        // The player's collider radius, so the sprite stops at the wall rather than half inside it.
        private const float BoundaryInset = 0.5f;

        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private PlayerInputActions _inputActions;
        private PlayerStats _stats;
        private Vector2 _moveInput;

        public Vector2 FacingDirection { get; private set; } = Vector2.right;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<PlayerStats>();
            _inputActions = new PlayerInputActions();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        private void OnDestroy()
        {
            _inputActions.Dispose();
        }

        private void Update()
        {
            var keyboardInput = _inputActions.Player.Move.ReadValue<Vector2>();
            var joystickInput = joystick != null ? joystick.Direction : Vector2.zero;
            _moveInput = joystickInput != Vector2.zero ? joystickInput : keyboardInput;

            if (_moveInput != Vector2.zero)
            {
                FacingDirection = _moveInput.normalized;
            }

            if (_spriteRenderer != null && _moveInput.x != 0f)
            {
                _spriteRenderer.flipX = _moveInput.x < 0f;
            }

            if (_animator != null)
            {
                _animator.SetBool(IsMovingParam, _moveInput != Vector2.zero);
            }
        }

        // Velocity-driven for the same reason as EnemyChaser: MovePosition pinned the player onto an
        // exact path every step, so the crowd read as an immovable wall instead of something you can
        // shove a way through. With velocity the player's contacts push enemies aside, and the
        // player's Rigidbody2D mass controls how hard.
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_moveInput == Vector2.zero) return;
            if (!collision.collider.CompareTag("Enemy")) return;

            var body = collision.rigidbody;
            if (body == null) return;

            var away = body.position - _rigidbody.position;
            if (away.sqrMagnitude < 0.0001f) return;

            body.AddForce(away.normalized * crowdPushForce, ForceMode2D.Force);
        }

        private void FixedUpdate()
        {
            var effectiveSpeed = moveSpeed * (1f + (_stats != null ? _stats.MoveSpeedBonus : 0f));
            _rigidbody.linearVelocity = _moveInput.normalized * effectiveSpeed;

            // A hard clamp rather than a wall collider: a collider would let the crowd's push
            // force squeeze the player through the boundary, and it would fight the shove-through
            // mechanic every time the player is pinned against the edge.
            var clamped = ArenaBounds.Clamp(_rigidbody.position, BoundaryInset);
            if (clamped != _rigidbody.position)
            {
                _rigidbody.position = clamped;
                // Kill the outward component so the player slides along the wall instead of
                // stalling against it while the input still points outward.
                var outward = clamped.normalized;
                var velocity = _rigidbody.linearVelocity;
                var into = Vector2.Dot(velocity, outward);
                if (into > 0f) _rigidbody.linearVelocity = velocity - outward * into;
            }
        }
    }
}

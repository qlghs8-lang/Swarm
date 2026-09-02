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
        // moving — standing still should not part the crowd. Kept deliberately small: at 40 the
        // player parted the crowd just by walking into it, which made escape free and turned the
        // horde into scenery. The way out is meant to be fought for — enemies shove each other
        // aside (EnemyChaser.crowdPushForce) and a hit knocks one back for a beat
        // (EnemyChaser.ApplyKnockback), and those seams are what the player leaves through.
        [SerializeField] private float crowdPushForce = 12f;

        // How much speed each enemy pressed against the player costs. Velocity is assigned outright
        // every step, so the contact solver can never slow the player down on its own — without
        // this, wading into a hundred bodies is exactly as fast as walking across empty grass.
        // This is the resistance; crowdPushForce only decides how the bodies get out of the way.
        [SerializeField, Range(0f, 0.4f)] private float crowdSlowPerContact = 0.11f;

        // Floor on that slowdown. Being surrounded should be a fight, not a full stop: at zero the
        // player would be pinned in place with no way to spend health and shove out.
        [SerializeField, Range(0.1f, 1f)] private float minCrowdSpeedMultiplier = 0.45f;

        // The player's collider radius, so the sprite stops at the wall rather than half inside it.
        // Tracks the body collider, which was shrunk from 0.5 to 0.25 to sit on the torso.
        private const float BoundaryInset = 0.25f;

        private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private PlayerInputActions _inputActions;
        private PlayerStats _stats;
        private Vector2 _moveInput;
        private ContactFilter2D _enemyContactFilter;
        // Sized past any plausible front line; overflow just means the slowdown is already floored.
        private readonly Collider2D[] _contactBuffer = new Collider2D[16];

        public Vector2 FacingDirection { get; private set; } = Vector2.right;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<PlayerStats>();
            _inputActions = new PlayerInputActions();

            // Non-trigger only, so the hurtbox this rigidbody also owns is not counted as a contact.
            _enemyContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask("Enemy"),
                useTriggers = false,
            };
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

        /// <summary>
        /// Read live from the solver rather than accumulated in OnCollisionStay2D, which fires after
        /// FixedUpdate and would leave the slowdown one physics step behind the crowd.
        /// </summary>
        private float GetCrowdSpeedMultiplier()
        {
            if (crowdSlowPerContact <= 0f) return 1f;

            var contacts = _rigidbody.GetContacts(_enemyContactFilter, _contactBuffer);
            if (contacts <= 0) return 1f;

            return Mathf.Max(1f - contacts * crowdSlowPerContact, minCrowdSpeedMultiplier);
        }

        private void FixedUpdate()
        {
            var effectiveSpeed = moveSpeed * (1f + (_stats != null ? _stats.MoveSpeedBonus : 0f));
            _rigidbody.linearVelocity = _moveInput.normalized * (effectiveSpeed * GetCrowdSpeedMultiplier());

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

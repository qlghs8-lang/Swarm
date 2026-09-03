using UnityEngine;

namespace Swarm.Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyChaser : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;

        // How fast an enemy can change its velocity. This is what makes a crowd pushable: with the
        // velocity assigned outright every physics step, any shove the player landed was erased
        // 0.02s later and the wall regenerated instantly — impossible to escape once 200 deep.
        // Accelerating instead lets a shoved enemy keep drifting away for a moment, which is the
        // gap the player leaves through.
        [SerializeField] private float acceleration = 25f;

        // Shoulder force this enemy applies to other enemies it is pressed against. This is what
        // makes the crowd behave like jelly instead of a rigid block: a body shoved at the front
        // passes the shove back through the pack, so a gap opens where the player broke in and
        // closes again behind. Moderate for the ordinary ones — high enough that the wall gives,
        // low enough that it does not dissolve into soup and stop being a wall at all. The fast
        // enemy overrides it much higher in its prefab, because "overtakes the pack and reaches
        // you first" is its whole identity.
        [SerializeField] private float crowdPushForce = 14f;

        // Knockback window. On a hit the enemy stops steering and carries an outward velocity for
        // a beat, which is where the gap the player slips through actually comes from — escape
        // is paid for by fighting, not by walking into the crowd. ~110ms matches the window the
        // genre uses: long enough to open a seam, short enough that the wall reforms right after.
        [SerializeField] private float knockbackDuration = 0.11f;
        [SerializeField] private float knockbackSpeed = 6f;

        // Knockback is divided by the body's own mass, so one value covers every enemy: the basic
        // one (mass 1) gets the full 6, the tank (4) barely rocks, the boss (50) shrugs it off.
        private const float KnockbackReferenceMass = 1f;

        // How much of that shove is aimed sideways rather than straight ahead. Pushing a blocker
        // directly away is nearly useless here: away-from-me on a head-on contact means
        // toward-the-player, which just moves the wall forward with the pusher still behind it.
        // Shoving the blocker aside is what opens the gap to slip through.
        [SerializeField] private float lateralPushBias = 2.5f;

        // Every enemy chasing the exact same point at the exact same speed is what makes a crowd
        // read as one object: they queue into clean lines, and when something shoves the line it
        // translates rigidly because all eight respond identically. A few percent of spread breaks
        // both — the formation is ragged to begin with, and a push buckles it instead of sliding it.
        [SerializeField, Range(0f, 0.5f)] private float speedVariance = 0.14f;
        [SerializeField, Range(0f, 1f)] private float accelerationVariance = 0.3f;

        /// <summary>Degrees each enemy's aim is offset from dead-on. Small enough that they still
        /// converge, large enough that they do not stack into a single file.</summary>
        [SerializeField] private float aimJitterDegrees = 7f;

        // Mass is what decides how far a given impulse moves a body, so identical masses are the
        // most direct reason a shoved line moves as one piece. Spreading it makes the same shove
        // displace each enemy differently and the line comes apart.
        [SerializeField, Range(0f, 0.5f)] private float massVariance = 0.2f;

        private Rigidbody2D _rigidbody;
        private Transform _target;
        private float _speedMultiplier = 1f;
        private float _stunTimer;

        // A timed slow, kept separate from _speedMultiplier (which the poison owns and clears back
        // to 1 when it expires). Two systems sharing one multiplier means whichever ends first
        // wipes the other; multiplying two independent tracks lets them stack and expire cleanly.
        private float _slowMultiplier = 1f;
        private float _slowTimer;
        // Which way this enemy shoves a blocker it hits dead-on. Fixed per instance so it commits
        // to one side instead of jittering left-right against the same target every step.
        private float _shoveSide;
        private float _speedScale = 1f;
        private float _accelerationScale = 1f;
        private float _aimOffsetRadians;
        private float _baseMass;
        private float _knockbackTimer;

        // Counts how many systems are currently holding this enemy in place (the boss's patterns
        // planting it for a wind-up, for example). A counter rather than a flag so two overlapping
        // holds cannot have the first one to finish release the enemy out from under the second.
        private int _movementHolds;
        // The direction this enemy is steering, cached from FixedUpdate. The shove below used to
        // read the live velocity instead, which fed back on itself: a push turned the velocity,
        // the turned velocity aimed the next push somewhere else, and in a tight clump every body
        // ended up vibrating left-right against its neighbours. Steering intent does not wobble.
        private Vector2 _steerHeading;

        // Present only on enemies that shrug off crowd control (the boss). Cached once: this is
        // read on every hit, and every hit is also a TryGetComponent from EnemyHealth already.
        private EnemyStatusImmunity _statusImmunity;

        // Every collider on this enemy, with the layers each one was authored to ignore. A held
        // enemy adds its own layer to that list so the pack slides through it, and gets its
        // original masks back on release rather than an assumed empty one.
        private Collider2D[] _colliders;
        private int[] _colliderExcludeLayers;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            TryGetComponent(out _statusImmunity);
            _baseMass = _rigidbody.mass;
            _shoveSide = (GetInstanceID() & 1) == 0 ? 1f : -1f;

            _colliders = GetComponents<Collider2D>();
            _colliderExcludeLayers = new int[_colliders.Length];
            for (var i = 0; i < _colliders.Length; i++)
            {
                _colliderExcludeLayers[i] = _colliders[i].excludeLayers;
            }
        }

        private void OnEnable()
        {
            _speedMultiplier = 1f;
            _stunTimer = 0f;
            _slowMultiplier = 1f;
            _slowTimer = 0f;
            _knockbackTimer = 0f;
            _steerHeading = Vector2.zero;
            _movementHolds = 0;
            SetCrowdPassThrough(false);

            // Rolled per spawn rather than per prefab, so a pooled body that comes back is not the
            // same individual it was last time.
            _speedScale = 1f + Random.Range(-speedVariance, speedVariance);
            _accelerationScale = 1f + Random.Range(-accelerationVariance, accelerationVariance);
            _aimOffsetRadians = Random.Range(-aimJitterDegrees, aimJitterDegrees) * Mathf.Deg2Rad;
            _rigidbody.mass = _baseMass * (1f + Random.Range(-massVariance, massVariance));
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = multiplier;
        }

        /// <summary>
        /// Plants this enemy where it stands until the matching <see cref="ReleaseMovement"/>.
        /// Unlike a stun this is not crowd control -- it is the enemy's own behaviour holding
        /// still, so status immunity does not apply and nothing cuts it short.
        /// </summary>
        public void HoldMovement()
        {
            _movementHolds++;
            if (_movementHolds == 1) SetCrowdPassThrough(true);
        }

        /// <summary>Releases one hold taken by <see cref="HoldMovement"/>.</summary>
        public void ReleaseMovement()
        {
            if (_movementHolds == 0) return;

            _movementHolds--;
            if (_movementHolds == 0) SetCrowdPassThrough(false);
        }

        /// <summary>
        /// While held, this enemy stops colliding with its own kind. Standing still in the middle
        /// of the pack otherwise means a hundred bodies leaning on one spot: the boss planted for a
        /// wind-up was slowly bulldozed off its own telegraph by the crowd it had walked into, and
        /// the crowd itself jammed against it. Letting them slide through keeps the boss on the
        /// circle it drew and keeps the pack flowing. The player's layer is untouched, so the body
        /// is still solid to whoever is dodging it, and the hurtbox trigger still lands its damage.
        /// </summary>
        private void SetCrowdPassThrough(bool enabled)
        {
            if (_colliders == null) return;

            var ownLayer = 1 << gameObject.layer;
            for (var i = 0; i < _colliders.Length; i++)
            {
                var collider = _colliders[i];
                if (collider == null) continue;

                var mask = enabled ? _colliderExcludeLayers[i] | ownLayer : _colliderExcludeLayers[i];
                collider.excludeLayers = mask;
            }
        }

        /// <summary>Stops this enemy dead for the duration. Hard control: the longer of the
        /// running stun and the new one wins, so overlapping procs do not cut each other short.
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (duration <= 0f) return;
            if (_statusImmunity != null && _statusImmunity.ImmuneToStun) return;

            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>Scales this enemy's move speed for the duration. Soft control, so the strongest
        /// slow in effect wins and refreshing extends it rather than stacking multiplicatively --
        /// otherwise a chaining weapon that re-applies every second grinds a crowd to a halt.
        /// </summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (duration <= 0f || multiplier >= 1f) return;
            if (_statusImmunity != null && _statusImmunity.ImmuneToSlow) return;

            _slowMultiplier = _slowTimer > 0f ? Mathf.Min(_slowMultiplier, multiplier) : multiplier;
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        /// <summary>
        /// Reverses this enemy's velocity outward for the knockback window. Steering is suspended
        /// for the duration, so the solver keeps the outward motion and the body shoves whoever is
        /// behind it — the shove propagating through the pack is the point, not the single enemy
        /// moving back.
        /// </summary>
        public void ApplyKnockback(Vector2 direction, float scale = 1f)
        {
            if (_statusImmunity != null && _statusImmunity.ImmuneToKnockback) return;
            if (knockbackDuration <= 0f || knockbackSpeed <= 0f) return;
            if (direction.sqrMagnitude < 0.0001f) return;

            var massScale = _rigidbody.mass > 0.01f ? KnockbackReferenceMass / _rigidbody.mass : 1f;
            _rigidbody.linearVelocity = direction.normalized * (knockbackSpeed * scale * massScale);
            _knockbackTimer = knockbackDuration;
        }

        /// <summary>Knocks this enemy straight away from whatever it is chasing.</summary>
        public void ApplyKnockbackFromTarget(float scale = 1f)
        {
            if (_target == null) return;

            ApplyKnockback(_rigidbody.position - (Vector2)_target.position, scale);
        }

        // Movement is velocity-driven rather than Rigidbody2D.MovePosition. MovePosition forces the
        // body onto an exact position every physics step, which overwrites whatever the contact
        // solver just did — so enemies could not be shoved aside by the player or by each other and
        // the whole crowd travelled as one rigid block, a faster enemy unable to work its way past a
        // slower one. Setting velocity leaves the solver's collision impulses intact, so bodies
        // displace each other and slide apart while still steering toward the player.
        private static Vector2 Rotate(Vector2 vector, float radians)
        {
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (crowdPushForce <= 0f || _stunTimer > 0f) return;

            var body = collision.rigidbody;
            if (body == null || !collision.collider.CompareTag("Enemy")) return;

            if (_steerHeading == Vector2.zero) return;

            var heading = _steerHeading;
            var away = body.position - _rigidbody.position;
            if (away.sqrMagnitude < 0.0001f) return;
            away.Normalize();

            // Only shove what is actually in the way. Pushing a neighbour that is already behind
            // does nothing for this enemy and just adds noise to a packed crowd.
            if (Vector2.Dot(away, heading) <= 0f) return;

            // The part of "away" that is across the direction of travel. Near zero on a dead-on
            // contact, which is exactly the case that needs a side to be picked for it.
            var lateral = away - heading * Vector2.Dot(away, heading);
            if (lateral.sqrMagnitude < 0.01f)
            {
                lateral = new Vector2(-heading.y, heading.x) * _shoveSide;
            }

            var push = (away + lateral.normalized * lateralPushBias).normalized;
            body.AddForce(push * crowdPushForce, ForceMode2D.Force);
        }

        private void FixedUpdate()
        {
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.fixedDeltaTime;
                if (_slowTimer <= 0f) _slowMultiplier = 1f;
            }

            if (_movementHolds > 0)
            {
                // Steering intent is cleared as well, so the crowd shove in OnCollisionStay2D goes
                // quiet too: a boss planted for a wind-up should not still be pushing the pack
                // along the heading it had when it stopped.
                _steerHeading = Vector2.zero;
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.fixedDeltaTime;
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.fixedDeltaTime;

                // Deliberately leaves the velocity alone. Re-steering here, even partially, would
                // cancel the outward motion before the contact solver has carried it into the
                // bodies behind, and the seam would never open.
                return;
            }

            if (_target == null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            var direction = (Vector2)_target.position - _rigidbody.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            var aim = Rotate(direction.normalized, _aimOffsetRadians);
            _steerHeading = aim;
            var desired = aim * (moveSpeed * _speedMultiplier * _slowMultiplier * _speedScale);
            _rigidbody.linearVelocity = Vector2.MoveTowards(
                _rigidbody.linearVelocity, desired,
                acceleration * _accelerationScale * Time.fixedDeltaTime);
        }
    }
}

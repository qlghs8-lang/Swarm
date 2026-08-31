using UnityEngine;

namespace Swarm.Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyChaser : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;

        private Rigidbody2D _rigidbody;
        private Transform _target;
        private float _speedMultiplier = 1f;
        private float _freezeTimer;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            _speedMultiplier = 1f;
            _freezeTimer = 0f;
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

        public void ApplyFreeze(float duration)
        {
            _freezeTimer = Mathf.Max(_freezeTimer, duration);
        }

        // Movement is velocity-driven rather than Rigidbody2D.MovePosition. MovePosition forces the
        // body onto an exact position every physics step, which overwrites whatever the contact
        // solver just did — so enemies could not be shoved aside by the player or by each other and
        // the whole crowd travelled as one rigid block, a faster enemy unable to work its way past a
        // slower one. Setting velocity leaves the solver's collision impulses intact, so bodies
        // displace each other and slide apart while still steering toward the player.
        private void FixedUpdate()
        {
            if (_freezeTimer > 0f)
            {
                _freezeTimer -= Time.fixedDeltaTime;
                _rigidbody.linearVelocity = Vector2.zero;
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

            _rigidbody.linearVelocity = direction.normalized * (moveSpeed * _speedMultiplier);
        }
    }
}

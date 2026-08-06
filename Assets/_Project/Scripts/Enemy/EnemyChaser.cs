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

        private void FixedUpdate()
        {
            if (_freezeTimer > 0f)
            {
                _freezeTimer -= Time.fixedDeltaTime;
                return;
            }

            if (_target == null) return;

            var direction = (Vector2)_target.position - _rigidbody.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            _rigidbody.MovePosition(_rigidbody.position + direction.normalized * (moveSpeed * _speedMultiplier * Time.fixedDeltaTime));
        }
    }
}

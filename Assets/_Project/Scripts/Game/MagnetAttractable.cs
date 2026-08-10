using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class MagnetAttractable : MonoBehaviour
    {
        [SerializeField] private float attractSpeed = 10f;

        private Rigidbody2D _rigidbody;
        private Transform _player;
        private Collider2D _playerCollider;
        private PlayerStats _playerStats;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _player = player.transform;
                player.TryGetComponent(out _playerStats);
                player.TryGetComponent(out _playerCollider);
            }
        }

        private void FixedUpdate()
        {
            if (_player == null || _playerStats == null || _playerStats.MagnetRadius <= 0f) return;

            // Target the player's actual collider center, not the transform origin — the
            // collider can be offset from it (e.g. raised to chest height for hit detection),
            // and chasing the transform origin instead leaves a permanent gap it can never close.
            Vector2 targetPosition = _playerCollider != null ? _playerCollider.bounds.center : _player.position;

            var distance = Vector2.Distance(_rigidbody.position, targetPosition);
            if (distance > _playerStats.MagnetRadius) return;

            var newPosition = Vector2.MoveTowards(_rigidbody.position, targetPosition, attractSpeed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(newPosition);
        }
    }
}

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
        private PlayerStats _playerStats;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _player = player.transform;
                player.TryGetComponent(out _playerStats);
            }
        }

        private void FixedUpdate()
        {
            if (_player == null || _playerStats == null || _playerStats.MagnetRadius <= 0f) return;

            var distance = Vector2.Distance(_rigidbody.position, _player.position);
            if (distance > _playerStats.MagnetRadius) return;

            var newPosition = Vector2.MoveTowards(_rigidbody.position, _player.position, attractSpeed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(newPosition);
        }
    }
}

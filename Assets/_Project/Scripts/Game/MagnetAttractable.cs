using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class MagnetAttractable : MonoBehaviour
    {
        [SerializeField] private float attractSpeed = 10f;
        [SerializeField] private float forcedSpeedMultiplier = 2.5f;

        // Every loose pickup currently on the ground. A magnet drop has to reach all of them at
        // once, and FindObjectsByType at that moment would walk the whole scene — a hundred pots,
        // several hundred wall stones and every enemy — to find the handful that can be attracted.
        private static readonly List<MagnetAttractable> Active = new();

        private Rigidbody2D _rigidbody;
        private bool _forced;
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

        private void OnEnable()
        {
            // Pickups are pooled, so a reused instance must forget the sweep that collected the
            // last one or it would fly at the player the moment it is dropped.
            _forced = false;
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>Pulls every pickup on the ground to the player, ignoring magnet radius.
        /// Raised by <see cref="Swarm.Game.MagnetPickup"/>.</summary>
        public static void AttractAll()
        {
            for (var i = 0; i < Active.Count; i++)
            {
                Active[i]._forced = true;
            }
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            if (!_forced && (_playerStats == null || _playerStats.MagnetRadius <= 0f)) return;

            // Target the player's actual collider center, not the transform origin — the
            // collider can be offset from it (e.g. raised to chest height for hit detection),
            // and chasing the transform origin instead leaves a permanent gap it can never close.
            Vector2 targetPosition = _playerCollider != null ? _playerCollider.bounds.center : _player.position;

            if (!_forced)
            {
                var distance = Vector2.Distance(_rigidbody.position, targetPosition);
                if (distance > _playerStats.MagnetRadius) return;
            }

            // A swept pickup can start half a screen away, so it moves faster than the ordinary
            // radius pull — at the normal speed the far ones would still be in transit long after
            // the sweep stopped reading as one event.
            var speed = _forced ? attractSpeed * forcedSpeedMultiplier : attractSpeed;
            var newPosition = Vector2.MoveTowards(_rigidbody.position, targetPosition, speed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(newPosition);
        }
    }
}

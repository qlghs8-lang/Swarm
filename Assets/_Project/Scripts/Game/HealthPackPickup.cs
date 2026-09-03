using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// A pot drop that heals a share of the player's maximum health.
    ///
    /// A percentage rather than a flat number so it keeps meaning something after max-health
    /// passives have tripled the bar — a flat 20 would be a third of the starting pool and a
    /// rounding error by the tenth minute.
    ///
    /// It is not picked up at full health. The alternative is a pack that vanishes for nothing
    /// because the player happened to walk over it while undamaged, which reads as the game
    /// stealing the find; leaving it on the ground lets them come back for it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HealthPackPickup : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 1f)] private float healPercent = 0.1f;

        private GameObject _sourcePrefab;
        private bool _collected;

        private void OnEnable()
        {
            _collected = false;
        }

        public void SetSourcePrefab(GameObject prefab)
        {
            _sourcePrefab = prefab;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (_collected) return;
            if (!other.CompareTag("Player")) return;
            if (!other.TryGetComponent<PlayerHealth>(out var health)) return;
            if (health.IsDead || health.CurrentHealth >= health.MaxHealth) return;

            _collected = true;

            // At least 1: a 10% heal on a small bar must never round down to nothing.
            health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * healPercent)));
            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

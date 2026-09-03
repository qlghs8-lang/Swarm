using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// A pot drop that sweeps every loose pickup in the arena into the player at once.
    ///
    /// The pull is unconditional rather than a temporary radius boost: the moment of value is
    /// seeing the whole field of orbs the player fought over come to them, and a timed buff would
    /// spend most of its duration on ground they have already cleared.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MagnetPickup : MonoBehaviour
    {
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

            _collected = true;

            MagnetAttractable.AttractAll();
            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

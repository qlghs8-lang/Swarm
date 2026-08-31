using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class GoldPickup : MonoBehaviour
    {
        [SerializeField] private int amount = 1;

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

            GoldWallet.Add(amount);
            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class ExperiencePickup : MonoBehaviour
    {
        [SerializeField] private int amount = 5;

        private GameObject _sourcePrefab;
        private bool _collected;

        private void OnEnable()
        {
            _collected = false;
        }

        public void SetAmount(int value)
        {
            amount = value;
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

            if (other.TryGetComponent<PlayerExperience>(out var experience))
            {
                experience.AddExperience(amount);
            }

            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

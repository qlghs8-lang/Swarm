using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class ExperiencePickup : MonoBehaviour
    {
        [SerializeField] private int amount = 5;

        public void SetAmount(int value)
        {
            amount = value;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<PlayerExperience>(out var experience))
            {
                experience.AddExperience(amount);
            }

            Destroy(gameObject);
        }
    }
}

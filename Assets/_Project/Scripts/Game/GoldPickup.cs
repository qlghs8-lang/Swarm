using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class GoldPickup : MonoBehaviour
    {
        [SerializeField] private int amount = 1;

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            GoldWallet.Add(amount);
            Destroy(gameObject);
        }
    }
}

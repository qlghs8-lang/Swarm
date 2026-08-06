using Swarm.Player;
using UnityEngine;

namespace Swarm.Enemy
{
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField] private int damage = 10;

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Player")) return;

            if (collision.collider.TryGetComponent<PlayerHealth>(out var health))
            {
                health.TakeDamage(damage);
            }
        }
    }
}

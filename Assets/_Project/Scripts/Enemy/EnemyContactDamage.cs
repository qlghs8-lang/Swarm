using Swarm.Player;
using UnityEngine;

namespace Swarm.Enemy
{
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField] private int damage = 10;

        // Damage keys off PlayerHurtbox's trigger, not the player's body collider. The body
        // collider is oversized on purpose (crowd shoving, arena clamp), so using it here made
        // hits land outside the drawn character. PlayerHealth's invincibility window already
        // guards against Enter and Stay both firing in the same physics step.
        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);

        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerHurtbox>(out var hurtbox)) return;

            var health = hurtbox.Health;
            if (health == null) return;

            health.TakeDamage(damage);
        }
    }
}

using UnityEngine;

namespace Swarm.Player
{
    /// <summary>
    /// Splits the player's damage-receiving area away from the physics body.
    ///
    /// The body CircleCollider2D is non-trigger, so it physically holds enemies off at
    /// bodyRadius + enemyRadius. A hurtbox smaller than the body can therefore never be
    /// touched at all - that is total invincibility, not a fairer hitbox. So the body was
    /// shrunk to the torso (0.5 -> 0.25) and this trigger sits a hair outside it, which is
    /// the smallest hurtbox that still registers the moment an enemy presses against the
    /// sprite. Keeping it separate from the body means the physics radius can be retuned
    /// for shoving without silently changing how much damage the player eats.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [DisallowMultipleComponent]
    public class PlayerHurtbox : MonoBehaviour
    {
        // Local units, multiplied by the transform's 0.8 scale at runtime.
        // 0.28 * 0.8 = 0.224 world radius: the torso, and just past the 0.25 body collider
        // so contact always lands. Anything below the body radius is unreachable - see
        // EnsureReachable, which clamps rather than letting the player go invincible.
        [SerializeField] private float radius = 0.28f;

        // How far past the body collider the trigger must extend to register reliably.
        private const float ReachMargin = 0.03f;

        // Chest height, same as the body collider's offset so the hurtbox sits on the torso
        // and not on the feet.
        [SerializeField] private Vector2 offset = new Vector2(0f, 0.7f);

        [SerializeField] private bool drawGizmo = true;

        private CircleCollider2D _collider;
        private CircleCollider2D _body;
        private PlayerHealth _health;

        public PlayerHealth Health => _health;
        public float Radius => radius;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _body = GetComponent<CircleCollider2D>();

            // Built at runtime so the scene keeps a single authored collider and this stays
            // a code-only change. The body collider is non-trigger, so enemies only raise
            // trigger callbacks for this one.
            _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _collider.offset = offset;

            EnsureReachable();
            _collider.radius = radius;
        }

        /// <summary>
        /// The body collider keeps enemies at arm's length, so a hurtbox inside it never gets
        /// touched. Clamp instead of shipping a player who cannot be hit.
        /// </summary>
        private void EnsureReachable()
        {
            if (_body == null) return;

            var minimum = _body.radius + ReachMargin;
            if (radius >= minimum) return;

            Debug.LogWarning(
                $"PlayerHurtbox radius {radius} sits inside the body collider ({_body.radius}), " +
                $"which would make the player unhittable. Clamped to {minimum}.", this);
            radius = minimum;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);

            // Lets the radius be tuned in the inspector while the game is running.
            if (_collider == null) return;
            EnsureReachable();
            _collider.radius = radius;
            _collider.offset = offset;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(offset, radius);
        }
    }
}

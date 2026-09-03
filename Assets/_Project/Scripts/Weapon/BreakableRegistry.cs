using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    /// <summary>
    /// The colliders of breakable scenery (pots), so weapon targeting can tell them apart from
    /// actual enemies in O(1).
    ///
    /// Breakables sit on the Enemy layer and carry the Enemy tag on purpose: every weapon in the
    /// game finds its victims the same way — an Enemy-layer overlap (or a projectile trigger),
    /// a CompareTag("Enemy"), then TryGetComponent&lt;IDamageable&gt; — so sharing the layer and
    /// the tag is what makes a pot breakable by all fourteen weapons without editing any of them.
    ///
    /// What must NOT follow from that is auto-aim treating a pot as a target: an archer that
    /// shoots the nearest "enemy" would empty its quiver into the scenery while the swarm closes
    /// in. <see cref="EnemyTargeting.FindNearest"/> and <see cref="EnemyTargeting.FindMultiple"/>
    /// therefore skip anything registered here. A per-candidate GetComponent in those loops would
    /// cost more than the query itself, which is why this is a set of colliders and not an
    /// interface check.
    /// </summary>
    public static class BreakableRegistry
    {
        private static readonly HashSet<Collider2D> Colliders = new();

        public static void Register(Collider2D collider)
        {
            if (collider != null) Colliders.Add(collider);
        }

        public static void Unregister(Collider2D collider)
        {
            if (collider != null) Colliders.Remove(collider);
        }

        public static bool Contains(Collider2D collider) => Colliders.Contains(collider);
    }
}

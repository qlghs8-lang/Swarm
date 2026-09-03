using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>
    /// Shared hit resolution for boss patterns. They only ever need the player, so the query is
    /// filtered to the Player layer instead of collecting every collider in the blast radius.
    /// </summary>
    public static class BossAttackHits
    {
        private static ContactFilter2D _playerFilter;
        private static bool _playerFilterReady;
        private static readonly List<Collider2D> HitBuffer = new();

        private static ContactFilter2D PlayerFilter
        {
            get
            {
                if (!_playerFilterReady)
                {
                    var mask = LayerMask.GetMask("Player");
                    if (mask == 0)
                    {
                        Debug.LogError("Layer 'Player' is not defined in Tags & Layers. " +
                                       "Boss attacks fall back to all layers until it is added.");
                        mask = Physics2D.AllLayers;
                    }

                    _playerFilter = new ContactFilter2D
                    {
                        useLayerMask = true,
                        layerMask = mask,
                        useTriggers = true,
                    };
                    _playerFilterReady = true;
                }

                return _playerFilter;
            }
        }

        public static void DamagePlayersInCircle(Vector2 centre, float radius, int damage) =>
            DamagePlayersInEllipse(centre, radius, 1f, damage);

        /// <summary>
        /// Damages the player inside an ellipse of <paramref name="radius"/> across and
        /// <paramref name="radius"/> * <paramref name="verticalSquash"/> tall. A pattern whose
        /// telegraph is squashed to match 3/4 perspective art has to hit the same shape it draws,
        /// or a player standing just above the boss is struck by an attack the warning said
        /// they had cleared.
        /// </summary>
        public static void DamagePlayersInEllipse(Vector2 centre, float radius, float verticalSquash,
                                                  int damage)
        {
            // The circle query is the broad phase: an ellipse never reaches past the circle it is
            // squashed out of, so anything outside this is outside the ellipse too.
            var count = Physics2D.OverlapCircle(centre, radius, PlayerFilter, HitBuffer);
            var verticalRadius = radius * Mathf.Max(0.01f, verticalSquash);

            for (var i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (!hit.CompareTag("Player")) continue;

                // Measured against the collider's nearest point rather than its centre, so the
                // player is hit when their body touches the ellipse — the same rule OverlapCircle
                // applies to the broad phase.
                var nearest = hit.ClosestPoint(centre);
                var offset = nearest - centre;
                var x = offset.x / radius;
                var y = offset.y / verticalRadius;
                if (x * x + y * y > 1f) continue;

                if (hit.TryGetComponent<PlayerHealth>(out var health))
                {
                    health.TakeDamage(damage);
                }
            }
        }
    }
}

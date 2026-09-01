using UnityEngine;

namespace Swarm.Arena
{
    /// <summary>
    /// The arena's shape, shared by everything that has to respect it: the player clamp, enemy
    /// spawning, and the builder that lays the ground and the wall.
    ///
    /// A bounded arena exists so that running in one direction stops being a permanent answer.
    /// On an unbounded map the safe play is to flee forever, which makes melee and area weapons
    /// structurally worse than ranged ones and makes "surrounded and killed" — the failure this
    /// game is built around — impossible.
    /// </summary>
    public static class ArenaBounds
    {
        /// <summary>World units from the origin to the inside face of the wall. At the player's
        /// base speed of 4 that is 17.5s from centre to edge, 35s across.</summary>
        public const float Radius = 70f;

        /// <summary>Keeps <paramref name="position"/> inside the arena, leaving
        /// <paramref name="inset"/> units of clearance from the boundary.</summary>
        public static Vector2 Clamp(Vector2 position, float inset)
        {
            var limit = Mathf.Max(0f, Radius - inset);
            var sqrDistance = position.sqrMagnitude;
            if (sqrDistance <= limit * limit) return position;

            return position * (limit / Mathf.Sqrt(sqrDistance));
        }

        public static bool Contains(Vector2 position, float inset)
        {
            var limit = Mathf.Max(0f, Radius - inset);
            return position.sqrMagnitude <= limit * limit;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    public static class EnemyTargeting
    {
        // Every enemy query used to be Physics2D.OverlapCircleAll(origin, range): no layer filter,
        // so it collected *every* collider in range (pickups, projectiles, the player) into a
        // freshly allocated array, then threw most of them away on a tag check. Filtering to the
        // Enemy layer and writing into a caller-owned list removes both the garbage and the work.
        private const string EnemyLayerName = "Enemy";

        private static ContactFilter2D _enemyFilter;
        private static bool _filterReady;

        // FindNearest fully consumes this before returning a Transform, and nothing it calls can
        // re-enter the query, so one shared buffer is safe here. FindMultiple is different: its
        // results stay alive while the caller deals damage (which can run damage-event handlers),
        // so callers pass their own list instead.
        private static readonly List<Collider2D> QueryBuffer = new();

        private static ContactFilter2D EnemyFilter
        {
            get
            {
                if (!_filterReady)
                {
                    var mask = LayerMask.GetMask(EnemyLayerName);
                    if (mask == 0)
                    {
                        // A missing layer would silently make every weapon stop finding targets,
                        // which reads as "all my damage broke" rather than "a layer is missing".
                        Debug.LogError($"Layer '{EnemyLayerName}' is not defined in Tags & Layers. " +
                                       "Enemy queries fall back to all layers until it is added.");
                        mask = Physics2D.AllLayers;
                    }

                    _enemyFilter = new ContactFilter2D
                    {
                        useLayerMask = true,
                        layerMask = mask,
                        useTriggers = true,
                    };
                    _filterReady = true;
                }

                return _enemyFilter;
            }
        }

        /// <summary>
        /// Fills <paramref name="results"/> with the enemy colliders overlapping the circle and
        /// returns how many there are. The list is cleared first and is never reallocated.
        /// </summary>
        public static int OverlapEnemies(Vector2 origin, float range, List<Collider2D> results)
        {
            return Physics2D.OverlapCircle(origin, range, EnemyFilter, results);
        }

        // Where an attack should actually aim/land on a target — the enemy's hit collider
        // center, not its transform (which sits at the feet on bottom-pivot sprites and
        // can be well below the collider once it's offset up to body height).
        public static Vector2 GetHitPoint(Transform target)
        {
            if (target == null) return Vector2.zero;

            return target.TryGetComponent<Collider2D>(out var collider)
                ? (Vector2)collider.bounds.center
                : (Vector2)target.position;
        }

        /// <summary>
        /// Whether <paramref name="hit"/> falls inside a cone of half-angle
        /// <paramref name="halfAngleDegrees"/> pointing along <paramref name="facing"/> from
        /// <paramref name="center"/>.
        ///
        /// Cone weapons used to test hit.transform.position, but enemy transforms sit at the feet
        /// while their colliders are offset up to body height (+0.75 on every enemy prefab), and
        /// facing is measured between collider centres. That mismatch tilted each enemy's measured
        /// direction downward by atan(0.75 / distance): zero when attacking straight up or down, and
        /// up to ~57 degrees when attacking sideways at point-blank range. Enemies pressed against
        /// the player dropped out of the cone entirely.
        /// </summary>
        public static bool IsInsideCone(Collider2D hit, Vector2 center, Vector2 facing, float halfAngleDegrees)
        {
            if (hit == null) return false;

            var bounds = hit.bounds;
            var toEnemy = (Vector2)bounds.center - center;
            var distance = toEnemy.magnitude;
            var enemyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);

            // Touching or overlapping the swing origin: there is no meaningful direction left to test.
            if (distance <= enemyRadius || distance <= Mathf.Epsilon) return true;

            // Widen the cone by the enemy's angular size so a body clipping the edge still counts,
            // instead of testing a single point against a hard edge.
            var toleranceDegrees = Mathf.Asin(Mathf.Clamp01(enemyRadius / distance)) * Mathf.Rad2Deg;
            return Vector2.Angle(facing, toEnemy) <= halfAngleDegrees + toleranceDegrees;
        }

        public static Transform FindNearest(Vector2 origin, float range)
        {
            var count = OverlapEnemies(origin, range, QueryBuffer);
            Transform nearest = null;
            var nearestSqrDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var hit = QueryBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy")) continue;
                // Breakable scenery shares the layer and the tag so that every weapon's damage
                // path reaches it unchanged; auto-aim must not, or a pot standing closer than the
                // swarm would soak the whole volley. See BreakableRegistry.
                if (BreakableRegistry.Contains(hit)) continue;

                var sqrDistance = ((Vector2)hit.transform.position - origin).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with up to <paramref name="maxCount"/> enemy
        /// transforms, nearest first, and returns how many were written.
        /// </summary>
        public static int FindMultiple(Vector2 origin, float range, int maxCount, List<Transform> results)
        {
            results.Clear();
            if (maxCount <= 0) return 0;

            var count = OverlapEnemies(origin, range, QueryBuffer);
            if (count == 0) return 0;

            // Partial selection sort: maxCount is typically 1-8 out of a handful of candidates,
            // so this beats sorting the whole set and needs no extra distance array.
            var limit = Mathf.Min(maxCount, count);

            for (var slot = 0; slot < limit; slot++)
            {
                var bestIndex = -1;
                var bestSqrDistance = float.MaxValue;

                for (var i = slot; i < count; i++)
                {
                    var hit = QueryBuffer[i];
                    if (hit == null || !hit.CompareTag("Enemy")) continue;
                    if (BreakableRegistry.Contains(hit)) continue;

                    var sqrDistance = ((Vector2)hit.transform.position - origin).sqrMagnitude;
                    if (sqrDistance < bestSqrDistance)
                    {
                        bestSqrDistance = sqrDistance;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0) break;

                (QueryBuffer[slot], QueryBuffer[bestIndex]) = (QueryBuffer[bestIndex], QueryBuffer[slot]);
                results.Add(QueryBuffer[slot].transform);
            }

            return results.Count;
        }
    }
}

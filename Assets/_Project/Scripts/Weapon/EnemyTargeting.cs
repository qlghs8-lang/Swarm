using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    public static class EnemyTargeting
    {
        public static Transform FindNearest(Vector2 origin, float range)
        {
            var hits = Physics2D.OverlapCircleAll(origin, range);
            Transform nearest = null;
            var nearestSqrDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                var sqrDistance = ((Vector2)hit.transform.position - origin).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        public static List<Transform> FindMultiple(Vector2 origin, float range, int maxCount)
        {
            var hits = Physics2D.OverlapCircleAll(origin, range);
            var candidates = new List<(Transform transform, float sqrDistance)>();

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;

                candidates.Add((hit.transform, ((Vector2)hit.transform.position - origin).sqrMagnitude));
            }

            candidates.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            var result = new List<Transform>();
            for (var i = 0; i < Mathf.Min(maxCount, candidates.Count); i++)
            {
                result.Add(candidates[i].transform);
            }

            return result;
        }
    }
}

using UnityEngine;

namespace Swarm.Weapon
{
    public static class ProjectileSpread
    {
        public static Vector2 GetDirection(Vector2 baseDirection, int index, int count, float spreadAngleDegrees)
        {
            if (count <= 1) return baseDirection;

            var effectiveSpread = spreadAngleDegrees > 0f ? spreadAngleDegrees : 20f;
            var offsetDegrees = effectiveSpread * ((float)index / (count - 1) - 0.5f);
            return Quaternion.Euler(0f, 0f, offsetDegrees) * baseDirection;
        }
    }
}

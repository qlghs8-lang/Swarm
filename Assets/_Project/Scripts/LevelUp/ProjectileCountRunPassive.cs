using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "ProjectileCountRunPassive", menuName = "Swarm/LevelUp/Projectile Count Run Passive")]
    public class ProjectileCountRunPassive : RunPassive
    {
        [SerializeField] private int amountPerLevel = 1;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseProjectileCount(amountPerLevel);
            }
        }
    }
}

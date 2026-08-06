using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "AreaSizeRunPassive", menuName = "Swarm/LevelUp/Area Size Run Passive")]
    public class AreaSizeRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.15f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseAreaSize(percentPerLevel);
            }
        }
    }
}

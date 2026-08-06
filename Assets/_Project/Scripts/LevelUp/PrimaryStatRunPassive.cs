using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "PrimaryStatRunPassive", menuName = "Swarm/LevelUp/Primary Stat Run Passive")]
    public class PrimaryStatRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.1f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreasePrimaryStat(percentPerLevel);
            }
        }
    }
}

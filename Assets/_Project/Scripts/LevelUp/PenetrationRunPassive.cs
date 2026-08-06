using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "PenetrationRunPassive", menuName = "Swarm/LevelUp/Penetration Run Passive")]
    public class PenetrationRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.05f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseArmorPenetration(percentPerLevel);
                stats.IncreaseMagicPenetration(percentPerLevel);
            }
        }
    }
}

using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "MaxHealthRunPassive", menuName = "Swarm/LevelUp/Max Health Run Passive")]
    public class MaxHealthRunPassive : RunPassive
    {
        [SerializeField] private int amountPerLevel = 20;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerHealth>(out var health))
            {
                health.IncreaseMaxHealth(amountPerLevel);
            }
        }
    }
}

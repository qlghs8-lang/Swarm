using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "CooldownReductionRunPassive", menuName = "Swarm/LevelUp/Cooldown Reduction Run Passive")]
    public class CooldownReductionRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.06f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseCooldownReduction(percentPerLevel);
            }
        }
    }
}

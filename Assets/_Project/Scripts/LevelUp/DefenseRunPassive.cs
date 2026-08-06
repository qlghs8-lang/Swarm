using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "DefenseRunPassive", menuName = "Swarm/LevelUp/Defense Run Passive")]
    public class DefenseRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.05f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseDefense(percentPerLevel);
            }
        }
    }
}

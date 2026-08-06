using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    [CreateAssetMenu(fileName = "MoveSpeedRunPassive", menuName = "Swarm/LevelUp/Move Speed Run Passive")]
    public class MoveSpeedRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.1f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseMoveSpeed(percentPerLevel);
            }
        }
    }
}

using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveMoveSpeedUpgrade", menuName = "Swarm/Game/Passive Move Speed Upgrade")]
    public class PassiveMoveSpeedUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.05f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseMoveSpeed(percentPerLevel * Level);
            }
        }
    }
}

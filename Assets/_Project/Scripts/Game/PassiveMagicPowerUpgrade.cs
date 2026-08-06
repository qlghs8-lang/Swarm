using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveMagicPowerUpgrade", menuName = "Swarm/Game/Passive Magic Power Upgrade")]
    public class PassiveMagicPowerUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.05f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseMagicPower(percentPerLevel * Level);
            }
        }
    }
}

using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveAttackPowerUpgrade", menuName = "Swarm/Game/Passive Attack Power Upgrade")]
    public class PassiveAttackPowerUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.05f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseAttackPower(percentPerLevel * Level);
            }
        }
    }
}

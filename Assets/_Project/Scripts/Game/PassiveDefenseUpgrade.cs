using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveDefenseUpgrade", menuName = "Swarm/Game/Passive Defense Upgrade")]
    public class PassiveDefenseUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.03f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseDefense(percentPerLevel * Level);
            }
        }
    }
}

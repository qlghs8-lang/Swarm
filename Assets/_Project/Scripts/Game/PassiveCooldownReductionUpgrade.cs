using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveCooldownReductionUpgrade", menuName = "Swarm/Game/Passive Cooldown Reduction Upgrade")]
    public class PassiveCooldownReductionUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.04f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseCooldownReduction(percentPerLevel * Level);
            }
        }
    }
}

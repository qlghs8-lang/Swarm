using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassivePenetrationUpgrade", menuName = "Swarm/Game/Passive Penetration Upgrade")]
    public class PassivePenetrationUpgrade : PassiveUpgrade
    {
        [SerializeField] private float percentPerLevel = 0.04f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseArmorPenetration(percentPerLevel * Level);
                stats.IncreaseMagicPenetration(percentPerLevel * Level);
            }
        }
    }
}

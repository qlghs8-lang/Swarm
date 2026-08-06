using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveMagnetUpgrade", menuName = "Swarm/Game/Passive Magnet Upgrade")]
    public class PassiveMagnetUpgrade : PassiveUpgrade
    {
        [SerializeField] private float amountPerLevel = 1f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseMagnetRadius(amountPerLevel * Level);
            }
        }
    }
}

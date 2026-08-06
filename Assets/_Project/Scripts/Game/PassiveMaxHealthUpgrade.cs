using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "PassiveMaxHealthUpgrade", menuName = "Swarm/Game/Passive Max Health Upgrade")]
    public class PassiveMaxHealthUpgrade : PassiveUpgrade
    {
        [SerializeField] private int amountPerLevel = 10;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            if (player.TryGetComponent<PlayerHealth>(out var health))
            {
                health.IncreaseMaxHealth(amountPerLevel * Level);
            }
        }
    }
}

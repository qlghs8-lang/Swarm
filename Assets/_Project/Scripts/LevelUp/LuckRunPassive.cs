using Swarm.Player;
using UnityEngine;

namespace Swarm.LevelUp
{
    // 행운 스탯을 소비하는 시스템이 아직 없음 — 이번에는 스탯만 쌓아두고, 드랍률 등 실제 효과는 나중에 연결한다.
    [CreateAssetMenu(fileName = "LuckRunPassive", menuName = "Swarm/LevelUp/Luck Run Passive")]
    public class LuckRunPassive : RunPassive
    {
        [SerializeField] private float percentPerLevel = 0.1f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.IncreaseLuck(percentPerLevel);
            }
        }
    }
}

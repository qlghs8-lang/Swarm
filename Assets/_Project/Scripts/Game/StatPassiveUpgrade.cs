using Swarm.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Swarm.Game
{
    /// <summary>
    /// A permanent, gold-purchased upgrade that raises one stat. Replaces the eight near-identical
    /// subclasses. The purchased level is stored per asset id by the base class, so the ids in the
    /// existing .asset files must not change — they are the PlayerPrefs keys holding player
    /// progress.
    /// </summary>
    [CreateAssetMenu(fileName = "StatPassiveUpgrade", menuName = "Swarm/Game/Stat Passive Upgrade")]
    public class StatPassiveUpgrade : PassiveUpgrade
    {
        [SerializeField] private StatType stat;

        [FormerlySerializedAs("percentPerLevel")]
        [SerializeField] private float amountPerLevel = 0.05f;

        public override void ApplyToPlayer(GameObject player)
        {
            if (Level <= 0) return;

            StatApplier.Apply(player, stat, amountPerLevel * Level);
        }
    }
}

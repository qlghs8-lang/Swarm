using Swarm.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Swarm.LevelUp
{
    /// <summary>
    /// A level-up card that raises one stat for the current run. Replaces the nine near-identical
    /// subclasses that differed only in which PlayerStats method they called — a new stat passive
    /// is now a new .asset, not a new class.
    /// </summary>
    [CreateAssetMenu(fileName = "StatRunPassive", menuName = "Swarm/LevelUp/Stat Run Passive")]
    public class StatRunPassive : RunPassive
    {
        [SerializeField] private StatType stat;

        // Named percentPerLevel in the old per-stat classes; most stats are percentages but max
        // health and projectile count are flat amounts, so the neutral name is the honest one.
        [FormerlySerializedAs("percentPerLevel")]
        [SerializeField] private float amountPerLevel = 0.1f;

        public override void ApplyLevel(GameObject player, int newLevel)
        {
            StatApplier.Apply(player, stat, amountPerLevel);
        }
    }
}

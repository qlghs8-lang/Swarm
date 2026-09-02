using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>
    /// Marks an enemy as immune to crowd-control effects. Added to the boss, whose fight is built
    /// around telegraphed slams the player has to read and dodge: a boss that can be knocked back
    /// on every hit or frozen by a chain-lightning proc never gets to finish a slam, so the fight
    /// collapses into "stand still and hold the trigger". Ordinary enemies carry no such component
    /// and stay fully pushable.
    ///
    /// Only hard crowd control is blocked. Damage over time, the poison slow and its defence
    /// penalty still land, so damage-over-time builds keep working against the boss.
    /// </summary>
    public class EnemyStatusImmunity : MonoBehaviour
    {
        [SerializeField] private bool immuneToKnockback = true;
        [SerializeField] private bool immuneToFreeze = true;

        public bool ImmuneToKnockback => immuneToKnockback;
        public bool ImmuneToFreeze => immuneToFreeze;
    }
}

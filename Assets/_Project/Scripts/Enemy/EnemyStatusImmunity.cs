using UnityEngine;
using UnityEngine.Serialization;

namespace Swarm.Enemy
{
    /// <summary>
    /// Marks an enemy as immune to crowd-control effects. Added to the boss, whose fight is built
    /// around telegraphed slams the player has to read and dodge: a boss that can be knocked back
    /// on every hit or stunned by every lightning strike never gets to finish a slam, so the fight
    /// collapses into "stand still and hold the trigger". Ordinary enemies carry no such component
    /// and stay fully pushable.
    ///
    /// Only hard crowd control is blocked. Damage over time, slows (the poison's and chain
    /// lightning's) and defence penalties still land, so those builds keep working against the
    /// boss -- a slowed boss still walks its slam through, it just arrives later.
    /// </summary>
    public class EnemyStatusImmunity : MonoBehaviour
    {
        [SerializeField] private bool immuneToKnockback = true;
        [FormerlySerializedAs("immuneToFreeze")]
        [SerializeField] private bool immuneToStun = true;
        [SerializeField] private bool immuneToSlow;

        public bool ImmuneToKnockback => immuneToKnockback;

        /// <summary>Blocks anything that takes control away outright — the lightning stun.</summary>
        public bool ImmuneToStun => immuneToStun;

        /// <summary>Off by default: a slow leaves the boss moving and dodgeable, so it is not the
        /// kind of control the boss fight has to shrug off.</summary>
        public bool ImmuneToSlow => immuneToSlow;
    }
}

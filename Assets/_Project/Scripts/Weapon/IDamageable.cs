using Swarm.Player;

namespace Swarm.Weapon
{
    public interface IDamageable
    {
        /// <param name="isCritical">Only set for direct hits, so the damage number can mark them.
        /// Damage-over-time ticks carry the crit multiplier in their precomputed damage but are not
        /// themselves crit events, and are left unmarked.</param>
        /// <param name="knockbackScale">Per-weapon multiplier on the knockback a hit applies.
        /// Knockback is OPT-IN: the default of 0 means a hit deals damage without pushing, and
        /// only the weapons whose feel depends on impact pass a value. 1 is a full-strength hit;
        /// rapid-fire or piercing weapons pass a fraction so their hit density does not shove
        /// enemies around harder than a single heavy swing would.</param>
        void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower,
                        float penetration = 0f, bool isCritical = false, float knockbackScale = 0f);
    }
}

using Swarm.Player;

namespace Swarm.Weapon
{
    public interface IDamageable
    {
        /// <param name="isCritical">Only set for direct hits, so the damage number can mark them.
        /// Damage-over-time ticks carry the crit multiplier in their precomputed damage but are not
        /// themselves crit events, and are left unmarked.</param>
        void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower,
                        float penetration = 0f, bool isCritical = false);
    }
}

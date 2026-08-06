using Swarm.Player;

namespace Swarm.Weapon
{
    public interface IDamageable
    {
        void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower, float penetration = 0f);
    }
}

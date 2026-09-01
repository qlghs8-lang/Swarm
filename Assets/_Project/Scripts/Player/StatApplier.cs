using UnityEngine;

namespace Swarm.Player
{
    /// <summary>
    /// The single place that maps a <see cref="StatType"/> onto the component that owns it.
    /// Both run passives and permanent shop upgrades route through here, so a new stat needs one
    /// enum entry and one case rather than a pair of ScriptableObject subclasses.
    /// </summary>
    public static class StatApplier
    {
        public static void Apply(GameObject player, StatType stat, float amount)
        {
            if (player == null) return;

            // Max health lives on PlayerHealth, not PlayerStats — the only stat that does.
            if (stat == StatType.MaxHealth)
            {
                if (player.TryGetComponent<PlayerHealth>(out var health))
                {
                    health.IncreaseMaxHealth(Mathf.RoundToInt(amount));
                }

                return;
            }

            if (!player.TryGetComponent<PlayerStats>(out var stats)) return;

            switch (stat)
            {
                case StatType.PrimaryStat:
                    stats.IncreasePrimaryStat(amount);
                    break;
                case StatType.AttackPower:
                    stats.IncreaseAttackPower(amount);
                    break;
                case StatType.MagicPower:
                    stats.IncreaseMagicPower(amount);
                    break;
                case StatType.MoveSpeed:
                    stats.IncreaseMoveSpeed(amount);
                    break;
                case StatType.Defense:
                    stats.IncreaseDefense(amount);
                    break;
                case StatType.CooldownReduction:
                    stats.IncreaseCooldownReduction(amount);
                    break;
                case StatType.Penetration:
                    // One card raises both, matching the old PenetrationRunPassive/PassivePenetrationUpgrade.
                    stats.IncreaseArmorPenetration(amount);
                    stats.IncreaseMagicPenetration(amount);
                    break;
                case StatType.MagnetRadius:
                    stats.IncreaseMagnetRadius(amount);
                    break;
                case StatType.ProjectileCount:
                    stats.IncreaseProjectileCount(Mathf.RoundToInt(amount));
                    break;
                case StatType.AreaSize:
                    stats.IncreaseAreaSize(amount);
                    break;
                case StatType.Luck:
                    // Luck is critical chance — see PlayerStats.RollDamageMultiplier.
                    stats.IncreaseLuck(amount);
                    break;
                default:
                    Debug.LogError($"StatApplier has no case for {stat}.");
                    break;
            }
        }
    }
}

using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    public abstract class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int unlockCost;
        [SerializeField] private DamageStatType damageStatType;
        [SerializeField] private int maxHealthBonus;
        [SerializeField] private float defenseBonus;
        [SerializeField] private float moveSpeedBonus;
        [SerializeField] private float cooldownReductionBonus;

        private string UnlockedKey => $"Swarm_CharacterUnlocked_{id}";

        public string Id => id;
        public string DisplayName => displayName;
        public int UnlockCost => unlockCost;
        public DamageStatType DamageStatType => damageStatType;
        public bool IsUnlocked => unlockCost <= 0 || PlayerPrefs.GetInt(UnlockedKey, 0) == 1;

        public bool TryUnlock()
        {
            if (IsUnlocked) return false;
            if (!GoldWallet.TrySpend(unlockCost)) return false;

            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.Save();
            return true;
        }

        public void ApplyToPlayer(GameObject player)
        {
            DisableAllWeapons(player);

            if (player.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.SetDamageStatType(damageStatType);
                if (defenseBonus != 0f) stats.IncreaseDefense(defenseBonus);
                if (moveSpeedBonus != 0f) stats.IncreaseMoveSpeed(moveSpeedBonus);
                if (cooldownReductionBonus != 0f) stats.IncreaseCooldownReduction(cooldownReductionBonus);
            }

            if (maxHealthBonus != 0 && player.TryGetComponent<PlayerHealth>(out var health))
            {
                health.IncreaseMaxHealth(maxHealthBonus);
            }

            MarkAvailableWeapons(player);
            UnlockStartingWeapon(player);
        }

        protected static void DisableAllWeapons(GameObject player)
        {
            foreach (var weapon in player.GetComponents<ILevelableWeapon>())
            {
                weapon.SetAvailable(false);

                if (weapon is MonoBehaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        protected static void SetAvailable<T>(GameObject player) where T : LevelableWeapon
        {
            foreach (var weapon in player.GetComponents<T>())
            {
                weapon.SetAvailable(true);
            }
        }

        protected abstract void MarkAvailableWeapons(GameObject player);

        protected abstract void UnlockStartingWeapon(GameObject player);
    }
}

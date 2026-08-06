using UnityEngine;

namespace Swarm.Game
{
    public abstract class PassiveUpgrade : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int baseCost = 10;
        [SerializeField] private int costIncreasePerLevel = 5;
        [SerializeField] private int maxLevel = 5;

        private string LevelKey => $"Swarm_PassiveLevel_{id}";

        public string DisplayName => displayName;
        public int MaxLevel => maxLevel;
        public int Level => PlayerPrefs.GetInt(LevelKey, 0);
        public int Cost => baseCost + costIncreasePerLevel * Level;
        public bool IsMaxed => Level >= maxLevel;

        public bool TryPurchase()
        {
            if (IsMaxed) return false;
            if (!GoldWallet.TrySpend(Cost)) return false;

            PlayerPrefs.SetInt(LevelKey, Level + 1);
            PlayerPrefs.Save();
            return true;
        }

        public abstract void ApplyToPlayer(GameObject player);
    }
}

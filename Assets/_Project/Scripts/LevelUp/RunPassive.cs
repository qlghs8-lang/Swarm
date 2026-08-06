using UnityEngine;

namespace Swarm.LevelUp
{
    public abstract class RunPassive : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private int maxLevel = 5;

        public string DisplayName => displayName;
        public string Description => description;
        public int MaxLevel => maxLevel;

        public abstract void ApplyLevel(GameObject player, int newLevel);
    }
}

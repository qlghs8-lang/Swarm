using UnityEngine;

namespace Swarm.Weapon
{
    public abstract class LevelableWeapon : MonoBehaviour, ILevelableWeapon
    {
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private int maxLevel = 8;
        [SerializeField] private float damagePerLevel = 0.15f;
        [SerializeField] private float cooldownReductionPerLevel = 0.08f;
        [SerializeField] private LevelableWeapon evolutionTarget;

        public bool IsAvailable { get; private set; }
        public LevelableWeapon EvolutionTarget => evolutionTarget;
        public int Level { get; private set; }
        public int MaxLevel => maxLevel;
        public string CardDisplayName => Level <= 0 ? $"{displayName} 획득" : $"{displayName} Lv.{Level + 1}";
        public string CardDescription => description;

        protected float DamageMultiplier => 1f + damagePerLevel * Mathf.Max(0, Level - 1);
        protected float CooldownMultiplier => Mathf.Max(0.3f, 1f - cooldownReductionPerLevel * Mathf.Max(0, Level - 1));

        public void SetAvailable(bool available)
        {
            IsAvailable = available;
        }

        public void Retire()
        {
            SetAvailable(false);
            enabled = false;
        }

        public void SetStartingLevel(int level)
        {
            Level = level;
            enabled = level > 0;
        }

        public void LevelUp()
        {
            if (Level >= maxLevel) return;

            Level++;
            enabled = true;
        }
    }
}

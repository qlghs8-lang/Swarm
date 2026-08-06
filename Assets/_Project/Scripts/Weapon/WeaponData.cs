using UnityEngine;

namespace Swarm.Weapon
{
    public abstract class WeaponData : ScriptableObject
    {
        [SerializeField] private int damage = 10;
        [SerializeField] private float attackInterval = 1f;

        public int Damage => damage;
        public float AttackInterval => attackInterval;
    }
}

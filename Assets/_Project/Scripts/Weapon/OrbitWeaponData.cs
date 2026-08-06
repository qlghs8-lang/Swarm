using UnityEngine;

namespace Swarm.Weapon
{
    [CreateAssetMenu(fileName = "OrbitWeaponData", menuName = "Swarm/Weapon/Orbit Weapon Data")]
    public class OrbitWeaponData : WeaponData
    {
        [SerializeField] private float orbitRadius = 1.8f;
        [SerializeField] private float angularSpeed = 180f;
        [SerializeField] private float hitRadius = 0.5f;

        public float OrbitRadius => orbitRadius;
        public float AngularSpeed => angularSpeed;
        public float HitRadius => hitRadius;
    }
}

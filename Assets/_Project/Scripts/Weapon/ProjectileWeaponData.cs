using UnityEngine;

namespace Swarm.Weapon
{
    [CreateAssetMenu(fileName = "ProjectileWeaponData", menuName = "Swarm/Weapon/Projectile Weapon Data")]
    public class ProjectileWeaponData : WeaponData
    {
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float range = 6f;
        [SerializeField] private int projectileCount = 1;
        [SerializeField] private float spreadAngleDegrees = 0f;

        public float ProjectileSpeed => projectileSpeed;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float Range => range;
        public int ProjectileCount => projectileCount;
        public float SpreadAngleDegrees => spreadAngleDegrees;
    }
}

using UnityEngine;

namespace Swarm.Weapon
{
    [CreateAssetMenu(fileName = "AoeWeaponData", menuName = "Swarm/Weapon/Aoe Weapon Data")]
    public class AoeWeaponData : WeaponData
    {
        [SerializeField] private float radius = 1.5f;

        public float Radius => radius;
    }
}

using UnityEngine;

namespace Swarm.Weapon
{
    [CreateAssetMenu(fileName = "AoeWeaponData", menuName = "Swarm/Weapon/Aoe Weapon Data")]
    public class AoeWeaponData : WeaponData
    {
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float attackAngleDegrees = 150f;
        [SerializeField] private float forwardOffset = 0f;

        public float Radius => radius;
        public float AttackAngleDegrees => attackAngleDegrees;
        public float ForwardOffset => forwardOffset;
    }
}

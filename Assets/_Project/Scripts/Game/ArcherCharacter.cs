using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "ArcherCharacter", menuName = "Swarm/Game/Archer Character")]
    public class ArcherCharacter : CharacterDefinition
    {
        protected override void MarkAvailableWeapons(GameObject player)
        {
            SetAvailable<ArcherArrowWeapon>(player);
            SetAvailable<ArcherVolleyWeapon>(player);
            SetAvailable<ArcherRollWeapon>(player);
        }

        protected override void UnlockStartingWeapon(GameObject player)
        {
            if (player.TryGetComponent<ArcherArrowWeapon>(out var weapon))
            {
                weapon.SetStartingLevel(1);
            }
        }
    }
}

using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "WarriorCharacter", menuName = "Swarm/Game/Warrior Character")]
    public class WarriorCharacter : CharacterDefinition
    {
        protected override void MarkAvailableWeapons(GameObject player)
        {
            SetAvailable<WarriorForwardWeapon>(player);
            SetAvailable<WarriorOrbitWeapon>(player);
            SetAvailable<WarriorLifestealWeapon>(player);
        }

        protected override void UnlockStartingWeapon(GameObject player)
        {
            if (player.TryGetComponent<WarriorForwardWeapon>(out var forwardWeapon))
            {
                forwardWeapon.SetStartingLevel(1);
            }
        }
    }
}

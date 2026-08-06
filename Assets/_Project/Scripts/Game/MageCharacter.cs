using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [CreateAssetMenu(fileName = "MageCharacter", menuName = "Swarm/Game/Mage Character")]
    public class MageCharacter : CharacterDefinition
    {
        protected override void MarkAvailableWeapons(GameObject player)
        {
            SetAvailable<MageBoltWeapon>(player);
            SetAvailable<MageFireballWeapon>(player);
            SetAvailable<MageHealWeapon>(player);
        }

        protected override void UnlockStartingWeapon(GameObject player)
        {
            if (player.TryGetComponent<MageBoltWeapon>(out var boltWeapon))
            {
                boltWeapon.SetStartingLevel(1);
            }
        }
    }
}

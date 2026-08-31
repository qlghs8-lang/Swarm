#if UNITY_EDITOR
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    public class DebugEvolveWeaponsButton : MonoBehaviour
    {
        private const int MaxLevel = 8;

        private GameObject _player;

        private void Awake()
        {
            _player = GameObject.FindGameObjectWithTag("Player");
        }

        public void EvolveWarriorWeapons()
        {
            Evolve<WarriorForwardWeapon, WarriorComboAttackWeapon>();
            Evolve<WarriorOrbitWeapon, WarriorSwordChasingWeapon>();
            Evolve<WarriorLifestealWeapon, WarriorRageWeapon>();
        }

        public void EvolveArcherWeapons()
        {
            Evolve<ArcherArrowWeapon, ArcherFocusWeapon>();
            Evolve<ArcherVolleyWeapon, ArcherSprayWeapon>();
            Evolve<ArcherRollWeapon, ArcherToxicRollWeapon>();
        }

        public void EvolveMageWeapons()
        {
            Evolve<MageBoltWeapon, MageIceBoltWeapon>();
            Evolve<MageFireballWeapon, MageExplosionWeapon>();
            Evolve<MageHealWeapon, MageBlessingWeapon>();
        }

        private void Evolve<TOriginal, TEvolution>()
            where TOriginal : LevelableWeapon
            where TEvolution : LevelableWeapon
        {
            if (_player == null) return;
            if (!_player.TryGetComponent<TOriginal>(out var original)) return;
            if (!_player.TryGetComponent<TEvolution>(out var evolution)) return;

            original.Retire();
            evolution.SetAvailable(true);
            evolution.SetStartingLevel(MaxLevel);
        }
    }
}
#endif

#if UNITY_EDITOR
using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    public class DebugLevelUpButton : MonoBehaviour
    {
        private PlayerExperience _experience;

        private void Awake()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.TryGetComponent(out _experience);
        }

        public void ForceLevelUp()
        {
            if (_experience != null) _experience.AddExperience(_experience.ExpToNextLevel);
        }
    }
}
#endif

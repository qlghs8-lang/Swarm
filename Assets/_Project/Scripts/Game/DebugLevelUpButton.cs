using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    // Drives a UI button that is present in Game.unity, the shipping scene. Previously #if
    // UNITY_EDITOR'd out, which both broke the scene reference in player builds and left the
    // button itself visible and clickable. The class now ships, and outside the editor and
    // development builds it hides its own button object.
    public class DebugLevelUpButton : MonoBehaviour
    {
        private PlayerExperience _experience;

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                gameObject.SetActive(false);
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.TryGetComponent(out _experience);
        }

        public void ForceLevelUp()
        {
            if (_experience != null) _experience.AddExperience(_experience.ExpToNextLevel);
        }
    }
}

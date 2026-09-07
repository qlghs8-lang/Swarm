using Swarm.Player;
using UnityEngine;

namespace Swarm.Game
{
    // Drives a UI button that is present in Game.unity, the shipping scene. Previously #if
    // UNITY_EDITOR'd out, which both broke the scene reference in player builds and left the
    // button itself visible and clickable. The class now ships and hides its own button object
    // unless DevUi is on — so the editor shows the same screen as a release build by default.
    public class DebugLevelUpButton : MonoBehaviour
    {
        private PlayerExperience _experience;

        private void Awake()
        {
            if (!DevUi.IsEnabled)
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

using Swarm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class ExperienceBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        private PlayerExperience _playerExperience;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out _playerExperience))
            {
                _playerExperience.OnExperienceChanged += UpdateFill;
                UpdateFill(_playerExperience.CurrentExp, _playerExperience.ExpToNextLevel);
            }
        }

        private void OnDestroy()
        {
            if (_playerExperience != null)
            {
                _playerExperience.OnExperienceChanged -= UpdateFill;
            }
        }

        private void UpdateFill(int current, int max)
        {
            fillImage.fillAmount = (float)current / max;
        }
    }
}

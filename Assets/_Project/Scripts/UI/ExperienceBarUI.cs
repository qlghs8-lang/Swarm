using Swarm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class ExperienceBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        private PlayerExperience _playerExperience;
        private RectTransform _fillRect;
        private float _fullWidth;

        private void Start()
        {
            _fillRect = fillImage.rectTransform;
            _fullWidth = ((RectTransform)_fillRect.parent).rect.width;

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
            var size = _fillRect.sizeDelta;
            size.x = _fullWidth * Mathf.Clamp01((float)current / max);
            _fillRect.sizeDelta = size;
        }
    }
}

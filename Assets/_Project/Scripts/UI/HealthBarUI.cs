using Swarm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        private PlayerHealth _playerHealth;
        private RectTransform _fillRect;
        private float _fullWidth;

        private void Start()
        {
            _fillRect = fillImage.rectTransform;
            _fullWidth = ((RectTransform)_fillRect.parent).rect.width;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out _playerHealth))
            {
                _playerHealth.OnHealthChanged += UpdateFill;
                UpdateFill(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdateFill;
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

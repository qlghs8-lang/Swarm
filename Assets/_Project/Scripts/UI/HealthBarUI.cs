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
        private int _lastCurrent;
        private int _lastMax = 1;

        private void Start()
        {
            _fillRect = fillImage.rectTransform;

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

        // 캔버스 레이아웃이 잡히거나 해상도가 바뀌면 폭이 달라지므로 다시 반영한다.
        private void OnRectTransformDimensionsChange()
        {
            if (_fillRect != null) UpdateFill(_lastCurrent, _lastMax);
        }

        private void UpdateFill(int current, int max)
        {
            _lastCurrent = current;
            _lastMax = Mathf.Max(1, max);

            // 부모가 스트레치 앵커일 수 있어 매번 실제 폭을 읽는다 (해상도/회전 변경 대응).
            var fullWidth = ((RectTransform)_fillRect.parent).rect.width;
            var size = _fillRect.sizeDelta;
            size.x = fullWidth * Mathf.Clamp01(max > 0 ? (float)current / max : 0f);
            _fillRect.sizeDelta = size;
        }
    }
}

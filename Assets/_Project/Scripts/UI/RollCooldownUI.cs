using Swarm.Settings;
using Swarm.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// On-screen roll button. Shows the active roll skill, its cooldown sweep and
    /// the remaining seconds, and triggers the roll when pressed.
    /// </summary>
    public class RollCooldownUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject iconRoot;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text cooldownText;
        [SerializeField] private Button rollButton;

        [Header("Look")]
        [SerializeField] private Color readyIconColor = Color.white;
        [SerializeField] private Color cooldownIconColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private ArcherRollWeapon _rollWeapon;
        private ArcherToxicRollWeapon _toxicRollWeapon;

        private RectTransform _rect;
        // 씬에 배치된 위치를 '오른쪽'의 기준으로 삼는다. 왼쪽은 이 값을 화면 기준으로 뒤집은 것이라,
        // 나중에 버튼을 조금 옮기더라도 좌우가 따로 놀지 않는다.
        private Vector2 _rightAnchoredPosition;
        private Vector2 _rightAnchor;
        private Vector2 _rightPivot;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _rightAnchoredPosition = _rect.anchoredPosition;
            _rightAnchor = _rect.anchorMin;
            _rightPivot = _rect.pivot;
        }

        private void OnEnable()
        {
            GameSettings.OnChanged += ApplySide;
            ApplySide();
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= ApplySide;
        }

        /// <summary>설정한 쪽으로 버튼을 옮긴다. 앵커·피벗까지 같이 뒤집어야 화면 폭이 달라져도 붙어 있다.</summary>
        private void ApplySide()
        {
            if (_rect == null) return;

            var onLeft = GameSettings.RollSide == RollButtonSide.Left;

            var anchorX = onLeft ? 1f - _rightAnchor.x : _rightAnchor.x;
            _rect.anchorMin = new Vector2(anchorX, _rightAnchor.y);
            _rect.anchorMax = new Vector2(anchorX, _rightAnchor.y);
            _rect.pivot = new Vector2(onLeft ? 1f - _rightPivot.x : _rightPivot.x, _rightPivot.y);
            _rect.anchoredPosition = new Vector2(
                onLeft ? -_rightAnchoredPosition.x : _rightAnchoredPosition.x,
                _rightAnchoredPosition.y);
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _rollWeapon);
                player.TryGetComponent(out _toxicRollWeapon);
            }

            if (iconRoot != null) iconRoot.SetActive(false);
        }

        private void Update()
        {
            var active = GetActive();

            if (active == null)
            {
                if (iconRoot != null && iconRoot.activeSelf) iconRoot.SetActive(false);
                return;
            }

            if (iconRoot != null && !iconRoot.activeSelf) iconRoot.SetActive(true);

            var progress = active.CooldownProgress01;
            var ready = progress >= 1f;

            // Dark sweep shrinks clockwise as the cooldown fills up.
            if (fillImage != null)
            {
                fillImage.enabled = !ready;
                fillImage.fillAmount = 1f - progress;
            }

            if (cooldownText != null)
            {
                var remaining = active.CooldownRemaining;
                cooldownText.enabled = !ready;
                if (!ready) cooldownText.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0");
            }

            if (iconImage != null) iconImage.color = ready ? readyIconColor : cooldownIconColor;

            if (rollButton != null) rollButton.interactable = ready;
        }

        public void TryRoll()
        {
            GetActive()?.TryRoll();
        }

        private IActiveRoll GetActive()
        {
            if (_toxicRollWeapon != null && _toxicRollWeapon.Level > 0) return _toxicRollWeapon;
            if (_rollWeapon != null && _rollWeapon.Level > 0) return _rollWeapon;
            return null;
        }
    }
}

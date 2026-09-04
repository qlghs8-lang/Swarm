using Swarm.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// 보스 체력바 HUD.
    /// 프레임(BossBar_Frame)과 채움(BossBar_Fill)을 분리한 2레이어 구조로,
    /// 채움 이미지를 Image.type = Filled(Horizontal)로 잘라 체력 비율을 연속적으로 표시한다.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("BossBar_Frame 스프라이트를 쓰는 배경 이미지 (Simple)")]
        [SerializeField] private Image frameImage;

        [Tooltip("BossBar_Fill 스프라이트를 쓰는 채움 이미지 (Filled / Horizontal / Origin Left)")]
        [SerializeField] private Image fillImage;

        [SerializeField] private GameObject root;

        [Header("Fill Calibration")]
        // BossBar_Fill.png는 채움 영역만 잘라낸 텍스처(1094x69)이고, BossHP_Fill의 RectTransform이
        // 프레임 안쪽 홈 위치에 앵커로 고정돼 있다. 그래서 보정 없이 0~1을 그대로 쓰면 된다.
        // (스프라이트에 투명 여백이 있으면 Unity가 Image.fillAmount를 '여백을 제외한 내용 영역' 기준으로
        //  적용하기 때문에, 여기서 또 보정하면 이중 보정이 되어 체력이 100%여도 89%처럼 보인다.)
        [Tooltip("채움 영역 시작 비율. 기본 0 - 텍스처를 직접 갈아끼울 때만 건드릴 것")]
        [SerializeField, Range(0f, 1f)] private float fillStart = 0f;

        [Tooltip("채움 영역 끝 비율. 기본 1 - 텍스처를 직접 갈아끼울 때만 건드릴 것")]
        [SerializeField, Range(0f, 1f)] private float fillEnd = 1f;

        [Header("Animation")]
        [Tooltip("0이면 즉시 반영, 값이 클수록 빠르게 따라간다")]
        [SerializeField] private float lerpSpeed = 8f;

        private EnemyHealth _target;
        private float _targetRatio = 1f;
        private float _displayRatio = 1f;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            ConfigureFillImage();
        }

        private void ConfigureFillImage()
        {
            if (fillImage == null) return;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        public void Bind(EnemyHealth target)
        {
            if (target == null) return;

            Unbind();

            _target = target;
            _target.OnHealthChanged += UpdateFill;
            _target.OnDied += HandleDied;

            _targetRatio = 1f;
            _displayRatio = 1f;
            ApplyRatio(_displayRatio);

            if (root != null) root.SetActive(true);
        }

        public void Unbind()
        {
            if (_target != null)
            {
                _target.OnHealthChanged -= UpdateFill;
                _target.OnDied -= HandleDied;
                _target = null;
            }
        }

        private void HandleDied()
        {
            Unbind();
            _targetRatio = 0f;
            _displayRatio = 0f;
            ApplyRatio(0f);
            if (root != null) root.SetActive(false);
        }

        private void UpdateFill(int current, int max)
        {
            _targetRatio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (lerpSpeed <= 0f)
            {
                _displayRatio = _targetRatio;
                ApplyRatio(_displayRatio);
            }
        }

        private void Update()
        {
            if (lerpSpeed <= 0f) return;
            if (Mathf.Approximately(_displayRatio, _targetRatio)) return;

            _displayRatio = Mathf.MoveTowards(
                _displayRatio,
                _targetRatio,
                Mathf.Max(Mathf.Abs(_displayRatio - _targetRatio) * lerpSpeed, 0.05f) * Time.deltaTime);

            ApplyRatio(_displayRatio);
        }

        /// <summary>
        /// 체력 비율(0~1)을 그대로 fillAmount로 넣는다.
        /// fillStart/fillEnd는 텍스처를 교체했을 때를 위한 예비 보정값이며 기본값은 0/1이다.
        /// </summary>
        private void ApplyRatio(float ratio)
        {
            if (fillImage == null) return;
            fillImage.fillAmount = Mathf.Lerp(fillStart, fillEnd, Mathf.Clamp01(ratio));
        }

        private void OnDestroy()
        {
            Unbind();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (fillEnd < fillStart) fillEnd = fillStart;
            ConfigureFillImage();
        }
#endif
    }
}

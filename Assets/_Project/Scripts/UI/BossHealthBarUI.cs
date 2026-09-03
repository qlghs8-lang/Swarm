using Swarm.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// 보스 체력바 HUD.
    /// 체력 구간별로 미리 만들어 둔 체력바 스프라이트(BossBar_State0~3)를 교체해 표시한다.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image barImage;
        [SerializeField] private GameObject root;

        [Tooltip("체력이 높은 순서대로 배치 (0 = 가득 찬 상태)")]
        [SerializeField] private Sprite[] stateSprites;

        [Tooltip("stateSprites와 같은 개수. 해당 스프라이트를 쓰기 위한 최소 체력 비율")]
        [SerializeField] private float[] stateThresholds = { 0.8f, 0.45f, 0.2f, 0f };

        private EnemyHealth _target;
        private int _currentState = -1;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Bind(EnemyHealth target)
        {
            if (target == null) return;

            Unbind();

            _target = target;
            _target.OnHealthChanged += UpdateFill;
            _target.OnDied += HandleDied;

            _currentState = -1;
            ApplyState(0);

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
            if (root != null) root.SetActive(false);
        }

        private void UpdateFill(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            ApplyState(GetStateIndex(ratio));
        }

        private int GetStateIndex(float ratio)
        {
            if (stateSprites == null || stateSprites.Length == 0) return -1;

            int last = stateSprites.Length - 1;
            if (stateThresholds == null) return last;

            int count = Mathf.Min(stateSprites.Length, stateThresholds.Length);
            for (int i = 0; i < count; i++)
            {
                if (ratio >= stateThresholds[i]) return i;
            }
            return last;
        }

        private void ApplyState(int index)
        {
            if (barImage == null || stateSprites == null) return;
            if (index < 0 || index >= stateSprites.Length) return;
            if (index == _currentState) return;

            _currentState = index;
            barImage.sprite = stateSprites[index];
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}

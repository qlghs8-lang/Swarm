using Swarm.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject root;

        private EnemyHealth _target;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        public void Bind(EnemyHealth target)
        {
            Unbind();

            _target = target;
            _target.OnHealthChanged += UpdateFill;
            _target.OnDied += HandleDied;

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
            fillImage.fillAmount = max > 0 ? (float)current / max : 0f;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}

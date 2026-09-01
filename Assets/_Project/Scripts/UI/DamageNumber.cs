using Swarm.Weapon;
using UnityEngine;

namespace Swarm.UI
{
    [RequireComponent(typeof(TextMesh))]
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.6f;

        // Crits are read at a glance from colour + size, not from the number itself: the damage
        // figure is only ~2x, which is easy to miss in a screen full of floating text.
        [SerializeField] private Color criticalColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private float criticalScale = 1.45f;

        private TextMesh _textMesh;
        private Color _originalColor;
        private Color _startColor;
        private float _elapsed;
        private GameObject _sourcePrefab;
        private Vector3 _originalScale;

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
            _originalScale = transform.localScale;
            _originalColor = _textMesh.color;
            _startColor = _originalColor;

            if (TryGetComponent<MeshRenderer>(out var meshRenderer) && _textMesh.font != null)
            {
                meshRenderer.material = _textMesh.font.material;
            }
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            // Pooled instances keep whatever scale the previous crit left behind, so restore it here
            // rather than only in the non-crit Setup path.
            transform.localScale = _originalScale;
        }

        public void SetSourcePrefab(GameObject prefab)
        {
            _sourcePrefab = prefab;
        }

        public void Setup(int amount)
        {
            Setup(amount, false);
        }

        public void Setup(int amount, bool isCritical)
        {
            _textMesh.text = isCritical ? amount + "!" : amount.ToString();
            _startColor = isCritical ? criticalColor : _originalColor;
            _textMesh.color = _startColor;
            transform.localScale = isCritical ? _originalScale * criticalScale : _originalScale;
        }

        public void Setup(string text, Color color)
        {
            _textMesh.text = text;
            _textMesh.color = color;
            _startColor = color;
            transform.localScale = _originalScale;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            var t = Mathf.Clamp01(_elapsed / lifetime);
            _textMesh.color = new Color(_startColor.r, _startColor.g, _startColor.b, (1f - t) * _startColor.a);

            if (_elapsed >= lifetime)
            {
                SharedObjectPool.Release(_sourcePrefab, gameObject);
            }
        }
    }
}

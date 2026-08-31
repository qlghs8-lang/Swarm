using Swarm.Weapon;
using UnityEngine;

namespace Swarm.UI
{
    [RequireComponent(typeof(TextMesh))]
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.6f;

        private TextMesh _textMesh;
        private Color _originalColor;
        private Color _startColor;
        private float _elapsed;
        private GameObject _sourcePrefab;

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
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
        }

        public void SetSourcePrefab(GameObject prefab)
        {
            _sourcePrefab = prefab;
        }

        public void Setup(int amount)
        {
            _textMesh.text = amount.ToString();
            _startColor = _originalColor;
            _textMesh.color = _startColor;
        }

        public void Setup(string text, Color color)
        {
            _textMesh.text = text;
            _textMesh.color = color;
            _startColor = color;
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

using UnityEngine;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StrikeFlash : MonoBehaviour
    {
        [SerializeField] private float duration = 0.15f;

        private SpriteRenderer _spriteRenderer;
        private Color _startColor;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _startColor = _spriteRenderer.color;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / duration);
            _spriteRenderer.color = new Color(_startColor.r, _startColor.g, _startColor.b, _startColor.a * (1f - t));

            if (_elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}

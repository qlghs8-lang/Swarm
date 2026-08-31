using UnityEngine;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StrikeFlash : MonoBehaviour
    {
        [SerializeField] private float duration = 0.15f;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameDuration = 0.05f;

        private SpriteRenderer _spriteRenderer;
        private Color _startColor;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _startColor = _spriteRenderer.color;

            if (frames != null && frames.Length > 0)
            {
                _spriteRenderer.sprite = frames[0];
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (frames != null && frames.Length > 0)
            {
                var frameIndex = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(_elapsed / frameDuration));
                _spriteRenderer.sprite = frames[frameIndex];

                if (_elapsed >= frames.Length * frameDuration)
                {
                    Destroy(gameObject);
                }

                return;
            }

            var t = Mathf.Clamp01(_elapsed / duration);
            _spriteRenderer.color = new Color(_startColor.r, _startColor.g, _startColor.b, _startColor.a * (1f - t));

            if (_elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}

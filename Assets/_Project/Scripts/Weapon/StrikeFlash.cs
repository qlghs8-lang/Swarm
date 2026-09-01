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

        // Pooled rather than destroyed: the mage's lightning strike spawns one of these per target,
        // and at eight targets a cast that is over seven a second — by far the most frequently
        // created object in the game. Instantiate/Destroy at that rate is a steady GC drip.
        private GameObject _sourcePrefab;

        public void SetSourcePrefab(GameObject prefab)
        {
            _sourcePrefab = prefab;
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _startColor = _spriteRenderer.color;
        }

        // Reuse means the previous life's faded-out colour and finished timer are still here.
        private void OnEnable()
        {
            _elapsed = 0f;
            _spriteRenderer.color = _startColor;

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
                    Release();
                }

                return;
            }

            var t = Mathf.Clamp01(_elapsed / duration);
            _spriteRenderer.color = new Color(_startColor.r, _startColor.g, _startColor.b, _startColor.a * (1f - t));

            if (_elapsed >= duration)
            {
                Release();
            }
        }

        private void Release()
        {
            // A null source prefab (an instance placed by hand in a scene) falls through to Destroy.
            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

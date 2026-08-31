using UnityEngine;

namespace Swarm.Weapon
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class FireballAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameDuration = 0.06f;

        private SpriteRenderer _spriteRenderer;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _elapsed = 0f;
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            _elapsed += Time.deltaTime;
            var frameIndex = Mathf.FloorToInt(_elapsed / frameDuration) % frames.Length;
            _spriteRenderer.sprite = frames[frameIndex];
        }
    }
}

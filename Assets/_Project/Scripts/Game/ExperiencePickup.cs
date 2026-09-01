using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class ExperiencePickup : MonoBehaviour
    {
        [SerializeField] private int amount = 5;

        [Header("Tiers")]
        // One prefab and one pool for all three sizes: the orb picks its look from the amount it
        // was given, so a tank's drop reads as worth more from across the screen without needing
        // a second prefab, a second pool and a second field on every enemy.
        [SerializeField] private Sprite smallSprite;
        [SerializeField] private Sprite mediumSprite;
        [SerializeField] private Sprite largeSprite;
        [SerializeField] private int mediumThreshold = 10;
        [SerializeField] private int largeThreshold = 30;

        private SpriteRenderer _renderer;
        private GameObject _sourcePrefab;
        private bool _collected;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _collected = false;
        }

        public void SetAmount(int value)
        {
            amount = value;
            ApplyTierSprite();
        }

        private void ApplyTierSprite()
        {
            if (_renderer == null) return;

            var sprite = amount >= largeThreshold ? largeSprite
                       : amount >= mediumThreshold ? mediumSprite
                       : smallSprite;

            if (sprite != null) _renderer.sprite = sprite;
        }

        public void SetSourcePrefab(GameObject prefab)
        {
            _sourcePrefab = prefab;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (_collected) return;
            if (!other.CompareTag("Player")) return;

            _collected = true;

            if (other.TryGetComponent<PlayerExperience>(out var experience))
            {
                experience.AddExperience(amount);
            }

            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    [RequireComponent(typeof(Collider2D))]
    public class GoldPickup : MonoBehaviour
    {
        [SerializeField] private int amount = 1;

        [Header("Tiers")]
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite pileSprite;
        [SerializeField] private int pileThreshold = 10;

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

        /// <summary>Nothing calls this yet — every drop is worth 1. It exists so a big payout can
        /// be dropped as one pile instead of a shower of coins.</summary>
        public void SetAmount(int value)
        {
            amount = value;
            if (_renderer == null) return;

            var sprite = amount >= pileThreshold ? pileSprite : coinSprite;
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

            GoldWallet.Add(amount);
            SharedObjectPool.Release(_sourcePrefab, gameObject);
        }
    }
}

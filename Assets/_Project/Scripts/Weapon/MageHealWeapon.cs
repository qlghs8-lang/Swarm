using System.Collections;
using Swarm.Player;
using Swarm.UI;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageHealWeapon : LevelableWeapon
    {
        [SerializeField] private int healAmount = 15;
        [SerializeField] private float baseCooldown = 30f;
        [SerializeField] private GameObject healNumberPrefab;
        [SerializeField] private Color healNumberColor = new(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private Sprite[] healEffectFrames;
        [SerializeField] private float healEffectFrameDuration = 0.08f;
        [SerializeField] private float healEffectScale = 1f;

        private float _timer;
        private PlayerStats _stats;
        private PlayerHealth _health;
        private SpriteRenderer _healEffectRenderer;
        private Coroutine _healEffectCoroutine;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            CreateHealEffectRenderer();
        }

        private void CreateHealEffectRenderer()
        {
            var effectObject = new GameObject("HealEffect (Temp)");
            effectObject.transform.SetParent(transform);
            effectObject.transform.localPosition = Vector3.zero;
            effectObject.transform.localScale = Vector3.one * healEffectScale;
            _healEffectRenderer = effectObject.AddComponent<SpriteRenderer>();
            _healEffectRenderer.sortingOrder = 2;
            effectObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_healEffectRenderer != null)
            {
                _healEffectRenderer.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveCooldown = Mathf.Max(0.5f, baseCooldown * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveCooldown) return;

            _timer = 0f;
            TriggerHeal();
        }

        private void TriggerHeal()
        {
            if (_health == null) return;

            var amount = Mathf.RoundToInt(healAmount * DamageMultiplier);
            _health.Heal(amount);
            SpawnHealNumber(amount);
            PlayHealEffect();
        }

        private void PlayHealEffect()
        {
            if (healEffectFrames == null || healEffectFrames.Length == 0) return;

            if (_healEffectCoroutine != null)
            {
                StopCoroutine(_healEffectCoroutine);
            }

            _healEffectCoroutine = StartCoroutine(HealEffectRoutine());
        }

        private IEnumerator HealEffectRoutine()
        {
            _healEffectRenderer.gameObject.SetActive(true);
            foreach (var frameSprite in healEffectFrames)
            {
                _healEffectRenderer.sprite = frameSprite;
                yield return new WaitForSeconds(healEffectFrameDuration);
            }

            _healEffectRenderer.gameObject.SetActive(false);
            _healEffectCoroutine = null;
        }

        private void SpawnHealNumber(int amount)
        {
            if (healNumberPrefab == null) return;

            var instance = SharedObjectPool.Get(healNumberPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            if (instance.TryGetComponent<DamageNumber>(out var damageNumber))
            {
                damageNumber.SetSourcePrefab(healNumberPrefab);
                damageNumber.Setup($"+{amount}", healNumberColor);
            }
        }
    }
}

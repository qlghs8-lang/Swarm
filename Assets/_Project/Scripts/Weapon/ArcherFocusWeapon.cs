using System.Collections;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ArcherFocusWeapon : LevelableWeapon
    {
        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private float burstInterval = 0.06f;
        [SerializeField] private Sprite[] hitEffectFrames;
        [SerializeField] private float hitEffectFrameDuration = 0.045f;
        [SerializeField] private Material effectMaterial;

        private float _timer;
        private ObjectPool _pool;
        private PlayerStats _stats;
        private SpriteEffectPlayer _hitEffect;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _hitEffect = SpriteEffectPlayer.Create(
                this, "FocusHitEffect (Temp)", effectMaterial, hitEffectFrames, hitEffectFrameDuration);
        }

        private void OnDisable()
        {
            _hitEffect?.Hide();
        }

        private void Start()
        {
            if (data != null && data.ProjectilePrefab != null)
            {
                _pool = new ObjectPool(data.ProjectilePrefab);
            }
        }

        private void Update()
        {
            if (_pool == null) return;

            var cooldownMultiplier = CooldownMultiplier * (1f - (_stats != null ? _stats.CooldownReduction : 0f));
            var effectiveInterval = Mathf.Max(0.1f, data.AttackInterval * cooldownMultiplier);

            _timer += Time.deltaTime;
            if (_timer < effectiveInterval) return;

            if (TryFire())
            {
                _timer = 0f;
            }
        }

        private bool TryFire()
        {
            var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
            var target = EnemyTargeting.FindNearest(origin, data.Range);
            if (target == null) return false;

            var count = Mathf.Max(1, data.ProjectileCount + (_stats != null ? _stats.ProjectileCountBonus : 0));
            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;

            StartCoroutine(FireBurst(target, count, damage, penetration, isCritical));
            return true;
        }

        private IEnumerator FireBurst(Transform target, int count, int damage, float penetration, bool isCritical)
        {
            for (var i = 0; i < count; i++)
            {
                if (target == null || !target.gameObject.activeInHierarchy) yield break;

                var origin = _stats != null ? _stats.AttackOrigin : (Vector2)transform.position;
                var direction = (EnemyTargeting.GetHitPoint(target) - origin).normalized;
                var instance = _pool.Get(origin, Quaternion.identity);
                if (instance.TryGetComponent<Projectile>(out var projectile))
                {
                    projectile.Launch(direction, data.ProjectileSpeed, data.Range, damage, _pool, 0, DamageStatType.AttackPower, penetration, PlayHitEffect, isCritical);
                }

                if (i < count - 1) yield return new WaitForSeconds(burstInterval);
            }
        }

        private void PlayHitEffect(Vector2 position)
        {
            _hitEffect?.Play(position);
        }
    }
}

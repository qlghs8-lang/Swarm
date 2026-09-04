using System.Collections.Generic;
using Swarm.Player;
using UnityEngine;

namespace Swarm.Weapon
{
    public class MageIceBoltWeapon : LevelableWeapon
    {
        // Reused across calls so target selection allocates nothing per shot.
        private readonly List<Transform> _targetBuffer = new();

        [SerializeField] private ProjectileWeaponData data;
        [SerializeField] private int baseJumps = 2;
        [SerializeField] private int levelsPerExtraJump = 2;
        [SerializeField] private float chainRange = 4f;
        // Chain lightning's control is a slow, not a stun: it lands on every link instead of
        // proccing, so it has to be something a crowd can be left standing in.
        [SerializeField, Range(0f, 1f)] private float slowMultiplier = 0.7f;
        [SerializeField] private float slowDuration = 1.5f;
        [SerializeField] private Sprite boltSprite;
        [SerializeField] private Color boltColor = new(0.5f, 0.85f, 1f, 0.95f);
        [SerializeField] private Sprite[] travelFrames;
        [SerializeField] private float travelFrameDuration = 0.08f;
        [SerializeField] private float visualScale = 1f;
        [SerializeField] private Sprite[] hitEffectFrames;
        [SerializeField] private float hitEffectFrameDuration = 0.05f;
        [SerializeField] private float hitEffectScale = 1f;

        private float _timer;
        private PlayerStats _stats;
        private SpriteEffectPlayer _hitEffect;
        private RuntimeObjectPool _boltPool;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _boltPool = new RuntimeObjectPool(CreateBoltObject);
            _hitEffect = SpriteEffectPlayer.Create(
                this, "LightningHitEffect (Temp)", null, hitEffectFrames, hitEffectFrameDuration,
                initialScale: hitEffectScale);
        }

        private void OnDisable()
        {
            _hitEffect?.Hide();
        }

        private void Update()
        {
            if (data == null) return;

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
            var boltCount = Mathf.Max(1, 1 + (_stats != null ? _stats.ProjectileCountBonus : 0));
            EnemyTargeting.FindMultiple(origin, data.Range, boltCount, _targetBuffer);
            var targets = _targetBuffer;
            if (targets.Count == 0) return false;

            var damageMultiplier = DamageMultiplier * (_stats != null ? _stats.RollDamageMultiplier() : 1f);
            var isCritical = _stats != null && _stats.LastAttackWasCritical;
            var damage = Mathf.RoundToInt(data.Damage * damageMultiplier);
            var damageType = _stats != null ? _stats.DamageStatType : DamageStatType.AttackPower;
            var penetration = _stats != null ? _stats.GetPenetration() : 0f;
            var maxJumps = baseJumps + (Level - 1) / levelsPerExtraJump;

            for (var i = 0; i < targets.Count; i++)
            {
                SpawnBolt(origin, targets[i], damage, damageType, penetration, maxJumps, isCritical);
            }

            return true;
        }

        // The bare object only. Everything that differs per shot is re-applied in SpawnBolt, so
        // a recycled bolt is set up exactly like a fresh one.
        private GameObject CreateBoltObject()
        {
            var boltObject = new GameObject("IceBolt (Temp)");

            var spriteRenderer = boltObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerName = SortingLayers.EFFECT;
            spriteRenderer.sortingOrder = 0;

            boltObject.AddComponent<ChainLightningBolt>();
            return boltObject;
        }

        private void SpawnBolt(Vector2 origin, Transform target, int damage, DamageStatType damageType, float penetration, int maxJumps, bool isCritical)
        {
            var boltObject = _boltPool.Get();
            boltObject.transform.position = origin;
            boltObject.transform.localScale = Vector3.one * visualScale;
            boltObject.transform.rotation = Quaternion.identity;

            if (!boltObject.TryGetComponent<ChainLightningBolt>(out var bolt)) return;
            if (!boltObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer)) return;

            bolt.SetPool(_boltPool);
            bolt.Launch(target, damage, data.ProjectileSpeed, maxJumps, chainRange, slowMultiplier, slowDuration, penetration, damageType, PlayHitEffect, isCritical);

            if (travelFrames != null && travelFrames.Length > 0)
            {
                spriteRenderer.color = Color.white;
                spriteRenderer.sprite = travelFrames[0];
                bolt.SetTravelAnimation(spriteRenderer, travelFrames, travelFrameDuration);
            }
            else
            {
                spriteRenderer.sprite = boltSprite;
                spriteRenderer.color = boltColor;
            }
        }

        private void PlayHitEffect(Vector2 position)
        {
            _hitEffect?.Play(position, 0f, hitEffectScale);
        }
    }
}

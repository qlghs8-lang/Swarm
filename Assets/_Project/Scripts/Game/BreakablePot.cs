using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// A pot scattered around the arena that any weapon can smash for a chance at loot.
    ///
    /// It takes damage like an enemy — same layer, same tag, same <see cref="IDamageable"/> — and
    /// dies to a single hit whatever that hit was worth. Giving it health would mean the first
    /// minute of a run, when damage is lowest, is exactly when pots are least worth attacking,
    /// which is backwards: the reward is meant to pull the player towards them early.
    ///
    /// Not pooled and not cleaned up. <c>PotField</c> keeps adding new ones over the run, and a
    /// smashed one stays where it fell as rubble — a hundred idle SpriteRenderers over ten
    /// minutes, which is cheaper than the bookkeeping that removing them would need and leaves
    /// the arena showing where the player has already been.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class BreakablePot : MonoBehaviour, IDamageable
    {
        [Header("Art")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] breakFrames;
        [SerializeField] private float frameDuration = 0.07f;

        [Header("Drops")]
        [SerializeField] private GameObject experiencePickupPrefab;
        [SerializeField] private GameObject goldPickupPrefab;
        [SerializeField] private GameObject healthPackPrefab;
        [SerializeField] private GameObject magnetPickupPrefab;
        // One basic enemy's worth. A pot should read as a small bonus on the way past, not as a
        // reason to stop killing things.
        [SerializeField] private int experienceAmount = 5;
        [SerializeField] private int goldAmount = 3;
        [SerializeField] private float dropScatterRadius = 0.25f;

        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private bool _broken;
        private float _breakTime;
        private int _idleSortingOrder;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _idleSortingOrder = _renderer.sortingOrder;
            if (idleSprite != null) _renderer.sprite = idleSprite;

            BreakableRegistry.Register(_collider);

            // Switched off until Break() needs to step frames: an intact pot has nothing to do,
            // and there are a hundred of them standing at once.
            enabled = false;
        }

        private void OnDestroy()
        {
            BreakableRegistry.Unregister(_collider);
        }

        public void TakeDamage(int amount, DamageStatType damageType = DamageStatType.AttackPower,
                               float penetration = 0f, bool isCritical = false, float knockbackScale = 0f)
        {
            if (_broken) return;
            Break();
        }

        private void Break()
        {
            _broken = true;
            _breakTime = Time.time;

            BreakableRegistry.Unregister(_collider);
            _collider.enabled = false;

            // The pot is scenery once it is broken: dropping it below the props the arena scatters
            // keeps a rubble pile from covering a flower it happens to land on.
            _renderer.sortingOrder = _idleSortingOrder - 1;

            SpawnDrop();

            if (breakFrames == null || breakFrames.Length == 0)
            {
                // No art to play: hide it rather than leaving an intact-looking pot standing with
                // its collider off, which reads as a bug the player cannot interact with.
                _renderer.enabled = false;
                return;
            }

            _renderer.sprite = breakFrames[0];
            enabled = true;
        }

        private void Update()
        {
            var index = Mathf.FloorToInt((Time.time - _breakTime) / frameDuration);
            if (index >= breakFrames.Length)
            {
                // The last frame is a settled rubble pile: it stays on the ground for good and
                // this component goes quiet again.
                _renderer.sprite = breakFrames[breakFrames.Length - 1];
                enabled = false;
                return;
            }

            _renderer.sprite = breakFrames[index];
        }

        private void SpawnDrop()
        {
            var drop = PotDropTable.Roll();
            var prefab = drop switch
            {
                PotDrop.Experience => experiencePickupPrefab,
                PotDrop.Gold => goldPickupPrefab,
                PotDrop.HealthPack => healthPackPrefab,
                PotDrop.Magnet => magnetPickupPrefab,
                _ => null,
            };

            if (prefab == null) return;

            var offset = Random.insideUnitCircle * dropScatterRadius;
            var instance = SharedObjectPool.Get(prefab, transform.position + (Vector3)offset,
                                                Quaternion.identity);

            switch (drop)
            {
                case PotDrop.Experience when instance.TryGetComponent<ExperiencePickup>(out var experience):
                    experience.SetSourcePrefab(prefab);
                    experience.SetAmount(experienceAmount);
                    break;

                case PotDrop.Gold when instance.TryGetComponent<GoldPickup>(out var gold):
                    gold.SetSourcePrefab(prefab);
                    // Forced to the pile sprite: a pot is worth three coins, which is under the
                    // pile threshold an enemy drop uses, but at a 5% chance — the rarest drop
                    // after the capped magnet — it should read as the find that it is.
                    gold.SetAmount(goldAmount, true);
                    break;

                case PotDrop.HealthPack when instance.TryGetComponent<HealthPackPickup>(out var pack):
                    pack.SetSourcePrefab(prefab);
                    break;

                case PotDrop.Magnet when instance.TryGetComponent<MagnetPickup>(out var magnet):
                    magnet.SetSourcePrefab(prefab);
                    break;
            }
        }
    }
}

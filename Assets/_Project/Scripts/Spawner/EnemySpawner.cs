using System.Collections.Generic;
using Swarm.Enemy;
using Swarm.Weapon;
using UnityEngine;

namespace Swarm.Spawner
{
    public class EnemySpawner : MonoBehaviour
    {
        [System.Serializable]
        private struct EnemySpawnEntry
        {
            public GameObject prefab;
            public int weight;
            public float unlockTime;
        }

        [SerializeField] private EnemySpawnEntry[] enemyEntries;
        [SerializeField] private float spawnInterval = 1.5f;
        [SerializeField] private float spawnIntervalMin = 0.5f;
        [SerializeField] private float spawnRateRampDuration = 480f;
        [SerializeField] private float spawnRadius = 8f;

        [Header("Crowd control")]
        // Nothing removed enemies except killing them, so anything slower than the player (the
        // tank moves at 1 against the player's 4) trailed behind forever and the live count only
        // ever grew. Raising the spawn rate on top of that makes the count diverge instead of
        // settling, which breaks both the frame rate and any attempt to measure difficulty.
        [SerializeField] private int maxActiveEnemies = 150;
        [SerializeField] private float recycleDistance = 22f;
        [SerializeField] private float recycleCheckInterval = 0.5f;

        [Header("Difficulty")]
        [SerializeField] private float maxHealthMultiplierEnd = 2.5f;
        [SerializeField] private float defenseBonusEnd = 0.25f;
        [SerializeField] private float magicDefenseBonusEnd = 0.25f;
        [SerializeField] private float difficultyRampDuration = 600f;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private float bossSpawnTime = 600f;
        [SerializeField] private float[] waveTimes = { 180f, 360f, 540f };
        [SerializeField] private int[] waveBurstCounts = { 8, 14, 20 };

        public event System.Action<EnemyHealth> OnBossSpawned;
        public event System.Action<int> OnWaveTriggered;

        /// <summary>Live enemies this spawner is responsible for. Excludes the boss and any
        /// enemies placed by other systems, such as the test stage's training dummies.</summary>
        public int ActiveEnemyCount => _active.Count;

        private Transform _target;
        private float _timer;
        private float _elapsedTime;
        private float _recycleTimer;
        private int _nextWaveIndex;
        private bool _bossSpawned;
        private readonly Dictionary<GameObject, ObjectPool> _pools = new();
        private readonly List<GameObject> _active = new();

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;

            foreach (var entry in enemyEntries)
            {
                if (entry.prefab == null) continue;

                _pools[entry.prefab] = new ObjectPool(entry.prefab);
            }
        }

        private void Update()
        {
            if (_target == null) return;

            _elapsedTime += Time.deltaTime;

            var rampT = Mathf.Clamp01(_elapsedTime / spawnRateRampDuration);
            var effectiveInterval = Mathf.Lerp(spawnInterval, spawnIntervalMin, rampT);

            _timer += Time.deltaTime;
            if (_timer >= effectiveInterval)
            {
                _timer = 0f;
                SpawnEnemy();
            }

            _recycleTimer += Time.deltaTime;
            if (_recycleTimer >= recycleCheckInterval)
            {
                _recycleTimer = 0f;
                SweepActiveEnemies();
            }

            CheckWaveTriggers();
            CheckBossSpawn();
        }

        /// <summary>
        /// Drops enemies that have died back out of the tracking list, and teleports the ones that
        /// have fallen too far behind to a fresh position around the player. Recycling rather than
        /// despawning keeps the pressure — and the experience and gold they are still carrying —
        /// in play; the distance is far enough off-screen that the move is never visible.
        /// </summary>
        private void SweepActiveEnemies()
        {
            var recycleDistanceSqr = recycleDistance * recycleDistance;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var enemy = _active[i];

                // Released to the pool (killed) or destroyed outright.
                if (enemy == null || !enemy.activeInHierarchy)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                var offset = (Vector2)enemy.transform.position - (Vector2)_target.position;
                if (offset.sqrMagnitude < recycleDistanceSqr) continue;

                MoveTo(enemy, GetSpawnPosition());
            }
        }

        private Vector2 GetSpawnPosition()
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            return (Vector2)_target.position + offset;
        }

        // Physics2D.autoSyncTransforms is off, so moving only the Transform would leave the
        // collider registered at the old position until the next physics step — long enough for a
        // weapon's targeting query to find this enemy back where it came from.
        private static void MoveTo(GameObject enemy, Vector2 position)
        {
            enemy.transform.position = position;
            if (enemy.TryGetComponent<Rigidbody2D>(out var body))
            {
                body.position = position;
            }
        }

        private void CheckWaveTriggers()
        {
            if (_nextWaveIndex >= waveTimes.Length) return;
            if (_elapsedTime < waveTimes[_nextWaveIndex]) return;

            var burstCount = _nextWaveIndex < waveBurstCounts.Length
                ? waveBurstCounts[_nextWaveIndex]
                : waveBurstCounts[^1];

            for (var i = 0; i < burstCount; i++)
            {
                // A wave is a deliberate spike, so it is allowed past the cap rather than being
                // silently swallowed when the field is already full.
                SpawnEnemy(ignoreCap: true);
            }

            OnWaveTriggered?.Invoke(_nextWaveIndex + 1);
            _nextWaveIndex++;
        }

        private void CheckBossSpawn()
        {
            if (_bossSpawned || bossPrefab == null || _elapsedTime < bossSpawnTime) return;

            _bossSpawned = true;

            var instance = Instantiate(bossPrefab, GetSpawnPosition(), Quaternion.identity);
            if (instance.TryGetComponent<EnemyHealth>(out var health))
            {
                health.ResetHealth();
                OnBossSpawned?.Invoke(health);
            }
        }

        private void SpawnEnemy(bool ignoreCap = false)
        {
            if (!ignoreCap && _active.Count >= maxActiveEnemies) return;

            var prefab = PickWeightedPrefab();
            if (prefab == null) return;

            var pool = _pools[prefab];
            var instance = pool.Get(GetSpawnPosition(), Quaternion.identity);

            if (instance.TryGetComponent<EnemyHealth>(out var health))
            {
                health.SetPool(pool);

                var difficultyT = Mathf.Clamp01(_elapsedTime / difficultyRampDuration);
                var healthMultiplier = Mathf.Lerp(1f, maxHealthMultiplierEnd, difficultyT);
                var defenseBonus = Mathf.Lerp(0f, defenseBonusEnd, difficultyT);
                var magicDefenseBonus = Mathf.Lerp(0f, magicDefenseBonusEnd, difficultyT);
                health.ApplyDifficulty(healthMultiplier, defenseBonus, magicDefenseBonus);

                health.ResetHealth();
            }

            _active.Add(instance);
        }

        private GameObject PickWeightedPrefab()
        {
            var totalWeight = 0;
            foreach (var entry in enemyEntries)
            {
                if (entry.prefab == null || entry.unlockTime > _elapsedTime) continue;
                totalWeight += entry.weight;
            }

            if (totalWeight <= 0) return null;

            var roll = Random.Range(0, totalWeight);
            var cumulative = 0;

            foreach (var entry in enemyEntries)
            {
                if (entry.prefab == null || entry.unlockTime > _elapsedTime) continue;

                cumulative += entry.weight;
                if (roll < cumulative) return entry.prefab;
            }

            return null;
        }
    }
}

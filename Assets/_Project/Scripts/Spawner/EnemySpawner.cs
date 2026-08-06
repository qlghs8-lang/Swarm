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

        private Transform _target;
        private float _timer;
        private float _elapsedTime;
        private int _nextWaveIndex;
        private bool _bossSpawned;
        private readonly Dictionary<GameObject, ObjectPool> _pools = new();

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

            CheckWaveTriggers();
            CheckBossSpawn();
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
                SpawnEnemy();
            }

            OnWaveTriggered?.Invoke(_nextWaveIndex + 1);
            _nextWaveIndex++;
        }

        private void CheckBossSpawn()
        {
            if (_bossSpawned || bossPrefab == null || _elapsedTime < bossSpawnTime) return;

            _bossSpawned = true;

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            var spawnPosition = (Vector2)_target.position + offset;

            var instance = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
            if (instance.TryGetComponent<EnemyHealth>(out var health))
            {
                health.ResetHealth();
                OnBossSpawned?.Invoke(health);
            }
        }

        private void SpawnEnemy()
        {
            var prefab = PickWeightedPrefab();
            if (prefab == null) return;

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            var spawnPosition = (Vector2)_target.position + offset;

            var pool = _pools[prefab];
            var instance = pool.Get(spawnPosition, Quaternion.identity);

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

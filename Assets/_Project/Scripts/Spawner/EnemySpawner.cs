using System.Collections.Generic;
using Swarm.Arena;
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

        [System.Serializable]
        private struct WaveDefinition
        {
            [Tooltip("화면에 띄울 문구. 비우면 WAVE n")] public string label;
            public float time;
            public int count;
            [Tooltip("비우면 일반 스폰 풀에서 추첨")] public GameObject prefab;
            [Tooltip("0이면 사방. 90이면 무작위 한 방향 ±45도에 몰아서 스폰")] public float arcDegrees;
        }

        [SerializeField] private EnemySpawnEntry[] enemyEntries;
        [SerializeField] private float spawnInterval = 1.5f;
        [SerializeField] private float spawnIntervalMin = 0.5f;
        [SerializeField] private float spawnRateRampDuration = 480f;
        [SerializeField] private float spawnRadius = 8f;

        // One enemy radius plus a little, so nothing spawns clipping the wall.
        private const float SpawnBoundaryInset = 1.5f;

        // Enemies used to appear evenly all around, so running in a straight line simply outran
        // half of them and parted the rest — the player could never actually be enclosed. Biasing
        // spawns toward where the player is heading means the ground they flee onto already has
        // enemies on it, which is what makes being surrounded possible at all.
        [SerializeField, Range(0f, 1f)] private float forwardSpawnBias = 0.6f;
        [SerializeField] private float forwardArcDegrees = 180f;

        [Header("Crowd control")]
        // Nothing removed enemies except killing them, so anything slower than the player (the
        // tank moves at 1 against the player's 4) trailed behind forever and the live count only
        // ever grew. Raising the spawn rate on top of that makes the count diverge instead of
        // settling, which breaks both the frame rate and any attempt to measure difficulty.
        // 150 was a guess; measured at ~2.2ms/frame (400-500 FPS) with 150 on screen, so the
        // cap was nowhere near the performance ceiling. Raised so the field can reach its real
        // equilibrium instead of being clipped by the cap before it gets there.
        [SerializeField] private int maxActiveEnemies = 400;
        [SerializeField] private float recycleDistance = 22f;
        [SerializeField] private float recycleCheckInterval = 0.5f;

        [Header("Difficulty")]
        [SerializeField] private float maxHealthMultiplierEnd = 2.5f;
        [SerializeField] private float defenseBonusEnd = 0.25f;
        [SerializeField] private float magicDefenseBonusEnd = 0.25f;
        [SerializeField] private float difficultyRampDuration = 600f;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private float bossSpawnTime = 600f;
        [SerializeField] private WaveDefinition[] waves;

        [Header("Experience")]
        // Stepped rather than continuous. A drop worth a fraction more every second reads as
        // noise; a jump every couple of minutes reads as progress, and the orb changes tier at
        // the same moment so the step is visible on the ground rather than only in the bar.
        [SerializeField] private float experienceMultiplierStart = 1.5f;
        [SerializeField] private float experienceMultiplierEnd = 2.6f;
        [SerializeField] private int experienceStageCount = 5;
        [SerializeField] private float experienceRampDuration = 600f;

        public event System.Action<EnemyHealth> OnBossSpawned;
        public event System.Action<string> OnWaveTriggered;

        /// <summary>Live enemies this spawner is responsible for. Excludes the boss and any
        /// enemies placed by other systems, such as the test stage's training dummies.</summary>
        public int ActiveEnemyCount => _active.Count;

        private Transform _target;
        private Rigidbody2D _targetBody;
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
            if (player != null)
            {
                _target = player.transform;
                player.TryGetComponent(out _targetBody);
            }

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
            var heading = _targetBody != null ? _targetBody.linearVelocity : Vector2.zero;

            if (heading.sqrMagnitude > 0.25f && Random.value < forwardSpawnBias)
            {
                var headingDegrees = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
                return PositionOnArc(headingDegrees, forwardArcDegrees);
            }

            return PositionOnArc(Random.Range(0f, 360f), 360f);
        }

        /// <summary>A point on the spawn ring, within <paramref name="arcDegrees"/> centred on
        /// <paramref name="centreDegrees"/>. A full 360 arc is the old even ring.</summary>
        private Vector2 PositionOnArc(float centreDegrees, float arcDegrees)
        {
            var half = Mathf.Max(0f, arcDegrees) * 0.5f;
            var degrees = centreDegrees + Random.Range(-half, half);
            var radians = degrees * Mathf.Deg2Rad;
            var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * spawnRadius;
            var position = (Vector2)_target.position + offset;

            // Against the wall, half the ring is outside the arena. Mirroring the offset back
            // inside is what makes being cornered dangerous: the crowd arrives from the open side
            // rather than politely spawning behind the stones.
            if (!ArenaBounds.Contains(position, SpawnBoundaryInset))
            {
                var mirrored = (Vector2)_target.position - offset;
                position = ArenaBounds.Contains(mirrored, SpawnBoundaryInset)
                    ? mirrored
                    : ArenaBounds.Clamp(position, SpawnBoundaryInset);
            }

            return position;
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
            if (waves == null || _nextWaveIndex >= waves.Length) return;
            if (_elapsedTime < waves[_nextWaveIndex].time) return;

            var wave = waves[_nextWaveIndex];

            // One direction is picked for the whole wave, so a wave with a narrow arc arrives as a
            // single mass from one side instead of trickling in from everywhere at once.
            var centreDegrees = Random.Range(0f, 360f);
            var arc = wave.arcDegrees > 0f ? wave.arcDegrees : 360f;

            for (var i = 0; i < wave.count; i++)
            {
                // A wave is a deliberate spike, so it is allowed past the cap rather than being
                // silently swallowed when the field is already full.
                SpawnEnemy(ignoreCap: true, prefabOverride: wave.prefab,
                           position: PositionOnArc(centreDegrees, arc));
            }

            var label = string.IsNullOrWhiteSpace(wave.label) ? $"WAVE {_nextWaveIndex + 1}" : wave.label;
            OnWaveTriggered?.Invoke(label);
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

        private void SpawnEnemy(bool ignoreCap = false, GameObject prefabOverride = null, Vector2? position = null)
        {
            if (!ignoreCap && _active.Count >= maxActiveEnemies) return;

            var prefab = prefabOverride != null ? prefabOverride : PickWeightedPrefab();
            if (prefab == null) return;

            // A wave prefab need not be in the regular rotation — the fast enemy is wave-only —
            // so its pool is created on demand rather than only in Start.
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool(prefab);
                _pools[prefab] = pool;
            }

            var instance = pool.Get(position ?? GetSpawnPosition(), Quaternion.identity);

            if (instance.TryGetComponent<EnemyHealth>(out var health))
            {
                health.SetPool(pool);

                var difficultyT = Mathf.Clamp01(_elapsedTime / difficultyRampDuration);
                var healthMultiplier = Mathf.Lerp(1f, maxHealthMultiplierEnd, difficultyT);
                var defenseBonus = Mathf.Lerp(0f, defenseBonusEnd, difficultyT);
                var magicDefenseBonus = Mathf.Lerp(0f, magicDefenseBonusEnd, difficultyT);
                health.ApplyDifficulty(healthMultiplier, defenseBonus, magicDefenseBonus);
                health.SetExperienceMultiplier(GetExperienceMultiplier());

                health.ResetHealth();
            }

            _active.Add(instance);
        }

        /// <summary>The drop multiplier for the stage the run is currently in. Applied at spawn,
        /// so an enemy recycled by <see cref="SweepActiveEnemies"/> keeps the value it was born
        /// with — deliberately the conservative direction, since a stale value is only ever lower
        /// than the current one.</summary>
        private float GetExperienceMultiplier()
        {
            if (experienceStageCount <= 1) return experienceMultiplierStart;

            var stageDuration = experienceRampDuration / experienceStageCount;
            var stage = Mathf.Clamp(Mathf.FloorToInt(_elapsedTime / stageDuration), 0, experienceStageCount - 1);
            return Mathf.Lerp(experienceMultiplierStart, experienceMultiplierEnd,
                              stage / (float)(experienceStageCount - 1));
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

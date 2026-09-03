using System.Collections;
using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>
    /// Rocks called down onto the player's own position, three per cast. Where the slam punishes
    /// standing next to the boss, this one reaches the whole arena, so ranged builds cannot simply
    /// pick a corner and ignore the fight. Each rock aims at wherever the player is standing when
    /// its warning appears, which means the answer is always the same and always available: keep
    /// moving. Standing still is what gets punished, not being in the wrong place.
    /// </summary>
    public class BossMeteorAttack : MonoBehaviour, IBossPattern
    {
        [Header("Timing")]
        [Tooltip("패턴 시전 쿨타임(초). 마지막 돌이 떨어진 뒤부터 다시 셉니다.")]
        [SerializeField] private float interval = 10f;
        [Tooltip("한 번 시전할 때 떨어지는 돌의 수")]
        [SerializeField] private int dropCount = 3;
        [Tooltip("경고 원이 뜨고 돌이 떨어지기까지의 시간")]
        [SerializeField] private float warningDuration = 0.85f;
        [Tooltip("돌과 돌 사이의 간격. warningDuration보다 짧으면 경고가 겹쳐서 뜹니다.")]
        [SerializeField] private float delayBetweenDrops = 0.55f;
        [Tooltip("보스가 등장한 뒤 첫 시전까지의 시간")]
        [SerializeField] private float firstCastDelay = 5f;

        [Header("Impact")]
        // Kept in step with the width of the impact art (bossrock.aseprite, scaled by
        // BossRockSetup): the telegraph promises exactly the area the dust covers, so a player who
        // reads the circle and a player who reads the explosion learn the same rule. Changing this
        // without rescaling the effect puts the two back out of agreement.
        [Tooltip("피해 반경. Effect_BossRock_Impact의 먼지 폭과 맞춰져 있습니다.")]
        [SerializeField] private float radius = 1.8f;
        [SerializeField] private int damage = 30;
        [Tooltip("조준 위치에 더해지는 무작위 오차. 0이면 플레이어 발밑에 정확히 떨어집니다.")]
        [SerializeField] private float aimScatter = 0.6f;
        [SerializeField] private Color warningColor = new(1f, 0.55f, 0.1f, 0.35f);

        [Header("Effects (optional)")]
        [Tooltip("떨어지는 돌. 경고와 함께 위쪽에서 생성되어 착지 지점까지 내려옵니다.")]
        [SerializeField] private GameObject fallingRockPrefab;
        [SerializeField] private float fallHeight = 12f;
        [Tooltip("착지 순간 재생할 이펙트")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectLifetime = 1.5f;

        private float _timer;
        private Transform _player;
        private BossTelegraph[] _telegraphs;
        private int _dropsResolving;

        public bool IsCasting { get; private set; }

        private void Awake()
        {
            // One telegraph per drop, because a short delayBetweenDrops means several warnings are
            // on screen at the same time and they cannot share a single circle.
            _timer = Mathf.Max(0f, interval - firstCastDelay);

            var count = Mathf.Max(1, dropCount);
            _telegraphs = new BossTelegraph[count];
            for (var i = 0; i < count; i++)
            {
                _telegraphs[i] = new BossTelegraph($"BossMeteorTelegraph {i} (Temp)", warningColor);
            }
        }

        private void OnDestroy()
        {
            if (_telegraphs == null) return;
            foreach (var telegraph in _telegraphs) telegraph?.Destroy();
        }

        private void Update()
        {
            if (IsCasting) return;

            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject == null) return;
                _player = playerObject.transform;
            }

            _timer += Time.deltaTime;
            if (_timer < interval) return;

            // The timer is left running past the interval so a cast held back by another pattern
            // starts the moment the boss is free rather than losing a whole cycle.
            if (BossPatternGate.IsAnyPatternCasting(gameObject, this)) return;

            _timer = 0f;
            StartCoroutine(MeteorRoutine());
        }

        private IEnumerator MeteorRoutine()
        {
            IsCasting = true;
            _dropsResolving = 0;

            var count = Mathf.Min(Mathf.Max(1, dropCount), _telegraphs.Length);
            for (var i = 0; i < count; i++)
            {
                StartCoroutine(DropRoutine(_telegraphs[i]));
                if (i < count - 1) yield return new WaitForSeconds(delayBetweenDrops);
            }

            // The cooldown starts once the last rock has actually landed, so a long warning or a
            // wide drop spacing does not quietly eat into the player's recovery time.
            while (_dropsResolving > 0) yield return null;

            IsCasting = false;
        }

        private IEnumerator DropRoutine(BossTelegraph telegraph)
        {
            _dropsResolving++;

            var target = (Vector3)AimPoint();
            telegraph.Show(target, radius);

            var rock = SpawnFallingRock(target);

            var elapsed = 0f;
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / warningDuration;
                telegraph.SetFill(t);

                if (rock != null)
                {
                    // Eased so the rock accelerates into the ground instead of drifting down at a
                    // constant speed, which reads as floating rather than falling.
                    rock.position = target + Vector3.up * (fallHeight * (1f - t * t));
                }

                yield return null;
            }

            telegraph.Hide();
            if (rock != null) Destroy(rock.gameObject);

            SpawnImpactEffect(target);
            BossAttackHits.DamagePlayersInCircle(target, radius, damage);

            _dropsResolving--;
        }

        private Vector2 AimPoint()
        {
            var origin = _player != null ? (Vector2)_player.position : (Vector2)transform.position;
            if (aimScatter <= 0f) return origin;

            return origin + Random.insideUnitCircle * aimScatter;
        }

        private Transform SpawnFallingRock(Vector3 target)
        {
            if (fallingRockPrefab == null) return null;

            var rock = Instantiate(fallingRockPrefab, target + Vector3.up * fallHeight, Quaternion.identity);
            return rock.transform;
        }

        private void SpawnImpactEffect(Vector3 position)
        {
            if (impactEffectPrefab == null) return;

            var effect = Instantiate(impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, impactEffectLifetime);
        }
    }
}

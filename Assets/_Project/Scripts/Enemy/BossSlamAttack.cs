using System.Collections;
using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>
    /// The boss's ground slam: a wide circle centred on the boss, telegraphed, then a single
    /// instant of damage. Melee characters stand inside its reach by definition, so the circle
    /// is deliberately large and the cooldown correspondingly long — the pattern is a periodic
    /// demand to disengage, not a tax on every second spent in melee.
    /// </summary>
    public class BossSlamAttack : MonoBehaviour, IBossPattern
    {
        // The boss's own body collider has a radius of ~3.45, so the original 3-unit slam never
        // reached past its own sprite: anything the collider pushed out of the way was already out
        // of range and the attack could not land on a player who was simply standing next to the
        // boss. The reach now extends well beyond the body, and the telegraph is long enough that
        // running outward the moment the circle appears still clears it — a dodge, not a coin flip.
        [SerializeField] private float interval = 7f;
        [SerializeField] private float radius = 8f;
        [SerializeField] private int damage = 25;
        [SerializeField] private float telegraphDuration = 1f;
        [SerializeField] private Color telegraphColor = new(1f, 0.2f, 0.2f, 0.35f);
        [Tooltip("보스가 등장한 뒤 첫 슬램까지의 시간. 쿨타임 전체를 기다리면 등장 직후가 무방비로 비어 보입니다.")]
        [SerializeField] private float firstCastDelay = 3f;

        // The slam lands where the circle was planted, so a boss that keeps chasing during the
        // wind-up is walking away from its own attack: the player only has to backpedal and the
        // pattern misses on its own. Planting the boss for the cast makes the telegraph a promise
        // it stays behind, and reads as a wind-up rather than an effect that happens to fire.
        [Tooltip("시전 중 보스가 제자리에 멈춥니다. 끄면 예전처럼 시전 중에도 계속 추적합니다.")]
        [SerializeField] private bool holdPositionWhileCasting = true;

        [Tooltip("착지 직후 보스가 다시 움직이기까지의 경직 시간.")]
        [SerializeField] private float recoveryDuration = 0.4f;

        // Standing still while the walk cycle keeps striding reads as a bug rather than a wind-up:
        // the boss moonwalks in place. The animator is parked on the first frame of whatever state
        // it is in, which for the boss is the walk clip's neutral standing pose.
        [Tooltip("시전 중 보스 애니메이션을 정지하고 첫 프레임(정지 포즈)으로 고정합니다.")]
        [SerializeField] private bool freezeAnimationWhileCasting = true;

        // The impact art (bossslam.aseprite) is drawn in 3/4 perspective, so its dust disc is an
        // ellipse roughly 0.6 as tall as it is wide. A flat circular telegraph under it left the
        // top and bottom of the warning sticking out past the dust, which reads as the attack
        // missing the area it promised. Telegraph and damage are squashed together so the warning
        // stays an exact statement of what the slam hits.
        [Tooltip("경고 원과 피격 판정의 세로 납작함. 1이면 정원, 0.6이면 이펙트의 먼지 타원과 일치합니다.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float perspectiveSquash = 0.6f;

        [Header("Effects (optional)")]
        [Tooltip("착지 순간 재생할 이펙트. 비우면 이펙트 없이 판정만 발생합니다.")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectLifetime = 1.5f;

        // bossslam.aseprite is drawn with its dust disc sitting about 32 of the canvas's 512 pixels
        // below the middle, so spawning the sprite straight on the impact point drew the dust a
        // full unit lower than the telegraph circle it is supposed to cover -- the boss appeared to
        // slam a patch of ground in front of its feet. The art is lifted by that difference so its
        // disc centre and the damage ellipse are the same point again. BossSlamSetup measures the
        // value from the canvas constants and writes it here.
        [Tooltip("이펙트 스폰 위치 보정. 아트의 먼지 원 중심이 캔버스 중앙과 어긋난 만큼을 상쇄합니다.")]
        [SerializeField] private Vector2 impactEffectOffset = new(0f, 1.06f);

        private float _timer;
        private BossTelegraph _telegraph;
        private EnemyChaser _chaser;
        private Animator _animator;
        private bool _movementHeld;
        private float _animatorSpeed = 1f;

        public bool IsCasting { get; private set; }

        private void Awake()
        {
            _telegraph = new BossTelegraph("BossSlamTelegraph (Temp)", telegraphColor,
                                           verticalSquash: perspectiveSquash);
            _timer = Mathf.Max(0f, interval - firstCastDelay);
            TryGetComponent(out _chaser);
            TryGetComponent(out _animator);
        }

        private void OnDisable()
        {
            // A boss killed mid-cast stops its coroutine wherever it happens to be, so the hold is
            // released here as well -- otherwise a pooled body would come back planted forever.
            ReleaseMovement();
            _telegraph?.Hide();
            IsCasting = false;
        }

        private void OnDestroy()
        {
            _telegraph?.Destroy();
        }

        private void Update()
        {
            if (IsCasting) return;

            _timer += Time.deltaTime;
            if (_timer < interval) return;

            // Hold the timer at the cooldown rather than resetting it, so a slam delayed by
            // another pattern fires the instant the boss is free instead of waiting a full
            // extra cycle.
            if (BossPatternGate.IsAnyPatternCasting(gameObject, this)) return;

            _timer = 0f;
            StartCoroutine(SlamRoutine());
        }

        private IEnumerator SlamRoutine()
        {
            IsCasting = true;
            HoldMovement();

            // The circle is planted where the boss stands at the start of the wind-up and the
            // damage is resolved against that same point. Reading the damage off the boss instead
            // let it drift up to a couple of units during the telegraph while chasing the player,
            // so the attack landed somewhere other than where it was drawn.
            _telegraph.Show(transform.position, radius);

            var elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                elapsed += Time.deltaTime;
                _telegraph.SetFill(elapsed / telegraphDuration);
                yield return null;
            }

            var impactPoint = _telegraph.Position;
            _telegraph.Hide();

            SpawnImpactEffect(impactPoint);
            BossAttackHits.DamagePlayersInEllipse(impactPoint, radius, perspectiveSquash, damage);

            // The boss stays planted for a beat after the impact. Snapping straight back into the
            // chase on the frame the damage resolves erases the reward for dodging, since the boss
            // is already closing again before the player has finished moving out.
            if (recoveryDuration > 0f) yield return new WaitForSeconds(recoveryDuration);

            ReleaseMovement();
            IsCasting = false;
        }

        private void HoldMovement()
        {
            if (!holdPositionWhileCasting || _movementHeld) return;

            _movementHeld = true;

            if (_chaser != null) _chaser.HoldMovement();
            FreezeAnimation();
        }

        private void ReleaseMovement()
        {
            if (!_movementHeld) return;

            _movementHeld = false;

            if (_chaser != null) _chaser.ReleaseMovement();
            ResumeAnimation();
        }

        /// <summary>Parks the animator on the first frame of its current state and stops it there.
        /// Rewinding rather than freezing in place is what makes the pose static and identical on
        /// every cast instead of whichever stride the walk happened to be mid-way through.</summary>
        private void FreezeAnimation()
        {
            if (!freezeAnimationWhileCasting || _animator == null) return;
            if (!_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;

            _animatorSpeed = _animator.speed;

            var state = _animator.GetCurrentAnimatorStateInfo(0);
            _animator.Play(state.fullPathHash, 0, 0f);
            // Play only queues the state; without this the frame is not written until the animator
            // next advances, which at speed 0 would leave the old stride on screen for a frame.
            _animator.Update(0f);
            _animator.speed = 0f;
        }

        private void ResumeAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            _animator.speed = _animatorSpeed <= 0f ? 1f : _animatorSpeed;
        }

        private void SpawnImpactEffect(Vector3 position)
        {
            if (impactEffectPrefab == null) return;

            var effect = Instantiate(impactEffectPrefab, position + (Vector3)impactEffectOffset,
                                     Quaternion.identity);
            Destroy(effect, impactEffectLifetime);
        }
    }
}

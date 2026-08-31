using UnityEngine;

namespace Swarm.Weapon
{
    // The fireball art is a one-shot birth -> peak -> burnout sequence, not a loop. Cycling the whole
    // strip made the projectile visibly re-ignite five times per flight, and its core snapped
    // backwards on every wrap (frames advance the core to the right, so frame N -> frame 0 reads as
    // the flame jumping back against the direction of travel).
    //
    // The strip is split into three roles instead:
    //   intro  - plays once on launch (ignition)
    //   cruise - ping-pongs for the rest of the flight (frames that differ only slightly, so this
    //            reads as flicker rather than a restart)
    //   expire - driven by distance travelled, not time, so the flame thins out as the projectile
    //            reaches the end of its range
    [RequireComponent(typeof(SpriteRenderer))]
    public class FireballAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;

        [Header("Intro (plays once on launch)")]
        [SerializeField] private int introFrameCount = 1;
        [SerializeField] private float introFrameDuration = 0.05f;

        [Header("Cruise (ping-pongs during flight)")]
        [SerializeField] private int cruiseFrameCount = 2;
        [SerializeField] private float cruiseFrameDuration = 0.08f;

        [Header("Expire (driven by travel progress)")]
        [SerializeField] private int expireFrameCount = 1;
        [Range(0f, 1f)]
        [SerializeField] private float expireStartProgress = 0.8f;

        private SpriteRenderer _spriteRenderer;
        private Projectile _projectile;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _projectile = GetComponent<Projectile>();
        }

        private void OnEnable()
        {
            _elapsed = 0f;

            // Pooled instances are re-enabled before Launch() runs, so show the first frame
            // immediately instead of leaving last flight's burnout sprite on screen for one frame.
            if (frames != null && frames.Length > 0 && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = frames[0];
            }
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            _elapsed += Time.deltaTime;
            _spriteRenderer.sprite = frames[ResolveFrameIndex()];
        }

        private int ResolveFrameIndex()
        {
            var introCount = Mathf.Clamp(introFrameCount, 0, frames.Length);
            var cruiseCount = Mathf.Clamp(cruiseFrameCount, 0, frames.Length - introCount);
            var expireCount = Mathf.Clamp(expireFrameCount, 0, frames.Length - introCount - cruiseCount);

            // Burnout takes priority: once the projectile is nearly out of range it should thin out
            // regardless of where the cruise ping-pong happens to be.
            if (expireCount > 0 && _projectile != null && expireStartProgress < 1f)
            {
                var progress = _projectile.TravelProgress;
                if (progress >= expireStartProgress)
                {
                    var t = Mathf.InverseLerp(expireStartProgress, 1f, progress);
                    var step = Mathf.Clamp(Mathf.FloorToInt(t * expireCount), 0, expireCount - 1);
                    return introCount + cruiseCount + step;
                }
            }

            var introDuration = introCount * Mathf.Max(0.0001f, introFrameDuration);
            if (introCount > 0 && _elapsed < introDuration)
            {
                var step = Mathf.FloorToInt(_elapsed / Mathf.Max(0.0001f, introFrameDuration));
                return Mathf.Clamp(step, 0, introCount - 1);
            }

            if (cruiseCount <= 0)
            {
                return Mathf.Clamp(introCount - 1, 0, frames.Length - 1);
            }

            var cruiseStep = Mathf.FloorToInt((_elapsed - introDuration) / Mathf.Max(0.0001f, cruiseFrameDuration));
            return introCount + PingPong(cruiseStep, cruiseCount);
        }

        // 0,1,2,1,0,1,2... - with two frames this is a plain alternation, which is the common case.
        private static int PingPong(int step, int count)
        {
            if (count <= 1) return 0;

            var period = count * 2 - 2;
            var position = ((step % period) + period) % period;
            return position < count ? position : period - position;
        }
    }
}

using Swarm.Game;
using UnityEngine;

namespace Swarm.Arena
{
    /// <summary>
    /// Seeds the arena with pots and keeps adding more as the run goes on.
    ///
    /// The field grows rather than being topped up to a fixed number. A field held at a constant
    /// count would make the arena worth exactly as much in the tenth minute as in the first, and
    /// the pots the player already smashed would be the reason nothing new appears. Growing it
    /// means the ground behind them refills while they are away, so walking a lap is worth more
    /// the longer they have been running — which is the direction the danger curve moves too.
    ///
    /// A smashed pot is not replaced and not removed: its last frame stays on the ground as
    /// rubble, which is what makes "I have already been here" readable at a glance and stops the
    /// growth from erasing the record of the run.
    /// </summary>
    public class PotField : MonoBehaviour
    {
        // The camera is orthographic size 6, so it shows roughly 21x12 units of a 70 unit arena.
        // Thirty is thin on purpose: the opening minute should have the player finding pots on
        // the way to somewhere, not walking a field of them.
        private const int InitialCount = 30;

        private const float TrickleInterval = 20f;
        private const int TrickleCount = 1;

        private const float BurstInterval = 60f;
        private const int BurstCount = 5;

        // Kept off the centre sigil and out of the pocket the player starts in, matching the
        // clearance ArenaBuilder gives its props, and a little short of the wall so a pot never
        // ends up half-buried in the stones.
        private const float CentreClearance = 7f;
        private const float EdgeInset = 2.5f;

        // Well past the ~10.6 units of arena the camera shows either side of the player, so a pot
        // never appears in view. Random placement over an arena this size would land in view only
        // about one time in fifty anyway — but that one time reads as the game dropping loot on
        // the player's head, and it is the difference between a spawn they walked to and one that
        // came to them.
        private const float MinPlayerDistance = 18f;

        private const int Seed = 20260903;

        private GameObject _prefab;
        private System.Random _random;
        private Transform _player;
        private float _trickleTimer;
        private float _burstTimer;

        public void Build(GameObject prefab)
        {
            _prefab = prefab;
            _random = new System.Random(Seed);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;

            // The opening field ignores the player-distance rule: the run has not started, the
            // player is at the centre, and holding pots 18 units out would leave a bare ring
            // around the one place they are guaranteed to be looking.
            for (var i = 0; i < InitialCount; i++)
            {
                Spawn(RandomPoint());
            }
        }

        private void Update()
        {
            var delta = Time.deltaTime;

            _trickleTimer += delta;
            if (_trickleTimer >= TrickleInterval)
            {
                _trickleTimer -= TrickleInterval;
                SpawnBatch(TrickleCount);
            }

            _burstTimer += delta;
            if (_burstTimer >= BurstInterval)
            {
                _burstTimer -= BurstInterval;
                SpawnBatch(BurstCount);
            }
        }

        private void SpawnBatch(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Spawn(RandomPointAwayFromPlayer());
            }
        }

        private void Spawn(Vector3 position)
        {
            if (_prefab == null) return;

            Instantiate(_prefab, position, Quaternion.identity, transform);
        }

        private Vector3 RandomPointAwayFromPlayer()
        {
            if (_player == null) return RandomPoint();

            var minDistanceSqr = MinPlayerDistance * MinPlayerDistance;

            // Bounded rather than a while(true): with a 70 unit arena and an 18 unit exclusion
            // almost every draw passes on the first try, and a player standing somewhere that
            // rejects every draw must not hang the frame.
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var point = RandomPoint();
                if (((Vector2)point - (Vector2)_player.position).sqrMagnitude >= minDistanceSqr)
                {
                    return point;
                }
            }

            // Last resort: straight out from the player in some direction, clamped inside the
            // wall. Being off screen is the property that matters here; an even spread is not.
            var angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
            var away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            return ArenaBounds.Clamp((Vector2)_player.position + away * MinPlayerDistance, EdgeInset);
        }

        private Vector3 RandomPoint()
        {
            var inner = CentreClearance;
            var outer = ArenaBounds.Radius - EdgeInset;

            // Square-rooted so the points spread evenly over the area rather than crowding the
            // middle, which is what a uniform radius would do.
            var t = (float)_random.NextDouble();
            var radius = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, t));
            var angle = (float)(_random.NextDouble() * Mathf.PI * 2f);

            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
    }
}

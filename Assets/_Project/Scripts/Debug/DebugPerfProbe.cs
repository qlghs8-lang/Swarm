#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Swarm.Player;
using Swarm.Spawner;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Swarm.Game
{
    /// <summary>
    /// TEST ONLY — reproducible frame-cost measurement. Fills the field to a target enemy count,
    /// holds it there, then averages frame time and per-frame GC allocation over a fixed window.
    ///
    ///   F3  적 150 한 번 측정          (docs 시나리오 A)
    ///   F4  적 400 한 번 측정          (docs 시나리오 B)
    ///   F5  현재 상태 그대로 측정      (docs 시나리오 C — F2로 보스를 부른 뒤)
    ///   F6  전자동 스윕 시작/중단      (150·250·400·550·700 각 3회, 약 4분)
    ///
    /// Why a tool and not just the Profiler window: the Profiler reports what happened over a
    /// window the operator picked by hand, so two runs are never the same window. Here the enemy
    /// count, the settle time and the sample window are all fixed, so the number is comparable
    /// between runs — which is the only reason it is worth putting in a README.
    ///
    /// Installs itself at runtime and nothing in any scene references it, so deleting this file
    /// removes it completely.
    /// </summary>
    public class DebugPerfProbe : MonoBehaviour
    {
        private const string ObjectName = "DebugPerfProbe (Runtime)";

        private const int PresetLow = 150;
        private const int PresetHigh = 400;

        /// <summary>Enemy counts the F6 sweep walks through. Ascending, so each step mostly tops
        /// the field up rather than having to drain it. Reaches well past the spawner's own
        /// Max Active Enemies of 400 on purpose: the first sweep held 60 FPS at every point up to
        /// 700, so the headroom question was still unanswered at the top of the range.</summary>
        private static readonly int[] SweepTargets = { 150, 400, 700, 1100, 1600, 2200 };
        private const int RepsPerTarget = 3;

        // Long enough for the crowd to stop arriving and start pressing — the physics cost of a
        // packed field is in the contacts, and contacts only exist once the pack has closed.
        // Measuring the moment the last one spawns reads the cheapest frame of the scenario.
        private const float SettleSeconds = 3f;
        private const float MeasureSeconds = 10f;

        // Spawning hundreds in one frame produces a multi-hundred-millisecond hitch that Unity's
        // own timers then spread over the following frames. Drip-feeding keeps the fill out of the
        // measurement window entirely.
        private const int SpawnsPerFrame = 25;

        // Replacing the dead inside the window is a much smaller trickle, and is capped so a burst
        // of kills cannot turn a measurement frame into a spawn frame.
        private const int TopUpPerFrame = 8;

        private const float FillTimeoutSeconds = 30f;

        // Draining is done by simply not replacing the dead, so it moves at the player's kill
        // rate. Generous, because a low target reached from a full field is the slow direction.
        private const float DrainTimeoutSeconds = 30f;
        private const float TargetFrameMs = 1000f / 60f;

        /// <summary>
        /// Below this, per-frame allocation is not a finding — it is measurement noise. The first
        /// Development Build read 1-19 B/frame across the whole sweep; a ratio test on numbers
        /// that small is meaningless (its fitted intercept came out at -2 B), so an absolute floor
        /// decides first and the ratio only applies once there is something real to divide.
        /// </summary>
        private const float NegligibleBytesPerFrame = 256f;

        /// <summary>
        /// Above one fixed timestep per frame, per-frame numbers stop being comparable: the engine
        /// runs several physics steps inside a single frame, so anything measured "per frame"
        /// silently covers several steps' worth of work. The second sweep hit this hard — the
        /// 2200-enemy row spent 341 ms per frame, essentially the Maximum Allowed Timestep of
        /// 333 ms, and its GC-per-frame was inflated to 11-14 KB by the same multiplier.
        /// Rows past this line are still reported, but excluded from any fit.
        /// </summary>
        private static float StepClampMs => Time.fixedDeltaTime * 1000f;

        private enum Phase { Idle, Filling, Draining, Settling, Measuring }

        private struct Sample
        {
            public int Enemies;
            public float AverageFps;
            public float AverageMs;
            public float WorstFps;
            public float GcBytesPerFrame;
            public bool GcExact;
            public float SetPassCalls;
            public float Batches;
            public float DrawCalls;
            public int Collections;
            public int Frames;
            public float Seconds;
        }

        private EnemySpawner _spawner;
        private PlayerHealth _playerHealth;
        private PlayerExperience _playerExperience;
        private MethodInfo _spawnMethod;

        // Bound once from _spawnMethod. MethodInfo.Invoke allocates on every call — it boxes the
        // arguments and builds its own parameter storage — and TopUp calls it inside the
        // measurement window, so the instrument was contributing to the very number it reports.
        // A bound delegate calls straight through with no allocation at all.
        private System.Action<bool, GameObject, Vector2?> _spawn;

        private FieldInfo _spawnIntervalField;
        private FieldInfo _spawnIntervalMinField;
        private FieldInfo _baseExpToLevelField;
        private readonly object[] _spawnArgs = new object[3];

        private Phase _phase = Phase.Idle;
        private int _targetCount;
        private string _label;
        private float _phaseElapsed;

        private bool _sweepActive;
        private int _sweepTargetIndex;
        private int _sweepRep;
        private List<Sample>[] _sweepSamples;

        // Chosen when the sweep starts and rewritten after every completed rep. The first 2200
        // sweep produced no file at all: the report was only written at the very end, so leaving
        // play mode part-way through threw away every measurement taken up to that point.
        private string _sweepReportPath;

        private bool _interruptedNoticeShown;

        private bool _spawnFrozen;
        private float _savedSpawnInterval;
        private float _savedSpawnIntervalMin;

        private bool _levelUpFrozen;
        private int _savedBaseExpToLevel;

        // Far beyond anything a five-minute sweep can earn, but small enough that
        // baseExpToLevel * level cannot overflow int even at an absurd level.
        private const int FrozenExpToLevel = 1_000_000;

        private int _frames;
        private float _worstFrameSeconds;
        private long _gcAllocTotal;
        private int _enemySampleTotal;
        private double _setPassTotal;
        private double _batchesTotal;
        private double _drawCallsTotal;
        private long _managedAtStart;
        private int _collectionsAtStart;

        // ProfilerRecorder is the only one of these that reports the number the question actually
        // asks for: bytes the managed heap allocated in THIS frame. GC.GetTotalMemory reports the
        // heap's live size, so a collection inside the window makes its delta negative and
        // meaningless, and Profiler.GetTotalAllocatedMemoryLong reports Unity's NATIVE allocators
        // — meshes, textures, the physics world — which is not GC pressure at all. The recorder is
        // the primary source; GC.GetTotalMemory is kept as a fallback for the case where the
        // counter is unavailable, and is reported as an estimate with its collection count so a
        // reader can tell the two apart.
        private ProfilerRecorder _gcRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _drawCallsRecorder;
        private bool _gcRecorderValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall();
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                              UnityEngine.SceneManagement.LoadSceneMode mode)
            => TryInstall();

        private static void TryInstall()
        {
            // Only where enemies actually spawn — there is nothing to measure on the title screen.
            if (Object.FindAnyObjectByType<EnemySpawner>() == null) return;
            if (Object.FindAnyObjectByType<DebugPerfProbe>() != null) return;

            new GameObject(ObjectName, typeof(DebugPerfProbe));
        }

        private void Awake()
        {
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _playerHealth);
                player.TryGetComponent(out _playerExperience);
            }

            // The fill goes through the spawner's own SpawnEnemy rather than Instantiate, so the
            // enemies being measured are the real thing: pooled, counted in ActiveEnemyCount,
            // carrying the difficulty ramp, and swept by the recycler like every other enemy.
            // Reflection is what keeps that possible without widening the spawner's public API for
            // a test-only file that is meant to be deleted before release.
            const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            _spawnMethod = typeof(EnemySpawner).GetMethod("SpawnEnemy", Hidden);
            _spawn = BindSpawn(_spawner, _spawnMethod);
            _spawnIntervalField = typeof(EnemySpawner).GetField("spawnInterval", Hidden);
            _spawnIntervalMinField = typeof(EnemySpawner).GetField("spawnIntervalMin", Hidden);
            _baseExpToLevelField = typeof(PlayerExperience).GetField("baseExpToLevel", Hidden);
        }

        /// <summary>Binds the spawner's private SpawnEnemy to a delegate so the measurement loop
        /// can call it without the per-call allocation MethodInfo.Invoke incurs. Falls back to
        /// null (and Invoke) if the signature ever changes.</summary>
        private static System.Action<bool, GameObject, Vector2?> BindSpawn(EnemySpawner spawner, MethodInfo method)
        {
            if (spawner == null || method == null) return null;

            try
            {
                return (System.Action<bool, GameObject, Vector2?>)System.Delegate.CreateDelegate(
                    typeof(System.Action<bool, GameObject, Vector2?>), spawner, method);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("DebugPerfProbe: SpawnEnemy 델리게이트 바인딩 실패, 리플렉션 호출로 " +
                                 $"떨어집니다(측정 창에서 약간의 GC 할당 발생) — {exception.Message}");
                return null;
            }
        }

        private void OnEnable()
        {
            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcRecorderValid = _gcRecorder.Valid;

            // Best effort. These exist in development players and the editor, but a platform that
            // does not publish them just leaves the columns blank rather than failing the run.
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            // "Batches Count" came back as a flat 0 on the first real run while SetPass reported
            // fine, so Draw Calls is recorded alongside it rather than trusting either name alone.
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        private void OnDisable()
        {
            SetSpawnFrozen(false);
            SetLevelUpFrozen(false);

            if (_gcRecorder.Valid) _gcRecorder.Dispose();
            if (_setPassRecorder.Valid) _setPassRecorder.Dispose();
            if (_batchesRecorder.Valid) _batchesRecorder.Dispose();
            if (_drawCallsRecorder.Valid) _drawCallsRecorder.Dispose();
            _gcRecorderValid = false;
        }

        private void Update()
        {
            ReadKeys();

            if (_phase == Phase.Idle) return;

            // A four-minute sweep at 700 enemies is a death sentence otherwise, and a run that
            // ends in the result screen produces no number at all. Refreshed every frame rather
            // than set once so the window can never expire mid-measurement; it lapses on its own
            // the moment the probe stops asking.
            if (_playerHealth != null) _playerHealth.SetInvincible(1f);

            if (HandleInterruption()) return;

            switch (_phase)
            {
                case Phase.Filling: TickFilling(); break;
                case Phase.Draining: TickDraining(); break;
                case Phase.Settling: TickSettling(); break;
                case Phase.Measuring: TickMeasuring(); break;
            }
        }

        /// <summary>
        /// A level-up panel holds timeScale at 0, and a four-minute sweep over hundreds of kills
        /// will open several. Frames spent on a paused game are not frames of the scenario, so
        /// the step in progress is thrown away and restarted once the panel is closed rather than
        /// averaged in. Returns true while the probe is holding.
        /// </summary>
        private bool HandleInterruption()
        {
            if (Mathf.Approximately(Time.timeScale, 1f))
            {
                if (!_interruptedNoticeShown) return false;

                _interruptedNoticeShown = false;
                Debug.Log($"DebugPerfProbe: 재개 — [{_label}]를 처음부터 다시 측정합니다.");

                // Restart from the fill so the settle time is honoured again: the crowd's state
                // after a pause is not the state a settled measurement assumes.
                _phase = ChooseApproachPhase();
                _phaseElapsed = 0f;
                SetOverlayEnabled(true);
                return false;
            }

            if (!_interruptedNoticeShown)
            {
                _interruptedNoticeShown = true;
                Debug.LogWarning($"DebugPerfProbe: timeScale이 {Time.timeScale:0.##}이라 측정을 멈춥니다 " +
                                 "(레벨업 카드 창으로 보입니다). 카드를 고르면 자동으로 다시 시작합니다.");
            }

            return true;
        }

        private void ReadKeys()
        {
            if (Keyboard.current == null) return;

            // Labels match the scenario names in docs/performance-profiling.md, so a pasted
            // console line says which procedure produced it without any extra note-taking.
            if (Keyboard.current.f3Key.wasPressedThisFrame) Begin(PresetLow, "시나리오 A");
            else if (Keyboard.current.f4Key.wasPressedThisFrame) Begin(PresetHigh, "시나리오 B");
            else if (Keyboard.current.f5Key.wasPressedThisFrame) Begin(0, "시나리오 C (현재 상태)");
            else if (Keyboard.current.f6Key.wasPressedThisFrame) ToggleSweep();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 단발 측정
        // ─────────────────────────────────────────────────────────────────────────────

        /// <param name="targetCount">Enemies to hold at, or 0 to measure the field as it stands —
        /// which is how the boss scenario is measured, since the boss and its ground effects are
        /// set up by hand (F2) rather than by a count.</param>
        private bool Begin(int targetCount, string label)
        {
            if (_phase != Phase.Idle)
            {
                Debug.LogWarning("DebugPerfProbe: 측정이 이미 진행 중입니다. (F6으로 스윕 중단)");
                return false;
            }

            if (_spawner == null)
            {
                Debug.LogWarning("DebugPerfProbe: EnemySpawner를 찾지 못해 측정을 시작할 수 없습니다.");
                return false;
            }

            if (targetCount > 0 && _spawnMethod == null)
            {
                Debug.LogWarning("DebugPerfProbe: EnemySpawner.SpawnEnemy를 찾지 못했습니다. " +
                                 "이름이 바뀐 것으로 보입니다 — F5(현재 상태 측정)만 사용하세요.");
                return false;
            }

            // A measurement taken under the F1 fast-forward is not a measurement of anything: the
            // frame rate is unchanged but ten times the simulation runs inside each frame.
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Debug.LogWarning($"DebugPerfProbe: timeScale이 {Time.timeScale:0.##}이라 측정을 시작하지 않습니다. " +
                                 "F1(배속)에서 손을 떼고 레벨업 창을 닫은 뒤 다시 누르세요.");
                return false;
            }

            _targetCount = targetCount;
            _label = label;
            _phaseElapsed = 0f;

            // Holding the count at the target means suppressing the spawner's own trickle as well
            // as replacing the dead: without the freeze a 150 target drifts to ~190 over the
            // window (Game.unity spawns every 0.3s), and the row stops being a 150 measurement.
            // Enemies killed inside the window are replaced by TopUp instead, so the spawn cost
            // is still represented — the real game is spawning continuously at equilibrium too.
            if (targetCount > 0) SetSpawnFrozen(true);

            // The first sweep at these counts was interrupted by level-up panels over and over:
            // an invincible player mowing through 2200 enemies levels constantly, every panel
            // holds timeScale at 0, and every one of those throws the rep away and restarts it.
            // Freezing the requirement out of reach stops them entirely — and as a bonus the
            // weapon loadout stays fixed across the whole sweep, so the 150 row and the 2200 row
            // are measuring the same build of the player rather than drifting apart.
            SetLevelUpFrozen(true);

            _phase = ChooseApproachPhase();

            Debug.Log($"DebugPerfProbe: [{_label}] 시작 — " + _phase switch
            {
                Phase.Filling => $"{targetCount}마리까지 채우는 중",
                Phase.Draining => $"{_spawner.ActiveEnemyCount}마리 → {targetCount}마리까지 줄어들기를 기다리는 중",
                _ => "안정화 중",
            });
            return true;
        }

        /// <summary>Fill, drain, or neither — decided from where the live count sits relative to
        /// the target.</summary>
        private Phase ChooseApproachPhase()
        {
            if (_targetCount <= 0) return Phase.Settling;
            if (_spawner.ActiveEnemyCount < _targetCount) return Phase.Filling;
            if (_spawner.ActiveEnemyCount > _targetCount) return Phase.Draining;
            return Phase.Settling;
        }

        private void TickFilling()
        {
            _phaseElapsed += Time.unscaledDeltaTime;

            if (_spawner.ActiveEnemyCount >= _targetCount)
            {
                EnterSettling();
                return;
            }

            if (_phaseElapsed >= FillTimeoutSeconds)
            {
                Debug.LogWarning($"DebugPerfProbe: [{_label}] {FillTimeoutSeconds}초 안에 " +
                                 $"{_targetCount}마리에 도달하지 못했습니다(현재 {_spawner.ActiveEnemyCount}). " +
                                 "도달한 수 그대로 측정합니다.");
                EnterSettling();
                return;
            }

            // Clamped to the deficit. Spawning a flat batch overshot the target by up to a full
            // batch — the first sweep's "250" row was measured at 255 for exactly this reason.
            Spawn(Mathf.Min(SpawnsPerFrame, _targetCount - _spawner.ActiveEnemyCount));
        }

        /// <summary>
        /// Waits for a field that is FULLER than the target to come back down. Pressing F3 (150)
        /// after a few minutes of play measured 202 enemies before this existed: filling could
        /// only ever add, so a target below the live count was silently ignored and the row was
        /// mislabelled. With spawning frozen the count falls at the player's kill rate.
        /// </summary>
        private void TickDraining()
        {
            _phaseElapsed += Time.unscaledDeltaTime;

            if (_spawner.ActiveEnemyCount <= _targetCount)
            {
                EnterSettling();
                return;
            }

            if (_phaseElapsed >= DrainTimeoutSeconds)
            {
                Debug.LogWarning($"DebugPerfProbe: [{_label}] {DrainTimeoutSeconds}초 안에 " +
                                 $"{_targetCount}마리까지 줄지 않았습니다(현재 {_spawner.ActiveEnemyCount}). " +
                                 "적을 처치하는 속도가 느린 것으로 보입니다 — 현재 수 그대로 측정합니다.");
                EnterSettling();
            }
        }

        private void EnterSettling()
        {
            _phase = Phase.Settling;
            _phaseElapsed = 0f;
        }

        private void TickSettling()
        {
            _phaseElapsed += Time.unscaledDeltaTime;
            TopUp();

            if (_phaseElapsed < SettleSeconds) return;

            // The stats overlay draws through OnGUI and builds an interpolated string every frame,
            // both of which allocate. Left running it would contribute several hundred bytes a
            // frame to a number whose whole purpose is to be near zero, so it is parked for the
            // window and put back afterwards.
            SetOverlayEnabled(false);

            _phase = Phase.Measuring;
            _phaseElapsed = 0f;
            _frames = 0;
            _worstFrameSeconds = 0f;
            _gcAllocTotal = 0L;
            _enemySampleTotal = 0;
            _setPassTotal = 0d;
            _batchesTotal = 0d;
            _drawCallsTotal = 0d;
            _collectionsAtStart = System.GC.CollectionCount(0);
            _managedAtStart = System.GC.GetTotalMemory(false);
        }

        private void TickMeasuring()
        {
            _phaseElapsed += Time.unscaledDeltaTime;
            TopUp();

            _frames++;
            if (Time.unscaledDeltaTime > _worstFrameSeconds) _worstFrameSeconds = Time.unscaledDeltaTime;
            if (_gcRecorderValid) _gcAllocTotal += _gcRecorder.LastValue;
            if (_setPassRecorder.Valid) _setPassTotal += _setPassRecorder.LastValue;
            if (_batchesRecorder.Valid) _batchesTotal += _batchesRecorder.LastValue;
            if (_drawCallsRecorder.Valid) _drawCallsTotal += _drawCallsRecorder.LastValue;
            _enemySampleTotal += _spawner != null ? _spawner.ActiveEnemyCount : 0;

            if (_phaseElapsed < MeasureSeconds) return;

            var sample = BuildSample();
            LogSample(_label, sample);

            SetOverlayEnabled(true);

            if (_sweepActive) AdvanceSweep(sample);
            else EndRun();
        }

        private void EndRun()
        {
            SetSpawnFrozen(false);
            SetLevelUpFrozen(false);
            _interruptedNoticeShown = false;
            _phase = Phase.Idle;
        }

        private Sample BuildSample()
        {
            if (_frames <= 0) return default;

            var sample = new Sample
            {
                Enemies = _enemySampleTotal / _frames,
                AverageFps = _frames / _phaseElapsed,
                AverageMs = _phaseElapsed / _frames * 1000f,
                WorstFps = _worstFrameSeconds > 0f ? 1f / _worstFrameSeconds : 0f,
                SetPassCalls = (float)(_setPassTotal / _frames),
                Batches = (float)(_batchesTotal / _frames),
                DrawCalls = (float)(_drawCallsTotal / _frames),
                Collections = System.GC.CollectionCount(0) - _collectionsAtStart,
                Frames = _frames,
                Seconds = _phaseElapsed,
                GcExact = _gcRecorderValid,
            };

            sample.GcBytesPerFrame = _gcRecorderValid
                ? _gcAllocTotal / (float)_frames
                // Without the counter all that is left is the heap delta, which undercounts by
                // whatever was collected during the window — hence Collections beside it.
                : (System.GC.GetTotalMemory(false) - _managedAtStart) / (float)_frames;

            return sample;
        }

        private static void LogSample(string label, Sample s)
        {
            if (s.Frames <= 0)
            {
                Debug.LogWarning($"DebugPerfProbe: [{label}] 표본이 없어 결과를 낼 수 없습니다.");
                return;
            }

            var alloc = s.GcExact
                ? $"GC {s.GcBytesPerFrame:0} B/frame"
                : $"GC ~{s.GcBytesPerFrame:0} B/frame (추정, GC {s.Collections}회)";

            // One line, because this is what gets copied into the README.
            Debug.Log($"[PERF] {label} | 적 {s.Enemies} | 평균 {s.AverageFps:0.0} FPS ({s.AverageMs:0.00} ms) " +
                      $"| 최저 {s.WorstFps:0.0} FPS | {alloc} " +
                      $"| SetPass {s.SetPassCalls:0} · Draw {s.DrawCalls:0} " +
                      $"| {s.Frames}프레임 / {s.Seconds:0.0}초 " +
                      $"| vSync {QualitySettings.vSyncCount} · {(Application.isEditor ? "Editor" : "Player")}");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 전자동 스윕 (F6)
        // ─────────────────────────────────────────────────────────────────────────────

        private void ToggleSweep()
        {
            if (_sweepActive)
            {
                Debug.LogWarning("DebugPerfProbe: 스윕을 중단합니다. 여기까지의 결과만 저장합니다.");
                FinishSweep(aborted: true);
                return;
            }

            if (_phase != Phase.Idle)
            {
                Debug.LogWarning("DebugPerfProbe: 단발 측정이 진행 중이라 스윕을 시작할 수 없습니다.");
                return;
            }

            _sweepSamples = new List<Sample>[SweepTargets.Length];
            for (var i = 0; i < _sweepSamples.Length; i++) _sweepSamples[i] = new List<Sample>(RepsPerTarget);

            _sweepTargetIndex = 0;
            _sweepRep = 0;
            _sweepActive = true;
            _sweepReportPath = NewReportPath();

            var perRun = SettleSeconds + MeasureSeconds;
            var estimate = SweepTargets.Length * RepsPerTarget * perRun;
            Debug.Log($"DebugPerfProbe: 스윕 시작 — {SweepTargets.Length}개 지점 × {RepsPerTarget}회, " +
                      $"약 {estimate / 60f:0.0}분. 측정 중에는 조작하지 마세요. F6으로 중단.");

            if (!BeginSweepStep()) FinishSweep(aborted: true);
        }

        private bool BeginSweepStep()
        {
            var target = SweepTargets[_sweepTargetIndex];
            return Begin(target, $"스윕 {target}마리 ({_sweepRep + 1}/{RepsPerTarget})");
        }

        private void AdvanceSweep(Sample sample)
        {
            _sweepSamples[_sweepTargetIndex].Add(sample);

            // Persisted here rather than only at the end, so the file on disk always reflects
            // everything measured so far. Costs one file write per 13 seconds, outside the
            // measurement window.
            PersistSweep(finished: false);

            _sweepRep++;
            if (_sweepRep >= RepsPerTarget)
            {
                _sweepRep = 0;
                _sweepTargetIndex++;
            }

            if (_sweepTargetIndex >= SweepTargets.Length)
            {
                FinishSweep(aborted: false);
                return;
            }

            // Straight back into the next fill without dropping the spawn freeze — the targets
            // ascend, so the field only ever needs topping up.
            _phaseElapsed = 0f;
            _targetCount = SweepTargets[_sweepTargetIndex];
            _label = $"스윕 {_targetCount}마리 ({_sweepRep + 1}/{RepsPerTarget})";
            _phase = ChooseApproachPhase();
        }

        private void FinishSweep(bool aborted)
        {
            _sweepActive = false;
            SetOverlayEnabled(true);
            EndRun();

            var report = BuildReport(finished: !aborted);
            Debug.Log(report.Summary);

            if (WriteReport(_sweepReportPath, report.Markdown))
            {
                Debug.Log($"DebugPerfProbe: 결과를 저장했습니다 →\n{_sweepReportPath}");
            }
        }

        private void PersistSweep(bool finished)
        {
            if (_sweepReportPath == null) return;

            WriteReport(_sweepReportPath, BuildReport(finished).Markdown);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 스폰 제어
        // ─────────────────────────────────────────────────────────────────────────────

        private void Spawn(int count)
        {
            if (count <= 0) return;

            // ignoreCap so the probe can be pointed above the spawner's Max Active Enemies without
            // editing a serialized value that a running play session would throw away anyway.
            if (_spawn != null)
            {
                for (var i = 0; i < count; i++) _spawn(true, null, null);
                return;
            }

            if (_spawnMethod == null) return;

            _spawnArgs[0] = true;
            _spawnArgs[1] = null;
            _spawnArgs[2] = null;

            for (var i = 0; i < count; i++) _spawnMethod.Invoke(_spawner, _spawnArgs);
        }

        /// <summary>Replaces enemies killed inside the settle/measure window so the count the row
        /// is labelled with is the count that was actually on screen.</summary>
        private void TopUp()
        {
            if (_targetCount <= 0 || _spawner == null) return;

            var deficit = _targetCount - _spawner.ActiveEnemyCount;
            if (deficit <= 0) return;

            Spawn(Mathf.Min(TopUpPerFrame, deficit));
        }

        /// <summary>Suppresses the spawner's own timed trickle by pushing its interval out of
        /// reach, and puts the serialized values back afterwards. In the editor a play-mode write
        /// to a serialized field is discarded when play stops anyway, so a crash mid-sweep cannot
        /// leave the asset modified.</summary>
        private void SetSpawnFrozen(bool frozen)
        {
            if (frozen == _spawnFrozen) return;
            if (_spawner == null) return;
            if (_spawnIntervalField == null || _spawnIntervalMinField == null)
            {
                if (frozen)
                {
                    Debug.LogWarning("DebugPerfProbe: spawnInterval 필드를 찾지 못해 스포너 트리클을 " +
                                     "멈추지 못했습니다. 낮은 목표치에서 적 수가 위로 흐를 수 있습니다.");
                }
                return;
            }

            if (frozen)
            {
                _savedSpawnInterval = (float)_spawnIntervalField.GetValue(_spawner);
                _savedSpawnIntervalMin = (float)_spawnIntervalMinField.GetValue(_spawner);
                _spawnIntervalField.SetValue(_spawner, 9999f);
                _spawnIntervalMinField.SetValue(_spawner, 9999f);
            }
            else
            {
                _spawnIntervalField.SetValue(_spawner, _savedSpawnInterval);
                _spawnIntervalMinField.SetValue(_spawner, _savedSpawnIntervalMin);
            }

            _spawnFrozen = frozen;
        }

        /// <summary>Pushes the level-up requirement out of reach so no card panel opens mid-run,
        /// and restores the serialized value afterwards. Same technique, and the same play-mode
        /// safety, as <see cref="SetSpawnFrozen"/>.</summary>
        private void SetLevelUpFrozen(bool frozen)
        {
            if (frozen == _levelUpFrozen) return;
            if (_playerExperience == null || _baseExpToLevelField == null)
            {
                if (frozen)
                {
                    Debug.LogWarning("DebugPerfProbe: PlayerExperience.baseExpToLevel을 찾지 못해 " +
                                     "레벨업을 막지 못했습니다. 측정 중 카드 창이 뜨면 해당 회차는 " +
                                     "버리고 다시 측정합니다.");
                }
                return;
            }

            if (frozen)
            {
                _savedBaseExpToLevel = (int)_baseExpToLevelField.GetValue(_playerExperience);
                _baseExpToLevelField.SetValue(_playerExperience, FrozenExpToLevel);
            }
            else
            {
                _baseExpToLevelField.SetValue(_playerExperience, _savedBaseExpToLevel);
            }

            _levelUpFrozen = frozen;
        }

        private static void SetOverlayEnabled(bool enabled)
        {
            var overlay = Object.FindAnyObjectByType<DebugStatsOverlay>();
            if (overlay != null) overlay.enabled = enabled;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 리포트
        // ─────────────────────────────────────────────────────────────────────────────

        private struct Report
        {
            public string Summary;
            public string Markdown;
        }

        private Report BuildReport(bool finished)
        {
            var medians = new List<Sample>();
            var collectionTotals = new List<int>();
            for (var i = 0; i < SweepTargets.Length; i++)
            {
                if (_sweepSamples[i].Count == 0) continue;

                medians.Add(Median(_sweepSamples[i]));

                var collections = 0;
                for (var r = 0; r < _sweepSamples[i].Count; r++) collections += _sweepSamples[i][r].Collections;
                collectionTotals.Add(collections);
            }

            var crossing = FindSixtyFpsCrossing(medians);

            var md = new StringBuilder(4096);
            md.AppendLine("# 성능 실측 결과");
            md.AppendLine();
            md.AppendLine($"측정 시각: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            md.AppendLine();
            if (!finished)
            {
                var done = 0;
                for (var i = 0; i < _sweepSamples.Length; i++) done += _sweepSamples[i].Count;
                md.AppendLine($"> ⏳ **진행 중 / 중단됨** — {SweepTargets.Length * RepsPerTarget}회 중 {done}회 완료. " +
                              "이 파일은 회차가 끝날 때마다 갱신되므로, 여기까지의 값은 모두 유효합니다.");
                md.AppendLine();
            }

            md.AppendLine("## 환경");
            md.AppendLine();
            md.AppendLine("| 항목 | 값 |");
            md.AppendLine("| --- | --- |");
            AppendRow(md, "CPU", SystemInfo.processorType + $" ({SystemInfo.processorCount} threads)");
            AppendRow(md, "GPU", $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType})");
            AppendRow(md, "RAM", $"{SystemInfo.systemMemorySize} MB");
            AppendRow(md, "OS", SystemInfo.operatingSystem);
            AppendRow(md, "Unity", Application.unityVersion);
            AppendRow(md, "Scripting Backend", ScriptingBackend);
            AppendRow(md, "빌드", Application.isEditor ? "Editor (README에 쓰지 말 것)" : $"Player · Development={Debug.isDebugBuild}");
            AppendRow(md, "해상도", $"{Screen.width}×{Screen.height} {(Screen.fullScreen ? "Fullscreen" : "Windowed")} @ {Screen.currentResolution.refreshRateRatio.value:0}Hz");
            AppendRow(md, "품질 레벨", QualityLevelName);
            AppendRow(md, "vSync", QualitySettings.vSyncCount == 0 ? "Off (0)" : $"**On ({QualitySettings.vSyncCount}) — 측정 무효**");
            AppendRow(md, "targetFrameRate", Application.targetFrameRate.ToString());
            AppendRow(md, "fixedDeltaTime", $"{Time.fixedDeltaTime:0.####}s");
            AppendRow(md, "측정 방법", $"안정화 {SettleSeconds:0}초 후 {MeasureSeconds:0}초 평균, {RepsPerTarget}회 중앙값");
            AppendRow(md, "GC 계측", _gcRecorderValid ? "ProfilerRecorder \"GC Allocated In Frame\" (정확)" : "GC.GetTotalMemory 델타 (추정)");
            md.AppendLine();

            md.AppendLine("## 스케일링 — 적 수 대비 프레임 비용");
            md.AppendLine();
            // Counters that reported a flat zero everywhere are not data — they are a counter
            // name this platform does not publish. Omitted rather than printed as a column of 0s.
            var showDraw = medians.Exists(s => s.DrawCalls > 0f);
            var showBatches = medians.Exists(s => s.Batches > 0f);
            var anyClamped = false;

            md.Append("| 적 수 | 평균 ms | 평균 FPS | 최저 FPS | GC B/frame | GC 수집(합) | SetPass");
            if (showDraw) md.Append(" | Draw");
            if (showBatches) md.Append(" | Batches");
            md.AppendLine(" |");

            md.Append("| --- | --- | --- | --- | --- | --- | ---");
            if (showDraw) md.Append(" | ---");
            if (showBatches) md.Append(" | ---");
            md.AppendLine(" |");

            for (var i = 0; i < medians.Count; i++)
            {
                var s = medians[i];
                var clamped = s.AverageMs > StepClampMs;
                anyClamped |= clamped;

                md.Append($"| {s.Enemies}{(clamped ? " ⚠️" : string.Empty)} | {s.AverageMs:0.00} " +
                          $"| {s.AverageFps:0.0} | {s.WorstFps:0.0} " +
                          $"| {s.GcBytesPerFrame:0} | {collectionTotals[i]} | {s.SetPassCalls:0}");
                if (showDraw) md.Append($" | {s.DrawCalls:0}");
                if (showBatches) md.Append($" | {s.Batches:0}");
                md.AppendLine(" |");
            }
            md.AppendLine();

            if (anyClamped)
            {
                md.AppendLine($"⚠️ = 프레임 시간이 fixedDeltaTime({StepClampMs:0} ms)를 넘어 " +
                              "**한 프레임에 물리 스텝이 여러 번** 돈 구간. 이 행의 \"프레임당\" 수치는 " +
                              "여러 스텝 분량을 합친 값이라 위 행들과 같은 의미가 아니며, 아래 적합에서 제외된다.");
                md.AppendLine();
            }
            md.AppendLine(crossing.Text);
            md.AppendLine();
            md.AppendLine(AllocSplit(medians));
            md.AppendLine();

            md.AppendLine("## 회차별 원본");
            md.AppendLine();
            md.AppendLine("최저 FPS는 GC 수집이 일어난 회차에서 크게 떨어진다. 두 열을 나란히 보라.");
            md.AppendLine();
            md.AppendLine("| 적 수 | 회차 | 평균 ms | 평균 FPS | 최저 FPS | GC B/frame | GC 수집 | 프레임 |");
            md.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            for (var i = 0; i < SweepTargets.Length; i++)
            {
                for (var r = 0; r < _sweepSamples[i].Count; r++)
                {
                    var s = _sweepSamples[i][r];
                    md.AppendLine($"| {s.Enemies} | {r + 1} | {s.AverageMs:0.00} | {s.AverageFps:0.0} " +
                                  $"| {s.WorstFps:0.0} | {s.GcBytesPerFrame:0} | {s.Collections} | {s.Frames} |");
                }
            }
            md.AppendLine();

            md.AppendLine("## 참고");
            md.AppendLine();
            md.AppendLine("- 측정 중 플레이어는 무적 상태였습니다(긴 스윕이 사망으로 끊기지 않게).");
            // Read from the saved value, not from _levelUpFrozen: the final report is built after
            // EndRun has already restored it.
            var levelUpNote = _savedBaseExpToLevel > 0 ? "적용됨" : "적용 실패";
            md.AppendLine($"- 측정 중 레벨업을 막았습니다({levelUpNote}). 카드 창이 뜨지 않고, " +
                          "무기 구성이 스윕 내내 고정되므로 각 행이 서로 비교 가능합니다.");
            md.AppendLine("- 목표치를 유지하기 위해 스포너의 자체 스폰 주기를 잠시 늘리고, 죽은 만큼은 프로브가 채웠습니다.");
            md.AppendLine("- `DebugStatsOverlay`는 측정 창 동안 꺼져 있었습니다(OnGUI 할당이 GC 수치를 오염시키므로).");
            md.AppendLine("- 레벨업 카드 창(timeScale 0)이 열리면 해당 회차는 버리고 카드 선택 후 처음부터 다시 측정했습니다.");
            md.AppendLine("- 병목의 소재(`Physics2D.Simulate` 비중 등)는 여전히 Profiler 창에서 읽어야 합니다 — docs/performance-profiling.md §4.1.");

            var summary = new StringBuilder(512);
            summary.AppendLine("[PERF SWEEP] 완료");
            foreach (var s in medians)
            {
                summary.AppendLine($"  적 {s.Enemies,4} | {s.AverageMs,6:0.00} ms | {s.AverageFps,6:0.0} FPS " +
                                   $"| 최저 {s.WorstFps,6:0.0} | GC {s.GcBytesPerFrame,6:0} B/frame");
            }
            summary.Append("  → ").Append(crossing.Text);

            return new Report { Summary = summary.ToString(), Markdown = md.ToString() };
        }

        private static void AppendRow(StringBuilder md, string key, string value)
            => md.Append("| ").Append(key).Append(" | ").Append(value).AppendLine(" |");

        private static string ScriptingBackend
        {
            get
            {
#if ENABLE_IL2CPP
                return "IL2CPP";
#elif ENABLE_MONO
                return "Mono";
#else
                return "Unknown";
#endif
            }
        }

        private static string QualityLevelName
        {
            get
            {
                var names = QualitySettings.names;
                var level = QualitySettings.GetQualityLevel();
                return level >= 0 && level < names.Length ? $"{names[level]} ({level})" : level.ToString();
            }
        }

        private static Sample Median(List<Sample> samples)
        {
            // Per-metric median rather than "the median run": with three samples this is the
            // middle value of each column, which is what a reader of the table expects and is
            // robust against one outlier frame in one column.
            var median = samples[0];
            median.AverageMs = MedianOf(samples, s => s.AverageMs);
            median.AverageFps = MedianOf(samples, s => s.AverageFps);
            median.WorstFps = MedianOf(samples, s => s.WorstFps);
            median.GcBytesPerFrame = MedianOf(samples, s => s.GcBytesPerFrame);
            median.SetPassCalls = MedianOf(samples, s => s.SetPassCalls);
            median.Batches = MedianOf(samples, s => s.Batches);
            median.Enemies = Mathf.RoundToInt(MedianOf(samples, s => s.Enemies));
            return median;
        }

        private static readonly List<float> MedianBuffer = new();

        private static float MedianOf(List<Sample> samples, System.Func<Sample, float> selector)
        {
            MedianBuffer.Clear();
            for (var i = 0; i < samples.Count; i++) MedianBuffer.Add(selector(samples[i]));
            MedianBuffer.Sort();

            var count = MedianBuffer.Count;
            if (count == 0) return 0f;
            return count % 2 == 1
                ? MedianBuffer[count / 2]
                : (MedianBuffer[count / 2 - 1] + MedianBuffer[count / 2]) * 0.5f;
        }

        /// <summary>
        /// Splits per-frame GC allocation into the part that scales with enemy count and the part
        /// that does not, by least-squares fitting bytes against enemies across the sweep.
        ///
        /// This is the measurement that actually answers "is my game allocating?". The first real
        /// sweep read ~10 KB/frame at every single point from 150 to 701 enemies — a 4.7x change
        /// in the game's content moved the number by 1.3%. A cost that does not move when the
        /// game moves is not the game: it is a fixed floor (in the editor, the editor itself).
        /// The slope is the only part this project's gameplay is provably responsible for.
        /// </summary>
        private static string AllocSplit(List<Sample> allMedians)
        {
            // Clamped rows would dominate the fit while measuring the timestep clamp rather than
            // the game: the 2200-enemy row read 11-14 KB/frame purely because one frame covered
            // sixteen physics steps. Fitting with it flipped the verdict from "not gameplay" to
            // "gameplay" on an artifact.
            var medians = allMedians.FindAll(s => s.AverageMs <= StepClampMs);
            var dropped = allMedians.Count - medians.Count;

            if (medians.Count < 2) return string.Empty;

            double sumX = 0d, sumY = 0d, sumXY = 0d, sumXX = 0d;
            foreach (var s in medians)
            {
                sumX += s.Enemies;
                sumY += s.GcBytesPerFrame;
                sumXY += (double)s.Enemies * s.GcBytesPerFrame;
                sumXX += (double)s.Enemies * s.Enemies;
            }

            var n = medians.Count;
            var denominator = n * sumXX - sumX * sumX;
            if (System.Math.Abs(denominator) < 1e-6) return string.Empty;

            var slope = (n * sumXY - sumX * sumY) / denominator;
            var intercept = (sumY - slope * sumX) / n;
            var atTop = medians[medians.Count - 1];
            var scaling = slope * atTop.Enemies;

            var peak = 0f;
            foreach (var s in medians) peak = Mathf.Max(peak, s.GcBytesPerFrame);

            string verdict;
            if (peak < NegligibleBytesPerFrame)
            {
                // The whole sweep is under the floor: nothing to attribute, and the fitted
                // coefficients are noise around zero. Say so plainly instead of dividing them.
                verdict = $"→ 전 구간 최대 **{peak:0} B/frame**. 프레임당 GC 할당이 사실상 **0**이다. " +
                          "적합 계수는 0 주변의 노이즈이므로 해석하지 말 것.";
            }
            else if (slope <= 0d || scaling < NegligibleBytesPerFrame)
            {
                verdict = "→ 적 수에 따라 움직이는 몫이 측정 노이즈 수준이다. **이 할당은 게임플레이가 아니다.**";
            }
            else if (System.Math.Abs(intercept) >= NegligibleBytesPerFrame &&
                     System.Math.Abs(scaling) < 0.05d * System.Math.Abs(intercept))
            {
                verdict = "→ 적 수에 따라 움직이는 몫이 고정분의 5% 미만이다. **이 할당은 게임플레이가 아니다** — " +
                          "에디터라면 에디터 오버헤드, 빌드라면 매 프레임 도는 고정 시스템을 의심하라.";
            }
            else
            {
                verdict = "→ 적 수에 비례하는 몫이 유의미하다. 게임플레이 경로에 프레임당 할당이 있다.";
            }

            var note = dropped > 0
                ? $" *(물리 스텝 클램프 구간 {dropped}개 지점 제외)*"
                : string.Empty;

            return "**GC 할당 분해** (최소자승 적합)" + note + ": " +
                   $"고정분 ≈ **{intercept:0} B/frame** + 적 1마리당 ≈ **{slope:0.00} B/frame** " +
                   $"(최대 지점 {atTop.Enemies}마리에서 적 기여분 ≈ {scaling:0} B/frame)\n\n" + verdict;
        }

        private struct Crossing
        {
            public string Text;
        }

        /// <summary>Where the curve crosses 16.67 ms, linearly interpolated between the two
        /// measured points that straddle it. This is the sentence the whole sweep exists to
        /// produce: an absolute FPS number means nothing without the reader's hardware, but
        /// "holds 60 FPS up to N enemies" is a claim about the design.</summary>
        private static Crossing FindSixtyFpsCrossing(List<Sample> medians)
        {
            if (medians.Count == 0) return new Crossing { Text = "측정된 지점이 없습니다." };

            for (var i = 0; i < medians.Count; i++)
            {
                if (medians[i].AverageMs < TargetFrameMs) continue;

                if (i == 0)
                {
                    return new Crossing
                    {
                        Text = $"**가장 낮은 측정 지점({medians[0].Enemies}마리)에서 이미 60 FPS 미만입니다** " +
                               $"({medians[0].AverageMs:0.00} ms). 더 낮은 지점을 측정해야 합니다.",
                    };
                }

                var lo = medians[i - 1];
                var hi = medians[i];
                var t = Mathf.InverseLerp(lo.AverageMs, hi.AverageMs, TargetFrameMs);
                var enemies = Mathf.RoundToInt(Mathf.Lerp(lo.Enemies, hi.Enemies, t));

                // The interval that straddles 60 FPS also straddles the point where the frame
                // stops fitting in one fixed timestep, and the curve bends sharply there. A
                // straight line across it is a rough read, not a measurement.
                var coarse = hi.AverageMs > StepClampMs
                    ? $"\n\n다만 이 구간({lo.Enemies}~{hi.Enemies})은 프레임이 fixedDeltaTime을 넘어서는 " +
                      "지점을 포함해 곡선이 급격히 꺾인다. 정확한 한계를 원하면 이 사이에 측정 지점을 더 넣을 것."
                    : string.Empty;

                return new Crossing
                {
                    Text = $"**적 약 {enemies}마리까지 60 FPS를 유지합니다** " +
                           $"({lo.Enemies}마리 {lo.AverageMs:0.00} ms → {hi.Enemies}마리 {hi.AverageMs:0.00} ms 사이 보간)." +
                           coarse,
                };
            }

            var top = medians[medians.Count - 1];
            return new Crossing
            {
                Text = $"**측정 범위 전체({top.Enemies}마리까지)에서 60 FPS를 유지합니다** " +
                       $"(최대 지점 {top.AverageMs:0.00} ms). 한계점을 찾으려면 SweepTargets를 더 늘리세요.",
            };
        }

        /// <summary>In the editor the report lands beside the project so it can be opened and
        /// pasted without hunting through the persistent data path.</summary>
        private static string NewReportPath()
        {
            var root = Application.isEditor
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.persistentDataPath;

            return Path.Combine(root, "PerfReports", $"perf_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
        }

        private static bool WriteReport(string path, string markdown)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, markdown, Encoding.UTF8);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"DebugPerfProbe: 결과 파일 저장 실패 — {exception.Message}\n" +
                                 "콘솔의 [PERF SWEEP] 요약을 대신 사용하세요.");
                return false;
            }
        }
    }
}
#endif

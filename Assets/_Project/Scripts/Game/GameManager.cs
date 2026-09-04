using Swarm.Audio;
using Swarm.Enemy;
using Swarm.Player;
using Swarm.Spawner;
using Swarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class GameManager : MonoBehaviour
    {
        private const string SelectedCharacterKey = "Swarm_SelectedCharacterId";

        [SerializeField] private Text timerText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultTimeText;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private PassiveUpgrade[] passiveUpgrades;
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private BossHealthBarUI bossHealthBarUI;
        [SerializeField] private WaveAnnouncementUI waveAnnouncementUI;

        // The run is designed as ten minutes to the boss plus a couple of minutes to kill it.
        // Without a hard stop, refusing to engage the boss is a legal way to farm forever, and the
        // spawner has been at its rate cap since 08:00 — so the longer that goes on, the further
        // the run drifts from anything that was balanced or tested.
        [SerializeField] private float timeLimit = 900f;

        /// <summary>How long before the limit the countdown warning fires.</summary>
        [SerializeField] private float timeLimitWarning = 60f;

        private float _elapsedTime;
        private bool _isGameEnded;
        private PlayerHealth _playerHealth;
        private EnemyHealth _boss;
        private bool _warnedOfTimeLimit;
        private ResultSequence _resultSequence;
        private int _goldAtRunStart;

        // The timer only ever shows whole seconds, but it was rebuilt from a fresh interpolated
        // string on every frame — sixty throwaway allocations a second for one visible change.
        private int _lastShownSecond = -1;
        private bool _lastShownWasCountdown;

        private void Awake()
        {
            // Added rather than wired: the sequence needs no references of its own, so a scene
            // that predates it still gets the presentation without being touched.
            _resultSequence = GetComponent<ResultSequence>();
            if (_resultSequence == null) _resultSequence = gameObject.AddComponent<ResultSequence>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out _playerHealth))
            {
                _playerHealth.OnDied += HandleGameOver;
            }

            if (enemySpawner != null)
            {
                enemySpawner.OnBossSpawned += HandleBossSpawned;
                enemySpawner.OnWaveTriggered += HandleWaveTriggered;
            }
        }

        private void Start()
        {
            RunStats.Reset();
            // The wallet is a lifetime total that persists across runs, so what this run earned is
            // only knowable as a difference against where it started.
            _goldAtRunStart = GoldWallet.Current;
            BackgroundMusic.SetPitch(1f);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");
            CharacterDefinition character = System.Array.Find(characters, c => c.Id == selectedId) ??
                                             (characters.Length > 0 ? characters[0] : null);
            character?.ApplyToPlayer(player);

            foreach (var upgrade in passiveUpgrades)
            {
                if (upgrade != null)
                {
                    upgrade.ApplyToPlayer(player);
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied -= HandleGameOver;
            }

            if (enemySpawner != null)
            {
                enemySpawner.OnBossSpawned -= HandleBossSpawned;
                enemySpawner.OnWaveTriggered -= HandleWaveTriggered;
            }

            if (_boss != null)
            {
                _boss.OnDied -= HandleClear;
            }
        }

        private void Update()
        {
            if (_isGameEnded) return;

            _elapsedTime += Time.deltaTime;

            if (timeLimit <= 0f)
            {
                SetTimerText(_elapsedTime, false);
                return;
            }

            var remaining = timeLimit - _elapsedTime;

            // Once the boss is out, the clock the player cares about is the one they are running
            // out of, not the one they have survived. Before that the limit is far enough away to
            // be noise, and counting down from 15:00 for ten minutes would just read as pressure
            // that isn't there yet.
            if (_boss != null) SetTimerText(remaining, true);
            else SetTimerText(_elapsedTime, false);

            if (!_warnedOfTimeLimit && timeLimitWarning > 0f && remaining <= timeLimitWarning)
            {
                _warnedOfTimeLimit = true;
                waveAnnouncementUI?.Show($"남은 시간 {FormatTime(Mathf.Max(0f, remaining))}");
            }

            if (remaining <= 0f)
            {
                // A time-out is a loss, not a clear: the boss is the win condition, and surviving
                // past it without killing it is exactly the case this guards against.
                HandleGameOver();
            }
        }

        private void HandleBossSpawned(EnemyHealth boss)
        {
            _boss = boss;
            bossHealthBarUI?.Bind(boss);
            boss.OnDied += HandleClear;
            waveAnnouncementUI?.Show("BOSS");
        }

        private void HandleWaveTriggered(string label)
        {
            waveAnnouncementUI?.Show(label);
        }

        private void HandleGameOver()
        {
            ShowResult(false, 0);
        }

        private void HandleClear()
        {
            // EnemyHealth.Die() has already credited the wallet; this only reports it. Destroy is
            // deferred to the end of the frame, so the boss reference is still readable here.
            ShowResult(true, _boss != null ? _boss.GoldReward : 0);
        }

        private void ShowResult(bool cleared, int goldBonus)
        {
            if (_isGameEnded) return;
            _isGameEnded = true;
            RunStats.MarkRunEnded();

            // The clear sequence hands time back so the arena can empty out on screen, which also
            // means the spawner would keep feeding it. Nothing should arrive after the run ends.
            if (enemySpawner != null) enemySpawner.enabled = false;

            if (_resultSequence != null)
            {
                _resultSequence.Play(cleared, _elapsedTime, _goldAtRunStart, goldBonus,
                                     resultPanel, resultTitleText, resultTimeText);
                return;
            }

            // Fallback for a scene where the sequence could not be attached: the old instant panel.
            resultTitleText.text = cleared ? "CLEAR!" : "GAME OVER";
            resultTimeText.text = goldBonus > 0
                ? $"생존 시간: {FormatTime(_elapsedTime)}\n클리어 보너스  +{goldBonus} G"
                : $"생존 시간: {FormatTime(_elapsedTime)}";
            resultPanel.SetActive(true);
            Time.timeScale = 0f;
        }

        public void Restart()
        {
            EndSequence();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToTitle()
        {
            EndSequence();
            SceneManager.LoadScene(titleSceneName);
        }

        /// <summary>
        /// Hands back everything the sequence borrowed. fixedDeltaTime, the BGM pitch and the
        /// camera's brain all outlive the scene load, so leaving any of them bent would carry the
        /// death screen's slow motion into the next run.
        /// </summary>
        private void EndSequence()
        {
            Time.timeScale = 1f;
            if (_resultSequence != null) _resultSequence.Cleanup();
        }

        /// <summary>Writes the clock only when the displayed second (or the mode it is counting
        /// in) actually changes.</summary>
        private void SetTimerText(float seconds, bool isCountdown)
        {
            if (timerText == null) return;

            var second = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            if (second == _lastShownSecond && isCountdown == _lastShownWasCountdown) return;

            _lastShownSecond = second;
            _lastShownWasCountdown = isCountdown;
            timerText.text = FormatTime(seconds);
        }

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }
    }
}
